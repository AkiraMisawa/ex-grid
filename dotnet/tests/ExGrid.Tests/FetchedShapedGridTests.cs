using ExGrid.Data;
using Xunit;

namespace ExGrid.Tests;

/// <summary>A grid built from a shape whose rows are fetched a page at a time (ADR-0343).</summary>
public sealed class FetchedShapedGridTests
{
    private static readonly DataShape Shape = new([new("desk", SnapshotKind.Text), new("day", SnapshotKind.Date), new("pnl", SnapshotKind.Decimal)]);

    private static GridViewSpecReading Spec(string columns = """[{"column": "desk"}, {"column": "day"}, {"column": "pnl", "format": "#,##0"}]""")
        => GridViewSpec.Read($$"""{"data": "trades", "columns": {{columns}}, "sorts": [{"column": "pnl", "direction": "Descending"}]}""", _ => Shape);

    // A server's page: the rows at [start, start + count) of a result of total rows, desk "D{i}".
    private static Snapshot Page(int start, int count, bool withTime = false)
    {
        var builder = new SnapshotColumnsBuilder();
        var desk = builder.Text("desk");
        var day = builder.Date("day");
        var pnl = builder.Decimal("pnl");
        for (var i = start; i < start + count; i++)
        {
            desk.Append($"D{i}");
            if (withTime)
                day.Append(new DateTime(2026, 10, 21, 9, 30, 0));
            else
                day.Append(new DateOnly(2026, 10, 21));
            pnl.Append(1000m * i);
        }
        return builder.Build();
    }

    [Fact] // ADR-0343: each fetch is handed the grid's query, and the rows come back as a Snapshot page
    public async Task Rows_are_fetched_as_snapshot_pages_under_the_specs_sort()
    {
        var asked = new List<GridQuery>();
        var grid = ShapedGrid.Fetch(Spec(), (query, _) =>
        {
            asked.Add(query);
            return ValueTask.FromResult(new ShapedPage(Page(query.Range.Start, Math.Min(query.Range.Count, 1_000 - query.Range.Start)), query.Range.Start, 1_000));
        }, readAheadRows: 0);

        await grid.Source.OnRangeNeededAsync(new RowRange(500, 3));

        // The first page, fetched as the grid is made, and the range asked; each under the spec's sort.
        Assert.Equal([new RowRange(0, 100), new RowRange(500, 3)], asked.Select(q => q.Range));
        Assert.All(asked, q => Assert.Equal(new SortSpec("pnl", SortDirection.Descending), Assert.Single(q.Sorts)));
        Assert.Equal(500, grid.Source.WindowStart);
        Assert.Equal(1_000, grid.Source.TotalCount);
        var rows = grid.Source.Window;
        Assert.Equal(["D500", "D501", "D502"], rows.Select(r => (string)grid.Columns[0].Value(r)!));
        Assert.Equal([500, 501, 502], rows.Select(r => r.Position));
        Assert.Equal("501,000", grid.Columns[2].Info.TextOf(rows[1]));
        Assert.All(rows, r => Assert.Same(rows[0].Page, r.Page));
    }

    [Fact] // ADR-0343: a shape does not say whether a page holds a time, so a date shows it by default
    public async Task A_date_with_no_format_shows_its_time()
    {
        var grid = ShapedGrid.Fetch(Spec(), (query, _) => ValueTask.FromResult(new ShapedPage(Page(0, 1, withTime: true), 0, 1)));
        await grid.Source.OnRangeNeededAsync(new RowRange(0, 1));

        Assert.Equal("2026-10-21 09:30:00", grid.Columns[1].Info.TextOf(grid.Source.Window[0]));

        var dated = ShapedGrid.Fetch(Spec("""[{"column": "day", "format": "yyyy-mm-dd"}]"""),
            (query, _) => ValueTask.FromResult(new ShapedPage(Page(0, 1, withTime: true), 0, 1)));
        await dated.Source.OnRangeNeededAsync(new RowRange(0, 1));
        Assert.Equal("2026-10-21", dated.Columns[0].Info.TextOf(dated.Source.Window[0]));
    }

    [Fact] // ADR-0343 / spine rule 1: a page of another shape fails its fetch, naming the input, and is never shown
    public async Task A_page_of_another_shape_fails_its_fetch()
    {
        var builder = new SnapshotColumnsBuilder();
        builder.Text("desk").Append("Rates");
        builder.Text("day").Append("2026-10-21");
        builder.Decimal("pnl").Append(1m);
        var wrong = builder.Build();
        var grid = ShapedGrid.Fetch(Spec(), (query, _) => ValueTask.FromResult(new ShapedPage(wrong, 0, 1)));
        Exception? failed = null;
        grid.Source.FetchFailed += e => failed = e;

        await grid.Source.OnRangeNeededAsync(new RowRange(0, 1));

        var refused = Assert.IsType<BoardRefusedException>(failed);
        Assert.Equal("trades", refused.Path);
        Assert.Equal("Input 'trades', column 'day': declared date, and a page its source answered holds text.", refused.Message);
        Assert.Empty(grid.Source.Window);
    }

    [Fact] // ADR-0343: a page claiming rows past its own count is refused where it is made
    public void A_page_running_past_its_count_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShapedPage(Page(0, 3), 5, 6));
}
