namespace ExPivot.Engine;

/// <summary>
/// A map from 64-bit keys to small integers by open addressing, hashed through MurmurHash3's
/// <c>fmix64</c> (<see cref="CellKey.Mix"/>), so that keys packed from small integers — a leaf's
/// Items, a number's bits, a date's ticks — spread over the table instead of chaining
/// (ticket 09: a packed key's own hash is <c>lo ^ hi</c>). One probe sequence, no deletion, and
/// nothing allocated per entry: the aggregation looks it up once per row.
/// </summary>
internal sealed class LongIntMap
{
    private long[] _keys;
    private int[] _values; // the value + 1; 0 is an empty slot
    private int _mask;

    public LongIntMap(int capacity = 16)
    {
        var size = 16;
        while (size < capacity * 2)
            size <<= 1;
        _keys = new long[size];
        _values = new int[size];
        _mask = size - 1;
    }

    /// <summary>How many keys the map holds.</summary>
    public int Count { get; private set; }

    /// <summary>How many slots the table has, for the hash's own test.</summary>
    internal int Capacity => _keys.Length;

    public bool TryGetValue(long key, out int value)
    {
        var slot = CellKey.Mix(key) & _mask;
        while (true)
        {
            var stored = _values[slot];
            if (stored == 0)
            {
                value = -1;
                return false;
            }
            if (_keys[slot] == key)
            {
                value = stored - 1;
                return true;
            }
            slot = (slot + 1) & _mask;
        }
    }

    /// <summary>The value under <paramref name="key"/>, adding <paramref name="value"/> under it when
    /// there is none.</summary>
    public int GetOrAdd(long key, int value, out bool added)
    {
        var slot = CellKey.Mix(key) & _mask;
        while (true)
        {
            var stored = _values[slot];
            if (stored == 0)
                break;
            if (_keys[slot] == key)
            {
                added = false;
                return stored - 1;
            }
            slot = (slot + 1) & _mask;
        }
        _keys[slot] = key;
        _values[slot] = value + 1;
        added = true;
        if (++Count * 2 > _keys.Length)
            Grow();
        return value;
    }

    /// <summary>The slot <paramref name="key"/> lands in first, for the hash's own test.</summary>
    internal int HomeSlot(long key) => CellKey.Mix(key) & _mask;

    private void Grow()
    {
        var keys = _keys;
        var values = _values;
        _keys = new long[keys.Length * 2];
        _values = new int[keys.Length * 2];
        _mask = _keys.Length - 1;
        for (var i = 0; i < keys.Length; i++)
        {
            if (values[i] == 0)
                continue;
            var slot = CellKey.Mix(keys[i]) & _mask;
            while (_values[slot] != 0)
                slot = (slot + 1) & _mask;
            _keys[slot] = keys[i];
            _values[slot] = values[i];
        }
    }
}
