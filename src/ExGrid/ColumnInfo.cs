namespace ExGrid;

/// <summary>
/// The slice of a Column that filtering and sorting need: a name to address it by,
/// the declared type, and the value accessor. Every column carries an accessor —
/// even a Template Column (ADR-0020) — because Filter and Sort read values through it.
///
/// <para><paramref name="IsQueryable"/> is false only for an Action Column, which has no
/// value at all: the engine then refuses to sort or filter on it by name rather than
/// ordering every row by nothing (ADR-0020).</para>
///
/// <para><paramref name="Text"/> is the text the cell displays — the column's format applied —
/// which is what a Find matches (ADR-0047). A Source that searches has to format exactly as
/// the grid does, so the grid hands it the function rather than leaving it to be re-derived.
/// Null where the column displays no value of its own.</para>
/// </summary>
public sealed record ColumnInfo<TRow>(
    string Name, ColumnType Type, Func<TRow, object?> Value, bool IsQueryable = true, Func<TRow, string>? Text = null);
