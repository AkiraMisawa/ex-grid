using ExChart.Engine;

namespace ExChart.Engine.Tests;

// The definition's refusals and defaults are shared cases (tests/cases/chart/definition.json, ADR-0370);
// what stays here is C#'s own: a decimal, which the Script Core's values do not have.
public class ChartDefinitionTests
{
    [Fact] // ADR-0251: a value keeps the type and the digits it was handed in with
    public void A_decimal_value_keeps_its_digits_and_a_double_its_round_trip()
    {
        Assert.Equal("1234567.89", ChartValue.Of(1234567.89m).ToInvariantString());
        Assert.True(ChartValue.Of(1234567.89m).IsDecimal);
        Assert.Equal("0.1", ChartValue.Of(0.1).ToInvariantString());
        Assert.Equal(1234567.89, ChartValue.Of(1234567.89m).ToDouble());
    }
}
