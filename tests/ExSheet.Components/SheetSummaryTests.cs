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

    // A Formula ExSheet refuses because it cannot answer it as Excel does (MROUND-001): its cell is #VALUE!.
    private const string Refusing = "=MROUND(6.05,0.1)";

    private static string Declined(Bunit.IRenderedComponent<Components.ExSheet> cut)
        => cut.Find(".ex-summary.ex-summary-declined").TextContent;

    [Fact] // ADR-0130 (2026-10-13, decided with the user): a refused cell among the cells leaves no figure, and the line says why
    public async Task ADR0130_a_refused_cell_among_the_cells_leaves_no_figure_and_says_why()
    {
        var cut = RenderWithEveryFigure();
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "A2", "2");
        await EnterAsync(cut, "A3", Refusing);
        await EnterAsync(cut, "A4", "x");
        Assert.Equal("#VALUE!", CellText(cut, "A3"));

        await GoToAsync(cut, "A1:A4");

        // Not even Count or Numerical Count, which an Error Value would leave (b5): Excel answers A3.
        cut.WaitForAssertion(() => Assert.Equal(
            "No figures: ExSheet refused the Formula in A3 with #VALUE! because it cannot answer it as Excel does, so a figure over it would not be Excel's.",
            Declined(cut)));
        Assert.Empty(Figures(cut));
    }

    [Fact] // ADR-0130, ADR-0047 (2026-10-13): a refused #NAME? — a function Excel has that ExSheet does not declare — leaves no figure, and the line names what the cell shows
    public async Task ADR0130_a_refused_name_leaves_no_figure_and_the_line_says_it_shows_NAME()
    {
        var cut = RenderWithEveryFigure();
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "A2", "=NPV(0.1,1,2)");
        Assert.Equal("#NAME?", CellText(cut, "A2"));

        await GoToAsync(cut, "A1:A2");

        cut.WaitForAssertion(() => Assert.Equal(
            "No figures: ExSheet refused the Formula in A2 with #NAME? because it cannot answer it as Excel does, so a figure over it would not be Excel's.",
            Declined(cut)));
        Assert.Empty(Figures(cut));
    }

    [Fact] // ADR-0130 (2026-10-13): a Formula that reads a refused cell is refused too; the first refused cell in reading order is named, and the figures come back once none is left
    public async Task ADR0130_the_first_refused_cell_is_named_and_the_figures_return_without_one()
    {
        var cut = RenderWithEveryFigure();
        await EnterAsync(cut, "B2", Refusing);
        await EnterAsync(cut, "A3", "=B2+1");
        await EnterAsync(cut, "A1", "4");

        await GoToAsync(cut, "A1:B3");
        cut.WaitForAssertion(() => Assert.Contains("the Formula in B2 ", Declined(cut)));

        await GoToAsync(cut, "A1:A3");
        cut.WaitForAssertion(() => Assert.Contains("the Formula in A3 ", Declined(cut)));

        await EnterAsync(cut, "B2", "6");
        await GoToAsync(cut, "A1:A3");
        cut.WaitForAssertion(() => Assert.Equal(
            ["Average: 5.5", "Count: 2", "Numerical Count: 2", "Min: 4", "Max: 7", "Sum: 11"], Figures(cut)));
    }

    [Fact] // ADR-0130 (2026-10-13): the reason is asked through CommandLabel by its id, so a Consumer words it in its own language
    public async Task ADR0130_the_refused_reason_is_worded_through_command_label()
    {
        var cut = RenderSheet(ps => ps
            .Add(p => p.SummaryFigures, SummaryFigures.All)
            .Add(p => p.CommandLabel, id => id == SheetCommandIds.SummaryRefused
                ? "集計なし: {0} の数式は Excel と同じ答えを出せないため、ExSheet が #VALUE! で拒否しました。"
                : null));
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "A2", Refusing);

        await GoToAsync(cut, "A1:A2");

        cut.WaitForAssertion(() => Assert.Equal(
            "集計なし: A2 の数式は Excel と同じ答えを出せないため、ExSheet が #VALUE! で拒否しました。", Declined(cut)));
    }
}
