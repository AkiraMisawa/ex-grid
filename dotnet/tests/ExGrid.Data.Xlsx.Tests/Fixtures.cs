using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace ExGrid.Data.Xlsx.Tests;

/// <summary>Packages built part by part, as Excel lays them out, and the reads the tests share.</summary>
internal static class Fixtures
{
    public const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A zip of the parts, each written as UTF-8.</summary>
    public static byte[] Zip(params (string Name, string Content)[] parts)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in parts)
            {
                using var stream = zip.CreateEntry(name).Open();
                var bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes);
            }
        }
        return memory.ToArray();
    }

    public static string ContentTypes(string workbookType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml", string extra = "")
        => $"""
           <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
           <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
             <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
             <Default Extension="xml" ContentType="application/xml"/>
             <Override PartName="/xl/workbook.xml" ContentType="{workbookType}"/>
             <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
             {extra}
           </Types>
           """;

    public const string RootRels = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    public static string WorkbookXml(string sheets = """<sheet name="Sheet1" sheetId="1" r:id="rId1"/>""", string head = "")
        => $"""
           <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
           <workbook xmlns="{Main}" xmlns:r="{Rel}">{head}<sheets>{sheets}</sheets></workbook>
           """;

    public static string WorkbookRels(string extra = "")
        => $"""
           <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
           <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
             <Relationship Id="rId1" Type="{Rel}/worksheet" Target="worksheets/sheet1.xml"/>
             {extra}
           </Relationships>
           """;

    public static string Sheet(string sheetData, string head = "", string tail = "")
        => $"""
           <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
           <worksheet xmlns="{Main}" xmlns:r="{Rel}">{head}<sheetData>{sheetData}</sheetData>{tail}</worksheet>
           """;

    /// <summary>A workbook of one worksheet, <paramref name="sheetXml"/>, with the parts given beside it, each related from the
    /// workbook part by its relationship type's last word (<c>sharedStrings</c>, <c>styles</c>, <c>theme</c>, <c>sheetMetadata</c>).</summary>
    public static byte[] Book(string sheetXml, string? sharedStrings = null, string? styles = null, string? theme = null, string? metadata = null,
        string? workbook = null)
    {
        var parts = new List<(string, string)>
        {
            ("[Content_Types].xml", ContentTypes()),
            ("_rels/.rels", RootRels),
            ("xl/workbook.xml", workbook ?? WorkbookXml()),
            ("xl/worksheets/sheet1.xml", sheetXml),
        };
        var rels = new StringBuilder();
        var id = 2;
        void Add(string? content, string type, string target)
        {
            if (content is null)
                return;
            parts.Add(("xl/" + target, content));
            rels.Append($"""<Relationship Id="rId{id++}" Type="{Rel}/{type}" Target="{target}"/>""");
        }
        Add(sharedStrings, "sharedStrings", "sharedStrings.xml");
        Add(styles, "styles", "styles.xml");
        Add(theme, "theme", "theme/theme1.xml");
        Add(metadata, "sheetMetadata", "metadata.xml");
        parts.Add(("xl/_rels/workbook.xml.rels", WorkbookRels(rels.ToString())));
        return Zip([.. parts]);
    }

    public static async Task<XlsxReader> OpenAsync(byte[] file, XlsxReadOptions? options = null)
        => await XlsxReader.OpenAsync(new MemoryStream(file, writable: false), options, Token);

    /// <summary>Every cell of every worksheet, read a batch at a time.</summary>
    public static async Task<List<List<XlsxCell>>> ReadAllAsync(XlsxReader reader)
    {
        var sheets = new List<List<XlsxCell>>();
        for (var i = 0; i < reader.Sheets.Count; i++)
        {
            if (reader.Sheets[i].Part is null)
            {
                sheets.Add([]);
                continue;
            }
            sheets.Add([.. (await reader.ReadSheetAsync(i)).Cells]);
        }
        return sheets;
    }

    /// <summary>The refusal opening <paramref name="file"/> and reading all of it meets.</summary>
    public static async Task<XlsxException> RefusalAsync(byte[] file, XlsxReadOptions? options = null)
        => await Assert.ThrowsAsync<XlsxException>(async () =>
        {
            using var reader = await OpenAsync(file, options);
            await ReadAllAsync(reader);
        });

    /// <summary>A workbook written by <see cref="XlsxWriter"/>.</summary>
    public static async Task<byte[]> WriteAsync(Func<XlsxWriter, Task> write, XlsxWriteOptions? options = null)
    {
        using var memory = new MemoryStream();
        await using (var writer = XlsxWriter.Create(memory, options))
        {
            await write(writer);
            await writer.CompleteAsync(Token);
        }
        return memory.ToArray();
    }

    /// <summary>An OLE compound file (MS-CFB, version 3) whose directory lists <paramref name="names"/>, four to a sector.</summary>
    public static byte[] Ole(params string[] names)
    {
        const int size = 512;
        var dirSectors = (names.Length + 3) / 4;
        var file = new byte[size * (2 + dirSectors)];
        var header = file.AsSpan();
        ReadOnlySpan<byte> signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
        signature.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header[24..], 0x3E);
        BinaryPrimitives.WriteUInt16LittleEndian(header[26..], 3);
        BinaryPrimitives.WriteUInt16LittleEndian(header[28..], 0xFFFE);
        BinaryPrimitives.WriteUInt16LittleEndian(header[30..], 9);
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..], 6);
        BinaryPrimitives.WriteUInt32LittleEndian(header[44..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header[48..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header[56..], 4096);
        BinaryPrimitives.WriteUInt32LittleEndian(header[60..], 0xFFFFFFFE);
        BinaryPrimitives.WriteUInt32LittleEndian(header[68..], 0xFFFFFFFE);
        for (var i = 0; i < 109; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(header[(76 + i * 4)..], i == 0 ? 0u : 0xFFFFFFFF);
        // The FAT, in sector 0: itself, then the directory's chain, sector 1 onward.
        var fat = file.AsSpan(size, size);
        fat.Fill(0xFF);
        BinaryPrimitives.WriteUInt32LittleEndian(fat, 0xFFFFFFFD);
        for (var s = 1; s <= dirSectors; s++)
            BinaryPrimitives.WriteUInt32LittleEndian(fat[(s * 4)..], s == dirSectors ? 0xFFFFFFFE : (uint)(s + 1));
        for (var i = 0; i < names.Length; i++)
        {
            var entry = file.AsSpan(size * 2 + i * 128, 128);
            var name = Encoding.Unicode.GetBytes(names[i] + "\0");
            name.CopyTo(entry);
            BinaryPrimitives.WriteUInt16LittleEndian(entry[64..], (ushort)name.Length);
            entry[66] = (byte)(i == 0 ? 5 : 2);
        }
        return file;
    }

    /// <summary>The same zip, every part stored rather than deflated.</summary>
    public static byte[] Stored(byte[] zip)
    {
        using var source = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        using var memory = new MemoryStream();
        using (var target = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                using var from = entry.Open();
                using var to = target.CreateEntry(entry.FullName, CompressionLevel.NoCompression).Open();
                from.CopyTo(to);
            }
        }
        return memory.ToArray();
    }

    /// <summary>Makes <paramref name="part"/>'s deflate stream one that cannot be inflated: its first block's type is the
    /// reserved one.</summary>
    public static byte[] Damage(byte[] zip, string part)
    {
        var name = Encoding.UTF8.GetBytes(part);
        for (var at = 0; at + 30 < zip.Length; at++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(at)) != 0x04034b50)
                continue;
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(at + 26));
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(at + 28));
            if (nameLength == name.Length && zip.AsSpan(at + 30, nameLength).SequenceEqual(name))
            {
                Assert.Equal(8, BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(at + 8)));
                zip[at + 30 + nameLength + extraLength] = 0b111;
                return zip;
            }
        }
        throw new InvalidOperationException($"The zip has no part '{part}'.");
    }

    /// <summary>Rewrites the uncompressed size the zip declares for <paramref name="part"/>, in its local header and its central
    /// directory entry, from <paramref name="actual"/> to <paramref name="declared"/>.</summary>
    public static void Lie(byte[] zip, string part, int actual, int declared)
    {
        var name = Encoding.UTF8.GetBytes(part);
        for (var at = 0; at + 46 < zip.Length; at++)
        {
            var signature = BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(at));
            int nameAt, sizeAt;
            if (signature == 0x04034b50)
            {
                nameAt = at + 30;
                sizeAt = at + 22;
            }
            else if (signature == 0x02014b50)
            {
                nameAt = at + 46;
                sizeAt = at + 24;
            }
            else
            {
                continue;
            }
            if (nameAt + name.Length <= zip.Length && zip.AsSpan(nameAt, name.Length).SequenceEqual(name)
                && BinaryPrimitives.ReadInt32LittleEndian(zip.AsSpan(sizeAt)) == actual)
            {
                BinaryPrimitives.WriteInt32LittleEndian(zip.AsSpan(sizeAt), declared);
            }
        }
    }

    /// <summary>A stream that cannot seek, as a browser's file and a request body are.</summary>
    public sealed class Forward(byte[] bytes) : Stream
    {
        private readonly MemoryStream inner = new(bytes, writable: false);

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>A stream that refuses synchronous writes, as ASP.NET Core's response body does.</summary>
    public sealed class AsyncOnly : Stream
    {
        private readonly MemoryStream inner = new();

        public byte[] ToArray() => inner.ToArray();

        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous writes are disallowed.");

        public override void Flush() => throw new InvalidOperationException("Synchronous flushes are disallowed.");

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => inner.WriteAsync(buffer, offset, count, cancellationToken);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => inner.WriteAsync(buffer, cancellationToken);

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
