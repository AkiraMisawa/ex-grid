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
}
