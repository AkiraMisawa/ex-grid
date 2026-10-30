using System.Buffers;
using Apache.Arrow.Memory;

namespace ExGrid.Data.Arrow;

/// <summary>
/// The memory Arrow's stream and file readers read into: zeroed managed arrays, as Arrow's own
/// allocator zeroes what it hands out, which the collector reclaims once nothing refers to them.
/// <para>
/// A read never disposes a record batch (<see cref="ArrowSnapshotReader"/>): disposing one releases
/// the dictionary it shares with the batches after it — one Arrow concatenated from a delta, or undid
/// from compressed buffers — and the next batch, or the next delta, would meet released memory.
/// Arrow's default allocator is native and freed when disposed; this one needs no disposing.
/// </para>
/// </summary>
internal sealed class ManagedMemory : MemoryAllocator
{
    public static readonly ManagedMemory Instance = new();

    protected override IMemoryOwner<byte> AllocateInternal(int length, out int bytesAllocated)
    {
        bytesAllocated = length;
        return new Owner(new byte[length]);
    }

    /// <summary>An array as Arrow holds memory; disposing it leaves the array to the collector.</summary>
    private sealed class Owner(byte[] array) : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; } = array;

        public void Dispose()
        {
        }
    }
}
