using ExGrid.Clipboard;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The copy rules' shared cases are in <c>tests/cases/clipboard/copy.json</c>; what stays here is what
/// only C# has: a plan's list that a caller could cast back, and a decision's absent half read.
/// </summary>
public class CopyRuleTests
{
    private static readonly GridExtent Grid = new(1_000_000, 50);

    [Fact] // ADR-0005: the plan's segments cannot be cast back to a mutable array
    public void Segments_is_not_a_castable_mutable_array()
    {
        var selection = GridSelection.Empty
            .Click(new(1, 2), Grid).ExtendTo(new(2, 3), Grid)
            .ToggleRange(new(6, 2), Grid).ExtendTo(new(8, 3), Grid);

        Assert.IsNotType<SelectionRange[]>(ClipboardRules.PlanCopy(selection).Plan.Segments);
    }

    [Fact] // ADR-0005: a decision is approved or refused, never both — the absent half refuses to answer
    public void Reading_the_absent_half_of_a_decision_throws()
    {
        var refused = ClipboardRules.PlanCopy(GridSelection.Empty);
        Assert.Throws<InvalidOperationException>(() => refused.Plan);

        var approved = ClipboardRules.PlanCopy(GridSelection.Empty.Click(new(0, 0), Grid));
        Assert.Throws<InvalidOperationException>(() => approved.Reason);
    }
}
