using System.Buffers.Binary;
using System.Numerics;

namespace ExGrid.Data.Arrow;

/// <summary>
/// Moves bits between Arrow's validity bitmaps — bytes, least significant bit first, a bit set for a
/// value — and the Snapshot's Blanks — 64-bit words, least significant bit first, a bit set for a
/// Blank. Both put row <c>i</c> at bit <c>i</c>, so a word-aligned run is one inversion per 64 rows.
/// </summary>
internal static class Bitmaps
{
    public static int Words(int bits) => (bits + 63) >> 6;

    /// <summary>
    /// Fills <paramref name="blanks"/> for <paramref name="length"/> rows from Arrow's
    /// <paramref name="validity"/> starting at bit <paramref name="bitOffset"/>; true when any row is a
    /// Blank. Bits past the buffer read as nulls, so the caller checks the buffer's size first.
    /// </summary>
    public static bool BlanksFromValidity(ReadOnlySpan<byte> validity, int bitOffset, int length, Span<ulong> blanks)
    {
        var any = 0UL;
        var words = Words(length);
        for (var w = 0; w < words; w++)
        {
            var word = ~ReadBits(validity, bitOffset + (w << 6));
            var tail = length - (w << 6);
            if (tail < 64)
                word &= (1UL << tail) - 1;
            blanks[w] = word;
            any |= word;
        }
        return any != 0;
    }

    /// <summary>Whether bit <paramref name="index"/> of a Snapshot bit set is set; an empty set holds none.</summary>
    public static bool Get(ReadOnlySpan<ulong> bits, int index) => !bits.IsEmpty && (bits[index >> 6] & (1UL << index)) != 0;

    /// <summary>Whether bit <paramref name="index"/> of an Arrow bitmap is set.</summary>
    public static bool GetByte(ReadOnlySpan<byte> bits, int index) => (bits[index >> 3] & (1 << (index & 7))) != 0;

    /// <summary>
    /// Sets in Arrow's <paramref name="validity"/>, from bit <paramref name="target"/>, a bit for each of
    /// <paramref name="length"/> rows that is not a Blank in the Snapshot's <paramref name="blanks"/> from
    /// bit <paramref name="source"/>. The validity's bits there start clear. An empty
    /// <paramref name="blanks"/> holds no Blank.
    /// </summary>
    public static void ValidityFromBlanks(ReadOnlySpan<ulong> blanks, int source, int length, Span<ulong> validity, int target)
    {
        for (var done = 0; done < length; done += 64)
        {
            var count = Math.Min(64, length - done);
            var valid = blanks.IsEmpty ? ulong.MaxValue : ~ReadBits(blanks, source + done);
            if (count < 64)
                valid &= (1UL << count) - 1;
            var at = target + done;
            var shift = at & 63;
            validity[at >> 6] |= valid << shift;
            if (shift != 0 && shift + count > 64)
                validity[(at >> 6) + 1] |= valid >> (64 - shift);
        }
    }

    /// <summary>The number of bits set among the first <paramref name="length"/>.</summary>
    public static int Count(ReadOnlySpan<ulong> bits, int length)
    {
        var count = 0;
        var whole = length >> 6;
        for (var w = 0; w < whole; w++)
            count += BitOperations.PopCount(bits[w]);
        var tail = length & 63;
        if (tail != 0)
            count += BitOperations.PopCount(bits[whole] & ((1UL << tail) - 1));
        return count;
    }

    /// <summary>The 64 bits of a byte bitmap from bit <paramref name="bit"/>; bits past its end read as 0.</summary>
    private static ulong ReadBits(ReadOnlySpan<byte> bytes, int bit)
    {
        var index = bit >> 3;
        var shift = bit & 7;
        ulong low;
        byte next;
        if (index + 9 <= bytes.Length)
        {
            low = BinaryPrimitives.ReadUInt64LittleEndian(bytes[index..]);
            next = bytes[index + 8];
        }
        else
        {
            Span<byte> window = stackalloc byte[9];
            window.Clear();
            if (index < bytes.Length)
                bytes[index..].CopyTo(window);
            low = BinaryPrimitives.ReadUInt64LittleEndian(window);
            next = window[8];
        }
        return shift == 0 ? low : (low >> shift) | ((ulong)next << (64 - shift));
    }

    /// <summary>The 64 bits of a word bitmap from bit <paramref name="bit"/>; bits past its end read as 0.</summary>
    private static ulong ReadBits(ReadOnlySpan<ulong> words, int bit)
    {
        var index = bit >> 6;
        var shift = bit & 63;
        var low = index < words.Length ? words[index] : 0;
        if (shift == 0)
            return low;
        var high = index + 1 < words.Length ? words[index + 1] : 0;
        return (low >> shift) | (high << (64 - shift));
    }
}
