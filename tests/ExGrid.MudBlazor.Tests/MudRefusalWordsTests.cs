using Bunit;
using ExGrid.Chrome;
using ExGrid.Clipboard;
using ExGrid.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// Under this Chrome a refusal is said in the Chrome's words (ADR-0203, WR-3): MudBlazor has no
/// localised text for a refused paste or copy, so <see cref="MudGridChrome.Label"/> words each, the
/// English beneath it; the grid's live region announces them once, and the empty string silences
/// them.
/// </summary>
public class MudRefusalWordsTests : MudTestContext
{
    private IRenderedComponent<ExGrid<Trade>> RenderGrid(MudGridChrome chrome, Action<PasteRefusalReason>? refused = null)
        => Render<ExGrid<Trade>>(ps =>
        {
            ps.Add(g => g.Window, Rows(5))
              .Add(g => g.TotalCount, 5)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 400)
              .Add(g => g.Chrome, chrome);
            if (refused is not null)
                ps.Add(g => g.OnPasteRefused, refused);
        });

    /// <summary>A two-by-two block onto the one cell at Book, row 1: ADR-0014's
    /// <see cref="PasteRefusalReason.SingleCellTarget"/>.</summary>
    private static async Task PasteBlockOntoOneCellAsync(IRenderedComponent<ExGrid<Trade>> cut)
    {
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 30 });
        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("a\tb\r\nc\td\r\n", null));
    }

    private static string Announced(IRenderedComponent<ExGrid<Trade>> cut) => cut.Find(".ex-announce").TextContent;

    [Fact] // ADR-0203 / WR-3, ERR-2: with nothing of its own to say, this Chrome says a refusal in the English the built-in Chrome does, once
    public async Task A_refusal_is_announced_in_english_under_this_chrome()
    {
        var cut = RenderGrid(MudGridChrome.Default);

        await PasteBlockOntoOneCellAsync(cut);

        var words = BuiltInCommandLabels.For(RefusalLabelIds.PasteSingleCellTarget);
        Assert.Equal(words, Announced(cut));
        Assert.Equal(1, cut.FindAll("[role=status]").Count(region => region.TextContent.Contains(words, StringComparison.Ordinal)));
    }

    [Fact] // ADR-0203 / WR-3: the Chrome's Label words a refusal, as it words what MudBlazor has no key for
    public async Task The_chromes_label_words_a_refusal()
    {
        var chrome = new MudGridChrome { Label = id => id == RefusalLabelIds.PasteSingleCellTarget ? "Wähle einen Bereich in der Form der Kopie." : null };
        var cut = RenderGrid(chrome);

        await PasteBlockOntoOneCellAsync(cut);

        Assert.Equal("Wähle einen Bereich in der Form der Kopie.", Announced(cut));
    }

    [Fact] // ADR-0203: the Chrome's Label answering no words silences the announcement, and the reason is still raised
    public async Task The_chromes_label_answering_nothing_silences_it()
    {
        var refused = new List<PasteRefusalReason>();
        var chrome = new MudGridChrome { Label = id => id == RefusalLabelIds.PasteSingleCellTarget ? "" : null };
        var cut = RenderGrid(chrome, refused.Add);

        await PasteBlockOntoOneCellAsync(cut);

        Assert.Equal("", Announced(cut));
        Assert.Equal([PasteRefusalReason.SingleCellTarget], refused);
    }

    [Fact] // ADR-0203 / WR-3: every refusal reason has words under this Chrome
    public void Every_refusal_reason_has_words_under_this_chrome()
    {
        IGridChrome chrome = MudGridChrome.Default;

        foreach (var reason in Enum.GetValues<PasteRefusalReason>())
            Assert.Equal(BuiltInCommandLabels.For(RefusalLabelIds.For(reason)), chrome.Words(RefusalLabelIds.For(reason)));
        foreach (var reason in Enum.GetValues<CopyRefusalReason>())
            Assert.Equal(BuiltInCommandLabels.For(RefusalLabelIds.For(reason)), chrome.Words(RefusalLabelIds.For(reason)));
    }
}
