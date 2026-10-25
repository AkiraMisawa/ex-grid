using System.Globalization;
using Xunit;

namespace ExGrid.Tests;

public class SortSemanticsTests
{
    private static IReadOnlyList<Trade> Sort(IReadOnlyList<Trade> rows, params SortSpec[] sorts)
        => GridQueryEngine.Apply(rows, TradeColumns.All, null, sorts);

    [Fact] // ADR-0023: numbers sort on one number line whatever their runtime type
    public void Mixed_numeric_runtime_types_sort_numerically()
    {
        var sorted = Sort(
            [new(Amount: 2), new(Amount: 1.5), new(Amount: 3m), new(Amount: 10L)],
            new SortSpec("Amount", SortDirection.Ascending));

        Assert.Equal([1.5, 2, 3m, 10L], sorted.Select(t => t.Amount));
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

    [Fact] // ADR-0023: 1.1f and 1.1d normalise to the same decimal, so they tie and stay stable
    public void Float_and_double_of_the_same_value_tie_in_a_sort()
    {
        var sorted = Sort(
            [new(Book: "a", Amount: 1.1d), new(Book: "b", Amount: 1.1f)],
            new SortSpec("Amount", SortDirection.Ascending));

        Assert.Equal(["a", "b"], sorted.Select(t => t.Book));
    }

    [Fact] // ADR-0023: a cell of an undeclared date type is refused even when the primary level never ties
    public void An_undeclared_date_type_is_refused_even_when_the_primary_level_never_ties()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Sort(
            [
                new(Book: "credit", TradedOn: new DateTime(2026, 8, 30)),
                new(Book: "rates", TradedOn: new DateOnly(2026, 8, 30)),
            ],
            new SortSpec("Book", SortDirection.Ascending),
            new SortSpec("TradedOn", SortDirection.Ascending)));

        Assert.Contains("TradedOn", ex.Message);
        Assert.Contains("declared DateTime", ex.Message);
    }

    [Fact] // ADR-0023: a refusal raised while sorting still names the column
    public void Sorting_an_undeclared_date_type_throws_naming_the_column()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Sort(
            [new(TradedOn: new DateTime(2026, 8, 30)), new(TradedOn: new DateOnly(2026, 8, 30))],
            new SortSpec("TradedOn", SortDirection.Ascending)));
        Assert.Contains("TradedOn", ex.Message);
    }
}
