using Bunit;
using ExGrid.Summarizing;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// A Sheet's Selection Summary beside Excel's status bar, as the seventeenth Windows run read it
/// (<c>verification/2026-10-11-windows-17/report.md</c>, Part B's b5 to b8; ADR-0130, SM-7, SM-9).
/// Each case is the run's gesture on a Sheet with all six figures shown, as the run ticked them in
/// Excel's Customise Status Bar. The figure texts are Excel's verbatim, since ExSheet writes them; the
/// names are the grid's words (the run's Excel wrote <c>Min.</c> and <c>Max.</c>).
/// </summary>
public class SheetSummaryTests : SheetTestContext
{
    private Bunit.IRenderedComponent<Components.ExSheet> RenderWithEveryFigure()
        => RenderSheet(ps => ps.Add(p => p.SummaryFigures, SummaryFigures.All));

    private static string[] Figures(Bunit.IRenderedComponent<Components.ExSheet> cut)
        => [.. cut.FindAll(".ex-summary .ex-summary-figure").Select(figure => figure.TextContent)];

    private static async Task FormatAsync(Bunit.IRenderedComponent<Components.ExSheet> cut, string address, string format)
    {
        await GoToAsync(cut, address);
        await cut.InvokeAsync(() => cut.Instance.SetNumberFormatAsync(NumberFormat.Parse(format)));
    }

    [Fact] // ADR-0130 / SM-7: an Error Value among the cells leaves Count and Numerical Count, and no other figure (run 17, b5)
    public async Task ADR0130_an_error_among_the_cells_leaves_count_and_numerical_count()
    {
        var cut = RenderWithEveryFigure();
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "A2", "2");
        await EnterAsync(cut, "A3", "=1/0");
        await EnterAsync(cut, "A4", "x");

        await GoToAsync(cut, "A1:A5");

        // Excel: "Count: 4    Numerical Count: 2", and nothing else: no Average, Min., Max. or Sum.
        cut.WaitForAssertion(() => Assert.Equal(["Count: 4", "Numerical Count: 2"], Figures(cut)));
    }

    [Fact] // ADR-0130 / SM-7: the error cleared, every figure is back (run 17, b6, SM-7's control)
    public async Task ADR0130_the_error_cleared_every_figure_is_back()
    {
        var cut = RenderWithEveryFigure();
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "A2", "2");
        await EnterAsync(cut, "A3", "=1/0");
        await EnterAsync(cut, "A4", "x");
        await GoToAsync(cut, "A3");
        await PressAsync(cut, "Delete");

        await GoToAsync(cut, "A1:A5");

        // Excel: "Average: 1.5    Count: 3    Numerical Count: 2    Min.: 1    Max.: 2    Sum: 3".
        cut.WaitForAssertion(() => Assert.Equal(
            ["Average: 1.5", "Count: 3", "Numerical Count: 2", "Min: 1", "Max: 2", "Sum: 3"], Figures(cut)));
    }

    [Fact] // ADR-0130: a Boolean is counted and not numerically; a date is a number; the figures in General (run 17, b7)
    public async Task ADR0130_a_boolean_is_counted_and_a_date_is_a_number()
    {
        var cut = RenderWithEveryFigure();
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "A2", "TRUE");
        // Read as the date 46306, as Excel read it; its Max below is that serial number.
        await EnterAsync(cut, "A3", "2026-10-11");
        await EnterAsync(cut, "A4", "x");

        await GoToAsync(cut, "A1:A4");

        // Excel: "Average: 23153.5    Count: 4    Numerical Count: 2    Min.: 1    Max.: 46306    Sum: 46307".
        cut.WaitForAssertion(() => Assert.Equal(
            ["Average: 23153.5", "Count: 4", "Numerical Count: 2", "Min: 1", "Max: 46306", "Sum: 46307"], Figures(cut)));

        await GoToAsync(cut, "A1:A2");

        // Excel: "Average: 1    Count: 2    Numerical Count: 1    Min.: 1    Max.: 1    Sum: 1".
        cut.WaitForAssertion(() => Assert.Equal(
            ["Average: 1", "Count: 2", "Numerical Count: 1", "Min: 1", "Max: 1", "Sum: 1"], Figures(cut)));

        await GoToAsync(cut, "A3");

        // Excel: no figure for one cell.
        cut.WaitForAssertion(() => Assert.Empty(Figures(cut)));
    }

    [Fact] // ADR-0130 / SM-9: every figure but the counts wears the active cell's format, whatever the other's (run 17, b8)
    public async Task ADR0130_the_figures_wear_the_active_cells_format_across_mixed_formats()
    {
        var cut = RenderWithEveryFigure();
        await FormatAsync(cut, "A1", "#,##0.00");
        await FormatAsync(cut, "A2", "0%");
        await EnterAsync(cut, "A1", "1000");
        // Excel's automatic percent entry, as in the run: 2000 typed into a 0% cell is 20, shown 2000%.
        await EnterAsync(cut, "A2", "2000");
        Assert.Equal("2000%", CellText(cut, "A2"));

        // A press on A1, Shift+press on A2: the active cell is A1.
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "ArrowDown", shift: true);

        // Excel: "Average: 510.00    Count: 2    Numerical Count: 2    Min.: 20.00    Max.: 1,000.00    Sum: 1,020.00".
        cut.WaitForAssertion(() => Assert.Equal(
            ["Average: 510.00", "Count: 2", "Numerical Count: 2", "Min: 20.00", "Max: 1,000.00", "Sum: 1,020.00"], Figures(cut)));

        // A press on A2, Shift+press on A1: the active cell is A2.
        await GoToAsync(cut, "A2");
        await PressAsync(cut, "ArrowUp", shift: true);

        // Excel: "Average: 51000%    Count: 2    Numerical Count: 2    Min.: 2000%    Max.: 100000%    Sum: 102000%".
        cut.WaitForAssertion(() => Assert.Equal(
            ["Average: 51000%", "Count: 2", "Numerical Count: 2", "Min: 2000%", "Max: 100000%", "Sum: 102000%"], Figures(cut)));
    }
}
