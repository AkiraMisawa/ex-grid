using Apache.Arrow.Ipc;

namespace ExGrid.Data.Arrow;

/// <summary>
/// A Snapshot as Apache Arrow (ADR-0064): read from Arrow's IPC stream or file format — which
/// pyarrow, DuckDB and Polars write — and written as an uncompressed IPC stream, which they read.
/// <para>
/// A read converts into the Snapshot's own layout, one column at a time from Arrow's buffers: another
/// producer's dictionary is taken under the Snapshot's rules (in the order values first appear, one
/// entry per exact text, a null entry or a null index a Blank), and a type outside ADR-0064's table is
/// refused by name. A read either yields a Snapshot or refuses whole with a
/// <see cref="SnapshotException"/>, naming the column, and the row when a value is the problem; it
/// never yields a Snapshot with a row left out.
/// </para>
/// <para>
/// A write gives each kind its Arrow type — Text a dictionary of <c>utf8</c>, Decimal
/// <c>decimal128(38, scale)</c>, Double <c>float64</c>, Integer <c>int64</c>, Date <c>date32</c> or a
/// <c>timestamp</c> without a time zone, Boolean <c>bool</c> — and a Blank a null slot. The captions,
/// the Record Key and the version travel in the metadata (<see cref="SnapshotArrowMetadata"/>), so a
/// Snapshot round-trips whole.
/// </para>
/// </summary>
public static class SnapshotArrow
{
    /// <summary>
    /// Reads a Snapshot from an Arrow IPC stream, or from an Arrow file (<c>.arrow</c>, Feather 2),
    /// told apart by the file's leading <c>ARROW1</c>. Record batch by record batch, in slices that
    /// yield between them and report progress (<paramref name="options"/>); the stream is read to its
    /// end and left open.
    /// </summary>
    /// <param name="stream">The stream to read, from its current position.</param>
    /// <param name="options">How the load paces itself and reports its progress.</param>
    /// <param name="codecs">
    /// What undoes compressed buffers — <c>Apache.Arrow.Compression</c>'s
    /// <c>CompressionCodecFactory</c>. Without it, a stream whose buffers are compressed is refused,
    /// naming the codec it needs; this package writes uncompressed streams, and only an application
    /// that reads another producer's compressed ones needs the codecs.
    /// </param>
    /// <param name="cancellationToken">Cancels the read; a cancelled read yields nothing.</param>
    /// <exception cref="SnapshotException">The stream cannot be read whole into a Snapshot: a type outside
    /// ADR-0064's table, a value a kind cannot hold, a compressed stream without its codec, a stream
    /// cut short, or one that is not Arrow at all.</exception>
    /// <exception cref="OperationCanceledException">The read was cancelled.</exception>
    public static ValueTask<Snapshot> ReadAsync(
        Stream stream,
        SnapshotLoadOptions? options = null,
        ICompressionCodecFactory? codecs = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("The stream cannot be read.", nameof(stream));
        return new ArrowSnapshotReader(options, codecs, cancellationToken).ReadAsync(stream);
    }

    /// <summary>
    /// Reads a Snapshot from an Arrow IPC stream, or an Arrow file, held in memory — a payload already
    /// fetched whole. Its uncompressed buffers are read where they lie, without a copy.
    /// </summary>
    /// <param name="arrow">The stream's or the file's bytes.</param>
    /// <param name="options">How the load paces itself and reports its progress.</param>
    /// <param name="codecs">What undoes compressed buffers; see <see cref="ReadAsync(Stream, SnapshotLoadOptions?, ICompressionCodecFactory?, CancellationToken)"/>.</param>
    /// <param name="cancellationToken">Cancels the read; a cancelled read yields nothing.</param>
    /// <exception cref="SnapshotException">The bytes cannot be read whole into a Snapshot.</exception>
    /// <exception cref="OperationCanceledException">The read was cancelled.</exception>
    public static ValueTask<Snapshot> ReadAsync(
        ReadOnlyMemory<byte> arrow,
        SnapshotLoadOptions? options = null,
        ICompressionCodecFactory? codecs = null,
        CancellationToken cancellationToken = default)
        => new ArrowSnapshotReader(options, codecs, cancellationToken).ReadAsync(arrow);

    /// <summary>
    /// Writes <paramref name="snapshot"/> to <paramref name="stream"/> as an uncompressed Arrow IPC
    /// stream: its rows in the Snapshot's order (<see cref="Snapshot.Rows"/>), in record batches of
    /// 65,536 rows, each written to the stream before the next is made. HTTP's own compression does
    /// the rest (ADR-0064). The stream is left open.
    /// </summary>
    /// <param name="snapshot">The Snapshot to write.</param>
    /// <param name="stream">Where to write it, from its current position.</param>
    /// <param name="cancellationToken">Cancels the write between record batches.</param>
    /// <exception cref="SnapshotException">A value cannot be written exactly in its column's Arrow type:
    /// a Decimal past <c>decimal128</c>'s 38 digits at the column's scale, a Date that needs
    /// nanoseconds outside their range, or text that is not valid Unicode. Nothing more is written
    /// once a value is refused, and what was written is not a whole stream.</exception>
    /// <exception cref="OperationCanceledException">The write was cancelled.</exception>
    public static Task WriteAsync(Snapshot snapshot, Stream stream, CancellationToken cancellationToken = default)
        => WriteAsync(snapshot, stream, ArrowSnapshotWriter.DefaultBatchRows, cancellationToken);

    /// <summary>Writes with record batches of <paramref name="batchRows"/> rows; the tests make them small.</summary>
    internal static Task WriteAsync(Snapshot snapshot, Stream stream, int batchRows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchRows);
        if (!stream.CanWrite)
            throw new ArgumentException("The stream cannot be written.", nameof(stream));
        return new ArrowSnapshotWriter(snapshot, batchRows).WriteAsync(stream, cancellationToken);
    }
}

/// <summary>
/// The metadata keys under which a Snapshot's own facts travel in an Arrow schema (ADR-0064). Another
/// producer may write them too, and a read honours them whoever wrote them.
/// </summary>
public static class SnapshotArrowMetadata
{
    /// <summary>
    /// A field's caption: the data's own label for the column. Written only when the caption is not
    /// the column's name; a column read without it takes its name as its caption.
    /// </summary>
    public const string Caption = "exgrid.caption";

    /// <summary>
    /// The schema's Record Key: the name of the Text or Integer column whose value tells each record
    /// from every other. Written only when the Snapshot has one; a read with it refuses a Blank key and
    /// a key carried twice, naming the key.
    /// </summary>
    public const string RecordKey = "exgrid.recordKey";

    /// <summary>The schema's Snapshot version, in invariant decimal digits; a read without it gives version 0.</summary>
    public const string Version = "exgrid.version";
}
