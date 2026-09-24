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
/// Entering a cell by key (ADR-0020, completed by ADR-0037): Space on a cell with several
/// actions makes it Interactive with the keyboard left on the root; Space on a Template
/// cell hands its content one focus request; Space on an editable cell opens Overwrite
/// with the space in it. What the browser does with real keys — the held Space, the
/// caret landing in a real field — is layer 3's.
///
/// 20px rows in a 120px Viewport whose header takes the first 20: five rows painted.
/// Columns, all fixed: Book 0–100 (editable), Review 100–300 (three actions),
/// Note 300–400 (a recording template), Open 400–500 (one action).
/// </summary>
public class InteractiveTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const string FocusIdentifier = "Blazor._internal.domWrapper.focus";
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(150);

    private const int Review = 1;
    private const int Note = 2;

    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    // Every render of every Note cell, in order: which row it painted and the request it
    // was handed. Held in a field and built once per test, so every row is handed the
    // same fragment instance and memoisation is not defeated by the test itself (ADR-0003).
    private readonly List<(string Book, int Request)> _noteRenders = [];
    private readonly RenderFragment<TemplateCellContext<TestRow>> _noteTemplate;

    public InteractiveTests()
    {
        _noteTemplate = cell =>
        {
            _noteRenders.Add((cell.Row.Book, cell.FocusRequest));
            return builder => builder.AddMarkupContent(0, "<span class='note'></span>");
        };
    }

    private GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        GridColumn<TestRow>.ActionColumn("Review",
            [new GridAction("approve", "Approve"), new GridAction("query", "Query"), new GridAction("escalate", "Escalate")],
            width: new ColumnWidthSpec(ColumnWidth.Fixed(200))),
        GridColumn<TestRow>.TemplateColumn("Note", ColumnType.Text, r => r.Book, _noteTemplate, width: Fixed100),
        GridColumn<TestRow>.ActionColumn("Open", [new GridAction("open", "Open")], width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        List<GridActionEventArgs<TestRow>>? raised = null,
        Action<GridSelection>? onSelectionChanged = null,
        GridColumn<TestRow>[]? columns = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, columns ?? Columns())
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 600);
            if (raised is not null)
                ps.Add(g => g.OnAction, raised.Add);
            if (onSelectionChanged is not null)
                ps.Add(g => g.SelectionChanged, onSelectionChanged);
        });

    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, alt: false, meta: false, metaIsPrimary: false));

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    /// <summary>The body cell at (row, column) of the rows painted from the top — the
    /// Viewport's own arithmetic puts the click there.</summary>
    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, int row, int column)
        => ClickAsync(cut, column switch { 0 => 50, 1 => 150, 2 => 350, _ => 450 }, (row * RowHeightPx) + 10);

    private static string? ActiveDescendant(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-grid").GetAttribute("aria-activedescendant");

    private static IReadOnlyList<string> Chosen(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll("button.ex-action-chosen").Select(button => button.TextContent)];

    private static AngleSharp.Dom.IElement CellAt(IRenderedComponent<ExGrid<TestRow>> cut, int row, int column)
        => cut.FindAll("[role=gridcell]").Single(cell => cell.Id!.EndsWith($"r{row}c{column}", StringComparison.Ordinal));

    private Dictionary<int, int> RowRenders(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindComponents<ExGridRow<TestRow>>().ToDictionary(row => row.Instance.RowIndex, row => row.RenderCount);

    private int FocusCalls => JSInterop.Invocations.Count(invocation => invocation.Identifier == FocusIdentifier);

    /// <summary>Scrolls, and lets the fling settle (ADR-0004): a jump of more than a
    /// Viewport paints Placeholders until the delay passes, and a Placeholder is not a
    /// painted cell — nothing can be named on it or handed to it.</summary>
    private async Task ScrollAndSettleAsync(IRenderedComponent<ExGrid<TestRow>> cut, double top)
    {
        await ScrollToAsync(cut.Find(".ex-scroller"), top);
        Clock.Advance(SettleDelay);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-placeholder")));
    }

    [Fact] // ADR-0037 / KB-20: Space on several actions chooses the first, and the keyboard stays on the root
    public async Task Space_on_several_actions_chooses_the_first_and_keeps_the_keyboard_on_the_root()
    {
        var raised = new List<GridActionEventArgs<TestRow>>();
        var cut = RenderGrid(raised);
        await ClickCellAsync(cut, 0, Review);
        var focusCalls = FocusCalls;

        await PressAsync(cut, " ");

        Assert.Equal(["Approve"], Chosen(cut));
        Assert.Empty(raised);
        // No DOM focus moved: the root keeps the keyboard and names the chosen button.
        Assert.Equal(focusCalls, FocusCalls);
        var chosen = cut.Find("button.ex-action-chosen");
        Assert.False(string.IsNullOrEmpty(chosen.Id));
        Assert.Equal(chosen.Id, ActiveDescendant(cut));
    }

    [Fact] // ADR-0037 / KB-21: the arrows, Home and End choose — clamped, never leaving the cell
    public async Task Arrows_home_and_end_choose_within_the_cell_and_clamp()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(onSelectionChanged: s => selection = s);
        await ClickCellAsync(cut, 0, Review);
        await PressAsync(cut, " ");

        await PressAsync(cut, "ArrowRight");
        Assert.Equal(["Query"], Chosen(cut));
        await PressAsync(cut, "ArrowRight");
        await PressAsync(cut, "ArrowRight"); // past the end: clamped, still inside
        Assert.Equal(["Escalate"], Chosen(cut));

        await PressAsync(cut, "Home");
        Assert.Equal(["Approve"], Chosen(cut));
        await PressAsync(cut, "ArrowLeft"); // past the start: clamped too
        Assert.Equal(["Approve"], Chosen(cut));
        await PressAsync(cut, "End");
        Assert.Equal(["Escalate"], Chosen(cut));

        // None of it moved the Focus: choosing is inside the cell.
        Assert.Equal(new CellPosition(0, Review), selection!.Focus);
    }

    [Fact] // ADR-0037 / KB-21: Space fires the chosen action once, and firing leaves
    public async Task Space_fires_the_chosen_action_once_and_leaves()
    {
        var raised = new List<GridActionEventArgs<TestRow>>();
        var cut = RenderGrid(raised);
        await ClickCellAsync(cut, 0, Review);
        await PressAsync(cut, " ");
        await PressAsync(cut, "ArrowRight");

        await PressAsync(cut, " ");

        var args = Assert.Single(raised);
        Assert.Equal("Review", args.ColumnName);
        Assert.Equal("query", args.ActionName);
        Assert.Equal("Row 000000", args.Row.Book);
        Assert.Empty(Chosen(cut));
        Assert.Equal(CellAt(cut, 0, Review).Id, ActiveDescendant(cut));
    }

    [Fact] // ADR-0020/0037 / KB-22: Enter never fires — it leaves and cycles the Focus as it always does
    public async Task Enter_leaves_without_firing_and_keeps_its_meaning()
    {
        var raised = new List<GridActionEventArgs<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(raised, s => selection = s);
        await ClickCellAsync(cut, 0, Review);
        await PressAsync(cut, " ");
        await PressAsync(cut, "ArrowRight");

        await PressAsync(cut, "Enter");

        Assert.Empty(raised);
        Assert.Empty(Chosen(cut));
        Assert.Equal(new CellPosition(1, Review), selection!.Focus);
        Assert.Equal(CellAt(cut, 1, Review).Id, ActiveDescendant(cut));
    }

    [Fact] // ADR-0020/0037 / KB-22: Tab never fires either — it leaves and cycles across
    public async Task Tab_leaves_without_firing_and_keeps_its_meaning()
    {
        var raised = new List<GridActionEventArgs<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(raised, s => selection = s);
        await ClickCellAsync(cut, 0, Review);
        await PressAsync(cut, " ");

        await PressAsync(cut, "Tab");

        Assert.Empty(raised);
        Assert.Empty(Chosen(cut));
        Assert.Equal(new CellPosition(0, Note), selection!.Focus);
    }

    [Fact] // ADR-0037 / KB-22: every other key the grid claims leaves first, then keeps its meaning
    public async Task A_key_that_moves_nothing_still_leaves()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(onSelectionChanged: s => selection = s);
        await ClickCellAsync(cut, 0, Review);
        await PressAsync(cut, " ");

        // ArrowUp at the first row moves nothing and would render nothing — the chosen
        // button must not stay painted for a mode that has ended.
        await PressAsync(cut, "ArrowUp");

        Assert.Empty(Chosen(cut));
        Assert.Equal(new CellPosition(0, Review), selection!.Focus);
        Assert.Equal(CellAt(cut, 0, Review).Id, ActiveDescendant(cut));
    }

    [Fact] // ADR-0037/0012 / KB-22: Escape leaves the cell, and only the next one leaves the grid
    public async Task Escape_leaves_the_cell_without_releasing_the_grid()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, Review);
        await PressAsync(cut, " ");

        await PressAsync(cut, "Escape");

        Assert.Empty(Chosen(cut));
        Assert.Equal(0, Js.BlurCount);
        Assert.Equal(CellAt(cut, 0, Review).Id, ActiveDescendant(cut));

        await PressAsync(cut, "Escape");
        Assert.Equal(1, Js.BlurCount);
    }

    [Fact] // ADR-0037 / KB-22: a pointer press leaves — even one on the Interactive cell itself
    public async Task A_pointer_press_leaves()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, Review);
        await PressAsync(cut, " ");

        // The same cell: no selection change, so the press alone must carry the repaint.
        await ClickCellAsync(cut, 0, Review);
        Assert.Empty(Chosen(cut));

        await PressAsync(cut, " ");
        await ClickCellAsync(cut, 2, 0);
        Assert.Empty(Chosen(cut));
    }

    [Fact] // ADR-0037: pressing an action with the pointer ends Interactive too
    public async Task Pressing_an_action_with_the_pointer_leaves()
    {
        var raised = new List<GridActionEventArgs<TestRow>>();
        var cut = RenderGrid(raised);
        await ClickCellAsync(cut, 0, Review);
        await PressAsync(cut, " ");

        await CellAt(cut, 0, Review).QuerySelectorAll("button")[2].ClickAsync(new MouseEventArgs());

        Assert.Equal("escalate", Assert.Single(raised).ActionName);
        Assert.Empty(Chosen(cut));
    }

    [Fact] // ADR-0037 / RR-12: entering, choosing and leaving re-render the engaged row and no other
    public async Task Only_the_engaged_row_rerenders()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, Review);
        var before = RowRenders(cut);

        await PressAsync(cut, " ");
        await PressAsync(cut, "ArrowRight");
        await PressAsync(cut, "Escape");

        var after = RowRenders(cut);
        Assert.Equal(before[0] + 3, after[0]);
        Assert.All(after.Where(row => row.Key != 0), row => Assert.Equal(before[row.Key], row.Value));
    }

    [Fact] // ADR-0033/0037 / A11Y-18: the chosen button is named while painted, and Interactive outlives a scroll
    public async Task The_chosen_button_is_named_only_while_it_is_painted()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, Review);
        await PressAsync(cut, " ");
        var chosenId = cut.Find("button.ex-action-chosen").Id;

        // A wheel scroll takes the row out of the Window's painted slice: the attribute
        // must not name an id that no longer exists (ADR-0033).
        await ScrollAndSettleAsync(cut, 20 * RowHeightPx);
        Assert.Null(ActiveDescendant(cut));

        // Back again: the mode is the core's state, not the element's, so it survives the
        // row being recycled (P7), and the same button is chosen and named.
        await ScrollAndSettleAsync(cut, 0);
        Assert.Equal(["Approve"], Chosen(cut));
        Assert.Equal(chosenId, ActiveDescendant(cut));
    }

    [Fact] // ADR-0037 / KB-23: a Template cell is handed exactly one focus request, and no other cell any
    public async Task Space_on_a_template_hands_its_content_one_request()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, Note);
        _noteRenders.Clear();

        await PressAsync(cut, " ");
        // Anything that renders again — here a move away — must hand over nothing more.
        await PressAsync(cut, "ArrowDown");

        var requests = _noteRenders.Where(render => render.Request != 0).ToList();
        var request = Assert.Single(requests);
        Assert.Equal("Row 000000", request.Book);
        // The keyboard was not taken by the core: a template's control takes it itself.
        Assert.Empty(Chosen(cut));
    }

    [Fact] // ADR-0037 / KB-23: a row re-created by virtualisation never sees a stale request
    public async Task A_recycled_row_never_sees_the_request_again()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, Note);
        await PressAsync(cut, " ");
        _noteRenders.Clear();

        await ScrollAndSettleAsync(cut, 20 * RowHeightPx);
        await ScrollAndSettleAsync(cut, 0);

        Assert.Contains(_noteRenders, render => render.Book == "Row 000000");
        Assert.All(_noteRenders, render => Assert.Equal(0, render.Request));
    }

    [Fact] // ADR-0037 / KB-23: the request waits for the cell to be painted
    public async Task The_request_waits_until_the_cell_is_painted()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, Note);
        await ScrollAndSettleAsync(cut, 20 * RowHeightPx);
        _noteRenders.Clear();

        await PressAsync(cut, " ");

        // Nothing painted the Focus row, so nothing was handed over — but Space asked for
        // the reveal that will paint it.
        Assert.DoesNotContain(_noteRenders, render => render.Request != 0);
        Assert.Contains(Js.ScrolledTo, offset => offset.Top == 0);

        // The browser answers the reveal with a scroll event. That jump is a fling, which
        // paints Placeholders first — still nothing to hand it to…
        await ScrollToAsync(cut.Find(".ex-scroller"), 0);
        Assert.DoesNotContain(_noteRenders, render => render.Request != 0);

        // …until the fling settles and the real cell is painted: that paint delivers it.
        Clock.Advance(SettleDelay);
        cut.WaitForAssertion(() => Assert.Contains(_noteRenders, render => render.Request != 0));
        var request = Assert.Single(_noteRenders, render => render.Request != 0);
        Assert.Equal("Row 000000", request.Book);
    }

    [Fact] // ADR-0037 / KB-23: a request the Focus walked away from is dropped undelivered
    public async Task A_request_is_dropped_when_the_focus_moves_first()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, Note);
        await ScrollAndSettleAsync(cut, 20 * RowHeightPx);
        await PressAsync(cut, " ");

        await PressAsync(cut, "ArrowDown");
        // Back onto the very cell that was asked for: a request kept alive would now be
        // delivered long after the Space that made it — the stolen focus this rule exists
        // to prevent.
        await PressAsync(cut, "ArrowUp");
        _noteRenders.Clear();
        await ScrollAndSettleAsync(cut, 0);

        Assert.Contains(_noteRenders, render => render.Book == "Row 000000");
        Assert.All(_noteRenders, render => Assert.Equal(0, render.Request));
    }

    [Fact] // ADR-0037 / RR-12: a template's request renders the Focus row at most twice — the request and its clearing
    public async Task A_template_request_rerenders_only_the_focus_row()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, Note);
        var before = RowRenders(cut);

        await PressAsync(cut, " ");
        await PressAsync(cut, "ArrowRight");

        var after = RowRenders(cut);
        Assert.Equal(before[0] + 2, after[0]);
        Assert.All(after.Where(row => row.Key != 0), row => Assert.Equal(before[row.Key], row.Value));
    }

    [Fact] // ADR-0020/0010 / KB-25: Space on an editable cell opens Overwrite containing the space
    public async Task Space_on_an_editable_cell_opens_overwrite_with_a_space()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, 0);

        await PressAsync(cut, " ");

        Assert.Equal(" ", cut.Find("input.ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0012/0037 / KB-25: with no Focus, Space only places it — it never fires what nobody saw focused
    public async Task Space_with_no_focus_only_places_it()
    {
        var raised = new List<GridActionEventArgs<TestRow>>();
        GridSelection? selection = null;
        GridColumn<TestRow>[] columns =
        [
            GridColumn<TestRow>.ActionColumn("Open", [new GridAction("open", "Open")], width: Fixed100),
            new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        ];
        var cut = RenderGrid(raised, s => selection = s, columns);

        await PressAsync(cut, " ");

        Assert.Equal(new CellPosition(0, 0), selection!.Focus);
        Assert.Empty(raised);

        // The Focus is placed now, so the next Space is an ordinary one.
        await PressAsync(cut, " ");
        Assert.Single(raised);
    }

    [Fact] // ADR-0033/0037 / A11Y-17: the grid's own buttons are out of the page's tab sequence
    public void Action_buttons_are_out_of_the_tab_sequence()
    {
        var cut = RenderGrid();

        var buttons = cut.FindAll("button.ex-action");
        Assert.NotEmpty(buttons);
        Assert.All(buttons, button => Assert.Equal("-1", button.GetAttribute("tabindex")));
    }
}
