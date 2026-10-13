using Bunit;
using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The grid tells its Consumer when an edit opens and when it ends (ADR-0050 section 6, SH-29):
/// however it opens, and however it ends — committed, cancelled or discarded. An edit a Reject
/// holds open is still open. It carries no text (ADR-0007), and it is raised in C#, where the
/// editing state lives, with no round trip between the two. 20px rows; Book is editable, Amount
/// is not.
/// </summary>
public class EditingNotificationTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns(Func<TestRow, string, EditVerdict>? validate = null) =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true, validate: validate),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        List<bool>? told,
        List<GridEditIntent<TestRow>>? edits = null,
        Func<TestRow, string, EditVerdict>? validate = null,
        int totalCount = 50,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null,
        Action<GridEditIntent<TestRow>>? onEdit = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, totalCount)
              .Add(g => g.Columns, Columns(validate))
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 160)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) =>
              {
                  edits?.Add(intent);
                  onEdit?.Invoke(intent);
              });
            if (told is not null)
                ps.Add(g => g.OnEditingChanged, told.Add);
            extra?.Invoke(ps);
        });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, false, false, false, false));

    /// <summary>What a key had done by the time it first waited: the key, whether it was already
    /// answered, and <paramref name="read"/>, all in one turn of the renderer's dispatcher, taken
    /// once it is idle (<see cref="GridTestContext.ReadWhenIdleAsync{T}"/>), as the browser's call
    /// reaches the grid. The key goes on afterwards as the test answers what it waits for.</summary>
    /// <remarks>A key raised from the test's own thread and read there after the call returned is
    /// read at the right moment only while the dispatcher has nothing else in hand, for only then
    /// does the call run on the test's thread before it returns. After an answer the test gave from
    /// outside the dispatcher — <c>SetVoidResult</c> on the key gate — the grid's continuation runs
    /// as the dispatcher's work on a pool thread, and the test, resumed on another by the key it
    /// awaited, can call while that work is still unwinding. The next key is then queued behind it
    /// and runs on a third thread while the test reads: a list of what the Consumer heard,
    /// enumerated as that key added to it, threw "Collection was modified" (2 of 30 runs of this
    /// class beside four busy cores, 2026-10-13).</remarks>
    private Task<(Task Key, bool Answered, T Read)> KeyAndReadInOneTurnAsync<T>(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, Func<T> read)
        => ReadWhenIdleAsync(() =>
        {
            var pressed = cut.Instance.OnKeyAsync(key, false, false, false, false, false);
            return (pressed, pressed.IsCompleted, read());
        });

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
        => cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = text });

    private List<string> GateModesTold()
        => [.. JSInterop.Invocations.Where(i => i.Identifier == "setEditing").Select(i => (string)i.Arguments[0]!)];

    /// <summary>A Chrome that paints the Cell Editor, keeping the contract it was last handed.</summary>
    private sealed class EditorChrome : IGridChrome
    {
        public CellEditorContext? EditorHanded { get; private set; }

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context)
        {
            EditorHanded = context;
            return builder => builder.AddMarkupContent(0, "<span class='stub-editor'></span>");
        }

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;
    }

    [Fact] // ADR-0050 section 6 / SH-29: a character typed onto a cell opens an edit, and the grid says so once
    public async Task Typing_onto_a_cell_raises_the_opening()
    {
        var told = new List<bool>();
        var cut = RenderGrid(told);
        await ClickCellAsync(cut, 50, 10);
        Assert.Empty(told);

        await PressAsync(cut, "9");
        await TypeAsync(cut, "99");

        Assert.Equal([true], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0010: F2 opens an edit; F2 again moves between the two states, and the edit is still open
    public async Task F2_raises_the_opening_and_a_change_of_state_raises_nothing()
    {
        var told = new List<bool>();
        var cut = RenderGrid(told);
        await ClickCellAsync(cut, 50, 10);

        await PressAsync(cut, "F2");
        await PressAsync(cut, "F2");
        await PressAsync(cut, "F2");

        Assert.Equal([true], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0010: a double click opens Caret, and that is an edit opening
    public async Task A_double_click_raises_the_opening()
    {
        var told = new List<bool>();
        var cut = RenderGrid(told);

        await cut.Find(".ex-viewport").DoubleClickAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 10 });

        Assert.Equal([true], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0051: a press into the Formula Bar opens an edit on the Focus cell
    public async Task A_press_into_the_formula_bar_raises_the_opening()
    {
        var told = new List<bool>();
        var cut = RenderGrid(told, extra: ps => ps.Add(g => g.ShowFormulaBar, true));
        await ClickCellAsync(cut, 50, 10);

        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());

        Assert.Equal([true], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0007: Enter commits, the intent leaves, and the edit has ended
    public async Task A_commit_raises_the_end()
    {
        var told = new List<bool>();
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(told, edits);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");

        await PressAsync(cut, "Enter");

        Assert.Single(edits);
        Assert.Equal([true, false], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0010: a press past the editor commits it, and the edit has ended
    public async Task A_press_past_the_editor_raises_the_end()
    {
        var told = new List<bool>();
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(told, edits);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");

        await ClickCellAsync(cut, 50, 50);

        Assert.Single(edits);
        Assert.Equal([true, false], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0035: Ctrl+Enter commits the text over the Selection, and the edit has ended
    public async Task A_fill_by_ctrl_enter_raises_the_end()
    {
        var told = new List<bool>();
        var pastes = new List<GridPasteIntent>();
        var cut = RenderGrid(told, extra: ps => ps.Add(g => g.OnPaste, pastes.Add));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");

        await PressAsync(cut, "Enter", ctrl: true);

        Assert.Single(pastes);
        Assert.Equal([true, false], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0007: Escape cancels, the text dies with the editor, and the edit has ended
    public async Task A_cancel_raises_the_end()
    {
        var told = new List<bool>();
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(told, edits);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");

        await PressAsync(cut, "Escape");

        Assert.Empty(edits);
        Assert.Equal([true, false], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0011 (note of 2026-10-07): an order change landing under the editor ends nothing — the edit stays open, and the Consumer hears no end
    public async Task An_order_change_under_the_editor_raises_no_end()
    {
        var told = new List<bool>();
        var discarded = new List<EditDiscardReason>();
        var cut = RenderGrid(told, extra: ps => ps.Add(g => g.OnEditDiscarded, discarded.Add));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");

        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 1));

        Assert.Empty(discarded);
        Assert.Equal([true], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0011: a row that left the Window takes the typing at the commit, and the edit has ended
    public async Task A_discard_at_the_commit_raises_the_end()
    {
        var told = new List<bool>();
        var edits = new List<GridEditIntent<TestRow>>();
        var discarded = new List<EditDiscardReason>();
        var cut = RenderGrid(told, edits, totalCount: 500, extra: ps => ps.Add(g => g.OnEditDiscarded, discarded.Add));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");
        cut.Render(ps => ps.Add(g => g.WindowStart, 100).Add(g => g.Window, TestRows.Many(50)));

        await PressAsync(cut, "Enter");

        Assert.Empty(edits);
        Assert.Equal([EditDiscardReason.RowLeftTheWindow], discarded);
        Assert.Equal([true, false], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0011: a column change landing under the editor discards the edit, and it has ended
    public async Task A_discard_by_a_column_change_raises_the_end()
    {
        var told = new List<bool>();
        var discarded = new List<EditDiscardReason>();
        var cut = RenderGrid(told, extra: ps => ps.Add(g => g.OnEditDiscarded, discarded.Add));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");

        cut.Render(ps => ps.Add(g => g.Columns, (GridColumn<TestRow>[])
            [new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true)]));

        Assert.Equal([EditDiscardReason.ColumnsChanged], discarded);
        Assert.Equal([true, false], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0035: a column that stopped being Editable takes the typing at the commit, and the edit has ended
    public async Task A_discard_because_the_column_stopped_being_editable_raises_the_end()
    {
        var told = new List<bool>();
        var edits = new List<GridEditIntent<TestRow>>();
        var discarded = new List<EditDiscardReason>();
        var cut = RenderGrid(told, edits, extra: ps => ps.Add(g => g.OnEditDiscarded, discarded.Add));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");
        // Same names, so the selection and the editor both stand: only the declaration moved.
        cut.Render(ps => ps.Add(g => g.Columns, (GridColumn<TestRow>[])
            [new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
             new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100)]));
        Assert.Equal([true], told);

        await PressAsync(cut, "Enter");

        Assert.Empty(edits);
        Assert.Equal([EditDiscardReason.ColumnNoLongerEditable], discarded);
        Assert.Equal([true, false], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0010: a Chrome's editor commits through its contract, and the edit has ended
    public async Task A_commit_from_a_chrome_editor_raises_the_end()
    {
        var told = new List<bool>();
        var edits = new List<GridEditIntent<TestRow>>();
        var chrome = new EditorChrome();
        var cut = RenderGrid(told, edits, extra: ps => ps.Add(g => g.Chrome, chrome));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");
        Assert.Equal([true], told);

        await cut.InvokeAsync(() => chrome.EditorHanded!.Commit());

        Assert.Single(edits);
        Assert.Equal([true, false], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0010: a Chrome's editor cancels through its contract, and the edit has ended
    public async Task A_cancel_from_a_chrome_editor_raises_the_end()
    {
        var told = new List<bool>();
        var edits = new List<GridEditIntent<TestRow>>();
        var chrome = new EditorChrome();
        var cut = RenderGrid(told, edits, extra: ps => ps.Add(g => g.Chrome, chrome));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");

        await cut.InvokeAsync(() => chrome.EditorHanded!.Cancel());

        Assert.Empty(edits);
        Assert.Equal([true, false], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0051: a press that points writes a Reference into the edit, which stays open, and raises nothing
    public async Task A_press_that_points_raises_nothing()
    {
        var told = new List<bool>();
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(told, edits, extra: ps => ps
            .Add(g => g.PointAt, (text, caret) => text.StartsWith('=') && caret > 0 && caret <= text.Length && "=+-*/(,".Contains(text[caret - 1]))
            .Add(g => g.ReferenceText, (SelectionRange range) => FormattableString.Invariant($"R{range.TopRow + 1}")));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "=");

        await ClickCellAsync(cut, 50, 50);

        Assert.Equal("=R3", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
        Assert.Empty(edits);
        Assert.Equal([true], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0010: the key gate is told without waiting for the Consumer's handler
    public async Task The_key_gate_does_not_wait_for_the_consumers_handler()
    {
        // A handler that awaits something — an application fetching, logging — and has not
        // finished: the gate must hear the new mode regardless, or keys go on being claimed as
        // the mode before.
        var held = new TaskCompletionSource();
        var told = new List<bool>();
        var cut = RenderGrid(told: null, extra: ps => ps.Add(g => g.OnEditingChanged, (bool open) =>
        {
            told.Add(open);
            return held.Task;
        }));
        await ClickCellAsync(cut, 50, 10);

        // The browser answers at once here, so a key that does not wait for the handler is answered
        // in the turn it was raised in; one that waited would not be answered while the handler is
        // held, which is to the end of the test.
        var opening = await KeyAndReadInOneTurnAsync(cut, "9", () => (Told: told.ToArray(), Gate: GateModesTold()[^1]));

        Assert.True(opening.Answered);
        Assert.Equal([true], opening.Read.Told);
        Assert.Equal("overwrite", opening.Read.Gate);
        Assert.NotEmpty(cut.FindAll(".ex-editor"));

        var ending = await KeyAndReadInOneTurnAsync(cut, "Escape", () => (Told: told.ToArray(), Gate: GateModesTold()[^1]));

        Assert.True(ending.Answered);
        Assert.Equal([true, false], ending.Read.Told);
        Assert.Equal("none", ending.Read.Gate);
        Assert.False(held.Task.IsCompleted);
        held.SetResult();
    }

    [Fact] // ADR-0050 section 6 / ADR-0010: the browser's messages go out before the Consumer hears, so a render the Consumer causes comes behind them
    public async Task The_key_gate_and_the_keyboard_are_told_before_the_consumer_hears()
    {
        // A Consumer that renders on hearing — /sheet greys out its buttons, and renders the
        // grid with it — sends that render the moment it hears. Heard first on a circuit, the
        // render removed the editor while it held DOM focus, ahead of the hand-back, and a paste
        // after the edit went past the grid (found by layer 3 on the Server host, 2026-09-30).
        var heard = new List<(bool Open, string Gate, int Reclaims)>();
        var cut = RenderGrid(told: null, extra: ps => ps.Add(g => g.OnEditingChanged, (bool open) =>
            heard.Add((open, GateModesTold()[^1], Js.FocusReclaimed.Invocations.Count))));
        await ClickCellAsync(cut, 50, 10);

        await PressAsync(cut, "9");
        await PressAsync(cut, "Escape");

        Assert.Equal([true, false], heard.Select(h => h.Open));
        Assert.Equal("overwrite", heard[0].Gate);
        Assert.Equal("none", heard[1].Gate);
        Assert.Equal(heard[0].Reclaims + 1, heard[1].Reclaims);
    }

    [Fact] // ADR-0050 section 6 / ADR-0007: the Edit Intent reaches the Consumer before the end, so the value is handled before anything done on hearing the end
    public async Task The_edit_intent_is_raised_before_the_end()
    {
        var heard = new List<string>();
        var cut = RenderGrid(
            told: null,
            extra: ps => ps.Add(g => g.OnEditingChanged, (bool open) => heard.Add(open ? "open" : "end")),
            onEdit: _ => heard.Add("intent"));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");

        await PressAsync(cut, "Enter");

        Assert.Equal(["open", "intent", "end"], heard);
    }

    [Fact] // ADR-0050 section 6 / ADR-0035: a Ctrl+Enter fill's paste intent reaches the Consumer before the end
    public async Task A_fill_by_ctrl_enter_is_raised_before_the_end()
    {
        var heard = new List<string>();
        var cut = RenderGrid(told: null, extra: ps => ps
            .Add(g => g.OnEditingChanged, (bool open) => heard.Add(open ? "open" : "end"))
            .Add(g => g.OnPaste, (GridPasteIntent _) => heard.Add("paste")));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");

        await PressAsync(cut, "Enter", ctrl: true);

        Assert.Equal(["open", "paste", "end"], heard);
    }

    [Fact] // ADR-0050 section 6 / ADR-0011: a discard at the commit is raised before the end
    public async Task A_discard_at_the_commit_is_raised_before_the_end()
    {
        var heard = new List<string>();
        var cut = RenderGrid(told: null, totalCount: 500, extra: ps => ps
            .Add(g => g.OnEditingChanged, (bool open) => heard.Add(open ? "open" : "end"))
            .Add(g => g.OnEditDiscarded, (EditDiscardReason reason) => heard.Add(reason.ToString())));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");
        cut.Render(ps => ps.Add(g => g.WindowStart, 100).Add(g => g.Window, TestRows.Many(50)));

        await PressAsync(cut, "Enter");

        Assert.Equal(["open", nameof(EditDiscardReason.RowLeftTheWindow), "end"], heard);
    }

    [Fact] // ADR-0050 section 6: a discard the Consumer asks for ends the edit like any other
    public async Task A_discard_the_consumer_asks_for_raises_the_end()
    {
        var told = new List<bool>();
        var discarded = new List<EditDiscardReason>();
        var cut = RenderGrid(told, extra: ps => ps.Add(g => g.OnEditDiscarded, discarded.Add));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");

        Assert.True(await cut.Instance.DiscardEditAsync("The data under the editor was replaced."));

        Assert.Equal([EditDiscardReason.DiscardedByConsumer], discarded);
        Assert.Equal([true, false], told);
    }

    [Fact] // ADR-0050 section 6 / ADR-0034: a Reject holds the editor, so the edit has not ended, and nothing is raised
    public async Task A_rejected_commit_raises_nothing_and_the_edit_stays_open()
    {
        var told = new List<bool>();
        var cut = RenderGrid(told, validate: (_, _) => EditVerdict.Reject("no"));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");

        await PressAsync(cut, "Enter");
        await ClickCellAsync(cut, 50, 50);

        Assert.NotEmpty(cut.FindAll(".ex-editor"));
        Assert.Equal([true], told);

        await PressAsync(cut, "Escape");

        Assert.Equal([true, false], told);
    }

    [Fact] // ADR-0050 section 6: raised in C# where the state changes, not a round trip later behind the key gate
    public async Task The_opening_and_the_end_are_raised_before_the_browser_has_answered()
    {
        var told = new List<bool>();
        var cut = RenderGrid(told);
        await ClickCellAsync(cut, 50, 10);
        // Stood up on the dispatcher, like the keys: the interop's handlers are the grid's to read.
        var gate = await ReadWhenIdleAsync(Js.UnansweredGateMode);

        var opening = await KeyAndReadInOneTurnAsync(cut, "9", told.ToArray);

        // An opening's key is answered once the gate has the edit (ADR-0010); the Consumer has
        // heard before that.
        Assert.False(opening.Answered);
        Assert.Equal([true], opening.Read);
        gate.SetVoidResult();
        await opening.Key;

        gate = await ReadWhenIdleAsync(Js.UnansweredGateMode);
        var ending = await KeyAndReadInOneTurnAsync(cut, "Escape", told.ToArray);

        // An ending's is not held for the gate's reply at all (PushEditingStateAsync).
        Assert.Equal([true, false], ending.Read);
        Assert.True(ending.Answered);
        gate.SetVoidResult();
        await ending.Key;
    }

    [Fact] // ADR-0050 section 6 / ADR-0010: a commit's value, its Focus move and the key's answer go out together; the gesture does not wait for the browser's replies
    public async Task A_commit_moves_the_focus_and_answers_without_waiting_for_the_browser()
    {
        // Found by CI on the Server host (2026-09-30). The Consumer renders a committed value the
        // moment it hears it, which is before the end and before anything is waited for (ADR-0050
        // section 6). The Focus move and the Enter's answer waited for the gate's reply behind
        // that render: for a round trip the page showed the value with the Focus still on the
        // edited cell, and a Ctrl+C typed on seeing it was held behind the unanswered Enter,
        // which the listener cannot replay as the browser's copy, and lost.
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(told: null, edits);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");
        var gate = await ReadWhenIdleAsync(Js.UnansweredGateMode);

        var enter = await KeyAndReadInOneTurnAsync(cut, "Enter", () =>
            (Edits: edits.Count, Gate: GateModesTold()[^1], Focus: KeyboardHolder.ActiveDescendant(cut.Find(".ex-grid"))));

        Assert.Equal(1, enter.Read.Edits);
        Assert.Equal("none", enter.Read.Gate);
        Assert.EndsWith("-r1c0", enter.Read.Focus);
        Assert.True(enter.Answered);
        gate.SetVoidResult();
        await enter.Key;
    }

    [Fact] // ADR-0050 section 6 / SH-29: off by default — a Consumer that does not listen edits and commits exactly as before
    public async Task Without_a_listener_an_edit_opens_and_commits_as_before()
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(told: null, edits);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");
        await TypeAsync(cut, "99");
        Assert.NotEmpty(cut.FindAll(".ex-editor"));

        await PressAsync(cut, "Enter");

        Assert.Equal("99", Assert.Single(edits).Value);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }
}
