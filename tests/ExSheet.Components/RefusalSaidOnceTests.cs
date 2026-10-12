using AngleSharp.Dom;
using Bunit;
using ExGrid.Chrome;
using ExGrid.Clipboard;
using ExSheet.Components.Tests.Support;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// A refusal on a Sheet is announced once, in the Sheet's words (ADR-0203): ExSheet answers the
/// grid's refusal ids through the grid's label seam, so the grid's live region says them, and the
/// Sheet's notice shows the same words hidden from assistive technology, so no second region says
/// them again.
/// </summary>
public class RefusalSaidOnceTests : SheetTestContext
{
    private static Task PasteAsync(IRenderedComponent<ExSheet> cut, string text)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnPasteAsync(text, null));
    }

    private static string Notice(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-sheet-notice").TextContent;

    private static string Announced(IRenderedComponent<ExSheet> cut) => Grid(cut).Find(".ex-announce").TextContent;

    /// <summary>The live regions whose accessible text — outside anything hidden from assistive
    /// technology — says <paramref name="words"/>.</summary>
    private static int RegionsSaying(IRenderedComponent<ExSheet> cut, string words)
        => cut.FindAll("[role=status], [role=alert], [aria-live]")
            .Count(region => SpokenText(region).Contains(words, StringComparison.Ordinal));

    private static string SpokenText(INode node) => node switch
    {
        IElement element when element.GetAttribute("aria-hidden") == "true" => "",
        IElement element => string.Concat(element.ChildNodes.Select(SpokenText)),
        _ => node.TextContent,
    };

    [Fact] // ADR-0203 / ERR-2: a paste the grid refuses on a Sheet is announced once, in the Sheet's words, and the notice shows them unsaid
    public async Task A_refused_paste_is_announced_once_in_the_sheets_words()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "A1048576");

        await PasteAsync(cut, "1\r\n2\r\n");

        var words = SheetWords.PasteRefused(PasteRefusalReason.SpillPastExtent);
        Assert.Equal(words, Announced(cut));
        Assert.Equal(words, Notice(cut));
        Assert.Equal(1, RegionsSaying(cut, words));
        Assert.Equal(0, RegionsSaying(cut, BuiltInCommandLabels.For(RefusalLabelIds.PasteSpillPastExtent)));
    }

    [Fact] // ADR-0203 / ERR-2, ADR-0049: a copy the Sheet refuses in the engine's words is announced once, by the grid
    public async Task A_copy_the_sheet_refuses_is_announced_once()
    {
        var cut = RenderSheet();
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["PV"]);
        await EnterAsync(cut, "A2", "=SUM(Positions[PV])");
        await GoToAsync(cut, "A1:A2");

        Grid(cut).Instance.BuildCopyPayload();

        const string Engine = "A2 is waiting for a Linked Table's data (#GETTING_DATA)";
        cut.WaitForAssertion(() => Assert.Contains(Engine, Announced(cut)));
        cut.WaitForAssertion(() => Assert.Contains(Engine, Notice(cut)));
        Assert.Equal(1, RegionsSaying(cut, Engine));
    }

    [Fact] // ADR-0203: a Consumer's words for a refusal, through ExSheet's CommandLabel, are what the grid says and the notice shows
    public async Task A_consumers_words_through_the_sheets_label_are_said_once()
    {
        Func<string, string?> label = id => id == RefusalLabelIds.PasteSpillPastExtent ? "Le bloc dépasse la feuille." : null;
        var cut = RenderSheet(ps => ps.Add(s => s.CommandLabel, label));
        await GoToAsync(cut, "A1048576");

        await PasteAsync(cut, "1\r\n2\r\n");

        Assert.Equal("Le bloc dépasse la feuille.", Announced(cut));
        Assert.Equal("Le bloc dépasse la feuille.", Notice(cut));
        Assert.Equal(1, RegionsSaying(cut, "Le bloc dépasse la feuille."));
    }

    [Fact] // ADR-0203: a notice of the Sheet's own, which the grid does not say, is still said by the notice
    public async Task A_notice_of_the_sheets_own_is_still_said_by_the_notice()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "A1048576");
        await PasteAsync(cut, "1\r\n2\r\n");

        await GoToAsync(cut, "nonsense");

        Assert.Contains("'nonsense' is not a cell address", Notice(cut));
        Assert.Equal(1, RegionsSaying(cut, "'nonsense' is not a cell address"));
    }

    [Fact] // ADR-0203 / ADR-0050 item 3: a shape mismatch on a Sheet, whose paste may spill, is said once, in the Sheet's words, which offer one cell — the notice shows the same words
    public async Task A_shape_mismatch_is_said_once_in_the_sheets_words()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "A1:C3");

        await PasteAsync(cut, "1\t2\r\n3\t4\r\n");

        var words = SheetWords.PasteRefused(PasteRefusalReason.ShapeMismatch);
        Assert.Contains("Select one cell", words);
        Assert.Equal(words, Announced(cut));
        Assert.Equal(words, Notice(cut));
        Assert.Equal(1, RegionsSaying(cut, words));
    }

    [Fact] // ADR-0203: every refusal the grid raises has the Sheet's words behind the label seam, by either id the grid asks with
    public void Every_refusal_has_the_sheets_words()
    {
        foreach (var reason in Enum.GetValues<PasteRefusalReason>())
        {
            Assert.Equal(SheetWords.PasteRefused(reason), SheetCommandIds.EnglishFor(RefusalLabelIds.For(reason)));
            Assert.Equal(SheetWords.PasteRefused(reason), SheetCommandIds.EnglishFor(RefusalLabelIds.For(reason, pasteMaySpill: true)));
        }
        foreach (var reason in Enum.GetValues<CopyRefusalReason>())
            Assert.Equal(SheetWords.CopyRefused(reason), SheetCommandIds.EnglishFor(RefusalLabelIds.For(reason)));
    }
}
