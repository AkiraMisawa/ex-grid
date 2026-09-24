namespace ExGrid.Columns;

/// <summary>One declared rectangle resolved against the current column order
/// (ADR-0032): where it starts, how many members it spans, and which tiers.</summary>
/// <param name="Group">The declaration, unchanged — membership is gesture-proof.</param>
/// <param name="FirstColumn">The leftmost member's index in the flat order.</param>
/// <param name="MemberCount">The colspan.</param>
/// <param name="TopTier">The rectangle's top tier, 1 directly above the leaf row.</param>
/// <param name="TierSpan">The rowspan; the rectangle occupies tiers
/// <c>[TopTier − TierSpan + 1, TopTier]</c>.</param>
/// <param name="Pinned">Whether the whole rectangle sits inside the pinned block — a
/// straddle was refused at resolution.</param>
public sealed record ResolvedHeaderGroup(
    HeaderGroup Group, int FirstColumn, int MemberCount, int TopTier, int TierSpan, bool Pinned)
{
    public string Label => Group.Label;

    public int BottomTier => TopTier - TierSpan + 1;
}

/// <summary>
/// The Header Groups resolved against the current column order — once per push, off
/// the render path (ADR-0032). Every refusal is by name at resolution: an unknown
/// member, members no longer adjacent, overlapping rectangles, a rectangle straddling
/// the pinned boundary. A dishonest header over money is the failure this design
/// refuses everywhere.
/// </summary>
public sealed class HeaderGroupLayout
{
    /// <summary>No tiers: the band is the one header row it always was.</summary>
    public static HeaderGroupLayout Empty { get; } = new([], 0, []);

    private readonly int[] _leafTierSpans;

    private HeaderGroupLayout(IReadOnlyList<ResolvedHeaderGroup> groups, int tierCount, int[] leafTierSpans)
    {
        Groups = groups;
        TierCount = tierCount;
        _leafTierSpans = leafTierSpans;
    }

    /// <summary>In declaration order; the positions are the flat order's.</summary>
    public IReadOnlyList<ResolvedHeaderGroup> Groups { get; }

    /// <summary>How many tiers stand above the leaf row. The band is
    /// <c>(1 + TierCount) × HeaderHeight</c> tall (ADR-0032).</summary>
    public int TierCount { get; }

    /// <summary>
    /// How many tiers tall one column's leaf header is: 1 under the lowest rectangle
    /// covering it, more where tiers below that rectangle are uncovered, and the full
    /// band — <c>1 + TierCount</c> — for a column no tier covers, with nothing declared
    /// (ADR-0032).
    /// </summary>
    public int LeafTierSpanOf(int column)
        => TierCount == 0 || column >= _leafTierSpans.Length ? 1 : _leafTierSpans[column];

    /// <summary>
    /// Whether <paramref name="pinnedCount"/> pinned columns would leave every rectangle
    /// wholly on one side of the boundary — the straddle <see cref="Resolve"/> refuses,
    /// asked before it would have to be refused, so that the column menu never offers a
    /// pin the grid would then reject (ADR-0032/0010).
    /// </summary>
    public bool AllowsPinnedCount(int pinnedCount)
    {
        foreach (var group in Groups)
        {
            if (group.FirstColumn < pinnedCount && group.FirstColumn + group.MemberCount > pinnedCount)
                return false;
        }
        return true;
    }

    public static HeaderGroupLayout Resolve(
        IReadOnlyList<HeaderGroup> groups, IReadOnlyList<string> columnNames, int pinnedCount)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(columnNames);
        ArgumentOutOfRangeException.ThrowIfNegative(pinnedCount);
        if (groups.Count == 0)
            return Empty;

        var indexOf = new Dictionary<string, int>(columnNames.Count, StringComparer.Ordinal);
        for (var i = 0; i < columnNames.Count; i++)
            indexOf[columnNames[i]] = i;

        var resolved = new ResolvedHeaderGroup[groups.Count];
        var tierCount = 0;
        for (var g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            var first = int.MaxValue;
            var last = int.MinValue;
            foreach (var member in group.Columns)
            {
                if (!indexOf.TryGetValue(member, out var index))
                {
                    throw new InvalidOperationException(
                        $"Header Group '{group.Label}' names the member column '{member}', which no column " +
                        "carries (ADR-0032).");
                }
                first = Math.Min(first, index);
                last = Math.Max(last, index);
            }
            if (last - first + 1 != group.Columns.Count)
            {
                throw new InvalidOperationException(
                    $"Header Group '{group.Label}' members are not adjacent in the current column order: they " +
                    $"span positions {first}-{last} with columns between them that are not members. A rectangle " +
                    "over non-adjacent columns would label strangers (ADR-0032).");
            }
            if (first < pinnedCount && last >= pinnedCount)
            {
                throw new InvalidOperationException(
                    $"Header Group '{group.Label}' straddles the pinned boundary: members {first}-{last} cross " +
                    $"the first {pinnedCount} pinned columns. Half sticky, half scrolling cannot be drawn " +
                    "honestly (ADR-0032).");
            }
            resolved[g] = new ResolvedHeaderGroup(
                group, first, group.Columns.Count, group.Tier, group.TierSpan, last < pinnedCount);
            tierCount = Math.Max(tierCount, group.Tier);
        }

        for (var a = 0; a < resolved.Length; a++)
        {
            for (var b = a + 1; b < resolved.Length; b++)
            {
                var one = resolved[a];
                var two = resolved[b];
                var columnsMeet = one.FirstColumn <= two.FirstColumn + two.MemberCount - 1
                    && two.FirstColumn <= one.FirstColumn + one.MemberCount - 1;
                var tiersMeet = one.BottomTier <= two.TopTier && two.BottomTier <= one.TopTier;
                if (columnsMeet && tiersMeet)
                {
                    throw new InvalidOperationException(
                        $"Header Groups '{one.Label}' and '{two.Label}' overlap: their columns and tiers both " +
                        "intersect, and one rectangle would paint over the other (ADR-0032).");
                }
            }
        }

        // The lowest covered tier per column decides how far its leaf header stretches:
        // up to that rectangle's underside, or the full band when nothing covers it.
        var leafSpans = new int[columnNames.Count];
        for (var c = 0; c < leafSpans.Length; c++)
        {
            var lowest = int.MaxValue;
            foreach (var group in resolved)
            {
                if (c >= group.FirstColumn && c < group.FirstColumn + group.MemberCount)
                    lowest = Math.Min(lowest, group.BottomTier);
            }
            leafSpans[c] = lowest == int.MaxValue ? 1 + tierCount : lowest;
        }

        return new HeaderGroupLayout(resolved, tierCount, leafSpans);
    }
}
