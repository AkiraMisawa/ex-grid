using System.Text.RegularExpressions;
using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>A Linked Table as a Sheet knows it (CONTEXT.md, ADR-0049): its name, its columns, and whether its data has arrived.</summary>
/// <param name="Name">The name Formulas use, as declared; matched without regard to case.</param>
/// <param name="Columns">The column names, in order, as declared; matched without regard to case.</param>
/// <param name="IsWaiting">
/// Whether no snapshot has arrived yet: every Formula that reads the table shows <c>#GETTING_DATA</c>.
/// </param>
/// <param name="RowCount">How many rows the current snapshot holds; 0 while waiting.</param>
public sealed record LinkedTable(string Name, IReadOnlyList<string> Columns, bool IsWaiting, int RowCount);

public sealed partial class Sheet
{
    private readonly Dictionary<string, TableState> _tables = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>For each table name, declared or not, the Formula cells that name it in a structured reference.</summary>
    private readonly Dictionary<string, HashSet<CellAddress>> _tableReaders = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The Linked Tables declared on this Sheet, in the order they were declared.</summary>
    public IReadOnlyList<LinkedTable> LinkedTables =>
        [.. _tables.Values.OrderBy(t => t.Order).Select(t => new LinkedTable(t.Name, t.Columns, t.Data is null, t.RowCount))];

    /// <summary>
    /// Declares a Linked Table by name and columns (ADR-0049), before any of its rows arrive.
    /// Formulas that read it stop being <c>#NAME?</c> and show <c>#GETTING_DATA</c> until the
    /// first snapshot is pushed.
    /// </summary>
    /// <param name="name">
    /// The name Formulas read it by, as Excel names a Table: a letter, <c>_</c> or <c>\</c>, then
    /// letters, digits, <c>_</c> and <c>.</c> (ASCII); not a cell address, not <c>R</c>, <c>C</c> or an
    /// R1C1 address, not <c>TRUE</c> or <c>FALSE</c>; at most 255 characters.
    /// </param>
    /// <param name="columns">The column names: at least one, none empty, no two the same without regard to case.</param>
    /// <exception cref="ArgumentException">The name or the columns are not ones a structured reference can name.</exception>
    /// <exception cref="InvalidOperationException">A table of that name is already declared.</exception>
    public SheetChange DeclareLinkedTable(string name, IReadOnlyList<string> columns)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(columns);
        if (!IsTableName(name)) throw new ArgumentException($"'{name}' is not a name a Linked Table can have.", nameof(name));
        if (columns.Count == 0) throw new ArgumentException("A Linked Table has at least one column.", nameof(columns));
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Count; i++)
        {
            if (string.IsNullOrEmpty(columns[i])) throw new ArgumentException("A column name is empty.", nameof(columns));
            if (!index.TryAdd(columns[i], i)) throw new ArgumentException($"The column '{columns[i]}' is declared twice.", nameof(columns));
        }
        if (_tables.ContainsKey(name)) throw new InvalidOperationException($"A Linked Table named '{name}' is already declared.");
        _tables[name] = new TableState(name, [.. columns], index, _tables.Count);
        return RecalculateReaders(name);
    }

    /// <summary>
    /// Replaces a Linked Table's data with a whole snapshot, in one step (ADR-0049): no
    /// recalculation sees part of one snapshot and part of another, and only the Formulas that
    /// read the table — and what depends on them — recalculate.
    /// </summary>
    /// <param name="name">The declared table's name.</param>
    /// <param name="rows">The rows, each holding one Value per declared column in order; <see langword="null"/> is a blank.</param>
    /// <exception cref="InvalidOperationException">No table of that name is declared.</exception>
    /// <exception cref="ArgumentException">
    /// A row does not have one Value per column, or holds <c>#GETTING_DATA</c> or <c>#CIRC!</c>, which
    /// are states of a computation and not data.
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
        // The snapshot is built whole before it replaces the old one.
        table.Data = [.. columns.Select(c => (IReadOnlyList<Value?>)c.AsReadOnly())];
        table.RowCount = count;
        return RecalculateReaders(table.Name);
    }

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

    private sealed class TableState(string name, IReadOnlyList<string> columns, Dictionary<string, int> columnIndex, int order)
    {
        public string Name { get; } = name;

        public IReadOnlyList<string> Columns { get; } = columns;

        public Dictionary<string, int> ColumnIndex { get; } = columnIndex;

        public int Order { get; } = order;

        /// <summary>The current snapshot, column by column; <see langword="null"/> until the first arrives.</summary>
        public IReadOnlyList<Value?>[]? Data { get; set; }

        public int RowCount { get; set; }
    }
}
