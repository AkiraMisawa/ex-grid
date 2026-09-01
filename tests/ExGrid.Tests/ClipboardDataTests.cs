using ExGrid.Clipboard;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The two-format assembly (ADR-0005): displayed text as TSV, raw values as an HTML
/// table, one operation. The plan under test comes from the real rules, so the
/// ordering asserted here is the ordering copies actually get.
/// </summary>
public class ClipboardDataTests
{
    private static readonly GridExtent Grid = new(100, 26);

    private static string Display(int row, int column) => $"d{row}.{column}";

    private static string Raw(int row, int column) => $"r{row}.{column}";

    [Fact] // ADR-0005: one range emits its rows as lines and its columns as tab-separated cells
    public void A_single_range_becomes_tsv_and_an_html_table()
    {
        var plan = ClipboardRules.PlanCopy(
            GridSelection.Empty.Click(new(1, 2), Grid).ExtendTo(new(2, 3), Grid)).Plan;

        var (text, html) = ClipboardData.Assemble(plan, Display, Raw);

        Assert.Equal("d1.2\td1.3\r\nd2.2\td2.3\r\n", text);
        Assert.Equal(
            "<table><tr><td>r1.2</td><td>r1.3</td></tr><tr><td>r2.2</td><td>r2.3</td></tr></table>",
            html);
    }

    [Fact] // ADR-0011 / CP-9: vertically aligned ranges stack in position order, never creation order
    public void Vertical_segments_stack_in_position_order()
    {
        // Created bottom-first: rows 5 then rows 1-2, same column span.
        var selection = GridSelection.Empty
            .Click(new(5, 0), Grid).ExtendTo(new(5, 1), Grid)
            .ToggleRange(new(1, 0), Grid).ExtendTo(new(2, 1), Grid);
        var plan = ClipboardRules.PlanCopy(selection).Plan;

        var (text, _) = ClipboardData.Assemble(plan, Display, Raw);

        Assert.Equal("d1.0\td1.1\r\nd2.0\td2.1\r\nd5.0\td5.1\r\n", text);
    }

    [Fact] // ADR-0011: horizontally aligned ranges concatenate left to right, gaps closed
    public void Horizontal_segments_concatenate_in_position_order()
    {
        var selection = GridSelection.Empty
            .Click(new(3, 4), Grid).ExtendTo(new(4, 4), Grid)
            .ToggleRange(new(3, 1), Grid).ExtendTo(new(4, 1), Grid);
        var plan = ClipboardRules.PlanCopy(selection).Plan;

        var (text, _) = ClipboardData.Assemble(plan, Display, Raw);

        Assert.Equal("d3.1\td3.4\r\nd4.1\td4.4\r\n", text);
    }

    [Fact] // Excel's TSV convention: a cell holding a tab, a break or a quote is quoted, not mangled
    public void A_cell_holding_a_tab_or_break_is_quoted()
    {
        var plan = ClipboardRules.PlanCopy(GridSelection.Empty.Click(new(0, 0), Grid)).Plan;

        var (text, _) = ClipboardData.Assemble(plan, (_, _) => "a\tb\"c\nd", Raw);

        Assert.Equal("\"a\tb\"\"c\nd\"\r\n", text);
    }

    [Fact] // The raw value travels HTML-escaped, so markup in a value stays a value
    public void Html_cells_are_escaped()
    {
        var plan = ClipboardRules.PlanCopy(GridSelection.Empty.Click(new(0, 0), Grid)).Plan;

        var (_, html) = ClipboardData.Assemble(plan, Display, (_, _) => "a<b>&c");

        Assert.Equal("<table><tr><td>a&lt;b&gt;&amp;c</td></tr></table>", html);
    }
}
