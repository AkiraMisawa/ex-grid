namespace ExGrid.Cells;

/// <summary>
/// What a press handed over by a grid pointed at from outside landed on (ADR-0058). The grid names
/// the shape and decides nothing about it: what is written for a shape, and which shapes are refused,
/// is the Consumer's.
/// </summary>
public enum GridPointedPressKind
{
    /// <summary>One cell, pressed without Shift: <see cref="GridPointedPress{TRow}.Row"/> and
    /// <see cref="GridPointedPress{TRow}.Column"/> name it.</summary>
    Cell,

    /// <summary>A column's own header, pressed without Shift: <see cref="GridPointedPress{TRow}.Column"/>
    /// names the column.</summary>
    ColumnHeader,

    /// <summary>More than one cell: a Shift+press on a cell, a press on a cell that is dragged onto
    /// another, or a press on a Row Heading or on the corner where the Headings meet.</summary>
    SeveralCells,

    /// <summary>More than one column: a Shift+press on a column's header, or a press on one that is
    /// dragged onto another column.</summary>
    SeveralColumns,

    /// <summary>A Header Group's rectangle (ADR-0032), which stands over several columns.</summary>
    HeaderGroup,
}
