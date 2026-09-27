using System.Globalization;
using Bunit;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// The one undo stack (ticket 12, ADR-0048): every operation, the user's and the Consumer's, is
/// one engine step in the order it was done; replacing the document clears it; each ExSheet has
/// its own (ADR-0018). Driven through the component's public <c>UndoAsync</c> and
/// <c>RedoAsync</c> — Ctrl+Z and Ctrl+Y do not reach ExSheet yet (the ticket says why).
/// </summary>
public class UndoStackTests : SheetTestContext
{
    [Fact] // ADR-0048: undo and redo step through edits in order, one edit per step
    public async Task Undo_and_redo_step_through_edits_in_order()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "A1", "2");
        await EnterAsync(cut, "B1", "=A1*10");
        Assert.Equal("20", CellText(cut, "B1"));

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("", CellText(cut, "B1"));
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("1", CellText(cut, "A1"));
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("", CellText(cut, "A1"));
        Assert.False(await cut.Instance.UndoAsync());

        Assert.True(await cut.Instance.RedoAsync());
        Assert.True(await cut.Instance.RedoAsync());
        Assert.True(await cut.Instance.RedoAsync());
        Assert.Equal("20", CellText(cut, "B1"));
        Assert.False(await cut.Instance.RedoAsync());
    }

    [Fact] // ADR-0048/0003: an undo repaints only the rows it changed
    public async Task An_undo_repaints_only_the_rows_it_changed()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "A4", "4");
        var before = cut.FindComponents<global::ExGrid.Components.ExGridRow<SheetRow>>().ToDictionary(r => r.Instance.RowIndex, r => r.Instance.Row);

        await cut.Instance.UndoAsync();

        foreach (var row in cut.FindComponents<global::ExGrid.Components.ExGridRow<SheetRow>>())
        {
            if (row.Instance.RowIndex == 3) Assert.NotSame(before[3], row.Instance.Row);
            else Assert.Same(before[row.Instance.RowIndex], row.Instance.Row);
        }
    }

    [Fact] // ADR-0048: a new operation after an undo forgets what was undone
    public async Task A_new_edit_forgets_the_undone_steps()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1");
        await cut.Instance.UndoAsync();

        await EnterAsync(cut, "A2", "2");

        Assert.False(cut.Instance.CanRedo);
        Assert.False(await cut.Instance.RedoAsync());
    }

    [Fact] // ADR-0048, SH-13: a Consumer command lands on the same stack and is undone in its place in the order
    public async Task A_consumer_command_is_undone_in_its_place()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1");
        await cut.InvokeAsync(() => cut.Instance.DoAsync(SheetEdit.InsertRows(0)));
        await EnterAsync(cut, "A1", "top");
        Assert.Equal("1", CellText(cut, "A2"));

        await cut.Instance.UndoAsync();
        Assert.Equal("", CellText(cut, "A1"));
        Assert.Equal("1", CellText(cut, "A2"));

        await cut.Instance.UndoAsync();
        Assert.Equal("1", CellText(cut, "A1"));
        Assert.Equal("", CellText(cut, "A2"));

        await cut.Instance.UndoAsync();
        Assert.Equal("", CellText(cut, "A1"));
    }

    [Fact] // ADR-0048: a refused Consumer command changes nothing and is no step
    public async Task A_refused_command_is_no_step()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1048576"), "last");
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));
        await EnterAsync(cut, "A1", "first");

        await Assert.ThrowsAsync<SheetRefusedException>(() => cut.Instance.DoAsync(SheetEdit.InsertRows(0)));

        Assert.True(await cut.Instance.UndoAsync());
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0048: an undo raises the Sheet Document like any change
    public async Task An_undo_raises_the_document()
    {
        var raised = new List<SheetDocument>();
        var cut = RenderSheet(ps => ps.Add(s => s.DocumentChanged, raised.Add));
        await EnterAsync(cut, "A1", "1");

        await cut.Instance.UndoAsync();

        Assert.Equal(2, raised.Count);
        Assert.Empty(raised[^1].Cells);
    }

    [Fact] // ADR-0048, SH-13: replacing the Sheet Document clears the stack
    public async Task Replacing_the_document_clears_the_stack()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "A2", "2");
        await cut.Instance.UndoAsync();

        cut.Render(ps => ps.Add(s => s.Document, new Sheet(CultureInfo.GetCultureInfo("en-US")).ToDocument()));

        Assert.False(cut.Instance.CanUndo);
        Assert.False(cut.Instance.CanRedo);
        Assert.False(await cut.Instance.UndoAsync());
    }

    [Fact] // ADR-0048/0018, SH-13: two ExSheets on one page keep separate stacks
    public async Task Two_sheets_keep_separate_stacks()
    {
        var first = RenderSheet();
        var second = RenderSheet();
        await EnterAsync(first, "A1", "first");
        await EnterAsync(second, "A1", "second");
        await EnterAsync(second, "A2", "again");

        Assert.True(await first.Instance.UndoAsync());

        Assert.Equal("", CellText(first, "A1"));
        Assert.Equal("second", CellText(second, "A1"));
        Assert.Equal("again", CellText(second, "A2"));
        Assert.False(first.Instance.CanUndo);
        Assert.True(second.Instance.CanUndo);
    }
}
