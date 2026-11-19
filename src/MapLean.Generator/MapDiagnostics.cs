using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace MapLean.Generator;

internal static class MapDiagnostics
{
    public const string HelpRoot = "https://github.com/DevBastiani/MapLean#";
    public const string ToTarget = "ML0001";
    public const string Partial = "ML0002";
    public const string Signature = "ML0003";
    public const string CannotMap = "ML0004";
    public const string Required = "ML0005";
    public const string Left = "ML0006";
    public const string TypeKind = "ML0007";
    public const string Nested = "ML0008";
    public const string Access = "ML0009";

    private static readonly DiagnosticDescriptor[] Descriptors =
    {
        Describe(ToTarget, "To belongs on a method", "Move [To] onto a partial method. That method takes the source and returns the destination.", DiagnosticSeverity.Error),
        Describe(Partial, "Method must be partial", "Add partial to {0}.", DiagnosticSeverity.Error),
        Describe(Signature, "Method shape", "Give {0} one source parameter and a destination return type.", DiagnosticSeverity.Error),
        Describe(CannotMap, "Unsupported member types", "Change {2} so MapLean can copy {0} to {1}. MapLean copies matching types, nested objects, nullable objects, and List<T>.", DiagnosticSeverity.Error),
        Describe(Required, "Required member missing", "Add a public {0} on the source, or remove required from {0}.", DiagnosticSeverity.Error),
        Describe(Left, "Destination member left", "{0} has no matching source member. Add a public member with that name on the source, or mark it with Leave to keep the destination value.", DiagnosticSeverity.Warning),
        Describe(TypeKind, "Declaring type must be a class", "Change {0} to a partial class. It is a {1}, and MapLean writes a partial class.", DiagnosticSeverity.Error),
        Describe(Nested, "Declaring type must be top-level", "Move {0} out of {1}. MapLean writes a top-level partial class, and a nested type would not receive the method.", DiagnosticSeverity.Error),
        Describe(Access, "Accessibility must be public or internal", "Make {0} public or internal. MapLean repeats that accessibility on the generated partial.", DiagnosticSeverity.Error),
    };

    public static Diagnostic Create(Diag diag)
    {
        DiagnosticDescriptor? descriptor = null;
        foreach (var candidate in Descriptors)
        {
            if (candidate.Id == diag.Id)
            {
                descriptor = candidate;
                break;
            }
        }

        if (descriptor is null)
            throw new InvalidOperationException(diag.Id);

        var location = diag.Path.Length == 0
            ? Location.None
            : Location.Create(
                diag.Path,
                new TextSpan(diag.Start, diag.Length),
                new LinePositionSpan(
                    new LinePosition(diag.StartLine, diag.StartCharacter),
                    new LinePosition(diag.EndLine, diag.EndCharacter)));
        var args = new object[diag.Args.Length];
        for (var i = 0; i < args.Length; i++)
            args[i] = diag.Args[i];
        return Diagnostic.Create(descriptor, location, args);
    }

    private static DiagnosticDescriptor Describe(string id, string title, string message, DiagnosticSeverity severity)
        => new(
            id,
            title,
            message,
            "MapLean",
            severity,
            isEnabledByDefault: true,
            helpLinkUri: HelpRoot + id.ToLowerInvariant());
}
