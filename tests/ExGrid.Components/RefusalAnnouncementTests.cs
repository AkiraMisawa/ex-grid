using AngleSharp.Dom;
using Bunit;
using ExGrid.Chrome;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A refusal is worded by the built-in Chrome unless someone else words it (ADR-0203, ERR-2): the
/// words for the reason reach the root's live region once, as the refusal happens; a Consumer's
/// words through <c>CommandLabel</c> replace them, and the empty string silences them; a
/// substituted Chrome answers through <see cref="IGridChrome.Words"/>; binding the refusal events
/// silences nothing.
/// </summary>
public class RefusalAnnouncementTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
        new("Locked", ColumnType.Text, r => r.Book, header: "Desk", width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Window())
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20)
              .Add(g => g.ViewportHeight, 100)
              .Add(g => g.ViewportWidth, 350);
            extra?.Invoke(ps);
        });

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task ExtendRightAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, true, false, false, false));

    /// <summary>A block of two by two onto the one cell at Book, row 0: ADR-0014's
    /// <see cref="PasteRefusalReason.SingleCellTarget"/>.</summary>
    private static async Task PasteBlockOntoOneCellAsync(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        await ClickCellAsync(cut, 50, 10);
        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("a\tb\r\nc\td\r\n", null));
    }

    private static string Announced(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-announce").TextContent;

    /// <summary>The live regions whose accessible text says <paramref name="words"/>: a region's
    /// text outside anything hidden from assistive technology.</summary>
    private static int RegionsSaying(IRenderedComponent<ExGrid<TestRow>> cut, string words)
        => cut.FindAll("[role=status], [role=alert], [aria-live]")
            .Count(region => SpokenText(region).Contains(words, StringComparison.Ordinal));

    private static string SpokenText(INode node) => node switch
    {
        IElement element when element.GetAttribute("aria-hidden") == "true" => "",
        IElement element => string.Concat(element.ChildNodes.Select(SpokenText)),
        _ => node.TextContent,
    };

    [Fact] // ADR-0203 / ERR-2: a refused paste is announced in the root's live region, in the built-in words, once
    public async Task A_refused_paste_is_announced_once_in_the_built_in_words()
    {
        var cut = RenderGrid();

        await PasteBlockOntoOneCellAsync(cut);

        var words = BuiltInCommandLabels.For(RefusalLabelIds.PasteSingleCellTarget);
        Assert.Equal(words, Announced(cut));
        Assert.Equal(1, RegionsSaying(cut, words));
    }

    [Fact] // ADR-0203 / ERR-2, ADR-0035: a target that is not editable is said naming the column that refused, by its header
    public async Task A_target_that_is_not_editable_is_said_naming_the_column()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 150, 10);                      // Amount, editable
        await ExtendRightAsync(cut);                              // into Locked, headed "Desk"

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", null));

        Assert.Equal("Nothing was changed: the selection covers Desk, which cannot be edited.", Announced(cut));
    }

    [Fact] // ADR-0203: binding OnPasteRefused silences nothing — the reason is raised and the words are announced
    public async Task Binding_the_refusal_event_does_not_silence_the_words()
    {
        var refused = new List<PasteRefusalReason>();
        var cut = RenderGrid(ps => ps.Add(g => g.OnPasteRefused, (PasteRefusalReason r) => refused.Add(r)));

        await PasteBlockOntoOneCellAsync(cut);

        Assert.Equal([PasteRefusalReason.SingleCellTarget], refused);
        Assert.Equal(BuiltInCommandLabels.For(RefusalLabelIds.PasteSingleCellTarget), Announced(cut));
    }

    [Fact] // ADR-0203 / ERR-2: a Consumer's words through CommandLabel replace the built-in ones, announced once; null keeps them
    public async Task A_consumers_words_replace_the_built_in_ones()
    {
        Func<string, string?> label = id => id == RefusalLabelIds.PasteSingleCellTarget ? "Pick a block as big as the copy." : null;
        var cut = RenderGrid(ps => ps.Add(g => g.CommandLabel, label));

        await PasteBlockOntoOneCellAsync(cut);

        Assert.Equal("Pick a block as big as the copy.", Announced(cut));
        Assert.Equal(1, RegionsSaying(cut, "Pick a block as big as the copy."));
        Assert.Equal(0, RegionsSaying(cut, BuiltInCommandLabels.For(RefusalLabelIds.PasteSingleCellTarget)));

        // An id the Consumer says nothing about keeps the built-in words.
        await ClickCellAsync(cut, 150, 10);
        await ExtendRightAsync(cut);
        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", null));
        Assert.Equal("Nothing was changed: the selection covers Desk, which cannot be edited.", Announced(cut));
    }

    [Fact] // ADR-0203: the Consumer's words name the column where they hold its place
    public async Task A_consumers_words_name_the_column_where_they_hold_its_place()
    {
        Func<string, string?> label = id => id == RefusalLabelIds.PasteTargetNotEditable
            ? $"{RefusalLabelIds.ColumnPlaceholder} is read-only." : null;
        var cut = RenderGrid(ps => ps.Add(g => g.CommandLabel, label));
        await ClickCellAsync(cut, 250, 10);                      // Locked, headed "Desk"

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", null));

        Assert.Equal("Desk is read-only.", Announced(cut));
    }

    [Fact] // ADR-0203 / ERR-2: answering no words silences the announcement for that reason; the Consumer, still told the reason, owns saying it
    public async Task Answering_no_words_silences_the_announcement()
    {
        var refused = new List<PasteRefusalReason>();
        Func<string, string?> label = id => id == RefusalLabelIds.PasteSingleCellTarget ? "" : null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.CommandLabel, label)
            .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => refused.Add(r)));

        await PasteBlockOntoOneCellAsync(cut);

        Assert.Equal("", Announced(cut));
        Assert.Equal([PasteRefusalReason.SingleCellTarget], refused);
    }

    [Fact] // ADR-0203 / ERR-2: a refused copy is announced in the built-in words, once, and still raised
    public async Task A_refused_copy_is_announced_once()
    {
        var refused = new List<CopyRefusalReason>();
        var cut = RenderGrid(ps => ps.Add(g => g.OnCopyRefused, (CopyRefusalReason r) => refused.Add(r)));

        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());

        Assert.Equal("none", payload.Kind);
        Assert.Equal([CopyRefusalReason.EmptySelection], refused);
        var words = BuiltInCommandLabels.For(RefusalLabelIds.CopyEmptySelection);
        cut.WaitForAssertion(() => Assert.Equal(words, Announced(cut)));
        Assert.Equal(1, RegionsSaying(cut, words));
    }

    [Fact] // ADR-0203 / CP-23: a write the browser rejects is said in the Consumer's words when it gives them
    public async Task A_rejected_clipboard_write_is_said_in_the_consumers_words()
    {
        Func<string, string?> label = id => id == RefusalLabelIds.CopyClipboardUnavailable ? "The clipboard said no." : null;
        var cut = RenderGrid(ps => ps.Add(g => g.CommandLabel, label));

        await cut.InvokeAsync(() => cut.Instance.OnCopyWriteRejectedAsync());

        cut.WaitForAssertion(() => Assert.Equal("The clipboard said no.", Announced(cut)));
    }

    [Fact] // ADR-0203 / ADR-0050 item 9: a copy answer's own sentence is what is announced, once — never the built-in words beside it
    public async Task A_copy_answers_refusal_is_announced_in_its_own_sentence_once()
    {
        Func<GridCopyRequest, GridCopyAnswer> answer = _ => GridCopyAnswer.Refuse("Not while the figures are loading.");
        var cut = RenderGrid(ps => ps.Add(g => g.CopyAnswer, answer));
        await ClickCellAsync(cut, 50, 10);

        await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());

        cut.WaitForAssertion(() => Assert.Equal("Not while the figures are loading.", Announced(cut)));
        Assert.Equal(0, RegionsSaying(cut, BuiltInCommandLabels.For(RefusalLabelIds.CopyRefusedByConsumer)));
    }

    [Fact] // ADR-0203, principle 4: a substituted Chrome answers the words through IGridChrome.Words, and the grid's CommandLabel is not asked first
    public async Task A_substituted_chromes_words_are_announced()
    {
        var asked = new List<string>();
        Func<string, string?> label = id =>
        {
            asked.Add(id);
            return null;
        };
        var cut = RenderGrid(ps => ps
            .Add(g => g.Chrome, new WordingChrome(id => id == RefusalLabelIds.PasteSingleCellTarget ? "Chrome's own words." : null))
            .Add(g => g.CommandLabel, label));

        await PasteBlockOntoOneCellAsync(cut);

        Assert.Equal("Chrome's own words.", Announced(cut));
        Assert.DoesNotContain(RefusalLabelIds.PasteSingleCellTarget, asked);
    }

    [Fact] // ADR-0203: a substituted Chrome with no words of its own leaves the built-in Chrome's
    public async Task A_substituted_chrome_without_words_leaves_the_built_in_ones()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.Chrome, new WordingChrome(_ => null)));

        await PasteBlockOntoOneCellAsync(cut);

        Assert.Equal(BuiltInCommandLabels.For(RefusalLabelIds.PasteSingleCellTarget), Announced(cut));
    }

    /// <summary>A Chrome that draws nothing of its own and words refusals by the function it is given.</summary>
    private sealed class WordingChrome(Func<string, string?> words) : IGridChrome
    {
        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public string? Words(string id) => words(id);
    }
}
