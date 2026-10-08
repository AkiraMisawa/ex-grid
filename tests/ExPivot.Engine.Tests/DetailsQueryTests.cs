using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// Show Details is a Source Version question (ADR-0151): resolved from the detached cell the user
/// acted on — its row's Items, its value column's Items, the Hidden Items and the Source Version —
/// it is the question the engine itself asks for the same cell, and it stays answerable after any
/// number of later layouts, while the data provider holds that version.
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

    private sealed record Trade(long Id, string Desk, decimal Amount);

    [Fact] // ADR-0151 (PV-14): Details keep answering after layouts that evicted their Report Version, and are refused — as the data having changed — only once the provider no longer holds their Source Version
    public async Task Details_outlive_the_report_version_and_end_with_their_source_version()
    {
        var fields = PivotFields.Of<Trade>().Key("Id", t => t.Id).Text("Desk", t => t.Desk).Number("Amount", t => t.Amount);
        var trades = new[] { new Trade(1, "East", 10m), new Trade(2, "East", 20m), new Trade(3, "West", 30m) };
        var data = PivotSource.From(trades, fields);
        await using var source = PivotReportSource.From(data);
        var client = new PivotReportClient(source);
        var layout = new PivotLayout { Rows = [P("Desk")], Values = [Sum("Amount")] };
        Assert.True(await client.ReadAsync(layout, PivotReportSettings.Invariant, new(0, 10), cancellationToken: Ct));
        var shown = client.Current!;
        var details = shown.Metadata.DetailsQuery(shown.Rows[0], 0, start: 1, count: 1);

        // Two layout gestures: two Report Versions, and the one the cell was shown in is evicted.
        Assert.True(await client.ReadAsync(layout with { Rows = [P("Desk") with { Sort = PivotSort.Descending }] },
            PivotReportSettings.Invariant, new(0, 10), cancellationToken: Ct, markChanges: false));
        Assert.True(await client.ReadAsync(layout with { Form = PivotReportForm.Tabular },
            PivotReportSettings.Invariant, new(0, 10), cancellationToken: Ct, markChanges: false));
        var copy = await source.CopyAsync(new(shown.Metadata.Version, [shown.Metadata.ValueColumns[0].Name], [new(0, 0, 0, 0)]), Ct);
        Assert.Equal(PivotReportRefusalKind.ReportVersionNotHeld, copy.Refusal!.Kind);

        var page = await source.DetailsAsync(details, Ct);
        Assert.False(page.IsRefused);
        Assert.Equal(2, page.Total);
        Assert.Equal(20m, Assert.Single(page.Records).Values[^1]);

        // Data that moved on further than the provider holds: refused, naming the data's change.
        for (var i = 0; i < SnapshotPivotSource.AnswersHeld + 1; i++)
        {
            data.Apply(fields.Batch(changed: [trades[2] = trades[2] with { Amount = trades[2].Amount + 1 }]));
            Assert.True(await client.ReadAsync(layout, PivotReportSettings.Invariant, new(0, 10), cancellationToken: Ct));
        }
        var refused = await source.DetailsAsync(details, Ct);
        Assert.True(refused.IsRefused);
        Assert.Equal(PivotSourceRefusalKind.SourceVersionNotHeld, refused.Refusal!.Kind);
    }
}
