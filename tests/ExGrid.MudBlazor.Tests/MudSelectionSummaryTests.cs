using Bunit;
using ExGrid.Chrome;
using ExGrid.Summarizing;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// The Selection Summary under MudBlazor (ADR-0130, WR-1/3, SM-11): the core's figures painted as
/// MudBlazor captions in the Chrome's words, deciding nothing of their own.
/// </summary>
public class MudSelectionSummaryTests : MudTestContext
{
    private static SelectionSummaryContext Answered() => new(
        SelectionSummaryStatus.Answered,
        [
            new SummaryFigureText(SummaryFigures.Average, SummaryLabelIds.Average, "2"),
            new SummaryFigureText(SummaryFigures.Sum, SummaryLabelIds.Sum, "6"),
        ],
        null);

    [Fact] // ADR-0130 / SM-11: each figure a caption, its text the core's
    public void ADR0130_each_figure_is_a_caption_with_the_cores_text()
    {
        var cut = Render(MudGridChrome.Default.SelectionSummary(Answered())!);

        Assert.Equal(["Average: 2", "Sum: 6"],
            cut.FindAll(".mud-ex-grid-summary-figure").Select(e => e.TextContent.Trim()));
    }

    [Fact] // ADR-0130 / WR-3: the figures are named in the Chrome's words
    public void ADR0130_the_figures_are_named_in_the_chromes_words()
    {
        var chrome = new MudGridChrome { Label = id => id == SummaryLabelIds.Sum ? "合計" : null };

        var cut = Render(chrome.SelectionSummary(Answered())!);

        Assert.Equal("合計: 6", cut.FindAll(".mud-ex-grid-summary-figure")[1].TextContent.Trim());
    }

    [Fact] // ADR-0130: the pending mark and a decline's reason, never a figure
    public void ADR0130_pending_and_declined_show_no_figure()
    {
        var pending = Render(MudGridChrome.Default.SelectionSummary(new(SelectionSummaryStatus.Pending, [], null))!);
        var declined = Render(MudGridChrome.Default.SelectionSummary(new(SelectionSummaryStatus.Declined, [], "Too many rows"))!);

        Assert.Equal("Calculating…", pending.Find(".mud-ex-grid-summary-pending").TextContent.Trim());
        Assert.Equal("Too many rows", declined.Find(".mud-ex-grid-summary-declined").TextContent.Trim());
        Assert.Empty(declined.FindAll(".mud-ex-grid-summary-figure"));
    }
}
