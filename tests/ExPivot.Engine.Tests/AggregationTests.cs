using System.Globalization;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>What each Aggregation answers (ADR-0060, "Aggregation"): the table, exact money, and
/// totals from records.</summary>
public class AggregationTests
{
    private sealed record Obs(string Group, object? Value);

    private static readonly PivotField<Obs>[] ObsFields =
    [
        new("Group", PivotFieldType.Text, o => o.Group),
        new("Value", PivotFieldType.Number, o => o.Value),
    ];

    /// <summary>The one value of a Value Field over the given values, all in one group.</summary>
    private static PivotValue? Aggregate(PivotAggregation aggregation, params object?[] values)
    {
        var report = PivotEngine.Compute(values.Select(v => new Obs("g", v)).ToArray(), ObsFields,
            new PivotLayout { Values = [new PivotValueField("Value", aggregation)] }, EnUs);
        return report.ValueAt(report.Rows.Single(), 0);
    }

    private static string? Text(PivotAggregation aggregation, params object?[] values) => Aggregate(aggregation, values)?.Text;

    [Theory] // ADR-0060: the table, over numbers
    [InlineData(PivotAggregation.Sum, "20")]
    [InlineData(PivotAggregation.Count, "4")]
    [InlineData(PivotAggregation.Average, "5")]
    [InlineData(PivotAggregation.Max, "8")]
    [InlineData(PivotAggregation.Min, "2")]
    [InlineData(PivotAggregation.Product, "384")]
    [InlineData(PivotAggregation.CountNumbers, "4")]
    [InlineData(PivotAggregation.Var, "6.66666666666667")]
    [InlineData(PivotAggregation.Varp, "5")]
    [InlineData(PivotAggregation.StdDev, "2.58198889747161")]
    [InlineData(PivotAggregation.StdDevp, "2.23606797749979")]
    public void Each_aggregation_over_numbers(PivotAggregation aggregation, string expected)
        => Assert.Equal(expected, Text(aggregation, 2, 4, 6, 8));

    [Theory] // ADR-0060 (a reading): values, but none a number
    [InlineData(PivotAggregation.Sum, "0")]
    [InlineData(PivotAggregation.Count, "2")]
    [InlineData(PivotAggregation.Average, "#DIV/0!")]
    [InlineData(PivotAggregation.Max, "0")]
    [InlineData(PivotAggregation.Min, "0")]
    [InlineData(PivotAggregation.Product, "0")]
    [InlineData(PivotAggregation.CountNumbers, "0")]
    [InlineData(PivotAggregation.StdDev, "#DIV/0!")]
    [InlineData(PivotAggregation.StdDevp, "#DIV/0!")]
    [InlineData(PivotAggregation.Var, "#DIV/0!")]
    [InlineData(PivotAggregation.Varp, "#DIV/0!")]
    public void Values_none_of_them_a_number(PivotAggregation aggregation, string expected)
        => Assert.Equal(expected, Text(aggregation, "a", true));

    [Theory] // ADR-0060 (a reading): records whose every value is Blank are an empty cell
    [InlineData(PivotAggregation.Sum)]
    [InlineData(PivotAggregation.Count)]
    [InlineData(PivotAggregation.Average)]
    [InlineData(PivotAggregation.CountNumbers)]
    [InlineData(PivotAggregation.StdDev)]
    public void Only_blanks_are_an_empty_cell(PivotAggregation aggregation)
        => Assert.Null(Aggregate(aggregation, null, null));

    [Fact] // ADR-0060: text, a Boolean and a date are counted, and are never a number
    public void Non_numbers_are_counted_and_not_summed()
    {
        object?[] values = [5, "7", true, new DateTime(2026, 1, 1), null];

        Assert.Equal("5", Text(PivotAggregation.Sum, values));
        Assert.Equal("4", Text(PivotAggregation.Count, values));
        Assert.Equal("1", Text(PivotAggregation.CountNumbers, values));
        Assert.Equal("5", Text(PivotAggregation.Average, values));
    }

    [Fact] // ADR-0060: StdDev and Var take two numbers; the population forms take one
    public void The_sample_forms_take_two_numbers()
    {
        Assert.Equal("#DIV/0!", Text(PivotAggregation.StdDev, 4));
        Assert.Equal("#DIV/0!", Text(PivotAggregation.Var, 4));
        Assert.Equal("0", Text(PivotAggregation.StdDevp, 4));
        Assert.Equal("0", Text(PivotAggregation.Varp, 4));
    }

    [Fact] // ADR-0060: money stays exact — decimals sum in decimal
    public void Decimals_sum_exactly()
    {
        var sum = Aggregate(PivotAggregation.Sum, 0.1m, 0.2m)!;

        Assert.Equal(0.3m, sum.Exact);
        Assert.Equal("0.3", sum.Text);
        Assert.Equal("0.3", sum.ToString(null, CultureInfo.InvariantCulture));
    }

    [Fact] // ADR-0060: a double among the numbers makes the whole Aggregation double, Excel's arithmetic
    public void A_double_makes_the_aggregation_double()
    {
        var sum = Aggregate(PivotAggregation.Sum, 0.1, 0.2)!;

        Assert.Null(sum.Exact);
        Assert.Equal("0.3", sum.Text); // at most 15 significant digits, as Excel shows it
        Assert.Equal("0.30000000000000004", sum.ToString(null, CultureInfo.InvariantCulture));
        Assert.Null(Aggregate(PivotAggregation.Sum, 1m, 2.5)!.Exact);
    }

    [Fact] // ADR-0060: integral types are exact too, and an average of them is a decimal division
    public void Integral_values_are_exact()
    {
        Assert.Equal(6m, Aggregate(PivotAggregation.Sum, 1, 2L, (short)3)!.Exact);
        Assert.Equal("3.33333333333333", Text(PivotAggregation.Average, 3, 3, 4));
        Assert.Equal(3m, Aggregate(PivotAggregation.Max, 1, 3m, 2)!.Exact);
    }

    [Fact] // ADR-0060: a decimal sum that overflows falls back to double rather than failing
    public void A_decimal_overflow_falls_back_to_double()
    {
        var sum = Aggregate(PivotAggregation.Sum, decimal.MaxValue, decimal.MaxValue)!;

        Assert.Null(sum.Exact);
        Assert.Equal(2 * (double)decimal.MaxValue, sum.Number);
    }

    [Fact] // ADR-0060: a non-finite number in the data is #NUM!, never a number
    public void A_non_finite_value_is_num()
    {
        Assert.Equal("#NUM!", Text(PivotAggregation.Sum, 1, double.NaN));
        Assert.Equal("#NUM!", Text(PivotAggregation.Max, double.PositiveInfinity));
        Assert.Equal("2", Text(PivotAggregation.Count, 1, double.NaN));
        Assert.True(Aggregate(PivotAggregation.Sum, double.NaN)!.IsError);
    }

    [Fact] // ADR-0060: an overflow in double is #NUM!
    public void A_double_overflow_is_num()
        => Assert.Equal("#NUM!", Text(PivotAggregation.Product, double.MaxValue, 10.0));

    [Fact] // ADR-0060: a total is aggregated from its records, never from the totals below it
    public void An_average_total_is_the_average_of_the_records()
    {
        Obs[] observations = [new("A", 1), new("A", 2), new("A", 3), new("B", 10)];
        var report = PivotEngine.Compute(observations, ObsFields,
            new PivotLayout { Rows = [P("Group")], Values = [new PivotValueField("Value", PivotAggregation.Average)] }, EnUs);

        // The averages of A and B are 2 and 10; their mean would be 6. The records' mean is 4.
        Assert.Equal(["i A || 2", "i B || 10", "t Grand Total || 4"], Lines(report));
    }

    [Fact] // ADR-0060: where no record carries both a row and a column, the cell is empty — Count too
    public void A_cell_with_no_record_is_empty_for_every_aggregation()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region")],
            Columns = [P("Product")],
            Values = [Value("Amount", PivotAggregation.Count)],
        };
        var report = Report(layout);

        Assert.Equal(["Row Labels", "Apples", "Pears", "Plums", "Grand Total"], Headers(report));
        Assert.Equal(
        [
            "i East || 2 | 1 |  | 3",
            "i North ||  | 1 |  | 1",
            "i West || 1 |  | 1 | 2",
            "i (blank) ||  |  | 1 | 1",
            "t Grand Total || 3 | 2 | 2 | 7",
        ], Lines(report));
    }

    [Fact] // ADR-0060: a Value Field's number format, under the report's culture
    public void A_number_format_formats_the_values()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region")],
            Values = [Sum("Amount") with { NumberFormat = "#,##0.00" }],
        };
        var thousand = new[] { Sales[0] with { Amount = 1234.5m } };

        Assert.Equal("1,234.50", Cell(Report(layout, thousand), 0, 0));
        Assert.Equal("1.234,50", Cell(Report(layout, thousand, new PivotOptions { Culture = CultureInfo.GetCultureInfo("de-DE") }), 0, 0));
    }

    [Theory] // ADR-0060/0061: a format that cannot be used, or would run away, is refused
    [InlineData("N999999999")]
    [InlineData("N31")]
    [InlineData("Q")]
    [InlineData("")]
    public void A_runaway_or_broken_number_format_is_refused(string format)
    {
        Assert.NotNull(PivotNumberFormat.Check(format));
        Assert.Throws<InvalidOperationException>(() =>
            Report(new PivotLayout { Values = [Sum("Amount") with { NumberFormat = format }] }));
    }

    [Theory] // ADR-0060: formats that can be used
    [InlineData("N2")]
    [InlineData("N30")]
    [InlineData("#,##0.00")]
    [InlineData("0.0%")]
    [InlineData("C")]
    public void A_usable_number_format_is_accepted(string format)
        => Assert.Null(PivotNumberFormat.Check(format));

    [Fact] // ADR-0060: several Value Fields of one field are each read from one accumulation
    public void Several_aggregations_of_one_field()
    {
        var layout = new PivotLayout
        {
            Values = [Sum("Amount"), Value("Amount", PivotAggregation.Average), Value("Amount", PivotAggregation.Max)],
        };
        var report = Report(layout);

        Assert.Equal(["Sum of Amount", "Average of Amount", "Max of Amount"], Headers(report));
        Assert.Equal(["t  || 285 | 40.7142857142857 | 100"], Lines(report));
    }
}
