using Bunit;
using ExGrid.MudBlazor;
using Xunit;

namespace ExSheet.MudBlazor.Tests;

/// <summary>
/// Under ExGrid.MudBlazor's Chrome too, a Selection holding a refused cell shows no figure of the
/// Selection Summary, and the caption says why in the Sheet's words (ADR-0130; 2026-10-13, decided with
/// the user), worded through the Grid Chrome's Label as a refusal on a Sheet is (ADR-0203).
/// </summary>
public class MudSummaryRefusedTests : MudSheetTestContext
{
    // MROUND-001: a fractional multiple ExSheet refuses, its cell #VALUE!.
    private static ExSheet.Engine.SheetDocument Refusing() => DocumentOf(("A1", "1"), ("A2", "=MROUND(6.05,0.1)"));

    [Fact] // ADR-0130 (2026-10-13): a refused cell in the Selection declines the summary, in the Sheet's English
    public async Task ADR0130_a_refused_cell_declines_the_summary_under_the_mud_chrome()
    {
        var page = RenderPage(Refusing());

        await GoToAsync(page, "A1:A2");

        page.WaitForAssertion(() => Assert.Equal(
            "No figures: ExSheet refused the Formula in A2 with #VALUE! because it cannot answer it as Excel does, so a figure over it would not be Excel's.",
            page.Find(".mud-ex-grid-summary-declined").TextContent.Trim()));
        Assert.Empty(page.FindAll(".mud-ex-grid-summary-figure"));
    }

    [Fact] // ADR-0130 / ADR-0203 (2026-10-13): the Grid Chrome's Label words the reason, the cell in its {0}
    public async Task ADR0130_the_grid_chromes_label_words_the_refused_reason()
    {
        var chrome = new MudSheetChrome
        {
            Grid = new MudGridChrome { Label = id => id == SheetCommandIds.SummaryRefused ? "Keine Werte: {0} wurde abgelehnt." : null },
        };
        var page = RenderPage(Refusing(), chrome);

        await GoToAsync(page, "A1:A2");

        page.WaitForAssertion(() => Assert.Equal(
            "Keine Werte: A2 wurde abgelehnt.", page.Find(".mud-ex-grid-summary-declined").TextContent.Trim()));
    }
}
