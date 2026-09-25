using AngleSharp.Dom;
using Bunit;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A popover takes the keyboard when it opens and gives it back when it closes
/// (ADR-0039): Alt+↓ opens the column menu, every opening asks the contents to take DOM
/// focus, every close returns it to the root, and each popover is named.
/// </summary>
public class PopoverKeyboardTests : GridTestContext
{
    private const string Focus = "Blazor._internal.domWrapper.focus";
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, filterUi: FilterUiMode.Both),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary>A Chrome whose seams record what they were handed and render markers.</summary>
    private sealed class StubChrome : IGridChrome
    {
        public ColumnMenuContext? Menu { get; private set; }

        public FilterPanelContext? Panel { get; private set; }

        public object? Context { get; private set; }

        public RenderFragment? FilterPanel(FilterPanelContext context)
        {
            Panel = context;
            return builder => builder.AddMarkupContent(0, "<div class='ex-stub-panel'>stub panel</div>");
        }

        public RenderFragment? ColumnMenu(ColumnMenuContext context)
        {
            Menu = context;
            return builder => builder.AddMarkupContent(0, "<div class='ex-stub-menu'>stub menu</div>");
        }

        public RenderFragment? ContextMenu<TRow>(ContextMenuContext<TRow> context)
        {
            Context = context;
            return builder => builder.AddMarkupContent(0, "<div class='ex-stub-context'>stub context</div>");
        }

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        IGridChrome? chrome = null, bool withSource = true, TestSource? source = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            if (withSource)
            {
                source ??= new TestSource();
                source.Push(TestRows.Window(), totalCount: 3);
                ps.Add(g => g.Source, source);
            }
            else
            {
                ps.Add(g => g.Window, TestRows.Window()).Add(g => g.TotalCount, 3);
            }
            ps.Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            if (chrome is not null)
                ps.Add(g => g.Chrome, chrome);
        });

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(
            new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task KeyAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool alt = false, bool shift = false,
        bool fromDescendant = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            key, ctrl: false, shift: shift, alt: alt, meta: false, metaIsPrimary: false,
            fromDescendant: fromDescendant));

    private static Task AltDownAsync(IRenderedComponent<ExGrid<TestRow>> cut) => KeyAsync(cut, "ArrowDown", alt: true);

    private static IElement Item(IRenderedComponent<ExGrid<TestRow>> cut, string label)
        => cut.FindAll(".ex-popover button[role=menuitem]").Single(b => b.TextContent == label);

    private static string RefOf(IElement element)
        => element.GetAttribute("blazor:elementreference")
           ?? throw new InvalidOperationException("the element binds no reference");

    /// <summary>Every element the grid has asked the browser to focus, in order.</summary>
    private string[] Focused()
        => [.. JSInterop.Invocations
            .Where(i => i.Identifier == Focus)
            .Select(i => ((ElementReference)i.Arguments[0]!).Id)];

    private string? LastFocused() => Focused().LastOrDefault();

    /// <summary>The root's reference, as the listener was attached to it: bUnit writes a
    /// reference's id into the markup only when the element is new, and by the time a
    /// test runs the root has been rendered again (the attach lifts Prerendered).</summary>
    private string RootRef(IRenderedComponent<ExGrid<TestRow>> cut) => Js.RootReferenceId;

    [Fact] // ADR-0039 / KB-28: Alt+↓ opens the menu of the Focus's column, named by its header
    public async Task Alt_down_opens_the_menu_of_the_focus_column()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 150, 10); // row 0, Amount

        await AltDownAsync(cut);

        var menu = cut.Find(".ex-popover[role=menu]");
        Assert.Equal("Amount", menu.GetAttribute("aria-label"));
        Assert.Contains(cut.FindAll(".ex-popover button[role=menuitem]"), b => b.TextContent == "Sort ascending");
    }

    [Fact] // ADR-0039 / KB-28: with no Focus, or on a grid whose headers carry no menu, Alt+↓ does nothing
    public async Task Alt_down_does_nothing_without_a_focus_or_without_a_menu()
    {
        var withMenus = RenderGrid();
        await AltDownAsync(withMenus);
        Assert.Empty(withMenus.FindAll(".ex-popover"));

        var withoutMenus = RenderGrid(withSource: false);
        await ClickCellAsync(withoutMenus, 150, 10);
        await AltDownAsync(withoutMenus);
        Assert.Empty(withoutMenus.FindAll(".ex-menu-button"));
        Assert.Empty(withoutMenus.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0039 / KB-29: the built-in menu puts DOM focus on its first enabled item
    public async Task The_built_in_menu_focuses_its_first_enabled_item()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 150, 10);

        await AltDownAsync(cut);

        Assert.Equal(RefOf(Item(cut, "Sort ascending")), LastFocused());
    }

    [Fact] // ADR-0039 / KB-29: opened by pointer too, the menu takes the keyboard
    public async Task A_menu_opened_by_its_button_takes_the_keyboard_too()
    {
        var cut = RenderGrid();

        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());

        Assert.Equal(RefOf(Item(cut, "Sort ascending")), LastFocused());
    }

    [Fact] // ADR-0039 / KB-29: the built-in panel focuses its first control once its contents have settled
    public async Task The_built_in_panel_focuses_its_first_control()
    {
        var cut = RenderGrid();

        // A value-list column: the list arrives, and its search field is the first control.
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        await Item(cut, "Filter").ClickAsync(new MouseEventArgs());
        Assert.Equal(RefOf(cut.Find(".ex-popover[role=dialog] input[type=search]")), LastFocused());

        // A condition column: the operator is.
        await cut.Find(".ex-popover[role=dialog] .ex-popover-actions button:nth-child(2)").ClickAsync(new MouseEventArgs());
        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());
        await Item(cut, "Filter").ClickAsync(new MouseEventArgs());
        Assert.Equal(RefOf(cut.Find(".ex-popover[role=dialog] select")), LastFocused());
    }

    [Fact] // ADR-0039 / KB-29: the Context Menu opened by key takes the keyboard as well
    public async Task The_context_menu_opened_by_key_focuses_its_first_item()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);

        await KeyAsync(cut, "F10", shift: true);

        Assert.Equal(RefOf(Item(cut, "Copy")), LastFocused());
    }

    [Fact] // ADR-0039 / KB-29: each opening counts the request once, in every context, and a render does not
    public async Task Every_opening_counts_the_focus_request_once()
    {
        var chrome = new StubChrome();
        var cut = RenderGrid(chrome);
        await ClickCellAsync(cut, 50, 10);

        await AltDownAsync(cut);
        var first = chrome.Menu!.FocusRequest;
        Assert.True(first > 0);
        cut.Render();
        Assert.Equal(first, chrome.Menu!.FocusRequest);

        await KeyAsync(cut, "Escape", fromDescendant: true);
        await AltDownAsync(cut);
        Assert.Equal(first + 1, chrome.Menu!.FocusRequest);

        await cut.InvokeAsync(() => chrome.Menu!.Commands.Single(c => c.Id == "filter").Invoke());
        cut.Render();
        Assert.Equal(first + 2, chrome.Panel!.FocusRequest);

        await KeyAsync(cut, "Escape", fromDescendant: true);
        await KeyAsync(cut, "F10", shift: true);
        Assert.Equal(first + 3, ((ContextMenuContext<TestRow>)chrome.Context!).FocusRequest);
    }

    [Fact] // ADR-0039 / ADR-0037: the core never reaches into contents it did not render
    public async Task A_substituted_chrome_is_asked_and_never_focused_by_the_core()
    {
        var cut = RenderGrid(new StubChrome());
        await ClickCellAsync(cut, 50, 10);
        var before = Focused().Length;

        await AltDownAsync(cut);

        Assert.NotNull(cut.Find(".ex-stub-menu"));
        Assert.Equal(before, Focused().Length);
    }

    [Fact] // ADR-0039 / KB-32: a command run from a menu hands the keyboard back to the root
    public async Task Running_a_command_returns_the_keyboard_to_the_root()
    {
        var cut = RenderGrid();
        var root = RootRef(cut);
        await ClickCellAsync(cut, 150, 10);
        await AltDownAsync(cut);

        await Item(cut, "Sort ascending").ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Equal(root, LastFocused());
    }

    [Theory] // ADR-0039 / KB-32: Apply, Cancel and Clear each close the panel and hand the keyboard back
    [InlineData("OK")]
    [InlineData("Cancel")]
    [InlineData("Clear")]
    public async Task Closing_the_panel_returns_the_keyboard_to_the_root(string button)
    {
        var cut = RenderGrid();
        var root = RootRef(cut);
        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());
        await Item(cut, "Filter").ClickAsync(new MouseEventArgs());

        await cut.FindAll(".ex-popover[role=dialog] .ex-popover-actions button")
            .Single(b => b.TextContent == button).ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Equal(root, LastFocused());
    }

    [Fact] // ADR-0039 / KB-32: the ▾ pressed again closes the menu and hands the keyboard back
    public async Task The_toggle_returns_the_keyboard_to_the_root()
    {
        var cut = RenderGrid();
        var root = RootRef(cut);
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());

        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Equal(root, LastFocused());
    }

    [Fact] // ADR-0010 / ADR-0039: a command a substituted Chrome invokes closes the menu and hands the keyboard back
    public async Task A_command_run_by_a_substituted_chrome_closes_its_menu()
    {
        var chrome = new StubChrome();
        var source = new TestSource();
        var cut = RenderGrid(chrome, source: source);
        var root = RootRef(cut);
        await ClickCellAsync(cut, 150, 10);
        await AltDownAsync(cut);

        // Invoked from the Chrome's own event, not the grid's: the grid renders itself.
        await cut.InvokeAsync(() => chrome.Menu!.Commands.Single(c => c.Id == "sort-descending").Invoke());

        Assert.Equal([new SortSpec("Amount", SortDirection.Descending)], source.Sorts);
        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Equal(root, LastFocused());
    }

    [Fact] // ADR-0010 / ADR-0039: the filter command a Chrome invokes replaces the menu with the panel
    public async Task The_filter_command_run_by_a_chrome_leaves_the_panel_standing()
    {
        var chrome = new StubChrome();
        var cut = RenderGrid(chrome);
        await ClickCellAsync(cut, 150, 10);
        await AltDownAsync(cut);

        await cut.InvokeAsync(() => chrome.Menu!.Commands.Single(c => c.Id == "filter").Invoke());

        Assert.NotNull(cut.Find(".ex-stub-panel"));
        Assert.Empty(cut.FindAll(".ex-stub-menu"));
    }

    [Fact] // ADR-0039 / KB-32: a Chrome's own Close closes the popover, re-renders the grid, and hands the keyboard back
    public async Task A_chromes_own_close_returns_the_keyboard_to_the_root()
    {
        var chrome = new StubChrome();
        var cut = RenderGrid(chrome);
        var root = RootRef(cut);
        await ClickCellAsync(cut, 50, 10);
        await AltDownAsync(cut);
        Assert.NotNull(cut.Find(".ex-stub-menu"));

        await cut.InvokeAsync(() => chrome.Menu!.Close());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-popover")));
        Assert.Equal(root, LastFocused());
    }

    [Fact] // ADR-0039 / KB-32: Escape from inside a popover closes it and hands the keyboard back
    public async Task Escape_from_inside_returns_the_keyboard_to_the_root()
    {
        var cut = RenderGrid();
        var root = RootRef(cut);
        await ClickCellAsync(cut, 150, 10);
        await AltDownAsync(cut);

        await KeyAsync(cut, "Escape", fromDescendant: true);

        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Equal(root, LastFocused());
    }

    [Fact] // ADR-0039 / A11Y-19: the menus are menus, the panel a dialog, each column's named by its header
    public async Task Each_popover_is_named()
    {
        var cut = RenderGrid();
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        var menu = cut.Find(".ex-popover");
        Assert.Equal("menu", menu.GetAttribute("role"));
        Assert.Equal("Book", menu.GetAttribute("aria-label"));

        await Item(cut, "Filter").ClickAsync(new MouseEventArgs());
        var panel = cut.Find(".ex-popover");
        Assert.Equal("dialog", panel.GetAttribute("role"));
        Assert.Equal("Book", panel.GetAttribute("aria-label"));

        await KeyAsync(cut, "Escape", fromDescendant: true);
        await ClickCellAsync(cut, 50, 10);
        await KeyAsync(cut, "F10", shift: true);
        var context = cut.Find(".ex-popover");
        Assert.Equal("menu", context.GetAttribute("role"));
        Assert.Null(context.GetAttribute("aria-label"));
    }

    // ---- Inside a popover (ADR-0039's table, KB-30/31), under the built-in Chrome. With a
    // Source and nothing else wired, the column menu's enabled items are Sort ascending,
    // Sort descending and Filter; Hide, Pin, Unpin and Size to fit are disabled.

    private static Task PressAsync(IElement element, string key, bool shift = false)
        => element.KeyDownAsync(new KeyboardEventArgs { Key = key, ShiftKey = shift });

    /// <summary>The enabled items' references, read at the render that created them.</summary>
    private static Dictionary<string, string> EnabledItemRefs(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-popover button[role=menuitem]:not([disabled])")
            .ToDictionary(b => b.TextContent, RefOf);

    [Fact] // ADR-0039 / KB-30: ↑ / ↓ move among the enabled items, stepping over disabled ones and wrapping
    public async Task The_arrows_move_among_the_enabled_items_and_wrap()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 150, 10);
        await AltDownAsync(cut);
        var refs = EnabledItemRefs(cut);
        Assert.Equal(["Sort ascending", "Sort descending", "Filter"], refs.Keys);

        await PressAsync(Item(cut, "Sort ascending"), "ArrowDown");
        Assert.Equal(refs["Sort descending"], LastFocused());

        // Past the last enabled item, over the four disabled ones, and round to the first.
        await PressAsync(Item(cut, "Filter"), "ArrowDown");
        Assert.Equal(refs["Sort ascending"], LastFocused());

        await PressAsync(Item(cut, "Sort ascending"), "ArrowUp");
        Assert.Equal(refs["Filter"], LastFocused());
    }

    [Fact] // ADR-0039 / KB-30: Home and End go to the first and last enabled items
    public async Task Home_and_end_go_to_the_first_and_last_enabled_items()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 150, 10);
        await AltDownAsync(cut);
        var refs = EnabledItemRefs(cut);

        await PressAsync(Item(cut, "Sort descending"), "End");
        Assert.Equal(refs["Filter"], LastFocused());

        await PressAsync(Item(cut, "Filter"), "Home");
        Assert.Equal(refs["Sort ascending"], LastFocused());
    }

    [Theory] // ADR-0039 / KB-30: Enter and Space run the item, close the menu and hand the keyboard back
    [InlineData("Enter")]
    [InlineData(" ")]
    public async Task Enter_and_space_run_the_item_and_close(string key)
    {
        var source = new TestSource();
        var cut = RenderGrid(source: source);
        var root = RootRef(cut);
        await ClickCellAsync(cut, 150, 10);
        await AltDownAsync(cut);

        await PressAsync(Item(cut, "Sort descending"), key);

        Assert.Equal([new SortSpec("Amount", SortDirection.Descending)], source.Sorts);
        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Equal(root, LastFocused());
    }

    [Theory] // ADR-0039 / KB-30: Tab and Shift+Tab close the menu as a Cancel — nothing runs
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tab_closes_the_menu_as_a_cancel(bool shift)
    {
        var source = new TestSource();
        var cut = RenderGrid(source: source);
        var root = RootRef(cut);
        await ClickCellAsync(cut, 150, 10);
        await AltDownAsync(cut);

        await PressAsync(Item(cut, "Sort descending"), "Tab", shift);

        Assert.Empty(source.SortChanges);
        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Equal(root, LastFocused());
    }

    [Fact] // ADR-0039 / KB-30: the Context Menu answers the same keys
    public async Task The_context_menu_answers_the_same_keys()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);
        await KeyAsync(cut, "F10", shift: true);
        var refs = EnabledItemRefs(cut);
        var labels = refs.Keys.ToArray();
        Assert.True(labels.Length >= 2);

        await PressAsync(Item(cut, labels[0]), "ArrowDown");
        Assert.Equal(refs[labels[1]], LastFocused());

        await PressAsync(Item(cut, labels[1]), "End");
        Assert.Equal(refs[labels[^1]], LastFocused());
    }

    [Fact] // ADR-0039 / KB-29: moving straight from one column's menu to another's still hands over the keyboard
    public async Task A_menu_opened_over_another_takes_the_keyboard()
    {
        var cut = RenderGrid();
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        var asked = Focused().Length;

        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal("Amount", cut.Find(".ex-popover[role=menu]").GetAttribute("aria-label"));
        Assert.Equal(asked + 1, Focused().Length);
        Assert.Equal(RefOf(Item(cut, "Sort ascending")), LastFocused());
    }

    /// <summary>The panel of one column, opened by pointer — and the root's reference,
    /// read before anything re-rendered it.</summary>
    private async Task<(IRenderedComponent<ExGrid<TestRow>> Cut, string Root)> OpenPanelAsync(
        int column, TestSource? source = null)
    {
        var cut = RenderGrid(source: source);
        var root = RootRef(cut);
        await cut.FindAll(".ex-menu-button")[column].ClickAsync(new MouseEventArgs());
        await Item(cut, "Filter").ClickAsync(new MouseEventArgs());
        return (cut, root);
    }

    // Enter in a value field is the browser's implicit submission of the panel's form —
    // which bUnit cannot perform; what is asserted here is that submitting the form is
    // the OK, and layer 3 presses the real key.

    [Fact] // ADR-0039 / KB-31: submitting the condition form applies exactly what OK applies
    public async Task Submitting_the_condition_applies_what_ok_applies()
    {
        var byOk = new TestSource();
        var (okCut, _) = await OpenPanelAsync(1, byOk);
        await okCut.Find(".ex-popover[role=dialog] input:not([type])").InputAsync(new ChangeEventArgs { Value = "5" });
        await okCut.FindAll(".ex-popover[role=dialog] .ex-popover-actions button")
            .Single(b => b.TextContent == "OK").ClickAsync(new MouseEventArgs());

        var byEnter = new TestSource();
        var (cut, root) = await OpenPanelAsync(1, byEnter);
        await cut.Find(".ex-popover[role=dialog] input:not([type])").InputAsync(new ChangeEventArgs { Value = "5" });
        await cut.Find(".ex-popover[role=dialog] form").SubmitAsync();

        var expected = Assert.Single(byOk.Filter!.Columns);
        var actual = Assert.Single(byEnter.Filter!.Columns);
        Assert.Equal(expected.Key, actual.Key);
        Assert.Equal(expected.Value.Clauses, actual.Value.Clauses);
        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Equal(root, LastFocused());
    }

    [Fact] // ADR-0039 / KB-31: submitting the value list's form is its OK
    public async Task Submitting_the_value_list_applies_it()
    {
        var source = new TestSource { DistinctAnswer = DistinctValues.Of(["Alpha", "Beta", "Gamma"]) };
        var (cut, root) = await OpenPanelAsync(0, source);
        // Untick one value, so what is applied is not the everything-chosen no-op.
        await cut.FindAll(".ex-popover[role=dialog] input[type=checkbox]")[0]
            .ChangeAsync(new ChangeEventArgs { Value = false });

        await cut.Find(".ex-popover[role=dialog] form").SubmitAsync();

        Assert.NotNull(source.Filter);
        Assert.True(source.Filter!.Columns.ContainsKey("Book"));
        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Equal(root, LastFocused());
    }

    [Theory] // ADR-0039 / KB-31: the panel holds exactly one text field and no submit button, so Enter in it is implicit submission
    [InlineData(0)]
    [InlineData(1)]
    public async Task The_panel_form_is_one_text_field_and_no_submit_button(int column)
    {
        var source = new TestSource { DistinctAnswer = DistinctValues.Of(["Alpha", "Beta"]) };
        var (cut, _) = await OpenPanelAsync(column, source);

        var form = cut.Find(".ex-popover[role=dialog] form");
        // The fields that block implicit submission, by the HTML standard's list; a
        // second one would silently turn Enter into nothing.
        var blocking = form.QuerySelectorAll("input").Where(i =>
            (i.GetAttribute("type") ?? "text") is "text" or "search" or "number" or "date" or "email"
                or "url" or "tel" or "password" or "datetime-local" or "month" or "time" or "week");
        Assert.Single(blocking);
        Assert.Empty(form.QuerySelectorAll("button:not([type=button]), input[type=submit]"));
    }

    [Fact] // ADR-0039 / KB-31: Tab wraps inside the panel — off the last control to the first, and back
    public async Task Tab_wraps_inside_the_panel()
    {
        var (cut, _) = await OpenPanelAsync(1);
        var operatorRef = RefOf(cut.Find(".ex-popover[role=dialog] select"));
        var clearRef = RefOf(cut.FindAll(".ex-popover[role=dialog] .ex-popover-actions button")
            .Single(b => b.TextContent == "Clear"));
        var sentinels = cut.FindAll(".ex-popover[role=dialog] .ex-focus-wrap");
        Assert.Equal(2, sentinels.Count);

        // Tab off Clear lands on the far sentinel, which passes focus to the first control.
        await sentinels[1].FocusAsync(new FocusEventArgs());
        Assert.Equal(operatorRef, LastFocused());

        // Shift+Tab off the first control lands on the near one, which passes it to Clear.
        await cut.FindAll(".ex-popover[role=dialog] .ex-focus-wrap")[0].FocusAsync(new FocusEventArgs());
        Assert.Equal(clearRef, LastFocused());
    }

    // ---- A popover stays inside its grid's box (ADR-0040), and a popup inside it keeps
    // its Escape (ADR-0039's corrected row). The grid is 120px tall, the band 20px.

    private static double Px(string style, string property)
    {
        var at = style.IndexOf(property + ":", StringComparison.Ordinal);
        Assert.True(at >= 0, $"no {property} in '{style}'");
        var value = style[(at + property.Length + 1)..].TrimStart();
        return double.Parse(value[..value.IndexOf("px", StringComparison.Ordinal)], System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact] // ADR-0040 / UX-11: a column's popover is bounded by the grid's box — its top to the Viewport's bottom
    public async Task A_column_popover_is_bounded_by_the_grids_box()
    {
        var cut = RenderGrid();

        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        var menu = cut.Find(".ex-popover").GetAttribute("style")!;
        Assert.Equal(120, Px(menu, "top") + Px(menu, "max-height"));

        await Item(cut, "Filter").ClickAsync(new MouseEventArgs());
        var panel = cut.Find(".ex-popover").GetAttribute("style")!;
        Assert.Equal(120, Px(panel, "top") + Px(panel, "max-height"));
    }

    [Fact] // ADR-0040: the Context Menu opens on the side of the pointer with more room, bounded by it
    public async Task The_context_menu_opens_where_there_is_more_room()
    {
        var cut = RenderGrid();

        // Row 0, near the top: below, bounded by the rest of the grid.
        await cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs { Button = 2, OffsetX = 50, OffsetY = 10 });
        var high = cut.Find(".ex-popover[role=menu]").GetAttribute("style")!;
        Assert.DoesNotContain("translateY", high);
        Assert.Equal(120, Px(high, "top") + Px(high, "max-height"));
        await KeyAsync(cut, "Escape", fromDescendant: true);

        // Row 2, near the bottom: above, hanging from the pointer, bounded by the room above.
        await cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs { Button = 2, OffsetX = 50, OffsetY = 55 });
        var low = cut.Find(".ex-popover[role=menu]").GetAttribute("style")!;
        Assert.Contains("translateY(-100%)", low);
        Assert.Equal(Px(low, "top"), Px(low, "max-height"));
    }

    [Fact] // ADR-0039: a popup the contents report is handed to the key gate, and forgotten when the popover closes
    public async Task A_reported_inner_popup_reaches_the_key_gate_and_goes_with_the_popover()
    {
        var chrome = new StubChrome();
        var cut = RenderGrid(chrome);
        await ClickCellAsync(cut, 150, 10);
        await AltDownAsync(cut);
        await cut.InvokeAsync(() => chrome.Menu!.Commands.Single(c => c.Id == "filter").Invoke());
        cut.Render();

        await cut.InvokeAsync(() => chrome.Panel!.InnerPopupChanged!(true));
        Assert.Equal([true], Js.InnerPopupTold.Invocations.Select(i => (bool)i.Arguments[0]!));

        // Reported twice, told once.
        await cut.InvokeAsync(() => chrome.Panel!.InnerPopupChanged!(true));
        Assert.Single(Js.InnerPopupTold.Invocations);

        // Closed with the popover — told the popup is gone too.
        await cut.InvokeAsync(() => chrome.Panel!.Close());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-popover")));
        Assert.Equal([true, false], Js.InnerPopupTold.Invocations.Select(i => (bool)i.Arguments[0]!));
    }

    [Fact] // ADR-0039: a report from contents already closed changes nothing
    public async Task A_late_report_from_closed_contents_is_ignored()
    {
        var chrome = new StubChrome();
        var cut = RenderGrid(chrome);
        await ClickCellAsync(cut, 150, 10);
        await AltDownAsync(cut);
        var stale = chrome.Menu!.InnerPopupChanged!;
        await KeyAsync(cut, "Escape", fromDescendant: true);

        await cut.InvokeAsync(() => stale(true));

        Assert.Empty(Js.InnerPopupTold.Invocations);
    }

    // ---- Review round (2026-09-24): an opening starts clean, whatever the last one left.

    /// <summary>The Filter command, run while the Consumer's value-list query is still
    /// running: in a browser the event's handler renders the panel and waits on; bUnit would
    /// wait for the whole handler, so the click is started and the panel waited for.</summary>
    private static void OpenPanelWhileTheListIsPending(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        _ = Item(cut, "Filter").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".ex-popover[role=dialog]")));
    }

    [Fact] // ADR-0039 / KB-29: a menu opened while a panel's value list is still on its way takes the keyboard
    public async Task A_menu_opened_over_a_waiting_panel_takes_the_keyboard()
    {
        var source = new TestSource { DistinctPending = new() };
        var cut = RenderGrid(source: source);
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        OpenPanelWhileTheListIsPending(cut);

        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal("Amount", cut.Find(".ex-popover[role=menu]").GetAttribute("aria-label"));
        Assert.Equal(RefOf(Item(cut, "Sort ascending")), LastFocused());
        // The answer that arrives for the panel left behind changes nothing.
        await cut.InvokeAsync(() => source.DistinctPending.SetResult(DistinctValues.Of(["Alpha"])));
        Assert.Equal("Amount", cut.Find(".ex-popover[role=menu]").GetAttribute("aria-label"));
    }

    [Fact] // ADR-0009: what the user chose in the condition form while the list was on its way survives TooMany
    public async Task A_choice_made_while_waiting_survives_too_many()
    {
        var source = new TestSource { DistinctPending = new() };
        var cut = RenderGrid(source: source);
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        OpenPanelWhileTheListIsPending(cut);

        await cut.Find(".ex-popover select").ChangeAsync(new ChangeEventArgs { Value = nameof(FilterOperator.StartsWith) });
        await cut.InvokeAsync(() => source.DistinctPending.SetResult(DistinctValues.TooMany));

        Assert.Equal(nameof(FilterOperator.StartsWith), cut.Find(".ex-popover select").GetAttribute("value"));
    }

    [Fact] // ADR-0009: an untouched form still starts over on TooMany's operator
    public async Task An_untouched_form_starts_over_on_too_many()
    {
        var source = new TestSource { DistinctPending = new() };
        var cut = RenderGrid(source: source);
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        OpenPanelWhileTheListIsPending(cut);

        await cut.InvokeAsync(() => source.DistinctPending.SetResult(DistinctValues.TooMany));

        Assert.Equal(nameof(FilterOperator.Contains), cut.Find(".ex-popover select").GetAttribute("value"));
    }

    [Fact] // ADR-0009: a substituted panel pulls the list itself; the core asks nothing for a panel it does not draw
    public async Task The_core_fetches_no_list_for_a_panel_it_does_not_draw()
    {
        var chrome = new StubChrome();
        var source = new TestSource();
        var cut = RenderGrid(chrome, source: source);
        await ClickCellAsync(cut, 50, 10);
        await AltDownAsync(cut);

        await cut.InvokeAsync(() => chrome.Menu!.Commands.Single(c => c.Id == "filter").Invoke());

        Assert.NotNull(cut.Find(".ex-stub-panel"));
        Assert.Empty(source.DistinctRequested);
    }

    [Fact] // ADR-0039: a popup reported by contents that another popover replaced is forgotten with them
    public async Task A_popup_report_does_not_outlive_its_popover()
    {
        var chrome = new StubChrome();
        var cut = RenderGrid(chrome);
        await ClickCellAsync(cut, 150, 10);
        await AltDownAsync(cut);
        var first = chrome.Menu!.InnerPopupChanged!;
        await cut.InvokeAsync(() => first(true));
        Assert.Equal([true], Js.InnerPopupTold.Invocations.Select(i => (bool)i.Arguments[0]!));

        // Another column's menu, straight over this one: the gate is told the popup is gone.
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        Assert.Equal([true, false], Js.InnerPopupTold.Invocations.Select(i => (bool)i.Arguments[0]!));

        // And a late report from the replaced contents changes nothing.
        await cut.InvokeAsync(() => first(true));
        Assert.Equal(2, Js.InnerPopupTold.Invocations.Count);
    }

    [Fact] // ADR-0040 / ADR-0027: across, a popover is clamped by the widest it may grow, written inline
    public async Task A_popover_near_the_right_edge_stays_inside_the_grid()
    {
        var cut = RenderGrid();

        // The second column starts at 100px of a 350px Viewport; a popover up to 320px wide
        // from there would reach 420px.
        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());
        var style = cut.Find(".ex-popover").GetAttribute("style")!;

        Assert.True(Px(style, "left") + Px(style, "max-width") <= 350, style);
        Assert.Equal(200, Px(style, "min-width"));
    }
}
