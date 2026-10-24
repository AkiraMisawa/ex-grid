using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What of the selection's keys is C#'s alone: an allocation, and a cell count past what a JavaScript
/// number holds exactly. The keys themselves are in the shared cases
/// (tests/cases/selection/keyboard.json, ADR-0350).
/// </summary>
public class SelectionKeyboardTests
{
    private static readonly GridExtent Grid = new(100, 26);

    [Fact] // ADR-0011 / SL-1: Ctrl+A over 10^6 x 50 is one rectangle, and making it allocates nothing per cell
    public void SL1_select_all_over_a_million_rows_is_one_rectangle_and_allocates_nothing_per_cell()
    {
        var million = new GridExtent(1_000_000, 50);
        var selection = GridSelection.Empty.Click(new(10, 3), million);
        selection.SelectAll(million); // warm: the first call's JIT is not the selection's allocation

        var before = GC.GetAllocatedBytesForCurrentThread();
        var all = selection.SelectAll(million);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Single(all.Ranges);
        Assert.Equal(new SelectionRange(0, 0, 1_000_000, 50), all.Ranges[0]);
        Assert.Equal(50_000_000L, all.CellCount);
        // 5 x 10^7 cells; a byte per cell would be 50 MB. One selection and its list are a few hundred bytes.
        Assert.True(allocated < 4_096, $"Ctrl+A over 10^6 x 50 allocated {allocated:N0} bytes");
    }

    [Theory] // ADR-0011 / SL-2: there is no cap — selecting the whole result succeeds at any size the extent can name
    // The smaller sizes are in the shared cases; this one's count, about 4.6 × 10^18, is past 2^53.
    [InlineData(int.MaxValue, int.MaxValue)]
    public void SL2_selecting_the_whole_result_succeeds_at_any_size(int rows, int columns)
    {
        var extent = new GridExtent(rows, columns);

        var all = GridSelection.Empty.Click(new(rows - 1, columns - 1), extent).SelectAll(extent);

        Assert.Equal([new SelectionRange(0, 0, rows, columns)], all.Ranges);
        Assert.Equal((long)rows * columns, all.CellCount);
        Assert.Equal(new CellPosition(rows - 1, columns - 1), all.Focus);
    }
}
