using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Extensions;
using Xunit;
using FilterOperator = ExGrid.FilterOperator;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// The filter panel under MudBlazor (WR-1/2/3, ADR-0009/0030/0039): MudBlazor's controls,
/// the whole FilterPanelContext, and — the promise that matters — the same choices making
/// the same FilterSpec as the built-in panel, because both ask FilterPanelChoices.
/// </summary>
public class MudFilterPanelTests : MudTestContext
{
    private readonly List<FilterSpec?> _applied = [];
    private readonly List<bool> _popups = [];
    private readonly List<string> _listKeys = [];
    private int _closed;
    private int _cleared;

    private FilterPanelContext Context(
        ColumnType type = ColumnType.Text, FilterUiMode mode = FilterUiMode.ValueList,
        DistinctValues? answer = null, FilterSpec? current = null, int focusRequest = 1, int focusLastRequest = 0)
        => new(
            "Book", type, current, FilterOperators.AllowedFor(type), mode,
            () => Task.FromResult(answer ?? DistinctValues.Of(["Alpha", "Beta", null, "Gamma"])),
            spec => _applied.Add(spec), () => _cleared++, () => _closed++, focusRequest,
            InnerPopupChanged: _popups.Add, FocusLastRequest: focusLastRequest,
            ValueListKey: e => _listKeys.Add(e.Key));

    // MudBlazor's selects and pickers draw their popups into the page's provider — every
    // MudBlazor app has one — so the tests render one too.
    private IRenderedComponent<MudPopoverProvider>? _provider;

    private IRenderedComponent<ContainerFragment> RenderPanel(FilterPanelContext context, MudGridChrome? chrome = null)
    {
        _provider ??= Render<MudPopoverProvider>();
        return Render((chrome ?? MudGridChrome.Default).FilterPanel(context)!);
    }

    private static IReadOnlyList<IElement> ValueBoxes(IRenderedComponent<ContainerFragment> cut)
        => cut.FindAll(".mud-ex-grid-filter-value input[type=checkbox]");

    private static IReadOnlyList<string> ValueLabels(IRenderedComponent<ContainerFragment> cut)
        => [.. cut.FindAll(".mud-ex-grid-filter-value").Select(v => v.TextContent.Trim())];

    private static IElement Button(IRenderedComponent<ContainerFragment> cut, string cssClass)
        => cut.Find($".{cssClass}");

    [Fact] // WR-1: a value list of MudCheckBoxes with a search field and a Blank entry, every value chosen at first
    public void The_value_list_is_mud_checkboxes_with_a_search_and_a_blank_entry()
    {
        var cut = RenderPanel(Context());

        Assert.NotNull(cut.Find(".mud-ex-grid-filter-search input"));
        Assert.Equal(["Alpha", "Beta", "(Blanks)", "Gamma"], ValueLabels(cut));
        Assert.All(ValueBoxes(cut), box => Assert.True(box.IsChecked()));
        Assert.Equal(4, cut.FindComponents<MudCheckBox<bool>>().Count);
    }

    [Fact] // WR-1 / ADR-0009: what the choices mean is FilterPanelChoices' — a part chosen is one In clause
    public async Task Applying_a_part_of_the_list_is_one_in_clause()
    {
        var cut = RenderPanel(Context());

        await ValueBoxes(cut)[1].ChangeAsync(new ChangeEventArgs { Value = false });
        await cut.Find("form").SubmitAsync();

        var spec = Assert.Single(_applied);
        var clause = Assert.Single(spec!.Clauses);
        Assert.Equal(FilterOperator.In, clause.Operator);
        Assert.Equal(["Alpha", null, "Gamma"], clause.Values!);
    }

    [Fact] // WR-1 / ADR-0009: everything chosen applies no filter at all
    public async Task Applying_everything_is_no_filter()
    {
        var cut = RenderPanel(Context());

        await cut.Find("form").SubmitAsync();

        Assert.Null(Assert.Single(_applied));
    }

    [Fact] // WR-1 / ADR-0009: the applied list is what starts chosen
    public void The_applied_list_starts_chosen()
    {
        var cut = RenderPanel(Context(current: new FilterSpec([new FilterClause(FilterOperator.In, Values: ["Beta", null])])));

        Assert.Equal([false, true, true, false], ValueBoxes(cut).Select(b => b.IsChecked()));
    }

    [Fact] // WR-1: the search narrows what is shown, and choosing stays what it was for the rest
    public async Task The_search_narrows_what_is_shown()
    {
        var cut = RenderPanel(Context());

        await cut.Find(".mud-ex-grid-filter-search input").InputAsync(new ChangeEventArgs { Value = "ph" });

        Assert.Equal(["Alpha"], ValueLabels(cut));
    }

    [Fact] // WR-1 / ADR-0044 / FL-16: Cancel closes as a discard, and the panel draws no Clear — clearing is the command above it
    public async Task Cancel_is_the_cores_exit_and_there_is_no_clear()
    {
        var cut = RenderPanel(Context());

        await Button(cut, "mud-ex-grid-filter-cancel").ClickAsync(new MouseEventArgs());

        Assert.Equal(1, _closed);
        Assert.Equal(0, _cleared);
        Assert.Empty(_applied);
        Assert.Empty(cut.FindAll(".mud-ex-grid-filter-clear"));
        Assert.Equal(2, cut.FindAll(".mud-ex-grid-filter-actions button").Count);
    }

    [Fact] // WR-3: MudBlazor's words where it has keys; the Chrome's own for the rest; Blank never "empty"
    public void The_wording_is_mudblazors_where_keyed_and_the_chromes_elsewhere()
    {
        Services.AddSingleton<ILocalizationInterceptor>(new FrenchInterceptor());
        var chrome = new MudGridChrome
        {
            Label = id => id switch
            {
                MudExGridWords.Search => "Rechercher",
                MudExGridWords.BlankValue => "(Vides)",
                _ => null,
            },
        };

        var cut = RenderPanel(Context(), chrome);

        Assert.Equal("Appliquer", Button(cut, "mud-ex-grid-filter-apply").TextContent.Trim());
        Assert.Equal("Annuler", Button(cut, "mud-ex-grid-filter-cancel").TextContent.Trim());
        Assert.Contains("Rechercher", cut.Find(".mud-ex-grid-filter-search").TextContent);
        Assert.Contains("(Vides)", ValueLabels(cut));
    }

    [Fact] // WR-3 / CONTEXT.md Blank: the English defaults never say "empty"
    public void The_blank_words_never_say_empty()
    {
        var cut = RenderPanel(Context());

        Assert.Contains("(Blanks)", ValueLabels(cut));
        Assert.DoesNotContain(ValueLabels(cut), l => l.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The operator select's items, as its popup lists them once opened —
    /// drawn by MudBlazor's provider, outside the panel: an Inner Popup (ADR-0039).</summary>
    private async Task<IReadOnlyList<string>> OperatorItemsAsync(FilterPanelContext context)
    {
        var cut = RenderPanel(context);
        await cut.FindComponent<MudSelect<FilterOperator>>().InvokeAsync(
            () => cut.FindComponent<MudSelect<FilterOperator>>().Instance.OpenMenu());
        return [.. _provider!.FindAll(".mud-list-item").Select(i => i.TextContent.Trim())];
    }

    [Fact] // WR-2 / WR-3: the operator offers exactly Allowed, in the core's order, in MudBlazor's words and the Chrome's
    public async Task The_operator_offers_exactly_what_the_core_allows()
    {
        var text = await OperatorItemsAsync(Context(ColumnType.Text, FilterUiMode.Condition));
        Assert.Equal(
            ["equals", "not equals", "contains", "not contains", "starts with", "ends with", "is one of", "is blank", "is not blank"],
            text.Select(t => t.ToLowerInvariant()));
        Assert.DoesNotContain(text, t => t.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact] // ADR-0039: the operator's list is an Inner Popup, reported as it opens and closes
    public async Task The_operator_list_is_reported_as_an_inner_popup()
    {
        var cut = RenderPanel(Context(ColumnType.Text, FilterUiMode.Condition));
        var select = cut.FindComponent<MudSelect<FilterOperator>>();

        await select.InvokeAsync(() => select.Instance.OpenMenu());
        Assert.Equal([true], _popups);

        await select.InvokeAsync(() => select.Instance.CloseMenu());
        Assert.Equal([true, false], _popups);
    }

    [Fact] // WR-2 / WR-3: a number's operators are MudBlazor's symbols, and still exactly Allowed
    public async Task A_numbers_operators_are_mudblazors_symbols()
    {
        var number = await OperatorItemsAsync(Context(ColumnType.Number, FilterUiMode.Condition));
        Assert.Equal(FilterOperators.AllowedFor(ColumnType.Number).Count, number.Count);
        // MudBlazor's own words for them, as its localiser answers — not the grid's.
        var mud = Services.GetRequiredService<ILocalizationInterceptor>();
        Assert.Equal(
            new[] { "EqualSign", "NotEqualSign", "GreaterThanSign", "GreaterThanOrEqualSign", "LessThanSign", "LessThanOrEqualSign" }
                .Select(k => mud.Handle("MudDataGrid_" + k).Value),
            number.Take(6));
    }

    private sealed class FrenchInterceptor : ILocalizationInterceptor
    {
        public LocalizedString Handle(string key, params object[] arguments) => key switch
        {
            "MudDataGrid_Apply" => new(key, "Appliquer"),
            "MudDataGrid_Cancel" => new(key, "Annuler"),
            _ => new(key, key, resourceNotFound: true),
        };
    }

    private int FocusCalls()
        => JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));

    [Fact] // KB-29 / ADR-0044: asked, the panel's first control takes DOM focus once the list has arrived
    public void The_first_control_takes_focus_once_the_list_has_arrived()
    {
        var before = FocusCalls();

        RenderPanel(Context());

        Assert.True(FocusCalls() > before);
    }

    [Fact] // ADR-0044 / KB-29: unasked, the panel takes nothing — the popover opens with the keyboard on the commands above
    public void Unasked_the_panel_takes_no_focus()
    {
        var before = FocusCalls();

        RenderPanel(Context(focusRequest: 0));

        Assert.Equal(before, FocusCalls());
    }

    [Fact] // ADR-0044 / KB-31: Shift+Tab from the commands asks for the last control, Cancel
    public void Asked_for_its_last_control_the_panel_focuses_cancel()
    {
        var cut = RenderPanel(Context(focusRequest: 0, focusLastRequest: 1));

        var cancel = cut.FindComponents<MudButton>().Single(b => b.Instance.Class == "mud-ex-grid-filter-cancel");
        var focused = Assert.Single(JSInterop.Invocations, i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ReferenceOf(cancel.Instance).Id, ((ElementReference)focused.Arguments[0]!).Id);
    }

    /// <summary>The reference a MudButton focuses through, read from the button itself.</summary>
    private static ElementReference ReferenceOf(MudButton button)
    {
        for (var type = button.GetType(); type is not null; type = type.BaseType)
        {
            var field = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                .FirstOrDefault(f => f.FieldType == typeof(ElementReference));
            if (field is not null)
                return (ElementReference)field.GetValue(button)!;
        }
        throw new InvalidOperationException("MudButton holds no element reference");
    }

    [Fact] // ADR-0044 / KB-31: the panel draws no Tab wrap of its own — the core's sentinels stand around it
    public void The_panel_has_no_sentinels_of_its_own()
    {
        var cut = RenderPanel(Context());

        Assert.Empty(cut.FindAll("span[tabindex]"));
    }

    [Fact] // ADR-0044 / FL-15: a key on the value list is handed to the core; a key in the search field is not
    public async Task The_value_list_hands_its_keys_to_the_core()
    {
        var cut = RenderPanel(Context());

        await cut.Find(".mud-ex-grid-filter-values").KeyDownAsync(new KeyboardEventArgs { Key = "s" });
        await cut.Find(".mud-ex-grid-filter-search input").KeyDownAsync(new KeyboardEventArgs { Key = "e" });

        Assert.Equal(["s"], _listKeys);
    }

    [Fact] // KB-31 / SRV-5: Enter in a value field submits whatever Apply's state — the default
           // button it clicks is never disabled. Gated on Apply's own disabled button, an Enter
           // typed with the value was dropped on a Server circuit, where the button is enabled
           // a round trip later.
    public void The_default_button_is_never_disabled()
    {
        var cut = RenderPanel(Context(ColumnType.Text, FilterUiMode.Condition));

        var submits = cut.Find("form").QuerySelectorAll("button[type=submit]");
        Assert.Contains("mud-ex-grid-filter-default", submits[0].ClassName);
        Assert.False(submits[0].HasAttribute("disabled"));
        Assert.True(submits[0].HasAttribute("hidden"));
        // Apply itself still shows it is unavailable (WR-2), and a submit then applies nothing.
        Assert.True(cut.Find(".mud-ex-grid-filter-apply").HasAttribute("disabled"));
    }

    // ---- The condition form (WR-1's TooMany degrade, WR-2).

    [Fact] // WR-1 / ADR-0009: a TooMany answer degrades to the condition form, on Contains for text
    public void Too_many_degrades_to_the_condition_form()
    {
        var cut = RenderPanel(Context(answer: DistinctValues.TooMany));

        Assert.Empty(cut.FindAll(".mud-ex-grid-filter-value"));
        Assert.NotNull(cut.Find(".mud-ex-grid-filter-operator"));
        Assert.Equal(FilterOperator.Contains, cut.FindComponent<MudSelect<FilterOperator>>().Instance.GetState(x => x.Value));
    }

    [Fact] // WR-2: the operand is MudBlazor's control for the column's type
    public void The_operand_is_the_types_own_control()
    {
        Assert.Single(RenderPanel(Context(ColumnType.Text, FilterUiMode.Condition)).FindComponents<MudTextField<string>>());
        Assert.Single(RenderPanel(Context(ColumnType.Number, FilterUiMode.Condition)).FindComponents<MudNumericField<decimal?>>());
        // Editable: the date can be typed, not only picked — its input is not read-only.
        var date = RenderPanel(Context(ColumnType.Date, FilterUiMode.Condition));
        Assert.Single(date.FindComponents<MudDatePicker>());
        Assert.False(date.Find(".mud-ex-grid-filter-operand input").HasAttribute("readonly"));
        Assert.Single(RenderPanel(Context(ColumnType.Boolean, FilterUiMode.Condition)).FindComponents<MudSelect<bool?>>());
    }

    [Fact] // WR-2: Apply is unavailable until the operand is given, and a boolean starts with nothing chosen
    public async Task Apply_waits_for_the_operand()
    {
        var text = RenderPanel(Context(ColumnType.Text, FilterUiMode.Condition));
        Assert.True(Button(text, "mud-ex-grid-filter-apply").HasAttribute("disabled"));
        await text.Find(".mud-ex-grid-filter-operand input").InputAsync(new ChangeEventArgs { Value = "Al" });
        Assert.False(Button(text, "mud-ex-grid-filter-apply").HasAttribute("disabled"));

        var flag = RenderPanel(Context(ColumnType.Boolean, FilterUiMode.Condition));
        Assert.Null(flag.FindComponent<MudSelect<bool?>>().Instance.GetState(x => x.Value));
        Assert.True(Button(flag, "mud-ex-grid-filter-apply").HasAttribute("disabled"));
    }

    [Fact] // WR-2: the Enter that submits while Apply is unavailable applies nothing
    public async Task A_submit_before_the_operand_applies_nothing()
    {
        var cut = RenderPanel(Context(ColumnType.Text, FilterUiMode.Condition));

        await cut.Find("form").SubmitAsync();

        Assert.Empty(_applied);
    }

    [Fact] // WR-2 / ADR-0009: a condition applies as FilterPanelChoices says — the typed operand, one clause
    public async Task A_condition_applies_its_typed_operand()
    {
        var cut = RenderPanel(Context(ColumnType.Number, FilterUiMode.Condition,
            current: new FilterSpec([new FilterClause(FilterOperator.GreaterThan, 5m)])));

        // The condition in force is where the form starts, and applying it again is the same spec.
        await cut.Find("form").SubmitAsync();

        var clause = Assert.Single(Assert.Single(_applied)!.Clauses);
        Assert.Equal(FilterOperator.GreaterThan, clause.Operator);
        Assert.Equal(5m, clause.Value);
    }

    [Fact] // WR-2: the blank operators take no operand, so Apply is available at once
    public async Task A_blank_operator_applies_without_an_operand()
    {
        var cut = RenderPanel(Context(ColumnType.Text, FilterUiMode.Condition,
            current: new FilterSpec([new FilterClause(FilterOperator.IsBlank)])));

        Assert.Empty(cut.FindAll(".mud-ex-grid-filter-operand"));
        await cut.Find("form").SubmitAsync();

        Assert.Equal(FilterOperator.IsBlank, Assert.Single(Assert.Single(_applied)!.Clauses).Operator);
    }

    // ---- In the grid: the same choices, the same FilterSpec, under either Chrome (WR-1).

    private async Task<GridFilter?> FilterFromGridAsync(IGridChrome? chrome, Func<IRenderedComponent<ExGrid<Trade>>, Task> choose)
    {
        GridFilter? filter = null;
        var cut = Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Window, Rows(5))
            .Add(g => g.TotalCount, 5)
            .Add(g => g.Columns, (GridColumn<Trade>[])[
                new("Book", ColumnType.Text, r => r.Book, width: new ColumnWidthSpec(ColumnWidth.Fixed(100)), filterUi: FilterUiMode.ValueList),
                new("Amount", ColumnType.Number, r => r.Amount, width: new ColumnWidthSpec(ColumnWidth.Fixed(112)))])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 400)
            .Add(g => g.Chrome, chrome)
            .Add(g => g.OnFilterChanged, f => filter = f)
            .Add(g => g.OnDistinctValuesNeeded, (_, _) => Task.FromResult(DistinctValues.Of(["Book 000", "Book 001", null]))));
        // The filter stands below the commands as the popover opens (ADR-0044).
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        await choose(cut);
        Assert.Empty(cut.FindAll(".ex-popover"));
        return filter;
    }

    [Fact] // WR-1 / FN-17: unticking the same value makes the same FilterSpec under both Chromes
    public async Task The_same_choices_make_the_same_filter_under_both_chromes()
    {
        var builtIn = await FilterFromGridAsync(null, async cut =>
        {
            await cut.FindAll(".ex-popover input[type=checkbox]")[1].ChangeAsync(new ChangeEventArgs { Value = false });
            await cut.FindAll(".ex-popover-actions button").Single(b => b.TextContent == "OK").ClickAsync(new MouseEventArgs());
        });
        var mud = await FilterFromGridAsync(MudGridChrome.Default, async cut =>
        {
            await cut.FindAll(".mud-ex-grid-filter-value input[type=checkbox]")[1].ChangeAsync(new ChangeEventArgs { Value = false });
            await cut.Find("form.mud-ex-grid-filter").SubmitAsync();
        });

        var expected = Assert.Single(builtIn!.Columns);
        var actual = Assert.Single(mud!.Columns);
        Assert.Equal(expected.Key, actual.Key);
        Assert.Equal(expected.Value.Clauses[0].Operator, actual.Value.Clauses[0].Operator);
        Assert.Equal(expected.Value.Clauses[0].Values!, actual.Value.Clauses[0].Values!);
    }

    private static IElement SelectAll(IRenderedComponent<ContainerFragment> cut)
        => cut.Find(".mud-ex-grid-filter-all input[type=checkbox]");

    [Fact] // FL-10 / ADR-0009: with a search, the submit applies the matching checked values and no hidden one
    public async Task A_search_narrows_what_the_submit_applies()
    {
        var cut = RenderPanel(Context());

        await cut.Find(".mud-ex-grid-filter-search input").InputAsync(new ChangeEventArgs { Value = "ph" });
        await cut.Find("form").SubmitAsync();

        var clause = Assert.Single(Assert.Single(_applied)!.Clauses);
        Assert.Equal(["Alpha"], clause.Values!);
    }

    [Fact] // FL-11 / ADR-0009: (Select All) is a tri-state MudCheckBox over the values shown
    public async Task Select_all_is_tri_state_over_the_values_shown()
    {
        var cut = RenderPanel(Context());
        var all = cut.FindComponent<MudCheckBox<bool?>>();
        Assert.True(all.Instance.GetState(x => x.Value));
        Assert.Equal("(Select All)", cut.Find(".mud-ex-grid-filter-all").TextContent.Trim());

        await ValueBoxes(cut)[1].ChangeAsync(new ChangeEventArgs { Value = false });
        Assert.Null(cut.FindComponent<MudCheckBox<bool?>>().Instance.GetState(x => x.Value)); // mixed

        await SelectAll(cut).ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.All(ValueBoxes(cut), box => Assert.True(box.IsChecked()));
        await SelectAll(cut).ChangeAsync(new ChangeEventArgs { Value = false });
        Assert.All(ValueBoxes(cut), box => Assert.False(box.IsChecked()));

        await cut.Find(".mud-ex-grid-filter-search input").InputAsync(new ChangeEventArgs { Value = "amm" });
        Assert.Equal("(Select All Search Results)", cut.Find(".mud-ex-grid-filter-all").TextContent.Trim());
    }

    [Fact] // FL-13 / ADR-0009: while searching a filtered column, the matches can join the filter in force
    public async Task Add_current_selection_joins_the_filter_in_force()
    {
        var cut = RenderPanel(Context(current: new FilterSpec([new FilterClause(FilterOperator.In, Values: ["Beta"])])));
        Assert.Empty(cut.FindAll(".mud-ex-grid-filter-add"));

        await cut.Find(".mud-ex-grid-filter-search input").InputAsync(new ChangeEventArgs { Value = "amm" });
        await ValueBoxes(cut)[0].ChangeAsync(new ChangeEventArgs { Value = true }); // Gamma, the one shown
        await cut.Find(".mud-ex-grid-filter-add input").ChangeAsync(new ChangeEventArgs { Value = true });
        await cut.Find("form").SubmitAsync();

        var clause = Assert.Single(Assert.Single(_applied)!.Clauses);
        Assert.Equal(["Beta", "Gamma"], clause.Values!);
    }

    [Fact] // FL-14 / ADR-0009: a two-condition filter reopens as its two conditions and applies as one spec
    public async Task Two_conditions_round_trip()
    {
        var current = new FilterSpec(
            [new FilterClause(FilterOperator.StartsWith, "Al"), new FilterClause(FilterOperator.EndsWith, "ma")],
            FilterCombinator.Or);
        var cut = RenderPanel(Context(mode: FilterUiMode.Condition, current: current));

        Assert.Equal(FilterOperator.EndsWith, cut.FindComponent<MudSelect<FilterOperator?>>().Instance.GetState(x => x.Value));
        await cut.Find("form").SubmitAsync();

        var spec = Assert.Single(_applied)!;
        Assert.Equal(FilterCombinator.Or, spec.Combinator);
        Assert.Equal([FilterOperator.StartsWith, FilterOperator.EndsWith], spec.Clauses.Select(c => c.Operator));
        Assert.Equal(["Al", "ma"], spec.Clauses.Select(c => c.Value));
    }
}
