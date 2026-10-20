using System.Globalization;
using ExGrid.Columns;
using ExGrid.Data;
using Xunit;

namespace ExGrid.Tests;

public sealed class ShapedGridTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static string Formatted(string code, object value)
    {
        Assert.True(FormatCode.TryParse(code, out var format, out var reason), reason);
        return format.Format(value, Invariant);
    }

    [Theory] // ADR-0342: an Excel format code in the invariant spelling (ADR-0120)
    [InlineData("#,##0.00", 1234567.891, "1,234,567.89")]
    [InlineData("#,##0.00", -1234.5, "-1,234.50")]
    [InlineData("0.00%", 0.1234, "12.34%")]
    [InlineData("0.00000", 1.08734, "1.08734")]
    [InlineData("0.00E+00", 1234.5, "1.23E+03")]
    [InlineData("0.00E+00", 1.5e-30, "1.50E-30")]
    [InlineData("#,##0,", 1234567.0, "1,235")]
    [InlineData("#,##0.00;(#,##0.00)", -5.0, "(5.00)")]
    [InlineData("#,##0.00;(#,##0.00);\"-\"", 0.0, "-")]
    [InlineData("\"$\"#,##0.00", 1.5, "$1.50")]
    [InlineData("$#,##0.00", 1.5, "$1.50")]
    [InlineData("0.0 \"bp\"", 12.25, "12.3 bp")]
    [InlineData("0.00\" p.a.\"", 4.5, "4.50 p.a.")]
    [InlineData("#,##0.00;", -1500.0, "")]
    [InlineData("#,##0;-#,##0;", 0.0, "")]
    public void A_number_shows_as_its_format_code_says(string code, double value, string text) =>
        Assert.Equal(text, Formatted(code, value));

    [Fact] // ADR-0342: a double keeps 15 significant digits, as Excel shows 1.005 in 0.00
    public void A_double_rounds_as_excel_rounds_it() => Assert.Equal("1.01", Formatted("0.00", 1.005));

    [Fact] // ADR-0342, ExSheet's FMT-063: a negative that rounds to zero in one section shows no minus
    public void A_negative_rounding_to_zero_shows_no_minus() => Assert.Equal("0.00", Formatted("0.00", -0.001));

    [Fact] // ADR-0342: the section is chosen by the value's sign before rounding, as Excel chooses it
    public void A_negative_rounding_to_zero_takes_the_negative_section() =>
        Assert.Equal("(0.00)", Formatted("0.00;(0.00)", -0.001));

    [Theory] // ADR-0342: decimals and integers are formatted exactly
    [InlineData("#,##0.00", "12345678901234567890.125", "12,345,678,901,234,567,890.13")]
    public void A_decimal_shows_as_its_format_code_says(string code, string value, string text) =>
        Assert.Equal(text, Formatted(code, decimal.Parse(value, Invariant)));

    [Theory] // ADR-0342: dates, with m read as minutes after an hour and before a second
    [InlineData("yyyy-mm-dd", "2026-10-20 09:05:07", "2026-10-20")]
    [InlineData("yyyy-mm-dd hh:mm:ss", "2026-10-20 09:05:07", "2026-10-20 09:05:07")]
    [InlineData("d mmm yyyy", "2026-10-20 09:05:07", "20 Oct 2026")]
    [InlineData("m/d/yy", "2026-10-20 09:05:07", "10/20/26")]
    [InlineData("h:mm AM/PM", "2026-10-20 15:05:07", "3:05 PM")]
    [InlineData("mm:ss", "2026-10-20 15:05:07", "05:07")]
    [InlineData("d", "2026-10-20 15:05:07", "20")]
    [InlineData("h", "2026-10-20 15:05:07", "15")]
    public void A_date_shows_as_its_format_code_says(string code, string value, string text) =>
        Assert.Equal(text, Formatted(code, DateTime.Parse(value, Invariant)));

    [Theory] // ADR-0342: a code outside the subset is refused rather than shown some other way
    [InlineData("", "is empty")]
    [InlineData("General", "holds the letter 'G' unquoted; quote text as \"text\"")]
    [InlineData("[Red]0.00", "holds a part in brackets (a colour, a condition or a locale), which a grid does not show")]
    [InlineData("0.00_)", "holds '_' spacing, which a grid does not show")]
    [InlineData("# ?/?", "holds the placeholder '?', which a grid does not show")]
    [InlineData("0;@", "holds a text placeholder '@', and a number column holds no text")]
    [InlineData("\"open", "opens a quotation that it does not close")]
    [InlineData("hh:mm:ss.000", "holds fractional seconds, which a grid does not show")]
    public void A_code_outside_the_subset_is_refused(string code, string reason)
    {
        Assert.False(FormatCode.TryParse(code, out _, out var said));
        Assert.Equal(reason, said);
    }

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
