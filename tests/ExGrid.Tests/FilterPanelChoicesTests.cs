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
}
