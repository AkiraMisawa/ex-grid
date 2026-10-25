using System.Text.Json;

namespace ExChart.Engine.Tests.Cases;

/// <summary>
/// Runs the shared cases of <c>tests/cases/chart/</c> against the C# engine (ADR-0370, section 3); Vitest
/// runs the same files against <c>@ex-grid/chart</c>. <c>tests/cases/README.md</c> says what a file holds,
/// and <see cref="ChartCase"/> reads one case.
/// </summary>
public class ChartCaseTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "cases", "chart");

    /// <summary>Every case of every chart file, as (file, name) rows.</summary>
    public static TheoryData<string, string> Cases()
    {
        var rows = new TheoryData<string, string>();
        foreach (var file in Directory.GetFiles(Root, "*.json").Select(Path.GetFileName).Order(StringComparer.Ordinal))
        {
            var document = Read(file!);
            if (document.GetProperty("kind").GetString() != "chart")
                throw new InvalidOperationException($"chart/{file} is of kind {document.GetProperty("kind")}, and only chart files live there.");
            foreach (var entry in document.GetProperty("cases").EnumerateArray())
                rows.Add(file!, entry.GetProperty("name").GetString()!);
        }
        return rows;
    }

    [Theory] // ADR-0370: the layout's cases hold on the C# engine before the Script Core takes them
    [MemberData(nameof(Cases))]
    public void A_shared_chart_case_holds(string file, string name)
    {
        var document = Read(file);
        var matches = document.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("name").GetString() == name).ToList();
        Assert.True(matches.Count == 1, $"chart/{file} holds {matches.Count} cases named \"{name}\".");
        new ChartCase(document, matches[0]).Run();
    }

    private static JsonElement Read(string file)
    {
        using var stream = File.OpenRead(Path.Combine(Root, file));
        using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow });
        return document.RootElement.Clone();
    }
}
