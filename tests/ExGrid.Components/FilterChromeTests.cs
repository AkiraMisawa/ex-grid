using AngleSharp.Dom;
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
/// The filter panel and the column menu (ADR-0009/0010), stacked in the column's one
/// popover (ADR-0044): the core decides the items and the operators, the panel applies on
/// OK, and a substituted Chrome changes rendering and nothing about behaviour.
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
        TestSource source, IGridChrome? chrome = null, GridColumn<TestRow>[]? columns = null,
        Func<string, string?>? commandLabel = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Source, source)
              .Add(g => g.Columns, columns ?? Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            if (chrome is not null)
                ps.Add(g => g.Chrome, chrome);
            if (commandLabel is not null)
                ps.Add(g => g.CommandLabel, commandLabel);
        });

    private static TestSource PushedSource()
    {
        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        return source;
    }

    private static Task OpenMenuAsync(IRenderedComponent<ExGrid<TestRow>> cut, int column = 0)
        => cut.FindAll(".ex-menu-button")[column].ClickAsync(new MouseEventArgs());

    /// <summary>The filter stands below the commands as the popover opens (ADR-0044).</summary>
    private static Task OpenPanelAsync(IRenderedComponent<ExGrid<TestRow>> cut, int column = 0)
        => OpenMenuAsync(cut, column);

    [Fact] // ADR-0036 / ADR-0044: the core names the commands and does not name them in a language; a letter the label lacks is appended
    public async Task A_consumer_renames_a_command_without_replacing_the_menu()
    {
        var cut = RenderGrid(PushedSource(), commandLabel: id => id switch
        {
            "sort-ascending" => "昇順で並べ替え",
            "hide" => "この列を隠す",
            _ => null,     // the rest fall back to the built-in Chrome's table
        });

        await OpenMenuAsync(cut);

        var labels = cut.FindAll(".ex-popover button[role=menuitem]").Select(b => b.TextContent).ToList();
        Assert.Equal(
            ["昇順で並べ替え(S)", "Sort descending", "Clear filter", "この列を隠す",
             "Pin up to this column", "Unpin all columns", "Size to fit"],
            labels);
    }

    [Fact] // ADR-0010 / ADR-0044 / FN-18: the core decides the menu's commands; Chrome only lays them out
    public async Task The_menu_commands_are_the_cores()
    {
        var cut = RenderGrid(PushedSource());

        await OpenMenuAsync(cut);

        var labels = cut.FindAll(".ex-popover button[role=menuitem]").Select(b => b.TextContent).ToList();
        Assert.Equal(
            ["Sort ascending", "Sort descending", "Clear filter", "Hide this column",
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

    [Fact] // ADR-0009/0023: In chosen in the condition form filters to its one value, instead of a clause the engine refuses
    public async Task In_chosen_as_a_condition_filters_to_its_operand()
    {
        var source = GridSource.From(TestRows.Many(50));
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns(FilterUiMode.Condition))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350));

        await OpenPanelAsync(cut);
        await cut.Find(".ex-popover select").ChangeAsync(new ChangeEventArgs { Value = nameof(FilterOperator.In) });
        await cut.Find(".ex-popover input").InputAsync(new ChangeEventArgs { Value = "Row 000001" });
        await cut.FindAll(".ex-popover-actions button").Single(b => b.TextContent == "OK")
            .ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Single(cut.FindAll(".ex-row"));
        Assert.Equal(["Row 000001"], Assert.Single(source.Filter!.Columns.Values).Clauses[0].Values!);
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

        // The panel stood below the menu from the opening (ADR-0044).
        await OpenMenuAsync(cut);
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

    private static Task OkAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-popover-actions button").Single(b => b.TextContent == "OK").ClickAsync(new MouseEventArgs());

    private static IReadOnlyList<object?> AppliedValues(TestSource source)
        => Assert.Single(source.FilterChanges[^1]!.Columns["Book"].Clauses).Values!;

    private static IElement ValueBox(IRenderedComponent<ExGrid<TestRow>> cut, string label)
        => cut.FindAll(".ex-popover-list label").Single(l => l.TextContent.Trim() == label).QuerySelector("input")!;

    [Fact] // ADR-0009 / FL-10: with a search, OK applies the matching checked values and no hidden one
    public async Task A_search_narrows_what_ok_applies()
    {
        var source = PushedSource();
        source.DistinctAnswer = DistinctValues.Of(["Alpha", "Beta", "Gamma"]);
        var cut = RenderGrid(source);
        await OpenPanelAsync(cut);

        await cut.Find(".ex-popover input[type=search]").InputAsync(new ChangeEventArgs { Value = "alp" });
        await OkAsync(cut);

        Assert.Equal(["Alpha"], AppliedValues(source));
    }

    [Fact] // ADR-0009 / FL-11: (Select All) reads and toggles the values shown, with the mixed state
    public async Task Select_all_reads_and_toggles_the_values_shown()
    {
        var source = PushedSource();
        source.DistinctAnswer = DistinctValues.Of(["Alpha", "Beta", "Gamma"]);
        var cut = RenderGrid(source);
        await OpenPanelAsync(cut);
        IElement All() => cut.Find(".ex-popover .ex-select-all");

        Assert.Equal("true", All().GetAttribute("aria-checked"));
        Assert.Equal("(Select All)", All().TextContent);
        await ValueBox(cut, "Beta").ChangeAsync(new ChangeEventArgs { Value = false });
        Assert.Equal("mixed", All().GetAttribute("aria-checked"));

        await All().ClickAsync(new MouseEventArgs()); // mixed → everything shown checked
        Assert.Equal("true", All().GetAttribute("aria-checked"));
        await All().ClickAsync(new MouseEventArgs()); // all → none
        Assert.Equal("false", All().GetAttribute("aria-checked"));

        await cut.Find(".ex-popover input[type=search]").InputAsync(new ChangeEventArgs { Value = "gam" });
        Assert.Equal("(Select All Search Results)", All().TextContent);
        await All().ClickAsync(new MouseEventArgs());
        await OkAsync(cut);
        Assert.Equal(["Gamma"], AppliedValues(source));
    }

    [Fact] // ADR-0009 / FL-13: while searching a filtered column, the matches can be added to the filter in force
    public async Task Add_current_selection_joins_the_filter_in_force()
    {
        var source = PushedSource();
        source.DistinctAnswer = DistinctValues.Of(["Alpha", "Beta", "Gamma", "Delta"]);
        var cut = RenderGrid(source);

        // First filter: Alpha alone.
        await OpenPanelAsync(cut);
        await cut.Find(".ex-popover input[type=search]").InputAsync(new ChangeEventArgs { Value = "alp" });
        Assert.Empty(cut.FindAll(".ex-popover-add")); // nothing in force yet to add to
        await OkAsync(cut);
        Assert.Equal(["Alpha"], AppliedValues(source));
        source.Raise();

        // Second: search Gamma, check it, and add it.
        await OpenPanelAsync(cut);
        await cut.Find(".ex-popover input[type=search]").InputAsync(new ChangeEventArgs { Value = "gam" });
        await ValueBox(cut, "Gamma").ChangeAsync(new ChangeEventArgs { Value = true });
        await cut.Find(".ex-popover-add input").ChangeAsync(new ChangeEventArgs { Value = true });
        await OkAsync(cut);

        Assert.Equal(["Alpha", "Gamma"], AppliedValues(source));
    }

    [Fact] // ADR-0009 / FL-13: left off, a search replaces the filter in force, as Excel does by default
    public async Task Without_add_a_search_replaces_the_filter()
    {
        var source = PushedSource();
        source.DistinctAnswer = DistinctValues.Of(["Alpha", "Beta", "Gamma", "Delta"]);
        var cut = RenderGrid(source);
        await OpenPanelAsync(cut);
        await ValueBox(cut, "Beta").ChangeAsync(new ChangeEventArgs { Value = false });
        await ValueBox(cut, "Gamma").ChangeAsync(new ChangeEventArgs { Value = false });
        await ValueBox(cut, "Delta").ChangeAsync(new ChangeEventArgs { Value = false });
        await OkAsync(cut);
        source.Raise();

        await OpenPanelAsync(cut);
        await cut.Find(".ex-popover input[type=search]").InputAsync(new ChangeEventArgs { Value = "gam" });
        await ValueBox(cut, "Gamma").ChangeAsync(new ChangeEventArgs { Value = true });
        await OkAsync(cut);

        Assert.Equal(["Gamma"], AppliedValues(source));
    }

    [Fact] // ADR-0009 / FL-14: two conditions joined by Or apply as one spec of two clauses
    public async Task Two_conditions_apply_as_one_spec()
    {
        var source = PushedSource();
        var cut = RenderGrid(source, columns: Columns(FilterUiMode.Condition));
        await OpenPanelAsync(cut);

        await cut.Find(".ex-popover select").ChangeAsync(new ChangeEventArgs { Value = nameof(FilterOperator.StartsWith) });
        await cut.Find(".ex-popover input").InputAsync(new ChangeEventArgs { Value = "Al" });
        await cut.FindAll(".ex-popover-join input")[1].ChangeAsync(new ChangeEventArgs { Value = "on" });
        await cut.Find(".ex-popover select.ex-popover-second").ChangeAsync(new ChangeEventArgs { Value = nameof(FilterOperator.EndsWith) });
        await cut.Find(".ex-popover input.ex-popover-second").InputAsync(new ChangeEventArgs { Value = "ma" });
        await OkAsync(cut);

        var spec = source.FilterChanges[^1]!.Columns["Book"];
        Assert.Equal(FilterCombinator.Or, spec.Combinator);
        Assert.Equal([FilterOperator.StartsWith, FilterOperator.EndsWith], spec.Clauses.Select(c => c.Operator));
        Assert.Equal(["Al", "ma"], spec.Clauses.Select(c => c.Value));
    }

    // ---- The one popover's own (ADR-0044): Clear filter is a command, and Excel's letters.

    private static IElement Command(IRenderedComponent<ExGrid<TestRow>> cut, string label)
        => cut.FindAll(".ex-popover button[role=menuitem]").Single(b => b.TextContent == label);

    private static Task KeyOnAsync(IElement element, string key)
        => element.KeyDownAsync(new KeyboardEventArgs { Key = key });

    private static TestSource FilteredSource()
    {
        var source = PushedSource();
        source.OnFilterChanged(new GridFilter(new Dictionary<string, FilterSpec>
        {
            ["Book"] = new([new FilterClause(FilterOperator.In, Values: new object?[] { "Alpha" })]),
        }));
        source.DistinctAnswer = DistinctValues.Of(["Alpha", "Beta"]);
        return source;
    }

    [Fact] // ADR-0044 / FL-16: Clear filter is disabled with no filter, and the panel has OK and Cancel only
    public async Task Clear_filter_is_disabled_without_a_filter()
    {
        var cut = RenderGrid(PushedSource());

        await OpenMenuAsync(cut);

        Assert.True(Command(cut, "Clear filter").HasAttribute("disabled"));
        Assert.Equal(["OK", "Cancel"], cut.FindAll(".ex-popover-actions button").Select(b => b.TextContent));
    }

    [Fact] // ADR-0044 / FL-16: running Clear filter removes the column's filter and closes the popover
    public async Task Clear_filter_clears_and_closes()
    {
        var source = FilteredSource();
        var cut = RenderGrid(source);
        await OpenMenuAsync(cut);

        await Command(cut, "Clear filter").ClickAsync(new MouseEventArgs());

        Assert.Null(source.FilterChanges[^1]);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0044 / FL-15: the built-in labels show their letter underlined
    public async Task The_letters_are_underlined()
    {
        var cut = RenderGrid(PushedSource());

        await OpenMenuAsync(cut);

        Assert.Equal("S", Command(cut, "Sort ascending").QuerySelector("u")!.TextContent);
        Assert.Equal("o", Command(cut, "Sort descending").QuerySelector("u")!.TextContent);
        Assert.Equal("C", Command(cut, "Clear filter").QuerySelector("u")!.TextContent);
        Assert.Null(Command(cut, "Size to fit").QuerySelector("u"));
    }

    [Fact] // ADR-0036 / ADR-0044: the Context Menu answers no letter, so marks none — even on a Consumer's command named like a sort
    public async Task The_context_menu_marks_no_letter()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, PushedSource())
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ContextCommands, (ContextMenuContext<TestRow> _) =>
                (IEnumerable<GridCommand>)[new GridCommand("sort-ascending", true, () => Task.CompletedTask)]));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });

        await cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs { Button = 2, OffsetX = 50, OffsetY = 10 });

        Assert.Contains(cut.FindAll(".ex-popover button[role=menuitem]"), b => b.TextContent == "Sort ascending");
        Assert.Empty(cut.FindAll(".ex-popover u"));
    }

    [Theory] // ADR-0044 / FL-15: S and O sort from any command, and close the popover
    [InlineData("s", SortDirection.Ascending)]
    [InlineData("O", SortDirection.Descending)]
    public async Task S_and_o_sort_from_a_command(string key, SortDirection direction)
    {
        var source = PushedSource();
        var cut = RenderGrid(source);
        await OpenMenuAsync(cut);

        await KeyOnAsync(Command(cut, "Sort descending"), key);

        Assert.Equal([new SortSpec("Book", direction)], source.Sorts);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0044 / FL-15: C clears the filter from a command
    public async Task C_clears_from_a_command()
    {
        var source = FilteredSource();
        var cut = RenderGrid(source);
        await OpenMenuAsync(cut);

        await KeyOnAsync(Command(cut, "Sort ascending"), "c");

        Assert.Null(source.FilterChanges[^1]);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0044 / FL-15: the letters act on the value list too — S sorts from a checkbox
    public async Task The_letters_act_on_the_value_list()
    {
        var source = FilteredSource();
        var cut = RenderGrid(source);
        await OpenMenuAsync(cut);

        await KeyOnAsync(cut.Find(".ex-popover-list"), "s");

        Assert.Equal([new SortSpec("Book", SortDirection.Ascending)], source.Sorts);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0044 / FL-15: E moves the keyboard to the search box, from a command and from the value list
    public async Task E_moves_to_the_search_box()
    {
        var cut = RenderGrid(FilteredSource());
        await OpenMenuAsync(cut);
        var search = cut.Find(".ex-popover input[type=search]").GetAttribute("blazor:elementreference");

        await KeyOnAsync(Command(cut, "Sort ascending"), "e");
        Assert.Equal(search, LastFocusedId());

        await KeyOnAsync(cut.Find(".ex-popover-list"), "E");
        Assert.Equal(2, JSInterop.Invocations.Count(i => i.Identifier == "Blazor._internal.domWrapper.focus"
            && ((ElementReference)i.Arguments[0]!).Id == search));
    }

    [Fact] // ADR-0044 / FL-15: a substituted panel hands its value list's keys to the core, which answers the letters
    public async Task A_substituted_panel_hands_its_letters_to_the_core()
    {
        var source = FilteredSource();
        var chrome = new StubChrome();
        var cut = RenderGrid(source, chrome);
        await OpenMenuAsync(cut);
        Assert.Equal(0, chrome.Panel!.FocusRequest);

        await cut.InvokeAsync(() => chrome.Panel!.ValueListKey!(new KeyboardEventArgs { Key = "e" }));
        cut.WaitForAssertion(() => Assert.Equal(1, chrome.Panel!.FocusRequest));

        await cut.InvokeAsync(() => chrome.Menu!.ResolveKey!("Tab", true, false));
        cut.WaitForAssertion(() => Assert.Equal(1, chrome.Panel!.FocusLastRequest));

        await cut.InvokeAsync(() => chrome.Panel!.ValueListKey!(new KeyboardEventArgs { Key = "o" }));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-popover")));
        Assert.Equal([new SortSpec("Book", SortDirection.Descending)], source.Sorts);
    }

    private string? LastFocusedId()
        => JSInterop.Invocations.Where(i => i.Identifier == "Blazor._internal.domWrapper.focus")
            .Select(i => ((ElementReference)i.Arguments[0]!).Id).LastOrDefault();
}
