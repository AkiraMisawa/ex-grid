using System.Text.Json;
using ExGrid.Cells;

namespace ExGrid.Script;

/// <summary>
/// The rows a Blazor ExGrid sent to its Script Core, by the id each crossed with (ADR-0350 section 15). The grid
/// hands a row back by its id, in an edit, an action or a refusal, so a row is kept for as long as the browser may
/// still hand it back: until the bridge says no object of it is left there, and only if no message sent after the
/// last one the bridge had read when it said so carries it again. The same row is the same id while it is sent,
/// so the bridge keeps the object it made of it and the core skips it (ADR-0003).
/// </summary>
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
    }

    /// <summary>A message being written: its number, and what each row it carries said before it, so a message
    /// that fails half written leaves the rows as if it had never been begun.</summary>
    private sealed class Message(long sequence)
    {
        public long Sequence { get; } = sequence;
        public List<(long Id, Held Held, long Before, bool Added)> Touched { get; } = [];
        /// <summary>Whether the message's columns format any value, read once for all its rows.</summary>
        public bool? Formatted { get; set; }
    }

    /// <summary>How many rows are kept, for the tests.</summary>
    internal int Count => _rows.Count;

    /// <summary>Writes a message that carries rows, numbered after the last one sent, in the order .NET sends
    /// them. The number is spent only once the message is written: one that throws while it is written, a
    /// column's value or a Row Key refused, takes no number and keeps no row, so the bridge, which reads the
    /// numbers in order, never waits for one that will not come.</summary>
    internal T Sending<T>(Func<long, T> write)
    {
        if (_writing is not null)
            throw new InvalidOperationException("Internal: a message that carries rows was begun while another was written.");
        var message = new Message(_sequence + 1);
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
                var (id, held, before, added) = message.Touched[i];
                if (added)
                {
                    _rows.Remove(id);
                    _ids.Remove(held.Identity);
                }
                else
                {
                    held.LastSent = before;
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
    /// its Row Key where the source has one, each column's value, each formatted column's text of it, and each
    /// cell's state where <paramref name="cellState"/> answers one other than Normal for any of them (ADR-0006).</summary>
    internal void Write(Utf8JsonWriter json, TRow row, IReadOnlyList<GridColumn<TRow>> columns, Func<TRow, object>? rowKey, CellStateOf<TRow>? cellState)
    {
        ArgumentNullException.ThrowIfNull(row);
        var message = _writing ?? throw new InvalidOperationException("Internal: a row written outside a message.");
        json.WriteStartObject();
        json.WriteNumber("i", IdOf(row, message));
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
        if (cellState is not null)
            WriteStates(json, row, columns, cellState);
        json.WriteEndObject();
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

    /// <summary>The row the browser named by <paramref name="id"/>. One it no longer holds is a defect of the
    /// bridge: the browser lets a row go only once it cannot hand it back.</summary>
    internal TRow RowOf(long id) => _rows.TryGetValue(id, out var held)
        ? held.Row
        : throw new InvalidOperationException(FormattableString.Invariant($"Internal: the browser named row {id}, which .NET had let go."));

    /// <summary>Lets go the rows the bridge no longer holds, unless a message after <paramref name="seen"/>, the
    /// last it had read, carries one again.</summary>
    internal void Release(IEnumerable<long> ids, long seen)
    {
        foreach (var id in ids)
        {
            if (!_rows.TryGetValue(id, out var held) || held.LastSent > seen)
                continue;
            _rows.Remove(id);
            _ids.Remove(held.Identity);
        }
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
            message.Touched.Add((id, added, 0, true));
        }
        else
        {
            message.Touched.Add((id, _rows[id], _rows[id].LastSent, false));
        }
        _rows[id].LastSent = message.Sequence;
        return id;
    }
}
