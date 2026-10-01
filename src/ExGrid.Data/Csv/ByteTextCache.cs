using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ExGrid.Data.Csv;

/// <summary>
/// The dictionary code of each text a column has read, keyed by the field's exact bytes, so that a
/// value seen before costs a hash of its bytes and no decoding. A field of up to eight bytes is its
/// own key, packed into a <see cref="ulong"/>; a longer one is hashed and compared with a copy.
/// <para>
/// Only bytes that decoded strictly are added, so a hit is valid text. A column whose values rarely
/// repeat — identifiers — gains nothing from it, so once it holds many entries and most lookups miss,
/// it gives up and lets go of what it holds; unless it is made to keep every entry, because it is
/// what tells the column's texts apart (ticket 07): UTF-8 decodes two different runs of bytes into
/// two different texts, so in UTF-8 a miss is a text not seen before.
/// </para>
/// </summary>
internal sealed class ByteTextCache(bool keepAll = false)
{
    private const int GiveUpAt = 1 << 16;

    private int[] table = new int[256]; // entry + 1; 0 is empty
    private Entry[] entries = new Entry[128];
    private byte[] arena = new byte[1024];
    private int count;
    private int arenaLength;
    private long lookups;
    private long hits;

    /// <summary>Whether the cache still keeps codes.</summary>
    public bool Enabled { get; private set; } = true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(ReadOnlySpan<byte> bytes, out int code)
    {
        lookups++;
        var key = Key(bytes, out var hash);
        var mask = table.Length - 1;
        for (var i = (int)(hash & (uint)mask); ; i = (i + 1) & mask)
        {
            var e = table[i] - 1;
            if (e < 0)
            {
                code = -1;
                return false;
            }
            ref var entry = ref entries[e];
            if (entry.Hash == hash && entry.Length == bytes.Length
                && (bytes.Length <= 8 ? entry.Key == key : arena.AsSpan(entry.Offset, bytes.Length).SequenceEqual(bytes)))
            {
                hits++;
                code = entry.Code;
                return true;
            }
        }
    }

    /// <summary>Keeps <paramref name="code"/> for <paramref name="bytes"/>, which <see cref="TryGet"/>
    /// has just missed.</summary>
    public void Add(ReadOnlySpan<byte> bytes, int code)
    {
        if (!keepAll && count >= GiveUpAt && hits * 2 < lookups)
        {
            GiveUp();
            return;
        }
        var key = Key(bytes, out var hash);
        var offset = 0;
        if (bytes.Length > 8)
        {
            if (arena.Length - arenaLength < bytes.Length)
                Array.Resize(ref arena, Math.Max(arena.Length * 2, arenaLength + bytes.Length));
            bytes.CopyTo(arena.AsSpan(arenaLength));
            offset = arenaLength;
            arenaLength += bytes.Length;
        }
        if (count == entries.Length)
            Array.Resize(ref entries, count * 2);
        entries[count] = new Entry { Key = key, Hash = hash, Length = bytes.Length, Offset = offset, Code = code };
        count++;
        if (count * 2 > table.Length)
        {
            Rehash();
            return;
        }
        var mask = table.Length - 1;
        var i = (int)(hash & (uint)mask);
        while (table[i] != 0)
            i = (i + 1) & mask;
        table[i] = count;
    }

    private void GiveUp()
    {
        Enabled = false;
        table = [];
        entries = [];
        arena = [];
    }

    private void Rehash()
    {
        var grown = new int[table.Length * 2];
        var mask = grown.Length - 1;
        for (var e = 0; e < count; e++)
        {
            var i = (int)(entries[e].Hash & (uint)mask);
            while (grown[i] != 0)
                i = (i + 1) & mask;
            grown[i] = e + 1;
        }
        table = grown;
    }

    /// <summary>A field of up to eight bytes as a <see cref="ulong"/> — with its length, it is every
    /// byte — and a hash of any field.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Key(ReadOnlySpan<byte> s, out uint hash)
    {
        if (s.Length <= 8)
        {
            ulong key;
            if (s.Length >= 4)
            {
                key = MemoryMarshal.Read<uint>(s) | ((ulong)MemoryMarshal.Read<uint>(s[^4..]) << 32);
            }
            else
            {
                key = 0;
                for (var i = 0; i < s.Length; i++)
                    key |= (ulong)s[i] << (8 * i);
            }
            hash = (uint)(((key ^ (ulong)s.Length) * 0x9E3779B97F4A7C15UL) >> 32);
            return key;
        }
        var h = (ulong)s.Length * 0x9E3779B97F4A7C15UL;
        var at = 0;
        for (; at + 8 <= s.Length; at += 8)
        {
            h = (h ^ MemoryMarshal.Read<ulong>(s[at..])) * 0xFF51AFD7ED558CCDUL;
            h ^= h >> 32;
        }
        if (at < s.Length)
        {
            h = (h ^ MemoryMarshal.Read<ulong>(s[^8..])) * 0xC4CEB9FE1A85EC53UL;
            h ^= h >> 29;
        }
        hash = (uint)(h ^ (h >> 32));
        return 0;
    }

    private struct Entry
    {
        public ulong Key;
        public uint Hash;
        public int Length;
        public int Offset;
        public int Code;
    }
}
