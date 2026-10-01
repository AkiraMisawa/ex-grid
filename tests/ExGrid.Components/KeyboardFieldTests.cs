using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The Keyboard Field (ADR-0080): the text field of the grid's own that holds the keyboard on a
/// grid that edits while no edit is open, so that an IME can start on a selected cell. What layer 2
/// can see: whether the field stands, where, and when it is read-only; which element is the tab stop
/// and which carries <c>aria-activedescendant</c> (ADR-0033); and what the core does with a
/// composition's start and its text. The composition itself, where DOM focus is, the ring and the
/// release of Tab ending as DOM focus leaves are the script's: layer 3's, and the source-shape tests'
/// (ShippedStylesheetTests). 20px rows; Book editable, Amount not, each 100px.
/// </summary>
public class KeyboardFieldTests : GridTestContext
{
    private const double RowHeightPx = 20;

    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] BookAndAmount(bool editable) =>
    [
        new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: editable),
        new GridColumn<TestRow>("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(bool editable = true, Action<GridEditIntent<TestRow>>? onEdit = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(200))
              .Add(g => g.TotalCount, 200)
              .Add(g => g.Columns, BookAndAmount(editable))
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            if (onEdit is not null)
                ps.Add(g => g.OnEdit, onEdit);
        });

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false));

    private static AngleSharp.Dom.IElement? KeyField(IRenderedComponent<ExGrid<TestRow>> cut)
        => KeyboardHolder.KeyFieldOf(cut.Find(".ex-grid"));

    /// <summary>Scrolls the Focus row out of the Window and lets the fling settle (ADR-0004).</summary>
    private async Task ScrollFarAsync(IRenderedComponent<ExGrid<TestRow>> cut, double top)
    {
        await ScrollToAsync(cut.Find(".ex-scroller"), top, 0);
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
    }

    [Fact] // ADR-0080 / ADR-0033 / A11Y-4 / ADR-0003: one field, the Viewport's first child, in no row; it is the grid's one tab stop, and the root is -1
    public void ADR0080_a_grid_that_edits_has_one_keyboard_field_and_it_is_the_tab_stop()
    {
        var cut = RenderGrid();

        var field = Assert.Single(cut.FindAll("input.ex-key-field"));
        Assert.Empty(cut.FindAll(".ex-row input"));
        Assert.Same(cut.Find(".ex-viewport").FirstElementChild, field.ParentElement);
        Assert.Equal("0", field.GetAttribute("tabindex"));
        // Still focusable by a press and by script, and out of the sequence: Shift+Tab from the
        // field must not land on it (ADR-0012's release of Tab).
        Assert.Equal("-1", cut.Find(".ex-grid").GetAttribute("tabindex"));
        // Its value is the script's: nothing binds it.
        Assert.Null(field.GetAttribute("value"));
    }

    [Fact] // ADR-0080 / ADR-0033 / A11Y-4: a display-only grid has no field, and its root stays the one tab stop
    public void ADR0080_a_display_only_grid_has_no_field_and_its_root_is_the_tab_stop()
    {
        var cut = RenderGrid(editable: false);

        Assert.Empty(cut.FindAll(".ex-key-field"));
        Assert.Equal("0", cut.Find(".ex-grid").GetAttribute("tabindex"));
    }

    /// <summary>A grid whose headers carry the ▾ (a sort is listened to), with its first column
    /// pinned, so both of the header's paths paint one.</summary>
    private IRenderedComponent<ExGrid<TestRow>> RenderGridWithMenus(bool editable)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, BookAndAmount(editable))
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.PinnedColumnCount, 1)
            .Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> _) => { }));

    /// <summary>The elements the browser's Tab would stop on: the natively focusable ones and any
    /// with a tabindex, less those taken out of the sequence by tabindex -1.</summary>
    private static List<AngleSharp.Dom.IElement> TabStops(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll("button, input, select, textarea, a[href], [tabindex]")
            .Where(element => element.GetAttribute("tabindex") is not { } index || !index.StartsWith('-'))
            .Where(element => !element.HasAttribute("disabled"))];

    [Theory] // ADR-0080 (2026-10-02) / ADR-0033 / A11Y-4: the header's ▾ buttons are not tab stops, on a grid that edits and on one that does not; the grid's one tab stop is its field, or its root
    [InlineData(true)]
    [InlineData(false)]
    public void ADR0080_the_headers_menu_buttons_are_out_of_the_tab_sequence(bool editable)
    {
        var cut = RenderGridWithMenus(editable);

        var menus = cut.FindAll(".ex-menu-button");
        Assert.Equal(2, menus.Count);
        Assert.All(menus, button => Assert.Equal("-1", button.GetAttribute("tabindex")));
        var stop = Assert.Single(TabStops(cut));
        Assert.Equal(editable ? "ex-key-field" : "ex-grid", stop.ClassList[0]);
    }

    [Fact] // ADR-0080 (2026-10-02) / ADR-0044 / FL-12: out of the tab sequence, the ▾ is still pressed, and Alt+↓ reaches the same popover by key
    public async Task ADR0080_a_press_and_alt_down_still_open_the_columns_popover()
    {
        var cut = RenderGridWithMenus(editable: true);

        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());
        Assert.Equal("Amount", cut.Find(".ex-popover [role=menu]").GetAttribute("aria-label"));
        // Escape in the popover, as the capture listener forwards a descendant's (ADR-0039).
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Escape", false, false, false, false, false, fromDescendant: true));
        Assert.Empty(cut.FindAll(".ex-popover"));

        await ClickCellAsync(cut, 150, 10); // row 0, Amount
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, true, false, false));
        Assert.Equal("Amount", cut.Find(".ex-popover [role=menu]").GetAttribute("aria-label"));
    }

    [Fact] // ADR-0080 / ADR-0033 / A11Y-20: a Prerendered grid that edits has no field and no tab stop
    public void ADR0080_a_prerendered_grid_that_edits_has_no_field_and_no_tab_stop()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));

        var cut = RenderGrid();

        Assert.Empty(cut.FindAll(".ex-key-field"));
        Assert.False(cut.Find(".ex-grid").HasAttribute("tabindex"));
        Assert.Equal("true", cut.Find(".ex-grid").GetAttribute("aria-busy"));
    }

    private sealed class NoListenerYet : BunitContext
    {
        // Interactive, but the module has not come back: on a circuit that is a module import and
        // a call away. Every call is left unanswered.
        public NoListenerYet()
        {
            Services.AddSingleton<Microsoft.JSInterop.IJSRuntime>(new Unanswered());
            SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        }

        private sealed class Unanswered : Microsoft.JSInterop.IJSRuntime
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
                new(new TaskCompletionSource<TValue>().Task);

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
                new(new TaskCompletionSource<TValue>().Task);
        }
    }

    [Fact] // ADR-0080 / A11Y-20: the field stands only once the key listener is attached — before, nothing would hear its keys
    public void ADR0080_the_field_waits_for_the_key_listener()
    {
        using var context = new NoListenerYet();

        var cut = context.Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(20))
            .Add(g => g.Columns, BookAndAmount(editable: true)));

        Assert.Empty(cut.FindAll(".ex-key-field"));
        Assert.False(cut.Find(".ex-grid").HasAttribute("tabindex"));
    }

    [Fact] // ADR-0080 / ADR-0033 / A11Y-5: the field carries aria-activedescendant, naming the Focus cell, and the root carries none
    public async Task ADR0080_the_field_carries_aria_activedescendant_and_the_root_none()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 30);

        var id = KeyField(cut)!.GetAttribute("aria-activedescendant");

        Assert.NotNull(id);
        Assert.EndsWith("r1c0", id, StringComparison.Ordinal);
        Assert.Equal("gridcell", cut.Find($"[id='{id}']").GetAttribute("role"));
        Assert.False(cut.Find(".ex-grid").HasAttribute("aria-activedescendant"));
        Assert.Equal(id, KeyboardHolder.ActiveDescendant(cut.Find(".ex-grid")));

        // It follows the Focus, as the root's did.
        await PressAsync(cut, "ArrowRight");
        Assert.EndsWith("r1c1", KeyField(cut)!.GetAttribute("aria-activedescendant"), StringComparison.Ordinal);
        Assert.False(cut.Find(".ex-grid").HasAttribute("aria-activedescendant"));
    }

    [Fact] // ADR-0080 / ADR-0033 / A11Y-5: a display-only grid keeps aria-activedescendant on its root
    public async Task ADR0080_a_display_only_grid_keeps_aria_activedescendant_on_its_root()
    {
        var cut = RenderGrid(editable: false);
        await ClickCellAsync(cut, 150, 30);

        Assert.EndsWith("r1c1", cut.Find(".ex-grid").GetAttribute("aria-activedescendant"), StringComparison.Ordinal);
    }

    [Fact] // ADR-0080 / ADR-0033 / A11Y-6: the field's attribute is cleared while the Focus is not painted, and the root's stays absent
    public async Task ADR0080_the_fields_aria_activedescendant_is_cleared_while_the_focus_is_not_painted()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);

        await ScrollFarAsync(cut, 100 * RowHeightPx);

        Assert.False(KeyField(cut)!.HasAttribute("aria-activedescendant"));
        Assert.False(cut.Find(".ex-grid").HasAttribute("aria-activedescendant"));

        await ScrollFarAsync(cut, 0);
        Assert.EndsWith("r0c0", KeyField(cut)!.GetAttribute("aria-activedescendant"), StringComparison.Ordinal);
        Assert.False(cut.Find(".ex-grid").HasAttribute("aria-activedescendant"));
    }

    [Fact] // ADR-0080 / ADR-0018 / A11Y-5: two grids that edit each have a field of their own, naming a cell of their own
    public async Task ADR0080_two_grids_that_edit_each_have_their_own_field()
    {
        var columns = (IReadOnlyList<GridColumn<TestRow>>)BookAndAmount(editable: true);
        var cut = Render(builder =>
        {
            builder.OpenComponent<ExGrid<TestRow>>(0);
            builder.AddComponentParameter(1, "Window", (IReadOnlyList<TestRow>)TestRows.Many(20));
            builder.AddComponentParameter(2, "Columns", columns);
            builder.CloseComponent();
            builder.OpenComponent<ExGrid<TestRow>>(3);
            builder.AddComponentParameter(4, "Window", (IReadOnlyList<TestRow>)TestRows.Many(20));
            builder.AddComponentParameter(5, "Columns", columns);
            builder.CloseComponent();
        });
        var viewports = cut.FindAll(".ex-viewport");
        await viewports[0].MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });
        await viewports[1].MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });

        var roots = cut.FindAll(".ex-grid");
        Assert.Equal(2, roots.Count);
        Assert.Equal(2, cut.FindAll("input.ex-key-field").Count);
        var ids = roots.Select(root => KeyboardHolder.ActiveDescendant(root)!).ToList();
        Assert.NotEqual(ids[0], ids[1]);
        for (var i = 0; i < roots.Count; i++)
        {
            // Each resolves to a cell inside its own grid.
            var named = cut.Find($"[id='{ids[i]}']");
            Assert.Equal(roots[i].Id, named.Closest(".ex-grid")!.Id);
        }
    }

    [Fact] // ADR-0080 / ADR-0010 / ADR-0008: the field stands in the Focus cell's box, the Cell Editor's arithmetic, read-only where typing opens nothing
    public async Task ADR0080_the_field_stands_over_the_focus_cell_and_is_read_only_where_typing_opens_nothing()
    {
        var cut = RenderGrid();

        await ClickCellAsync(cut, 50, 30);
        var field = KeyField(cut)!;
        Assert.Equal("left: 0px; top: 20px; width: 100px; height: 20px", field.GetAttribute("style"));
        Assert.False(field.HasAttribute("readonly"));

        // Amount is not Editable: an IME must stay off there, as it did on the root.
        await ClickCellAsync(cut, 150, 30);
        field = KeyField(cut)!;
        Assert.Equal("left: 100px; top: 20px; width: 100px; height: 20px", field.GetAttribute("style"));
        Assert.True(field.HasAttribute("readonly"));
    }

    [Fact] // ADR-0080 / ADR-0012: with no Focus the field stands over the first painted cell, where the first key would open the edit, and takes typing
    public void ADR0080_with_no_focus_the_field_stands_over_the_first_painted_cell()
    {
        var cut = RenderGrid();

        var field = KeyField(cut)!;
        Assert.Equal("left: 0px; top: 0px; width: 100px; height: 20px", field.GetAttribute("style"));
        Assert.False(field.HasAttribute("readonly"));
    }

    [Fact] // ADR-0080 / ED-30: with the Focus not painted the field waits held at the left edge; a composition's start reveals the Focus, and opens no edit
    public async Task ADR0080_a_compositions_start_reveals_the_focus_and_opens_nothing()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 30); // row 1
        await ScrollFarAsync(cut, 100 * RowHeightPx);
        Assert.Equal("left: 0px; top: 0px; width: 0px; height: 0px", KeyField(cut)!.GetAttribute("style"));
        Assert.Contains("ex-key-field-held", KeyField(cut)!.ParentElement!.ClassList);
        var writes = Js.ScrolledTo.Count;

        await cut.InvokeAsync(() => cut.Instance.OnKeyFieldCompositionStartAsync());

        // The Focus is revealed, as Excel scrolls to the active cell when typing begins...
        Assert.True(Js.ScrolledTo.Count > writes);
        Assert.True(Js.ScrolledTo[^1].Top <= RowHeightPx, $"revealed at {Js.ScrolledTo[^1].Top}");
        await ScrollFarAsync(cut, Js.ScrolledTo[^1].Top);
        // The Focus is painted again, and the field stands over it, in its box, no longer waiting.
        Assert.EndsWith("r1c0", KeyField(cut)!.GetAttribute("aria-activedescendant"), StringComparison.Ordinal);
        Assert.Matches(@"^left: 0px; top: -?\d+(\.\d+)?px; width: 100px; height: 20px$", KeyField(cut)!.GetAttribute("style"));
        Assert.DoesNotContain("ex-key-field-held", KeyField(cut)!.ParentElement!.ClassList);
        // ...and nothing else: no edit is open while the composition lasts.
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0080 / ED-30: a composition's start over a painted Focus moves nothing
    public async Task ADR0080_a_compositions_start_over_a_painted_focus_moves_nothing()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 30);
        var writes = Js.ScrolledTo.Count;

        await cut.InvokeAsync(() => cut.Instance.OnKeyFieldCompositionStartAsync());

        Assert.Equal(writes, Js.ScrolledTo.Count);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0080 / ADR-0010 / ED-30 / ED-4: a composition's text opens Overwrite holding it, as a typed character does
    public async Task ADR0080_a_compositions_text_opens_overwrite_holding_it()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(onEdit: intents.Add);
        await ClickCellAsync(cut, 50, 30);

        var opened = await cut.InvokeAsync(() => cut.Instance.OnKeyFieldTextAsync("かな"));

        Assert.True(opened);
        Assert.Equal("かな", cut.Find("input.ex-editor").GetAttribute("value"));
        // The editor is asked for the keyboard, as after a typed character (ADR-0021's note of
        // 2026-09-30); the script makes that request wait while the field composes.
        Assert.Contains(GridJSInterop.CellSurface, Js.Focused);
        // Overwrite: an arrow commits and moves (ADR-0010).
        await PressAsync(cut, "ArrowDown");
        var intent = Assert.Single(intents);
        Assert.Equal("かな", intent.Value);
        Assert.Equal("Row 000001", intent.Row.Book);
    }

    [Fact] // ADR-0080 / ADR-0012 / ED-30: with no Focus, a composition's text opens the edit where the first key would, as a typed character does
    public async Task ADR0080_a_compositions_text_with_no_focus_opens_on_the_first_painted_cell()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(onEdit: intents.Add);

        var opened = await cut.InvokeAsync(() => cut.Instance.OnKeyFieldTextAsync("か"));

        Assert.True(opened);
        Assert.Equal("か", cut.Find("input.ex-editor").GetAttribute("value"));
        await PressAsync(cut, "Enter");
        Assert.Equal("Row 000000", Assert.Single(intents).Row.Book);
    }

    [Fact] // ADR-0080 / ED-30: a second composition ended before the first one's editor has the keyboard opens nothing again; the script types it into the edit
    public async Task ADR0080_a_second_compositions_text_with_an_edit_open_leaves_the_edit_as_it_is()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 30);
        await cut.InvokeAsync(() => cut.Instance.OnKeyFieldTextAsync("かな"));

        var open = await cut.InvokeAsync(() => cut.Instance.OnKeyFieldTextAsync("です"));

        Assert.True(open);
        Assert.Equal("かな", Assert.Single(cut.FindAll("input.ex-editor")).GetAttribute("value"));
    }

    [Fact] // ADR-0080 / ED-30 / the fifteenth Windows run, i2: a cancelled composition leaves an edit open and empty, as Excel's
    public async Task ADR0080_a_cancelled_composition_opens_an_empty_edit()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 30);

        var opened = await cut.InvokeAsync(() => cut.Instance.OnKeyFieldTextAsync(""));

        Assert.True(opened);
        Assert.Equal("", cut.Find("input.ex-editor").GetAttribute("value"));
        await PressAsync(cut, "Escape");
        Assert.Empty(cut.FindAll("input.ex-editor"));
    }

    [Fact] // ADR-0080 / ADR-0035 / ED-30: over a cell that does not edit, a composition's text opens nothing
    public async Task ADR0080_a_compositions_text_over_a_cell_that_does_not_edit_opens_nothing()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 150, 30);

        var opened = await cut.InvokeAsync(() => cut.Instance.OnKeyFieldTextAsync("かな"));

        Assert.False(opened);
        Assert.Empty(cut.FindAll("input.ex-editor"));
    }

    [Fact] // ADR-0080 / ADR-0021 / ADR-0018: the keyboard comes back to the field through the handle's hand-back, the one the root's came through
    public async Task ADR0080_an_edit_ending_hands_the_keyboard_back_through_the_handle()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 30);
        await cut.InvokeAsync(() => cut.Instance.OnKeyFieldTextAsync("かな"));
        var reclaims = Js.FocusReclaimed.Invocations.Count;

        await PressAsync(cut, "Escape");

        // Where it lands — the field, while the keyboard is still this grid's — is the script's to
        // decide (reclaimFocus); the core asks no element of its own.
        Assert.True(Js.FocusReclaimed.Invocations.Count > reclaims);
        Assert.Equal(Js.RootReferenceId, Js.Focused[^1]);
    }
}
