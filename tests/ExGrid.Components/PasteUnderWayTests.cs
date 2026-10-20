using Bunit;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A paste under way, and the Escape that cancels it (ADR-0240 section 12; large-paste ticket 01). From the
/// paste's start in the Input Order until it lands or is refused, the root is busy (aria-busy, ex-pasting)
/// and the live region says so once. An Escape typed meanwhile reaches the core as a cancel naming the
/// paste, heard at once: before the Edit Intent the paste is refused as Cancelled, after it the paste
/// stands and the Escape's turn says so. What the script numbers and sends is layer 3's; here the calls are
/// made by hand. 20px rows in a 120px Viewport; Book and Amount, 100px each.
/// </summary>
public class PasteUnderWayTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        List<GridPasteIntent> intents, List<PasteRefusalReason> refused, Func<GridPasteIntent, Task>? onPaste = null)
    {
        var source = new TestSource();
        source.Push(TestRows.Many(50), totalCount: 50);
        return Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns,
            [
                new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
                new GridColumn<TestRow>("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
            ])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnPaste, async (GridPasteIntent i) =>
            {
                intents.Add(i);
                if (onPaste is not null)
                    await onPaste(i);
            })
            .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => refused.Add(r)));
    }

    // Sent on the renderer and not awaited: the call's task, which ends once its turn has been taken.
    private static async Task<Task> PasteAsync(IRenderedComponent<ExGrid<TestRow>> cut, FakeJSStream text, int input)
    {
        Task sent = Task.CompletedTask;
        await cut.InvokeAsync(() => { sent = cut.Instance.OnPasteStreamsAsync(text, null, input: input); });
        return sent;
    }

    private static async Task<Task> CancelAsync(IRenderedComponent<ExGrid<TestRow>> cut, int paste, int input)
    {
        Task sent = Task.CompletedTask;
        await cut.InvokeAsync(() => { sent = cut.Instance.OnPasteCancelAsync(paste, input); });
        return sent;
    }

    // Book, row 0: the paste's target.
    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });

    private static string? Busy(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-grid").GetAttribute("aria-busy");

    private static bool PaintedPasting(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-grid").ClassList.Contains("ex-pasting");

    private static string Announced(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-announce").TextContent;

    [Fact] // ADR-0240 §12: from the paste's start until it lands the root is busy and painted so, and the live region says it once
    public async Task ADR0240_a_paste_is_busy_from_its_start_until_it_lands()
    {
        List<GridPasteIntent> intents = [];
        List<PasteRefusalReason> refused = [];
        var cut = RenderGrid(intents, refused);
        await ClickCellAsync(cut);
        Assert.Null(Busy(cut));
        var opens = new TaskCompletionSource();

        var paste = await PasteAsync(cut, FakeJSStream.Gated("x", opens.Task), input: 1);

        Assert.Equal("true", Busy(cut));
        Assert.True(PaintedPasting(cut));
        Assert.Contains("Pasting. Press Escape to cancel.", Announced(cut));

        opens.SetResult();
        await paste;

        Assert.Single(intents);
        Assert.Null(Busy(cut));
        Assert.False(PaintedPasting(cut));
        Assert.DoesNotContain("Pasting.", Announced(cut));
    }

    [Fact] // ADR-0240 §12: a paste refused is no longer busy either
    public async Task ADR0240_a_paste_is_busy_until_it_is_refused()
    {
        List<GridPasteIntent> intents = [];
        List<PasteRefusalReason> refused = [];
        var cut = RenderGrid(intents, refused);
        await ClickCellAsync(cut);
        var opens = new TaskCompletionSource();

        var paste = await PasteAsync(cut, FakeJSStream.Held(opens.Task), input: 1);
        Assert.Equal("true", Busy(cut));
        opens.SetResult();
        await paste;

        Assert.Equal([PasteRefusalReason.Unreadable], refused);
        Assert.Null(Busy(cut));
        Assert.False(PaintedPasting(cut));
        Assert.Contains("the clipboard could not be read", Announced(cut));
    }

    [Fact] // ADR-0240 §12: an Escape before the Edit Intent cancels the paste, refused aloud, and the grid goes on answering
    public async Task ADR0240_a_cancel_before_the_intent_refuses_the_paste()
    {
        List<GridPasteIntent> intents = [];
        List<PasteRefusalReason> refused = [];
        var cut = RenderGrid(intents, refused);
        await ClickCellAsync(cut);
        var paste = await PasteAsync(cut, FakeJSStream.Gated("x", new TaskCompletionSource().Task), input: 1);

        var cancel = await CancelAsync(cut, paste: 1, input: 2);
        await paste;
        await cancel;

        Assert.Equal([PasteRefusalReason.Cancelled], refused);
        Assert.Empty(intents);
        Assert.Null(Busy(cut));
        Assert.Contains("Nothing was pasted: the paste was cancelled.", Announced(cut));
        Assert.Equal("2", cut.Find(".ex-grid > [data-ex-answered]").GetAttribute("data-ex-answered"));

        await (await PasteAsync(cut, FakeJSStream.Of("fill"), input: 3));

        Assert.Equal("fill", Assert.Single(intents).ValueFor(new CellPosition(0, 0)));
    }

    [Fact] // ADR-0240 §12: an Escape after the Edit Intent finds the paste standing, and its turn says so
    public async Task ADR0240_a_cancel_after_the_intent_leaves_the_paste_standing_and_says_so()
    {
        List<GridPasteIntent> intents = [];
        List<PasteRefusalReason> refused = [];
        var handled = new TaskCompletionSource();
        var cut = RenderGrid(intents, refused, _ => handled.Task);
        await ClickCellAsync(cut);
        var paste = await PasteAsync(cut, FakeJSStream.Of("x"), input: 1);
        Assert.Single(intents);

        var cancel = await CancelAsync(cut, paste: 1, input: 2);
        handled.SetResult();
        await paste;
        await cancel;

        Assert.Empty(refused);
        Assert.False(Assert.Single(intents).IsRefused);
        Assert.Contains("The paste had already been made, so it was not cancelled.", Announced(cut));
    }

    [Fact] // ADR-0240 §12: an Escape that names a paste already finished does nothing but say so
    public async Task ADR0240_a_cancel_naming_a_finished_paste_says_it_was_already_made()
    {
        List<GridPasteIntent> intents = [];
        List<PasteRefusalReason> refused = [];
        var cut = RenderGrid(intents, refused);
        await ClickCellAsync(cut);
        await (await PasteAsync(cut, FakeJSStream.Of("x"), input: 1));

        await (await CancelAsync(cut, paste: 1, input: 2));

        Assert.Single(intents);
        Assert.Empty(refused);
        Assert.Contains("The paste had already been made, so it was not cancelled.", Announced(cut));
    }

    [Fact] // ADR-0240 §12: a paste still waiting for its turn when its Escape arrives is cancelled as it starts
    public async Task ADR0240_a_cancel_ahead_of_the_pastes_turn_refuses_it_when_it_comes()
    {
        List<GridPasteIntent> intents = [];
        List<PasteRefusalReason> refused = [];
        var cut = RenderGrid(intents, refused);
        await ClickCellAsync(cut);
        var opens = new TaskCompletionSource();
        var first = await PasteAsync(cut, FakeJSStream.Held(opens.Task), input: 1);
        var second = await PasteAsync(cut, FakeJSStream.Of("x"), input: 2);

        var cancel = await CancelAsync(cut, paste: 2, input: 3);
        opens.SetResult();
        await first;
        await second;
        await cancel;

        Assert.Equal([PasteRefusalReason.Unreadable, PasteRefusalReason.Cancelled], refused);
        Assert.Empty(intents);
        Assert.Null(Busy(cut));
    }
}
