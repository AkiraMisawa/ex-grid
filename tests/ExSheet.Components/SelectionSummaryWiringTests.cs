using Bunit;
using ExSheet.Components.Tests.Support;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// The Selection Summary on a Sheet (ADR-0130, SM-12): ExSheet answers from its own cells — a
/// Formula's Value, never its Entry, every cell an array spills into — and holds the figures shown.
/// </summary>
public class SelectionSummaryWiringTests : SheetTestContext
{
    private static string Summary(IRenderedComponent<Components.ExSheet> cut) => cut.Find(".ex-summary").TextContent;

    [Fact] // ADR-0130 / SM-12: a Formula's Value and the cells an array spills into are summed
    public async Task ADR0130_formula_values_and_spilled_cells_are_summed()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "A2", "2");
        await EnterAsync(cut, "A3", "=A1+A2");
        await EnterAsync(cut, "A4", "text");
        await EnterAsync(cut, "B1", "=A1:A2*10"); // spills into B2

        await GoToAsync(cut, "A1:B4");

        // 1 + 2 + 3 + 10 + 20 = 36 over five numbers; the text is counted.
        cut.WaitForAssertion(() => Assert.Equal("Average: 7.2Count: 6Sum: 36", Summary(cut)));
    }

    [Fact] // ADR-0130 / SM-12: an Error Value among the cells leaves Count alone
    public async Task ADR0130_an_error_leaves_count_alone()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "A2", "=1/0");

        await GoToAsync(cut, "A1:A2");

        cut.WaitForAssertion(() => Assert.Equal("Count: 2", Summary(cut)));
    }

    [Fact] // ADR-0130: a change under the selection asks again
    public async Task ADR0130_a_change_under_the_selection_asks_again()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "A2", "2");
        await GoToAsync(cut, "A1:A2");
        cut.WaitForAssertion(() => Assert.Contains("Sum: 3", Summary(cut)));

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());

        cut.WaitForAssertion(() => Assert.Contains("Sum: 1", Summary(cut)));
    }

    [Fact] // ADR-0130 / SM-14: switched off, the Sheet's grid shows no summary
    public async Task ADR0130_switched_off_the_sheet_shows_no_summary()
    {
        var cut = RenderSheet(ps => ps.Add(p => p.ShowSelectionSummary, false));
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "A2", "2");

        await GoToAsync(cut, "A1:A2");

        Assert.Empty(cut.FindAll(".ex-summary"));
    }

    [Fact] // ADR-0130: the figures are shown in the Focus cell's Number Format, as Excel's status bar shows them
    public async Task ADR0130_the_figures_wear_the_focus_cells_number_format()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1234");
        await EnterAsync(cut, "A2", "1000.5");
        await GoToAsync(cut, "A1:A2");
        await cut.InvokeAsync(() => cut.Instance.SetNumberFormatAsync(global::ExSheet.Engine.NumberFormat.Parse("#,##0.00")));

        cut.WaitForAssertion(() => Assert.Equal("Average: 1,117.25Count: 2Sum: 2,234.50", Summary(cut)));
    }
}
