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
}
