using Xunit;

namespace ExGrid.Data.Tests;

/// <summary>
/// The Aggregations' one definition (ADR-0130, DA-18), read through one cell of values — the way
/// ExGrid's Selection Summary reads it. ExPivot's engine reads the same arithmetic column by column,
/// and its own tables (PV-4) pass through it.
/// </summary>
public class AggregateAccumulatorTests
{
    private static AggregateAccumulator Of(params object?[] values)
    {
        var accumulator = new AggregateAccumulator();
        foreach (var value in values)
            accumulator.Add(value);
        return accumulator;
    }

    [Fact] // ADR-0130: a number in every figure; text, Booleans and dates in Count only; Blank in none
    public void ADR0130_numbers_are_summed_and_everything_but_blank_is_counted()
    {
        var cell = Of(1, 2.5m, "x", true, new DateTime(2026, 10, 5), null, 4L);

        Assert.Equal(AggregateResult.Of(6m), cell.Read(Aggregation.Count));
        Assert.Equal(AggregateResult.Of(3m), cell.Read(Aggregation.CountNumbers));
        Assert.Equal(AggregateResult.Of(7.5m), cell.Read(Aggregation.Sum));
        Assert.Equal(AggregateResult.Of(2.5m), cell.Read(Aggregation.Average));
        Assert.Equal(AggregateResult.Of(1m), cell.Read(Aggregation.Min));
        Assert.Equal(AggregateResult.Of(4m), cell.Read(Aggregation.Max));
    }

    [Fact] // ADR-0130: text that looks like a number is text
    public void ADR0130_numeric_text_is_not_a_number()
    {
        var cell = Of("123", "4");

        Assert.Equal(AggregateResult.Of(2m), cell.Read(Aggregation.Count));
        Assert.Equal(AggregateResult.Of(0m), cell.Read(Aggregation.CountNumbers));
    }

    [Fact] // ADR-0130, PV-4: Integer and Decimal values are summed exactly, without trailing zeros
    public void ADR0130_money_is_summed_exactly()
    {
        var cell = Of(0.1m, 0.2m, 1.50m, 1.50m);

        var sum = cell.Read(Aggregation.Sum);
        Assert.Equal(3.3m, sum.Exact);
        Assert.Equal("3.3", sum.ToString());
    }

    [Fact] // ADR-0060: a double leaves exactness for Excel's arithmetic
    public void ADR0060_a_double_makes_the_sum_inexact()
    {
        var sum = Of(1m, 0.5d).Read(Aggregation.Sum);

        Assert.Null(sum.Exact);
        Assert.Equal(1.5d, sum.Number);
    }

    [Fact] // ADR-0130: an error among the values leaves Count alone
    public void ADR0130_an_error_among_the_values_leaves_count_alone()
    {
        var cell = Of(1, 2);
        cell.AddError();

        Assert.Equal(AggregateResult.Of(3m), cell.Read(Aggregation.Count));
        Assert.Equal(AggregateError.ErrorAmongValues, cell.Read(Aggregation.Sum).Error);
        Assert.Equal(AggregateError.ErrorAmongValues, cell.Read(Aggregation.CountNumbers).Error);
    }

    [Fact] // ADR-0060: a non-finite double is #NUM! in every numeric Aggregation
    public void ADR0060_a_non_finite_number_is_not_a_number()
    {
        var cell = Of(1, double.PositiveInfinity);

        Assert.Equal(AggregateError.NotANumber, cell.Read(Aggregation.Sum).Error);
        Assert.Equal(AggregateResult.Of(2m), cell.Read(Aggregation.CountNumbers));
    }

    [Fact] // ADR-0060: Average of values none of which is a number is #DIV/0!
    public void ADR0060_average_with_no_number_divides_by_zero()
    {
        Assert.Equal(AggregateError.DivideByZero, Of("a").Read(Aggregation.Average).Error);
        Assert.Equal(AggregateResult.Of(0m), Of("a").Read(Aggregation.Sum));
        Assert.True(Of(null, null).Read(Aggregation.Sum).IsEmpty);
    }

    [Fact] // ADR-0060: the sample deviation of 2, 4, 4, 4, 5, 5, 7, 9 is 2.138…, the population one 2
    public void ADR0060_deviations_answer_as_excel()
    {
        var cell = Of(2, 4, 4, 4, 5, 5, 7, 9);

        Assert.Equal(2d, cell.Read(Aggregation.StdDevp).Number, 12);
        Assert.Equal(2.138089935299395d, cell.Read(Aggregation.StdDev).Number, 12);
        Assert.Equal(40320d, Of(1, 2, 3, 4, 5, 6, 7, 8).Read(Aggregation.Product).Number);
    }

    [Fact] // ADR-0130: every integral type is a number, a char and an enum are not
    public void ADR0130_integral_types_are_numbers()
    {
        var cell = Of((byte)1, (short)2, 3u, 4UL, (sbyte)-1, (Half)0.5, 0.5f, 'c', DayOfWeek.Monday);

        Assert.Equal(AggregateResult.Of(7m), cell.Read(Aggregation.CountNumbers));
        Assert.Equal(AggregateResult.Of(9m), cell.Read(Aggregation.Count));
        Assert.Equal(10d, cell.Read(Aggregation.Sum).Number);
    }
}

/// <summary>Parts summarised elsewhere fold in by the same arithmetic (ADR-0066/0130).</summary>
public class AggregateMergeTests
{
    [Fact] // ADR-0130: a server's COUNT, SUM, MIN and MAX merge into what folding the values gives
    public void ADR0130_merged_parts_read_as_the_values_folded_in()
    {
        var folded = new AggregateAccumulator();
        foreach (var value in new object?[] { 1.25m, 3m, "x", 2m })
            folded.Add(value);

        var merged = new AggregateAccumulator();
        merged.Merge(new AggregateCounts { Values = 2, Numbers = 2 }, new AggregateSum { Exact = 4.25m }, new AggregateExtremes { ExactMin = 1.25m, ExactMax = 3m });
        merged.Merge(new AggregateCounts { Values = 1 }, default, default);
        merged.Merge(new AggregateCounts { Values = 1, Numbers = 1 }, new AggregateSum { Exact = 2m }, new AggregateExtremes { ExactMin = 2m, ExactMax = 2m });

        foreach (var aggregation in new[] { Aggregation.Sum, Aggregation.Count, Aggregation.CountNumbers, Aggregation.Average, Aggregation.Min, Aggregation.Max })
            Assert.Equal(folded.Read(aggregation), merged.Read(aggregation));
        Assert.Throws<InvalidOperationException>(() => merged.Read(Aggregation.Product));
    }
}
