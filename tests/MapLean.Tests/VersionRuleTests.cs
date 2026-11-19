using System.Text.RegularExpressions;
using Xunit;

namespace MapLean.Tests;

public sealed class VersionRuleTests
{
    [Fact]
    public void The_rule_beside_version_occurs_once()
    {
        var root = Root();
        var project = File.ReadAllText(Path.Combine(root, "src", "MapLean", "MapLean.csproj"));
        var match = Regex.Match(
            project,
            @"<!--\s*(?<rule>.+?)\s*-->\s*<Version>\s*(?<version>[^<]+)\s*</Version>",
            RegexOptions.Singleline);
        Assert.True(match.Success);
        var rule = match.Groups["rule"].Value.Trim();
        var version = match.Groups["version"].Value.Trim();
        Assert.Equal("0.0.1", version);
        Assert.Contains("semver.org item 4", rule, StringComparison.Ordinal);
        Assert.Contains("patch", rule, StringComparison.Ordinal);
        Assert.Contains("minor", rule, StringComparison.Ordinal);
        Assert.Contains("public name", rule, StringComparison.Ordinal);
        Assert.Contains("diagnostic id", rule, StringComparison.Ordinal);
        Assert.Contains("observable map result", rule, StringComparison.Ordinal);

        var copies = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (Skip(file))
                continue;
            if (!file.EndsWith(".cs", StringComparison.Ordinal) &&
                !file.EndsWith(".csproj", StringComparison.Ordinal) &&
                !file.EndsWith(".md", StringComparison.Ordinal) &&
                !file.EndsWith(".py", StringComparison.Ordinal) &&
                !file.EndsWith(".props", StringComparison.Ordinal))
                continue;
            copies += Count(File.ReadAllText(file), rule);
        }

        Assert.Equal(1, copies);
    }

    private static int Count(string text, string rule)
    {
        var copies = 0;
        var index = 0;
        while ((index = text.IndexOf(rule, index, StringComparison.Ordinal)) >= 0)
        {
            copies++;
            index += rule.Length;
        }

        return copies;
    }

    private static bool Skip(string file)
        => file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || file.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MapLean.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root was not found");
    }
}
