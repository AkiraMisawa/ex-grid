using System.Text.Json;
using ExGrid.Cells;
using ExGrid.Rows;

namespace ExGrid.Components;

/// <summary>
/// The rows a Blazor ExGrid sent to its Script Core, by the id each crossed with (ADR-0350 section 15). The grid
/// hands a row back by its id, in an edit, an action or a refusal, so a row is kept for as long as the browser may
/// still hand it back: until the bridge says no object of it is left there, and only if no message sent after the
/// last one the bridge had read when it said so carries it again. The same row is the same id while it is sent,
/// so the bridge keeps the object it made of it and the core skips it (ADR-0003).
/// </summary>
/// <remarks>
/// A row is a new instance when its data changes (ADR-0003), so a row the bridge holds the values of, read by the
/// columns and the source of now, crosses again as its id and what it paints alone (ADR-0350 section 15's note of
/// 2026-10-30): its values, texts and tones are not read again. The bridge holds them from the last state that carried
/// them whole until .NET answers that it let the row go, and a state is taken in in the order .NET sent it, so a
/// state that names a row by its id never comes before the one that carried it whole.
/// </remarks>
internal sealed class ScriptRows<TRow>
    where TRow : class
{
    private readonly Dictionary<object, long> _ids = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<long, Held> _rows = [];
    private long _nextId;
    private long _sequence;
    private Message? _writing;

    private sealed class Held(TRow row, object identity)
    {
        public TRow Row { get; } = row;
        public object Identity { get; } = identity;
        public long LastSent { get; set; }
        /// <summary>What the values the bridge holds of the row were read by: the last state that carried them whole.</summary>
        public RowsRead? Known { get; set; }
    }

    /// <summary>A message being written: its number, and what each row it carries said before it, so a message
    /// that fails half written leaves the rows as if it had never been begun.</summary>
    private sealed class Message(long sequence, RowsRead? state)
    {
        public long Sequence { get; } = sequence;
        /// <summary>What a state's rows are read by; <see langword="null"/> for an answer, which the bridge may take in
        /// before a state sent ahead of it, so never names a row by its id alone.</summary>
        public RowsRead? State { get; } = state;
        public List<(long Id, Held Held, long Before, RowsRead? KnownBefore, bool Added)> Touched { get; } = [];
        /// <summary>Whether the message's columns format any value, read once for all its rows.</summary>
        public bool? Formatted { get; set; }
        /// <summary>Whether the message's columns tone any value, read once for all its rows.</summary>
        public bool? Toned { get; set; }
    }

    /// <summary>How many rows are kept, for the tests.</summary>
    internal int Count => _rows.Count;

    /// <summary>Writes a message that carries rows, numbered after the last one sent, in the order .NET sends
    /// them. The number is spent only once the message is written: one that throws while it is written, a
    /// column's value or a Row Key refused, takes no number and keeps no row, so the bridge, which reads the
    /// numbers in order, never waits for one that will not come. <paramref name="state"/> is what a state's rows are
    /// read by; an answer to the browser's question has none.</summary>
    internal T Sending<T>(Func<long, T> write, RowsRead? state = null)
    {
        if (_writing is not null)
            throw new InvalidOperationException("Internal: a message that carries rows was begun while another was written.");
        var message = new Message(_sequence + 1, state);
        _writing = message;
        try
        {
            var written = write(message.Sequence);
            _sequence = message.Sequence;
            return written;
        }
        catch
        {
            for (var i = message.Touched.Count - 1; i >= 0; i--)
            {
                var (id, held, before, knownBefore, added) = message.Touched[i];
                if (added)
                {
                    _rows.Remove(id);
                    _ids.Remove(held.Identity);
                }
                else
                {
                    held.LastSent = before;
                    held.Known = knownBefore;
                }
            }
            throw;
        }
        finally
        {
            _writing = null;
        }
    }

    /// <summary>Writes <paramref name="row"/> into the message being written (<see cref="Sending{T}"/>): its id,
    /// its Row Key where the source has one, each column's value, each formatted column's text of it, and, for a row
    /// the grid paints, what <paramref name="paint"/> answers of it: each cell's state where one is other than Normal
    /// (ADR-0006), its tone, its Row Kind, whether it is marked, each cell's appearance and when it changed. For a grid
    /// of a Pointing Scope, the row read as the table's row by <paramref name="tableRow"/> (ADR-0430). In a state, a row
    /// whose values the bridge holds, read by what the state's rows are read by, is written without them.</summary>
    internal void Write(
        Utf8JsonWriter json, TRow row, IReadOnlyList<GridColumn<TRow>> columns, Func<TRow, object>? rowKey, RowPaint<TRow>? paint, Func<TRow, string>? tableRow)
    {
        ArgumentNullException.ThrowIfNull(row);
        var message = _writing ?? throw new InvalidOperationException("Internal: a row written outside a message.");
        json.WriteStartObject();
        var id = IdOf(row, message);
        json.WriteNumber("i", id);
        var held = _rows[id];
        if (message.State is { } state && held.Known is { } known && known.Reads(state))
        {
            WritePaint(json, row, columns, paint, tableRow);
            json.WriteEndObject();
            return;
        }
        held.Known = message.State ?? held.Known;
        if (rowKey is not null)
        {
            json.WritePropertyName("k");
            ScriptValues.WriteKey(json, rowKey(row) ?? throw new InvalidOperationException("The source's Row Key answered null for a row; every row has a key (ADR-0140)."));
        }
        var values = new object?[columns.Count];
        json.WriteStartArray("c");
        for (var c = 0; c < columns.Count; c++)
        {
            values[c] = columns[c].Info.Value(row);
            ScriptValues.Write(json, values[c], columns[c].Name);
        }
        json.WriteEndArray();
        message.Formatted ??= ScriptColumns.AnyFormatted(columns);
        if (message.Formatted.Value)
        {
            json.WriteStartArray("t");
            for (var c = 0; c < columns.Count; c++)
            {
                if (columns[c].Format is { } format && values[c] is { } value)
                    json.WriteStringValue(format(value));
                else
                    json.WriteNullValue();
            }
            json.WriteEndArray();
        }
        if (paint is not null)
        {
            message.Toned ??= columns.Any(column => column.Tone is not null);
            if (message.Toned.Value)
                WriteTones(json, values, columns);
        }
        WritePaint(json, row, columns, paint, tableRow);
        json.WriteEndObject();
    }

    // What the row paints, asked of the Consumer each time it is sent, and the row read as the table's row.
    private static void WritePaint(
        Utf8JsonWriter json, TRow row, IReadOnlyList<GridColumn<TRow>> columns, RowPaint<TRow>? paint, Func<TRow, string>? tableRow)
    {
        if (paint is not null)
        {
            if (paint.CellState is { } cellState)
                WriteStates(json, row, columns, cellState);
            paint.WriteRow(json, row, columns);
        }
        if (tableRow is not null)
        {
            json.WritePropertyName("p");
            json.WriteRawValue(tableRow(row));
        }
    }

    // Left out where every cell is Normal, as the grid paints a row the Consumer answers nothing for.
    private static void WriteStates(Utf8JsonWriter json, TRow row, IReadOnlyList<GridColumn<TRow>> columns, CellStateOf<TRow> cellState)
    {
        CellState[]? states = null;
        for (var c = 0; c < columns.Count; c++)
        {
            var state = cellState(row, columns[c]);
            if (state is not (CellState.Normal or CellState.Stale or CellState.Missing or CellState.Error or CellState.Modified))
                throw new ArgumentOutOfRangeException(nameof(cellState), state, $"CellState answered a state the grid does not know for column '{columns[c].Name}' (ADR-0006).");
            if (state != CellState.Normal)
                states ??= new CellState[columns.Count];
            if (states is not null)
                states[c] = state;
        }
        if (states is null)
            return;
        json.WriteStartArray("s");
        foreach (var state in states)
            json.WriteStringValue(ScriptJson.Camel(state));
        json.WriteEndArray();
    }

    // Each toned column's tone of the cell's value, a function of the value alone (ADR-0006), as a format's text is.
    private static void WriteTones(Utf8JsonWriter json, object?[] values, IReadOnlyList<GridColumn<TRow>> columns)
    {
        json.WriteStartArray("o");
        for (var c = 0; c < columns.Count; c++)
        {
            if (columns[c].Tone is { } tone && values[c] is { } value)
            {
                var toned = tone(value);
                if (!Enum.IsDefined(toned))
                    throw new ArgumentOutOfRangeException(nameof(columns), toned, $"Column '{columns[c].Name}' toned a value with a tone the grid does not know (ADR-0006).");
                json.WriteStringValue(ScriptJson.Camel(toned));
            }
            else
            {
                json.WriteNullValue();
            }
        }
        json.WriteEndArray();
    }

    /// <summary>The row the browser named by <paramref name="id"/>. One it no longer holds is a defect of the
    /// bridge: the browser lets a row go only once it cannot hand it back.</summary>
    internal TRow RowOf(long id) => _rows.TryGetValue(id, out var held)
        ? held.Row
        : throw new InvalidOperationException(FormattableString.Invariant($"Internal: the browser named row {id}, which .NET had let go."));

    /// <summary>Lets go the rows the bridge no longer holds, unless a message after <paramref name="seen"/>, the
    /// last it had read, carries one again, and answers those let go: no message after this one names them, so the
    /// bridge lets their values go too.</summary>
    internal long[] Release(IEnumerable<long> ids, long seen)
    {
        var released = new List<long>();
        foreach (var id in ids)
        {
            if (!_rows.TryGetValue(id, out var held) || held.LastSent > seen)
                continue;
            _rows.Remove(id);
            _ids.Remove(held.Identity);
            released.Add(id);
        }
        return [.. released];
    }

    private long IdOf(TRow row, Message message)
    {
        object identity = row!;
        if (!_ids.TryGetValue(identity, out var id))
        {
            id = ++_nextId;
            var added = new Held(row, identity);
            _ids.Add(identity, id);
            _rows.Add(id, added);
            message.Touched.Add((id, added, 0, null, true));
        }
        else
        {
            message.Touched.Add((id, _rows[id], _rows[id].LastSent, _rows[id].Known, false));
        }
        _rows[id].LastSent = message.Sequence;
        return id;
    }
}

/// <summary>What a state's rows are read by: the columns' number, and the source's Row Key, which a new source may
/// read otherwise (ADR-0350 section 15's note of 2026-10-30). A row's values read by other columns, or its key by
/// another reader, are not the ones it has now. A new source with the same reader, as a new <c>Rows</c> list is,
/// reads the same row the same way.</summary>
internal readonly record struct RowsRead(int ColumnsVersion, Delegate? RowKey)
{
    /// <summary>Whether rows read by this read as by <paramref name="other"/>: the same columns' number, and the same
    /// key reader by reference, since two closures over one object are equal delegates whatever they captured.</summary>
    public bool Reads(RowsRead other) => ColumnsVersion == other.ColumnsVersion && ReferenceEquals(RowKey, other.RowKey);
}
