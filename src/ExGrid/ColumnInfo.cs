namespace ExGrid;

/// <summary>
/// The slice of a Column that filtering and sorting need: a name to address it by,
/// the declared type, and the value accessor. Every column carries an accessor —
/// even a Template Column (ADR-0020) — because Filter and Sort read values through it.
///
/// <para><paramref name="IsQueryable"/> is false only for an Action Column, which has no
/// value at all: the engine then refuses to sort or filter on it by name rather than
/// ordering every row by nothing (ADR-0020).</para>
/// </summary>
public sealed record ColumnInfo<TRow>(string Name, ColumnType Type, Func<TRow, object?> Value, bool IsQueryable = true);
