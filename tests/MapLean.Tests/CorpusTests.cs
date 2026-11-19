using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using MapLean.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace MapLean.Tests;

public sealed class CorpusTests
{
    [Fact]
    public void Present_and_absent_match_and_skip_members_outside_the_contract()
    {
        var run = Compile("""
            using System.Collections.Generic;
            using MapLean;

            namespace Demo;

            public sealed record Address(string City, string? Line2);
            public sealed record LineItem(string Sku, int Qty);
            public sealed record Order(string Id, Address ShipTo, Address? BillTo, List<LineItem> Lines, string? Note, string Audit);
            public sealed record AddressDto(string City, string? Line2);
            public sealed record LineItemDto(string Sku, int Qty);
            public sealed record OrderDto(string Id, AddressDto ShipTo, AddressDto? BillTo, List<LineItemDto> Lines, string? Note)
            {
                public string Channel { get; init; } = "unset";
            }

            public static partial class OrderMaps
            {
                [To]
                public static partial OrderDto Apply(Order source);
            }

            public static class Probe
            {
                public static string Present()
                {
                    var lines = new List<LineItem>();
                    lines.Add(new LineItem("sku-0", 1));
                    lines.Add(new LineItem("sku-1", 2));
                    var order = new Order("o-1", new Address("Austin", "Suite 2"), new Address("Dallas", null), lines, null, "audit-secret");
                    var dto = OrderMaps.Apply(order);
                    return dto.Id + "|" + dto.ShipTo.City + "|" + dto.ShipTo.Line2 + "|" + dto.BillTo!.City + "|" + (dto.BillTo.Line2 == null) + "|" + (dto.Note == null) + "|" + dto.Lines.Count + "|" + dto.Lines[0].Sku + "|" + dto.Lines[1].Qty + "|" + dto.Channel + "|" + dto.Lines.Capacity;
                }

                public static string Absent()
                {
                    var order = new Order("o-2", new Address("Austin", null), null, new List<LineItem>(), "note", "audit-secret");
                    var dto = OrderMaps.Apply(order);
                    return dto.Id + "|" + (dto.BillTo == null) + "|" + dto.Note + "|" + dto.Lines.Count + "|" + dto.Channel;
                }
            }
            """);

        Assert.DoesNotContain("ML0001", run.Errors);
        Assert.DoesNotContain("Audit", run.Generated);
        Assert.DoesNotContain("Channel", run.Generated);
        var probe = run.Assembly.GetType("Demo.Probe")!;
        Assert.Equal("o-1|Austin|Suite 2|Dallas|True|True|2|sku-0|2|unset|2", probe.GetMethod("Present")!.Invoke(null, null));
        Assert.Equal("o-2|True|note|0|unset", probe.GetMethod("Absent")!.Invoke(null, null));
    }

    [Fact]
    public void Non_public_member_stays_at_its_default()
    {
        var run = Compile("""
            using MapLean;

            namespace Demo;

            public sealed class Box
            {
                public required string Label { get; init; }
                internal string Code { get; init; } = "secret";
            }

            public sealed class BoxDto
            {
                public required string Label { get; init; }
                internal string Code { get; init; } = "unset";
            }

            public static partial class BoxMaps
            {
                [To]
                public static partial BoxDto Apply(Box source);
            }

            public static class Probe
            {
                public static string Run()
                {
                    var dto = BoxMaps.Apply(new Box { Label = "lid" });
                    return dto.Label + "|" + dto.Code;
                }
            }
            """);

        Assert.DoesNotContain("ML0001", run.Errors);
        Assert.DoesNotContain("Code", run.Generated);
        var probe = run.Assembly.GetType("Demo.Probe")!;
        Assert.Equal("lid|unset", probe.GetMethod("Run")!.Invoke(null, null));
    }

    [Fact]
    public void Incompatible_type_is_the_diagnostic()
    {
        var run = Compile("""
            using MapLean;
            namespace Demo;
            public sealed class Src { public int N { get; init; } }
            public sealed class Dst { public required string N { get; init; } }
            public static partial class M { [To] public static partial Dst Apply(Src source); }
            """);
        Assert.Contains("ML0001", run.Errors);
    }

    [Fact]
    public void Required_member_with_no_source_is_the_diagnostic()
    {
        var run = Compile("""
            using MapLean;
            namespace Demo;
            public sealed class Src { public string Name { get; init; } = ""; }
            public sealed record Dst(string Name, string Region);
            public static partial class M { [To] public static partial Dst Apply(Src source); }
            """);
        Assert.Contains("ML0001", run.Errors);
        Assert.Contains("Region", run.Errors);
    }

    [Fact]
    public void Attribute_on_the_type_is_the_diagnostic()
    {
        var run = Compile("""
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name);
            public sealed record Dst(string Name);
            [To]
            public static partial class M { public static partial Dst Apply(Src source); }
            """);
        Assert.Contains("ML0001", run.Errors);
    }

    [Fact]
    public void Method_without_partial_is_the_diagnostic()
    {
        var run = Compile("""
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name);
            public sealed record Dst(string Name);
            public static partial class M { [To] public static Dst Apply(Src source) => new(source.Name); }
            """);
        Assert.Contains("ML0001", run.Errors);
    }

    [Fact]
    public void Invented_attribute_and_competitor_names_are_not_the_contract()
    {
        var run = Compile("""
            namespace Demo;
            public sealed class OtherAttribute : System.Attribute {}
            public static partial class M
            {
                [Other]
                public static string Apply(string source) => source;
                public static void CreateMap() {}
                public static void ProjectTo() {}
                public static void ForMember() {}
            }
            public interface IMapper {}
            """);
        Assert.DoesNotContain("ML0001", run.Errors);
        Assert.Equal("", run.Generated);
    }

    private static Run Compile(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "corpus.cs");
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(ToAttribute).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "corpus",
            new[] { tree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MapGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiags);
        var diags = output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Concat(generatorDiags.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            .Select(diagnostic => diagnostic.Id + " " + diagnostic.GetMessage())
            .ToImmutableArray();
        var generated = string.Join(
            "\n",
            output.SyntaxTrees.Where(treeItem => treeItem.FilePath.EndsWith(".g.cs", StringComparison.Ordinal)).Select(treeItem => treeItem.ToString()));
        var errors = string.Join("\n", diags);
        if (errors.Contains("ML0001", StringComparison.Ordinal) || generated.Length == 0)
            return new Run(errors, generated, null!);

        using var pe = new MemoryStream();
        var emit = output.Emit(pe);
        if (!emit.Success)
        {
            var emitErrors = string.Join("\n", emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(diagnostic => diagnostic.Id + " " + diagnostic.GetMessage()));
            return new Run(emitErrors, generated, null!);
        }

        pe.Position = 0;
        var assembly = new AssemblyLoadContext("corpus", true).LoadFromStream(pe);
        return new Run(errors, generated, assembly);
    }

    private sealed record Run(string Errors, string Generated, Assembly Assembly);
}
