using System.Globalization;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>What an Item is and in what order Items stand (ADR-0059, "Items" and "Order").</summary>
public class ItemTests
{
    private sealed record Thing(object? Key, decimal Amount);

    private enum Rating { Low, High }

    private static readonly PivotField<Thing>[] ThingFields =
    [
        new("Key", PivotFieldType.Text, t => t.Key),
        new("Amount", PivotFieldType.Number, t => t.Amount),
    ];

    private static PivotReport Things(params object?[] keys)
        => Report(keys.Select(k => new Thing(k, 1m)).ToArray(), ThingFields,
            new PivotLayout { Rows = [P("Key")], Values = [Sum("Amount")] });

    private static string[] Labels(PivotReport report)
        => report.Rows.Where(r => r.Role != PivotRowRole.GrandTotal).Select(r => r.Labels[0].Text!).ToArray();

    [Fact] // ADR-0059: a field's Items, ascending, (blank) last, each with its records' total
    public void Items_are_the_distinct_values_in_ascending_order_with_blank_last()
    {
        var report = Report(RowsBy("Region"));

        Assert.Equal(["Row Labels", "Sum of Amount"], Headers(report));
        Assert.Equal(
        [
            "i East || 180",
            "i North || 10",
            "i West || 90",
            "i (blank) || 5",
            "t Grand Total || 285",
        ], Lines(report));
    }

    [Fact] // ADR-0059 (a reading): text Items are told apart ignoring case, labelled as first seen
    public void Text_items_are_told_apart_ignoring_case_and_labelled_by_the_first_spelling()
    {
        var report = Things("East", "EAST", "east", "West");

        Assert.Equal(["East", "West"], Labels(report));
        Assert.Equal("3", Cell(report, 0, 0));
    }

    [Fact] // ADR-0059: spaces are significant
    public void Spaces_are_significant()
        => Assert.Equal(["East", "East "], Labels(Things("East", "East ")));

    [Fact] // ADR-0059/0023: an empty string is a value, a Blank is (blank), and they are two Items
    public void An_empty_string_is_an_item_of_its_own_beside_the_blank()
    {
        var report = Things("", null, "A");

        Assert.Equal(["", "A", "(blank)"], Labels(report));
    }

    [Fact] // ADR-0059: a number is its value, whatever its .NET type
    public void Numbers_of_any_type_with_one_value_are_one_item()
    {
        var report = Things(1, 1.0m, 1.0, 1L, (short)1, 2.5f);

        Assert.Equal(["1", "2.5"], Labels(report));
        Assert.Equal("5", Cell(report, 0, 0));
    }

    [Fact] // ADR-0059: numbers order by value, not by their text
    public void Numbers_order_by_value()
        => Assert.Equal(["9", "10", "100"], Labels(Things(10, 100, 9)));

    [Fact] // ADR-0059: a non-finite number is the Item #NUM!, after the Booleans and before (blank)
    public void A_non_finite_number_is_the_error_item()
    {
        var report = Things(double.NaN, double.PositiveInfinity, true, null, "x", 1);

        Assert.Equal(["1", "x", "TRUE", "#NUM!", "(blank)"], Labels(report));
        Assert.Equal("2", Cell(report, 3, 0));
    }

    [Fact] // ADR-0059: a date is its clock value; DateOnly is midnight of that day
    public void Dates_are_their_clock_value()
    {
        var report = Things(new DateTime(2026, 1, 1), new DateOnly(2026, 1, 1), new DateTime(2026, 1, 1, 13, 45, 0),
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.FromHours(9)));

        // The culture's own long time, whatever space ICU puts before its PM.
        var afternoon = new DateTime(2026, 1, 1, 13, 45, 0).ToString("G", EnUs.Culture);
        Assert.Equal(["1/1/2026", afternoon, "1/2/2026"], Labels(report));
        Assert.StartsWith("1/1/2026 1:45:00", afternoon);
        Assert.Equal("2", Cell(report, 0, 0));
    }

    [Fact] // ADR-0059: a field's format labels its Items, under the report's culture
    public void A_fields_format_labels_its_items()
    {
        PivotField<Sale>[] fields = [new("Date", PivotFieldType.Date, s => s.Date, format: "yyyy-MM"), .. Fields.Where(f => f.Name != "Date")];
        var report = PivotEngine.Compute(Sales.Where(s => s.Region is not null).ToArray()[..2], fields,
            new PivotLayout { Rows = [P("Date")], Values = [Sum("Amount")] }, EnUs);

        Assert.Equal(["2026-01", "2026-01"], Labels(report));
    }

    [Fact] // ADR-0059: a Boolean is labelled TRUE or FALSE, FALSE first
    public void Booleans_are_labelled_as_excel_labels_them()
        => Assert.Equal(["FALSE", "TRUE"], Labels(Report(RowsBy("Online"))));

    [Fact] // ADR-0059: across kinds, numbers, dates, text, Booleans, #NUM!, then (blank)
    public void Items_of_different_kinds_order_by_kind()
    {
        var report = Things("b", null, true, new DateTime(2026, 1, 1), 2);

        Assert.Equal(["2", "1/1/2026", "b", "TRUE", "(blank)"], Labels(report));
    }

    [Fact] // ADR-0059: text orders by the culture ignoring case
    public void Text_orders_by_the_culture_ignoring_case()
        => Assert.Equal(["Apple", "banana", "Cherry"], Labels(Things("banana", "Cherry", "Apple")));

    [Fact] // ADR-0059: any other type is text, by its name
    public void An_enum_is_text_by_its_name()
        => Assert.Equal(["High", "Low"], Labels(Things(Rating.Low, Rating.High)));

    [Fact] // ADR-0059: descending reverses the order, and (blank) stays last
    public void Descending_keeps_blank_last()
    {
        var layout = new PivotLayout { Rows = [P("Region") with { Sort = PivotSort.Descending }], Values = [Sum("Amount")] };

        Assert.Equal(["West", "North", "East", "(blank)"], Labels(Report(layout)));
    }

    [Fact] // ADR-0059: a field's declared Items come first, in their order — Excel's custom lists
    public void Declared_items_come_first_in_their_declared_order()
    {
        PivotField<Sale>[] fields =
        [
            .. Fields.Where(f => f.Name != "Product"),
            new("Product", PivotFieldType.Text, s => s.Product, itemOrder: ["Plums", "apples"]),
        ];
        var layout = new PivotLayout { Rows = [P("Product")], Values = [Sum("Amount")] };

        Assert.Equal(["Plums", "Apples", "Pears"], Labels(PivotEngine.Compute(Sales, fields, layout, EnUs)));
        Assert.Equal(["Pears", "Apples", "Plums"], Labels(PivotEngine.Compute(Sales, fields,
            layout with { Rows = [P("Product") with { Sort = PivotSort.Descending }] }, EnUs)));
    }

    [Fact] // ADR-0059: a Hidden Item written as a key reads back as the same Item in any culture
    public void An_item_key_is_its_kind_and_invariant_text()
    {
        Assert.Equal(new PivotItemKey(PivotItemKind.Number, "1234.5"), PivotItemKey.For(1234.5m));
        Assert.Equal(new PivotItemKey(PivotItemKind.Date, "2026-09-30T00:00:00"), PivotItemKey.For(new DateOnly(2026, 9, 30)));
        Assert.Equal(new PivotItemKey(PivotItemKind.Date, "2026-09-30T13:45:00.5"),
            PivotItemKey.For(new DateTime(2026, 9, 30, 13, 45, 0, 500)));
        Assert.Equal(PivotItemKey.Boolean(true), PivotItemKey.For(true));
        Assert.Equal(PivotItemKey.Blank, PivotItemKey.For(null));
        Assert.Equal(PivotItemKey.Error, PivotItemKey.For(double.NaN));
        Assert.Equal(PivotItemKey.Text("EAST"), PivotItemKey.For("east"));
        Assert.Equal(PivotItemKey.Text("EAST").GetHashCode(), PivotItemKey.For("east").GetHashCode());
        Assert.Equal(PivotItemKey.Number(0), PivotItemKey.For(-0.0));
    }

    [Theory] // ADR-0059: a key that could name no Item is refused, never kept
    [InlineData(PivotItemKind.Number, "12,5")]
    [InlineData(PivotItemKind.Number, "NaN")]
    [InlineData(PivotItemKind.Date, "30/09/2026")]
    [InlineData(PivotItemKind.Boolean, "true")]
    [InlineData(PivotItemKind.Blank, "")]
    [InlineData(PivotItemKind.Error, "#DIV/0!")]
    [InlineData(PivotItemKind.Text, null)]
    public void A_key_that_names_no_item_is_refused(PivotItemKind kind, string? value)
        => Assert.ThrowsAny<ArgumentException>(() => new PivotItemKey(kind, value));

    [Fact] // ADR-0059: labels follow the report's culture; the keys do not
    public void Labels_follow_the_culture()
    {
        var things = new[] { new Thing(1234.5m, 1m), new Thing(new DateTime(2026, 9, 30), 1m) };
        var german = PivotEngine.Compute(things, ThingFields, new PivotLayout { Rows = [P("Key")], Values = [Sum("Amount")] },
            new PivotOptions { Culture = CultureInfo.GetCultureInfo("de-DE") });

        Assert.Equal(["1234,5", "30.09.2026"], Labels(german));
    }
}
