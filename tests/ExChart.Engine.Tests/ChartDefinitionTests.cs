using ExChart.Engine;

namespace ExChart.Engine.Tests;

public class ChartDefinitionTests
{
    private static ChartPoint P(double x, double? y) => new(x, y is { } v ? ChartValue.Of(v) : null);

    [Fact] // ADR-0251: a value keeps the type and the digits it was handed in with
    public void A_decimal_value_keeps_its_digits_and_a_double_its_round_trip()
    {
        Assert.Equal("1234567.89", ChartValue.Of(1234567.89m).ToInvariantString());
        Assert.True(ChartValue.Of(1234567.89m).IsDecimal);
        Assert.Equal("0.1", ChartValue.Of(0.1).ToInvariantString());
        Assert.Equal(1234567.89, ChartValue.Of(1234567.89m).ToDouble());
    }

    [Fact] // ADR-0253: a missing value is handed in as null, never as NaN
    public void NaN_is_refused_as_a_value() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartValue.Of(double.NaN));

    [Fact] // ADR-0253: a series that declares no Series Kind is refused by name
    public void A_series_without_a_kind_is_refused()
    {
        var e = Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Numbers(),
            [new ChartSeries { Id = "a", Name = "EPE", Kind = SeriesKind.Undeclared, Points = [P(0, 1)] }]));
        Assert.Contains("EPE", e.Message);
        Assert.Contains("Series Kind", e.Message);
    }

    [Fact] // ADR-0251: a line joins neighbours in the data, so points out of order are refused
    public void Points_out_of_order_are_refused() =>
        Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Numbers(),
            [ChartSeries.Of("a", "A", SeriesKind.Level, [P(1, 1), P(0, 2)])]));

    [Fact] // ADR-0253: an Amount per period is always drawn from zero
    public void An_amount_that_says_it_is_not_from_zero_is_refused() =>
        Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Numbers(),
            [ChartSeries.Of("a", "CVA", SeriesKind.AmountPerPeriod, [P(0, null), P(1, 5)]) with { FromZero = false }]));

    [Fact] // ADR-0253: an amount needs an interval to be drawn per year over
    public void An_amount_on_categories_or_at_its_open_edge_is_refused()
    {
        Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Categories(["a", "b"]),
            [ChartSeries.Of("a", "CVA", SeriesKind.AmountPerPeriod, [P(0, null), P(1, 5)])]));
        Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Numbers(),
            [ChartSeries.Of("a", "CVA", SeriesKind.AmountPerPeriod, [P(0, 3), P(1, 5)])]));
    }

    [Fact] // ADR-0253: a Level reaches zero unless it says otherwise; an amount always does
    public void Reaching_zero_defaults_to_yes()
    {
        Assert.True(ChartSeries.Of("a", "EPE", SeriesKind.Level, []).ReachesZero);
        Assert.False((ChartSeries.Of("a", "Zero", SeriesKind.Level, []) with { FromZero = false }).ReachesZero);
        Assert.True(ChartSeries.Of("a", "CVA", SeriesKind.AmountPerPeriod, []).ReachesZero);
    }

    [Fact] // ADR-0256: a series that sets no colour key is coloured by its identity
    public void The_colour_key_defaults_to_the_identity() =>
        Assert.Equal("epe-before", ChartSeries.Of("epe-before", "EPE", SeriesKind.Level, []).EffectiveKeys.Colour);

    [Fact] // ADR-0251: the axis' scale is declared, with a default by type
    public void The_scale_defaults_by_type_and_can_be_declared()
    {
        Assert.Equal(AxisScale.Proportional, ChartAxis.Dates().EffectiveScale);
        Assert.Equal(AxisScale.Categories, ChartAxis.Categories(["1M"]).EffectiveScale);
        Assert.Equal(AxisScale.Categories, (ChartAxis.Numbers() with { Scale = AxisScale.Categories }).EffectiveScale);
    }

    [Fact] // ADR-0257: a function is sampled between positions, which categories do not have
    public void A_function_on_categories_or_as_a_step_is_refused()
    {
        Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Categories(["a"]),
            [ChartSeries.Sampled("c", "Curve", SeriesKind.Level, 0, 1, _ => 1.0)]));
        Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Numbers(),
            [ChartSeries.Sampled("c", "Fwd", SeriesKind.RateOverInterval, 0, 1, _ => 1.0)]));
    }
}
