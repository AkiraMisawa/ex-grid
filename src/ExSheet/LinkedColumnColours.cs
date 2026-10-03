using System.Collections;
using ExGrid.Cells;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// A Linked Table column the Formula being edited reads, and the colour its References wear
/// (ADR-0057, ADR-0049), so that the grid showing the table can outline the column in that colour.
/// The colour is the core's, never the Consumer's.
/// </summary>
/// <param name="Column">The table and the column, named as the Consumer declared them, however
/// the Formula cased them.</param>
/// <param name="Colour">The colour the column's References wear in the editor.</param>
public sealed record LinkedColumnColour(LinkedTableColumn Column, ReferenceColour Colour);

/// <summary>
/// What ExSheet tells its Consumer while a Formula is edited (ADR-0057): each Linked Table column
/// the Formula reads, once, in order of first appearance, with its colour. Empty once the edit ends.
/// </summary>
public sealed class LinkedColumnColours : IReadOnlyList<LinkedColumnColour>
{
    // A type of its own, not IReadOnlyList<LinkedColumnColour>, as the callback's argument: Razor
    // writes a nested type argument without global::, and in a Consumer's page, which imports
    // ExSheet.Components, "ExSheet.LinkedColumnColour" then names a member of the ExSheet
    // component (CS0426), so no method could be bound to the callback.
    private readonly LinkedColumnColour[] _columns;

    internal LinkedColumnColours(IEnumerable<LinkedColumnColour> columns) => _columns = [.. columns];

    /// <summary>No column: what is told when the edit ends.</summary>
    public static LinkedColumnColours None { get; } = new([]);

    /// <summary>The column at <paramref name="index"/>, in order of first appearance.</summary>
    /// <exception cref="IndexOutOfRangeException">The index is outside the list.</exception>
    public LinkedColumnColour this[int index] => _columns[index];

    /// <summary>How many columns the Formula reads.</summary>
    public int Count => _columns.Length;

    /// <summary>The columns, in order of first appearance.</summary>
    public IEnumerator<LinkedColumnColour> GetEnumerator() => ((IEnumerable<LinkedColumnColour>)_columns).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
