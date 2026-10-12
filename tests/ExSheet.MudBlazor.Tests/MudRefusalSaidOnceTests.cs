using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using ExGrid.Chrome;
using ExGrid.Clipboard;
using ExGrid.Components;
using ExGrid.MudBlazor;
using Xunit;

namespace ExSheet.MudBlazor.Tests;

/// <summary>
/// Under ExGrid.MudBlazor's Chrome too, a refusal on a Sheet is announced once, in the Sheet's
/// words (ADR-0203): the Chrome's label function answers the grid's refusal ids with the Sheet's
/// English beneath it, the grid's live region says them, and the Sheet's notice shows them unsaid.
/// </summary>
public class MudRefusalSaidOnceTests : MudSheetTestContext
{
    private static Task PasteAsync(IRenderedComponent<ContainerFragment> page, string text)
    {
        var grid = page.FindComponent<ExGrid<SheetRow>>();
        return grid.InvokeAsync(() => grid.Instance.OnPasteAsync(text, null));
    }

    private static int RegionsSaying(IRenderedComponent<ContainerFragment> page, string words)
        => page.FindAll("[role=status], [role=alert], [aria-live]")
            .Count(region => SpokenText(region).Contains(words, StringComparison.Ordinal));

    private static string SpokenText(INode node) => node switch
    {
        IElement element when element.GetAttribute("aria-hidden") == "true" => "",
        IElement element => string.Concat(element.ChildNodes.Select(SpokenText)),
        _ => node.TextContent,
    };

    [Fact] // ADR-0203 / ERR-2: under the MudBlazor Chrome a refused paste on a Sheet is announced once, in the Sheet's words
    public async Task A_refused_paste_is_announced_once_in_the_sheets_words()
    {
        var page = RenderPage();
        await GoToAsync(page, "A1048576");

        await PasteAsync(page, "1\r\n2\r\n");

        var words = SheetCommandIds.EnglishFor(RefusalLabelIds.PasteSpillPastExtent)!;
        Assert.Contains("past the Sheet's edge", words);
        Assert.Equal(words, page.Find(".ex-announce").TextContent);
        Assert.Equal(words, page.Find(".ex-sheet-notice").TextContent);
        Assert.Equal(1, RegionsSaying(page, words));
    }

    [Fact] // ADR-0203 / WR-3: a Grid Chrome's own Label words the refusal on a Sheet, and the Sheet's notice shows those words
    public async Task The_grid_chromes_label_words_a_refusal_on_a_sheet()
    {
        var chrome = new MudSheetChrome
        {
            Grid = new MudGridChrome { Label = id => id == RefusalLabelIds.PasteSpillPastExtent ? "Über den Rand der Tabelle." : null },
        };
        var page = RenderPage(chrome: chrome);
        await GoToAsync(page, "A1048576");

        await PasteAsync(page, "1\r\n2\r\n");

        Assert.Equal("Über den Rand der Tabelle.", page.Find(".ex-announce").TextContent);
        Assert.Equal("Über den Rand der Tabelle.", page.Find(".ex-sheet-notice").TextContent);
        Assert.Equal(1, RegionsSaying(page, "Über den Rand der Tabelle."));
    }
}
