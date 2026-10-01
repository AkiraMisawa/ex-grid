using Bunit;
using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// What a Consumer does with the grid's keyboard (ADR-0069). It gives the keyboard back to the
/// root when something of its own that held it over the grid goes away: the grid's own hand-back,
/// offered to it. Whether the browser grants it — DOM focus on nothing or already inside this grid,
/// never on another control, another grid, or the grid's Formula Bar or Name Box — is the handle's
/// to decide and layer 3's to show; here, that it is asked for under that condition, that it moves
/// nothing else, and that nothing is asked before the grid is attached (DC-56). And it hears the
/// Escape that leaves the grid: only the one pressed on the root with nothing left to dismiss, once
/// per press, in place of releasing the DOM focus, and never an inner layer's (DC-57).
///
/// 20px rows in a 120px Viewport whose header takes the first 20: five rows painted. Columns, all
/// fixed: Book 0–100 (editable), Review 100–300 (three actions), Amount 300–400.
/// </summary>
public class LeaveAndReturnKeyboardTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const int Book = 0;
    private const int Review = 1;
    private const int Amount = 2;

    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        GridColumn<TestRow>.ActionColumn("Review",
            [new GridAction("approve", "Approve"), new GridAction("query", "Query"), new GridAction("escalate", "Escalate")],
            width: new ColumnWidthSpec(ColumnWidth.Fixed(200))),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary>A Chrome whose Context Menu records the context it was handed and renders a
    /// marker: what a design system's menu with a popup of its own reports through.</summary>
    private sealed class StubChrome : IGridChrome
    {
        public object? Context { get; private set; }

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? ContextMenu<TRow>(ContextMenuContext<TRow> context)
        {
            Context = context;
            return builder => builder.AddMarkupContent(0, "<div class='ex-stub-context'>stub context</div>");
        }

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action? onLeave = null,
        Action<GridSelection>? onSelectionChanged = null,
        IGridChrome? chrome = null,
        Func<string, int, ValueTask<EditorCompletion?>>? complete = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 600);
            if (onLeave is not null)
                ps.Add(g => g.OnLeave, onLeave);
            if (onSelectionChanged is not null)
                ps.Add(g => g.SelectionChanged, onSelectionChanged);
            if (chrome is not null)
                ps.Add(g => g.Chrome, chrome);
            if (complete is not null)
                ps.Add(g => g.CompleteEditorText, complete);
        });

    /// <summary>The body cell at (row, column) of the rows painted from the top — the Viewport's
    /// own arithmetic puts the press there.</summary>
    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, int row, int column)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            Button = 0,
            Buttons = 1,
            OffsetX = column switch { Book => 50, Review => 150, _ => 350 },
            OffsetY = (row * RowHeightPx) + 10,
        });

    /// <summary>A key as the capture-phase listener forwards it: from the root unless it came
    /// from a focusable descendant, and a repeat when the browser said the key is held.</summary>
    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool shift = false, bool fromDescendant = false,
        bool repeat = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            key, ctrl: false, shift: shift, alt: false, meta: false, metaIsPrimary: false,
            fromDescendant: fromDescendant, repeat: repeat));

    private static Task ReturnKeyboardAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.InvokeAsync(() => cut.Instance.ReturnKeyboardAsync());

    private static string? ActiveDescendant(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-grid").GetAttribute("aria-activedescendant");

    // ---- DC-56: ReturnKeyboardAsync ------------------------------------------------------------

    [Fact] // ADR-0069/0021 (DC-56): the keyboard given back is the grid's own hand-back, asked of the handle under its condition — and never from the Formula Bar or the Name Box
    public async Task Returning_the_keyboard_asks_the_handle_for_the_root()
    {
        var cut = RenderGrid();
        var reclaims = Js.FocusReclaimed.Invocations.Count;
        var focusCalls = Js.FocusCalls;

        await ReturnKeyboardAsync(cut);

        // One request, the root's, through the handle that grants it only while DOM focus is on
        // nothing or already inside this root: never from another control or another grid. And
        // not as a gesture made in the Formula Bar or the Name Box, whose fields keep the keyboard.
        Assert.Equal(reclaims + 1, Js.FocusReclaimed.Invocations.Count);
        Assert.False((bool)Js.FocusReclaimed.Invocations.Last().Arguments[0]!);
        Assert.Equal(focusCalls + 1, Js.FocusCalls);
        Assert.Equal(Js.RootReferenceId, Js.Focused[^1]);
        // Taking the keyboard is all it does: nothing is released.
        Assert.Equal(0, Js.BlurCount);
    }

    [Fact] // ADR-0069 (DC-56): the keyboard comes back to the cell it left — neither the Focus nor the Selection moves, nothing scrolls, and nothing renders
    public async Task Returning_the_keyboard_moves_nothing()
    {
        var selections = new List<GridSelection>();
        var cut = RenderGrid(onSelectionChanged: selections.Add);
        await ClickCellAsync(cut, 2, Amount);
        var selected = selections.Count;
        var focus = ActiveDescendant(cut);
        var scrolls = Js.ScrolledTo.Count;
        var renders = cut.RenderCount;

        await ReturnKeyboardAsync(cut);

        Assert.Equal(selected, selections.Count);
        Assert.Equal(new CellPosition(2, Amount), selections[^1].Focus);
        Assert.Equal(focus, ActiveDescendant(cut));
        Assert.Equal(scrolls, Js.ScrolledTo.Count);
        Assert.Equal(renders, cut.RenderCount);
    }

    /// <summary>Interactive, but the grid's module has not come back: on a circuit, the listener
    /// is a module import and a call away. Every call is recorded and left unanswered.</summary>
    private sealed class NoListenerYet : BunitContext
    {
        public NoListenerYet()
        {
            Services.AddSingleton<Microsoft.JSInterop.IJSRuntime>(Runtime);
            SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        }

        public Unanswered Runtime { get; } = new();

        public sealed class Unanswered : Microsoft.JSInterop.IJSRuntime
        {
            public List<string> Asked { get; } = [];

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            {
                Asked.Add(identifier);
                return new(new TaskCompletionSource<TValue>().Task);
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
                => InvokeAsync<TValue>(identifier, args);
        }
    }

    [Fact] // ADR-0069 (DC-56): before the grid is attached it does nothing — no listener is on the root yet to hand keys to
    public async Task Returning_the_keyboard_before_the_grid_is_attached_does_nothing()
    {
        using var context = new NoListenerYet();
        var cut = context.Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(20))
            .Add(g => g.Columns, Columns()));

        await cut.InvokeAsync(() => cut.Instance.ReturnKeyboardAsync());

        // The module's import, which never came back, and nothing else.
        Assert.Equal(["import"], context.Runtime.Asked);
    }

    [Fact] // ADR-0069 (DC-56): a grid that is gone takes nothing — a Consumer may still hold it when what covered it closes
    public async Task Returning_the_keyboard_to_a_grid_that_is_gone_does_nothing()
    {
        var cut = RenderGrid();
        var grid = cut.Instance;
        var reclaims = Js.FocusReclaimed.Invocations.Count;
        await DisposeComponentsAsync();

        await grid.ReturnKeyboardAsync();

        Assert.Equal(reclaims, Js.FocusReclaimed.Invocations.Count);
    }

    // ---- DC-57: OnLeave ------------------------------------------------------------------------

    [Fact] // ADR-0069 (DC-57): with OnLeave declared, an Escape on the root with nothing left to dismiss raises it once, and the grid keeps the DOM focus
    public async Task An_escape_with_nothing_left_to_dismiss_raises_on_leave_and_keeps_the_focus()
    {
        var left = 0;
        var cut = RenderGrid(onLeave: () => left++);
        await ClickCellAsync(cut, 0, Amount);
        var focus = ActiveDescendant(cut);

        await PressAsync(cut, "Escape");

        Assert.Equal(1, left);
        Assert.Equal(0, Js.BlurCount);
        Assert.Equal(focus, ActiveDescendant(cut));
    }

    [Fact] // ADR-0069/0012 (DC-57, DC-1): without OnLeave, Escape with nothing left to dismiss releases the DOM focus as before — once a press, as every Escape acts (ADR-0012, refined 2026-10-01)
    public async Task Without_on_leave_escape_releases_the_focus_as_before()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, Amount);

        await PressAsync(cut, "Escape");
        await PressAsync(cut, "Escape", repeat: true);

        Assert.Equal(1, Js.BlurCount);
    }

    [Fact] // ADR-0012 (refined 2026-10-01): a held Escape is one press — the press closes the popover, and its repeats release nothing
    public async Task A_held_escape_closes_a_popover_and_its_repeats_release_nothing()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, Amount);
        await PressAsync(cut, "F10", shift: true);
        Assert.Single(cut.FindAll(".ex-popover"));

        await PressAsync(cut, "Escape", fromDescendant: true);
        await PressAsync(cut, "Escape", repeat: true);
        await PressAsync(cut, "Escape", repeat: true);

        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Equal(0, Js.BlurCount);
    }

    [Fact] // ADR-0012/0051 (refined 2026-10-01): a held Escape is one press — the press closes a Formula Entry's list, and its repeats leave the edit standing
    public async Task A_held_escape_closes_a_formula_entrys_list_and_its_repeats_leave_the_edit()
    {
        var cut = RenderGrid(complete: (text, caret) => ValueTask.FromResult<EditorCompletion?>(
            text.StartsWith('=') && caret > 1
                ? new EditorCompletion([new CompletionCandidate("SUM", 1, caret - 1, "SUM(")])
                : null));
        await ClickCellAsync(cut, 0, Book);
        await PressAsync(cut, "=");
        await cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = "=SU" });
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync("=SU", 3));
        Assert.Single(cut.FindAll(".ex-completion"));

        await PressAsync(cut, "Escape");
        await PressAsync(cut, "Escape", repeat: true);
        await PressAsync(cut, "Escape", repeat: true);

        Assert.Empty(cut.FindAll(".ex-completion"));
        Assert.Single(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.Equal(0, Js.BlurCount);
    }

    [Fact] // ADR-0069 (DC-57): a held Escape raises OnLeave once — its repeats raise nothing and release nothing until the key is released, and the next press raises it again
    public async Task A_held_escape_raises_on_leave_once_per_press()
    {
        var left = 0;
        var cut = RenderGrid(onLeave: () => left++);

        await PressAsync(cut, "Escape");
        await PressAsync(cut, "Escape", repeat: true);
        await PressAsync(cut, "Escape", repeat: true);

        Assert.Equal(1, left);
        Assert.Equal(0, Js.BlurCount);

        await PressAsync(cut, "Escape");

        Assert.Equal(2, left);
        Assert.Equal(0, Js.BlurCount);
    }

    [Fact] // ADR-0069/0039 (DC-57): an Escape that closes a popover peels only that layer, and so do the repeats of the key held after it; the next press raises OnLeave
    public async Task An_escape_that_closes_a_popover_does_not_raise_on_leave()
    {
        var left = 0;
        var cut = RenderGrid(onLeave: () => left++);
        await ClickCellAsync(cut, 0, Amount);
        await PressAsync(cut, "F10", shift: true);
        Assert.Single(cut.FindAll(".ex-popover"));

        // From the menu's item, which holds the keyboard once the popover has taken it.
        await PressAsync(cut, "Escape", fromDescendant: true);
        await PressAsync(cut, "Escape", repeat: true);

        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Equal(0, left);
        Assert.Equal(0, Js.BlurCount);

        await PressAsync(cut, "Escape");

        Assert.Equal(1, left);
    }

    [Fact] // ADR-0069/0039 (DC-57): an Inner Popup's Escape is its design system's — the gate leaves it to the control the core was told of — and the popover's after it is the popover's; only the next raises OnLeave
    public async Task An_inner_popups_escape_and_its_popovers_do_not_raise_on_leave()
    {
        var left = 0;
        var chrome = new StubChrome();
        var cut = RenderGrid(onLeave: () => left++, chrome: chrome);
        await ClickCellAsync(cut, 0, Amount);
        await PressAsync(cut, "F10", shift: true);
        var menu = Assert.IsType<ContextMenuContext<TestRow>>(chrome.Context);

        await cut.InvokeAsync(() => menu.InnerPopupChanged!(true));

        // The gate is told, and leaves the popup's own Escape to its control: that one never
        // reaches the core. The next is forwarded from the descendant and closes the popover,
        // the popup with it.
        Assert.True((bool)Js.InnerPopupTold.Invocations.Last().Arguments[0]!);
        await PressAsync(cut, "Escape", fromDescendant: true);

        Assert.Empty(cut.FindAll(".ex-stub-context"));
        Assert.Equal(0, left);

        await PressAsync(cut, "Escape");

        Assert.Equal(1, left);
        Assert.Equal(0, Js.BlurCount);
    }

    [Fact] // ADR-0069/0007 (DC-57): an Escape that cancels an edit does not raise OnLeave; the next one does
    public async Task An_escape_that_cancels_an_edit_does_not_raise_on_leave()
    {
        var left = 0;
        var cut = RenderGrid(onLeave: () => left++);
        await ClickCellAsync(cut, 0, Book);
        await PressAsync(cut, "x");
        Assert.Single(cut.FindAll(".ex-viewport .ex-editor"));

        await PressAsync(cut, "Escape");

        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.Equal(0, left);

        await PressAsync(cut, "Escape");

        Assert.Equal(1, left);
        Assert.Equal(0, Js.BlurCount);
    }

    [Fact] // ADR-0069/0051 (DC-57): an Escape that closes a Formula Entry's list leaves the edit open and does not raise OnLeave; nor does the one that cancels the edit; the third does
    public async Task An_escape_that_closes_a_formula_entrys_list_does_not_raise_on_leave()
    {
        var left = 0;
        var cut = RenderGrid(onLeave: () => left++, complete: (text, caret) => ValueTask.FromResult<EditorCompletion?>(
            text.StartsWith('=') && caret > 1
                ? new EditorCompletion([new CompletionCandidate("SUM", 1, caret - 1, "SUM(")])
                : null));
        await ClickCellAsync(cut, 0, Book);
        await PressAsync(cut, "=");
        await cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = "=SU" });
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync("=SU", 3));
        Assert.Single(cut.FindAll(".ex-completion"));

        await PressAsync(cut, "Escape");

        Assert.Empty(cut.FindAll(".ex-completion"));
        Assert.Single(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.Equal(0, left);

        await PressAsync(cut, "Escape");

        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.Equal(0, left);

        await PressAsync(cut, "Escape");

        Assert.Equal(1, left);
        Assert.Equal(0, Js.BlurCount);
    }

    [Fact] // ADR-0069/0037 (DC-57): an Escape that leaves an Interactive cell does not raise OnLeave; the next one does
    public async Task An_escape_that_leaves_an_interactive_cell_does_not_raise_on_leave()
    {
        var left = 0;
        var cut = RenderGrid(onLeave: () => left++);
        await ClickCellAsync(cut, 0, Review);
        await PressAsync(cut, " ");
        Assert.Single(cut.FindAll("button.ex-action-chosen"));

        await PressAsync(cut, "Escape");

        Assert.Empty(cut.FindAll("button.ex-action-chosen"));
        Assert.Equal(0, left);

        await PressAsync(cut, "Escape");

        Assert.Equal(1, left);
        Assert.Equal(0, Js.BlurCount);
    }

    [Fact] // ADR-0069/0020 (DC-57): an Escape that returns from a control inside a cell takes the keyboard back to the root and does not raise OnLeave; the next one does
    public async Task An_escape_from_a_control_inside_a_cell_does_not_raise_on_leave()
    {
        var left = 0;
        var cut = RenderGrid(onLeave: () => left++);
        await ClickCellAsync(cut, 0, Amount);
        var reclaims = Js.FocusReclaimed.Invocations.Count;

        await PressAsync(cut, "Escape", fromDescendant: true);

        Assert.Equal(reclaims + 1, Js.FocusReclaimed.Invocations.Count);
        Assert.Equal(0, left);

        await PressAsync(cut, "Escape");

        Assert.Equal(1, left);
        Assert.Equal(0, Js.BlurCount);
    }
}
