using Bunit;
using ExGrid.Components;
using Microsoft.AspNetCore.Components.Web;
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
/// Ctrl and an arrow write nothing, leave the text as it was, and tell why. After a press on a column's
/// header (ticket 71; Part B of the ninth run, Q52), ↓ points at the column's first row, ← and → at
/// the next column the table has, as a column, and ↑ is an edge; after a drag took back what its press
/// wrote, the arrows are the Sheet's own Point. The page is
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

    /// <summary>The dashes down the body of the column whose left edge is at <paramref name="left"/>.</summary>
    private static string ColumnDashesAt(int left) => FormattableString.Invariant($"left: {left}px; top: 0px; width: 100px; height: ");

    [Fact] // ADR-0058 (Part B of the ninth run, Q52) / SH-35: after a press on a column's header, ↓ points at the column's first row, replacing the column; the arrows then go on from that cell
    public async Task After_a_header_press_down_points_at_the_columns_first_row()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");
        await PressHeaderAsync(page.Positions, ValueX);
        Assert.Equal("=SUM(Positions[PV]", EditorText(page.Left));

        await ArrowAsync(page.Left, "ArrowDown");

        Assert.Equal("=SUM(" + LookupR1, EditorText(page.Left));
        Assert.Equal([ValueCellDashes(0)], DashesOf(page.Positions));
        Assert.Equal("", NameBox(page.Left));
        Assert.Empty(page.Positions.FindAll(".ex-focus, .ex-range"));
        Assert.Empty(page.Cut.Instance.LeftRefusals);

        await ArrowAsync(page.Left, "ArrowDown");
        Assert.Equal("=SUM(" + LookupR2, EditorText(page.Left));
        await TypeAsync(page.Left, "=SUM(" + LookupR2 + ")");
        await PressAsync(page.Left, "Enter");
        Assert.Equal("250", CellText(page.Left, "D2"));
    }

    [Fact] // ADR-0058 (Part B of the ninth run, Q52) / SH-35: the column's first row is the first in the grid's current order, and the grid scrolls it into view
    public async Task After_a_header_press_down_points_at_the_first_row_of_the_current_order_and_scrolls_to_it()
    {
        var page = await RenderAsync();
        Position[] numbered = Numbered(20);
        await page.Cut.Instance.ShowAsync(numbered);
        await StartTypingAsync(page.Left, "D2", "=");
        // ↓ from the last row in view scrolls the grid by a row: R-1 is above the view.
        await PressCellAsync(page.Positions, ValueX, Row(7));
        await ArrowAsync(page.Left, "ArrowDown");
        Assert.Equal("=XLOOKUP(\"R-9\", Positions[Id], Positions[PV])", EditorText(page.Left));
        // The Consumer sorts, descending: R-20 is first.
        await page.Cut.Instance.ShowAsync([.. numbered.Reverse()]);
        await PressHeaderAsync(page.Positions, ValueX);
        Assert.Equal("=Positions[PV]", EditorText(page.Left));
        var scrolls = ScrollsAsked(JSInterop);

        await ArrowAsync(page.Left, "ArrowDown");

        Assert.Equal("=XLOOKUP(\"R-20\", Positions[Id], Positions[PV])", EditorText(page.Left));
        Assert.True(ScrollsAsked(JSInterop) > scrolls, "the registered grid was not scrolled to the column's first row");
        Assert.Equal([ValueCellDashes(0)], DashesOf(page.Positions));
        Assert.Empty(page.Cut.Instance.LeftRefusals);
    }

    [Fact] // ADR-0058 (Part B of the ninth run, Q52) / SH-35: after a press on a column's header, ← and → point at the next column the table has, as a column, passing over the grid's own; with none that way nothing moves
    public async Task After_a_header_press_left_and_right_point_at_the_next_column_as_a_column()
    {
        var page = await RenderAsync();
        // Note, the grid's own column, now stands between Id and Book.
        await page.Cut.Instance.ReorderAsync("Id", "Note", "Book", "Value");
        await StartTypingAsync(page.Left, "D2", "=SUM(");
        await PressHeaderAsync(page.Positions, 350);
        Assert.Equal("=SUM(Positions[PV]", EditorText(page.Left));

        // Nothing right of Value.
        await ArrowAsync(page.Left, "ArrowRight");
        Assert.Equal("=SUM(Positions[PV]", EditorText(page.Left));

        await ArrowAsync(page.Left, "ArrowLeft");
        Assert.Equal("=SUM(Positions[Book]", EditorText(page.Left));
        Assert.StartsWith(ColumnDashesAt(200), Assert.Single(DashesOf(page.Positions)));

        await ArrowAsync(page.Left, "ArrowLeft");
        Assert.Equal("=SUM(Positions[Id]", EditorText(page.Left));
        Assert.StartsWith(ColumnDashesAt(0), Assert.Single(DashesOf(page.Positions)));

        // Nothing left of Id.
        await ArrowAsync(page.Left, "ArrowLeft");
        Assert.Equal("=SUM(Positions[Id]", EditorText(page.Left));

        await ArrowAsync(page.Left, "ArrowRight");
        Assert.Equal("=SUM(Positions[Book]", EditorText(page.Left));
        Assert.Empty(page.Cut.Instance.LeftRefusals);

        // Still a column: ↓ points at its first row.
        await ArrowAsync(page.Left, "ArrowRight");
        await ArrowAsync(page.Left, "ArrowDown");
        Assert.Equal("=SUM(" + LookupR1, EditorText(page.Left));
    }

    /// <summary>Every offset a grid on the page has asked the browser to scroll to, in the order asked.</summary>
    private List<(double Top, double Left)> ScrollOffsetsAsked()
        => [.. JSInterop.Invocations
            .Where(invocation => invocation.Identifier == "setScrollOffset")
            .Select(invocation => ((double)invocation.Arguments[0]!, (double)invocation.Arguments[1]!))];

    [Fact] // ADR-0058 (2026-10-01) / SH-35 / DC-55: after a header press, the column ← or → reaches is scrolled into view across only, the vertical offset left as it is; one whole in view moves nothing
    public async Task After_a_header_press_the_column_reached_is_scrolled_into_view_across_only()
    {
        var page = await RenderAsync();
        // 250px wide: Id and Book whole in view, Value cut off at 250px, Note out of view.
        await page.Cut.Instance.NarrowAsync(250);
        await ScrollToAsync(page.Positions, 40, 0);
        await StartTypingAsync(page.Left, "D2", "=");
        await PressHeaderAsync(page.Positions, IdX);
        Assert.Equal("=Positions[Id]", EditorText(page.Left));
        var asked = ScrollOffsetsAsked().Count;

        // Book is whole in view: nothing moves.
        await ArrowAsync(page.Left, "ArrowRight");
        Assert.Equal("=Positions[Book]", EditorText(page.Left));
        Assert.Empty(ScrollOffsetsAsked()[asked..]);

        // Value (200 to 300px) is brought to the view's right edge, 40px down as before.
        await ArrowAsync(page.Left, "ArrowRight");
        Assert.Equal("=Positions[PV]", EditorText(page.Left));
        // Down Value's body, cut to the rows painted 40px down.
        Assert.StartsWith("left: 200px; ", Assert.Single(DashesOf(page.Positions)));
        Assert.Equal([(40d, 50d)], ScrollOffsetsAsked()[asked..]);

        // Book is whole in view again from there; Id is brought back to the left edge.
        await ArrowAsync(page.Left, "ArrowLeft");
        Assert.Equal("=Positions[Book]", EditorText(page.Left));
        await ArrowAsync(page.Left, "ArrowLeft");
        Assert.Equal("=Positions[Id]", EditorText(page.Left));
        Assert.Equal([(40d, 50d), (40d, 0d)], ScrollOffsetsAsked()[asked..]);
        Assert.Empty(page.Positions.FindAll(".ex-focus, .ex-range"));
        Assert.Empty(page.Cut.Instance.LeftRefusals);
    }

    [Fact] // ADR-0058 (Part B of the ninth run, Q52) / SH-35: after a press on a column's header ↑ is an edge, and Shift and Ctrl with an arrow are refused as from a cell; ↓ still points
    public async Task After_a_header_press_up_is_an_edge_and_shift_and_ctrl_are_refused_as_from_a_cell()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");
        await PressHeaderAsync(page.Positions, ValueX);

        await ArrowAsync(page.Left, "ArrowUp");
        Assert.Empty(page.Cut.Instance.LeftRefusals);

        await ArrowAsync(page.Left, "ArrowDown", shift: true);
        await ArrowAsync(page.Left, "ArrowLeft", ctrl: true);
        await ArrowAsync(page.Left, "ArrowDown", shift: true, ctrl: true);

        Assert.Equal("=SUM(Positions[PV]", EditorText(page.Left));
        // The column stays dashed: Point over what the press wrote goes on.
        Assert.StartsWith(ColumnDashesAt(200), Assert.Single(DashesOf(page.Positions)));
        var refusals = page.Cut.Instance.LeftRefusals;
        Assert.Equal(
            [PointingRefusalReason.SeveralCells, PointingRefusalReason.DataEdge, PointingRefusalReason.DataEdge],
            refusals.Select(r => r.Reason));
        Assert.Contains("Shift+arrow", refusals[0].Message);
        Assert.Contains("Ctrl+arrow", refusals[1].Message);

        await ArrowAsync(page.Left, "ArrowDown");
        Assert.Equal("=SUM(" + LookupR1, EditorText(page.Left));
    }

    [Fact] // ADR-0058 (Part B of the ninth run, Q52) / SH-35: a first row whose key is blank, or that has not arrived, is refused as an arrow reaching it is: the column stays written and dashed
    public async Task After_a_header_press_down_onto_a_first_row_that_cannot_be_written_writes_nothing()
    {
        var page = await RenderAsync();
        var rows = Support.ScopedSheets.Positions;
        // The blank key is first.
        await page.Cut.Instance.ShowAsync([rows[3], rows[0]]);
        await StartTypingAsync(page.Left, "D2", "=SUM(");
        await PressHeaderAsync(page.Positions, ValueX);

        await ArrowAsync(page.Left, "ArrowDown");

        Assert.Equal("=SUM(Positions[PV]", EditorText(page.Left));
        Assert.StartsWith(ColumnDashesAt(200), Assert.Single(DashesOf(page.Positions)));
        Assert.Equal(PointingRefusalReason.BlankKey, Assert.Single(page.Cut.Instance.LeftRefusals).Reason);

        // The rows shown begin further on: the first has not arrived.
        await page.Cut.Instance.ShowAsync(rows, from: 30);
        await ArrowAsync(page.Left, "ArrowDown");

        Assert.Equal("=SUM(Positions[PV]", EditorText(page.Left));
        Assert.Equal(
            [PointingRefusalReason.BlankKey, PointingRefusalReason.RowNotArrived],
            page.Cut.Instance.LeftRefusals.Select(r => r.Reason));
    }

    [Fact] // ADR-0058 ("Settled while building ticket 41") / SH-35: after a header press, a column the grid no longer shows has nowhere to move from: nothing is written, and the reason is told
    public async Task After_a_header_press_a_column_the_grid_no_longer_shows_writes_nothing_and_tells_why()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");
        await PressHeaderAsync(page.Positions, IdX);
        Assert.Equal("=SUM(Positions[Id]", EditorText(page.Left));

        // Id is no longer shown.
        await page.Cut.Instance.ReorderAsync("Book", "Value", "Note");
        await ArrowAsync(page.Left, "ArrowDown");
        await ArrowAsync(page.Left, "ArrowRight");

        Assert.Equal("=SUM(Positions[Id]", EditorText(page.Left));
        var refusals = page.Cut.Instance.LeftRefusals;
        Assert.Equal([PointingRefusalReason.CellNotHeld, PointingRefusalReason.CellNotHeld], refusals.Select(r => r.Reason));
        Assert.Contains("column 'Id'", refusals[0].Message);
    }

    [Fact] // ADR-0058 ("Settled while building ticket 41") / SH-35: an arrow after a header press is a gesture of its own: a drag still held from that press takes back nothing
    public async Task A_drag_held_from_a_header_press_takes_back_nothing_an_arrow_wrote()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");
        var header = page.Positions.Find(".ex-header");
        await header.MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = ValueX, OffsetY = 30, ClientX = ValueX, ClientY = 30 });
        Assert.Equal("=SUM(Positions[PV]", EditorText(page.Left));

        await ArrowAsync(page.Left, "ArrowLeft");
        Assert.Equal("=SUM(Positions[Book]", EditorText(page.Left));
        await page.Positions.Find(".ex-header").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = IdX, OffsetY = 30, ClientX = IdX, ClientY = 30 });

        Assert.Equal("=SUM(Positions[Book]", EditorText(page.Left));
        var refusal = Assert.Single(page.Cut.Instance.LeftRefusals);
        Assert.Equal(PointingRefusalReason.SeveralColumns, refusal.Reason);
        Assert.DoesNotContain("taken back", refusal.Message);
    }

    [Fact] // ADR-0058 (Part B of the ninth run) / SH-35: after a drag took back what its press wrote, the arrows are the Sheet's own Point, from the edited cell, and the drag's reason is the only one told
    public async Task After_a_drag_took_back_its_press_the_arrows_are_the_sheets_own_point()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");
        var viewport = page.Positions.Find(".ex-viewport");
        await viewport.MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = ValueX, OffsetY = Row(0), ClientX = ValueX, ClientY = 40 + Row(0) });
        Assert.Equal("=" + LookupR1, EditorText(page.Left));
        await page.Positions.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = ValueX, OffsetY = Row(1), ClientX = ValueX, ClientY = 40 + Row(1) });
        await page.Positions.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = ValueX, OffsetY = Row(1) });
        Assert.Equal("=", EditorText(page.Left));

        await ArrowAsync(page.Left, "ArrowDown");
        Assert.Equal("=D3", EditorText(page.Left));
        await ArrowAsync(page.Left, "ArrowRight");
        Assert.Equal("=E3", EditorText(page.Left));

        Assert.Empty(DashesOf(page.Positions));
        var refusal = Assert.Single(page.Cut.Instance.LeftRefusals);
        Assert.Equal(PointingRefusalReason.SeveralCells, refusal.Reason);
        Assert.Contains("taken back", refusal.Message);
    }

    [Fact] // ADR-0058 (Part B of the ninth run) / SH-35: after a drag across headers took back what its press wrote, the arrows are the Sheet's own Point too
    public async Task After_a_header_drag_took_back_its_press_the_arrows_are_the_sheets_own_point()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");
        await page.Positions.Find(".ex-header").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = ValueX, OffsetY = 30, ClientX = ValueX, ClientY = 30 });
        Assert.Equal("=Positions[PV]", EditorText(page.Left));
        await page.Positions.Find(".ex-header").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = BookX, OffsetY = 30, ClientX = BookX, ClientY = 30 });
        Assert.Equal("=", EditorText(page.Left));

        await ArrowAsync(page.Left, "ArrowDown");

        Assert.Equal("=D3", EditorText(page.Left));
        Assert.Empty(DashesOf(page.Positions));
        Assert.Equal(PointingRefusalReason.SeveralColumns, Assert.Single(page.Cut.Instance.LeftRefusals).Reason);
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
