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
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, totalCount)
              .Add(g => g.Columns, Columns(validate))
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 160)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) => edits?.Add(intent));
            if (told is not null)
                ps.Add(g => g.OnEditingChanged, told.Add);
            extra?.Invoke(ps);
        });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, false, false, false, false));

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

    [Fact] // ADR-0050 section 6 / ADR-0011: an order change landing under the editor discards the edit, and it has ended
    public async Task A_discard_by_an_order_change_raises_the_end()
    {
        var told = new List<bool>();
        var discarded = new List<EditDiscardReason>();
        var cut = RenderGrid(told, extra: ps => ps.Add(g => g.OnEditDiscarded, discarded.Add));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");

        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 1));

        Assert.Equal([EditDiscardReason.OrderChanged], discarded);
        Assert.Equal([true, false], told);
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

        await PressAsync(cut, "9").WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        Assert.Equal([true], told);
        Assert.Equal("overwrite", GateModesTold()[^1]);
        Assert.NotEmpty(cut.FindAll(".ex-editor"));

        await PressAsync(cut, "Escape").WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        Assert.Equal([true, false], told);
        Assert.Equal("none", GateModesTold()[^1]);
        Assert.False(held.Task.IsCompleted);
        held.SetResult();
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
        var gate = Js.UnansweredGateMode();

        var opening = PressAsync(cut, "9");

        Assert.False(opening.IsCompleted);
        Assert.Equal([true], told);
        gate.SetVoidResult();
        await opening;

        gate = Js.UnansweredGateMode();
        var ending = PressAsync(cut, "Escape");

        Assert.False(ending.IsCompleted);
        Assert.Equal([true, false], told);
        gate.SetVoidResult();
        await ending;
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
