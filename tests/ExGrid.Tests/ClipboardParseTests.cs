using ExGrid.Clipboard;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What the paste event's flavours parse into (ADR-0005/0014): Excel's HTML table
/// preferred for precision, TSV as the fallback, null when nothing tabular is there.
/// </summary>
public class ClipboardParseTests
{
    [Fact] // ADR-0005: plain TSV — tabs are cells, lines are rows
    public void Tsv_parses_into_rows_and_cells()
    {
        var block = ClipboardParse.Parse(html: null, text: "a\tb\r\nc\td\r\n");

        Assert.NotNull(block);
        Assert.Equal([["a", "b"], ["c", "d"]], block);
    }

    [Fact] // ADR-0005 / CP-20: a thousands separator is not a cell boundary
    public void A_comma_is_never_a_cell_boundary()
    {
        Assert.Equal([["1,234"]], ClipboardParse.Parse(null, "1,234"));
        // European notation, where the comma is the decimal point and the dot groups.
        Assert.Equal([["1.234,56"]], ClipboardParse.Parse(null, "1.234,56"));
        Assert.Equal([["USD 1,234.00"]], ClipboardParse.Parse(null, "USD 1,234.00"));
    }

    [Fact] // ADR-0005 / CP-20: the shape a CSV heuristic would read as two columns is one column of money
    public void Lines_of_comma_grouped_numbers_stay_one_column()
    {
        var block = ClipboardParse.Parse(null, "1,234\r\n5,678\r\n");

        Assert.Equal([["1,234"], ["5,678"]], block);
    }

    [Fact] // ADR-0005 / CP-20: the tab still splits, and splits only where it is
    public void A_tab_is_the_only_cell_boundary()
    {
        Assert.Equal([["a,b", "c"]], ClipboardParse.Parse(null, "a,b\tc"));
    }

    [Fact] // Excel always appends a trailing break; it is not an extra empty row
    public void A_trailing_line_break_is_not_a_row()
    {
        Assert.Equal(2, ClipboardParse.Parse(null, "a\r\nb\r\n")!.Count);
        Assert.Equal(2, ClipboardParse.Parse(null, "a\nb")!.Count);
    }

    [Fact] // Excel quotes a cell that holds a tab or a break; reading it back is one cell
    public void A_quoted_cell_reads_back_as_one_cell()
    {
        var block = ClipboardParse.Parse(null, "\"a\tb\"\"c\nd\"\te\r\n");

        Assert.Equal([["a\tb\"c\nd", "e"]], block);
    }

    [Fact] // A short line pads with empty cells, as Excel pads — the block is rectangular
    public void A_ragged_block_is_padded_rectangular()
    {
        var block = ClipboardParse.Parse(null, "a\tb\tc\r\nd\r\n");

        Assert.Equal([["a", "b", "c"], ["d", "", ""]], block);
    }

    [Fact] // ADR-0014: nothing tabular consults no rule
    public void Nothing_tabular_is_null()
    {
        Assert.Null(ClipboardParse.Parse(null, null));
        Assert.Null(ClipboardParse.Parse("", ""));
        Assert.Null(ClipboardParse.Parse("<div>no table here</div>", ""));
    }

    [Fact] // ADR-0005: the HTML flavour is preferred when it holds a table
    public void An_html_table_wins_over_the_text()
    {
        var block = ClipboardParse.Parse(
            "<table><tr><td>h1</td><td>h2</td></tr></table>", "t1\tt2\r\n");

        Assert.Equal([["h1", "h2"]], block);
    }

    [Fact] // ADR-0005: Excel's x:num carries the raw number at full precision
    public void Excels_x_num_attribute_is_the_value()
    {
        var block = ClipboardParse.Parse(
            "<table><tr><td x:num=\"1234.56789\">1,234.57</td><td>text</td></tr></table>", null);

        Assert.Equal([["1234.56789", "text"]], block);
    }

    [Fact] // Wrapping markup and entities decode; a <br> inside a cell is a break in its value
    public void Cell_markup_is_stripped_and_entities_decode()
    {
        var block = ClipboardParse.Parse(
            "<table><tbody><tr><td><b>a&amp;b</b></td><td>x<br/>y</td></tr></tbody></table>", null);

        Assert.Equal([["a&b", "x\ny"]], block);
    }

    [Fact] // The grid's own html flavour round-trips
    public void The_grids_own_html_round_trips()
    {
        var block = ClipboardParse.Parse(
            "<table><tr><td>100.5</td><td>2026-01-05T00:00:00</td></tr><tr><td>-7</td><td>2026-01-06T00:00:00</td></tr></table>",
            null);

        Assert.Equal([["100.5", "2026-01-05T00:00:00"], ["-7", "2026-01-06T00:00:00"]], block);
    }


    [Fact] // ADR-0005: &nbsp; is value, not markup — Excel preserves a cell's own spaces with it
    public void Nbsp_edges_survive_where_markup_whitespace_is_trimmed()
    {
        var block = ClipboardParse.Parse(
            "<table><tr><td>\n  a&nbsp;&nbsp;\n</td><td>&nbsp;b</td></tr></table>", null);

        // The markup's pretty-printing is trimmed; the value's own spaces are not.
        Assert.Equal("a  ", block![0][0]);
        Assert.Equal(" b", block[0][1]);
    }
}
