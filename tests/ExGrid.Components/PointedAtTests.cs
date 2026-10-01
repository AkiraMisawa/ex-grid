using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A grid pointed at from outside (ADR-0058; DC-52, DC-53, DC-1): while a Consumer declares it, a
/// primary press on the rows or the column headers is handed over as what it landed on, and moves
/// neither DOM focus nor the Selection and the Focus, and runs no sort, column menu, reorder, resize or
/// Heading drag. The grid draws the dashes and the column outlines the declaration asks for. DOM focus
/// is observed as the press's default being suppressed (<c>blazor:onmousedown:preventdefault</c>) and
/// as no focus asked for. 50 rows of 20px in a 350 × 200 Viewport under a 20px header; Book, Note and
/// Amount are 100px each, and Note edits.
/// </summary>
public class PointedAtTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(150);

    private readonly List<GridPointedPress<TestRow>> _presses = [];
    private readonly List<GridSelection> _selections = [];
    private readonly List<IReadOnlyList<SortSpec>> _sorts = [];

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Note", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary>A declaration that records every press handed over, pointed at unless told
    /// otherwise.</summary>
    private GridPointedAt<TestRow> Declaration(bool pointedAt = true)
        => new(press =>
        {
            _presses.Add(press);
            return Task.CompletedTask;
        })
        {
            IsPointedAt = pointedAt,
        };

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        GridPointedAt<TestRow>? pointedAt,
        TestRow[]? rows = null,
        int? total = null,
        GridColumn<TestRow>[]? columns = null,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? more = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            rows ??= TestRows.Many(50);
            ps.Add(g => g.Window, rows)
              .Add(g => g.TotalCount, total ?? rows.Length)
              .Add(g => g.Columns, columns ?? Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.SelectionChanged, (GridSelection s) => _selections.Add(s))
              .Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> s) => _sorts.Add(s));
            if (pointedAt is not null)
                ps.Add(g => g.PointedAt, pointedAt);
            more?.Invoke(ps);
        });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false, long button = 0)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            Button = button, Buttons = button == 0 ? 1 : 2, OffsetX = x, OffsetY = y, ClientX = x, ClientY = 20 + y, ShiftKey = shift,
        });

    private static Task ReleaseAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, Buttons = 0, OffsetX = x, OffsetY = y, ClientX = x, ClientY = 20 + y });

    /// <summary>A press on the header and, as a browser follows it, its release and its click: the
    /// release where a gesture listens for one.</summary>
    private static async Task ClickHeaderAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y = 10, bool shift = false)
    {
        var press = new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ClientX = x, ClientY = y, ShiftKey = shift };
        await cut.Find(".ex-header").MouseDownAsync(press);
        var release = new MouseEventArgs { Button = 0, Buttons = 0, OffsetX = x, OffsetY = y, ClientX = x, ClientY = y, ShiftKey = shift };
        if (cut.Find(".ex-header").HasAttribute("blazor:onmouseup"))
            await cut.Find(".ex-header").MouseUpAsync(release);
        await cut.Find(".ex-header").ClickAsync(release);
    }

    private static bool KeepsFocus(IRenderedComponent<ExGrid<TestRow>> cut, string element)
        => cut.Find(element).HasAttribute("blazor:onmousedown:preventdefault");

    private static string? RootClass(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-grid").GetAttribute("class");

    private static List<(string? Class, string? Style)> Dashes(IRenderedComponent<ExGrid<TestRow>> cut, string layer = ".ex-selection")
        => [.. cut.FindAll($"{layer} .ex-point-dashes").Select(e => (e.GetAttribute("class"), e.GetAttribute("style")))];

    private static List<(string? Class, string? Style)> Outlines(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-selection .ex-reference-outline").Select(e => (e.GetAttribute("class"), e.GetAttribute("style")))];

    [Fact] // ADR-0058 / DC-52: a press on a cell is handed over as that cell, by its row's identity and its column's name
    public async Task A_press_on_a_cell_is_handed_over_as_that_cell()
    {
        var rows = TestRows.Many(50);
        var cut = RenderGrid(Declaration(), rows);
        var focusCalls = Js.FocusCalls;

        await PressAsync(cut, 250, 70);

        Assert.Equal([new GridPointedPress<TestRow>(GridPointedPressKind.Cell, rows[3], "Amount")], _presses);
        Assert.Same(rows[3], _presses[0].Row);
        // DOM focus stays where it was: the press's default is suppressed, and no focus is asked for.
        Assert.True(KeepsFocus(cut, ".ex-viewport"));
        Assert.Equal(focusCalls, Js.FocusCalls);
        Assert.Empty(_selections);
        Assert.Empty(cut.FindAll(".ex-range, .ex-focus"));
    }

    [Fact] // ADR-0058 / DC-52: the Selection and the Focus a grid had stay where they were, and so does DOM focus
    public async Task The_selection_and_the_focus_do_not_move()
    {
        var rows = TestRows.Many(50);
        var declared = Declaration(pointedAt: false);
        var cut = RenderGrid(declared, rows);
        await PressAsync(cut, 50, 30);
        await ReleaseAsync(cut, 50, 30);
        var focus = cut.Find(".ex-focus").GetAttribute("style");
        var selections = _selections.Count;
        var focusCalls = Js.FocusCalls;

        await cut.InvokeAsync(() => declared.IsPointedAt = true);
        await PressAsync(cut, 150, 110);
        await ReleaseAsync(cut, 150, 110);

        Assert.Equal([new GridPointedPress<TestRow>(GridPointedPressKind.Cell, rows[5], "Note")], _presses);
        Assert.Equal(selections, _selections.Count);
        Assert.Equal(focus, cut.Find(".ex-focus").GetAttribute("style"));
        Assert.Equal(focusCalls, Js.FocusCalls);
    }

    [Fact] // ADR-0058 / DC-52: a press on a column header is handed over as the column, and sorts nothing
    public async Task A_press_on_a_column_header_is_handed_over_and_sorts_nothing()
    {
        var cut = RenderGrid(Declaration());
        var focusCalls = Js.FocusCalls;

        await ClickHeaderAsync(cut, 150);

        Assert.Equal([new GridPointedPress<TestRow>(GridPointedPressKind.ColumnHeader, Column: "Note")], _presses);
        Assert.True(KeepsFocus(cut, ".ex-header"));
        Assert.Equal(focusCalls, Js.FocusCalls);
        Assert.Empty(_sorts);
        Assert.Empty(_selections);
    }

    [Fact] // ADR-0058 / DC-52: where the header selects, a press selects no column either
    public async Task A_header_press_selects_no_column_where_the_header_selects()
    {
        var cut = RenderGrid(Declaration(), more: ps => ps.Add(g => g.HeaderClickSelects, true));

        await ClickHeaderAsync(cut, 250);

        Assert.Equal([new GridPointedPress<TestRow>(GridPointedPressKind.ColumnHeader, Column: "Amount")], _presses);
        Assert.Empty(_selections);
        Assert.Empty(cut.FindAll(".ex-range, .ex-focus"));
    }

    [Fact] // ADR-0058 / DC-52: a header press runs no Heading drag; dragged onto another column, it is handed over once as several columns
    public async Task A_header_drag_is_handed_over_once_and_selects_nothing()
    {
        var cut = RenderGrid(Declaration());
        var header = cut.Find(".ex-header");
        await header.MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10, ClientX = 50, ClientY = 10 });

        // Within its own column, still one press; onto another, a drag across columns.
        await cut.Find(".ex-header").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 80, OffsetY = 10, ClientX = 80, ClientY = 10 });
        Assert.Single(_presses);
        await cut.Find(".ex-header").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 250, OffsetY = 10, ClientX = 250, ClientY = 10 });
        // The drag ends there: the grid follows it no further.
        Assert.False(cut.Find(".ex-header").HasAttribute("blazor:onmousemove"));
        await cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = 250, OffsetY = 10 });

        Assert.Equal(
        [
            new GridPointedPress<TestRow>(GridPointedPressKind.ColumnHeader, Column: "Book"),
            new GridPointedPress<TestRow>(GridPointedPressKind.SeveralColumns, Dragged: true),
        ], _presses);
        Assert.Empty(_selections);
        Assert.Empty(_sorts);
    }

    [Fact] // ADR-0058 / DC-52: where a reorder is wired, a header press grabs nothing and drops nothing
    public async Task A_header_press_grabs_nothing_to_reorder()
    {
        var orders = new List<IReadOnlyList<string>>();
        var cut = RenderGrid(Declaration(), more: ps => ps.Add(g => g.OnColumnOrderChanged, (IReadOnlyList<string> o) => orders.Add(o)));
        await cut.Find(".ex-header").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10, ClientX = 50, ClientY = 10 });

        // No gesture is listening on the root, as a reorder candidate would be.
        Assert.False(cut.Find(".ex-grid").HasAttribute("blazor:onmousemove"));
        await cut.Find(".ex-header").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 260, OffsetY = 10, ClientX = 260, ClientY = 10 });
        await cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = 260, OffsetY = 10 });

        Assert.Empty(orders);
        Assert.Empty(cut.FindAll(".ex-drop-indicator"));
        Assert.Equal(GridPointedPressKind.ColumnHeader, _presses[0].Kind);
    }

    [Fact] // ADR-0058 / DC-52: the column menu opens not, and a grip resizes or fits nothing
    public async Task The_column_menu_and_the_grips_do_nothing()
    {
        var widths = new List<ColumnWidthChange>();
        var cut = RenderGrid(Declaration(), more: ps => ps.Add(g => g.OnColumnWidthChanged, (ColumnWidthChange c) => widths.Add(c)));

        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll(".ex-popover"));

        await cut.FindAll(".ex-resize-grip")[0].MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, ClientX = 98 });
        Assert.Empty(cut.FindAll(".ex-resize-guide"));
        await cut.FindAll(".ex-resize-grip")[0].DoubleClickAsync(new MouseEventArgs());

        Assert.Empty(widths);
        Assert.Empty(_sorts);
    }

    [Fact] // ADR-0058 / DC-52 / ADR-0032: a press on a Header Group's rectangle is handed over as the group; one on a leaf beneath, as its column
    public async Task A_press_on_a_header_group_is_handed_over_as_the_group()
    {
        // One tier above the leaves: the band is 40px, the group's tier its top 20px.
        var cut = RenderGrid(Declaration(), more: ps => ps.Add(g => g.HeaderGroups, [new HeaderGroup("Values", ["Note", "Amount"])]));

        await ClickHeaderAsync(cut, 150, y: 5);
        await ClickHeaderAsync(cut, 250, y: 30);
        // Book is in no group: its leaf stands the band's full height.
        await ClickHeaderAsync(cut, 50, y: 5);

        Assert.Equal(
        [
            new GridPointedPress<TestRow>(GridPointedPressKind.HeaderGroup),
            new GridPointedPress<TestRow>(GridPointedPressKind.ColumnHeader, Column: "Amount"),
            new GridPointedPress<TestRow>(GridPointedPressKind.ColumnHeader, Column: "Book"),
        ], _presses);
        Assert.Empty(_sorts);
    }

    [Fact] // ADR-0058 / DC-52: Shift makes a press more than one cell, or more than one column, and moves nothing
    public async Task Shift_makes_a_press_several_cells_or_several_columns()
    {
        var cut = RenderGrid(Declaration());

        await PressAsync(cut, 150, 50, shift: true);
        await ReleaseAsync(cut, 150, 50);
        await ClickHeaderAsync(cut, 250, shift: true);

        Assert.Equal(
        [
            new GridPointedPress<TestRow>(GridPointedPressKind.SeveralCells),
            new GridPointedPress<TestRow>(GridPointedPressKind.SeveralColumns),
        ], _presses);
        Assert.Empty(_selections);
        Assert.Empty(_sorts);
    }

    [Fact] // ADR-0058 / DC-52: a press dragged onto another cell is handed over once more, as several cells; nothing is selected and nothing scrolls
    public async Task A_drag_across_cells_is_handed_over_once()
    {
        var rows = TestRows.Many(50);
        var cut = RenderGrid(Declaration(), rows);
        var scrolls = Js.ScrolledTo.Count;

        await PressAsync(cut, 50, 50);
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 70, OffsetY = 55 });
        Assert.Single(_presses);
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 150, OffsetY = 90 });
        Assert.False(cut.Find(".ex-viewport").HasAttribute("blazor:onmousemove"));
        await ReleaseAsync(cut, 250, 190);

        Assert.Equal(
        [
            new GridPointedPress<TestRow>(GridPointedPressKind.Cell, rows[2], "Book"),
            new GridPointedPress<TestRow>(GridPointedPressKind.SeveralCells, Dragged: true),
        ], _presses);
        Assert.Empty(_selections);
        Assert.Equal(scrolls, Js.ScrolledTo.Count);
    }

    [Fact] // ADR-0058 / DC-52: one hand-over per press; a double click opens no edit
    public async Task One_press_is_one_hand_over_and_a_double_click_opens_no_edit()
    {
        var cut = RenderGrid(Declaration());

        await PressAsync(cut, 150, 30);
        await ReleaseAsync(cut, 150, 30);
        Assert.Single(_presses);

        // The second press of a double click is a press of its own; the double click is not.
        await PressAsync(cut, 150, 30);
        await ReleaseAsync(cut, 150, 30);
        await cut.Find(".ex-viewport").DoubleClickAsync(new MouseEventArgs { Button = 0, OffsetX = 150, OffsetY = 30 });

        Assert.Equal(2, _presses.Count);
        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.DoesNotContain("ex-editing", RootClass(cut));
    }

    [Fact] // ADR-0058 / DC-52 with ADR-0063 / DC-59: a double click handed over is not heard as the grid's
    public async Task A_double_click_handed_over_raises_no_cell_double_click()
    {
        var heard = new List<CellPosition>();
        var declared = Declaration();
        var cut = RenderGrid(declared, more: ps => ps.Add(g => g.OnCellDoubleClick, (CellPosition cell) => heard.Add(cell)));

        // Book, where no edit opens: a grid that is not pointed at would raise OnCellDoubleClick here.
        await PressAsync(cut, 50, 30);
        await ReleaseAsync(cut, 50, 30);
        await PressAsync(cut, 50, 30);
        await ReleaseAsync(cut, 50, 30);
        await cut.Find(".ex-viewport").DoubleClickAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 30 });

        Assert.Equal(2, _presses.Count);
        Assert.Empty(heard);
        Assert.Empty(_selections);

        // The same double click on the same grid, no longer pointed at, is heard: the cell is one the
        // listener hears, and only the hand-over kept it from being.
        await cut.InvokeAsync(() => declared.IsPointedAt = false);
        await PressAsync(cut, 50, 30);
        await ReleaseAsync(cut, 50, 30);
        await cut.Find(".ex-viewport").DoubleClickAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 30 });

        Assert.Equal(2, _presses.Count);
        Assert.Equal([new CellPosition(1, 0)], heard);
    }

    [Fact] // ADR-0058 / DC-52: a secondary press is not handed over; it opens no menu and moves nothing
    public async Task A_secondary_press_is_not_handed_over_and_opens_no_menu()
    {
        var cut = RenderGrid(Declaration());

        await PressAsync(cut, 150, 30, button: 2);
        await cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs { Button = 2, OffsetX = 150, OffsetY = 30 });

        Assert.Empty(_presses);
        Assert.Empty(_selections);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0058 / DC-52: a press past the last column landed on no cell and no header, and nothing is handed over
    public async Task A_press_past_the_last_column_hands_nothing_over()
    {
        var cut = RenderGrid(Declaration());

        await PressAsync(cut, 320, 30);
        await ReleaseAsync(cut, 320, 30);
        await ClickHeaderAsync(cut, 320);

        Assert.Empty(_presses);
        Assert.Empty(_selections);
        Assert.Empty(_sorts);
    }

    [Fact] // ADR-0058 / DC-52 / ADR-0050: a press on a Row Heading, or on the corner, is more than one cell
    public async Task A_press_on_a_row_heading_or_the_corner_is_several_cells()
    {
        var cut = RenderGrid(Declaration(), more: ps => ps
            .Add(g => g.RowHeadings, (int row) => (row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Add(g => g.RowHeadingWidth, 40d));

        await PressAsync(cut, 10, 30);
        await ReleaseAsync(cut, 10, 30);
        await ClickHeaderAsync(cut, 10);

        Assert.Equal(
        [
            new GridPointedPress<TestRow>(GridPointedPressKind.SeveralCells),
            new GridPointedPress<TestRow>(GridPointedPressKind.SeveralCells),
        ], _presses);
        Assert.Empty(_selections);
    }

    [Fact] // ADR-0058 / DC-52 / ADR-0004: a cell whose row has not arrived is handed over without a row
    public async Task A_press_on_a_row_that_has_not_arrived_is_handed_over_without_a_row()
    {
        var cut = RenderGrid(Declaration(), rows: TestRows.Many(50), total: 1000);
        await ScrollToAsync(cut.Find(".ex-scroller"), 500 * 20);
        Clock.Advance(SettleDelay);

        await PressAsync(cut, 150, 10);

        var press = Assert.Single(_presses);
        Assert.Equal(new GridPointedPress<TestRow>(GridPointedPressKind.Cell, null, "Note"), press);
    }

    [Fact] // ADR-0058 / DC-52: an action and a checkbox act not while the grid is pointed at
    public async Task An_action_does_nothing_while_pointed_at()
    {
        var raised = 0;
        GridColumn<TestRow>[] columns =
        [
            new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
            GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")], width: Fixed100),
        ];
        var cut = RenderGrid(Declaration(), columns: columns, more: ps => ps
            .Add(g => g.OnAction, (GridActionEventArgs<TestRow> _) => raised++));

        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Equal(0, raised);
    }

    [Fact] // ADR-0058 / DC-52: the press is answered once the Consumer has heard it
    public async Task A_press_is_answered_once_the_consumer_has_heard_it()
    {
        var heard = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var declared = new GridPointedAt<TestRow>(_ => heard.Task) { IsPointedAt = true };
        var cut = RenderGrid(declared);

        // Not awaited: the press is still being answered while the Consumer has not heard it.
        var pressed = cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });
        var answered = Task.CompletedTask;
        await cut.InvokeAsync(() => { answered = cut.Instance.PressAnsweredAsync(); });
        Assert.False(answered.IsCompleted);

        heard.SetResult();
        await answered;
        await pressed;
    }

    [Fact] // ADR-0058 / DC-52 / ADR-0029: ex-pointed-at joins the root while declared, with the press defaults suppressed, and goes with them
    public async Task The_root_wears_ex_pointed_at_while_declared()
    {
        var declared = Declaration(pointedAt: false);
        var cut = RenderGrid(declared);
        Assert.Equal("ex-grid", RootClass(cut));
        Assert.False(KeepsFocus(cut, ".ex-viewport"));
        Assert.False(KeepsFocus(cut, ".ex-header"));

        await cut.InvokeAsync(() => declared.IsPointedAt = true);
        Assert.Equal("ex-grid ex-pointed-at", RootClass(cut));
        Assert.True(KeepsFocus(cut, ".ex-viewport"));
        Assert.True(KeepsFocus(cut, ".ex-header"));

        await cut.InvokeAsync(() => declared.IsPointedAt = false);
        Assert.Equal("ex-grid", RootClass(cut));
        Assert.False(KeepsFocus(cut, ".ex-viewport"));
        Assert.False(KeepsFocus(cut, ".ex-header"));
    }

    [Fact] // ADR-0058 / DC-52 / PF-3: what a press reaches is one handler for as long as the grid stays pointed at, and another once it stops
    public async Task Press_handlers_change_only_with_the_declaration()
    {
        var declared = Declaration();
        var cut = RenderGrid(declared);
        string?[] Handlers() => [cut.Find(".ex-viewport").GetAttribute("blazor:onmousedown"), cut.Find(".ex-header").GetAttribute("blazor:onmousedown")];
        var pointed = Handlers();

        cut.Render(ps => ps.Add(g => g.CellState, (_, _) => CellState.Normal));
        Assert.Equal(pointed, Handlers());

        await cut.InvokeAsync(() => declared.IsPointedAt = false);
        Assert.All(Handlers().Zip(pointed), pair => Assert.NotEqual(pair.Second, pair.First));
    }

    [Fact] // ADR-0058 / SH-32 / ADR-0018: a grid with an open edit of its own is not pointed at, whatever it is told; its presses go to its edit, and it is pointed at again once the edit ends
    public async Task A_grid_with_an_open_edit_of_its_own_is_not_pointed_at()
    {
        var rows = TestRows.Many(50);
        var declared = Declaration(pointedAt: false);
        var cut = RenderGrid(declared, rows);
        await PressAsync(cut, 150, 30);
        await ReleaseAsync(cut, 150, 30);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("x", false, false, false, false, false));
        Assert.Single(cut.FindAll(".ex-viewport .ex-editor"));
        var selections = _selections.Count;

        await cut.InvokeAsync(() => declared.IsPointedAt = true);
        Assert.DoesNotContain("ex-pointed-at", RootClass(cut));
        Assert.False(cut.Find(".ex-header").HasAttribute("blazor:onmousedown:preventdefault"));
        // A press on another cell is the grid's own: it commits the edit and moves the Focus there.
        await PressAsync(cut, 250, 70);
        await ReleaseAsync(cut, 250, 70);

        Assert.Empty(_presses);
        Assert.Equal(new CellPosition(3, 2), _selections[^1].Focus);
        Assert.True(_selections.Count > selections);
        // The edit is gone, and the declaration is in force again.
        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.Contains("ex-pointed-at", RootClass(cut));
        await PressAsync(cut, 50, 110);
        Assert.Equal([new GridPointedPress<TestRow>(GridPointedPressKind.Cell, rows[5], "Book")], _presses);
    }

    [Fact] // ADR-0058 / DC-1 / DC-52: without the declaration, or with one not pointed at, nothing changes, and a press is the grid's own again
    public async Task Without_the_declaration_nothing_changes()
    {
        var cut = RenderGrid(null);
        var without = cut.Markup;
        static string Unnumbered(string markup) => Regex.Replace(markup, "blazor:on([a-z]+)=\"\\d+\"", "blazor:on$1");

        var declared = Declaration(pointedAt: false);
        cut.Render(ps => ps.Add(g => g.PointedAt, declared));
        Assert.Equal(without, cut.Markup);

        await cut.InvokeAsync(() => declared.IsPointedAt = true);
        Assert.NotEqual(Unnumbered(without), Unnumbered(cut.Markup));
        cut.Render(ps => ps.Add(g => g.PointedAt, null));
        Assert.Equal(Unnumbered(without), Unnumbered(cut.Markup));
        Assert.DoesNotContain("ex-point-dashes", cut.Markup);

        // A declaration no longer passed is no longer heard.
        await cut.InvokeAsync(() => declared.Dashes = GridPointDashes<TestRow>.OverColumn("Note"));
        Assert.Empty(cut.FindAll(".ex-point-dashes"));

        await PressAsync(cut, 150, 30);
        await ReleaseAsync(cut, 150, 30);
        await ClickHeaderAsync(cut, 250);
        Assert.Empty(_presses);
        Assert.Equal(new CellPosition(1, 1), Assert.Single(_selections).Focus);
        Assert.Equal([new SortSpec("Amount", SortDirection.Ascending)], Assert.Single(_sorts));
    }

    [Fact] // ADR-0058 / DC-53: dashes over a cell named by its row's identity: one element, in the overlay, over that cell
    public async Task Dashes_are_drawn_over_the_cell_of_the_row_named()
    {
        var rows = TestRows.Many(50);
        var declared = Declaration();
        var cut = RenderGrid(declared, rows);

        await cut.InvokeAsync(() => declared.Dashes = GridPointDashes<TestRow>.OverCell(rows[4], "Note"));

        Assert.Equal([("ex-point-dashes", "left: 100px; top: 80px; width: 100px; height: 20px")], Dashes(cut));
        Assert.Empty(Dashes(cut, ".ex-selection-pinned"));
        Assert.Single(cut.FindAll(".ex-point-dashes"));
        Assert.Empty(cut.FindAll(".ex-row .ex-point-dashes"));
        // Nothing was selected for them: they are the Consumer's, not the Selection's.
        Assert.Empty(cut.FindAll(".ex-range, .ex-focus"));
    }

    [Fact] // ADR-0058 / DC-53: after a reorder the dashes are over the same row, wherever it went
    public async Task The_dashes_follow_their_row_through_a_reorder()
    {
        var rows = TestRows.Many(50);
        var declared = Declaration();
        var cut = RenderGrid(declared, rows);
        await cut.InvokeAsync(() => declared.Dashes = GridPointDashes<TestRow>.OverCell(rows[4], "Amount"));
        Assert.Equal([("ex-point-dashes", "left: 200px; top: 80px; width: 100px; height: 20px")], Dashes(cut));

        // The Consumer's sort: the same instances in another order.
        TestRow[] sorted = [rows[7], rows[3], rows[4], .. rows.Where((_, i) => i is not (7 or 3 or 4))];
        cut.Render(ps => ps.Add(g => g.Window, sorted).Add(g => g.RowSequenceVersion, 1));

        Assert.Equal([("ex-point-dashes", "left: 200px; top: 40px; width: 100px; height: 20px")], Dashes(cut));
    }

    [Fact] // ADR-0058 / DC-53: a row named by what the Consumer reads from it — its key — is dashed through a Window of new instances
    public async Task Dashes_by_what_the_consumer_reads_follow_new_instances()
    {
        var declared = Declaration();
        var cut = RenderGrid(declared, TestRows.Many(50));
        await cut.InvokeAsync(() => declared.Dashes = GridPointDashes<TestRow>.OverCell(r => r.Book == "Row 000006", "Book"));
        Assert.Equal([("ex-point-dashes", "left: 0px; top: 120px; width: 100px; height: 20px")], Dashes(cut));

        var fresh = TestRows.Many(50).Reverse().ToArray();
        cut.Render(ps => ps.Add(g => g.Window, fresh).Add(g => g.RowSequenceVersion, 1));

        // Row 000006 now stands 44th, and is not painted: nothing is drawn, and nothing scrolls.
        Assert.Empty(cut.FindAll(".ex-point-dashes"));
        fresh = [.. fresh.Skip(40), .. fresh.Take(40)];
        cut.Render(ps => ps.Add(g => g.Window, fresh).Add(g => g.RowSequenceVersion, 2));
        Assert.Equal([("ex-point-dashes", "left: 0px; top: 60px; width: 100px; height: 20px")], Dashes(cut));
    }

    [Fact] // ADR-0058 / DC-53 / ADR-0057: a row that is not painted is not dashed, and the grid does not scroll to it
    public async Task A_row_that_is_not_painted_is_not_dashed_or_scrolled_to()
    {
        var rows = TestRows.Many(50);
        var declared = Declaration();
        var cut = RenderGrid(declared, rows);
        var scrolls = Js.ScrolledTo.Count;

        await cut.InvokeAsync(() => declared.Dashes = GridPointDashes<TestRow>.OverCell(rows[40], "Note"));

        Assert.Empty(cut.FindAll(".ex-point-dashes"));
        // No overlay at all for it: nothing else is drawn here.
        Assert.Empty(cut.FindAll(".ex-selection, .ex-selection-pinned"));
        Assert.Equal(scrolls, Js.ScrolledTo.Count);

        // Scrolled to by the user, it is dashed.
        await ScrollToAsync(cut.Find(".ex-scroller"), 35 * 20);
        Clock.Advance(SettleDelay);
        cut.WaitForAssertion(() => Assert.StartsWith("left: 100px;", Assert.Single(Dashes(cut)).Style));
    }

    [Fact] // ADR-0058 / DC-53: dashes down a column are its body across all its rows, cut to the painted rows as a column outline is
    public async Task Dashes_down_a_column_are_its_body_cut_to_the_painted_rows()
    {
        var declared = Declaration();
        declared.Dashes = GridPointDashes<TestRow>.OverColumn("Amount");
        declared.OutlinedColumns = [new OutlinedColumn("Amount", new ReferenceColour(2))];
        var cut = RenderGrid(declared, total: 1000);

        var outline = Assert.Single(Outlines(cut));
        Assert.Equal([("ex-point-dashes", outline.Style)], Dashes(cut));
        Assert.StartsWith("left: 200px; top: 0px; width: 100px; height: ", outline.Style);

        await ScrollToAsync(cut.Find(".ex-scroller"), 500 * 20);
        Clock.Advance(SettleDelay);
        cut.WaitForAssertion(() => Assert.Equal([("ex-point-dashes", Assert.Single(Outlines(cut)).Style)], Dashes(cut)));
        Assert.NotEqual(outline.Style, Assert.Single(Dashes(cut)).Style);
    }

    [Fact] // ADR-0058 / DC-53 / ADR-0004: dashes over a pinned column are drawn in the pinned layer
    public void Dashes_over_a_pinned_column_are_in_the_pinned_layer()
    {
        var declared = Declaration();
        declared.Dashes = GridPointDashes<TestRow>.OverColumn("Book");
        var cut = RenderGrid(declared, TestRows.Many(5), more: ps => ps.Add(g => g.PinnedColumnCount, 1));

        Assert.Equal([("ex-point-dashes", "left: 0px; top: 0px; width: 100px; height: 100px")], Dashes(cut, ".ex-selection-pinned"));
        Assert.Empty(Dashes(cut));
    }

    [Fact] // ADR-0058 / DC-53: an empty request draws nothing, nor does a column the grid does not show
    public async Task No_dashes_for_an_empty_request_or_a_column_not_shown()
    {
        var declared = Declaration();
        var cut = RenderGrid(declared);
        Assert.Empty(cut.FindAll(".ex-point-dashes, .ex-selection, .ex-selection-pinned"));

        await cut.InvokeAsync(() => declared.Dashes = GridPointDashes<TestRow>.OverColumn("Hidden"));
        Assert.Empty(cut.FindAll(".ex-point-dashes"));

        await cut.InvokeAsync(() => declared.Dashes = GridPointDashes<TestRow>.OverColumn("Note"));
        Assert.Single(cut.FindAll(".ex-point-dashes"));

        await cut.InvokeAsync(() => declared.Dashes = null);
        Assert.Empty(cut.FindAll(".ex-point-dashes"));
    }

    [Fact] // ADR-0058 / ADR-0003: the declaration's changes are overlay and root paint, and no row renders for them
    public async Task The_declaration_renders_no_row()
    {
        var rows = TestRows.Many(50);
        var declared = Declaration(pointedAt: false);
        var cut = RenderGrid(declared, rows);
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await cut.InvokeAsync(() => declared.IsPointedAt = true);
        await cut.InvokeAsync(() => declared.Dashes = GridPointDashes<TestRow>.OverCell(rows[2], "Note"));
        await cut.InvokeAsync(() => declared.OutlinedColumns = [new OutlinedColumn("Note", new ReferenceColour(1))]);
        await cut.InvokeAsync(() => declared.Dashes = GridPointDashes<TestRow>.OverColumn("Note"));
        await cut.InvokeAsync(() => declared.IsPointedAt = false);

        Assert.Equal(before, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }

    [Fact] // ADR-0058 / ADR-0057: columns asked through the declaration are outlined as OutlinedColumns outlines, after the page's own; one asked twice is outlined twice
    public async Task Columns_asked_through_the_declaration_are_outlined_after_the_pages_own()
    {
        var declared = Declaration(pointedAt: false);
        var cut = RenderGrid(declared, TestRows.Many(5), more: ps => ps
            .Add(g => g.OutlinedColumns, [new OutlinedColumn("Note", new ReferenceColour(1))]));

        await cut.InvokeAsync(() => declared.OutlinedColumns =
            [new OutlinedColumn("Amount", new ReferenceColour(3)), new OutlinedColumn("Note", new ReferenceColour(2))]);

        Assert.Equal(
        [
            ("ex-reference-outline ex-reference-1", "left: 100px; top: 0px; width: 100px; height: 100px"),
            ("ex-reference-outline ex-reference-3", "left: 200px; top: 0px; width: 100px; height: 100px"),
            ("ex-reference-outline ex-reference-2", "left: 100px; top: 0px; width: 100px; height: 100px"),
        ], Outlines(cut));

        // The columns are named, and outlined wherever the order puts them.
        var columns = Columns();
        cut.Render(ps => ps.Add(g => g.Columns, [columns[2], columns[0], columns[1]]));
        Assert.Equal("left: 0px; top: 0px; width: 100px; height: 100px", Outlines(cut)[1].Style);

        await cut.InvokeAsync(() => declared.OutlinedColumns = null);
        Assert.Single(Outlines(cut));
    }

    [Fact] // ADR-0058 / ADR-0057: a null column is refused by name, where it is set
    public void A_null_outlined_column_is_refused()
    {
        var declared = Declaration();

        Assert.Throws<ArgumentNullException>(() => declared.OutlinedColumns = [null!]);
        Assert.Throws<ArgumentNullException>(() => new GridPointedAt<TestRow>(null!));
        Assert.Throws<ArgumentNullException>(() => GridPointDashes<TestRow>.OverCell((TestRow)null!, "Note"));
        Assert.Throws<ArgumentException>(() => GridPointDashes<TestRow>.OverColumn(""));
    }

    [Fact] // ADR-0058: the declaration tells of a change, and only of a change
    public void The_declaration_tells_of_each_change_once()
    {
        var declared = Declaration(pointedAt: false);
        var told = 0;
        declared.Changed += () => told++;
        IReadOnlyList<OutlinedColumn> outlined = [new OutlinedColumn("Note", new ReferenceColour(1))];
        var dashes = GridPointDashes<TestRow>.OverColumn("Note");

        declared.IsPointedAt = false;
        declared.Dashes = null;
        declared.OutlinedColumns = null;
        Assert.Equal(0, told);

        declared.IsPointedAt = true;
        declared.Dashes = dashes;
        declared.OutlinedColumns = outlined;
        Assert.Equal(3, told);

        declared.IsPointedAt = true;
        declared.Dashes = GridPointDashes<TestRow>.OverColumn("Note");
        declared.OutlinedColumns = outlined;
        Assert.Equal(3, told);
    }
}
