using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Keys;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Undo and redo reaching the Consumer (ADR-0007, KB-39; used by ExSheet as ADR-0050 item 8, DC-30). The C# half: which
/// keys the gate is handed, and what a forwarded key raises. That the real browser keeps the keys
/// in the editor while one is open, and leaves them to the page when undeclared, is layer 3's.
/// </summary>
public class UndoRedoKeyTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private sealed class History
    {
        public int Undos;
        public int Redos;
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(History? history, bool undo = true, bool redo = true)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(20))
              .Add(g => g.TotalCount, 20)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            if (history is not null && undo)
                ps.Add(g => g.OnUndo, () => history.Undos++);
            if (history is not null && redo)
                ps.Add(g => g.OnRedo, () => history.Redos++);
        });

    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false,
        bool meta = false, bool metaIsPrimary = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, meta, metaIsPrimary));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    [Fact] // ADR-0007 / ADR-0050 item 8 / DC-30 / DC-1: undeclared, the gate is handed none of the history keys
    public void Undeclared_the_keys_stay_the_browsers()
    {
        RenderGrid(history: null);

        foreach (var key in new[] { "Control+z", "Control+Z", "Control+y", "Control+Y", "Control+Shift+Z", "Control+Shift+z" })
        {
            Assert.DoesNotContain(key, Js.TakenAtAttach);
        }
        Assert.Empty(Js.ClaimsTold.Invocations);
    }

    [Fact] // ADR-0007 / ADR-0050 item 8 / DC-30: declared, the gate claims Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z
    public void Declared_the_gate_claims_the_history_keys()
    {
        RenderGrid(new History());

        Assert.Contains("Control+z", Js.TakenAtAttach);
        Assert.Contains("Control+y", Js.TakenAtAttach);
        Assert.Contains("Control+Shift+Z", Js.TakenAtAttach);
        // Everything the grid claimed before is still claimed.
        Assert.All(GridKeys.Taken, key => Assert.Contains(key, Js.TakenAtAttach));
    }

    [Fact] // ADR-0007 / ADR-0050 item 8: a declaration covers its own keys only; an undeclared redo stays the browser's
    public void Only_the_declared_side_is_claimed()
    {
        RenderGrid(new History(), undo: true, redo: false);

        Assert.Contains("Control+z", Js.TakenAtAttach);
        Assert.DoesNotContain("Control+y", Js.TakenAtAttach);
        Assert.DoesNotContain("Control+Shift+Z", Js.TakenAtAttach);
    }

    [Fact] // ADR-0007 / ADR-0050 item 8 / DC-30: with no edit open, Ctrl+Z raises undo, Ctrl+Y and Ctrl+Shift+Z redo
    public async Task The_keys_raise_undo_and_redo()
    {
        var history = new History();
        var cut = RenderGrid(history);

        await PressAsync(cut, "z", ctrl: true);
        await PressAsync(cut, "y", ctrl: true);
        await PressAsync(cut, "Z", ctrl: true, shift: true);

        Assert.Equal(1, history.Undos);
        Assert.Equal(2, history.Redos);
    }

    [Fact] // ADR-0007 / ADR-0050 item 8 / ADR-0012: Command is the Primary Modifier on an Apple keyboard
    public async Task Command_raises_them_on_an_apple_platform()
    {
        var history = new History();
        var cut = RenderGrid(history);

        await PressAsync(cut, "z", meta: true, metaIsPrimary: true);
        await PressAsync(cut, "z", shift: true, meta: true, metaIsPrimary: true);
        // Elsewhere Meta is the OS's, and the grid does not answer to it.
        await PressAsync(cut, "z", meta: true, metaIsPrimary: false);

        Assert.Equal(1, history.Undos);
        Assert.Equal(1, history.Redos);
    }

    [Fact] // ADR-0007 / ADR-0050 item 8 / ADR-0007 / DC-30: while an edit is open, the keys are the editor's, never the Consumer's undo
    public async Task While_an_edit_is_open_the_keys_are_the_editors()
    {
        var history = new History();
        var cut = RenderGrid(history);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "F2");

        await PressAsync(cut, "z", ctrl: true);
        await PressAsync(cut, "y", ctrl: true);

        Assert.Equal(0, history.Undos);
        Assert.Equal(0, history.Redos);
        // The edit is still open, on its text.
        Assert.Equal("Row 000000", cut.Find(".ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0007 / ADR-0050 item 8 / ADR-0003: a history key renders no row
    public async Task A_history_key_renders_no_row()
    {
        var cut = RenderGrid(new History());
        await ClickCellAsync(cut, 50, 10);
        var counts = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await PressAsync(cut, "z", ctrl: true);

        Assert.Equal(counts, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }

    [Fact] // ADR-0007 / ADR-0050 item 8: a declaration made after attach re-tells the gate; an unchanged one does not
    public void A_declaration_made_later_re_tells_the_gate()
    {
        var history = new History();
        var rows = TestRows.Many(20);
        var columns = Columns();
        void Base(ComponentParameterCollectionBuilder<ExGrid<TestRow>> ps) => ps
            .Add(g => g.Window, rows).Add(g => g.TotalCount, 20).Add(g => g.Columns, columns)
            .Add(g => g.RowHeight, 20d).Add(g => g.ViewportHeight, 120).Add(g => g.ViewportWidth, 350);
        var cut = Render<ExGrid<TestRow>>(Base);
        var undo = EventCallback.Factory.Create(this, () => history.Undos++);

        cut.Render(ps => { Base(ps); ps.Add(g => g.OnUndo, undo); });
        cut.Render(ps => { Base(ps); ps.Add(g => g.OnUndo, undo); });

        var told = Assert.Single(Js.ClaimsTold.Invocations);
        var keys = (IReadOnlyList<string>)told.Arguments[0]!;
        Assert.Contains("Control+z", keys);
        Assert.DoesNotContain("Control+y", keys);
    }
}
