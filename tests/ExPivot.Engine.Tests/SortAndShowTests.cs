using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>Ordering by a Value Field, Show Values As, and the Value Fields' captions (ADR-0059).</summary>
public class SortAndShowTests
{
    private static string[] RowLabels(PivotReport report)
        => report.Rows.Where(r => r.Role != PivotRowRole.GrandTotal).Select(r => r.Labels[0].Text!).ToArray();

    [Fact] // ADR-0059: by a Value Field, descending — largest first
    public void Sort_by_value_descending()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region") with { Sort = new PivotSort(PivotSortDirection.Descending, ByValue: 0) }],
            Values = [Sum("Amount")],
        };

        Assert.Equal(["East", "West", "North", "(blank)"], RowLabels(Report(layout)));
    }

    [Fact] // ADR-0059: by a Value Field, ascending — smallest first; the (blank) Item is an Item like any other
    public void Sort_by_value_ascending()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region") with { Sort = new PivotSort(PivotSortDirection.Ascending, ByValue: 0) }],
            Values = [Sum("Amount")],
        };

        Assert.Equal(["(blank)", "North", "West", "East"], RowLabels(Report(layout)));
    }

    [Fact] // ADR-0059: an Item with no value sorts last, and ties fall back to the label
    public void Blank_values_last_and_ties_by_label()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Product") with { Sort = new PivotSort(PivotSortDirection.Descending, ByValue: 0) }],
            Values = [Value("Region", PivotAggregation.Count)],
        };
        // Count of Region: Apples 3, Pears 2, Plums 1 (the Blank region is not counted).
        Assert.Equal(["Apples", "Pears", "Plums"], RowLabels(Report(layout)));

        var tie = Sales.Select(s => s with { Amount = 1 }).ToArray();
        var byAmount = layout with { Values = [Sum("Amount")], Rows = [P("Region") with { Sort = new PivotSort(PivotSortDirection.Descending, 0) }] };
        // East has three records, the others: West two, North one, (blank) one — North before (blank) by label.
        Assert.Equal(["East", "West", "North", "(blank)"], RowLabels(Report(byAmount, tie)));

        // Count of Region is empty on the (blank) Item — no value to count — so it sorts last
        // ascending as descending.
        var countOfRegion = new PivotLayout
        {
            Rows = [P("Region") with { Sort = new PivotSort(PivotSortDirection.Ascending, 0) }],
            Values = [Value("Region", PivotAggregation.Count)],
        };
        Assert.Equal(["North", "West", "East", "(blank)"], RowLabels(Report(countOfRegion)));
    }

    [Fact] // ADR-0059: a column field ordered by a Value Field at its column totals
    public void Column_field_sorted_by_value()
    {
        var layout = new PivotLayout
        {
            Columns = [P("Product") with { Sort = new PivotSort(PivotSortDirection.Descending, 0) }],
            Values = [Sum("Amount")],
        };

        Assert.Equal(["Apples", "Pears", "Plums", "Grand Total"], Headers(Report(layout)));
    }

    [Fact] // ADR-0059: % of Grand Total, formatted 0.00% by default
    public void Percent_of_grand_total()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region")],
            Values = [Sum("Amount") with { ShowValuesAs = PivotShowValuesAs.PercentOfGrandTotal }],
        };

        Assert.Equal(
        [
            "i East || 63.16%",
            "i North || 3.51%",
            "i West || 31.58%",
            "i (blank) || 1.75%",
            "t Grand Total || 100.00%",
        ], Lines(Report(layout)));
    }

    [Fact] // ADR-0059: % of Column Total and % of Row Total
    public void Percent_of_column_and_row_total()
    {
        var byColumn = new PivotLayout
        {
            Rows = [P("Region")],
            Columns = [P("Online")],
            Values = [Sum("Amount") with { ShowValuesAs = PivotShowValuesAs.PercentOfColumnTotal, NumberFormat = "0%" }],
        };
        Assert.Equal("i East || 38% | 84% | 63%", Lines(Report(byColumn))[0]);

        var byRow = byColumn with { Values = [Sum("Amount") with { ShowValuesAs = PivotShowValuesAs.PercentOfRowTotal, NumberFormat = "0%" }] };
        Assert.Equal("i East || 28% | 72% | 100%", Lines(Report(byRow))[0]);
    }

    [Fact] // ADR-0059: a zero divisor is #DIV/0!; an empty cell stays empty
    public void A_zero_total_is_div0()
    {
        var zeros = Sales.Select(s => s with { Amount = 0 }).ToArray();
        var layout = new PivotLayout
        {
            Rows = [P("Region")],
            Columns = [P("Online")],
            Values = [Sum("Amount") with { ShowValuesAs = PivotShowValuesAs.PercentOfGrandTotal }],
        };

        Assert.Equal("i North || #DIV/0! |  | #DIV/0!", Lines(Report(layout, zeros))[1]);
    }

    [Fact] // ADR-0059: Excel's default captions, and a number where one would repeat
    public void Default_captions()
    {
        var report = Report(new PivotLayout
        {
            Values = [Sum("Amount"), Sum("Amount"), Value("Amount", PivotAggregation.CountNumbers), Value("Region", PivotAggregation.Count)],
        });

        Assert.Equal(["Sum of Amount", "Sum of Amount2", "Count Numbers of Amount", "Count of Region"], report.ValueCaptions);
    }

    [Fact] // ADR-0059: a caption of its own; one a default would repeat pushes the default to a number
    public void A_caption_of_its_own()
    {
        var report = Report(new PivotLayout
        {
            Values = [Sum("Amount"), Sum("Quantity") with { Caption = "Sum of Amount" }],
        });

        Assert.Equal(["Sum of Amount2", "Sum of Amount"], report.ValueCaptions);
    }

    [Theory] // ADR-0059: a caption of its own that another Value Field or a Pivot Field has is refused
    [InlineData("Region")]
    [InlineData("amount")]
    public void A_taken_caption_is_refused(string caption)
    {
        Assert.Throws<InvalidOperationException>(() => Report(new PivotLayout { Values = [Sum("Amount") with { Caption = caption }] }));
        Assert.Throws<InvalidOperationException>(() => Report(new PivotLayout
        {
            Values = [Sum("Amount") with { Caption = "Total" }, Sum("Quantity") with { Caption = "TOTAL" }],
        }));
    }

    [Fact] // ADR-0059: the words are the Consumer's to replace
    public void Words_are_replaceable()
    {
        var options = EnUs with
        {
            Label = id => id switch
            {
                PivotWords.GrandTotal => "総計",
                "caption-sum" => "合計 / {0}",
                PivotWords.Blank => "(空白)",
                _ => null,
            },
        };
        var report = Report(RowsBy("Region"), options: options);

        Assert.Equal(["Row Labels", "合計 / Amount"], Headers(report));
        Assert.Equal("i (空白) || 5", Lines(report)[3]);
        Assert.Equal("t 総計 || 285", Lines(report)[4]);
    }
}
