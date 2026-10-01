using System.Text;
using ExGrid.Data;
using ExGrid.Data.Csv;
using Xunit;

namespace ExGrid.Data.Tests;

/// <summary>Files and streams the CSV tests read, and the reads they make of them.</summary>
internal static class CsvFixtures
{
    /// <summary>A loose Schema of Text columns, one per name.</summary>
    public static CsvSchema Texts(params string[] names) => new([.. names.Select(n => new CsvColumn(n, SnapshotKind.Text))]);

    public static byte[] Utf8(string text, bool bom = false)
        => bom ? [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)] : Encoding.UTF8.GetBytes(text);

    /// <summary>Reads <paramref name="text"/>, as UTF-8, under <paramref name="schema"/>.</summary>
    public static Snapshot Read(CsvSchema schema, string text) => Read(schema, Utf8(text));

    public static Snapshot Read(CsvSchema schema, byte[] bytes, int bufferSize = CsvLoad.DefaultBufferSize, int chunk = int.MaxValue)
        => CsvLoad.ReadAsync(schema, new Trickle(bytes, chunk), null, TestContext.Current.CancellationToken, bufferSize).AsTask().GetAwaiter().GetResult();

    /// <summary>The refusal of reading <paramref name="text"/> under <paramref name="schema"/>, which
    /// must yield no Snapshot.</summary>
    public static SnapshotException Refusal(CsvSchema schema, string text) => Refusal(schema, Utf8(text));

    public static SnapshotException Refusal(CsvSchema schema, byte[] bytes, int bufferSize = CsvLoad.DefaultBufferSize)
    {
        Snapshot? built = null;
        var refusal = Assert.Throws<SnapshotException>(() => built = Read(schema, bytes, bufferSize));
        Assert.Null(built);
        return refusal;
    }

    /// <summary>A stream that gives at most <paramref name="chunk"/> bytes a read, as a network does,
    /// and seeks only when asked to.</summary>
    public sealed class Trickle(byte[] bytes, int chunk = int.MaxValue, bool seekable = true) : Stream
    {
        private int position;

        public int Reads { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => seekable;

        public override bool CanWrite => false;

        public override long Length => seekable ? bytes.Length : throw new NotSupportedException();

        public override long Position
        {
            get => seekable ? position : throw new NotSupportedException();
            set => position = seekable ? (int)value : throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            Reads++;
            var n = Math.Min(Math.Min(buffer.Length, chunk), bytes.Length - position);
            bytes.AsSpan(position, n).CopyTo(buffer);
            position += n;
            return n;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Progress told at once, on the thread that reports it.</summary>
    public sealed class Told(List<SnapshotProgress> reports) : IProgress<SnapshotProgress>
    {
        public void Report(SnapshotProgress value) => reports.Add(value);
    }
}
