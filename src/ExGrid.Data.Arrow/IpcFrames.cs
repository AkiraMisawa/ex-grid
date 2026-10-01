using System.Buffers.Binary;
using System.Globalization;

namespace ExGrid.Data.Arrow;

/// <summary>One message of an IPC stream: where it starts (its continuation marker or length) and
/// ends (after its body), its kind (1 a schema, 2 a dictionary batch, 3 a record batch), and a record
/// batch's rows.</summary>
internal readonly record struct IpcMessage(int Start, int End, byte Kind, long Rows);

/// <summary>
/// The framing of Arrow's IPC stream, read without Apache.Arrow: each message is an optional
/// continuation marker (<c>0xFFFFFFFF</c>), its metadata's length, the metadata — a flatbuffer
/// <c>Message</c> that says its kind and its body's length — and the body; a length of 0 is the
/// end-of-stream marker.
/// <para>
/// Arrow lets a stream end without the marker, by closing. A stream that does is read here as cut
/// short and refused, because a stream cut between two record batches is otherwise read as whole,
/// and its totals would be quietly short — what ADR-0063 refuses. Every producer the family reads
/// from (pyarrow, Polars, DuckDB, Apache.Arrow) writes the marker when its writer is closed.
/// </para>
/// </summary>
internal static class IpcFrames
{
    private const int Continuation = -1;
    private const byte SchemaMessage = 1;
    private const byte RecordBatchMessage = 3;

    /// <summary>The largest first message a stream is taken to start with: a schema's metadata. Bytes
    /// that begin otherwise — an error page, another format — are refused before Arrow reads them, and
    /// before it allocates what their first four bytes would claim.</summary>
    private const int MaxSchemaLength = 64 << 20;

    /// <summary>The smallest metadata a message has: a flatbuffer <c>Message</c> padded to eight bytes.</summary>
    private const int MinMetadataLength = 8;

    /// <summary>The bytes are not Arrow at all.</summary>
    public static SnapshotException NotArrow()
        => new("The bytes are not an Arrow IPC stream or file: they begin with neither an IPC message nor ARROW1.");

    /// <summary>The bytes are gzip's, as an HTTP response compressed with gzip is before it is undone.</summary>
    public static SnapshotException Gzipped()
        => new("The bytes are compressed with gzip, not an Arrow IPC stream or file: undo the compression first. "
            + "An HttpClient does when its handler's AutomaticDecompression includes GZip.");

    /// <summary>The stream holds no schema.</summary>
    public static SnapshotException Empty()
        => new("The Arrow stream is empty: it holds no schema.");

    /// <summary>The bytes end inside what would be a stream's first message, its schema.</summary>
    public static SnapshotException EndsInFirstMessage()
        => new("The bytes end inside their first message: they are not an Arrow IPC stream, or they are one cut short; a stream is read only whole.");

    /// <summary>The stream was cut short.</summary>
    public static SnapshotException CutShort()
        => new("The Arrow stream ends before its end-of-stream marker, so it may have been cut short; a stream is read only whole.");

    /// <summary>
    /// The length of a stream's first message — its framing and its metadata, the schema's — read
    /// from <paramref name="head"/>, the stream's first eight bytes or all it has, before Arrow reads
    /// it. The metadata's length must be one a schema can have, so the length is always more than
    /// eight bytes.
    /// </summary>
    /// <exception cref="SnapshotException">The stream is empty, ends at once, or is not Arrow.</exception>
    public static int FirstMessageLength(ReadOnlySpan<byte> head)
    {
        if (head.IsEmpty)
            throw Empty();
        if (head.Length >= 2 && head[0] == 0x1F && head[1] == 0x8B)
            throw Gzipped();
        if (head.Length < 4)
            throw NotArrow();
        var framing = 4;
        var length = BinaryPrimitives.ReadInt32LittleEndian(head);
        if (length == Continuation)
        {
            if (head.Length < 8)
                throw NotArrow();
            length = BinaryPrimitives.ReadInt32LittleEndian(head[4..]);
            framing = 8;
        }
        if (length == 0)
            throw Empty();
        if (length is < MinMetadataLength or > MaxSchemaLength)
            throw NotArrow();
        return framing + length;
    }

    /// <summary>Checks that <paramref name="message"/>, a stream's first message whole, is a schema.</summary>
    /// <exception cref="SnapshotException">It is not.</exception>
    public static void CheckSchema(ReadOnlySpan<byte> message)
    {
        var framing = BinaryPrimitives.ReadInt32LittleEndian(message) == Continuation ? 8 : 4;
        if (!TryReadMessage(message[framing..], out var kind, out _, out _) || kind != SchemaMessage)
            throw NotArrow();
    }

    /// <summary>
    /// Walks a whole stream held in memory up to its end-of-stream marker, and returns where each
    /// record batch message ends and the rows they hold together, for the progress a read reports.
    /// </summary>
    /// <exception cref="SnapshotException">The bytes do not begin with a schema message, or the stream
    /// ends before its marker.</exception>
    public static (long[] BatchEnds, long Rows) Walk(ReadOnlySpan<byte> stream)
    {
        var batches = Messages(stream).Where(m => m.Kind == RecordBatchMessage).ToArray();
        return ([.. batches.Select(m => (long)m.End)], batches.Sum(m => m.Rows));
    }

    /// <summary>A whole stream's messages, in order, up to its end-of-stream marker.</summary>
    /// <exception cref="SnapshotException">The bytes do not begin with a schema message, a message is
    /// malformed, or the stream ends before its marker.</exception>
    public static List<IpcMessage> Messages(ReadOnlySpan<byte> stream)
    {
        var firstLength = FirstMessageLength(stream[..Math.Min(8, stream.Length)]);
        if (firstLength > stream.Length)
            throw EndsInFirstMessage();
        CheckSchema(stream[..firstLength]);
        var messages = new List<IpcMessage>();
        var position = 0;
        while (true)
        {
            var start = position;
            if (stream.Length - position < 4)
                throw CutShort();
            var length = BinaryPrimitives.ReadInt32LittleEndian(stream[position..]);
            position += 4;
            if (length == Continuation)
            {
                if (stream.Length - position < 4)
                    throw CutShort();
                length = BinaryPrimitives.ReadInt32LittleEndian(stream[position..]);
                position += 4;
            }
            // The first message is a schema, so this ends a stream that holds one.
            if (length == 0)
                return messages;
            if (length < 0)
                throw Malformed(start, "claims a negative length");
            if (length > stream.Length - position)
                throw CutShort();
            if (!TryReadMessage(stream.Slice(position, length), out var kind, out var bodyLength, out var rows))
                throw Malformed(start, "is not a message");
            position += length;
            if (bodyLength < 0)
                throw Malformed(start, "claims a negative body");
            if (bodyLength > stream.Length - position)
                throw CutShort();
            position += (int)bodyLength;
            messages.Add(new IpcMessage(start, position, kind, rows));
        }
    }

    private static SnapshotException Malformed(int at, string what)
        => new(string.Create(CultureInfo.InvariantCulture, $"The Arrow stream is malformed: the message at byte {at:N0} {what}."));

    /// <summary>A message's kind, its body's length and, for a record batch, its rows.</summary>
    private static bool TryReadMessage(ReadOnlySpan<byte> metadata, out byte kind, out long bodyLength, out long rows)
    {
        kind = 0;
        bodyLength = 0;
        rows = 0;
        // Message: version (0), header_type (1), header (2), bodyLength (3), custom_metadata (4).
        if (!FlatTable.TryRoot(metadata, out var message)
            || !message.TryByte(1, out kind)
            || !message.TryLong(3, out bodyLength))
        {
            return false;
        }
        // RecordBatch: length (0), nodes (1), buffers (2), compression (3).
        if (kind == RecordBatchMessage && (!message.TryTable(2, out var batch) || !batch.TryLong(0, out rows)))
            return false;
        return true;
    }

    /// <summary>A flatbuffer table, read with every offset checked against the buffer.</summary>
    private readonly ref struct FlatTable
    {
        private readonly ReadOnlySpan<byte> buffer;
        private readonly int table;
        private readonly int vtable;
        private readonly int vtableSize;

        private FlatTable(ReadOnlySpan<byte> buffer, int table, int vtable, int vtableSize)
        {
            this.buffer = buffer;
            this.table = table;
            this.vtable = vtable;
            this.vtableSize = vtableSize;
        }

        public static bool TryRoot(ReadOnlySpan<byte> buffer, out FlatTable root)
        {
            root = default;
            return buffer.Length >= 4 && TryAt(buffer, BinaryPrimitives.ReadUInt32LittleEndian(buffer), out root);
        }

        public bool TryByte(int id, out byte value)
        {
            value = 0;
            if (!TryField(id, 1, out var at))
                return false;
            if (at >= 0)
                value = buffer[at];
            return true;
        }

        public bool TryLong(int id, out long value)
        {
            value = 0;
            if (!TryField(id, 8, out var at))
                return false;
            if (at >= 0)
                value = BinaryPrimitives.ReadInt64LittleEndian(buffer[at..]);
            return true;
        }

        public bool TryTable(int id, out FlatTable value)
        {
            value = default;
            return TryField(id, 4, out var at) && at >= 0
                && TryAt(buffer, (long)at + BinaryPrimitives.ReadUInt32LittleEndian(buffer[at..]), out value);
        }

        private static bool TryAt(ReadOnlySpan<byte> buffer, long table, out FlatTable result)
        {
            result = default;
            if (table < 0 || table > buffer.Length - 4)
                return false;
            var vtable = table - BinaryPrimitives.ReadInt32LittleEndian(buffer[(int)table..]);
            if (vtable < 0 || vtable > buffer.Length - 4)
                return false;
            var size = BinaryPrimitives.ReadUInt16LittleEndian(buffer[(int)vtable..]);
            if (size < 4 || vtable + size > buffer.Length)
                return false;
            result = new FlatTable(buffer, (int)table, (int)vtable, size);
            return true;
        }

        /// <summary>Where field <paramref name="id"/>'s value lies, or -1 when the table leaves it at its
        /// default; false when its slot points outside the buffer.</summary>
        private bool TryField(int id, int width, out int at)
        {
            at = -1;
            var slot = 4 + (2 * id);
            if (slot + 2 > vtableSize)
                return true;
            var offset = BinaryPrimitives.ReadUInt16LittleEndian(buffer[(vtable + slot)..]);
            if (offset == 0)
                return true;
            at = table + offset;
            return at <= buffer.Length - width;
        }
    }
}
