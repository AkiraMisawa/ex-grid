using ExGrid.Selection;
using ExGrid.Summarizing;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The Selection Summary's bands (ADR-0130), found by sweeping the ranges' edges, are what the definition
/// answers read one band at a time, over random ranges that overlap, touch, repeat and reach past the rows and
/// columns. The sweep exists for a Selection of many ranges, which a Consumer places since ADR-0050 item 4's
/// note of 2026-11-12: walking every range for every band cost the square of their number. The C# half of
/// <c>packages/core/test/summary-bands-property.test.ts</c>. The seed is fixed, and every failure names the sample.
/// </summary>
public class SummaryBandsPropertyTests
{
    private const int Seed = 20261112;
    private const int Samples = 400;

    /// <summary>The definition: a band between each two neighbouring edges, with the columns of every range that covers it.</summary>
    private static List<(int Top, int Bottom, int[] Columns)> BandsByDefinition(IReadOnlyList<SelectionRange> ranges, int rowCount, int columnCount)
    {
        var edges = new SortedSet<int>();
        foreach (var range in ranges)
        {
            var top = Math.Clamp(range.TopRow, 0, rowCount);
            var bottom = Math.Clamp(range.TopRow + range.RowCount, 0, rowCount);
            if (top < bottom)
            {
                edges.Add(top);
                edges.Add(bottom);
            }
        }
        var bands = new List<(int, int, int[])>();
        int? previous = null;
        foreach (var edge in edges)
        {
            if (previous is { } top)
            {
                var columns = new SortedSet<int>();
                foreach (var range in ranges.Where(range => range.TopRow <= top && range.TopRow + range.RowCount > top))
                {
                    var right = Math.Min(range.LeftColumn + range.ColumnCount, columnCount);
                    for (var column = Math.Max(range.LeftColumn, 0); column < right; column++)
                        columns.Add(column);
                }
                if (columns.Count > 0)
                    bands.Add((top, edge, [.. columns]));
            }
            previous = edge;
        }
        return bands;
    }

    private static string Written(IEnumerable<(int Top, int Bottom, int[] Columns)> bands) =>
        string.Join(" ", bands.Select(band => $"{band.Top}-{band.Bottom}:[{string.Join(",", band.Columns)}]"));

    [Fact] // ADR-0130: the sweep answers the bands the definition does, for ranges that overlap, touch, repeat and reach outside
    public void The_sweep_answers_the_bands_the_definition_does()
    {
        var random = new Random(Seed);
        for (var sample = 0; sample < Samples; sample++)
        {
            var rowCount = random.Next(0, 13);
            var columnCount = random.Next(0, 7);
            // Past the rows and the columns on purpose: such cells are no cells, and are summarised nowhere.
            var ranges = Enumerable.Range(0, random.Next(0, 9))
                .Select(_ => new SelectionRange(random.Next(15), random.Next(8), random.Next(1, 6), random.Next(1, 5)))
                .ToList();
            Assert.True(
                Written(BandsByDefinition(ranges, rowCount, columnCount)) == Written(GridSummary.Bands(ranges, rowCount, columnCount)),
                $"sample {sample}: {string.Join(" ", ranges)} over {rowCount} rows × {columnCount} columns gave "
                + $"{Written(GridSummary.Bands(ranges, rowCount, columnCount))}, not {Written(BandsByDefinition(ranges, rowCount, columnCount))}");
        }
    }

    [Fact] // ADR-0130: rows that do not stand together are a band each, and the rows between them are in none
    public void Rows_that_do_not_stand_together_are_a_band_each()
    {
        int[] tops = [0, 2, 4, 7];
        var bands = GridSummary.Bands([.. tops.Select(top => new SelectionRange(top, 0, 1, 3))], 100, 3);
        Assert.Equal("0-1:[0,1,2] 2-3:[0,1,2] 4-5:[0,1,2] 7-8:[0,1,2]", Written(bands));
    }

    [Fact] // ADR-0130: each band's columns are its own, so a caller that writes to one changes no other band
    public void Each_bands_columns_are_its_own()
    {
        // The second range lies inside the first, so the three bands hold the same columns.
        var bands = GridSummary.Bands([new SelectionRange(0, 0, 3, 2), new SelectionRange(1, 0, 1, 2)], 10, 2).ToList();
        Assert.Equal("0-1:[0,1] 1-2:[0,1] 2-3:[0,1]", Written(bands));
        bands[0].Columns[0] = 9;
        Assert.Equal("0-1:[9,1] 1-2:[0,1] 2-3:[0,1]", Written(bands));
    }
}
