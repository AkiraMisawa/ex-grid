using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What of Enter and Tab cycling is C#'s alone: an order outside the enum. The cycling itself, ADR-0012's
/// diagram transcribed, is in the shared cases (tests/cases/selection/cycling.json, ADR-0350).
/// </summary>
public class SelectionCyclingTests
{
    private static readonly GridExtent Grid = new(10, 10);

    [Fact] // ADR-0012: an out-of-range CycleOrder is refused, never silently row-major
    public void An_unknown_cycle_order_is_refused()
    {
        var selection = GridSelection.Empty.Click(new(1, 1), Grid);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => selection.CycleFocus((CycleOrder)2, backward: false, Grid));
    }
}
