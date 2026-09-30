namespace ExGrid.Data.Storage;

/// <summary>
/// Where each Record Key is stored: an open-addressing table of row numbers, which reads the keys
/// themselves from the key column's arrays, so the index costs four bytes a slot. A Text key is its
/// code, which the dictionary makes unique per text; an Integer key is its value.
/// <para>Immutable once built, and shared by every version that holds its rows.</para>
/// </summary>
internal sealed class KeyIndex
{
    private readonly KeySource source;
    private int[] slots;
    private int bits;
    private int count;

    public KeyIndex(KeySource source, int expected)
    {
        this.source = source;
        bits = 4;
        while ((1 << bits) < expected * 2)
            bits++;
        slots = new int[1 << bits];
    }

    public int Count => count;

    /// <summary>Adds <paramref name="row"/>, whose key is <paramref name="key"/>; false, with the row that
    /// already carries the key, when one does.</summary>
    public bool TryAdd(int row, long key, out int existing)
    {
        if ((count + 1) * 2 > slots.Length)
            Grow();
        var mask = slots.Length - 1;
        for (var i = Hash(key, bits); ; i = (i + 1) & mask)
        {
            var slot = slots[i];
            if (slot == 0)
            {
                slots[i] = row + 1;
                count++;
                existing = -1;
                return true;
            }
            if (source.KeyOf(slot - 1) == key)
            {
                existing = slot - 1;
                return false;
            }
        }
    }

    public bool TryGet(long key, out int row)
    {
        var mask = slots.Length - 1;
        for (var i = Hash(key, bits); ; i = (i + 1) & mask)
        {
            var slot = slots[i];
            if (slot == 0)
            {
                row = -1;
                return false;
            }
            if (source.KeyOf(slot - 1) == key)
            {
                row = slot - 1;
                return true;
            }
        }
    }

    private void Grow()
    {
        var old = slots;
        bits++;
        slots = new int[1 << bits];
        var mask = slots.Length - 1;
        foreach (var slot in old)
        {
            if (slot == 0)
                continue;
            var i = Hash(source.KeyOf(slot - 1), bits);
            while (slots[i] != 0)
                i = (i + 1) & mask;
            slots[i] = slot;
        }
    }

    // Fibonacci hashing spreads the consecutive keys a blotter's ids usually are.
    private static int Hash(long key, int bits) => (int)(((ulong)key * 0x9E3779B97F4A7C15UL) >> (64 - bits));
}

/// <summary>Reads the key of a row from the key column's arrays, one array per segment.</summary>
internal sealed class KeySource
{
    private readonly long[][]? integers;
    private readonly int[][]? codes;
    private readonly int shift;
    private readonly int mask;

    private KeySource(long[][]? integers, int[][]? codes, int shift)
    {
        this.integers = integers;
        this.codes = codes;
        this.shift = shift;
        mask = shift >= 31 ? int.MaxValue : (1 << shift) - 1;
    }

    /// <summary>Keys stored across base segments of 2^<paramref name="shift"/> rows: row r is offset
    /// r mod 2^shift of segment r / 2^shift.</summary>
    public static KeySource Of(SnapshotKind kind, IReadOnlyList<ColumnData> segments, int shift)
        => kind == SnapshotKind.Text
            ? new KeySource(null, segments.Select(d => ((TextData)d).Codes).ToArray(), shift)
            : new KeySource(segments.Select(d => ((IntegerData)d).Values).ToArray(), null, shift);

    /// <summary>Keys stored in one segment: row r is its offset r.</summary>
    public static KeySource Of(ColumnData segment)
        => segment is TextData text
            ? new KeySource(null, [text.Codes], 31)
            : new KeySource([((IntegerData)segment).Values], null, 31);

    public long KeyOf(int row)
        => integers is not null ? integers[row >> shift][row & mask] : codes![row >> shift][row & mask];
}
