using System.Globalization;
using System.Runtime.InteropServices;
using Apache.Arrow;
using Apache.Arrow.Ipc;

namespace ExGrid.Data.Arrow;

/// <summary>
/// One read of an Arrow IPC stream or file into a Snapshot (ADR-0065), through a
/// <see cref="SnapshotColumnsBuilder"/>: the schema declares the columns — every type checked against
/// ADR-0065's table before a row is read — and each record batch is appended in chunks, with a
/// checkpoint after each, so a browser keeps painting and a cancellation is seen.
/// </summary>
internal sealed class ArrowSnapshotReader(SnapshotLoadOptions? options, ICompressionCodecFactory? codecs, CancellationToken cancellationToken)
{
    /// <summary>The rows read between two checkpoints.</summary>
    internal const int ChunkRows = 8192;

    private readonly ICompressionCodecFactory codecs = codecs ?? MissingCodecs.Instance;

    private static ReadOnlySpan<byte> FileMagic => "ARROW1"u8;

    public async ValueTask<Snapshot> ReadAsync(Stream stream)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long? total = stream.CanSeek ? stream.Length - stream.Position : null;
        var head = new byte[8];
        var got = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        if (head.AsSpan(0, got).StartsWith(FileMagic))
            return await ReadFileAsync(stream, head, got).ConfigureAwait(false);

        // The first message whole — a stream's schema — is checked before Arrow reads a byte, so
        // bytes that are not Arrow are refused as such, not by whatever Arrow makes of them.
        var length = IpcFrames.FirstMessageLength(head.AsSpan(0, got));
        var first = new byte[length];
        head.AsSpan(0, got).CopyTo(first);
        var rest = await stream.ReadAtLeastAsync(first.AsMemory(got), length - got, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        if (got + rest < length)
            throw IpcFrames.EndsInFirstMessage();
        IpcFrames.CheckSchema(first);

        var source = new TrackingStream(first, stream);
        using var reader = new ArrowStreamReader(source, ManagedMemory.Instance, codecs, leaveOpen: true);
        return await ReadBatchesAsync(reader, _ => source.BytesRead, total, null, () => source.ReachedEnd).ConfigureAwait(false);
    }

    public async ValueTask<Snapshot> ReadAsync(ReadOnlyMemory<byte> arrow)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (arrow.Span.StartsWith(FileMagic))
        {
            var file = MemoryMarshal.TryGetArray(arrow, out var segment)
                ? new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false)
                : new MemoryStream(arrow.ToArray(), writable: false);
            await using (file.ConfigureAwait(false))
                return await ReadFileAsync(file).ConfigureAwait(false);
        }

        var (ends, rows) = IpcFrames.Walk(arrow.Span);
        using var reader = new ArrowStreamReader(arrow, codecs);
        return await ReadBatchesAsync(reader, batch => ends[Math.Min(batch, ends.Length - 1)], arrow.Length, rows, null).ConfigureAwait(false);
    }

    /// <summary>An Arrow file from the Consumer's stream: read where it lies when the stream can seek
    /// and starts there, and otherwise copied, since a file is read from its footer.</summary>
    private async ValueTask<Snapshot> ReadFileAsync(Stream stream, byte[] head, int got)
    {
        if (stream.CanSeek)
        {
            stream.Seek(-got, SeekOrigin.Current);
            if (stream.Position == 0)
                return await ReadFileAsync(stream).ConfigureAwait(false);
        }
        var copy = new MemoryStream();
        await using (copy.ConfigureAwait(false))
        {
            if (!stream.CanSeek)
                copy.Write(head, 0, got);
            await stream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
            copy.Position = 0;
            return await ReadFileAsync(copy).ConfigureAwait(false);
        }
    }

    /// <summary>An Arrow file from a stream that can seek and starts at the file's first byte.</summary>
    private async ValueTask<Snapshot> ReadFileAsync(Stream file)
    {
        // A file ends with its footer and the magic again; one without them was cut short.
        var tail = new byte[FileMagic.Length];
        var whole = file.Length >= 2 * FileMagic.Length;
        if (whole)
        {
            file.Seek(-tail.Length, SeekOrigin.End);
            await file.ReadExactlyAsync(tail, cancellationToken).ConfigureAwait(false);
            file.Position = 0;
        }
        if (!whole || !tail.AsSpan().SequenceEqual(FileMagic))
            throw new SnapshotException("The Arrow file ends without its footer, so it may have been cut short; a file is read only whole.");
        using var reader = new ArrowFileReader(file, ManagedMemory.Instance, codecs, leaveOpen: true);
        return await ReadBatchesAsync(reader, null, null, null, null).ConfigureAwait(false);
    }

    /// <summary>
    /// The read itself, whichever way the bytes came. <paramref name="bytesAfter"/> says how far the
    /// bytes have come once a record batch is in; <paramref name="ranOut"/> whether the source ran out
    /// before an end-of-stream marker stopped the reader.
    /// </summary>
    private async ValueTask<Snapshot> ReadBatchesAsync(
        ArrowStreamReader reader,
        Func<int, long>? bytesAfter,
        long? totalBytes,
        long? totalRows,
        Func<bool>? ranOut)
    {
        var schema = await GuardAsync(() => reader.GetSchema(cancellationToken), ranOut).ConfigureAwait(false)
            ?? throw IpcFrames.Empty();
        var builder = new SnapshotColumnsBuilder(options, cancellationToken) { TotalRows = totalRows };
        var columns = Declare(schema, builder);
        var scratch = new ReadScratch();
        var batches = 0;
        // A record batch is not disposed: disposing one releases the dictionary its columns share
        // with the batches after it, and a later batch, or a delta Arrow concatenates onto it, would
        // read released memory. A batch from a stream or a file holds managed memory (ManagedMemory),
        // and one read in place holds the Consumer's own bytes — and, when its buffers were
        // compressed, what Arrow undid them into, which Arrow's finalizers release; either way it
        // is reclaimed once the batch is let go.
        while (await GuardAsync(() => reader.ReadNextRecordBatchAsync(cancellationToken), ranOut).ConfigureAwait(false) is { } batch)
        {
            long? bytes = bytesAfter?.Invoke(batches);
            var arrays = Arrays(batch, columns);
            for (var start = 0; start < batch.Length; start += ChunkRows)
            {
                var length = Math.Min(ChunkRows, batch.Length - start);
                long rowBase = builder.RowCount;
                for (var c = 0; c < columns.Length; c++)
                    columns[c].Read(arrays[c], start, length, rowBase, scratch);
                await builder.CheckpointAsync(bytes, totalBytes).ConfigureAwait(false);
            }
            batches++;
            cancellationToken.ThrowIfCancellationRequested();
        }
        if (ranOut?.Invoke() == true)
            throw IpcFrames.CutShort();
        return await builder.BuildAsync().ConfigureAwait(false);
    }

    /// <summary>Declares a column for each field, by ADR-0065's table, with the captions, the version
    /// and the Record Key the metadata carries.</summary>
    private static ColumnReader[] Declare(Schema schema, SnapshotColumnsBuilder builder)
    {
        var fields = schema.FieldsList;
        var names = new HashSet<string>(StringComparer.Ordinal);
        var columns = new ColumnReader[fields.Count];
        for (var i = 0; i < columns.Length; i++)
        {
            var field = fields[i];
            if (string.IsNullOrEmpty(field.Name))
                throw new SnapshotException(string.Create(CultureInfo.InvariantCulture, $"The Arrow stream's column {i + 1} has no name; every column of a Snapshot is named."));
            if (!names.Add(field.Name))
                throw new SnapshotException(null, field.Name, "the Arrow stream has two columns of this name, and a column's name is unique within a Snapshot.");
            var caption = field.Metadata is { } metadata && metadata.TryGetValue(SnapshotArrowMetadata.Caption, out var text) ? text : null;
            columns[i] = ColumnReader.For(field, caption, builder);
        }

        if (schema.Metadata is not { } facts)
            return columns;
        if (facts.TryGetValue(SnapshotArrowMetadata.Version, out var version))
        {
            if (!long.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                throw new SnapshotException($"The Arrow stream's {SnapshotArrowMetadata.Version} metadata '{version}' is not a version: a version is a whole number, 0 or more.");
            builder.Version = number;
        }
        if (facts.TryGetValue(SnapshotArrowMetadata.RecordKey, out var key))
        {
            var column = System.Array.Find(columns, c => c.Name == key)
                ?? throw new SnapshotException($"The Arrow stream names '{key}' its Record Key ({SnapshotArrowMetadata.RecordKey}), and holds no column of that name.");
            if (column.Kind is not (SnapshotKind.Text or SnapshotKind.Integer))
                throw new SnapshotException(null, key, $"the Arrow stream names it the Record Key, and a Record Key is a Text or an Integer column; this one is {column.Kind}.");
            builder.Key(key);
        }
        return columns;
    }

    /// <summary>A record batch's arrays, each checked to hold the batch's rows.</summary>
    private static ArrayData[] Arrays(RecordBatch batch, ColumnReader[] columns)
    {
        if (batch.ColumnCount != columns.Length)
        {
            throw new SnapshotException(string.Create(CultureInfo.InvariantCulture,
                $"The Arrow stream is malformed: a record batch holds {batch.ColumnCount} columns, and its schema {columns.Length}."));
        }
        // A Snapshot's rows are its columns' values: rows with no column would be read as none.
        if (columns.Length == 0 && batch.Length > 0)
        {
            throw new SnapshotException(string.Create(CultureInfo.InvariantCulture,
                $"The Arrow stream holds {batch.Length:N0} rows and no column, and a Snapshot holds rows only in its columns."));
        }
        var arrays = new ArrayData[columns.Length];
        for (var c = 0; c < arrays.Length; c++)
        {
            arrays[c] = batch.Column(c).Data;
            if (arrays[c].Length != batch.Length)
            {
                throw new SnapshotException(null, columns[c].Name, string.Create(CultureInfo.InvariantCulture,
                    $"the Arrow stream is malformed: a record batch of {batch.Length} rows holds {arrays[c].Length} of this column."));
            }
        }
        return arrays;
    }

    /// <summary>
    /// Runs one step of Arrow's reader. What Arrow throws is a refusal — the bytes are not Arrow, or
    /// a stream cut short — unless it is the source's own failure or a cancellation, which pass as they
    /// are; a refusal of this package's own, such as a missing codec's, passes too.
    /// </summary>
    private static async ValueTask<T> GuardAsync<T>(Func<ValueTask<T>> step, Func<bool>? ranOut)
    {
        try
        {
            return await step().ConfigureAwait(false);
        }
        catch (Exception e) when (e is not (SnapshotException or OperationCanceledException or IOException or OutOfMemoryException))
        {
            if (ranOut?.Invoke() == true)
                throw IpcFrames.CutShort();
            throw new SnapshotException($"The Arrow stream could not be read: {e.Message}", e);
        }
    }
}
