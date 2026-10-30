using ExGrid.Columns;
using ExGrid.Data;
using Xunit;

namespace ExGrid.Tests;

public sealed class ShapedGridTests
{
    // The format code's own cases, what it reads, refuses and writes, are shared with the Script Core:
    // tests/cases/shaped/format-code.json, run by Cases/FormatCodeCaseTests.cs (ADR-0370 section 2).

    private static readonly DataShape Shape = new(
    [
        new("desk", SnapshotKind.Text),
        new("notional", SnapshotKind.Decimal),
        new("rate", SnapshotKind.Double),
        new("count", SnapshotKind.Integer),
        new("tradeDate", SnapshotKind.Date),
        new("hedged", SnapshotKind.Boolean),
    ]);

    private static GridViewSpecReading Read(string json) => GridViewSpec.Read(json, name => name == "trades" ? Shape : null);

    [Theory] // ADR-0342: a decimal or double column must carry its format, refused naming the column
    [InlineData("""{"data": "trades", "columns": [{"column": "notional"}]}""", "columns[0].format",
        "Column 'notional' is a decimal and needs a format, such as \"#,##0.00\", \"0.00%\" or \"0.00000\": its places are chosen for what it holds, never guessed from the type (ADR-0342).")]
    [InlineData("""{"data": "trades", "columns": [{"column": "rate", "format": "yyyy-mm-dd"}]}""", "columns[0].format",
        "Column 'rate' is a number, and \"yyyy-mm-dd\" formats dates.")]
    [InlineData("""{"data": "trades", "columns": [{"column": "desk", "format": "0"}]}""", "columns[0].format",
        "Column 'desk' is text and shows as it is; it takes no format.")]
    [InlineData("""{"data": "trades", "columns": [{"column": "pnl"}]}""", "columns[0].column",
        "The grid shows column 'pnl', which input 'trades' does not declare.")]
    [InlineData("""{"data": "orders", "columns": [{"column": "desk"}]}""", "data",
        "The grid shows input 'orders', which the Board does not declare.")]
    [InlineData("""{"data": "trades", "columns": [{"column": "desk", "width": 10}]}""", "columns",
        "Column 'desk': a width of 10 is below the least a column is drawn at (40).")]
    [InlineData("""{"data": "trades", "columns": [{"column": "desk"}], "colour": "red"}""", "colour",
        "A grid's spec has no member 'colour'.")]
    public void A_spec_that_cannot_be_drawn_is_refused_by_path(string json, string path, string message)
    {
        var refused = Assert.Throws<BoardRefusedException>(() => Read(json));
        Assert.Equal(path, refused.Path);
        Assert.Equal(message, refused.Message);
    }

    [Fact] // ADR-0342: a member written twice is never settled by whichever came last
    public void A_member_written_twice_is_refused()
    {
        var refused = Assert.Throws<BoardRefusedException>(() => Read("""{"data": "trades", "data": "orders", "columns": [{"column": "desk"}]}"""));
        Assert.StartsWith("The grid's spec is not JSON", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0342 with ADR-0207: sorts and filters are the View State's, read by its reader
    public void A_sort_on_a_column_the_input_lacks_is_refused_by_the_view_states_reader()
    {
        var refused = Assert.Throws<BoardRefusedException>(() => Read(
            """{"data": "trades", "columns": [{"column": "desk"}], "sorts": [{"column": "pnl", "direction": "Ascending"}]}"""));
        Assert.StartsWith("Sort level 1 names column 'pnl'", refused.Message, StringComparison.Ordinal);
    }

    private static Snapshot Trades()
    {
        var builder = new SnapshotColumnsBuilder();
        var desk = builder.Text("desk");
        var notional = builder.Decimal("notional");
        var rate = builder.Double("rate");
        var count = builder.Integer("count");
        var date = builder.Date("tradeDate");
        var hedged = builder.Boolean("hedged");
        desk.Append("Rates"); notional.Append(1234567.5m); rate.Append(1.08734); count.Append(12345L); date.Append(new DateOnly(2026, 10, 20)); hedged.Append(true);
        desk.Append("FX"); notional.Append(-250m); rate.Append(0.5); count.Append(2L); date.Append(new DateOnly(2026, 10, 19)); hedged.Append(false);
        desk.Append("Credit"); notional.Append(99.99m); rate.Append(2.25); count.Append(7L); date.Append(new DateOnly(2026, 10, 18)); hedged.Append((bool?)null);
        return builder.Build();
    }

    [Fact] // ADR-0342: one column per declared column the view shows, its row a position in the source
    public void The_grid_is_built_from_the_spec_over_the_snapshot()
    {
        var spec = Read("""
            {"data": "trades",
             "columns": [{"column": "desk", "header": "Desk", "pinned": true, "width": 90},
                         {"column": "notional", "format": "#,##0.00"},
                         {"column": "rate", "format": "0.00000"},
                         {"column": "count"},
                         {"column": "tradeDate"}],
             "sorts": [{"column": "notional", "direction": "Descending"}]}
            """);
        var grid = ShapedGrid.Of(Trades(), spec);

        Assert.Equal(["desk", "notional", "rate", "count", "tradeDate"], grid.Columns.Select(c => c.Name));
        Assert.Equal("Desk", grid.Columns[0].Header);
        Assert.Equal(ColumnWidth.Fixed(90), grid.Columns[0].Width.Width);
        Assert.Equal(1, grid.PinnedColumnCount);
        Assert.Equal([ColumnType.Text, ColumnType.Number, ColumnType.Number, ColumnType.Number, ColumnType.Date], grid.Columns.Select(c => c.Type));

        // The source executes the spec's sort, with GridSource.From's semantics.
        var rows = grid.Source.Window;
        Assert.Equal(["Rates", "Credit", "FX"], rows.Select(r => (string)grid.Columns[0].Value(r)!));
        var first = rows[0];
        Assert.Equal("1,234,567.50", grid.Columns[1].Info.TextOf(first));
        Assert.Equal("1.08734", grid.Columns[2].Info.TextOf(first));
        Assert.Equal("12,345", grid.Columns[3].Info.TextOf(first));
        Assert.Equal("2026-10-20", grid.Columns[4].Info.TextOf(first));
    }

    [Fact] // ADR-0342: a date holding a time of day shows it by default rather than hide it
    public void A_date_with_a_time_of_day_shows_the_time_by_default()
    {
        var builder = new SnapshotColumnsBuilder();
        builder.Date("tradeDate").Append(new DateTime(2026, 10, 20, 9, 30, 0));
        var shape = new DataShape([new("tradeDate", SnapshotKind.Date)]);
        var spec = GridViewSpec.Read("""{"data": "t", "columns": [{"column": "tradeDate"}]}""", _ => shape);

        var grid = ShapedGrid.Of(builder.Build(), spec);

        Assert.Equal("2026-10-20 09:30:00", grid.Columns[0].Info.TextOf(grid.Source.Window[0]));
    }

    [Fact] // ADR-0341: the source's shape is checked before any row is drawn
    public void A_snapshot_of_another_shape_is_refused()
    {
        var builder = new SnapshotColumnsBuilder();
        builder.Double("notional").Append(1.0);
        var spec = Read("""{"data": "trades", "columns": [{"column": "notional", "format": "0.00"}]}""");

        var refused = Assert.Throws<BoardRefusedException>(() => ShapedGrid.Of(builder.Build(), spec));
        Assert.Equal("Input 'trades', column 'notional': declared decimal, and its source holds double.", refused.Message);
    }
}
