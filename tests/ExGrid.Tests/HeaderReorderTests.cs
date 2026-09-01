using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The reorder gesture's arithmetic (ADR-0011/0032): a drag never crosses an edge
/// that carries meaning — the pinned boundary or a Header Group's edge — and the
/// order it hands over moves the grabbed unit whole.
/// </summary>
public class HeaderReorderTests
{
    private static readonly string[] Names = ["TradeId", "CvaBefore", "CvaAfter", "CvaDiff", "FvaBefore", "FvaAfter"];

    private static HeaderGroupLayout Layout(int pinned = 0) => HeaderGroupLayout.Resolve(
        [new HeaderGroup("CVA", ["CvaBefore", "CvaAfter", "CvaDiff"])], Names, pinned);

    [Fact] // ADR-0011: without groups or pinning, every boundary of the block is allowed
    public void A_plain_column_may_drop_at_any_boundary()
    {
        var boundaries = HeaderReorder.AllowedBoundaries(
            HeaderGroupLayout.Empty, columnCount: 4, pinnedCount: 0, unitFirst: 1, unitCount: 1);

        Assert.Equal([0, 1, 2, 3, 4], boundaries);
    }

    [Fact] // ADR-0011: a drag never crosses the pinned boundary, from either side
    public void The_pinned_boundary_clamps_both_blocks()
    {
        Assert.Equal([0, 1, 2], HeaderReorder.AllowedBoundaries(
            HeaderGroupLayout.Empty, 6, pinnedCount: 2, unitFirst: 0, unitCount: 1));
        Assert.Equal([2, 3, 4, 5, 6], HeaderReorder.AllowedBoundaries(
            HeaderGroupLayout.Empty, 6, pinnedCount: 2, unitFirst: 4, unitCount: 1));
    }

    [Fact] // ADR-0032 / HG-14: a leaf inside a group clamps at the group's edge
    public void A_leaf_inside_a_group_clamps_at_its_edges()
    {
        // CvaAfter (index 2) inside CVA (1-3): boundaries 1..4 only.
        Assert.Equal([1, 2, 3, 4], HeaderReorder.AllowedBoundaries(
            Layout(), Names.Length, 0, unitFirst: 2, unitCount: 1));
    }

    [Fact] // ADR-0032: an uncovered leaf moves among top-level units — never into a group
    public void An_uncovered_leaf_treats_a_group_as_one_unit()
    {
        // TradeId (0): boundaries 0, 1 (before CVA), 4 (after CVA), 5, 6 — never 2 or 3.
        Assert.Equal([0, 1, 4, 5, 6], HeaderReorder.AllowedBoundaries(
            Layout(), Names.Length, 0, unitFirst: 0, unitCount: 1));
    }

    [Fact] // ADR-0032: a grabbed group moves among peer units, members as one
    public void A_group_moves_whole_among_its_peers()
    {
        var boundaries = HeaderReorder.AllowedBoundaries(
            Layout(), Names.Length, 0, unitFirst: 1, unitCount: 3);
        Assert.Equal([0, 1, 4, 5, 6], boundaries);

        var order = HeaderReorder.Reorder(Names, unitFirst: 1, unitCount: 3, insertAt: 6);
        Assert.Equal(["TradeId", "FvaBefore", "FvaAfter", "CvaBefore", "CvaAfter", "CvaDiff"], order);
    }

    [Fact] // ADR-0032 / HG-15: the moved unit keeps its members in their current order
    public void Reorder_moves_the_unit_whole()
    {
        Assert.Equal(
            ["CvaBefore", "TradeId", "CvaAfter", "CvaDiff", "FvaBefore", "FvaAfter"],
            HeaderReorder.Reorder(Names, unitFirst: 1, unitCount: 1, insertAt: 0));
        Assert.Equal(
            ["TradeId", "CvaAfter", "CvaBefore", "CvaDiff", "FvaBefore", "FvaAfter"],
            HeaderReorder.Reorder(Names, unitFirst: 2, unitCount: 1, insertAt: 1));
    }
}
