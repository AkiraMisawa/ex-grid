using Bunit;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The filter panel and the column menu (ADR-0009/0010): the core decides the items
/// and the operators, the panel applies on OK, and a substituted Chrome changes
/// rendering and nothing about behaviour.
/// </summary>
public class FilterChromeTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns(FilterUiMode mode = FilterUiMode.Both) =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, filterUi: mode),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary>A Chrome whose seams record what they were handed and render markers.</summary>
    private sealed class StubChrome : IGridChrome
    {
        public FilterPanelContext? Panel { get; private set; }

        public ColumnMenuContext? Menu { get; private set; }

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

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        TestSource source, IGridChrome? chrome = null, GridColumn<TestRow>[]? columns = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Source, source)
              .Add(g => g.Columns, columns ?? Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            if (chrome is not null)
                ps.Add(g => g.Chrome, chrome);
        });

    private static TestSource PushedSource()
    {
        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        return source;
    }

    private static Task OpenMenuAsync(IRenderedComponent<ExGrid<TestRow>> cut, int column = 0)
        => cut.FindAll(".ex-menu-button")[column].ClickAsync(new MouseEventArgs());

    private static async Task OpenPanelAsync(IRenderedComponent<ExGrid<TestRow>> cut, int column = 0)
    {
        await OpenMenuAsync(cut, column);
        await cut.FindAll(".ex-popover button[role=menuitem]")
            .Single(b => b.TextContent == "Filter").ClickAsync(new MouseEventArgs());
    }

    [Fact] // ADR-0010 / FN-18: the core decides the menu's commands; Chrome only lays them out
    public async Task The_menu_commands_are_the_cores()
    {
        var cut = RenderGrid(PushedSource());

        await OpenMenuAsync(cut);

        var labels = cut.FindAll(".ex-popover button[role=menuitem]").Select(b => b.TextContent).ToList();
        Assert.Equal(
            ["Sort ascending", "Sort descending", "Filter", "Hide this column",
             "Pin up to this column", "Unpin all columns", "Size to fit"],
            labels);
        // Commands whose sink nobody wired are disabled, not absent.
        Assert.True(cut.FindAll(".ex-popover button[role=menuitem]")
            .Single(b => b.TextContent == "Hide this column").HasAttribute("disabled"));
    }

    [Fact] // ADR-0010: the menu's sort commands travel the same path as a header click
    public async Task The_sort_commands_sort_the_source()
    {
        var source = PushedSource();
        var cut = RenderGrid(source);
        await OpenMenuAsync(cut);

        await cut.FindAll(".ex-popover button[role=menuitem]")
            .Single(b => b.TextContent == "Sort descending").ClickAsync(new MouseEventArgs());

        Assert.Equal([new SortSpec("Book", SortDirection.Descending)], source.Sorts);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0009 / FL-4: one fetch per opening; toggling applies nothing until OK
    public async Task The_panel_fetches_once_and_applies_on_ok()
    {
        var source = PushedSource();
        source.DistinctAnswer = DistinctValues.Of(["Alpha", "Beta", "Gamma"]);
        var cut = RenderGrid(source);

        await OpenPanelAsync(cut);
        Assert.Equal(["Book"], source.DistinctRequested);

        // Untick Beta: no notification yet, and no second fetch.
        var beta = cut.FindAll(".ex-popover-list input[type=checkbox]")[1];
        await beta.ChangeAsync(new ChangeEventArgs { Value = false });
        Assert.Empty(source.FilterChanges);
        Assert.Equal(["Book"], source.DistinctRequested);

        await cut.FindAll(".ex-popover-actions button").Single(b => b.TextContent == "OK")
            .ClickAsync(new MouseEventArgs());

        var applied = Assert.Single(source.FilterChanges);
        var spec = applied!.Columns["Book"];
        var clause = Assert.Single(spec.Clauses);
        Assert.Equal(FilterOperator.In, clause.Operator);
        Assert.Equal(["Alpha", "Gamma"], clause.Values);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0009 / FL-3: TooMany degrades to the search-shaped condition, without breaking
    public async Task Too_many_degrades_to_a_condition_with_search()
    {
        var source = PushedSource();
        source.DistinctAnswer = DistinctValues.TooMany;
        var cut = RenderGrid(source);

        await OpenPanelAsync(cut);

        Assert.Empty(cut.FindAll(".ex-popover-list"));
        Assert.NotNull(cut.Find(".ex-popover select"));
        // The degraded form is Excel's search: Contains, ready to type into.
        Assert.Equal(
            FilterOperator.Contains.ToString(),
            cut.Find(".ex-popover select").GetAttribute("value"));

        await cut.Find(".ex-popover input").InputAsync(new ChangeEventArgs { Value = "Alp" });
        await cut.FindAll(".ex-popover-actions button").Single(b => b.TextContent == "OK")
            .ClickAsync(new MouseEventArgs());

        var applied = Assert.Single(source.FilterChanges);
        var clause = Assert.Single(applied!.Columns["Book"].Clauses);
        Assert.Equal(FilterOperator.Contains, clause.Operator);
        Assert.Equal("Alp", clause.Value);
    }

    [Fact] // ADR-0002 / FL-7: an Opaque Filter passes straight through, with no UI for it
    public async Task An_opaque_filter_survives_a_panel_apply()
    {
        var source = PushedSource();
        var opaque = new object();
        source.OnFilterChanged(new GridFilter(new Dictionary<string, FilterSpec>(), opaque));
        source.DistinctAnswer = DistinctValues.TooMany;
        var cut = RenderGrid(source);
        await OpenPanelAsync(cut);

        await cut.Find(".ex-popover input").InputAsync(new ChangeEventArgs { Value = "x" });
        await cut.FindAll(".ex-popover-actions button").Single(b => b.TextContent == "OK")
            .ClickAsync(new MouseEventArgs());

        Assert.Same(opaque, source.FilterChanges[^1]!.Opaque);
    }

    [Fact] // ADR-0009/0011 / FL-8: applying a filter clears the Selection
    public async Task Applying_a_filter_clears_the_selection()
    {
        var source = GridSource.From(TestRows.Many(50));
        GridSelection? selection = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns(FilterUiMode.Condition))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });
        Assert.False(selection!.IsEmpty);

        await OpenPanelAsync(cut);
        await cut.Find(".ex-popover input").InputAsync(new ChangeEventArgs { Value = "Row 000001" });
        await cut.FindAll(".ex-popover-actions button").Single(b => b.TextContent == "OK")
            .ClickAsync(new MouseEventArgs());

        Assert.True(selection!.IsEmpty);
        Assert.Single(cut.FindAll(".ex-row"));
    }

    [Fact] // FN-17: swapping Chrome changes rendering and nothing about behaviour
    public async Task A_stub_chrome_renders_but_the_core_still_decides()
    {
        var source = PushedSource();
        source.DistinctAnswer = DistinctValues.Of(["Alpha"]);
        var chrome = new StubChrome();
        var cut = RenderGrid(source, chrome);

        await OpenMenuAsync(cut);
        Assert.NotNull(cut.Find(".ex-stub-menu"));
        Assert.NotNull(chrome.Menu);
        // FN-18: the commands are produced by the core; Chrome has no way to add one.
        Assert.Equal(7, chrome.Menu!.Commands.Count);

        // Behaviour travels through the context, not through the markup: invoking the
        // core's command works exactly as the built-in button does.
        await cut.InvokeAsync(() => chrome.Menu.Commands
            .Single(c => c.Id == "sort-ascending").Invoke());
        Assert.Equal([new SortSpec("Book", SortDirection.Ascending)], source.Sorts);

        await cut.InvokeAsync(() => chrome.Menu.Commands.Single(c => c.Id == "filter").Invoke());
        cut.Render();
        Assert.NotNull(cut.Find(".ex-stub-panel"));
        Assert.NotNull(chrome.Panel);
        // The allowed operators are the core's decision (ADR-0009).
        Assert.Equal(FilterOperators.AllowedFor(ColumnType.Text), chrome.Panel!.Allowed);

        // And the panel's Apply is the same write path.
        await cut.InvokeAsync(() => chrome.Panel.Apply(
            new FilterSpec([new FilterClause(FilterOperator.Equals, "Alpha")])));
        Assert.Equal(FilterOperator.Equals, Assert.Single(source.Filter!.Columns["Book"].Clauses).Operator);
    }

    [Fact] // A popover the user cannot dismiss is a popover in the way: the button toggles
    public async Task The_menu_button_toggles_its_menu()
    {
        var cut = RenderGrid(PushedSource());

        await OpenMenuAsync(cut);
        Assert.Single(cut.FindAll(".ex-popover"));

        await OpenMenuAsync(cut);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // Escape peels the outermost layer: the popover closes and the grid keeps the keyboard
    public async Task Escape_closes_the_popover_before_leaving_the_grid()
    {
        var cut = RenderGrid(PushedSource());
        await OpenMenuAsync(cut);

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Escape", false, false, false, false, false));

        Assert.Empty(cut.FindAll(".ex-popover"));
        // The grid was not blurred: Escape's Leave only fires with nothing to dismiss.
        Assert.False(Js.BlurCount > 0);
    }

    [Fact] // Escape works from inside the popover too: the capture listener forwards a
           // descendant's Escape (ADR-0012/0020), and the grid takes the keyboard back
    public async Task Escape_on_the_popover_itself_closes_it()
    {
        var cut = RenderGrid(PushedSource());
        await OpenMenuAsync(cut);

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            "Escape", ctrl: false, shift: false, alt: false, meta: false, metaIsPrimary: false,
            fromDescendant: true));

        Assert.Empty(cut.FindAll(".ex-popover"));
        // Reclaimed, not blurred: the way out of the popover leads back to the grid.
        Assert.False(Js.BlurCount > 0);
    }

    [Fact] // Clicking past an open popover dismisses it, as menus close everywhere else
    public async Task A_click_in_the_body_closes_the_popover()
    {
        var cut = RenderGrid(PushedSource());
        await OpenMenuAsync(cut);

        await cut.Find(".ex-viewport").MouseDownAsync(
            new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });

        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0010: no sink, no menu button — the header stays exactly what it was
    public void Menu_buttons_render_only_with_a_sink()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns()));

        Assert.Empty(cut.FindAll(".ex-menu-button"));
    }


    [Fact] // OK means what the panel shows: a domain shifted by another column's filter
           // must never turn OK into "remove the filter" — membership, not a count
    public async Task Ok_keeps_the_filter_when_the_domain_shrank_under_it()
    {
        var source = PushedSource();
        // Applied earlier: In [Alpha, Beta, Gamma]. Another column's filter has since
        // narrowed this column's own domain to [Alpha, Delta].
        source.OnFilterChanged(new GridFilter(new Dictionary<string, FilterSpec>
        {
            ["Book"] = new([new FilterClause(
                FilterOperator.In, Values: new object?[] { "Alpha", "Beta", "Gamma" })]),
        }));
        source.DistinctAnswer = DistinctValues.Of(["Alpha", "Delta"]);
        var cut = RenderGrid(source);
        await OpenPanelAsync(cut);

        await cut.FindAll(".ex-popover-actions button").Single(b => b.TextContent == "OK")
            .ClickAsync(new MouseEventArgs());

        // The user saw Alpha checked and Delta unchecked; OK applies exactly that —
        // the old count comparison (3 checked >= 2 listed) silently removed the filter.
        var applied = source.FilterChanges[^1];
        var clause = Assert.Single(applied!.Columns["Book"].Clauses);
        Assert.Equal(FilterOperator.In, clause.Operator);
        Assert.Equal(["Alpha"], clause.Values);
    }

    [Fact] // ADR-0027: the popover's min-width arrives inline — the same number the clamp uses
    public async Task The_popover_min_width_arrives_inline()
    {
        var cut = RenderGrid(PushedSource());
        await OpenMenuAsync(cut);

        Assert.Contains("min-width: 200px", cut.Find(".ex-popover").GetAttribute("style"));
    }
}
