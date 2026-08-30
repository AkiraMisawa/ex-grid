namespace ExGrid;

/// <summary>
/// The slice of a Column that filtering and sorting need: a name to address it by,
/// the declared type, and the value accessor. Every column carries an accessor —
/// even a Template Column (ADR-0020) — because Filter and Sort read values through it.
/// </summary>
public sealed record ColumnInfo<TRow>(string Name, ColumnType Type, Func<TRow, object?> Value);
