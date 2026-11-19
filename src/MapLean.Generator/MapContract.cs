using System.Collections.Immutable;

namespace MapLean.Generator;

internal sealed record Arg(string Name, Plan Value);

internal abstract record Plan
{
    private Plan()
    {
    }

    internal sealed record Copy(string Path) : Plan;

    internal sealed record Nested(string Type, ImmutableArray<Arg> Args, int CtorCount) : Plan;

    internal sealed record NullNested(string Path, string Local, string Type, ImmutableArray<Arg> Args) : Plan;

    internal sealed record List(string Path, string Local, string ElementType, Plan Element) : Plan;
}

internal sealed record MapContract(
    string? Namespace,
    string TypeAccessibility,
    bool TypeIsStatic,
    string TypeName,
    string MethodAccessibility,
    bool MethodIsStatic,
    string MethodName,
    string SourceType,
    string DestType,
    string ParameterName,
    Plan Root);

internal abstract record ContractResult
{
    private ContractResult()
    {
    }

    internal sealed record Ok(MapContract Contract, string Hint) : ContractResult;

    internal sealed record Bad(
        string Message,
        string Path,
        int StartLine,
        int StartCharacter,
        int EndLine,
        int EndCharacter,
        int Start,
        int Length) : ContractResult;
}

internal static class MapDiagnostics
{
    public const string Id = "ML0001";
}
