namespace ExGrid;

/// <summary>
/// The base of a <c>GridSource.From</c> given a Row Key (ADR-0141): the Consumer's rows by key, in
/// base order, each with its ordinal — its place in that order. A changed row keeps the ordinal of
/// the row it replaces; an added row takes the next, so it goes at the end; a removed row leaves a
/// gap, closed when the gaps grow to half the base. A whole new list in another order gives every
/// row a new ordinal in that order (<see cref="Reorder"/>). The ordinals only ever rise, so the base
/// order is ordinal order, and a row is found in it by binary search.
///
/// <para>It holds no lock: its source takes one around every call.</para>
/// </summary>
internal sealed class KeyedRows<TRow>
{
    private readonly Func<TRow, object> _key;
    private readonly Dictionary<object, Held> _byKey;
    private TRow[] _rows;
    private long[] _ordinals;
    private bool[] _gone;
    private int _length;
    private int _goneCount;
    private long _nextOrdinal;

    /// <summary>The rows in the order given, keyed by <paramref name="key"/>. A key that repeats,
    /// or a null one, is refused by name: two rows answering one key would be painted as one
    /// (ADR-0140).</summary>
    public KeyedRows(IReadOnlyList<TRow> rows, Func<TRow, object> key)
    {
        _key = key;
        _byKey = new Dictionary<object, Held>(rows.Count);
        _rows = new TRow[Math.Max(rows.Count, 4)];
        _ordinals = new long[_rows.Length];
        _gone = new bool[_rows.Length];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i] ?? throw new ArgumentException($"Row {i} is null; a Row Key names a row.", nameof(rows));
            var k = KeyOf(row, $"Row {i}");
            if (_byKey.TryGetValue(k, out var first))
            {
                throw new ArgumentException(
                    $"Rows {first.Ordinal} and {i} both have the Row Key '{k}'. A Row Key tells one row from every " +
                    "other, and two rows under one key would be painted as one (ADR-0140/0141).", nameof(rows));
            }
            _byKey.Add(k, new Held(row, i));
            _rows[i] = row;
            _ordinals[i] = i;
        }
        _length = rows.Count;
        _nextOrdinal = rows.Count;
    }

    /// <summary>How many rows the base holds.</summary>
    public int Count => _byKey.Count;

    /// <summary>A row's Row Key, refused by name when null.</summary>
    public object KeyOf(TRow row, string which)
        => _key(row) ?? throw new ArgumentException(
            $"{which} has a null Row Key. A Row Key names a row, and no row is named by nothing (ADR-0140).");

    /// <summary>The row held under <paramref name="key"/> now, and its ordinal.</summary>
    public bool TryGet(object key, out TRow row, out long ordinal)
    {
        if (_byKey.TryGetValue(key, out var held))
        {
            row = held.Row;
            ordinal = held.Ordinal;
            return true;
        }
        row = default!;
        ordinal = -1;
        return false;
    }

    /// <summary>Whether a row is held under <paramref name="key"/>.</summary>
    public bool Holds(object key) => _byKey.ContainsKey(key);

    /// <summary>Puts <paramref name="row"/> in the place of the row held under its key.</summary>
    public void Change(object key, TRow row)
    {
        var held = _byKey[key];
        held.Row = row;
        _rows[SlotOf(held.Ordinal)] = row;
    }

    /// <summary>Adds <paramref name="row"/> at the end of the base order.</summary>
    public void Add(object key, TRow row)
    {
        if (_length == _rows.Length)
            Grow();
        var ordinal = _nextOrdinal++;
        _byKey.Add(key, new Held(row, ordinal));
        _rows[_length] = row;
        _ordinals[_length] = ordinal;
        _gone[_length] = false;
        _length++;
    }

    /// <summary>Takes the row held under <paramref name="key"/> out of the base.</summary>
    public void Remove(object key)
    {
        if (!_byKey.Remove(key, out var held))
            return;
        var slot = SlotOf(held.Ordinal);
        _gone[slot] = true;
        _rows[slot] = default!;
        _goneCount++;
        // Closed once the gaps are half the base, so a day of bookings and cancellations costs
        // what the rows held do, and closing them costs, spread over the removals, a constant each.
        if (_goneCount > 64 && _goneCount * 2 > _length)
            Compact();
    }

    /// <summary>
    /// Puts the base in <paramref name="keys"/>' order, which names every key held once (ADR-0141, D7:
    /// a whole new list sets the order). Each row takes a new ordinal, above every ordinal given
    /// before, in that order: the ordinals still only rise, and the base order is still ordinal order,
    /// but an ordinal taken before this no longer says where a row stands among the ones taken after.
    /// </summary>
    public void Reorder(IReadOnlyList<object> keys)
    {
        if (keys.Count != _byKey.Count)
            throw new InvalidOperationException($"A new order names {keys.Count} keys of the {_byKey.Count} held (ADR-0141).");
        var size = Math.Max(keys.Count, 4);
        var rows = new TRow[size];
        var ordinals = new long[size];
        for (var i = 0; i < keys.Count; i++)
        {
            var held = _byKey[keys[i]];
            held.Ordinal = _nextOrdinal + i;
            rows[i] = held.Row;
            ordinals[i] = held.Ordinal;
        }
        _rows = rows;
        _ordinals = ordinals;
        _gone = new bool[size];
        _length = keys.Count;
        _goneCount = 0;
        _nextOrdinal += keys.Count;
    }

    /// <summary>The rows held, in base order, with their ordinals: what a whole requery reads.</summary>
    public (TRow[] Rows, long[] Ordinals) Snapshot()
    {
        var rows = new TRow[_byKey.Count];
        var ordinals = new long[rows.Length];
        var at = 0;
        for (var slot = 0; slot < _length; slot++)
        {
            if (_gone[slot])
                continue;
            rows[at] = _rows[slot];
            ordinals[at] = _ordinals[slot];
            at++;
        }
        return (rows, ordinals);
    }

    private int SlotOf(long ordinal)
    {
        var slot = Array.BinarySearch(_ordinals, 0, _length, ordinal);
        if (slot < 0)
            throw new InvalidOperationException($"The ordinal {ordinal} is not in the base (ADR-0141).");
        return slot;
    }

    private void Grow()
    {
        var size = Math.Max(4, _rows.Length * 2);
        Array.Resize(ref _rows, size);
        Array.Resize(ref _ordinals, size);
        Array.Resize(ref _gone, size);
    }

    private void Compact()
    {
        var at = 0;
        for (var slot = 0; slot < _length; slot++)
        {
            if (_gone[slot])
                continue;
            _rows[at] = _rows[slot];
            _ordinals[at] = _ordinals[slot];
            _gone[at] = false;
            at++;
        }
        Array.Clear(_rows, at, _length - at);
        Array.Clear(_gone, at, _length - at);
        _length = at;
        _goneCount = 0;
    }

    private sealed class Held(TRow row, long ordinal)
    {
        public TRow Row { get; set; } = row;

        public long Ordinal { get; set; } = ordinal;
    }
}
