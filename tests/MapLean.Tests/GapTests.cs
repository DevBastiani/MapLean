using Xunit;

namespace MapLean.Tests;

public sealed class GapTests
{
    private const string Head = "using System;\nusing System.Collections.Generic;\nusing MapLean;\nnamespace Demo;\n";

    private static CorpusTests.Run Build(string source) => CorpusTests.Compile(Head + source);

    private static string Result(CorpusTests.Run run)
    {
        Assert.Equal("", run.Errors);
        return (string)run.Assembly.GetType("Demo.Probe")!.GetMethod("Run")!.Invoke(null, null)!;
    }

    [Fact]
    public void Recursive_type_is_a_diagnostic_that_leave_resolves()
    {
        var open = Build("""
            public class N { public string V { get; set; } = ""; public N? Next { get; set; } }
            public class ND { public string V { get; set; } = ""; public ND? Next { get; set; } }
            public static partial class M { [To] public static partial ND Apply(N source); }
            """);
        Assert.Contains("ML0010", open.Errors);
        Assert.Contains("[Leave]", open.Errors);
        Assert.Contains("ND.Next", open.Errors);
        Assert.Contains("https://github.com/DevBastiani/MapLean#ml0010", open.Errors);

        var left = Build("""
            public class N { public string V { get; set; } = ""; public N? Next { get; set; } }
            public class ND { public string V { get; set; } = ""; [Leave] public ND? Next { get; set; } }
            public static partial class M { [To] public static partial ND Apply(N source); }
            public static class Probe { public static string Run() { var d = M.Apply(new N { V = "a", Next = new N { V = "b" } }); return d.V + "|" + (d.Next == null); } }
            """);
        Assert.Equal("a|True", Result(left));
    }

    [Fact]
    public void Recursion_through_a_list_or_a_second_type_is_a_diagnostic()
    {
        var list = Build("""
            public class T { public List<T> Kids { get; set; } = new(); }
            public class TD { public List<TD> Kids { get; set; } = new(); }
            public static partial class M { [To] public static partial TD Apply(T source); }
            """);
        Assert.Contains("ML0010", list.Errors);

        var mutual = Build("""
            public class A { public B? Other { get; set; } }
            public class B { public A? Back { get; set; } }
            public class AD { public BD? Other { get; set; } }
            public class BD { public AD? Back { get; set; } }
            public static partial class M { [To] public static partial AD Apply(A source); }
            """);
        Assert.Contains("ML0010", mutual.Errors);
    }

    [Fact]
    public void Overloads_and_same_named_classes_each_get_their_own_file()
    {
        var overloads = Build("""
            public record P(string Name); public record D(string Name);
            public record Q(int V); public record QD(int V);
            public static partial class M
            {
                [To] public static partial D Apply(P source);
                [To] public static partial QD Apply(Q source);
            }
            public static class Probe { public static string Run() => M.Apply(new P("x")).Name + M.Apply(new Q(2)).V; }
            """);
        Assert.Equal("x2", Result(overloads));

        var namespaces = CorpusTests.Compile("""
            namespace Demo
            {
                public record P(string Name); public record D(string Name);
                public static partial class M { [MapLean.To] public static partial D Apply(P source); }
                public static class Probe { public static string Run() => M.Apply(new P("x")).Name + Other.M.Apply(new P("y")).Name; }
            }
            namespace Demo.Other
            {
                public static partial class M { [MapLean.To] public static partial Demo.D Apply(Demo.P source); }
            }
            """);
        Assert.Equal("xy", Result(namespaces));
    }

    [Fact]
    public void Inherited_properties_are_mapped_and_the_most_derived_wins()
    {
        var run = Build("""
            public class BaseP { public int Id { get; set; } public string Tag { get; set; } = "base"; }
            public class P : BaseP { public string Name { get; set; } = ""; public new string Tag { get; set; } = "derived"; }
            public class BaseD { public int Id { get; set; } public string Tag { get; set; } = ""; }
            public class D : BaseD { public string Name { get; set; } = ""; public new string Tag { get; set; } = ""; }
            public static partial class M { [To] public static partial D Apply(P source); }
            public static class Probe { public static string Run() { var d = M.Apply(new P { Id = 7, Name = "x" }); return d.Id + "|" + d.Name + "|" + d.Tag + "|" + ((BaseD)d).Tag; } }
            """);
        Assert.DoesNotContain("ML0006", run.Warnings);
        Assert.Equal("7|x|derived|", Result(run));
    }

    [Fact]
    public void Nullable_nested_object_keeps_constructor_and_initializer_members()
    {
        var run = Build("""
            public record A(string City) { public string Zip { get; init; } = ""; }
            public record AD(string City) { public string Zip { get; init; } = ""; }
            public record P(A? Addr); public record D(AD? Addr);
            public static partial class M { [To] public static partial D Apply(P source); }
            public static class Probe { public static string Run() => M.Apply(new P(new A("c") { Zip = "z" })).Addr!.Zip + "|" + (M.Apply(new P(null)).Addr == null); }
            """);
        Assert.Equal("z|True", Result(run));
    }

    [Fact]
    public void Null_source_is_guarded_at_every_depth()
    {
        var run = Build("""
            public record I(int V); public record ID(int V);
            public record A(List<I> Items); public record AD(List<ID> Items);
            public record P(List<I>? Own, A? Addr, A? Deep); public record D(List<ID>? Own, AD? Addr, AD? Deep);
            public static partial class M { [To] public static partial D Apply(P source); }
            public static class Probe
            {
                public static string Run()
                {
                    var empty = M.Apply(new P(null, null, null));
                    var full = M.Apply(new P(new() { new(1) }, new A(new() { new(2) }), null));
                    return (empty.Own == null) + "|" + (empty.Addr == null) + "|" + full.Own![0].V + "|" + full.Addr!.Items[0].V + "|" + (full.Deep == null);
                }
            }
            """);
        Assert.Equal("True|True|1|2|True", Result(run));
    }

    [Fact]
    public void Nullable_to_non_nullable_is_a_diagnostic_unless_the_destination_is_oblivious()
    {
        var list = Build("""
            public record I(int V); public record ID(int V);
            public record P(List<I>? Items); public record D(List<ID> Items);
            public static partial class M { [To] public static partial D Apply(P source); }
            """);
        Assert.Contains("ML0004", list.Errors);

        var text = Build("""
            public record P(string? Name); public record D(string Name);
            public static partial class M { [To] public static partial D Apply(P source); }
            """);
        Assert.Contains("ML0004", text.Errors);

        var oblivious = Build("""
            public record I(int V); public record ID(int V);
            public record P(List<I>? Items, string? Name);
            #nullable disable
            public class D { public List<ID> Items { get; set; } public string Name { get; set; } }
            #nullable enable
            public static partial class M { [To] public static partial D Apply(P source); }
            public static class Probe { public static string Run() { var d = M.Apply(new P(null, null)); return (d.Items == null) + "|" + (d.Name == null); } }
            """);
        Assert.Equal("True|True", Result(oblivious));
    }

    [Fact]
    public void Generated_code_keeps_reference_nullability_without_compiler_warnings()
    {
        var run = Build("""
            public record I(int V); public record ID(int V);
            public record P(List<I?> V); public record D(List<ID?> V);
            public static partial class M
            {
                [To] public static partial D? Apply(P? source);
                [To] public static partial ID? Item(I? source);
            }
            public static class Probe
            {
                public static string Run()
                {
                    var d = M.Apply(new P(new() { null, new(2) }))!;
                    return (M.Apply(null) == null) + "|" + (d.V[0] == null) + "|" + d.V[1]!.V + "|" + (M.Item(null) == null);
                }
            }
            """);
        Assert.Equal("True|True|2|True", Result(run));
        Assert.Equal("", run.Build);
    }

    [Fact]
    public void Nullable_struct_is_mapped_when_present_and_rejected_for_a_non_nullable_destination()
    {
        var run = Build("""
            public record struct Pt(int X); public record struct PtD(int X);
            public record P(Pt? At, int? N); public record D(PtD? At, int? N);
            public static partial class M { [To] public static partial D Apply(P source); }
            public static class Probe
            {
                public static string Run()
                {
                    var some = M.Apply(new P(new Pt(3), 4));
                    var none = M.Apply(new P(null, null));
                    return some.At!.Value.X + "|" + some.N + "|" + (none.At == null) + "|" + (none.N == null);
                }
            }
            """);
        Assert.Equal("3|4|True|True", Result(run));

        var bad = Build("""
            public record struct Pt(int X); public record struct PtD(int X);
            public record P(Pt? At); public record D(PtD At);
            public static partial class M { [To] public static partial D Apply(P source); }
            """);
        Assert.Contains("ML0004", bad.Errors);
        Assert.DoesNotContain("capacity", bad.Errors);
        Assert.DoesNotContain("Add a public value", bad.Errors);
    }

    [Fact]
    public void Collections_other_than_list_are_a_diagnostic()
    {
        foreach (var members in new[]
        {
            "Dictionary<string, I> V", "I[] V", "IReadOnlyList<I> V", "HashSet<I> V",
        })
        {
            var dest = members.Replace("I>", "ID>").Replace("I[]", "ID[]");
            var run = Build($$"""
                public record I(int V); public record ID(int V);
                public record P({{members}}); public record D({{dest}});
                public static partial class M { [To] public static partial D Apply(P source); }
                """);
            Assert.Contains("ML0004", run.Errors);
            Assert.DoesNotContain("capacity", run.Errors);
        }
    }

    [Fact]
    public void Constructor_parameter_defaults_survive_leave_and_missing_sources()
    {
        var left = Build("""
            public record P(string Name);
            public record D(string Name, [Leave] string Channel = "unset");
            public static partial class M { [To] public static partial D Apply(P source); }
            public static class Probe { public static string Run() => M.Apply(new P("x")).Channel; }
            """);
        Assert.DoesNotContain("ML0006", left.Warnings);
        Assert.Equal("unset", Result(left));

        var open = Build("""
            public record P(string Name);
            public record D(string Name, int Retries = 3);
            public static partial class M { [To] public static partial D Apply(P source); }
            public static class Probe { public static string Run() => M.Apply(new P("x")).Retries.ToString(); }
            """);
        Assert.Contains("ML0006", open.Warnings);
        Assert.Contains("D.Retries", open.Warnings);
        Assert.Equal("3", Result(open));

        var middle = Build("""
            public record P(string Name, string Tail);
            public record D(string Name, [Leave] string Mid = "m", string Tail = "t");
            public static partial class M { [To] public static partial D Apply(P source); }
            public static class Probe { public static string Run() { var d = M.Apply(new P("x", "real")); return d.Name + "|" + d.Mid + "|" + d.Tail; } }
            """);
        Assert.Equal("x|m|real", Result(middle));
    }

    [Fact]
    public void Camel_case_constructor_parameter_binds_to_the_source_property()
    {
        var run = Build("""
            public class P { public string Name { get; set; } = ""; public int Age { get; set; } }
            public class D { public D(string name) { Name = name; } public string Name { get; } public int Age { get; set; } }
            public static partial class M { [To] public static partial D Apply(P source); }
            public static class Probe { public static string Run() { var d = M.Apply(new P { Name = "x", Age = 3 }); return d.Name + "|" + d.Age; } }
            """);
        Assert.Equal("", run.Warnings);
        Assert.Equal("x|3", Result(run));

        var exact = Build("""
            public class P { public string Name { get; set; } = "upper"; public string name { get; set; } = "lower"; }
            public class D { public D(string name) { Name = name; } public string Name { get; } }
            public static partial class M { [To] public static partial D Apply(P source); }
            public static class Probe { public static string Run() => M.Apply(new P()).Name; }
            """);
        Assert.Equal("lower", Result(exact));
    }

    [Fact]
    public void Constructor_parameter_without_source_or_default_has_its_own_diagnostic()
    {
        var run = Build("""
            public sealed class Src { public string Name { get; init; } = ""; }
            public sealed record Dst(string Name, string Region);
            public static partial class M { [To] public static partial Dst Apply(Src source); }
            """);
        Assert.Contains("ML0011", run.Errors);
        Assert.Contains("Region", run.Errors);
        Assert.DoesNotContain("required", run.Errors);
        Assert.DoesNotContain("ML0005", run.Errors);
        Assert.Contains("https://github.com/DevBastiani/MapLean#ml0011", run.Errors);
    }

    [Fact]
    public void Nullable_nested_object_with_a_skipped_default_parameter_still_compiles()
    {
        var run = Build("""
            public record A(string City, string Zone);
            public record AD(string City, [Leave] string Zone = "z");
            public record Q(A? Addr); public record E(AD? Addr);
            public static partial class M { [To] public static partial E Apply(Q source); }
            public static class Probe { public static string Run() => M.Apply(new Q(new A("c", "other"))).Addr!.Zone; }
            """);
        Assert.Equal("z", Result(run));
    }

    [Fact]
    public void Extension_in_and_keyword_named_parameters_are_supported()
    {
        var run = Build("""
            public record P(string Name); public record D(string Name);
            public static partial class M
            {
                [To] public static partial D ToDto(this P source);
                [To] public static partial D ByIn(in P source);
                [To] public static partial D Keyword(P @class);
            }
            public static class Probe { public static string Run() { var p = new P("x"); return p.ToDto().Name + M.ByIn(in p).Name + M.Keyword(p).Name; } }
            """);
        Assert.Equal("xxx", Result(run));
    }

    [Fact]
    public void Keyword_member_names_are_escaped()
    {
        var run = Build("""
            public record P(string @event, int @class);
            public sealed class D { public string @event { get; init; } = ""; public int @class { get; set; } }
            public static partial class M { [To] public static partial D Apply(P source); }
            public static class Probe { public static string Run() { var d = M.Apply(new P("e", 2)); return d.@event + d.@class; } }
            """);
        Assert.Equal("e2", Result(run));
    }

    [Fact]
    public void Ref_and_out_parameters_are_the_signature_diagnostic()
    {
        foreach (var modifier in new[] { "ref", "out", "ref readonly" })
        {
            var run = Build($$"""
                public record P(string Name); public record D(string Name);
                public static partial class M { [To] public static partial D Apply({{modifier}} P source); }
                """);
            Assert.Contains("ML0003", run.Errors);
        }
    }

    [Fact]
    public void Generic_container_is_supported_and_a_container_without_partial_is_a_diagnostic()
    {
        var generic = Build("""
            public record P(string Name); public record D(string Name);
            public partial class M<T> { [To] public static partial D Apply(P source); }
            public static class Probe { public static string Run() => M<int>.Apply(new P("x")).Name; }
            """);
        Assert.Equal("x", Result(generic));

        var plain = Build("""
            public record P(string Name); public record D(string Name);
            public static class M { [To] public static partial D Apply(P source); }
            """);
        Assert.Contains("ML0007", plain.Errors);
        Assert.Equal("", plain.Generated);
    }
}
