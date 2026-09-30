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
    /// when it already does.</summary>
    public static ulong[]? Trim(ulong[]? bits, int length)
    {
        if (bits is null)
            return null;
        var words = Words(length);
        return bits.Length == words ? bits : bits.AsSpan(0, words).ToArray();
    }
}
