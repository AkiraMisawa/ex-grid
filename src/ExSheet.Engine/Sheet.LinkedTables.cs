using System.Text.RegularExpressions;
using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>A Linked Table as a Sheet knows it (CONTEXT.md, ADR-0049): its name, its columns, its key, and whether its data has arrived.</summary>
/// <param name="Name">The name Formulas use, as declared; matched without regard to case.</param>
/// <param name="Columns">The column names, in order, as declared; matched without regard to case.</param>
/// <param name="IsWaiting">
/// Whether no snapshot is held: none has arrived yet, or the last was refused because a key repeated
/// in it. Every Formula that reads the table shows <c>#GETTING_DATA</c>.
/// </param>
/// <param name="RowCount">How many rows the current snapshot holds; 0 while waiting.</param>
/// <param name="Key">
/// The key column, one of <paramref name="Columns"/> as declared there, or <see langword="null"/> for a
/// table declared without one (ADR-0049, 2026-09-30). No key appears twice in a snapshot of it.
/// </param>
public sealed record LinkedTable(string Name, IReadOnlyList<string> Columns, bool IsWaiting, int RowCount, string? Key = null);

public sealed partial class Sheet
{
    private readonly Dictionary<string, TableState> _tables = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>For each table name, declared or not, the Formula cells that name it in a structured reference.</summary>
    private readonly Dictionary<string, HashSet<CellAddress>> _tableReaders = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The Linked Tables declared on this Sheet, in the order they were declared.</summary>
    public IReadOnlyList<LinkedTable> LinkedTables =>
        [.. _tables.Values.OrderBy(t => t.Order).Select(t => new LinkedTable(t.Name, t.Columns, t.Data is null, t.RowCount, t.Key))];

    /// <summary>
    /// Declares a Linked Table by name and columns (ADR-0049), before any of its rows arrive.
    /// Formulas that read it stop being <c>#NAME?</c> and show <c>#GETTING_DATA</c> until the
    /// first snapshot is pushed. The declaration is recorded in the Sheet Document, and a Sheet
    /// opened from one already has it; a table is never undeclared.
    /// <para>Declaring a table again is how a Consumer that declares at start-up meets a document
    /// that already carries the declaration: the same columns, in the same order, with the same key,
    /// change nothing; other columns or another key replace the declaration — the table's shape is
    /// the Consumer's — and drop the rows held so far, so readers wait again and a Formula naming a
    /// column that is gone reads <c>#REF!</c>.</para>
    /// </summary>
    /// <param name="name">
    /// The name Formulas read it by, as Excel names a Table: a letter, <c>_</c> or <c>\</c>, then
    /// letters, digits, <c>_</c> and <c>.</c> (ASCII); not a cell address, not <c>R</c>, <c>C</c> or an
    /// R1C1 address, not <c>TRUE</c> or <c>FALSE</c>; at most 255 characters.
    /// </param>
    /// <param name="columns">The column names: at least one, none empty, no two the same without regard to case.</param>
    /// <param name="key">
    /// The key column, one of <paramref name="columns"/> named without regard to case, or
    /// <see langword="null"/> for none (ADR-0049, 2026-09-30). A row is read by its key
    /// (<c>XLOOKUP</c>), and every snapshot of a keyed table is checked: one in which a key repeats is
    /// refused (<see cref="PushLinkedTable"/>). A table whose rows are told apart by several columns
    /// is given a column that joins them, and that column is its key.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The name or the columns are not ones a structured reference can name, or the key is not one of
    /// the columns.
    /// </exception>
    public SheetChange DeclareLinkedTable(string name, IReadOnlyList<string> columns, string? key = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(columns);
        if (WhyNotALinkedTable(name, columns) is { } why) throw new ArgumentException(why, IsTableName(name) ? nameof(columns) : nameof(name));
        if (WhyNotAKey(name, columns, key) is { } whyNotKey) throw new ArgumentException(whyNotKey, nameof(key));
        if (_tables.TryGetValue(name, out var held))
        {
            // The same declaration again changes nothing; other columns or another key replace it (ADR-0049).
            if (held.Columns.SequenceEqual(columns, StringComparer.Ordinal) && held.Key == KeyAsDeclared(columns, key)) return SheetChange.None;
            _tables.Remove(name);
            Declare(name, columns, key, held.Order);
        }
        else
        {
            Declare(name, columns, key);
        }
        return RecalculateReaders(name);
    }

    private void Declare(string name, IReadOnlyList<string> columns, string? key, int? order = null)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Count; i++) index[columns[i]] = i;
        _tables[name] = new TableState(name, [.. columns], index, KeyAsDeclared(columns, key), order ?? NextOrder());
    }

    /// <summary>The key column as the columns spell it, so that <c>id</c> and <c>Id</c> declare the same key.</summary>
    private static string? KeyAsDeclared(IReadOnlyList<string> columns, string? key) =>
        key is null ? null : columns.First(column => string.Equals(column, key, StringComparison.OrdinalIgnoreCase));

    private int NextOrder() => _tables.Count == 0 ? 0 : _tables.Values.Max(t => t.Order) + 1;

    /// <summary>Why a name and columns cannot be declared as a Linked Table, or <see langword="null"/> when they can.</summary>
    internal static string? WhyNotALinkedTable(string name, IReadOnlyList<string> columns)
    {
        if (!IsTableName(name)) return $"'{name}' is not a name a Linked Table can have.";
        if (columns.Count == 0) return "A Linked Table has at least one column.";
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            if (string.IsNullOrEmpty(column)) return "A column name is empty.";
            if (!seen.Add(column)) return $"The column '{column}' is declared twice.";
        }
        return null;
    }

    /// <summary>Why a key cannot be declared with a table's columns, or <see langword="null"/> when it can (ADR-0049, 2026-09-30).</summary>
    internal static string? WhyNotAKey(string name, IReadOnlyList<string> columns, string? key) =>
        key is null || columns.Contains(key, StringComparer.OrdinalIgnoreCase) ? null : $"The key '{key}' is not one of the columns of '{name}'.";

    /// <summary>The declarations, as a Sheet Document records them (ADR-0049).</summary>
    private IReadOnlyList<SheetDocumentTable> TableDeclarations =>
        [.. _tables.Values.OrderBy(t => t.Order).Select(t => new SheetDocumentTable(t.Name, t.Columns, t.Key))];

    /// <summary>
    /// Replaces a Linked Table's data with a whole snapshot, in one step (ADR-0049): no
    /// recalculation sees part of one snapshot and part of another, and only the Formulas that
    /// read the table — and what depends on them — recalculate.
    /// <para>A snapshot of a table declared with a key is checked first: no key may appear twice,
    /// compared as <c>XLOOKUP</c>'s exact match compares them, so <c>r-1</c> and <c>R-1</c> are one
    /// key while the number 1 and the text <c>1</c> are two. A blank key is not a key, and any number
    /// of rows may have none; an Error Value matches nothing, and is not compared either.</para>
    /// </summary>
    /// <param name="name">The declared table's name.</param>
    /// <param name="rows">The rows, each holding one Value per declared column in order; <see langword="null"/> is a blank.</param>
    /// <exception cref="InvalidOperationException">No table of that name is declared.</exception>
    /// <exception cref="RepeatedKeyException">
    /// A key repeats in the snapshot. It is refused, and the previous snapshot is not kept: the table
    /// waits again, and every Formula that reads it shows <c>#GETTING_DATA</c>
    /// (<see cref="RepeatedKeyException.Change"/> says which cells changed with it).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A row does not have one Value per column, or holds <c>#GETTING_DATA</c> or <c>#CIRC!</c>, which
    /// are states of a computation and not data. Nothing changes.
    /// </exception>
    public SheetChange PushLinkedTable(string name, IEnumerable<IReadOnlyList<Value?>> rows)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(rows);
        if (!_tables.TryGetValue(name, out var table)) throw new InvalidOperationException($"No Linked Table named '{name}' is declared.");
        var width = table.Columns.Count;
        var columns = new List<Value?>[width];
        for (var c = 0; c < width; c++) columns[c] = [];
        var count = 0;
        foreach (var row in rows)
        {
            if (row is null || row.Count != width) throw new ArgumentException($"Row {count} does not hold one Value for each of the {width} columns of '{table.Name}'.", nameof(rows));
            for (var c = 0; c < width; c++)
            {
                if (row[c] is { IsError: true, Error: ErrorValue.GettingData or ErrorValue.Circ } state)
                {
                    throw new ArgumentException($"{state.Error.ToText()} is not data a Linked Table can hold.", nameof(rows));
                }
                columns[c].Add(row[c]);
            }
            count++;
        }
        if (table.Key is { } key && FirstRepeat(columns[table.ColumnIndex[key]]) is (var first, var second))
        {
            // The table waits again, so its readers show #GETTING_DATA, which IFERROR does not
            // catch. The previous snapshot is not kept: it would show an older value (ADR-0049,
            // 2026-09-30).
            table.Data = null;
            table.RowCount = 0;
            var waiting = RecalculateReaders(table.Name);
            var keys = columns[table.ColumnIndex[key]];
            throw new RepeatedKeyException(table.Name, key, keys[first]!.Value, first, keys[second]!.Value, second, waiting);
        }
        // The snapshot is built whole before it replaces the old one.
        table.Data = [.. columns.Select(c => (IReadOnlyList<Value?>)c.AsReadOnly())];
        table.RowCount = count;
        return RecalculateReaders(table.Name);
    }

    /// <summary>
    /// The first two rows whose keys <c>XLOOKUP</c>'s exact match cannot tell apart, or
    /// <see langword="null"/>. Blank keys and Error Values are passed over: a blank is not a key, and
    /// an Error Value matches nothing.
    /// </summary>
    private static (int First, int Second)? FirstRepeat(IReadOnlyList<Value?> keys)
    {
        var seen = new Dictionary<(ValueKind Kind, double Number, string? Text), int>();
        for (var row = 0; row < keys.Count; row++)
        {
            if (keys[row] is not { IsError: false } key) continue;
            var identity = ExactMatchIdentity(key);
            if (seen.TryGetValue(identity, out var first)) return (first, row);
            seen.Add(identity, row);
        }
        return null;
    }

    /// <summary>
    /// What <c>XLOOKUP</c>'s exact match (<c>SameKindEqual</c>) compares of a Value: its kind, and
    /// then its number, its text in Excel's collation (<see cref="TextOrder"/>, which does not tell
    /// case apart) or its boolean. Two Values match exactly when their identities are equal.
    /// </summary>
    private static (ValueKind Kind, double Number, string? Text) ExactMatchIdentity(Value value) => value.Kind switch
    {
        ValueKind.Text => (ValueKind.Text, 0, TextOrder.EqualityKey(value.Text)),
        ValueKind.Number => (ValueKind.Number, value.Number, null),
        _ => (ValueKind.Boolean, value.Boolean ? 1 : 0, null),
    };

    private SheetChange RecalculateReaders(string name)
    {
        if (!_tableReaders.TryGetValue(name, out var readers) || readers.Count == 0) return SheetChange.None;
        return Recalculate([.. readers], []);
    }

    /// <summary>What a structured reference reads (ADR-0049).</summary>
    private Operand TableColumn(string name, string column)
    {
        if (!_tables.TryGetValue(name, out var table)) return Operand.Of(ErrorValue.Name);
        if (table.Data is null) return Operand.Of(ErrorValue.GettingData);
        return table.ColumnIndex.TryGetValue(column, out var index) ? Operand.Of(table.Data[index]) : Operand.Of(ErrorValue.Ref);
    }

    /// <summary>Whether a Formula reads a declared Linked Table whose first snapshot has not arrived.</summary>
    private bool ReadsWaitingTable(Node node)
    {
        foreach (var reference in node.StructuredReferences)
        {
            if (_tables.TryGetValue(reference.Table, out var table) && table.Data is null) return true;
        }
        return false;
    }

    private void RegisterTableReaders(CellAddress formulaCell, Node formula)
    {
        foreach (var reference in formula.StructuredReferences)
        {
            if (!_tableReaders.TryGetValue(reference.Table, out var set)) _tableReaders[reference.Table] = set = [];
            set.Add(formulaCell);
        }
    }

    private void UnregisterTableReaders(CellAddress formulaCell, Node formula)
    {
        foreach (var reference in formula.StructuredReferences)
        {
            if (_tableReaders.TryGetValue(reference.Table, out var set) && set.Remove(formulaCell) && set.Count == 0) _tableReaders.Remove(reference.Table);
        }
    }

    [GeneratedRegex(@"^[A-Za-z_\\][A-Za-z0-9_.]{0,254}$", RegexOptions.CultureInvariant)]
    private static partial Regex TableNamePattern();

    [GeneratedRegex(@"^[Rr][0-9]*[Cc][0-9]*$|^[RrCc]$", RegexOptions.CultureInvariant)]
    private static partial Regex RowColumnPattern();

    private static bool IsTableName(string name) =>
        TableNamePattern().IsMatch(name)
        && !CellAddress.TryParse(name, out _)
        && !RowColumnPattern().IsMatch(name)
        && !name.Equals("TRUE", StringComparison.OrdinalIgnoreCase)
        && !name.Equals("FALSE", StringComparison.OrdinalIgnoreCase);

    private sealed class TableState(string name, IReadOnlyList<string> columns, Dictionary<string, int> columnIndex, string? key, int order)
    {
        public string Name { get; } = name;

        public IReadOnlyList<string> Columns { get; } = columns;

        public Dictionary<string, int> ColumnIndex { get; } = columnIndex;

        /// <summary>The key column, as <see cref="Columns"/> spells it; <see langword="null"/> for none.</summary>
        public string? Key { get; } = key;

        public int Order { get; } = order;

        /// <summary>The current snapshot, column by column; <see langword="null"/> until the first arrives, and after one is refused.</summary>
        public IReadOnlyList<Value?>[]? Data { get; set; }

        public int RowCount { get; set; }
    }
}
