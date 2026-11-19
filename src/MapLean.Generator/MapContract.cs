namespace MapLean.Generator;

internal sealed record Arg(string Name, Plan Value);

internal abstract record Plan
{
    private Plan()
    {
    }

    internal sealed record Copy(string Path) : Plan;

    internal sealed record Nested(string Type, EquatableArray<Arg> Args, int CtorCount) : Plan;

    internal sealed record NullNested(string Path, string Local, string Type, EquatableArray<Arg> Args) : Plan;

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

internal readonly record struct Diag(
    string Id,
    EquatableArray<string> Args,
    string Path,
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter,
    int Start,
    int Length);

internal abstract record ContractResult
{
    private ContractResult()
    {
    }

    internal sealed record Ok(MapContract Contract, string Hint, EquatableArray<Diag> Warnings) : ContractResult;

    internal sealed record Bad(Diag Problem) : ContractResult;
}
