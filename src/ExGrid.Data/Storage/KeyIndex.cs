namespace ExGrid.Data.Storage;

/// <summary>
/// Where each Record Key is stored: an open-addressing table of row numbers, which reads the keys
/// themselves from the key column's arrays, so the index costs four bytes a slot. A Text key is its
/// code, which the dictionary makes unique per text; an Integer key is its value.
/// <para>
/// A Text key's codes all lie below the dictionary's count, so where that takes no more slots than
/// the table would, the index is a slot per code instead (ticket 07): nothing to hash or probe, and a
/// load, which makes a key's code as it reads its row, fills the slots in order.
/// </para>
/// <para>Immutable once built, and shared by every version that holds its rows.</para>
/// </summary>
internal sealed class KeyIndex
{
    private readonly KeySource source;
    private readonly bool byCode;
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

    private KeyIndex(KeySource source, int[] slots)
    {
        this.source = source;
        this.slots = slots;
        byCode = true;
    }

    public int Count => count;

    /// <summary>
    /// An index of a Text key's codes for <paramref name="expected"/> rows, every code below
    /// <paramref name="codes"/>: a slot per code when that takes no more room than the table would,
    /// and the table otherwise.
    /// </summary>
    public static KeyIndex ForCodes(KeySource source, int expected, int codes)
    {
        var table = new KeyIndex(source, expected);
        return codes <= table.slots.Length ? new KeyIndex(source, new int[codes]) : table;
    }

    /// <summary>Adds <paramref name="row"/>, whose key is <paramref name="key"/>; false, with the row that
    /// already carries the key, when one does.</summary>
    public bool TryAdd(int row, long key, out int existing)
    {
        if (byCode)
        {
            if ((ulong)key >= (ulong)slots.Length)
                Array.Resize(ref slots, (int)Math.Max(key + 1, slots.Length * 2L));
            ref var held = ref slots[(int)key];
            if (held != 0)
            {
                existing = held - 1;
                return false;
            }
            held = row + 1;
            count++;
            existing = -1;
            return true;
        }
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

    /// <summary>
    /// Adds the rows from <paramref name="first"/> on, whose keys are <paramref name="codes"/>, and
    /// returns how many were added: all of them, or as many as come before the first whose key another
    /// row carries, which <paramref name="existing"/> names.
    /// </summary>
    public int TryAdd(int first, ReadOnlySpan<int> codes, out int existing)
    {
        if (byCode)
        {
            // In one loop, with nothing to call: a browser runs .NET in an interpreter.
            var held = slots;
            for (var i = 0; i < codes.Length; i++)
            {
                var code = codes[i];
                if ((uint)code >= (uint)held.Length)
                {
                    Array.Resize(ref slots, Math.Max(code + 1, held.Length * 2));
                    held = slots;
                }
                if (held[code] != 0)
                {
                    count += i;
                    existing = held[code] - 1;
                    return i;
                }
                held[code] = first + i + 1;
            }
            count += codes.Length;
            existing = -1;
            return codes.Length;
        }
        for (var i = 0; i < codes.Length; i++)
        {
            if (!TryAdd(first + i, codes[i], out existing))
                return i;
        }
        existing = -1;
        return codes.Length;
    }

    /// <summary>As <see cref="TryAdd(int, ReadOnlySpan{int}, out int)"/>, for an Integer key's values.</summary>
    public int TryAdd(int first, ReadOnlySpan<long> values, out int existing)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (!TryAdd(first + i, values[i], out existing))
                return i;
        }
        existing = -1;
        return values.Length;
    }

    public bool TryGet(long key, out int row)
    {
        if (byCode)
        {
            if ((ulong)key < (ulong)slots.Length && slots[(int)key] != 0)
            {
                row = slots[(int)key] - 1;
                return true;
            }
            row = -1;
            return false;
        }
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
