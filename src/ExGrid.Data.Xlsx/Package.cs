using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace ExGrid.Data.Xlsx;

/// <summary>The names the package and its parts are read by (ECMA-376 Part 1 and Part 2, transitional).</summary>
internal static class Names
{
    public const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public const string PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    public const string ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
    public const string Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
    public const string StrictPrefix = "http://purl.oclc.org/ooxml/";

    public const string OfficeDocumentType = Relationships + "/officeDocument";
    public const string WorksheetType = Relationships + "/worksheet";
    public const string SharedStringsType = Relationships + "/sharedStrings";
    public const string StylesType = Relationships + "/styles";
    public const string ThemeType = Relationships + "/theme";
    public const string SheetMetadataType = Relationships + "/sheetMetadata";

    public const string WorkbookContent = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
    public const string TemplateContent = "application/vnd.openxmlformats-officedocument.spreadsheetml.template.main+xml";
    public const string BinaryContent = "application/vnd.ms-excel.sheet.binary.macroEnabled.main";
    public const string WorksheetContent = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
    public const string SharedStringsContent = "application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml";
    public const string StylesContent = "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml";
    public const string SheetMetadataContent = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheetMetadata+xml";
    public const string RelationshipsContent = "application/vnd.openxmlformats-package.relationships+xml";
    public const string VbaProjectContent = "application/vnd.ms-office.vbaProject";

    /// <summary>The future-metadata extension that marks a dynamic array (Excel 365).</summary>
    public const string DynamicArrayExtension = "{bdbb8cdc-fa1e-496e-a857-3c3f30c029c3}";
}

/// <summary>
/// The zip under a workbook: its parts by name, each opened through a stream that refuses a part that inflates past what
/// the zip declares, or whose compressed bytes cannot be inflated; and the XML of a part read with DTDs prohibited
/// (ADR-0330 §9).
/// </summary>
internal sealed class Package : IDisposable
{
    private readonly ZipArchive zip;
    private readonly Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.OrdinalIgnoreCase);

    private Package(ZipArchive zip)
    {
        this.zip = zip;
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith('/'))
                continue;
            entries.TryAdd(entry.FullName.TrimStart('/'), entry);
        }
    }

    public IEnumerable<string> PartNames => entries.Keys;

    public static Package Open(Stream stream, long maxUncompressedBytes)
    {
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException e)
        {
            throw new XlsxException(XlsxRefusal.NotAZip, "The file is not a zip package that can be read: " + e.Message, innerException: e);
        }
        var package = new Package(zip);
        try
        {
            long total = 0;
            foreach (var entry in package.entries.Values)
            {
                total += entry.Length;
                if (total > maxUncompressedBytes || entry.Length < 0)
                {
                    throw new XlsxException(XlsxRefusal.TooLarge, string.Create(CultureInfo.InvariantCulture,
                        $"The file holds more than the {maxUncompressedBytes:N0} uncompressed bytes a read is allowed (XlsxReadOptions.MaxUncompressedBytes)."));
                }
            }
        }
        catch
        {
            package.Dispose();
            throw;
        }
        return package;
    }

    public bool Has(string part) => entries.ContainsKey(part);

    public long LengthOf(string part) => entries.TryGetValue(part, out var e) ? e.Length : 0;

    /// <summary>The part's bytes as they inflate, or null when the package has no such part.</summary>
    /// <param name="part">The part's name.</param>
    /// <param name="sheet">The Sheet the part is, named by a refusal the read meets.</param>
    public Stream? OpenPart(string part, string? sheet = null)
    {
        if (!entries.TryGetValue(part, out var entry))
            return null;
        Stream inner;
        try
        {
            inner = entry.Open();
        }
        catch (InvalidDataException e)
        {
            throw new XlsxException(XlsxRefusal.NotAZip, "The part cannot be inflated: " + e.Message, part, sheet, innerException: e);
        }
        return new PartStream(inner, entry.Length, part, sheet);
    }

    /// <summary>A reader over the part's XML, DTDs prohibited, or null when the package has no such part.</summary>
    public XmlReader? OpenXml(string part)
    {
        var stream = OpenPart(part);
        return stream is null ? null : XmlReader.Create(stream, Settings);
    }

    public static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        CloseInput = true,
    };

    /// <summary>
    /// The refusal an <see cref="XmlException"/> met in <paramref name="part"/> stands for: a part that declares a
    /// document type, or one that is not well-formed.
    /// </summary>
    public XlsxException Refuse(XmlException e, string part, string? sheet = null)
    {
        if (DeclaresDoctype(part))
            return new XlsxException(XlsxRefusal.Dtd, "The part declares a document type (<!DOCTYPE), which a workbook never needs; it is not read.", part, sheet, innerException: e);
        return new XlsxException(XlsxRefusal.Malformed, "The part is not well-formed XML: " + e.Message, part, sheet, innerException: e);
    }

    private bool DeclaresDoctype(string part)
    {
        // The prolog comes first: a declaration, comments and processing instructions, then the document type
        // before the root element. The first 64 KiB hold any prolog a real part has.
        using var stream = OpenPart(part);
        if (stream is null)
            return false;
        var buffer = new byte[64 * 1024];
        var read = 0;
        while (read < buffer.Length)
        {
            int n;
            try
            {
                n = stream.Read(buffer, read, buffer.Length - read);
            }
            catch (Exception e) when (e is InvalidDataException or XlsxException)
            {
                break;
            }
            if (n == 0)
                break;
            read += n;
        }
        var text = Encoding.UTF8.GetString(buffer, 0, read);
        return text.Contains("<!DOCTYPE", StringComparison.Ordinal);
    }

    public void Dispose() => zip.Dispose();

    /// <summary>
    /// A part's inflated bytes, counted, and refused past what the zip declared: a zip bomb's headers may lie, and the
    /// sizes <see cref="Open"/> checked against <see cref="XlsxReadOptions.MaxUncompressedBytes"/> are the declared ones.
    /// .NET cuts a deflated part at its declared size, but reads a stored one to its compressed size, which may be larger.
    /// Bytes that cannot be inflated — a damaged deflate stream — are refused as malformed, naming the part, not left to
    /// escape as an <see cref="InvalidDataException"/>.
    /// </summary>
    private sealed class PartStream(Stream inner, long declared, string part, string? sheet) : Stream
    {
        private long read;

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                return Count(inner.Read(buffer, offset, count));
            }
            catch (InvalidDataException e)
            {
                throw Damaged(e);
            }
        }

        public override int Read(Span<byte> buffer)
        {
            try
            {
                return Count(inner.Read(buffer));
            }
            catch (InvalidDataException e)
            {
                throw Damaged(e);
            }
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            try
            {
                return Count(await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false));
            }
            catch (InvalidDataException e)
            {
                throw Damaged(e);
            }
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        private XlsxException Damaged(InvalidDataException e)
            => new(XlsxRefusal.Malformed, "The part's compressed bytes cannot be inflated: " + e.Message, part, sheet, innerException: e);

        private int Count(int n)
        {
            read += n;
            if (read > declared)
            {
                throw new XlsxException(XlsxRefusal.TooLarge, string.Create(CultureInfo.InvariantCulture,
                    $"The part inflates to more than the {declared:N0} bytes the zip declares for it."), part, sheet);
            }
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => declared;
        public override long Position { get => read; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }
    }
}

/// <summary>What the first bytes of a file say it is, before any zip is read.</summary>
internal static class Sniff
{
    private static ReadOnlySpan<byte> Ole => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public static bool IsOle(ReadOnlySpan<byte> head) => head.StartsWith(Ole);

    public static bool IsZip(ReadOnlySpan<byte> head)
        => head.Length >= 4 && head[0] == (byte)'P' && head[1] == (byte)'K'
           && ((head[2] == 3 && head[3] == 4) || (head[2] == 5 && head[3] == 6) || (head[2] == 7 && head[3] == 8));

    /// <summary>
    /// The refusal an OLE compound file stands for: an encrypted workbook, whose compound file holds an
    /// <c>EncryptedPackage</c> stream, or a legacy binary workbook. The compound file's directory is read through its
    /// sector chains (MS-CFB), and nothing else of the file: its header, the FAT sectors the directory's chain passes
    /// through, and the directory's own sectors. A directory that cannot be followed is taken for a legacy workbook.
    /// </summary>
    public static async ValueTask<XlsxException> ClassifyOleAsync(OleFile file, CancellationToken cancellationToken)
    {
        if (await IsEncryptedAsync(file, cancellationToken).ConfigureAwait(false))
        {
            return new XlsxException(XlsxRefusal.Encrypted,
                "The file is an encrypted workbook. Remove its password in Excel (File, Info, Protect Workbook, Encrypt with Password) and save it again.");
        }
        return new XlsxException(XlsxRefusal.Legacy,
            "The file is an Excel 97–2003 workbook (.xls), or another Office binary file, not an .xlsx. Save it from Excel as an Excel Workbook (.xlsx).");
    }

    private const uint LastSector = 0xFFFFFFFA;
    private const uint EndOfChain = 0xFFFFFFFE;
    private const int Guard = 1 << 16;

    /// <summary>Whether the compound file's directory names an <c>EncryptedPackage</c> or <c>EncryptionInfo</c> stream.</summary>
    private static async ValueTask<bool> IsEncryptedAsync(OleFile file, CancellationToken cancellationToken)
    {
        var header = new byte[512];
        if (!await file.ReadAsync(0, header, cancellationToken).ConfigureAwait(false))
            return false;
        var shift = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(30));
        if (shift is not (9 or 12))
            return false;
        var size = 1 << shift;
        var perSector = (uint)size / 4;
        long At(uint sector) => (long)(sector + 1) * size;

        // The FAT's own sectors: the first 109 the header lists, the rest along the DIFAT chain, read only when the
        // directory's chain reaches that far.
        var fatSectors = new List<uint>();
        for (var i = 0; i < 109; i++)
        {
            var s = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(76 + i * 4));
            if (s < LastSector)
                fatSectors.Add(s);
        }
        var difat = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(68));
        var difatRead = 0;
        var fatCache = new Dictionary<uint, byte[]?>();
        async ValueTask<uint> Next(uint sector)
        {
            var fat = sector / perSector;
            while (fat >= fatSectors.Count && difat < LastSector && difatRead++ < Guard)
            {
                var block = new byte[size];
                if (!await file.ReadAsync(At(difat), block, cancellationToken).ConfigureAwait(false))
                    return EndOfChain;
                for (var i = 0; i < size / 4 - 1; i++)
                {
                    var s = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(i * 4));
                    if (s < LastSector)
                        fatSectors.Add(s);
                }
                difat = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(size - 4));
            }
            if (fat >= fatSectors.Count)
                return EndOfChain;
            if (!fatCache.TryGetValue(fat, out var entries))
            {
                entries = new byte[size];
                if (!await file.ReadAsync(At(fatSectors[(int)fat]), entries, cancellationToken).ConfigureAwait(false))
                    entries = null;
                fatCache[fat] = entries;
            }
            return entries is null ? EndOfChain : BinaryPrimitives.ReadUInt32LittleEndian(entries.AsSpan((int)(sector % perSector * 4)));
        }

        var directory = new byte[size];
        var dir = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(48));
        for (var guard = 0; dir < LastSector && guard < Guard; guard++)
        {
            if (!await file.ReadAsync(At(dir), directory, cancellationToken).ConfigureAwait(false))
                break;
            for (var e = 0; e < size / 128; e++)
            {
                var entry = directory.AsSpan(e * 128, 128);
                var length = BinaryPrimitives.ReadUInt16LittleEndian(entry[64..]);
                if (length is >= 2 and <= 64 && Encoding.Unicode.GetString(entry[..(length - 2)]) is "EncryptedPackage" or "EncryptionInfo")
                    return true;
            }
            dir = await Next(dir).ConfigureAwait(false);
        }
        return false;
    }
}

/// <summary>
/// The bytes of an OLE compound file, read where they are asked for: a stream that seeks is read at each place, and one
/// that cannot is read forward only as far as the furthest byte asked for, and never past the size limit.
/// </summary>
internal abstract class OleFile
{
    /// <summary>Fills <paramref name="into"/> from <paramref name="offset"/>; false when the file ends first, or a stream
    /// that cannot seek would be read past the limit.</summary>
    public abstract ValueTask<bool> ReadAsync(long offset, Memory<byte> into, CancellationToken cancellationToken);

    /// <summary>A file in a stream that seeks, from <paramref name="start"/>.</summary>
    public static OleFile Seeking(Stream stream, long start) => new SeekingFile(stream, start);

    /// <summary>A file in a stream that cannot seek, whose first bytes, <paramref name="head"/>, are read already.</summary>
    public static OleFile Forward(Stream stream, ReadOnlyMemory<byte> head, long limit) => new ForwardFile(stream, head, limit);

    private sealed class SeekingFile(Stream stream, long start) : OleFile
    {
        public override async ValueTask<bool> ReadAsync(long offset, Memory<byte> into, CancellationToken cancellationToken)
        {
            if (start + offset + into.Length > stream.Length)
                return false;
            stream.Position = start + offset;
            return await stream.ReadAtLeastAsync(into, into.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false) == into.Length;
        }
    }

    private sealed class ForwardFile : OleFile
    {
        private readonly Stream stream;
        private readonly long limit;
        private readonly MemoryStream read = new();
        private bool ended;

        public ForwardFile(Stream stream, ReadOnlyMemory<byte> head, long limit)
        {
            this.stream = stream;
            this.limit = limit;
            read.Write(head.Span);
        }

        public override async ValueTask<bool> ReadAsync(long offset, Memory<byte> into, CancellationToken cancellationToken)
        {
            var need = offset + into.Length;
            if (need > limit)
                return false;
            if (need > read.Length && !ended)
            {
                var buffer = new byte[64 * 1024];
                while (read.Length < need)
                {
                    var n = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, need - read.Length)), cancellationToken).ConfigureAwait(false);
                    if (n == 0)
                    {
                        ended = true;
                        break;
                    }
                    read.Write(buffer, 0, n);
                }
            }
            if (need > read.Length)
                return false;
            read.GetBuffer().AsMemory((int)offset, into.Length).CopyTo(into);
            return true;
        }
    }
}
