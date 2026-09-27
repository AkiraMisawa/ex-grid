using ExGrid.Clipboard;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The fill handle's gesture, pure (ADR-0050, item 5): where the handle stands, which axis a
/// drag extends along, what a release would fill, and the Editable gate before any intent
/// (ADR-0035). The meaning of a fill is the Consumer's and is not here.
/// </summary>
public class FillRuleTests
{
    private static readonly GridExtent Grid = new(100, 10);

    private static readonly Func<int, bool> Editable = _ => true;

    // Rows 2-3, columns 1-2.
    private static readonly SelectionRange Source = new(2, 1, 2, 2);

    [Fact] // ADR-0050 item 5 / DC-12: the handle stands on the Selection's one range
    public void The_handle_stands_on_the_one_range()
    {
        var selection = GridSelection.Empty.Click(new(2, 1), Grid).ExtendTo(new(3, 2), Grid);

        Assert.Equal(Source, FillRules.HandleRange(selection));
    }

    [Fact] // ADR-0050 item 5 / DC-12: a disjoint Selection shows no handle, and nor does an empty one
    public void A_disjoint_or_empty_selection_has_no_handle()
    {
        var disjoint = GridSelection.Empty.Click(new(0, 0), Grid).ToggleRange(new(5, 5), Grid);

        Assert.Null(FillRules.HandleRange(disjoint));
        Assert.Null(FillRules.HandleRange(GridSelection.Empty));
    }

    [Fact] // ADR-0050 item 5: a pointer inside the source reaches nothing
    public void A_pointer_inside_the_source_reaches_nothing()
    {
        Assert.Null(FillRules.ExtensionFor(Source, new(3, 2)));
        Assert.Null(FillRules.PlanFill(Source, new(2, 1), Editable));
    }

    [Fact] // ADR-0050 item 5 / DC-13: down — the rows below, as wide as the source, never the source itself
    public void Dragging_down_fills_the_rows_below()
    {
        var extension = FillRules.ExtensionFor(Source, new(6, 2));

        Assert.Equal(new FillExtension(new SelectionRange(4, 1, 3, 2), GridDirection.Down), extension);
    }

    [Fact] // ADR-0050 item 5 / DC-13: up, right and left each extend the same way
    public void Each_direction_extends_beside_the_source()
    {
        Assert.Equal(new FillExtension(new SelectionRange(0, 1, 2, 2), GridDirection.Up),
            FillRules.ExtensionFor(Source, new(0, 1)));
        Assert.Equal(new FillExtension(new SelectionRange(2, 3, 2, 3), GridDirection.Right),
            FillRules.ExtensionFor(Source, new(3, 5)));
        Assert.Equal(new FillExtension(new SelectionRange(2, 0, 2, 1), GridDirection.Left),
            FillRules.ExtensionFor(Source, new(2, 0)));
    }

    [Fact] // ADR-0050 item 5 / DC-13: one axis only — the larger displacement decides
    public void The_larger_displacement_decides_the_axis()
    {
        // Three rows below and one column right: down, and only down.
        Assert.Equal(new FillExtension(new SelectionRange(4, 1, 3, 2), GridDirection.Down),
            FillRules.ExtensionFor(Source, new(6, 3)));
        // One row below and four columns right: right, and only right.
        Assert.Equal(new FillExtension(new SelectionRange(2, 3, 2, 4), GridDirection.Right),
            FillRules.ExtensionFor(Source, new(4, 6)));
    }

    [Fact] // ADR-0050 item 5: a tie goes to the vertical fill
    public void A_tie_fills_vertically()
    {
        Assert.Equal(GridDirection.Down, FillRules.ExtensionFor(Source, new(5, 4))!.Value.Direction);
        Assert.Equal(GridDirection.Up, FillRules.ExtensionFor(Source, new(0, 0))!.Value.Direction);
    }

    [Fact] // ADR-0050 item 5 / DC-13: an approved release carries the extension it reached
    public void An_approved_fill_carries_target_and_direction()
    {
        var decision = FillRules.PlanFill(Source, new(9, 1), Editable);

        Assert.NotNull(decision);
        Assert.False(decision.IsRefused);
        Assert.Equal(new FillExtension(new SelectionRange(4, 1, 6, 2), GridDirection.Down), decision.Extension);
        Assert.Throws<InvalidOperationException>(() => decision.Reason);
    }

    [Fact] // ADR-0050 item 5 / ADR-0035 / DC-14: a target covering a non-editable column is refused whole
    public void A_target_covering_a_non_editable_column_is_refused_whole()
    {
        var decision = FillRules.PlanFill(Source, new(2, 4), column => column != 4);

        Assert.NotNull(decision);
        Assert.True(decision.IsRefused);
        Assert.Equal(PasteRefusalReason.TargetNotEditable, decision.Reason);
        Assert.Throws<InvalidOperationException>(() => decision.Extension);
    }

    [Fact] // ADR-0035: the target is judged, not the source — a fill down a locked source column is still refused
    public void Editable_is_judged_on_the_target_columns()
    {
        // Dragging right into editable columns from a source in a locked column: the write
        // lands only in the target, so it goes through.
        Assert.False(FillRules.PlanFill(Source, new(2, 5), column => column >= 3)!.IsRefused);
        // Dragging down keeps the source's columns, so a locked one among them refuses.
        Assert.Equal(PasteRefusalReason.TargetNotEditable,
            FillRules.PlanFill(Source, new(8, 1), column => column != 2)!.Reason);
    }
}
