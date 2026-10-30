using System.Numerics;

namespace ExGrid.Data.Storage;

/// <summary>Bit sets as the slices expose them: one bit per row, least significant bit first.</summary>
internal static class Bits
{
    public static int Words(int length) => (length + 63) >> 6;

    public static bool Get(ReadOnlySpan<ulong> bits, int index) => (bits[index >> 6] & (1UL << index)) != 0;

    public static void Set(ulong[] bits, int index) => bits[index >> 6] |= 1UL << index;

    public static int Count(ReadOnlySpan<ulong> bits)
    {
        var count = 0;
        foreach (var word in bits)
            count += BitOperations.PopCount(word);
        return count;
    }

    /// <summary>A copy holding exactly <paramref name="length"/> bits' worth of words, or the same array
    /// when it already does. The bits may be fewer: a writer sizes them when it marks its first Blank,
    /// and the rows it writes after may outgrow them, unmarked and so clear.</summary>
    public static ulong[]? Trim(ulong[]? bits, int length)
    {
        if (bits is null)
            return null;
        var words = Words(length);
        if (bits.Length == words)
            return bits;
        var trimmed = new ulong[words];
        bits.AsSpan(0, Math.Min(words, bits.Length)).CopyTo(trimmed);
        return trimmed;
    }
}
