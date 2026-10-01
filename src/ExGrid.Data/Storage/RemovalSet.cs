namespace ExGrid.Data.Storage;

/// <summary>
/// The rows of one segment that a version does not hold (ADR-0063): they were removed, or replaced
/// by a changed record's new version. It belongs to the version, never to the shared segment, and a
/// batch copies only the sets of the segments it touches.
/// <para>
/// A few rows are held as sorted offsets, so a batch pays for what it changed and not for the
/// segment; many are held as a bit set. The bit set a slice hands out is made from the offsets on
/// first read, once, and shared by every version that shares the set.
/// </para>
/// </summary>
internal sealed class RemovalSet
{
    private readonly int length;
    private readonly int[]? sparse;
    private readonly ulong[]? dense;
    private ulong[]? bitmap;

    private RemovalSet(int length, int[]? sparse, ulong[]? dense, int count)
    {
        this.length = length;
        this.sparse = sparse;
        this.dense = dense;
        Count = count;
    }

    public int Count { get; }

    public bool Contains(int offset)
        => dense is not null ? Bits.Get(dense, offset) : Array.BinarySearch(sparse!, offset) >= 0;

    /// <summary>One bit per row of the segment, set for a row the version does not hold.</summary>
    public ulong[] Bitmap()
    {
        if (dense is not null)
            return dense;
        var made = Volatile.Read(ref bitmap);
        if (made is not null)
            return made;
        made = new ulong[Bits.Words(length)];
        foreach (var offset in sparse!)
            Bits.Set(made, offset);
        return Interlocked.CompareExchange(ref bitmap, made, null) ?? made;
    }

    /// <summary>
    /// The set that also leaves out <paramref name="offsets"/> — distinct, and none of them in
    /// <paramref name="current"/>. Neither input is changed.
    /// </summary>
    public static RemovalSet With(RemovalSet? current, int length, List<int> offsets)
    {
        var count = (current?.Count ?? 0) + offsets.Count;
        if (count <= 2 * Bits.Words(length))
        {
            var merged = new int[count];
            var existing = current?.sparse ?? [];
            existing.CopyTo(merged, 0);
            for (var i = 0; i < offsets.Count; i++)
                merged[existing.Length + i] = offsets[i];
            Array.Sort(merged);
            return new RemovalSet(length, merged, null, count);
        }
        var bits = current is null ? new ulong[Bits.Words(length)] : (ulong[])current.Bitmap().Clone();
        foreach (var offset in offsets)
            Bits.Set(bits, offset);
        return new RemovalSet(length, null, bits, count);
    }
}
