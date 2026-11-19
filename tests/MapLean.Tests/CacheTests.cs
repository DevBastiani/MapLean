using MapLean.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace MapLean.Tests;

public sealed class CacheTests
{
    [Fact]
    public void Irrelevant_edit_keeps_a_cached_step()
    {
        var map = CSharpSyntaxTree.ParseText(
            """
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name);
            public sealed record Dst(string Name);
            public static partial class M
            {
                [To]
                public static partial Dst Apply(Src source);
            }
            """,
            path: "map.cs");
        var other = CSharpSyntaxTree.ParseText(
            "namespace Demo; public sealed class Unrelated { public int N => 1; }",
            path: "other.cs");
        var compilation = CSharpCompilation.Create(
            "cache",
            new[] { map, other },
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new MapGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGenerators(compilation);
        var edited = CSharpSyntaxTree.ParseText(
            "namespace Demo; public sealed class Unrelated { public int N => 1; } // note",
            path: "other.cs");
        driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(other, edited));
        var steps = driver.GetRunResult().Results.Single().TrackedSteps;
        var reasons = steps
            .SelectMany(pair => pair.Value.SelectMany(step => step.Outputs.Select(output => pair.Key + " " + output.Reason)))
            .ToArray();
        Assert.Contains(reasons, reason => reason.EndsWith(" " + IncrementalStepRunReason.Cached, StringComparison.Ordinal));
    }

    private static IEnumerable<MetadataReference> References()
        => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(ToAttribute).Assembly.Location));
}
