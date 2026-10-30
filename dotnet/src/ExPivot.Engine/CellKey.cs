namespace ExPivot.Engine;

/// <summary>
/// Two small integers packed into one <c>long</c> — a cell's row node and column node, or a trie
/// node and an Item — and a hash that mixes them.
///
/// <para><c>Int64.GetHashCode</c> is the upper half XOR the lower one, so <c>(row, column)</c>
/// hashed to <c>row ^ column</c>: every cell on a diagonal shared one hash, and a layout of
/// thousands of row nodes by hundreds of column nodes had only a few thousand hash codes for
/// hundreds of thousands of cells. Lookups became chains: with 270 dates in Columns, a million
/// records took 23 s where a mixing hash took 3.3 s (ticket 09). The keys are therefore mixed by
/// MurmurHash3's 64-bit finaliser before they are hashed.</para>
/// </summary>
internal static class CellKey
{
    /// <summary>Compares packed keys, hashing them mixed.</summary>
    public static IEqualityComparer<long> Comparer { get; } = new MixingComparer();

    /// <summary>The key of <paramref name="high"/> and <paramref name="low"/>.</summary>
    public static long Of(int high, int low) => ((long)high << 32) | (uint)low;

    /// <summary>MurmurHash3's <c>fmix64</c>: every bit of the key reaches every bit of the hash.</summary>
    public static int Mix(long key)
    {
        var z = (ulong)key;
        z ^= z >> 33;
        z *= 0xff51afd7ed558ccdUL;
        z ^= z >> 33;
        z *= 0xc4ceb9fe1a85ec53UL;
        z ^= z >> 33;
        return (int)z;
    }

    private sealed class MixingComparer : IEqualityComparer<long>
    {
        public bool Equals(long x, long y) => x == y;

        public int GetHashCode(long key) => Mix(key);
    }
}
