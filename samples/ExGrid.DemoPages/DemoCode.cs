using System.Collections.Concurrent;

namespace ExGrid.DemoPages;

/// <summary>
/// The code a page shows under "The code", read from the page's own source, which
/// ExGrid.DemoPages.csproj embeds: what a page shows is what it runs, so the two cannot drift
/// apart. A region is the lines between <c>#region The code: name</c> and the next
/// <c>#endregion</c> — in a Razor file's markup, the same words inside a Razor comment — less the
/// indentation they share.
/// </summary>
public static class DemoCode
{
    private const string Start = "#region The code: ";

    private static readonly ConcurrentDictionary<(string File, string Region), string> Regions = new();

    /// <summary>The region <paramref name="region"/> of the embedded file <paramref name="file"/>.</summary>
    /// <exception cref="InvalidOperationException">The file is not embedded, or has no such region:
    /// a page whose code cannot be shown says so rather than show something else.</exception>
    public static string Region(string file, string region) => Regions.GetOrAdd((file, region), Read);

    private static string Read((string File, string Region) at)
    {
        using var stream = typeof(DemoCode).Assembly.GetManifestResourceStream("DemoCode/" + at.File)
            ?? throw new InvalidOperationException($"{at.File} is not embedded; list it under EmbeddedResource in ExGrid.DemoPages.csproj.");
        using var reader = new StreamReader(stream);
        var lines = reader.ReadToEnd().ReplaceLineEndings("\n").Split('\n');
        var start = Array.FindIndex(lines, line => Marker(line) == Start + at.Region);
        if (start < 0)
            throw new InvalidOperationException($"{at.File} has no region '{Start}{at.Region}'.");
        var end = Array.FindIndex(lines, start + 1, line => Marker(line).StartsWith("#endregion", StringComparison.Ordinal));
        if (end < 0)
            throw new InvalidOperationException($"The region '{Start}{at.Region}' of {at.File} has no #endregion.");
        var body = lines[(start + 1)..end];
        var indent = body.Where(line => line.Trim().Length > 0).Select(line => line.Length - line.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join('\n', body.Select(line => line.Length >= indent ? line[indent..] : line.TrimStart()));
    }

    // A marker line, as C# writes it or inside a Razor comment: @* #region The code: name *@.
    private static string Marker(string line)
    {
        var text = line.Trim();
        return text.StartsWith("@*", StringComparison.Ordinal) && text.EndsWith("*@", StringComparison.Ordinal)
            ? text[2..^2].Trim()
            : text;
    }
}
