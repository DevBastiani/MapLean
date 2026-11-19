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
                [Leave]
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
        Assert.DoesNotContain("ML0006", run.Warnings);
        Assert.DoesNotContain("Audit", run.Warnings);
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
        Assert.Contains("ML0004", run.Errors);
        Assert.Contains("https://github.com/DevBastiani/MapLean#ml0004", run.Errors);
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
        Assert.Contains("ML0005", run.Errors);
        Assert.Contains("Region", run.Errors);
        Assert.Contains("https://github.com/DevBastiani/MapLean#ml0005", run.Errors);
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
        Assert.Contains("Move [To] onto a partial method", run.Errors);
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
        Assert.Contains("ML0002", run.Errors);
        Assert.Contains("Add partial", run.Errors);
    }

    [Fact]
    public void Wrong_signature_is_its_own_diagnostic()
    {
        var run = Compile("""
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name);
            public sealed record Dst(string Name);
            public static partial class M { [To] public static partial Dst Apply(Src source, int extra); }
            """);
        Assert.Contains("ML0003", run.Errors);
        Assert.Contains("https://github.com/DevBastiani/MapLean#ml0003", run.Errors);
    }

    [Fact]
    public void Generated_file_starts_with_the_auto_generated_comment()
    {
        var run = Compile("""
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name);
            public sealed record Dst(string Name);
            public static partial class M { [To] public static partial Dst Apply(Src source); }
            """);
        Assert.StartsWith("// <auto-generated/>", run.Generated);
    }

    [Fact]
    public void Destination_without_source_warns_until_marked_leave()
    {
        var open = Compile("""
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name, string Audit);
            public sealed record Dst(string Name) { public string Extra { get; init; } = "kept"; }
            public static partial class M { [To] public static partial Dst Apply(Src source); }
            public static class Probe
            {
                public static string Run() => M.Apply(new Src("ada", "audit-secret")).Extra;
            }
            """);
        Assert.Contains("ML0006", open.Warnings);
        Assert.Contains("Extra", open.Warnings);
        Assert.DoesNotContain("Audit", open.Warnings);
        Assert.DoesNotContain("Extra", open.Generated);
        Assert.DoesNotContain("Audit", open.Generated);
        Assert.Equal("kept", open.Assembly.GetType("Demo.Probe")!.GetMethod("Run")!.Invoke(null, null));

        var marked = Compile("""
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name);
            public sealed record Dst(string Name) { [Leave] public string Extra { get; init; } = "kept"; }
            public sealed class IgnoreAttribute : System.Attribute {}
            public sealed record Other(string Name) { [Ignore] public string Extra { get; init; } = "open"; }
            public static partial class M { [To] public static partial Dst Apply(Src source); }
            public static partial class N { [To] public static partial Other Apply(Src source); }
            public sealed record WithSource(string Name, string Extra);
            public sealed record Held(string Name) { [Leave] public string Extra { get; init; } = "held"; }
            public static partial class P { [To] public static partial Held Apply(WithSource source); }
            public static class Probe
            {
                public static string Run() => M.Apply(new Src("ada")).Extra + "|" + N.Apply(new Src("ada")).Extra + "|" + P.Apply(new WithSource("ada", "from-source")).Extra;
            }
            """);
        Assert.DoesNotContain("Dst.Extra", marked.Warnings);
        Assert.DoesNotContain("Held.Extra", marked.Warnings);
        Assert.Contains("Other.Extra", marked.Warnings);
        Assert.Equal("kept|open|held", marked.Assembly.GetType("Demo.Probe")!.GetMethod("Run")!.Invoke(null, null));
    }

    [Fact]
    public void Partial_record_struct_and_nested_type_are_diagnostics()
    {
        var record = Compile("""
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name);
            public sealed record Dst(string Name);
            public partial record M { [To] public static partial Dst Apply(Src source); }
            """);
        Assert.Contains("ML0007", record.Errors);
        Assert.DoesNotContain("partial class", record.Generated);

        var asStruct = Compile("""
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name);
            public sealed record Dst(string Name);
            public partial struct M { [To] public static partial Dst Apply(Src source); }
            """);
        Assert.Contains("ML0007", asStruct.Errors);
        Assert.DoesNotContain("partial class", asStruct.Generated);

        var nested = Compile("""
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name);
            public sealed record Dst(string Name);
            public static partial class Outer
            {
                public static partial class Inner { [To] public static partial Dst Apply(Src source); }
            }
            """);
        Assert.Contains("ML0008", nested.Errors);
        Assert.Equal("", nested.Generated);
    }

    [Fact]
    public void Private_method_is_a_diagnostic_and_internal_is_kept()
    {
        var hidden = Compile("""
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name);
            public sealed record Dst(string Name);
            public static partial class M { [To] private static partial Dst Apply(Src source); }
            """);
        Assert.Contains("ML0009", hidden.Errors);
        Assert.DoesNotContain("public static partial", hidden.Generated);

        var internalMap = Compile("""
            using MapLean;
            namespace Demo;
            public sealed record Src(string Name);
            public sealed record Dst(string Name);
            internal static partial class M { [To] internal static partial Dst Apply(Src source); }
            """);
        Assert.Equal("", internalMap.Errors);
        Assert.Contains("internal static partial class M", internalMap.Generated);
        Assert.Contains("internal static partial", internalMap.Generated);
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

    internal static Run Compile(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "corpus.cs");
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(ToAttribute).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "corpus",
            new[] { tree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MapGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiags);
        var mapLean = generatorDiags.Where(diagnostic => diagnostic.Id.StartsWith("ML", StringComparison.Ordinal)).ToArray();
        var errors = string.Join("\n", mapLean.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(Format));
        var warnings = string.Join("\n", mapLean.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning).Select(Format));
        var generated = string.Join(
            "\n",
            output.SyntaxTrees.Where(treeItem => treeItem.FilePath.EndsWith(".g.cs", StringComparison.Ordinal)).Select(treeItem => treeItem.ToString()));
        if (errors.Length != 0 || generated.Length == 0)
            return new Run(errors, warnings, generated, null!, "");

        using var pe = new MemoryStream();
        var emit = output.Emit(pe);
        if (!emit.Success)
        {
            var emitErrors = string.Join("\n", emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(diagnostic => diagnostic.Id + " " + diagnostic.GetMessage()));
            return new Run(errors + emitErrors, warnings, generated, null!, "");
        }

        var build = string.Join("\n", emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning).Select(diagnostic => diagnostic.Id + " " + diagnostic.GetMessage()));
        pe.Position = 0;
        var assembly = new AssemblyLoadContext("corpus", true).LoadFromStream(pe);
        return new Run(errors, warnings, generated, assembly, build);
    }

    private static string Format(Diagnostic diagnostic)
        => diagnostic.Id + " " + diagnostic.GetMessage() + " " + diagnostic.Descriptor.HelpLinkUri;

    internal sealed record Run(string Errors, string Warnings, string Generated, Assembly Assembly, string Build);
}
