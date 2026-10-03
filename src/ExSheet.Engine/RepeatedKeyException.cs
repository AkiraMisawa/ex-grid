using System.Globalization;

namespace ExSheet.Engine;

/// <summary>
/// Thrown when a snapshot of a Linked Table declared with a key is refused because a key repeats in
/// it (ADR-0049, 2026-09-30): two rows whose keys <c>XLOOKUP</c>'s exact match cannot tell apart,
/// so a Formula reading one of them by key would read the first without a word. Case does not tell
/// keys apart, and a blank key is not a key.
/// <para>The snapshot is not taken, and the previous one is not kept, because it would show an older
/// value: the table waits again, and every Formula that reads it shows <c>#GETTING_DATA</c>, which
/// <c>IFERROR</c> does not catch, until a snapshot in which no key repeats is pushed.</para>
/// </summary>
public sealed class RepeatedKeyException : ArgumentException
{
    internal RepeatedKeyException(
        string table, IReadOnlyList<string> keyColumns, IReadOnlyList<Value> keys, int firstRow, IReadOnlyList<Value> repeats, int secondRow, SheetChange change)
        : base(Describe(table, keyColumns, keys, firstRow, repeats, secondRow), "rows")
    {
        Table = table;
        KeyColumns = keyColumns;
        Keys = keys;
        KeyColumn = string.Join(", ", keyColumns);
        Key = keys[0];
        FirstRow = firstRow;
        SecondRow = secondRow;
        Change = change;
    }

    /// <summary>The table's name, as declared.</summary>
    public string Table { get; }

    /// <summary>The key column's name, as declared; for a key of several columns, their names joined by <c>, </c>.</summary>
    public string KeyColumn { get; }

    /// <summary>The key that repeats, as the first of its rows holds it; for a key of several columns, its first column's Value (<see cref="Keys"/> holds them all).</summary>
    public Value Key { get; }

    /// <summary>The key's columns, as declared: one, or several for a key of several columns (ADR-0058, amended 2026-10-03).</summary>
    public IReadOnlyList<string> KeyColumns { get; }

    /// <summary>The key that repeats, one Value per key column, as the first of its rows holds it.</summary>
    public IReadOnlyList<Value> Keys { get; }

    /// <summary>The first row of the snapshot, from 0, that holds the key.</summary>
    public int FirstRow { get; }

    /// <summary>The row of the snapshot, from 0, that holds it again.</summary>
    public int SecondRow { get; }

    /// <summary>
    /// What the table's going back to waiting changed: its readers, and what depends on them, now
    /// show <c>#GETTING_DATA</c>. A component repaints the rows it names, as after any change.
    /// </summary>
    public SheetChange Change { get; }

    private static string Describe(string table, IReadOnlyList<string> keyColumns, IReadOnlyList<Value> keys, int firstRow, IReadOnlyList<Value> repeats, int secondRow)
    {
        var key = string.Join(", ", keys.Select(Written));
        var repeat = string.Join(", ", repeats.Select(Written));
        var held = keys.SequenceEqual(repeats)
            ? $"holds {key} in rows {firstRow} and {secondRow}"
            : $"holds {key} in row {firstRow} and {repeat} in row {secondRow}, which {(keys.Count == 1 ? "XLOOKUP" : "its lookup")} does not tell apart";
        var columns = keyColumns.Count == 1 ? $"its key column '{keyColumns[0]}'" : $"its key columns {string.Join(", ", keyColumns.Select(c => $"'{c}'"))}";
        return $"The snapshot of '{table}' is refused: {columns} {held}. The table waits for a snapshot in which no key repeats, and every Formula that reads it shows #GETTING_DATA.";
    }

    // As a Formula writes the constant (ADR-0058): text quoted, a number invariant, a boolean in capitals.
    private static string Written(Value key) => key.Kind switch
    {
        ValueKind.Text => "\"" + key.Text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"",
        ValueKind.Number => key.Number.ToString("R", CultureInfo.InvariantCulture),
        _ => key.Boolean ? "TRUE" : "FALSE",
    };
}
