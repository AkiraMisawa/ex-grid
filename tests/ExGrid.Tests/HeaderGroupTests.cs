using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// Header Groups are declared rectangles over the leaf columns (ADR-0032): membership
/// is a set resolved against the flat order, and every impossible declaration is
/// refused by name at resolution.
/// </summary>
public class HeaderGroupTests
{
    private static readonly string[] Names = ["TradeId", "CvaBefore", "CvaAfter", "CvaDiff", "FvaBefore", "FvaAfter"];

    [Fact] // ADR-0032 / HG-1: membership is a set — the painted order is the flat order's
    public void Members_resolve_in_the_flat_order_not_the_declarations()
    {
        var layout = HeaderGroupLayout.Resolve(
            [new HeaderGroup("CVA", ["CvaDiff", "CvaBefore", "CvaAfter"])], Names, pinnedCount: 0);

        var group = Assert.Single(layout.Groups);
        Assert.Equal(1, group.FirstColumn);
        Assert.Equal(3, group.MemberCount);
        Assert.Equal(1, layout.TierCount);
    }

    [Fact] // ADR-0032 / HG-4: a member name no column carries is refused, naming it
    public void An_unknown_member_is_refused_by_name()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => HeaderGroupLayout.Resolve(
            [new HeaderGroup("CVA", ["CvaBefore", "Nonsense"])], Names, 0));

        Assert.Contains("Nonsense", refusal.Message);
        Assert.Contains("CVA", refusal.Message);
    }

    [Fact] // ADR-0032 / HG-5: members not adjacent in the current order are refused, naming adjacency
    public void Non_adjacent_members_are_refused()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => HeaderGroupLayout.Resolve(
            [new HeaderGroup("CVA", ["CvaBefore", "CvaDiff"])], Names, 0));

        Assert.Contains("adjacent", refusal.Message);
    }

    [Fact] // ADR-0032 / HG-6: overlapping rectangles are refused
    public void Overlapping_rectangles_are_refused()
    {
        Assert.Throws<InvalidOperationException>(() => HeaderGroupLayout.Resolve(
            [
                new HeaderGroup("CVA", ["CvaBefore", "CvaAfter", "CvaDiff"]),
                new HeaderGroup("Also", ["CvaAfter", "CvaDiff"]),
            ], Names, 0));

        // Different tiers do not overlap: a tier-2 band over the same columns stands.
        var layered = HeaderGroupLayout.Resolve(
            [
                new HeaderGroup("CVA", ["CvaBefore", "CvaAfter", "CvaDiff"]),
                new HeaderGroup("Adjustments", ["CvaBefore", "CvaAfter", "CvaDiff", "FvaBefore", "FvaAfter"], tier: 2),
            ], Names, 0);
        Assert.Equal(2, layered.TierCount);
    }

    [Fact] // ADR-0032 / HG-7: a rectangle straddling the pinned boundary is refused, naming the boundary
    public void A_pinned_straddle_is_refused()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => HeaderGroupLayout.Resolve(
            [new HeaderGroup("CVA", ["CvaBefore", "CvaAfter"])], Names, pinnedCount: 2));

        Assert.Contains("pinned boundary", refusal.Message);

        // Entirely inside the pinned block is fine, and says so.
        var pinned = HeaderGroupLayout.Resolve(
            [new HeaderGroup("Keys", ["TradeId", "CvaBefore"])], Names, pinnedCount: 2);
        Assert.True(pinned.Groups[0].Pinned);
    }

    [Fact] // ADR-0032 / ADR-0010: a pinned count is asked about before it is refused — only a boundary through a group is
    public void A_pinned_count_through_a_group_is_not_allowed()
    {
        var layout = HeaderGroupLayout.Resolve(
            [new HeaderGroup("Who", ["Book", "Trader"]), new HeaderGroup("What", ["Notional", "Narrow"])],
            ["Id", "Book", "Trader", "Notional", "Narrow", "Date"], pinnedCount: 0);

        Assert.True(layout.AllowsPinnedCount(0));
        Assert.True(layout.AllowsPinnedCount(1));
        Assert.False(layout.AllowsPinnedCount(2));  // between Book and Trader
        Assert.True(layout.AllowsPinnedCount(3));
        Assert.False(layout.AllowsPinnedCount(4));  // between Notional and Narrow
        Assert.True(layout.AllowsPinnedCount(6));
        Assert.True(HeaderGroupLayout.Empty.AllowsPinnedCount(2));
    }

    [Fact] // ADR-0032 / HG-3: a column no tier covers stretches its leaf the full band
    public void An_uncovered_column_stretches_the_full_band()
    {
        var layout = HeaderGroupLayout.Resolve(
            [
                new HeaderGroup("CVA", ["CvaBefore", "CvaAfter", "CvaDiff"]),
                new HeaderGroup("Adjustments", ["CvaBefore", "CvaAfter", "CvaDiff", "FvaBefore", "FvaAfter"], tier: 2),
            ], Names, 0);

        Assert.Equal(3, layout.LeafTierSpanOf(0));  // TradeId: the full three-tier band
        Assert.Equal(1, layout.LeafTierSpanOf(1));  // CvaBefore: covered from tier 1
        Assert.Equal(2, layout.LeafTierSpanOf(4));  // FvaBefore: covered from tier 2 only
    }

    [Fact] // ADR-0032: a declaration that cannot be a rectangle is refused at construction
    public void Malformed_declarations_are_refused_at_construction()
    {
        Assert.Throws<ArgumentException>(() => new HeaderGroup("X", []));
        Assert.Throws<ArgumentException>(() => new HeaderGroup("X", ["A", "A"]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeaderGroup("X", ["A"], tier: 0));
        // Spanning below the leaf row cannot be drawn.
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeaderGroup("X", ["A"], tier: 1, tierSpan: 2));
    }

    [Fact] // ADR-0032 / HG-15: the declaration survives resolution unchanged — membership is gesture-proof
    public void The_declaration_is_not_rewritten_by_resolution()
    {
        var declaration = new HeaderGroup("CVA", ["CvaDiff", "CvaBefore", "CvaAfter"]);

        var layout = HeaderGroupLayout.Resolve([declaration], Names, 0);

        Assert.Same(declaration, layout.Groups[0].Group);
        Assert.Equal(["CvaDiff", "CvaBefore", "CvaAfter"], declaration.Columns);
    }
}
