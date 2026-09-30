using Bunit;
using ExGrid.Cells;
using ExGrid.Components;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Position = ExSheet.Components.Tests.Support.ScopedSheets.Position;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.Components.Tests;

/// <summary>
/// A Pointing Scope (ticket 37, ADR-0058; SH-32 and SH-35's layer 2 half): while a Sheet of the
/// Scope holds the keyboard, has an edit open and is in Point, a press on a registered grid writes
/// what reads the pressed cell by key, or the pressed column, where Point writes — in the table's
/// column names — or writes nothing and tells the Consumer why; a drag takes back what its press
/// wrote. A grid in no Scope, a registered grid with an open edit of its own, and another Sheet are
/// never pointed at, and only the Sheet that holds the keyboard points. The page is
/// <see cref="ScopedSheets"/>: rows of 20px under a header band of 40px (one tier of Header Groups);
/// Id, Book, Value (the table's PV) and Note (the grid's own) are 100px each.
/// </summary>
public partial class PointingScopeTests : SheetTestContext
{
    private const string LookupR2 = "XLOOKUP(\"R-2\", Positions[Id], Positions[PV])";

    // The x of each column's middle, and the y of each row's, in the registered grid.
    private const double IdX = 50, BookX = 150, ValueX = 250, NoteX = 350;

    private static double Row(int row) => (row * 20) + 10;

    private static readonly string[] TableColumns = ["Id", "Book", "PV"];

    private sealed record Page(
        IRenderedComponent<ScopedSheets> Cut,
        IRenderedComponent<SheetComponent> Left,
        IRenderedComponent<SheetComponent> Right,
        IRenderedComponent<ExGrid<Position>> Positions,
        IRenderedComponent<ExGrid<Position>> Unregistered);

    /// <summary>The page, with Positions declared on both Sheets with the key <paramref name="key"/>,
    /// or not declared at all, and the positions pushed.</summary>
    private async Task<Page> RenderAsync(string? key = "Id", bool declare = true)
    {
        var cut = RenderPage<ScopedSheets>();
        var sheets = cut.FindComponents<SheetComponent>();
        var grids = cut.FindComponents<ExGrid<Position>>();
        if (declare)
        {
            foreach (var sheet in new[] { cut.Instance.Left!, cut.Instance.Right! })
            {
                await sheet.DeclareLinkedTableAsync("Positions", TableColumns, key);
                await sheet.PushLinkedTableAsync("Positions", ScopedSheets.Positions.Select(ScopedSheets.TableRow));
            }
        }
        return new Page(cut, sheets[0], sheets[1], grids[0], grids[1]);
    }

    /// <summary>DOM focus comes into a Sheet, as the browser tells it.</summary>
    private static Task KeyboardInAsync(IRenderedComponent<SheetComponent> sheet) => sheet.Find(".ex-sheet").FocusInAsync(new FocusEventArgs());

    /// <summary>DOM focus leaves a Sheet for somewhere else on the page.</summary>
    private static Task KeyboardOutAsync(IRenderedComponent<SheetComponent> sheet) => sheet.Find(".ex-sheet").FocusOutAsync(new FocusEventArgs());

    /// <summary>The keyboard in the Sheet, the Focus at <paramref name="address"/>, and
    /// <paramref name="typed"/> typed there.</summary>
    private static async Task StartTypingAsync(IRenderedComponent<SheetComponent> sheet, string address, string typed)
    {
        await KeyboardInAsync(sheet);
        await GoToAsync(sheet, address);
        await PressAsync(sheet, typed[..1]);
        if (typed.Length > 1) await TypeAsync(sheet, typed);
    }

    private static async Task PressCellAsync(IRenderedComponent<ExGrid<Position>> grid, double x, double y, bool shift = false)
    {
        var press = new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ClientX = x, ClientY = 40 + y, ShiftKey = shift };
        await grid.Find(".ex-viewport").MouseDownAsync(press);
        var release = new MouseEventArgs { Button = 0, Buttons = 0, OffsetX = x, OffsetY = y, ClientX = x, ClientY = 40 + y, ShiftKey = shift };
        await grid.Find(".ex-viewport").MouseUpAsync(release);
    }

    /// <summary>A press on the header and, as a browser follows it, its release and its click.</summary>
    private static async Task PressHeaderAsync(IRenderedComponent<ExGrid<Position>> grid, double x, double y = 30, bool shift = false)
    {
        await grid.Find(".ex-header").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ClientX = x, ClientY = y, ShiftKey = shift });
        var release = new MouseEventArgs { Button = 0, Buttons = 0, OffsetX = x, OffsetY = y, ClientX = x, ClientY = y, ShiftKey = shift };
        if (grid.Find(".ex-header").HasAttribute("blazor:onmouseup"))
            await grid.Find(".ex-header").MouseUpAsync(release);
        await grid.Find(".ex-header").ClickAsync(release);
    }

    private static bool PointedAt(IRenderedComponent<ExGrid<Position>> grid) =>
        (grid.Find(".ex-grid").GetAttribute("class") ?? "").Split(' ').Contains("ex-pointed-at");

    private static string NameBox(IRenderedComponent<SheetComponent> sheet) =>
        sheet.Find("input.ex-name-box").GetAttribute("value") ?? "";

    [Fact] // ADR-0058 / SH-32: = and a press on a cell writes XLOOKUP by the row's key, in the table's column names; *2 and Enter compute it
    public async Task A_press_on_a_cell_writes_the_lookup_of_its_row_by_key()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");
        Assert.True(PointedAt(page.Positions));

        // R-2's cell in the grid column Value, which is the table's PV.
        await PressCellAsync(page.Positions, ValueX, Row(1));

        Assert.Equal("=" + LookupR2, EditorText(page.Left));
        Assert.Empty(page.Cut.Instance.LeftRefusals);
        // The grid kept neither a Selection nor a Focus for it.
        Assert.Empty(page.Positions.FindAll(".ex-focus, .ex-range"));
        await TypeAsync(page.Left, "=" + LookupR2 + "*2");
        await PressAsync(page.Left, "Enter");
        Assert.Equal("500", CellText(page.Left, "D2"));
    }

    [Fact] // ADR-0058 / SH-32: =SUM( and a press on a column header writes the table's column, ) and Enter compute it
    public async Task A_press_on_a_column_header_writes_the_tables_column()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");

        await PressHeaderAsync(page.Positions, ValueX);

        Assert.Equal("=SUM(Positions[PV]", EditorText(page.Left));
        await TypeAsync(page.Left, "=SUM(Positions[PV])");
        await PressAsync(page.Left, "Enter");
        Assert.Equal("360", CellText(page.Left, "D2"));
    }

    [Fact] // ADR-0058 / SH-32: the key is written as Excel writes a constant of its kind: text quoted, with a quote inside doubled
    public async Task A_key_with_a_quote_is_written_with_the_quote_doubled()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");

        await PressCellAsync(page.Positions, BookX, Row(2));

        Assert.Equal("=XLOOKUP(\"R-\"\"5\"\"\", Positions[Id], Positions[Book])", EditorText(page.Left));
        await PressAsync(page.Left, "Enter");
        Assert.Equal("Credit", CellText(page.Left, "D2"));
    }

    [Fact] // ADR-0058 / SH-32: a further press, on a registered grid or on the Sheet, replaces what this Point wrote
    public async Task A_further_press_replaces_what_this_point_wrote()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=1+");

        await PressCellAsync(page.Positions, ValueX, Row(1));
        await PressHeaderAsync(page.Positions, IdX);
        Assert.Equal("=1+Positions[Id]", EditorText(page.Left));

        await PressCellAsync(page.Positions, ValueX, Row(0));
        Assert.Equal("=1+XLOOKUP(\"R-1\", Positions[Id], Positions[PV])", EditorText(page.Left));

        // A press on the Sheet's own B3.
        await PressSheetCellAsync(page.Left, row: 2, column: 1);
        Assert.Equal("=1+B3", EditorText(page.Left));
        Assert.Equal("B3", NameBox(page.Left));
    }

    [Fact] // ADR-0058 / SH-35: after a press on a registered grid the Name Box is empty and F4 changes nothing
    public async Task After_a_press_the_name_box_is_empty_and_f4_changes_nothing()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");
        Assert.Equal("D2", NameBox(page.Left));

        await PressCellAsync(page.Positions, ValueX, Row(1));
        Assert.Equal("", NameBox(page.Left));
        await PressInEditorAsync(page.Left, "F4", "=" + LookupR2, 1 + LookupR2.Length);

        Assert.Equal("=" + LookupR2, EditorText(page.Left));
        Assert.Equal("", NameBox(page.Left));
    }

    [Fact] // ADR-0058 / SH-32: more than one cell by Shift writes nothing, and the reason is told
    public async Task Shift_and_a_press_writes_nothing_and_tells_why()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");

        await PressCellAsync(page.Positions, ValueX, Row(1), shift: true);

        Assert.Equal("=SUM(", EditorText(page.Left));
        var refusal = Assert.Single(page.Cut.Instance.LeftRefusals);
        Assert.Equal(PointingRefusalReason.SeveralCells, refusal.Reason);
        Assert.Contains("more than one cell", refusal.Message);
        Assert.Empty(page.Cut.Instance.RightRefusals);
    }

    [Fact] // ADR-0058 / SH-32 (2026-09-30): a drag that reaches another cell takes back what its press wrote, and tells why
    public async Task A_drag_takes_back_what_its_press_wrote()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");
        var viewport = page.Positions.Find(".ex-viewport");

        await viewport.MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = ValueX, OffsetY = Row(0), ClientX = ValueX, ClientY = 40 + Row(0) });
        Assert.Equal("=SUM(XLOOKUP(\"R-1\", Positions[Id], Positions[PV])", EditorText(page.Left));
        await page.Positions.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = ValueX, OffsetY = Row(1), ClientX = ValueX, ClientY = 40 + Row(1) });

        Assert.Equal("=SUM(", EditorText(page.Left));
        var refusal = Assert.Single(page.Cut.Instance.LeftRefusals);
        Assert.Equal(PointingRefusalReason.SeveralCells, refusal.Reason);
        Assert.Contains("taken back", refusal.Message);
        // Pointing goes on: the next press writes again.
        await page.Positions.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = ValueX, OffsetY = Row(1) });
        await PressCellAsync(page.Positions, ValueX, Row(1));
        Assert.Equal("=SUM(" + LookupR2, EditorText(page.Left));
    }

    [Fact] // ADR-0058 / SH-32: a drag from a press that wrote nothing takes back nothing an earlier press wrote
    public async Task A_drag_from_a_refused_press_takes_back_nothing()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");
        await PressCellAsync(page.Positions, ValueX, Row(1));
        Assert.Equal("=SUM(" + LookupR2, EditorText(page.Left));

        // Note is not the table's: that press writes nothing, and the drag from it takes nothing back.
        await page.Positions.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = NoteX, OffsetY = Row(0) });
        await page.Positions.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = NoteX, OffsetY = Row(2) });

        Assert.Equal("=SUM(" + LookupR2, EditorText(page.Left));
        Assert.Equal(
            [PointingRefusalReason.ColumnNotInTable, PointingRefusalReason.SeveralCells],
            page.Cut.Instance.LeftRefusals.Select(r => r.Reason));
        Assert.DoesNotContain("taken back", page.Cut.Instance.LeftRefusals[1].Message);
    }

    [Fact] // ADR-0058 / SH-32: several columns, by Shift or by a drag across headers, write nothing; the drag takes back its press
    public async Task Several_columns_write_nothing()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");

        await PressHeaderAsync(page.Positions, IdX, shift: true);
        Assert.Equal("=SUM(", EditorText(page.Left));

        await page.Positions.Find(".ex-header").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = IdX, OffsetY = 30, ClientX = IdX, ClientY = 30 });
        Assert.Equal("=SUM(Positions[Id]", EditorText(page.Left));
        await page.Positions.Find(".ex-header").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = ValueX, OffsetY = 30, ClientX = ValueX, ClientY = 30 });

        Assert.Equal("=SUM(", EditorText(page.Left));
        Assert.Equal(
            [PointingRefusalReason.SeveralColumns, PointingRefusalReason.SeveralColumns],
            page.Cut.Instance.LeftRefusals.Select(r => r.Reason));
        Assert.Contains("taken back", page.Cut.Instance.LeftRefusals[1].Message);
    }

    [Fact] // ADR-0058 / SH-32 / ADR-0032: a Header Group's rectangle writes nothing; the leaf beneath it writes its column
    public async Task A_header_group_writes_nothing()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");

        await PressHeaderAsync(page.Positions, BookX, y: 5);
        Assert.Equal("=SUM(", EditorText(page.Left));
        Assert.Equal(PointingRefusalReason.HeaderGroup, Assert.Single(page.Cut.Instance.LeftRefusals).Reason);

        await PressHeaderAsync(page.Positions, BookX, y: 30);
        Assert.Equal("=SUM(Positions[Book]", EditorText(page.Left));
    }

    [Fact] // ADR-0058 / SH-32: a column the table does not have writes nothing, from a cell or a header
    public async Task A_column_the_table_does_not_have_writes_nothing()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(");

        await PressCellAsync(page.Positions, NoteX, Row(1));
        await PressHeaderAsync(page.Positions, NoteX);

        Assert.Equal("=SUM(", EditorText(page.Left));
        Assert.All(page.Cut.Instance.LeftRefusals, r => Assert.Equal(PointingRefusalReason.ColumnNotInTable, r.Reason));
        Assert.Equal(2, page.Cut.Instance.LeftRefusals.Count);
        Assert.Contains("'Note'", page.Cut.Instance.LeftRefusals[0].Message);
    }

    [Fact] // ADR-0058 / SH-32: a cell of a table declared without a key writes nothing; its column header still writes the column
    public async Task A_cell_of_a_table_without_a_key_writes_nothing()
    {
        var page = await RenderAsync(key: null);
        await StartTypingAsync(page.Left, "D2", "=SUM(");

        await PressCellAsync(page.Positions, ValueX, Row(1));
        Assert.Equal("=SUM(", EditorText(page.Left));
        Assert.Equal(PointingRefusalReason.NoKey, Assert.Single(page.Cut.Instance.LeftRefusals).Reason);

        await PressHeaderAsync(page.Positions, ValueX);
        Assert.Equal("=SUM(Positions[PV]", EditorText(page.Left));
    }

    [Fact] // ADR-0058 / SH-32: a cell whose key is blank writes nothing
    public async Task A_cell_whose_key_is_blank_writes_nothing()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");

        await PressCellAsync(page.Positions, ValueX, Row(3));

        Assert.Equal("=", EditorText(page.Left));
        Assert.Equal(PointingRefusalReason.BlankKey, Assert.Single(page.Cut.Instance.LeftRefusals).Reason);
    }

    [Fact] // ADR-0058 / SH-32: a cell whose key is an Error Value writes nothing: no lookup finds it
    public async Task A_cell_whose_key_is_an_error_writes_nothing()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");
        // A grid registered afterwards is pointed at at once, and reads its rows' keys as errors.
        var erring = page.Cut.Instance.Scope.RegisterGrid<Position>("Positions",
            p => [Value.FromError(ErrorValue.NA), Value.FromText(p.Book), Value.FromNumber(p.PV)]);
        Assert.True(erring.IsPointedAt);

        await page.Left.InvokeAsync(() => erring.OnPress(
            new GridPointedPress<Position>(GridPointedPressKind.Cell, ScopedSheets.Positions[0], "PV")));

        Assert.Equal("=", EditorText(page.Left));
        Assert.Equal(PointingRefusalReason.KeyIsAnError, Assert.Single(page.Cut.Instance.LeftRefusals).Reason);
    }

    [Fact] // ADR-0058 / SH-32: a cell whose row has not arrived writes nothing
    public async Task A_cell_whose_row_has_not_arrived_writes_nothing()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");

        await PressCellAsync(page.Positions, ValueX, Row(6));

        Assert.Equal("=", EditorText(page.Left));
        Assert.Equal(PointingRefusalReason.RowNotArrived, Assert.Single(page.Cut.Instance.LeftRefusals).Reason);
    }

    [Fact] // ADR-0058 / SH-32: a table the pointing Sheet does not declare writes nothing
    public async Task A_table_the_sheet_does_not_declare_writes_nothing()
    {
        var page = await RenderAsync(declare: false);
        await StartTypingAsync(page.Left, "D2", "=");

        await PressHeaderAsync(page.Positions, ValueX);

        Assert.Equal("=", EditorText(page.Left));
        Assert.Equal(PointingRefusalReason.TableNotDeclared, Assert.Single(page.Cut.Instance.LeftRefusals).Reason);
    }

    [Fact] // ADR-0058 / SH-32 / ADR-0018: a grid in no Scope is never pointed at: a press on it is its own, and the edit stands
    public async Task A_grid_in_no_scope_is_never_pointed_at()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");

        Assert.False(PointedAt(page.Unregistered));
        await PressCellAsync(page.Unregistered, ValueX, Row(1));

        Assert.Equal("=", EditorText(page.Left));
        Assert.Single(page.Unregistered.FindAll(".ex-focus"));
        Assert.Empty(page.Cut.Instance.LeftRefusals);
    }

    [Fact] // ADR-0058 / SH-32 / ADR-0018: a registered grid with an open edit of its own is not pointed at, and a press goes to its edit
    public async Task A_registered_grid_with_its_own_open_edit_is_not_pointed_at()
    {
        var page = await RenderAsync();
        await PressCellAsync(page.Positions, NoteX, Row(0));
        await page.Positions.InvokeAsync(() => page.Positions.Instance.OnKeyAsync("x", false, false, false, false, false));
        Assert.Single(page.Positions.FindAll(".ex-viewport .ex-editor"));

        await StartTypingAsync(page.Left, "D2", "=");
        Assert.False(PointedAt(page.Positions));
        // A press on it is its own: it commits its edit and moves its Focus, and writes nothing on the left.
        await PressCellAsync(page.Positions, ValueX, Row(1));

        Assert.Equal("=", EditorText(page.Left));
        Assert.Equal("x", Assert.Single(page.Cut.Instance.Edits).Value);
        Assert.Empty(page.Cut.Instance.LeftRefusals);
        // Its edit ended, and the Sheet still points: it is pointed at again.
        Assert.True(PointedAt(page.Positions));
    }

    [Fact] // ADR-0058 / SH-35 / ED-26: when the keyboard leaves the Sheet no grid is pointed at, the edit stands, and pointing goes on once it is back
    public async Task When_the_keyboard_leaves_no_grid_is_pointed_at_and_the_edit_stands()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");
        Assert.True(PointedAt(page.Positions));

        await KeyboardOutAsync(page.Left);
        Assert.False(PointedAt(page.Positions));
        await PressCellAsync(page.Positions, ValueX, Row(1));
        Assert.Equal("=", EditorText(page.Left));
        Assert.Single(page.Positions.FindAll(".ex-focus"));

        await KeyboardInAsync(page.Left);
        Assert.True(PointedAt(page.Positions));
        await PressCellAsync(page.Positions, ValueX, Row(1));
        Assert.Equal("=" + LookupR2, EditorText(page.Left));
    }

    [Fact] // ADR-0058 / SH-35: the keyboard moving inside the Sheet — out of one element and into another at once — is not the keyboard leaving
    public async Task The_keyboard_moving_inside_the_sheet_does_not_stop_pointing()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");
        var changes = 0;
        page.Cut.Instance.PointedAt.Changed += () => changes++;
        var wrapper = page.Left.Find(".ex-sheet");

        // As the browser raises them when focus moves from the root into the Cell Editor: in one task.
        await page.Left.InvokeAsync(async () =>
        {
            var leaving = wrapper.FocusOutAsync(new FocusEventArgs());
            await wrapper.FocusInAsync(new FocusEventArgs());
            await leaving;
        });

        Assert.Equal(0, changes);
        Assert.True(PointedAt(page.Positions));
    }

    [Fact] // ADR-0058 / SH-35: only the Sheet that holds the keyboard points; another Sheet of the Scope points in its turn, and is never pointed at
    public async Task Only_the_sheet_that_holds_the_keyboard_points()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");

        // The keyboard goes to the right Sheet, which has no edit open: nothing points.
        await KeyboardOutAsync(page.Left);
        await KeyboardInAsync(page.Right);
        Assert.False(PointedAt(page.Positions));
        Assert.DoesNotContain("ex-pointed-at", Grid(page.Right).Find(".ex-grid").GetAttribute("class"));

        await GoToAsync(page.Right, "E5");
        await PressAsync(page.Right, "=");
        Assert.True(PointedAt(page.Positions));
        await PressCellAsync(page.Positions, ValueX, Row(1));

        Assert.Equal("=" + LookupR2, EditorText(page.Right));
        Assert.Equal("=", EditorText(page.Left));
        // Neither Sheet is pointed at through the Scope.
        Assert.DoesNotContain("ex-pointed-at", Grid(page.Left).Find(".ex-grid").GetAttribute("class"));
    }

    [Fact] // ADR-0058 / SH-32: where no Reference can go — after ) — no grid is pointed at, and pointing comes back with an operator
    public async Task No_grid_is_pointed_at_where_no_reference_can_go()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=SUM(1)");
        Assert.False(PointedAt(page.Positions));

        await TypeAsync(page.Left, "=SUM(1)+");
        Assert.True(PointedAt(page.Positions));

        await PressAsync(page.Left, "Escape");
        Assert.False(PointedAt(page.Positions));
    }

    [Fact] // ADR-0058 / SH-32: a press handed over after the Sheet stopped pointing writes nothing, and the Sheet that pointed last tells why
    public async Task A_press_after_the_sheet_stopped_pointing_writes_nothing_and_tells_why()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=1");
        Assert.False(PointedAt(page.Positions));
        await StartTypingAsync(page.Left, "D2", "=1+");
        await TypeAsync(page.Left, "=1+2");

        // A grid still painted pointed at on a circuit hands the press over all the same.
        await page.Positions.InvokeAsync(() => page.Cut.Instance.PointedAt.OnPress(
            new GridPointedPress<Position>(GridPointedPressKind.Cell, ScopedSheets.Positions[1], "Value")));

        Assert.Equal("=1+2", EditorText(page.Left));
        Assert.Equal(PointingRefusalReason.NotPointing, Assert.Single(page.Cut.Instance.LeftRefusals).Reason);
    }

    [Fact] // ADR-0058 / SH-32: a Sheet that leaves the Scope stops pointing
    public async Task A_sheet_that_leaves_the_scope_stops_pointing()
    {
        var page = await RenderAsync();
        await StartTypingAsync(page.Left, "D2", "=");
        Assert.True(PointedAt(page.Positions));

        await page.Left.InvokeAsync(() => page.Left.Instance.Dispose());

        Assert.False(PointedAt(page.Positions));
    }

    [Fact] // ADR-0058 / SH-32: registering refuses a correspondence that names no column
    public void Registering_refuses_an_unnamed_column()
    {
        var scope = new PointingScope();

        Assert.Throws<ArgumentException>(() => scope.RegisterGrid<Position>("", ScopedSheets.TableRow));
        Assert.Throws<ArgumentException>(() => scope.RegisterGrid<Position>("Positions", ScopedSheets.TableRow, new Dictionary<string, string> { ["Value"] = "" }));
        Assert.False(scope.RegisterGrid<Position>("Positions", ScopedSheets.TableRow).IsPointedAt);
    }

    /// <summary>A press on a cell of a Sheet's own grid: past the Row Headings and the columns before
    /// it, each at the default width, and down its rows.</summary>
    private static Task PressSheetCellAsync(IRenderedComponent<SheetComponent> sheet, int row, int column)
    {
        var heading = double.Parse(sheet.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(), System.Globalization.CultureInfo.InvariantCulture);
        return sheet.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            Button = 0, Buttons = 1, OffsetX = heading + column * SheetColumns.DefaultWidthPx + 5, OffsetY = SheetComponent.DefaultRowHeightPx * row + 5,
        });
    }
}
