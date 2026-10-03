namespace ExGrid.Data.Arrow;

/// <summary>
/// The Consumer's stream as Arrow's stream reader reads it: the bytes peeked to tell a stream from a
/// file first, then the rest. It counts the bytes read, for progress, and notes when the source has
/// run out. Arrow's reader stops at the end-of-stream marker without asking for more, so a reader
/// that saw the source run out has met a stream that ends without its marker (<see cref="IpcFrames"/>).
/// </summary>
internal sealed class TrackingStream(ReadOnlyMemory<byte> head, Stream source) : Stream
{
    private ReadOnlyMemory<byte> head = head;

    /// <summary>The bytes read so far, the peeked ones included.</summary>
    public long BytesRead { get; private set; }

    /// <summary>Whether a read found the source at its end.</summary>
    public bool ReachedEnd { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty)
            return 0;
        var read = FromHead(buffer);
        if (read == 0)
        {
            read = source.Read(buffer);
            ReachedEnd |= read == 0;
        }
        BytesRead += read;
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
            return 0;
        var read = FromHead(buffer.Span);
        if (read == 0)
        {
            read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            ReachedEnd |= read == 0;
        }
        BytesRead += read;
        return read;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int FromHead(Span<byte> buffer)
    {
        if (head.IsEmpty)
            return 0;
        var count = Math.Min(head.Length, buffer.Length);
        head.Span[..count].CopyTo(buffer);
        head = head[count..];
        return count;
    }
}
