using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>The cell index hashes its (row, column) keys mixed (ADR-0066, ticket 09). Packed into
/// one <c>long</c>, a key's own hash is <c>row ^ column</c>: a cube of thousands of row nodes by
/// 270 dates then has a few hundred hash codes for its cells, every lookup walks a chain, and a
/// million records took 23 s where a mixing hash took 3.3 s.</summary>
public class CellKeyTests
{
    [Fact] // ADR-0066: 300 row nodes by 270 dates and their total — each cell its own hash, where row ^ column gave fewer than 512
    public void The_cell_index_hashes_a_wide_layout_apart()
    {
        var keys = (from row in Enumerable.Range(0, 300) from column in Enumerable.Range(0, 271) select CellKey.Of(row, column)).ToArray();

        var mixed = keys.Select(CellKey.Comparer.GetHashCode).ToArray();
        var packed = keys.Select(key => key.GetHashCode()).ToArray();

        Assert.True(mixed.Distinct().Count() > keys.Length * 0.999, $"{mixed.Distinct().Count()} hash codes for {keys.Length} cells");
        Assert.True(packed.Distinct().Count() <= 512, "the packed key's own hash is row ^ column");
        // Spread over a table as large as the cells, as a dictionary spreads them: about 1 − 1/e of
        // the buckets hold a key when the hash is as good as random.
        Assert.True(Occupied(mixed, keys.Length) > 0.6, $"{Occupied(mixed, keys.Length):P0} of the buckets used");
        Assert.True(Occupied(packed, keys.Length) < 0.01, $"{Occupied(packed, keys.Length):P0} of the buckets used");
    }

    [Fact] // ADR-0066: a cube the engine built over 270 dates in Columns and 13 row Items hashes its cells apart
    public void A_cube_of_270_dates_hashes_its_cells_apart()
    {
        var start = new DateTime(2026, 1, 2);
        var sales = (from day in Enumerable.Range(0, 270)
                     from region in Enumerable.Range(0, 13)
                     select new Sale("R" + region, "P", start.AddDays(day), 1m, 1, true)).ToArray();
        var layout = new PivotLayout { Rows = [P("Region")], Columns = [P("Date")], Values = [Sum("Amount")] };

        var cube = PivotEngine.Aggregate(sales, Fields, layout);
        var keys = cube.CellKeys.ToArray();

        // 3,510 leaves, 13 row totals, 270 column totals and the grand total.
        Assert.Equal((270 * 13) + 13 + 270 + 1, keys.Length);
        Assert.True(keys.Select(CellKey.Comparer.GetHashCode).Distinct().Count() > keys.Length * 0.999);
        Assert.True(keys.Select(key => key.GetHashCode()).Distinct().Count() <= 512);
        var report = PivotEngine.Report(cube, layout, EnUs);
        Assert.Equal("3510", report.ValueAt(report.Rows[^1], 270)!.Text);
    }

    private static double Occupied(int[] hashes, int buckets)
        => (double)hashes.Select(hash => (uint)hash % (uint)buckets).Distinct().Count() / buckets;
}
