namespace ExGrid.Docs.Examples.Grid;

using ExGrid.Columns;

/// <summary>The whole column order of an Example that holds hidden columns while the user reorders the rest.</summary>
public static class ColumnOrder
{
    /// <summary>
    /// The whole order after the grid reported a new order of the columns it shows. Each hidden column keeps its
    /// place inside the Header Groups it belongs to, beside a column of its own group, so that showing it again puts
    /// it where the groups still stand together: a column dropped at the end would split its group, which the grid
    /// refuses. <paramref name="was"/> is the order before, every column in it; <paramref name="groups"/> are every
    /// group, hidden members included.
    /// </summary>
    public static List<string> Reordered(IReadOnlyList<string> was, IReadOnlyList<string> shown, IReadOnlyList<HeaderGroup> groups)
    {
        var order = shown.ToList();
        // The groups a column moves with, innermost first, then the whole grid.
        List<IReadOnlyList<string>> Nested(string name) =>
        [
            .. groups.Where(g => g.Columns.Contains(name)).OrderBy(g => g.Columns.Count).Select(g => g.Columns),
            was,
        ];
        // What moves as one inside `around`: the largest group of `name` within it, or the column alone.
        IReadOnlyList<string> UnitOf(string name, IReadOnlyList<string> around) =>
            Nested(name).LastOrDefault(g => g.Count < around.Count && g.All(around.Contains)) ?? [name];
        for (var at = 0; at < was.Count; at++)
        {
            var name = was[at];
            if (order.Contains(name))
                continue;
            var placed = false;
            foreach (var around in Nested(name))
            {
                bool Kin(string other) => order.Contains(other) && around.Contains(other);
                if (was.Take(at).LastOrDefault(Kin) is { } before)
                {
                    order.Insert(UnitOf(before, around).Max(order.IndexOf) + 1, name);
                    placed = true;
                    break;
                }
                if (was.Skip(at + 1).FirstOrDefault(Kin) is { } after)
                {
                    order.Insert(UnitOf(after, around).Where(order.Contains).Min(order.IndexOf), name);
                    placed = true;
                    break;
                }
            }
            // Nothing else is shown: the column stands alone.
            if (!placed)
                order.Add(name);
        }
        return order;
    }
}
