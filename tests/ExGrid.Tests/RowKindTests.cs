using ExGrid.Components;
using ExGrid.Rows;
using Xunit;

namespace ExGrid.Tests;

public class RowKindTests
{
    [Fact] // ADR-0024: a row nobody said anything about is a detail row
    public void The_default_kind_is_detail()
    {
        Assert.Equal(RowKind.Detail, default(RowKind));
        Assert.Equal("ex-row", RowClasses.For(RowKind.Detail, placeholder: false));
    }

    [Fact] // ADR-0024: the three roles are distinct in all six combinations
    public void All_six_combinations_are_distinct()
    {
        var combinations =
            (from kind in new[] { RowKind.Detail, RowKind.Group, RowKind.Total }
             from placeholder in new[] { false, true }
             select RowClasses.For(kind, placeholder)).ToList();

        Assert.Equal(6, combinations.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact] // ADR-0024: called once per painted row on every render — no allocation
    public void The_same_combination_returns_the_same_instance()
    {
        Assert.Same(RowClasses.For(RowKind.Group, placeholder: false), RowClasses.For(RowKind.Group, placeholder: false));
    }

    [Fact] // ADR-0038 / ADR-0027 P5: all twelve combinations are distinct, and each is one interned string
    public void All_twelve_striped_combinations_are_distinct_and_interned()
    {
        var combinations =
            (from kind in new[] { RowKind.Detail, RowKind.Group, RowKind.Total }
             from placeholder in new[] { false, true }
             from stripe in new[] { false, true }
             select (kind, placeholder, stripe)).ToList();

        Assert.Equal(12, combinations.Select(c => RowClasses.For(c.kind, c.placeholder, c.stripe)).Distinct(StringComparer.Ordinal).Count());
        Assert.All(combinations, c => Assert.Same(
            RowClasses.For(c.kind, c.placeholder, c.stripe), RowClasses.For(c.kind, c.placeholder, c.stripe)));
    }
}
