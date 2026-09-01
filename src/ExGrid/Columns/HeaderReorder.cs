namespace ExGrid.Columns;

/// <summary>
/// The reorder gesture's arithmetic (ADR-0011/0032): what you grab is the unit that
/// moves, and a drag never crosses an edge that carries meaning — the pinned boundary,
/// or a Header Group's edge. Pure, so the clamping is pinned in layer 1 and the
/// component only translates pixels to columns.
/// </summary>
public static class HeaderReorder
{
    /// <summary>
    /// The insertion boundaries a grabbed unit may drop at, as column indices
    /// (0 … columnCount). A leaf inside a group clamps at the group's edges; a group
    /// moves among its peer units; everything clamps at the pinned boundary. The
    /// grabbed unit's own edges are included — dropping there is the no-op drag.
    /// </summary>
    public static IReadOnlyList<int> AllowedBoundaries(
        HeaderGroupLayout layout, int columnCount, int pinnedCount,
        int unitFirst, int unitCount)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentOutOfRangeException.ThrowIfNegative(unitFirst);
        ArgumentOutOfRangeException.ThrowIfLessThan(unitCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(unitFirst + unitCount, columnCount);

        var unitEnd = unitFirst + unitCount;

        // The pinned boundary first (ADR-0011): position and pinned-ness are the same
        // fact, so a drop across it would change a column the user never touched.
        var blockStart = unitFirst < pinnedCount ? 0 : pinnedCount;
        var blockEnd = unitFirst < pinnedCount ? pinnedCount : columnCount;

        // Then the smallest group strictly containing the unit: a leaf clamps at its
        // group's edge, a group at its container's (ADR-0032).
        foreach (var group in layout.Groups)
        {
            var start = group.FirstColumn;
            var end = group.FirstColumn + group.MemberCount;
            if (start <= unitFirst && unitEnd <= end && !(start == unitFirst && end == unitEnd)
                && start >= blockStart && end <= blockEnd)
            {
                blockStart = Math.Max(blockStart, start);
                blockEnd = Math.Min(blockEnd, end);
            }
        }

        // Peer units inside the block: the outermost group spans, and each uncovered
        // column on its own. A boundary is any edge between peers — never the middle
        // of one, which would split it.
        var boundaries = new List<int> { blockStart };
        var column = blockStart;
        while (column < blockEnd)
        {
            var next = column + 1;
            foreach (var group in layout.Groups)
            {
                var start = group.FirstColumn;
                var end = group.FirstColumn + group.MemberCount;
                // Outermost within the block, covering this column — but never the
                // container itself, whose span is the whole block.
                if (start <= column && column < end && start >= blockStart && end <= blockEnd
                    && !(start == blockStart && end == blockEnd))
                {
                    next = Math.Max(next, end);
                }
            }
            boundaries.Add(next);
            column = next;
        }
        return boundaries;
    }

    /// <summary>The order after dropping the unit at one allowed boundary: the block
    /// moves whole, members in their current order — membership never changes by
    /// gesture (ADR-0032).</summary>
    public static string[] Reorder(
        IReadOnlyList<string> names, int unitFirst, int unitCount, int insertAt)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentOutOfRangeException.ThrowIfNegative(unitFirst);
        ArgumentOutOfRangeException.ThrowIfLessThan(unitCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(unitFirst + unitCount, names.Count);
        ArgumentOutOfRangeException.ThrowIfNegative(insertAt);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(insertAt, names.Count);

        var result = new string[names.Count];
        var unit = new string[unitCount];
        for (var i = 0; i < unitCount; i++)
            unit[i] = names[unitFirst + i];

        // The insertion point in the list with the unit removed.
        var target = insertAt <= unitFirst ? insertAt
            : insertAt >= unitFirst + unitCount ? insertAt - unitCount
            : unitFirst;

        var write = 0;
        for (var read = 0; read < names.Count; read++)
        {
            if (read >= unitFirst && read < unitFirst + unitCount)
                continue;
            if (write == target)
                write += unitCount;
            result[write++] = names[read];
        }
        for (var i = 0; i < unitCount; i++)
            result[target + i] = unit[i];
        return result;
    }
}
