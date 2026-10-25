using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// Show Details is a Source Version question (ADR-0151): resolved from the detached cell the user
/// acted on — its row's Items, its value column's Items, the Hidden Items and the Source Version —
/// it is the question the engine itself asks for the same cell. That it stays answerable after any
/// number of later layouts, while the Pivot Source holds that version, is a shared case
/// (<c>tests/cases/pivot/report-source.json</c>), as are the questions and records themselves.
/// </summary>
public class DetailsQueryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> Layouts() => [.. LayoutsByName.Keys];

    private static readonly Dictionary<string, PivotLayout> LayoutsByName = new()
    {
        ["compact"] = new() { Rows = [P("Region"), P("Product")], Columns = [P("Online")], Values = [Sum("Amount")] },
        ["tabular, values on rows"] = new()
        {
            Rows = [P("Region"), P("Product")], Columns = [P("Online")], Form = PivotReportForm.Tabular,
            Values = [Sum("Amount"), Value("Quantity", PivotAggregation.Count)], ValuesAxis = PivotAxis.Rows,
        },
        ["hidden items and a report filter"] = new()
        {
            Filters = [P("Online") with { HiddenItems = [PivotItemKey.Boolean(false)] }],
            Rows = [P("Region") with { HiddenItems = [PivotItemKey.Text("North")] }],
            Columns = [P("Product")],
            Values = [Sum("Amount"), Value("Amount", PivotAggregation.Max)],
        },
        ["no rows"] = new() { Columns = [P("Product")], Values = [Sum("Amount")] },
    };

    [Theory] // ADR-0151/0063: the question resolved from a detached display row is the engine's own question for that cell, and answers the same records
    [MemberData(nameof(Layouts))]
    public async Task A_cells_question_resolved_from_the_window_is_the_engines(string name)
    {
        var layout = LayoutsByName[name];
        var data = PivotSource.From(Sales, Fields);
        await using var source = PivotReportSource.From(data);
        var window = await source.WindowAsync(new("all", layout, PivotReportSettings.Invariant, new(0, 1_000)), Ct);
        var query = PivotQuery.For(layout);
        var report = PivotEngine.Report(PivotEngine.Cube(query, await data.AggregateAsync(query, Ct), data.Fields), layout);
        Assert.Equal(report.Rows.Count, window.Rows!.Count);

        for (var r = 0; r < report.Rows.Count; r++)
        {
            for (var c = -1; c < report.ValueColumns.Count; c++)
            {
                var expected = report.DetailsQuery(report.Rows[r], c);
                var resolved = window.Metadata!.DetailsQuery(window.Rows[r], c);
                Assert.Equal(expected, resolved);
                var records = await source.DetailsAsync(resolved, Ct);
                var engine = await data.DetailsAsync(expected, Ct);
                Assert.Equal(engine.Total, records.Total);
                Assert.Equal(engine.Records.Select(record => record.Values), records.Records.Select(record => record.Values));
            }
        }
    }
}
