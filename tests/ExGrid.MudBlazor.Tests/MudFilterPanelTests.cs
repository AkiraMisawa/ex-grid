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
    private int _closed;
    private int _cleared;

    private FilterPanelContext Context(
        ColumnType type = ColumnType.Text, FilterUiMode mode = FilterUiMode.ValueList,
        DistinctValues? answer = null, FilterSpec? current = null, int focusRequest = 1)
        => new(
            "Book", type, current, FilterOperators.AllowedFor(type), mode,
            () => Task.FromResult(answer ?? DistinctValues.Of(["Alpha", "Beta", null, "Gamma"])),
            spec => _applied.Add(spec), () => _cleared++, () => _closed++, focusRequest,
            InnerPopupChanged: _popups.Add);

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

    [Fact] // WR-1: Cancel closes as a discard, Clear removes the column's filter — both the core's
    public async Task Cancel_and_clear_are_the_cores_exits()
    {
        var cut = RenderPanel(Context());

        await Button(cut, "mud-ex-grid-filter-cancel").ClickAsync(new MouseEventArgs());
        await Button(cut, "mud-ex-grid-filter-clear").ClickAsync(new MouseEventArgs());

        Assert.Equal(1, _closed);
        Assert.Equal(1, _cleared);
        Assert.Empty(_applied);
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
        Assert.Equal("Effacer", Button(cut, "mud-ex-grid-filter-clear").TextContent.Trim());
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
            "MudDataGrid_Clear" => new(key, "Effacer"),
            _ => new(key, key, resourceNotFound: true),
        };
    }

    [Fact] // KB-29 / ADR-0039: once the list has arrived, the search field takes DOM focus
    public void The_first_control_takes_focus_once_the_list_has_arrived()
    {
        var before = JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));

        RenderPanel(Context());

        Assert.True(JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)) > before);
    }

    [Fact] // KB-31 / ADR-0039: the panel's Tab wrap — two sentinels, either side of the controls
    public void Two_sentinels_stand_either_side_of_the_controls()
    {
        var cut = RenderPanel(Context());

        var form = cut.Find("form.mud-ex-grid-filter");
        Assert.Contains("mud-ex-grid-focus-wrap", form.FirstElementChild!.ClassName);
        Assert.Contains("mud-ex-grid-focus-wrap", form.LastElementChild!.ClassName);
        Assert.All(cut.FindAll(".mud-ex-grid-focus-wrap"), s => Assert.Equal("0", s.GetAttribute("tabindex")));
    }

    [Fact] // WR-2 / KB-31: Apply is the form's one submit button, so Enter in a value field is Apply
    public void Apply_is_the_forms_submit_button()
    {
        var cut = RenderPanel(Context());

        var submits = cut.Find("form").QuerySelectorAll("button[type=submit]");
        Assert.Single(submits);
        Assert.Contains("mud-ex-grid-filter-apply", submits[0].ClassName);
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
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".ex-popover [role=menuitem]").Single(b => b.TextContent.Trim() == "Filter").ClickAsync(new MouseEventArgs());
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
}
