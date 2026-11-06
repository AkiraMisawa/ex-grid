namespace ExGrid;

/// <summary>
/// The check that a list of rows holds none twice (ADR-0003, ADR-0141): without a Row Key a row is told from
/// every other by its instance, so a null row, or one instance at two places, would leave two places nobody
/// could tell apart, and is refused by name, naming the positions. The Script Core's <c>distinct-rows.ts</c>
/// says the same in the same words.
/// </summary>
internal static class DistinctRows
{
    /// <summary>Refuses a null row in <paramref name="rows"/>, or an instance that stands twice.</summary>
    internal static void Refuse<TRow>(IReadOnlyList<TRow> rows, string paramName)
    {
        var seen = new Dictionary<object, int>(rows.Count, ReferenceEqualityComparer.Instance);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i] ?? throw Missing(i, paramName);
            if (seen.TryGetValue(row, out var first))
                throw Repeated(first, i, paramName);
            seen.Add(row, i);
        }
    }

    /// <summary>The refusal of a null row at <paramref name="position"/>.</summary>
    internal static ArgumentException Missing(int position, string paramName)
        => new($"The row at position {position} is null. Every row of a Window is a row the grid can name; a missing one "
            + "cannot be painted, selected or copied (ADR-0003).", paramName);

    /// <summary>The refusal of the row at <paramref name="position"/>, the instance already at <paramref name="first"/>.</summary>
    internal static ArgumentException Repeated(int first, int position, string paramName)
        => new($"The rows at positions {first} and {position} are the same object. Without a Row Key a row is told from "
            + "every other by its identity, so the grid could not tell the two apart: hand over a copy, or name the rows by a "
            + "Row Key (ADR-0003, ADR-0140).", paramName);
}
