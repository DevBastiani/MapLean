using MapLean.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace MapLean.Tests;

public sealed class CacheTests
{
    [Fact]
    public void Irrelevant_edit_keeps_the_contract_cached()
    {
        const string map = """
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name);
            public sealed record Dst(string Name);
            public static partial class M
            {
                [To]
                public static partial Dst Apply(Src source);
            }
            """;
        var reasons = Rerun(map, map, "namespace Demo; public sealed class Unrelated { public int N => 1; }", "namespace Demo; public sealed class Unrelated { public int N => 1; } // note");
        AssertContractKept(reasons);
    }

    [Fact]
    public void Whitespace_edit_keeps_the_contract_unchanged()
    {
        const string before = """
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name);
            public sealed record Dst(string Name);
            public static partial class M
            {
                [To]
                public static partial Dst Apply(Src source);
            }
            """;
        const string after = """
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name);
            public sealed record Dst(string Name);
            public static partial class M
            {
                [To]
                public static partial Dst Apply(Src  source);
            }
            """;
        var reasons = Rerun(before, after, "namespace Demo; public sealed class Unrelated { public int N => 1; }", "namespace Demo; public sealed class Unrelated { public int N => 1; }");
        AssertContract(reasons, IncrementalStepRunReason.Unchanged);
    }

    private static void AssertContract(string[] reasons, IncrementalStepRunReason expected)
        => AssertContract(reasons, reason => Assert.Equal("Contracts " + expected, reason));

    private static void AssertContractKept(string[] reasons)
        => AssertContract(reasons, reason =>
        {
            var kind = reason["Contracts ".Length..];
            Assert.True(kind is nameof(IncrementalStepRunReason.Cached) or nameof(IncrementalStepRunReason.Unchanged), reason);
        });

    private static void AssertContract(string[] reasons, Action<string> assert)
    {
        var contract = reasons.Where(reason => reason.StartsWith("Contracts ", StringComparison.Ordinal)).ToArray();
        Assert.True(contract.Length > 0, string.Join("\n", reasons));
        Assert.All(contract, assert);
    }

    private static string[] Rerun(string mapBefore, string mapAfter, string otherBefore, string otherAfter)
    {
        var map = CSharpSyntaxTree.ParseText(mapBefore, path: "map.cs");
        var other = CSharpSyntaxTree.ParseText(otherBefore, path: "other.cs");
        var compilation = CSharpCompilation.Create(
            "cache",
            new[] { map, other },
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new MapGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGenerators(compilation);
        var editedMap = CSharpSyntaxTree.ParseText(mapAfter, path: "map.cs");
        var editedOther = CSharpSyntaxTree.ParseText(otherAfter, path: "other.cs");
        compilation = compilation.ReplaceSyntaxTree(map, editedMap).ReplaceSyntaxTree(other, editedOther);
        driver = driver.RunGenerators(compilation);
        var steps = driver.GetRunResult().Results.Single().TrackedSteps;
        return steps
            .SelectMany(pair => pair.Value.SelectMany(step => step.Outputs.Select(output => pair.Key + " " + output.Reason)))
            .ToArray();
    }

    private static IEnumerable<MetadataReference> References()
        => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(ToAttribute).Assembly.Location));
}
