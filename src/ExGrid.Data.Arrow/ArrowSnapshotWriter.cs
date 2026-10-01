using System.Globalization;
using Apache.Arrow;
using Apache.Arrow.Ipc;

namespace ExGrid.Data.Arrow;

/// <summary>
/// One write of a Snapshot as an uncompressed Arrow IPC stream (ADR-0065). Every column's Arrow type
/// is settled, and every value checked to be writable in it, before the first byte is written, so a
/// refused write writes nothing. Then each record batch is made straight into Arrow's buffers from the
/// slices' spans — never through Arrow's per-value builders, which were measured 10–40 times slower —
/// written by Arrow into a buffer of this write's own, and copied to the Consumer's stream with an
/// asynchronous write before the next batch is made.
/// </summary>
internal sealed class ArrowSnapshotWriter(Snapshot snapshot, int batchRows)
{
    /// <summary>The rows of a record batch.</summary>
    public const int DefaultBatchRows = 65_536;

    public async Task WriteAsync(Stream target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var order = RowOrder.Of(snapshot);
        var capacity = Math.Max(1, Math.Min(batchRows, snapshot.RowCount));
        var columns = new ColumnWriter[snapshot.Columns.Count];
        for (var c = 0; c < columns.Length; c++)
            columns[c] = ColumnWriter.For(snapshot, snapshot.Columns[c], order, capacity);
        var schema = new Schema([.. columns.Select(c => c.Field)], SchemaMetadata());

        var buffer = new MemoryStream();
        using var writer = new ArrowStreamWriter(buffer, schema, leaveOpen: true);
        writer.WriteStart();
        await FlushAsync(buffer, target, cancellationToken).ConfigureAwait(false);
        for (var start = 0; start < snapshot.RowCount; start += batchRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = Math.Min(batchRows, snapshot.RowCount - start);
            var pieces = order.Pieces(start, length);
            var arrays = new IArrowArray[columns.Length];
            for (var c = 0; c < columns.Length; c++)
                arrays[c] = columns[c].Batch(length, pieces);
            // Not disposed: its buffers are this write's managed arrays, reused for the next batch.
            writer.WriteRecordBatch(new RecordBatch(schema, arrays, length));
            await FlushAsync(buffer, target, cancellationToken).ConfigureAwait(false);
        }
        writer.WriteEnd();
        await FlushAsync(buffer, target, cancellationToken).ConfigureAwait(false);
        await target.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private Dictionary<string, string> SchemaMetadata()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SnapshotArrowMetadata.Version] = snapshot.Version.ToString(CultureInfo.InvariantCulture),
        };
        if (snapshot.RecordKey is { } key)
            metadata[SnapshotArrowMetadata.RecordKey] = key.Name;
        return metadata;
    }

    private static async ValueTask FlushAsync(MemoryStream buffer, Stream target, CancellationToken cancellationToken)
    {
        if (buffer.Length == 0)
            return;
        await target.WriteAsync(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), cancellationToken).ConfigureAwait(false);
        buffer.SetLength(0);
    }
}

/// <summary>A run of rows that lie one after another in one slice, and where it starts in the
/// Snapshot's order.</summary>
internal readonly record struct Run(int Slice, int Offset, int Length, int Start);

/// <summary>The part of a run one record batch takes: <see cref="Count"/> rows from
/// <see cref="Offset"/> of a slice, at <see cref="At"/> in the batch and <see cref="Row"/> in the
/// Snapshot's order.</summary>
internal readonly record struct Piece(int Slice, int Offset, int Count, int At, int Row);

/// <summary>
/// The rows a Snapshot holds, in its order (<see cref="Snapshot.Rows"/>), as runs of rows stored one
/// after another: one run per slice for a Snapshot no batch has changed, and a few more for each
/// record a batch changed, added or removed. Each record batch is copied run by run.
/// </summary>
internal sealed class RowOrder
{
    private RowOrder(Run[] runs) => Runs = runs;

    public Run[] Runs { get; }

    public static RowOrder Of(Snapshot snapshot)
    {
        var rows = snapshot.Rows;
        var runs = new List<Run>();
        var i = 0;
        while (i < rows.Count)
        {
            var first = rows[i];
            var length = 1;
            while (i + length < rows.Count && rows[i + length] is var next && next.Slice == first.Slice && next.Offset == first.Offset + length)
                length++;
            runs.Add(new Run(first.Slice, first.Offset, length, i));
            i += length;
        }
        return new RowOrder([.. runs]);
    }

    /// <summary>The pieces of the runs that rows [<paramref name="start"/>, <paramref name="start"/> +
    /// <paramref name="length"/>) of the order take, in order.</summary>
    public Piece[] Pieces(int start, int length)
    {
        var end = start + length;
        var pieces = new List<Piece>();
        for (var r = First(start); r < Runs.Length && Runs[r].Start < end; r++)
        {
            var run = Runs[r];
            var from = Math.Max(start, run.Start);
            var to = Math.Min(end, run.Start + run.Length);
            pieces.Add(new Piece(run.Slice, run.Offset + (from - run.Start), to - from, from - start, from));
        }
        return [.. pieces];
    }

    /// <summary>The first run that holds row <paramref name="row"/> of the order or comes after it.</summary>
    private int First(int row)
    {
        int low = 0, high = Runs.Length - 1, found = Runs.Length;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            if (Runs[middle].Start + Runs[middle].Length > row)
            {
                found = middle;
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }
        return found;
    }
}
