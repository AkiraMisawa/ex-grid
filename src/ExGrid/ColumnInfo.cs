namespace ExGrid;

/// <summary>
/// The slice of a Column that filtering and sorting need: a name to address it by,
/// the declared type, and the value accessor. Every column carries an accessor —
/// even a Template Column (ADR-0020) — because Filter and Sort read values through it.
///
/// <para><paramref name="IsQueryable"/> is false for an Action or Mark Column, which has no
/// value at all, and for a column the Consumer declares not queryable because its source cannot
/// order or filter by it: the engine then refuses to sort or filter on it by name rather than
/// ordering every row by nothing (ADR-0020).</para>
///
/// <para><paramref name="Format"/> is the column's display format, null for the value's own text:
/// a date or a time in one ISO form by its type, anything else its own <c>ToString</c> (ADR-0006,
/// note of 2026-10-01). It travels here so that a Source can answer a Find against the text the user
/// reads (ADR-0055) — through <see cref="TextOf(TRow)"/>, the one place the displayed-text rule is
/// written, which the row paints by too. It is compared by delegate identity like
/// <paramref name="Value"/>, so a column rebuilt from the same declaration is the same column
/// to a Source.</para>
///
/// <para><paramref name="DateType"/> is which date type a Date column's values are, and what its
/// typed filter operands read as (ADR-0023, section of 2026-10-02): a cell or an operand of another
/// date type is refused, naming the column. Meaningless on a column that is not Date.</para>
/// </summary>
public sealed record ColumnInfo<TRow>(
    string Name, ColumnType Type, Func<TRow, object?> Value, bool IsQueryable = true, Func<object, string>? Format = null,
    DateType DateType = DateType.DateTime)
{
    /// <summary>Which date type a Date column's values are. Anything but the default on a column
    /// that is not Date is refused when the slice is built, naming the column: a declaration that
    /// does nothing (ADR-0023, section of 2026-10-02).</summary>
    public DateType DateType { get; init; } = Type == ColumnType.Date || DateType == DateType.DateTime
        ? DateType
        : throw new ArgumentException(
            $"Column '{Name}' ({Type}) declares a date type; only a Date column holds dates (ADR-0023).", nameof(DateType));

    /// <summary>The text a cell of this column displays for a row: empty for a Blank, the
    /// <see cref="Format"/> applied where there is one, the value's own text otherwise — for a
    /// date or a time, its ISO form by type in the invariant culture (ADR-0006, note of
    /// 2026-10-01).</summary>
    public string TextOf(TRow row) => TextFor(Value(row));

    /// <summary>The same text from a value already extracted — the row fetches the value once
    /// and derives both its text and its tone from it.</summary>
    internal string TextFor(object? value)
        => value is null ? "" : Format is { } format ? format(value) : DisplayText.Of(value);

    /// <summary>The entry a cell's editor opens on where the Consumer gives none, which Replace
    /// rewrites too: <see cref="TextOf(TRow)"/>, but a <see cref="double"/> in a column with no
    /// <see cref="Format"/> whole (ADR-0006, note of 2026-10-26).</summary>
    internal string EntryOf(TRow row) => Value(row) switch
    {
        null => "",
        var value when Format is null => DisplayText.EntryOf(value),
        var value => TextFor(value),
    };
}
