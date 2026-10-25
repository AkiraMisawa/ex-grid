namespace ExPivot.Engine;

/// <summary>
/// The most leaves a layout can make, from each field's count of Items (ADR-0370 section 3): the product,
/// over the fields in Rows and Columns, of each field's count less the Items the layout hides of it. The
/// leaves are the combinations of those Items that some record carries, so they are never more while the
/// counts are the data's and every Hidden Item is among them; Filters and the Values add none. The bound
/// is known at once, as a field is dragged, and is a hint for the drop target's mark only: a count gone
/// stale, or a Hidden Item the data no longer holds, moves the mark, never the answer, and the question is
/// asked with <see cref="PivotQuery.MaxLeaves"/> all the same.
/// </summary>
public static class PivotLeafBound
{
    /// <summary>Where a bound stops growing: 2<sup>53</sup> − 1, the largest whole number the
    /// TypeScript engine holds exactly, so both engines say the same bound.</summary>
    public const long Most = 9_007_199_254_740_991;

    /// <summary>The most leaves <paramref name="layout"/> can make, at most <see cref="Most"/>; or null
    /// when a field it places in Rows or Columns was not counted.</summary>
    public static long? Of(PivotLayout layout, PivotItemCounts counts)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(counts);
        long bound = 1;
        foreach (var placement in layout.Rows.Concat(layout.Columns))
        {
            if (counts.CountOf(placement.Field) is not { } count)
                return null;
            var hidden = new HashSet<ItemKey>(placement.HiddenItems.Select(ItemKey.FromPublic)).Count;
            var shown = Math.Max(0, count - hidden);
            bound = shown == 0 ? 0 : bound > Most / shown ? Most : bound * shown;
        }
        return bound;
    }
}
