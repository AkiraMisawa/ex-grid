using System.Globalization;
using Bunit;
using ExGrid.Keys;
using ExGrid.Selection;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// Excel's insert and delete keys on ExSheet (ticket 142; ADR-0050 item 14's note of 2026-10-02;
/// SH-48): Ctrl with <c>+</c>, with Shift or without it, and Ctrl with <c>-</c>. Whole rows insert or
/// delete rows, whole columns columns, each one undo step; any other Selection, and a key while an
/// edit is open, changes nothing and says why. That the browser does not zoom the page is layer 3's.
/// </summary>
public class StructureKeyTests : SheetTestContext
{
    private static SheetDocument DocumentOf(params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    private static string Notice(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-sheet-notice").TextContent;

    private static readonly GridExtent SheetExtent = new(Sheet.RowCount, Sheet.ColumnCount);

    // Ctrl+Shift+= on a US or UK layout types +; the keypad's + comes without Shift.
    private static Task InsertKeyAsync(IRenderedComponent<ExSheet> cut, bool shift = true) => PressAsync(cut, "+", ctrl: true, shift: shift);

    private static Task DeleteKeyAsync(IRenderedComponent<ExSheet> cut) => PressAsync(cut, "-", ctrl: true);

    [Fact] // ADR-0050 item 14, SH-48: the insert and delete keys are declared beside the formatting keys
    public void The_insert_and_delete_keys_are_declared()
    {
        var declared = Grid(RenderSheet()).Instance.DeclaredKeys!;

        Assert.Contains("Control+Shift++", declared);
        Assert.Contains("Control++", declared);
        Assert.Contains("Control+-", declared);
        Assert.Equal(SheetFormatKeys.Declared.Count + 3, declared.Count);
    }

    [Theory] // SH-48 (Part C, case 12): Ctrl+Plus over whole rows inserts as many rows above, one undo step
    [InlineData(true)]
    [InlineData(false)]
    public async Task Ctrl_plus_over_whole_rows_inserts_rows_above(bool shift)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A2", "x"), ("B1", "=A2"))));
        await GoToAsync(cut, "2:3");

        await InsertKeyAsync(cut, shift);

        Assert.Equal("", CellText(cut, "A2"));
        Assert.Equal("x", CellText(cut, "A4"));
        Assert.Equal("x", CellText(cut, "B1"));
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("x", CellText(cut, "A2"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // SH-48: Ctrl+Minus over whole rows deletes them
    public async Task Ctrl_minus_over_whole_rows_deletes_them()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A2", "gone"), ("A3", "stays"))));
        await GoToAsync(cut, "2:2");

        await DeleteKeyAsync(cut);

        Assert.Equal("stays", CellText(cut, "A2"));
    }

    [Fact] // SH-48: over whole columns the keys insert and delete columns
    public async Task Over_whole_columns_the_keys_insert_and_delete_columns()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("B1", "b"), ("C1", "c"))));
        await GoToAsync(cut, "B:B");

        await InsertKeyAsync(cut);
        Assert.Equal("", CellText(cut, "B1"));
        Assert.Equal("b", CellText(cut, "C1"));

        await GoToAsync(cut, "B:C");
        await DeleteKeyAsync(cut);
        Assert.Equal("c", CellText(cut, "B1"));
    }

    [Theory] // SH-48: any other Selection changes nothing and says why — a range, a cell, every cell
    [InlineData("A2:B3")]
    [InlineData("C5")]
    public async Task Any_other_selection_changes_nothing_and_says_why(string address)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A2", "x"))));
        await GoToAsync(cut, address);

        await InsertKeyAsync(cut);
        Assert.Contains("whole rows", Notice(cut));
        await DeleteKeyAsync(cut);

        Assert.Contains("Nothing was deleted", Notice(cut));
        Assert.Equal("x", CellText(cut, "A2"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // SH-48: every cell at once is neither whole rows nor whole columns alone, and is refused
    public async Task Every_cell_at_once_is_refused()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A2", "x"))));
        var everything = GridSelection.Empty.Click(new CellPosition(0, 0), SheetExtent).SelectAll(SheetExtent);
        var grid = Grid(cut);

        await grid.InvokeAsync(() => grid.Instance.OnDeclaredKey.InvokeAsync(new GridDeclaredKeyPress("Control+-", false, everything, 0)));

        Assert.Contains("Nothing was deleted", Notice(cut));
        Assert.Equal("x", CellText(cut, "A2"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // SH-48: several ranges have no one span, and are refused
    public async Task Several_ranges_are_refused()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A2", "x"))));
        var two = GridSelection.Empty.SelectRow(1, SheetExtent, 0).ToggleRow(4, SheetExtent, 0);
        Assert.Equal(2, two.Ranges.Count);
        var grid = Grid(cut);

        await grid.InvokeAsync(() => grid.Instance.OnDeclaredKey.InvokeAsync(new GridDeclaredKeyPress("Control+Shift++", false, two, 0)));

        Assert.Contains("Nothing was inserted", Notice(cut));
        Assert.Equal("x", CellText(cut, "A2"));
    }

    [Fact] // SH-48 / ADR-0050 item 14: the key acts on the Selection it carries, not the one last heard
    public async Task The_key_acts_on_the_selection_it_carries()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A2", "x"), ("A5", "y"))));
        await GoToAsync(cut, "A2");
        var rowFive = GridSelection.Empty.Click(new CellPosition(4, 0), SheetExtent).SelectWholeRows(SheetExtent);
        var grid = Grid(cut);

        await grid.InvokeAsync(() => grid.Instance.OnDeclaredKey.InvokeAsync(new GridDeclaredKeyPress("Control+-", false, rowFive, 0)));

        Assert.Equal("x", CellText(cut, "A2"));
        Assert.Equal("", CellText(cut, "A5"));
    }

    [Fact] // SH-48: while an edit is open the key changes nothing and says why
    public async Task While_an_edit_is_open_the_key_changes_nothing()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A2", "x"))));
        await GoToAsync(cut, "2:2");
        var grid = Grid(cut);
        var selection = GridSelection.Empty.SelectRow(1, SheetExtent, 0);

        await grid.InvokeAsync(() => grid.Instance.OnDeclaredKey.InvokeAsync(new GridDeclaredKeyPress("Control+Shift++", true, selection, 0)));

        Assert.Contains("a cell is being edited", Notice(cut));
        Assert.Equal("x", CellText(cut, "A2"));
        Assert.False(cut.Instance.CanUndo);
    }
}
