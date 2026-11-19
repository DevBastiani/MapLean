using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MapLean.Generator;

internal static class ContractReader
{
    public static ContractResult Read(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetNode is not MethodDeclarationSyntax methodSyntax || ctx.TargetSymbol is not IMethodSymbol method)
            return Fail(ctx.TargetNode, "Put To on a partial method.");

        if (!methodSyntax.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)))
            return Fail(methodSyntax.Identifier.GetLocation(), "The method must be partial.");

        if (method.Parameters.Length != 1 || method.ReturnsVoid || method.Arity != 0)
            return Fail(methodSyntax.Identifier.GetLocation(), "The method needs one source parameter and a destination return type.");

        var parameter = method.Parameters[0];
        var locals = 0;
        if (!TryPlan(parameter.Type, method.ReturnType, parameter.Name, ref locals, out var plan, out var error))
            return Fail(methodSyntax.Identifier.GetLocation(), error);

        var ns = method.ContainingNamespace is null || method.ContainingNamespace.IsGlobalNamespace
            ? null
            : method.ContainingNamespace.ToDisplayString();
        var contract = new MapContract(
            ns,
            Access(method.ContainingType.DeclaredAccessibility),
            method.ContainingType.IsStatic,
            method.ContainingType.Name,
            Access(method.DeclaredAccessibility),
            method.IsStatic,
            method.Name,
            Display(parameter.Type),
            Display(method.ReturnType),
            parameter.Name,
            plan);
        var hint = method.ContainingType.Name + "." + method.Name + ".g.cs";
        return new ContractResult.Ok(contract, hint);
    }

    private static bool TryPlan(ITypeSymbol source, ITypeSymbol dest, string sourceExpr, ref int locals, out Plan plan, out string error)
    {
        plan = null!;
        error = "";
        if (IsList(source, out var sourceElem) && IsList(dest, out var destElem))
        {
            var local = "ml" + locals++;
            if (!TryPlan(sourceElem, destElem, local + "Item", ref locals, out var element, out error))
                return false;
            plan = new Plan.List(sourceExpr, local, Display(destElem), element);
            return true;
        }

        if (SymbolEqualityComparer.Default.Equals(source, dest))
        {
            plan = new Plan.Copy(sourceExpr);
            return true;
        }

        if (source is INamedTypeSymbol && dest is INamedTypeSymbol && !IsAtomic(source) && !IsAtomic(dest))
        {
            if (source.NullableAnnotation == NullableAnnotation.Annotated && dest.NullableAnnotation != NullableAnnotation.Annotated)
            {
                error = $"Cannot map {source.ToDisplayString()} to {dest.ToDisplayString()} for {sourceExpr}.";
                return false;
            }

            var root = sourceExpr;
            string? nullLocal = null;
            if (source.NullableAnnotation == NullableAnnotation.Annotated)
            {
                nullLocal = "ml" + locals++;
                root = nullLocal;
            }

            if (!TryObject(source, dest, root, ref locals, out var args, out var typeName, out var ctorCount, out error))
                return false;

            plan = nullLocal is null
                ? new Plan.Nested(typeName, args, ctorCount)
                : new Plan.NullNested(sourceExpr, nullLocal, typeName, args);
            return true;
        }

        error = $"Cannot map {source.ToDisplayString()} to {dest.ToDisplayString()} for {sourceExpr}.";
        return false;
    }

    private static bool TryObject(
        ITypeSymbol source,
        ITypeSymbol dest,
        string sourceExpr,
        ref int locals,
        out ImmutableArray<Arg> args,
        out string typeName,
        out int ctorCount,
        out string error)
    {
        args = ImmutableArray<Arg>.Empty;
        typeName = Display(dest);
        ctorCount = 0;
        error = "";
        if (dest is not INamedTypeSymbol named)
        {
            error = $"Cannot map {source.ToDisplayString()} to {dest.ToDisplayString()} for {sourceExpr}.";
            return false;
        }

        var ctor = ChooseCtor(named);
        if (ctor is null)
        {
            error = $"Cannot map {source.ToDisplayString()} to {dest.ToDisplayString()} for {sourceExpr}.";
            return false;
        }

        var bound = new HashSet<string>(StringComparer.Ordinal);
        var built = ImmutableArray.CreateBuilder<Arg>();
        foreach (var parameter in ctor.Parameters)
        {
            bound.Add(parameter.Name);
            ctorCount++;
            var property = PublicProperty(source, parameter.Name);
            if (property is null)
            {
                if (!parameter.HasExplicitDefaultValue)
                {
                    error = $"{parameter.Name} is required and has no source member.";
                    return false;
                }

                built.Add(new Arg(parameter.Name, new Plan.Copy("default")));
                continue;
            }

            if (!TryPlan(property.Type, parameter.Type, sourceExpr + "." + parameter.Name, ref locals, out var child, out error))
                return false;
            built.Add(new Arg(parameter.Name, child));
        }

        foreach (var property in PublicProperties(named))
        {
            if (bound.Contains(property.Name))
                continue;
            if (property.SetMethod is not { DeclaredAccessibility: Accessibility.Public })
                continue;
            var from = PublicProperty(source, property.Name);
            if (from is null)
            {
                if (property.IsRequired)
                {
                    error = $"{property.Name} is required and has no source member.";
                    return false;
                }

                continue;
            }

            if (!TryPlan(from.Type, property.Type, sourceExpr + "." + property.Name, ref locals, out var child, out error))
                return false;
            built.Add(new Arg(property.Name, child));
        }

        args = built.ToImmutable();
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
        => type.GetMembers().OfType<IPropertySymbol>().Where(static property =>
            property.DeclaredAccessibility == Accessibility.Public &&
            !property.IsStatic &&
            property.Parameters.Length == 0 &&
            property.GetMethod is { DeclaredAccessibility: Accessibility.Public });

    private static IPropertySymbol? PublicProperty(ITypeSymbol type, string name)
        => PublicProperties(type).FirstOrDefault(property => property.Name == name);

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

    private static string Display(ITypeSymbol type)
        => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string Access(Accessibility accessibility)
        => accessibility == Accessibility.Internal ? "internal" : "public";

    private static ContractResult.Bad Fail(SyntaxNode node, string message)
        => Fail(node.GetLocation(), message);

    private static ContractResult.Bad Fail(Location location, string message)
    {
        var span = location.GetLineSpan();
        return new ContractResult.Bad(
            message,
            location.SourceTree?.FilePath ?? "",
            span.StartLinePosition.Line,
            span.StartLinePosition.Character,
            span.EndLinePosition.Line,
            span.EndLinePosition.Character,
            location.SourceSpan.Start,
            location.SourceSpan.Length);
    }
}
