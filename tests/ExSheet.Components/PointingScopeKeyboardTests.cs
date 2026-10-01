using Bunit;
using ExGrid.Components;
using Xunit;
using Position = ExSheet.Components.Tests.Support.ScopedSheets.Position;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.Components.Tests;

/// <summary>
/// The arrow keys after a press on a registered grid (ticket 41; ADR-0058, "The keyboard", as the
/// ninth Windows run settled it; SH-35's layer 2 half): ↑ and ↓ point one row further in the grid's
/// current order, and ← and → to the next column the table has, passing over the grid's columns the
/// table does not have; each rewrites what this Point wrote, moves the dashes, and has the grid scroll
/// the cell into view. At an edge nothing moves. A row that has not arrived, Shift and an arrow, and
/// Ctrl and an arrow write nothing, leave the text as it was, and tell why. The page is
/// <see cref="Support.ScopedSheets"/>: R-1, R-2, R-"5" and a blank key in rows 0 to 3, then rows that
/// have not arrived; Id, Book, Value (the table's PV) and Note (the grid's own).
/// </summary>
public partial class PointingScopeTests
{
    private const string LookupR1 = "XLOOKUP(\"R-1\", Positions[Id], Positions[PV])";

    /// <summary>An arrow key in the Sheet's Cell Editor, as the key listener forwards it while the edit
    /// is open: with the editor's text, and the caret after it, where Point left it.</summary>
    private static Task ArrowAsync(IRenderedComponent<SheetComponent> sheet, string key, bool shift = false, bool ctrl = false)
    {
        var grid = Grid(sheet);
        var text = EditorText(sheet);
        return grid.InvokeAsync(() => grid.Instance.OnKeyAsync(
            key, ctrl, shift, false, false, false, editorText: text, editorCaret: text.Length, editorSelectionEnd: text.Length));
    }

    /// <summary>Rows R-1 to R-<paramref name="count"/>, each with its number as its PV.</summary>
    private static Position[] Numbered(int count)
        => [.. Enumerable.Range(1, count).Select(i => new Position($"R-{i}", "Book", i, ""))];

    [Fact] // ADR-0058 ("The keyboard") / SH-35: =, a press on R-1's PV, ↓ gives R-2's lookup and moves the dashes; ↑ goes back; *2 and Enter compute it
    public async Task Down_and_up_point_one_row_further_in_the_registered_grid()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");
        await PressCellAsync(page.Positions, ValueX, Row(0));
        Assert.Equal("=" + LookupR1, EditorText(page.Left));

        await ArrowAsync(page.Left, "ArrowDown");

        Assert.Equal("=" + LookupR2, EditorText(page.Left));
        Assert.Equal([ValueCellDashes(1)], DashesOf(page.Positions));
        Assert.Equal("", NameBox(page.Left));
        // The keyboard and the Selection stayed where they were: the registered grid kept none.
        Assert.Empty(page.Positions.FindAll(".ex-focus, .ex-range"));
        Assert.True(PointedAt(page.Positions));

        await ArrowAsync(page.Left, "ArrowUp");
        Assert.Equal("=" + LookupR1, EditorText(page.Left));
        Assert.Equal([ValueCellDashes(0)], DashesOf(page.Positions));
        Assert.Empty(page.Cut.Instance.LeftRefusals);

        await ArrowAsync(page.Left, "ArrowDown");
        await TypeAsync(page.Left, "=" + LookupR2 + "*2");
        Assert.Empty(DashesOf(page.Positions));
        await PressAsync(page.Left, "Enter");
        Assert.Equal("500", CellText(page.Left, "D2"));
    }

    [Fact] // ADR-0058 ("The keyboard") / SH-35: ← and → move to the next column the table has, passing over the grid's columns the table does not have
    public async Task Left_and_right_pass_over_the_columns_the_table_does_not_have()
    {
        var page = await RenderAsync();
        // Note, the grid's own column, now stands between Id and Book.
        await page.Cut.Instance.ReorderAsync("Id", "Note", "Book", "Value");
        await StartTypingAsync(page.Left, "D2", "=");
        await PressCellAsync(page.Positions, IdX, Row(1));
        Assert.Equal("=XLOOKUP(\"R-2\", Positions[Id], Positions[Id])", EditorText(page.Left));

        await ArrowAsync(page.Left, "ArrowRight");
        Assert.Equal("=XLOOKUP(\"R-2\", Positions[Id], Positions[Book])", EditorText(page.Left));
        Assert.Equal(["left: 200px; top: 20px; width: 100px; height: 20px"], DashesOf(page.Positions));

        await ArrowAsync(page.Left, "ArrowRight");
        Assert.Equal("=" + LookupR2, EditorText(page.Left));

        // Nothing further right: nothing moves, and nothing is told.
        await ArrowAsync(page.Left, "ArrowRight");
        Assert.Equal("=" + LookupR2, EditorText(page.Left));

        await ArrowAsync(page.Left, "ArrowLeft");
        await ArrowAsync(page.Left, "ArrowLeft");
        Assert.Equal("=XLOOKUP(\"R-2\", Positions[Id], Positions[Id])", EditorText(page.Left));
        Assert.Equal(["left: 0px; top: 20px; width: 100px; height: 20px"], DashesOf(page.Positions));
        Assert.Empty(page.Cut.Instance.LeftRefusals);
    }

    [Fact] // ADR-0058 ("The keyboard") / SH-35: at an edge nothing moves — above the first row, and right of the last column the table has — and nothing is told
    public async Task At_an_edge_nothing_moves()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=1+");
        await PressCellAsync(page.Positions, ValueX, Row(0));

        await ArrowAsync(page.Left, "ArrowUp");
        // Note, right of Value, is the grid's own.
        await ArrowAsync(page.Left, "ArrowRight");

        Assert.Equal("=1+" + LookupR1, EditorText(page.Left));
        Assert.Equal([ValueCellDashes(0)], DashesOf(page.Positions));
        Assert.Empty(page.Cut.Instance.LeftRefusals);
    }

    [Fact] // ADR-0058 ("The keyboard") / SH-35: ↑ and ↓ follow the grid's current order, after a sort, whatever row the press was on
    public async Task The_arrows_follow_the_grids_current_order()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");
        await PressCellAsync(page.Positions, ValueX, Row(0));

        // The Consumer sorts: R-1 is third, after R-"5" and before the blank key.
        var rows = Support.ScopedSheets.Positions;
        await page.Cut.Instance.ShowAsync([rows[1], rows[2], rows[0], rows[3]]);
        Assert.Equal([ValueCellDashes(2)], DashesOf(page.Positions));

        await ArrowAsync(page.Left, "ArrowUp");

        Assert.Equal("=XLOOKUP(\"R-\"\"5\"\"\", Positions[Id], Positions[PV])", EditorText(page.Left));
        Assert.Equal([ValueCellDashes(1)], DashesOf(page.Positions));
    }

    [Fact] // ADR-0058 ("The keyboard") / SH-35: a row that has not arrived writes nothing, leaves the text as it was, and tells why
    public async Task A_row_that_has_not_arrived_writes_nothing_and_tells_why()
    {
        var page = await RenderAsync();
        var rows = Support.ScopedSheets.Positions;
        // Two rows arrived of the twenty-two the grid counts.
        await page.Cut.Instance.ShowAsync([rows[0], rows[1]]);
        await StartTypingAsync(page.Left, "D2", "=");
        await PressCellAsync(page.Positions, ValueX, Row(1));

        await ArrowAsync(page.Left, "ArrowDown");

        Assert.Equal("=" + LookupR2, EditorText(page.Left));
        Assert.Equal([ValueCellDashes(1)], DashesOf(page.Positions));
        var refusal = Assert.Single(page.Cut.Instance.LeftRefusals);
        Assert.Equal(PointingRefusalReason.RowNotArrived, refusal.Reason);
        Assert.Contains("has not arrived", refusal.Message);
    }

    [Fact] // ADR-0058 ("The keyboard") / SH-35 / SH-32: an arrow onto a row whose key is blank writes nothing, as a press there does, and tells why
    public async Task An_arrow_onto_a_blank_key_writes_nothing_and_tells_why()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");
        await PressCellAsync(page.Positions, ValueX, Row(2));
        var written = EditorText(page.Left);

        await ArrowAsync(page.Left, "ArrowDown");

        Assert.Equal(written, EditorText(page.Left));
        Assert.Equal([ValueCellDashes(2)], DashesOf(page.Positions));
        Assert.Equal(PointingRefusalReason.BlankKey, Assert.Single(page.Cut.Instance.LeftRefusals).Reason);
    }

    [Fact] // ADR-0058 ("The keyboard") / SH-35: Shift and an arrow is refused as a range is, and Ctrl and an arrow is not built: each writes nothing, leaves the text, and tells why
    public async Task Shift_and_ctrl_with_an_arrow_write_nothing_and_tell_why()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");
        await PressCellAsync(page.Positions, ValueX, Row(0));

        await ArrowAsync(page.Left, "ArrowDown", shift: true);
        await ArrowAsync(page.Left, "ArrowDown", ctrl: true);
        await ArrowAsync(page.Left, "ArrowRight", shift: true, ctrl: true);

        Assert.Equal("=SUM(" + LookupR1, EditorText(page.Left));
        Assert.Equal([ValueCellDashes(0)], DashesOf(page.Positions));
        var refusals = page.Cut.Instance.LeftRefusals;
        Assert.Equal(
            [PointingRefusalReason.SeveralCells, PointingRefusalReason.DataEdge, PointingRefusalReason.DataEdge],
            refusals.Select(r => r.Reason));
        Assert.Contains("Shift+arrow", refusals[0].Message);
        Assert.Contains("Ctrl+arrow", refusals[1].Message);
        // Point over the lookup goes on: the next arrow still moves it.
        await ArrowAsync(page.Left, "ArrowDown");
        Assert.Equal("=SUM(" + LookupR2, EditorText(page.Left));
    }

    [Fact] // ADR-0058 ("The keyboard") / SH-35 / DC-55: ↓ past the painted rows scrolls the registered grid to the cell, and the dashes are drawn there
    public async Task Down_past_the_painted_rows_scrolls_the_registered_grid()
    {
        var page = await RenderAsync();
        await page.Cut.Instance.ShowAsync(Numbered(20));
        await StartTypingAsync(page.Left, "D2", "=");
        // The last row in view: eight rows of 20px under the 40px header band.
        await PressCellAsync(page.Positions, ValueX, Row(7));
        Assert.Equal("=XLOOKUP(\"R-8\", Positions[Id], Positions[PV])", EditorText(page.Left));
        var scrolls = ScrollsAsked(JSInterop);

        await ArrowAsync(page.Left, "ArrowDown");

        Assert.Equal("=XLOOKUP(\"R-9\", Positions[Id], Positions[PV])", EditorText(page.Left));
        Assert.True(ScrollsAsked(JSInterop) > scrolls, "the registered grid was not scrolled to the pointed cell");
        // Scrolled by one row: R-9 is the last row in view, and dashed there.
        Assert.Equal([ValueCellDashes(7)], DashesOf(page.Positions));
        Assert.Empty(page.Cut.Instance.LeftRefusals);
    }

    [Fact] // ADR-0058 ("The keyboard"; decided with the user 2026-10-01, until Excel is observed) / SH-35: after a press on a column's header an arrow writes nothing, leaves the text, and tells why; Shift and Ctrl with one are told as after a cell
    public async Task After_a_header_press_an_arrow_writes_nothing_and_tells_why()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");
        await PressHeaderAsync(page.Positions, ValueX);

        await ArrowAsync(page.Left, "ArrowDown");
        await ArrowAsync(page.Left, "ArrowLeft");
        await ArrowAsync(page.Left, "ArrowDown", shift: true);
        await ArrowAsync(page.Left, "ArrowDown", ctrl: true);

        Assert.Equal("=SUM(Positions[PV]", EditorText(page.Left));
        // The column stays dashed: Point over what the press wrote goes on.
        Assert.StartsWith("left: 200px; top: 0px; width: 100px; height: ", Assert.Single(DashesOf(page.Positions)));
        var refusals = page.Cut.Instance.LeftRefusals;
        Assert.Equal(
            [PointingRefusalReason.FromColumnHeader, PointingRefusalReason.FromColumnHeader, PointingRefusalReason.SeveralCells, PointingRefusalReason.DataEdge],
            refusals.Select(r => r.Reason));
        Assert.Contains("Press a cell to point by keys", refusals[0].Message);
        // A press on a cell, and the arrows point by keys.
        await PressCellAsync(page.Positions, ValueX, Row(0));
        await ArrowAsync(page.Left, "ArrowDown");
        Assert.Equal("=SUM(" + LookupR2, EditorText(page.Left));
    }

    [Fact] // ADR-0058 ("The keyboard") / SH-35: a cell the grid no longer holds has nowhere to move from: nothing is written, and the reason is told
    public async Task A_cell_the_grid_no_longer_holds_writes_nothing_and_tells_why()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");
        await PressCellAsync(page.Positions, ValueX, Row(0));
        var rows = Support.ScopedSheets.Positions;

        // R-1 leaves the rows the grid holds.
        await page.Cut.Instance.ShowAsync([rows[1], rows[2]]);
        await ArrowAsync(page.Left, "ArrowDown");

        Assert.Equal("=" + LookupR1, EditorText(page.Left));
        Assert.Equal(PointingRefusalReason.CellNotHeld, Assert.Single(page.Cut.Instance.LeftRefusals).Reason);
    }

    [Fact] // ADR-0058 / ADR-0051: after a press on the Sheet's own cell the arrows point in the Sheet again
    public async Task After_a_press_on_the_sheet_the_arrows_point_in_the_sheet()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=1+");
        await PressCellAsync(page.Positions, ValueX, Row(0));
        await PressSheetCellAsync(page.Left, row: 2, column: 1);
        Assert.Equal("=1+B3", EditorText(page.Left));

        await ArrowAsync(page.Left, "ArrowDown");

        Assert.Equal("=1+B4", EditorText(page.Left));
        Assert.Empty(DashesOf(page.Positions));
    }

    [Fact] // ADR-0058 ("The keyboard") / SH-35: an arrow in a Sheet whose press did not write leaves the other Sheet's Point alone
    public async Task An_arrow_moves_only_what_its_own_sheets_press_wrote()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");
        await PressCellAsync(page.Positions, ValueX, Row(0));
        await KeyboardOutAsync(page.Left);
        await StartTypingAsync(page.Right, "E5", "=");

        await ArrowAsync(page.Right, "ArrowDown");

        // The Right Sheet's own Point: E6. The Left Sheet's lookup and its dashes stand.
        Assert.Equal("=E6", EditorText(page.Right));
        Assert.Equal("=" + LookupR1, EditorText(page.Left));
        Assert.Equal([ValueCellDashes(0)], DashesOf(page.Positions));
        Assert.Empty(page.Cut.Instance.RightRefusals);
    }
}
