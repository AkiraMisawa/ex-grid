using ExGrid.Chrome;
using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What a filter panel's choices mean (ADR-0009), in one place, so that the built-in
/// panel and a substituted one turn the same choices into the same FilterSpec (ADR-0010).
/// </summary>
public class FilterPanelChoicesTests
{
    private static readonly DistinctValues Domain = DistinctValues.Of(["Alpha", "Beta", null, "Gamma"]);

    private static FilterSpec InList(params object?[] values) => new([new FilterClause(FilterOperator.In, Values: values)]);

    [Fact] // ADR-0009: with no filter in force, every value starts chosen
    public void Without_a_filter_everything_starts_chosen()
        => Assert.Equal(["Alpha", "Beta", null, "Gamma"], FilterPanelChoices.InitiallyChosen(null, Domain));

    [Fact] // ADR-0009: the applied list starts chosen, intersected with the domain just fetched
    public void The_applied_list_starts_chosen_within_the_domain()
    {
        var chosen = FilterPanelChoices.InitiallyChosen(InList("Beta", "Delta", null), Domain);

        Assert.Equal(2, chosen.Count);
        Assert.Contains("Beta", chosen);
        Assert.Contains(null, chosen); // Blank is a value like any other (ADR-0023)
        Assert.DoesNotContain("Delta", chosen); // not shown, so never chosen unseen
    }

    [Fact] // ADR-0009: everything chosen is no filter at all — set membership, never a count
    public void Everything_chosen_is_no_filter()
        => Assert.Null(FilterPanelChoices.FromValueList(Domain, new HashSet<object?>(["Gamma", null, "Beta", "Alpha"])));

    [Fact] // ADR-0009: a part chosen is one In clause, in the domain's order
    public void A_part_chosen_is_one_in_clause_in_domain_order()
    {
        var spec = FilterPanelChoices.FromValueList(Domain, new HashSet<object?>(["Gamma", null]));

        var clause = Assert.Single(spec!.Clauses);
        Assert.Equal(FilterOperator.In, clause.Operator);
        Assert.Equal([null, "Gamma"], clause.Values!);
    }

    [Fact] // ADR-0009: nothing chosen keeps nothing, as asked — never silently no filter
    public void Nothing_chosen_is_a_filter_that_keeps_nothing()
    {
        var spec = FilterPanelChoices.FromValueList(Domain, new HashSet<object?>());

        Assert.Empty(Assert.Single(spec!.Clauses).Values!);
    }

    [Fact] // ADR-0009: a TooMany answer has no list to choose from
    public void A_too_many_answer_has_no_value_list()
        => Assert.Throws<ArgumentException>(() => FilterPanelChoices.FromValueList(DistinctValues.TooMany, new HashSet<object?>()));

    [Fact] // ADR-0009: the condition in force is what the form starts with
    public void The_condition_in_force_is_the_start()
    {
        var current = new FilterSpec([new FilterClause(FilterOperator.GreaterThan, 5m)]);

        Assert.Equal(FilterOperator.GreaterThan, FilterPanelChoices.InitialOperator(ColumnType.Number, current, tooMany: false));
        Assert.Equal(5m, FilterPanelChoices.InitialOperand(current));
        // TooMany does not override a condition the user was looking at.
        Assert.Equal(FilterOperator.GreaterThan, FilterPanelChoices.InitialOperator(ColumnType.Number, current, tooMany: true));
    }

    [Fact] // ADR-0009: TooMany degrades to Excel's search box — Contains where the type has it
    public void Too_many_starts_on_contains_where_the_type_has_it()
    {
        Assert.Equal(FilterOperator.Contains, FilterPanelChoices.InitialOperator(ColumnType.Text, null, tooMany: true));
        Assert.Equal(FilterOperators.AllowedFor(ColumnType.Number)[0], FilterPanelChoices.InitialOperator(ColumnType.Number, null, tooMany: true));
        Assert.Equal(FilterOperators.AllowedFor(ColumnType.Text)[0], FilterPanelChoices.InitialOperator(ColumnType.Text, null, tooMany: false));
        // An applied value list is not a condition to start from.
        Assert.Null(FilterPanelChoices.InitialOperand(InList("Alpha")));
    }

    [Fact] // ADR-0009: IsBlank and IsNotBlank take no operand; everything else does
    public void Only_the_blank_operators_take_no_operand()
    {
        Assert.False(FilterPanelChoices.TakesOperand(FilterOperator.IsBlank));
        Assert.False(FilterPanelChoices.TakesOperand(FilterOperator.IsNotBlank));
        Assert.All(Enum.GetValues<FilterOperator>().Where(o => o is not (FilterOperator.IsBlank or FilterOperator.IsNotBlank)),
            o => Assert.True(FilterPanelChoices.TakesOperand(o)));
    }

    [Fact] // ADR-0009: a condition is one clause; an operand-taking operator with none is no filter
    public void A_condition_is_one_clause()
    {
        Assert.Equal(FilterOperator.GreaterThan, Assert.Single(FilterPanelChoices.FromCondition(FilterOperator.GreaterThan, 5m)!.Clauses).Operator);
        Assert.Equal(5m, Assert.Single(FilterPanelChoices.FromCondition(FilterOperator.GreaterThan, 5m)!.Clauses).Value);
        Assert.Null(FilterPanelChoices.FromCondition(FilterOperator.Equals, null));
        Assert.Null(Assert.Single(FilterPanelChoices.FromCondition(FilterOperator.IsBlank, null)!.Clauses).Value);
    }

    [Fact] // ADR-0009/0023: In offered in a condition form is membership of its one operand — Values, never Value
    public void In_as_a_condition_is_a_one_member_list()
    {
        var clause = Assert.Single(FilterPanelChoices.FromCondition(FilterOperator.In, 5m)!.Clauses);

        Assert.Null(clause.Value);
        Assert.Equal([5m], clause.Values!);
    }

    [Fact] // ADR-0023: a typed operand, invariant first; text that does not read as the type is null
    public void Operands_parse_per_type()
    {
        Assert.Equal(1234.5m, FilterPanelChoices.ParseOperand(ColumnType.Number, "1234.5"));
        Assert.Null(FilterPanelChoices.ParseOperand(ColumnType.Number, "abc"));
        Assert.Equal(new DateTime(2026, 9, 24), FilterPanelChoices.ParseOperand(ColumnType.Date, "2026-09-24"));
        Assert.Equal(true, FilterPanelChoices.ParseOperand(ColumnType.Boolean, "true"));
        Assert.Equal("x", FilterPanelChoices.ParseOperand(ColumnType.Text, "x"));
    }

    private static string TextOf(object? value) => value?.ToString() ?? "(Blanks)";

    [Fact] // ADR-0009 / FL-10: with a search, OK applies the checked values among those that match
    public void A_search_applies_only_the_matching_checked_values()
    {
        // Everything is checked; the search shows Alpha and Gamma ("a" matches both, and Beta).
        var chosen = new HashSet<object?>(["Alpha", "Beta", null, "Gamma"]);

        var spec = FilterPanelChoices.FromValueList(Domain, chosen, TextOf, search: "mm", addToCurrent: false, current: null);

        Assert.Equal(InList("Gamma"), spec, FilterSpecComparer.Instance);
    }

    [Fact] // ADR-0009 / FL-10: a checked value the search hides is not applied
    public void A_hidden_checked_value_is_not_applied()
    {
        var chosen = new HashSet<object?>(["Alpha", "Gamma"]);

        var spec = FilterPanelChoices.FromValueList(Domain, chosen, TextOf, search: "alp", addToCurrent: false, current: null);

        Assert.Equal(InList("Alpha"), spec, FilterSpecComparer.Instance);
    }

    [Fact] // ADR-0009 / FL-13: added to the filter in force, the matches join it
    public void Added_to_the_filter_in_force_the_matches_join_it()
    {
        var chosen = new HashSet<object?>(["Alpha", "Beta", null, "Gamma"]);

        var spec = FilterPanelChoices.FromValueList(
            Domain, chosen, TextOf, search: "gam", addToCurrent: true, current: InList("Beta"));

        Assert.Equal(InList("Beta", "Gamma"), spec, FilterSpecComparer.Instance);
    }

    [Fact] // ADR-0009 / FL-13: adding keeps a value in force that the domain no longer shows
    public void Adding_keeps_what_was_in_force_even_outside_the_domain()
    {
        var spec = FilterPanelChoices.FromValueList(
            Domain, new HashSet<object?>(["Alpha"]), TextOf, search: "alp", addToCurrent: true, current: InList("Delta"));

        Assert.Equal(InList("Alpha", "Delta"), spec, FilterSpecComparer.Instance);
    }

    [Theory] // ADR-0009 / FL-13: add is offered only while searching a column with a value filter
    [InlineData("gam", true, true)]
    [InlineData("", true, false)]
    [InlineData("gam", false, false)]
    public void Add_is_offered_while_searching_a_value_filter(string search, bool valueFilter, bool offered)
    {
        FilterSpec? current = valueFilter ? InList("Beta") : new FilterSpec([new FilterClause(FilterOperator.Contains, "a")]);

        Assert.Equal(offered, FilterPanelChoices.OffersAddToFilter(current, search));
    }

    [Fact] // ADR-0009: with no search, what is checked is what applies, as before
    public void Without_a_search_the_checked_values_apply()
    {
        var spec = FilterPanelChoices.FromValueList(
            Domain, new HashSet<object?>(["Beta"]), TextOf, search: "", addToCurrent: true, current: InList("Alpha"));

        Assert.Equal(InList("Beta"), spec, FilterSpecComparer.Instance);
    }

    [Fact] // ADR-0009 / FL-11: (Select All) reads the values shown — checked, clear or mixed
    public void Select_all_reads_the_values_shown()
    {
        object?[] shown = ["Alpha", "Gamma"];

        Assert.Equal(SelectAllState.Checked, FilterPanelChoices.StateOfAll(shown, new HashSet<object?>(["Alpha", "Gamma", "Beta"])));
        Assert.Equal(SelectAllState.Unchecked, FilterPanelChoices.StateOfAll(shown, new HashSet<object?>(["Beta"])));
        Assert.Equal(SelectAllState.Mixed, FilterPanelChoices.StateOfAll(shown, new HashSet<object?>(["Gamma"])));
    }

    [Fact] // ADR-0009 / FL-11: toggling (Select All) checks every value shown, or clears them when all were checked
    public void Toggling_select_all_acts_on_the_values_shown_only()
    {
        var chosen = new HashSet<object?>(["Beta"]);

        FilterPanelChoices.ToggleAll(["Alpha", "Gamma"], chosen);
        Assert.Equal(3, chosen.Count);

        FilterPanelChoices.ToggleAll(["Alpha", "Gamma"], chosen);
        Assert.Equal(["Beta"], chosen);
    }

    [Fact] // ADR-0009 / FL-14: two conditions make one spec of two clauses with the chosen combinator
    public void Two_conditions_make_one_spec_with_their_combinator()
    {
        var spec = FilterPanelChoices.FromConditions(
            FilterOperator.GreaterThan, 10m, FilterCombinator.Or, FilterOperator.LessThan, 2m);

        Assert.Equal(
            new FilterSpec([new FilterClause(FilterOperator.GreaterThan, 10m), new FilterClause(FilterOperator.LessThan, 2m)], FilterCombinator.Or),
            spec, FilterSpecComparer.Instance);
    }

    [Fact] // ADR-0009 / FL-14: an unfinished second condition leaves the first alone
    public void An_unfinished_second_condition_leaves_the_first()
    {
        var spec = FilterPanelChoices.FromConditions(
            FilterOperator.GreaterThan, 10m, FilterCombinator.And, FilterOperator.LessThan, null);

        Assert.Equal(new FilterSpec([new FilterClause(FilterOperator.GreaterThan, 10m)]), spec, FilterSpecComparer.Instance);
        Assert.Null(FilterPanelChoices.FromConditions(FilterOperator.Equals, null, FilterCombinator.And, null, null));
    }

    [Fact] // ADR-0009 / FL-14: a two-clause filter in force reopens as its two conditions
    public void A_two_clause_filter_reopens_as_two_conditions()
    {
        var current = new FilterSpec(
            [new FilterClause(FilterOperator.GreaterThan, 10m), new FilterClause(FilterOperator.LessThan, 2m)], FilterCombinator.Or);

        Assert.Equal(FilterOperator.GreaterThan, FilterPanelChoices.InitialOperator(ColumnType.Number, current, tooMany: false));
        Assert.Equal(10m, FilterPanelChoices.InitialOperand(current));
        Assert.Equal((FilterOperator.LessThan, 2m, FilterCombinator.Or), FilterPanelChoices.InitialSecond(current));
        Assert.Equal((null, null, FilterCombinator.And), FilterPanelChoices.InitialSecond(InList("Beta")));
    }
}

/// <summary>FilterSpec holds lists, so records compare them by reference; this compares
/// what they say.</summary>
internal sealed class FilterSpecComparer : IEqualityComparer<FilterSpec?>
{
    internal static readonly FilterSpecComparer Instance = new();

    public bool Equals(FilterSpec? x, FilterSpec? y)
    {
        if (x is null || y is null)
            return x is null && y is null;
        return x.Combinator == y.Combinator
            && x.Clauses.Count == y.Clauses.Count
            && x.Clauses.Zip(y.Clauses).All(pair =>
                pair.First.Operator == pair.Second.Operator
                && Equals(pair.First.Value, pair.Second.Value)
                && (pair.First.Values ?? []).SequenceEqual(pair.Second.Values ?? []));
    }

    public int GetHashCode(FilterSpec? obj) => 0;
}
