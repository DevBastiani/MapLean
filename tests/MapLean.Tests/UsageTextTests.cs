using Xunit;

namespace MapLean.Tests;

public sealed class UsageTextTests
{
    private static readonly string[] Sentences =
    [
        "Put `[To]` on a partial method.",
        "The method takes the source and returns the destination.",
    ];

    [Fact]
    public void The_usage_text_occurs_once()
    {
        var root = Root();
        var self = Path.Combine(root, "tests", "MapLean.Tests", "UsageTextTests.cs");
        foreach (var sentence in Sentences)
        {
            var found = new List<string>();
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (Skip(file) || string.Equals(file, self, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!file.EndsWith(".md", StringComparison.Ordinal) &&
                    !file.EndsWith(".mdc", StringComparison.Ordinal) &&
                    !file.EndsWith(".cs", StringComparison.Ordinal))
                    continue;
                var text = File.ReadAllText(file);
                var index = 0;
                while ((index = text.IndexOf(sentence, index, StringComparison.Ordinal)) >= 0)
                {
                    found.Add(Path.GetRelativePath(root, file));
                    index += sentence.Length;
                }
            }

            Assert.True(found.Count == 1, $"'{sentence}' occurs {found.Count} times: {string.Join(", ", found)}");
            Assert.Equal("README.md", found[0]);
        }
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
