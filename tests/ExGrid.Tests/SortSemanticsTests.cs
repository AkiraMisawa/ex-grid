using System.Globalization;
using Xunit;

namespace ExGrid.Tests;

public class SortSemanticsTests
{
    private static IReadOnlyList<Trade> Sort(IReadOnlyList<Trade> rows, params SortSpec[] sorts)
        => GridQueryEngine.Apply(rows, TradeColumns.All, null, sorts);

    [Fact] // ADR-0023: text sorts case-insensitively
    public void Text_sorts_case_insensitively()
    {
        var sorted = Sort(
            [new(Book: "credit"), new(Book: "Rates"), new(Book: "CREDIT")],
            new SortSpec("Book", SortDirection.Ascending));

        Assert.Equal(["credit", "CREDIT", "Rates"], sorted.Select(t => t.Book));
    }

    [Fact] // ADR-0023: Blanks last ascending
    public void Blanks_sort_last_ascending()
    {
        var sorted = Sort(
            [new(Amount: 2), new(Amount: null), new(Amount: 1)],
            new SortSpec("Amount", SortDirection.Ascending));

        Assert.Equal([1, 2, null], sorted.Select(t => t.Amount));
    }

    [Fact] // ADR-0023: Blanks last descending too — Excel's behaviour, and the useful one
    public void Blanks_sort_last_descending()
    {
        var sorted = Sort(
            [new(Amount: 2), new(Amount: null), new(Amount: 1)],
            new SortSpec("Amount", SortDirection.Descending));

        Assert.Equal([2, 1, null], sorted.Select(t => t.Amount));
    }

    [Fact] // ADR-0023: the sort is stable — equal keys keep the incoming order
    public void Sort_is_stable_for_equal_keys()
    {
        var sorted = Sort(
            [
                new(Book: "rates", Amount: 1),
                new(Book: "Credit", Amount: 2),
                new(Book: "RATES", Amount: 3),
                new(Book: "Rates", Amount: 4),
            ],
            new SortSpec("Book", SortDirection.Ascending));

        // "rates" == "RATES" == "Rates" under the pinned comparison; their input order survives.
        Assert.Equal([2, 1, 3, 4], sorted.Select(t => t.Amount));
    }

    [Fact] // ADR-0023: the Sorts list is priority order — first entry primary
    public void Multi_column_sort_applies_in_priority_order()
    {
        var sorted = Sort(
            [
                new(Book: "Credit", Amount: 1),
                new(Book: "Rates", Amount: 2),
                new(Book: "Credit", Amount: 3),
            ],
            new SortSpec("Book", SortDirection.Ascending),
            new SortSpec("Amount", SortDirection.Descending));

        Assert.Equal([3, 1, 2], sorted.Select(t => t.Amount));
    }

    [Fact] // ADR-0023: numbers sort on one number line whatever their runtime type
    public void Mixed_numeric_runtime_types_sort_numerically()
    {
        var sorted = Sort(
            [new(Amount: 2), new(Amount: 1.5), new(Amount: 3m), new(Amount: 10L)],
            new SortSpec("Amount", SortDirection.Ascending));

        Assert.Equal([1.5, 2, 3m, 10L], sorted.Select(t => t.Amount));
    }

    [Fact] // ADR-0023: false before true ascending (Excel: FALSE < TRUE)
    public void False_sorts_before_true_ascending()
    {
        var sorted = Sort(
            [new(Cleared: true), new(Cleared: false), new(Cleared: null)],
            new SortSpec("Cleared", SortDirection.Ascending));

        Assert.Equal([false, true, null], sorted.Select(t => t.Cleared));
    }

    [Fact] // ADR-0023
    public void Dates_sort_chronologically()
    {
        var sorted = Sort(
            [
                new(TradedOn: new DateTime(2026, 8, 30)),
                new(TradedOn: new DateTime(2026, 1, 1)),
                new(TradedOn: null),
            ],
            new SortSpec("TradedOn", SortDirection.Descending));

        Assert.Equal(
            [new DateTime(2026, 8, 30), new DateTime(2026, 1, 1), null],
            sorted.Select(t => t.TradedOn));
    }

    [Fact] // ADR-0023: the sort collation is ordinal — the machine's culture must not reorder a Query's result
    public void Sort_collation_is_ordinal_and_ignores_current_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            // Any culture-aware collation would file 'é' with 'e', before 'z'.
            var sorted = Sort(
                [new(Book: "éclair"), new(Book: "zebra")],
                new SortSpec("Book", SortDirection.Ascending));

            Assert.Equal(["zebra", "éclair"], sorted.Select(t => t.Book));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact] // ADR-0023: a refusal raised while sorting still names the column
    public void Sorting_mixed_date_runtime_types_throws_naming_the_column()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Sort(
            [new(TradedOn: new DateTime(2026, 8, 30)), new(TradedOn: new DateOnly(2026, 8, 30))],
            new SortSpec("TradedOn", SortDirection.Ascending)));
        Assert.Contains("TradedOn", ex.Message);
    }

    [Fact] // ADR-0023: refuse rather than guess
    public void Sorting_by_an_unknown_column_throws_naming_it()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Sort([new(Book: "Rates")], new SortSpec("Notional", SortDirection.Ascending)));
        Assert.Contains("Notional", ex.Message);
    }
}
