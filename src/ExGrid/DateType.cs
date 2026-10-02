namespace ExGrid;

/// <summary>
/// Which of the three permitted date types a Date column holds (ADR-0023, section of 2026-10-02).
/// Declared by the Consumer, like <see cref="ColumnType"/>, and never inferred from values: a cell
/// of another date type is refused naming the column, and a typed filter operand reads as this
/// type or is refused. Not .NET's <see cref="DateTimeKind"/>, which a <see cref="DateTime"/>'s
/// comparison ignores.
/// </summary>
public enum DateType
{
    /// <summary>Values are <see cref="System.DateTime"/>, compared by their wall-clock ticks. The
    /// default for a Date column that declares none.</summary>
    DateTime,

    /// <summary>Values are <see cref="System.DateOnly"/>, compared by their day.</summary>
    DateOnly,

    /// <summary>Values are <see cref="System.DateTimeOffset"/>, compared by the instant they name;
    /// a typed operand needs an explicit offset.</summary>
    DateTimeOffset,
}
