namespace ExGrid.Cells;

/// <summary>
/// A column a Consumer asks the grid to outline, and the colour to outline it in (ADR-0057): a
/// Reference Outline over the column's body, across all its rows. It is how the grid that shows a
/// Linked Table outlines the columns a Formula edited elsewhere reads, in the colours the core told
/// that Formula's Consumer. The colour is one the core handed out, never one the Consumer picks. A
/// column is named by a name; an empty one is refused by name.
/// </summary>
public sealed record OutlinedColumn
{
    /// <summary>A column to outline, and its colour.</summary>
    /// <param name="column">The column's name, as <see cref="GridColumn{TRow}.Name"/> names it
    /// (compared ordinally).</param>
    /// <param name="colour">The colour the References that read it wear.</param>
    /// <exception cref="ArgumentNullException">The name is null.</exception>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    public OutlinedColumn(string column, ReferenceColour colour)
    {
        ArgumentException.ThrowIfNullOrEmpty(column);
        Column = column;
        Colour = colour;
    }

    /// <summary>The column's name, as <see cref="GridColumn{TRow}.Name"/> names it.</summary>
    public string Column { get; }

    /// <summary>The colour its outline is drawn in.</summary>
    public ReferenceColour Colour { get; }
}
