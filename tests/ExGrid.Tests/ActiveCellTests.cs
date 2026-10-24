using ExGrid.Keys;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The keys ADR-0052 added. Its rules as Excel showed them (the Focus stays while a range is extended,
/// and the Extent is the end that moves) are in the shared cases
/// (tests/cases/selection/active-cell.json, ADR-0350).
/// </summary>
public class ActiveCellTests
{

    [Theory] // ADR-0052: the three keys new to ExGrid are the core's, outside editing
    [InlineData(".", true, false, nameof(GridKeyKind.MoveFocusToNextCorner))]
    [InlineData("Backspace", true, false, nameof(GridKeyKind.RevealFocus))]
    [InlineData("Backspace", false, true, nameof(GridKeyKind.CollapseToFocus))]
    public void The_new_keys_resolve(string key, bool ctrl, bool shift, string kind)
    {
        Assert.Equal(Enum.Parse<GridKeyKind>(kind), GridKeys.Resolve(key, ctrl, shift, alt: false, meta: false, metaIsPrimary: false).Kind);
        Assert.Contains(GridKeys.Canonical(key, ctrl, shift, alt: false, meta: false, metaIsPrimary: false), GridKeys.Taken);
    }

    [Fact] // ADR-0052/0010: a bare Backspace is not the core's — it stays the editor's and the page's
    public void A_bare_backspace_is_not_taken()
    {
        Assert.DoesNotContain("Backspace", GridKeys.Taken);
    }
}
