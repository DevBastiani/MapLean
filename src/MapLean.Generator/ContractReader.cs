using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MapLean.Generator;

internal static class ContractReader
{
    public static ContractResult Read(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetNode is not MethodDeclarationSyntax methodSyntax || ctx.TargetSymbol is not IMethodSymbol method)
            return Bad(ctx.TargetNode, MapDiagnostics.ToTarget);

        var type = method.ContainingType;
        if (type.ContainingType is not null)
            return Bad(methodSyntax.Identifier, MapDiagnostics.Nested, type.Name, type.ContainingType.Name);

        if (type.TypeKind != TypeKind.Class || type.IsRecord)
            return Bad(methodSyntax.Identifier, MapDiagnostics.TypeKind, type.Name, KindName(type));

        if (methodSyntax.Parent is TypeDeclarationSyntax declaring && !declaring.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)))
            return Bad(methodSyntax.Identifier, MapDiagnostics.TypeKind, type.Name, "class without partial");

        if (!Supported(type.DeclaredAccessibility))
            return Bad(methodSyntax.Identifier, MapDiagnostics.Access, type.Name);

        if (!Supported(method.DeclaredAccessibility))
            return Bad(methodSyntax.Identifier, MapDiagnostics.Access, method.Name);

        if (!methodSyntax.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)))
            return Bad(methodSyntax.Identifier, MapDiagnostics.Partial, method.Name);

        if (method.Parameters.Length != 1 || method.ReturnsVoid || method.Arity != 0 || method.RefKind != RefKind.None ||
            method.Parameters[0].RefKind is not (RefKind.None or RefKind.In))
            return Bad(methodSyntax.Identifier, MapDiagnostics.Signature, method.Name);

        var parameter = method.Parameters[0];
        var parameterName = Escape(parameter.Name);
        var locals = 0;
        var warnings = new List<Diag>();
        if (!TryPlan(parameter.Type, method.ReturnType, parameterName, method.Name, new List<(ITypeSymbol, ITypeSymbol)>(), ref locals, warnings, out var plan, out var fault))
            return Bad(methodSyntax.Identifier, fault);

        var ns = method.ContainingNamespace is null || method.ContainingNamespace.IsGlobalNamespace
            ? null
            : method.ContainingNamespace.ToDisplayString();
        var contract = new MapContract(
            ns,
            Access(type.DeclaredAccessibility),
            type.IsStatic,
            type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            Access(method.DeclaredAccessibility),
            method.IsStatic,
            method.Name,
            Display(parameter.Type),
            Display(method.ReturnType),
            (methodSyntax.ParameterList.Parameters[0].Modifiers.Any(static m => m.IsKind(SyntaxKind.ThisKeyword)) ? "this " : "") + (parameter.RefKind == RefKind.In ? "in " : ""),
            parameterName,
            plan);
        var hint = HintName(type, method, parameter);
        return new ContractResult.Ok(contract, hint, new EquatableArray<Diag>(warnings));
    }

    private static bool TryPlan(
        ITypeSymbol source,
        ITypeSymbol dest,
        string sourceExpr,
        string member,
        List<(ITypeSymbol Source, ITypeSymbol Dest)> path,
        ref int locals,
        List<Diag> warnings,
        out Plan plan,
        out Fault fault)
    {
        plan = null!;
        fault = default;
        var nullable = source.NullableAnnotation == NullableAnnotation.Annotated || IsNullableValue(source);
        if (!nullable)
            return TryCore(source, dest, sourceExpr, member, path, ref locals, warnings, out plan, out fault);

        if (dest.NullableAnnotation == NullableAnnotation.NotAnnotated || (!dest.IsReferenceType && !IsNullableValue(dest)))
        {
            fault = Cannot(source, dest, sourceExpr);
            return false;
        }

        if (!IsList(source, out _) && SymbolEqualityComparer.Default.Equals(source, dest))
        {
            plan = new Plan.Copy(sourceExpr);
            return true;
        }

        var local = "ml" + locals++;
        var inner = IsNullableValue(source) ? local + ".Value" : local;
        if (!TryCore(Unwrap(source), Unwrap(dest), inner, member, path, ref locals, warnings, out var core, out fault))
            return false;
        var optional = dest.IsReferenceType && dest.NullableAnnotation != NullableAnnotation.Annotated ? "?" : "";
        plan = new Plan.Guard(sourceExpr, local, core, Display(dest) + optional, !dest.IsReferenceType);
        return true;
    }

    private static bool TryCore(
        ITypeSymbol source,
        ITypeSymbol dest,
        string sourceExpr,
        string member,
        List<(ITypeSymbol Source, ITypeSymbol Dest)> path,
        ref int locals,
        List<Diag> warnings,
        out Plan plan,
        out Fault fault)
    {
        plan = null!;
        fault = default;
        if (IsList(source, out var sourceElem) && IsList(dest, out var destElem))
        {
            var local = "ml" + locals++;
            if (!TryPlan(sourceElem, destElem, local + "Item", member, path, ref locals, warnings, out var element, out fault))
                return false;
            plan = new Plan.List(sourceExpr, local, Display(destElem), element);
            return true;
        }

        if (SymbolEqualityComparer.Default.Equals(source, dest))
        {
            plan = new Plan.Copy(sourceExpr);
            return true;
        }

        if (source is INamedTypeSymbol && dest is INamedTypeSymbol && !IsAtomic(source) && !IsAtomic(dest) &&
            !IsNullableValue(dest) && !IsEnumerable(source) && !IsEnumerable(dest))
        {
            foreach (var (seenSource, seenDest) in path)
            {
                if (SymbolEqualityComparer.Default.Equals(seenSource, source) && SymbolEqualityComparer.Default.Equals(seenDest, dest))
                {
                    fault = new Fault(MapDiagnostics.Recursive, member, dest.Name);
                    return false;
                }
            }

            path.Add((source, dest));
            var planned = TryObject(source, dest, sourceExpr, path, ref locals, warnings, out var args, out var typeName, out var ctorCount, out fault);
            path.RemoveAt(path.Count - 1);
            if (!planned)
                return false;

            plan = new Plan.Nested(typeName, args, ctorCount);
            return true;
        }

        fault = Cannot(source, dest, sourceExpr);
        return false;
    }

    private static bool TryObject(
        ITypeSymbol source,
        ITypeSymbol dest,
        string sourceExpr,
        List<(ITypeSymbol Source, ITypeSymbol Dest)> path,
        ref int locals,
        List<Diag> warnings,
        out EquatableArray<Arg> args,
        out string typeName,
        out int ctorCount,
        out Fault fault)
    {
        args = default;
        typeName = Display(dest.WithNullableAnnotation(NullableAnnotation.NotAnnotated));
        ctorCount = 0;
        fault = default;
        if (dest is not INamedTypeSymbol named)
        {
            fault = Cannot(source, dest, sourceExpr);
            return false;
        }

        var ctor = ChooseCtor(named);
        if (ctor is null)
        {
            fault = Cannot(source, dest, sourceExpr);
            return false;
        }

        var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var built = ImmutableArray.CreateBuilder<Arg>();
        foreach (var parameter in ctor.Parameters)
        {
            bound.Add(parameter.Name);
            ctorCount++;
            var name = Escape(parameter.Name);
            var destProperty = ForParameter(named, parameter.Name);
            if (IsLeave(parameter) || (destProperty is not null && IsLeave(destProperty)))
            {
                if (!parameter.HasExplicitDefaultValue)
                {
                    fault = new Fault(MapDiagnostics.Parameter, parameter.Name);
                    return false;
                }

                built.Add(new Arg(name, new Plan.Skip()));
                continue;
            }

            var property = ForParameter(source, parameter.Name);
            if (property is null)
            {
                if (!parameter.HasExplicitDefaultValue)
                {
                    fault = new Fault(MapDiagnostics.Parameter, parameter.Name);
                    return false;
                }

                warnings.Add(At(parameter, MapDiagnostics.Left, named.Name + "." + parameter.Name));
                built.Add(new Arg(name, new Plan.Skip()));
                continue;
            }

            if (!TryPlan(property.Type, parameter.Type, sourceExpr + "." + property.Name, named.Name + "." + parameter.Name, path, ref locals, warnings, out var child, out fault))
                return false;
            built.Add(new Arg(name, child));
        }

        foreach (var property in PublicProperties(named))
        {
            if (bound.Contains(property.Name))
                continue;
            if (property.SetMethod is not { DeclaredAccessibility: Accessibility.Public })
                continue;
            var from = PublicProperty(source, property.Name);
            if (from is null || IsLeave(property))
            {
                if (from is null && property.IsRequired)
                {
                    fault = new Fault(MapDiagnostics.Required, property.Name);
                    return false;
                }

                if (from is null && !IsLeave(property))
                    warnings.Add(At(property, MapDiagnostics.Left, named.Name + "." + property.Name));
                continue;
            }

            if (!TryPlan(from.Type, property.Type, sourceExpr + "." + property.Name, named.Name + "." + property.Name, path, ref locals, warnings, out var child, out fault))
                return false;
            built.Add(new Arg(Escape(property.Name), child));
        }

        args = new EquatableArray<Arg>(built.ToImmutable());
        return true;
    }

    private static IMethodSymbol? ChooseCtor(INamedTypeSymbol type)
    {
        IMethodSymbol? best = null;
        foreach (var ctor in type.InstanceConstructors)
        {
            if (ctor.DeclaredAccessibility != Accessibility.Public)
                continue;
            if (ctor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, type))
                continue;
            if (best is null || ctor.Parameters.Length > best.Parameters.Length)
                best = ctor;
        }

        return best;
    }

    private static IEnumerable<IPropertySymbol> PublicProperties(ITypeSymbol type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (!seen.Add(property.Name))
                    continue;
                if (property.DeclaredAccessibility == Accessibility.Public &&
                    !property.IsStatic &&
                    property.Parameters.Length == 0 &&
                    property.GetMethod is { DeclaredAccessibility: Accessibility.Public })
                    yield return property;
            }
        }
    }

    private static string HintName(INamedTypeSymbol type, IMethodSymbol method, IParameterSymbol parameter)
    {
        var name = new StringBuilder();
        foreach (var c in type.ToDisplayString() + "." + method.Name + "." + parameter.Type.ToDisplayString())
            name.Append(char.IsLetterOrDigit(c) || c == '.' ? c : '_');
        return name.Append(".g.cs").ToString();
    }

    private static IPropertySymbol? PublicProperty(ITypeSymbol type, string name)
        => PublicProperties(type).FirstOrDefault(property => property.Name == name);

    private static IPropertySymbol? ForParameter(ITypeSymbol type, string name)
        => PublicProperty(type, name)
            ?? PublicProperties(type).FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string Escape(string name)
        => SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

    private static bool IsLeave(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var type = attribute.AttributeClass;
            if (type is null)
                continue;
            if (type.Name == "LeaveAttribute" && type.ContainingNamespace.ToDisplayString() == "MapLean")
                return true;
        }

        return false;
    }

    private static bool IsList(ITypeSymbol type, out ITypeSymbol element)
    {
        if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.MetadataName == "List`1" &&
            named.OriginalDefinition.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
        {
            element = named.TypeArguments[0];
            return true;
        }

        element = null!;
        return false;
    }

    private static bool IsAtomic(ITypeSymbol type)
        => type.SpecialType != SpecialType.None || type.TypeKind == TypeKind.Enum;

    private static readonly SymbolDisplayFormat DisplayFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static string Display(ITypeSymbol type)
        => type.ToDisplayString(DisplayFormat);

    private static bool IsNullableValue(ITypeSymbol type)
        => type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    private static ITypeSymbol Unwrap(ITypeSymbol type)
        => IsNullableValue(type) ? ((INamedTypeSymbol)type).TypeArguments[0] : type;

    private static bool IsEnumerable(ITypeSymbol type)
        => type.SpecialType != SpecialType.System_String &&
            (type.SpecialType == SpecialType.System_Collections_IEnumerable ||
                type.AllInterfaces.Any(static contract => contract.SpecialType == SpecialType.System_Collections_IEnumerable));

    private static string Access(Accessibility accessibility)
        => accessibility == Accessibility.Internal ? "internal" : "public";

    private static bool Supported(Accessibility accessibility)
        => accessibility is Accessibility.Public or Accessibility.Internal;

    private static string KindName(ITypeSymbol type)
    {
        if (type.IsRecord)
            return "record";
        if (type.TypeKind == TypeKind.Struct)
            return "struct";
        return type.TypeKind.ToString().ToLowerInvariant();
    }

    private static Fault Cannot(ITypeSymbol source, ITypeSymbol dest, string sourceExpr)
        => new(MapDiagnostics.CannotMap, source.ToDisplayString(), dest.ToDisplayString(), sourceExpr);

    private static ContractResult.Bad Bad(SyntaxNode node, string id, params string[] args)
        => new(At(node.GetLocation(), id, args));

    private static ContractResult.Bad Bad(SyntaxToken token, string id, params string[] args)
        => new(At(token.GetLocation(), id, args));

    private static ContractResult.Bad Bad(SyntaxToken token, Fault fault)
    {
        var args = new string[fault.Args.Length];
        for (var i = 0; i < args.Length; i++)
            args[i] = fault.Args[i];
        return Bad(token, fault.Id, args);
    }

    private static Diag At(Location location, string id, params string[] args)
    {
        var span = location.GetLineSpan();
        return new Diag(
            id,
            new EquatableArray<string>(args),
            location.SourceTree?.FilePath ?? "",
            span.StartLinePosition.Line,
            span.StartLinePosition.Character,
            span.EndLinePosition.Line,
            span.EndLinePosition.Character,
            location.SourceSpan.Start,
            location.SourceSpan.Length);
    }

    private static Diag At(ISymbol symbol, string id, params string[] args)
        => At(symbol.Locations.FirstOrDefault() ?? Location.None, id, args);

    private readonly record struct Fault(string Id, EquatableArray<string> Args)
    {
        public Fault(string id, params string[] args)
            : this(id, new EquatableArray<string>(args))
        {
        }
    }
}
