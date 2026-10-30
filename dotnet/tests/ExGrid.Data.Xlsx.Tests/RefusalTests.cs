using System.Buffers.Binary;
using System.Text;
using Xunit;
using static ExGrid.Data.Xlsx.Tests.Fixtures;

namespace ExGrid.Data.Xlsx.Tests;

/// <summary>
/// The refusals the shared cases cannot hold (ADR-0330 §4, §9; the cases are <c>tests/cases/xlsx/refusals.json</c>): a stream
/// that cannot seek refused past the size limit as it is copied, the cause a refusal carries, the limits' defaults, and an OLE
/// file classified without being read whole.
/// </summary>
public class RefusalTests
{
    private static readonly string OneCell = Sheet("""<row r="1"><c r="A1"><v>1</v></c></row>""");

    [Fact] // ADR-0330 §9: a stream that cannot seek is refused past the size limit while it is copied, before any part is read
    public async Task Beyond_the_size_limit_a_forward_stream_is_refused_as_it_is_copied()
    {
        var book = Book(Sheet(string.Concat(Enumerable.Range(1, 2_000).Select(i => $"""<row r="{i}"><c r="A{i}"><v>{i}</v></c></row>"""))));

        var forward = await Assert.ThrowsAsync<XlsxException>(async () =>
            await XlsxReader.OpenAsync(new Forward(book), new XlsxReadOptions { MaxUncompressedBytes = 1_000 }, Token));

        Assert.Equal(XlsxRefusal.TooLarge, forward.Reason);
        Assert.Equal("The file is larger than the 1,000 uncompressed bytes a read is allowed (XlsxReadOptions.MaxUncompressedBytes).", forward.Message);
    }

    [Fact] // ADR-0330 §9: the limits default to the values the measurement recorded in ticket 01 set
    public void The_limits_default_to_the_measured_values()
    {
        var options = new XlsxReadOptions();

        Assert.Equal(XlsxReadOptions.DefaultMaxUncompressedBytes, options.MaxUncompressedBytes);
        Assert.Equal(XlsxReadOptions.DefaultMaxCells, options.MaxCells);
        Assert.Equal(XlsxReadOptions.DefaultMaxUncompressedBytes, new XlsxWriteOptions().MaxUncompressedBytes);
    }

    [Fact] // ADR-0330 §9: a part whose compressed bytes cannot be inflated is refused carrying the platform's own account of why
    public async Task A_part_that_cannot_be_inflated_carries_its_cause()
    {
        var sheet = await RefusalAsync(Damage(Book(OneCell), "xl/worksheets/sheet1.xml"));

        Assert.IsType<InvalidDataException>(sheet.InnerException);
    }

    [Fact] // ADR-0330 §4: an encrypted workbook is told from a legacy one by its header and directory alone, never by reading the file whole, whether its stream seeks or not
    public async Task An_ole_file_is_classified_without_being_read_whole()
    {
        byte[] Padded(byte[] ole) => [.. ole, .. new byte[4 << 20]];
        var seeking = new Counting(Padded(Ole("Root Entry", "\u0006DataSpaces", "Version", "DataSpaceMap", "DataSpaceInfo", "EncryptedPackage")), seekable: true);
        var forward = new Counting(Padded(Ole("Root Entry", "\u0006DataSpaces", "Version", "DataSpaceMap", "DataSpaceInfo", "EncryptedPackage")), seekable: false);
        var legacy = new Counting(Padded(Ole("Root Entry", "Workbook")), seekable: false);

        var a = await Assert.ThrowsAsync<XlsxException>(async () => await XlsxReader.OpenAsync(seeking, cancellationToken: Token));
        var b = await Assert.ThrowsAsync<XlsxException>(async () => await XlsxReader.OpenAsync(forward, cancellationToken: Token));
        var c = await Assert.ThrowsAsync<XlsxException>(async () => await XlsxReader.OpenAsync(legacy, cancellationToken: Token));

        Assert.Equal(XlsxRefusal.Encrypted, a.Reason);
        Assert.Equal(XlsxRefusal.Encrypted, b.Reason);
        Assert.Equal(XlsxRefusal.Legacy, c.Reason);
        // The header, the FAT and two directory sectors: a few sectors of the 4 MiB.
        Assert.InRange(seeking.BytesRead, 1, 4 * 512 + 8);
        Assert.InRange(forward.BytesRead, 1, 4 * 512);
        Assert.InRange(legacy.BytesRead, 1, 4 * 512);
        Assert.True(seeking.Disposed && forward.Disposed && legacy.Disposed);
    }

    // ---- files built for the refusals ------------------------------------------------------------

    /// <summary>A stream over <paramref name="bytes"/> that counts what is read from it, seeking or not.</summary>
    private sealed class Counting(byte[] bytes, bool seekable) : Stream
    {
        private readonly MemoryStream inner = new(bytes, writable: false);

        public long BytesRead { get; private set; }

        public bool Disposed { get; private set; }

        public override int Read(Span<byte> buffer)
        {
            var n = inner.Read(buffer);
            BytesRead += n;
            return n;
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(Read(buffer.Span));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromResult(Read(buffer.AsSpan(offset, count)));

        public override bool CanRead => true;
        public override bool CanSeek => seekable;
        public override bool CanWrite => false;
        public override long Length => seekable ? inner.Length : throw new NotSupportedException();
        public override long Position
        {
            get => seekable ? inner.Position : throw new NotSupportedException();
            set => inner.Position = seekable ? value : throw new NotSupportedException();
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => seekable ? inner.Seek(offset, origin) : throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
