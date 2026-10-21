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
