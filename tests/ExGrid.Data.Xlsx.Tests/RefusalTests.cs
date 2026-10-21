using System.Buffers.Binary;
using System.Text;
using Xunit;
using static ExGrid.Data.Xlsx.Tests.Fixtures;

namespace ExGrid.Data.Xlsx.Tests;

/// <summary>
/// Every refusal, by name (ADR-0330 §4, §9): a file that is not a plain <c>.xlsx</c>, a part that declares a DTD or is not
/// well-formed, and a file past a limit are refused with their reason, and the part, the Sheet and the cell they met.
/// Nothing of a refused file is read.
/// </summary>
public class RefusalTests
{
    private static readonly string OneCell = Sheet("""<row r="1"><c r="A1"><v>1</v></c></row>""");

    // ---- not a plain .xlsx ---------------------------------------------------------------------

    [Fact] // ADR-0330 §4: bytes that are not a zip are refused, and an empty file says so
    public async Task Not_a_zip_is_refused()
    {
        var text = await RefusalAsync(Encoding.UTF8.GetBytes("Desk,Notional\nEMEA,1\n"));
        var empty = await RefusalAsync([]);
        var damaged = await RefusalAsync([.. "PK"u8, 3, 4, .. new byte[40]]);

        Assert.Equal(XlsxRefusal.NotAZip, text.Reason);
        Assert.Equal("The file is not a zip package, so it is not an .xlsx.", text.Message);
        Assert.Equal(XlsxRefusal.NotAZip, empty.Reason);
        Assert.Equal("The file is empty.", empty.Message);
        Assert.Equal(XlsxRefusal.NotAZip, damaged.Reason);
    }

    [Fact] // ADR-0330 §4: a zip that is another document, or no Office document at all, is refused as not a workbook
    public async Task A_zip_that_is_not_a_workbook_is_refused()
    {
        var docx = Zip(
            ("[Content_Types].xml", ContentTypes().Replace("/xl/workbook.xml", "/word/document.xml")
                .Replace("spreadsheetml.sheet.main+xml", "wordprocessingml.document.main+xml")),
            ("_rels/.rels", RootRels.Replace("xl/workbook.xml", "word/document.xml")),
            ("word/document.xml", "<document/>"));
        var plain = Zip(("readme.txt", "hello"));

        var refusal = await RefusalAsync(docx);

        Assert.Equal(XlsxRefusal.NotAWorkbook, refusal.Reason);
        Assert.Equal("word/document.xml", refusal.Part);
        Assert.Equal(
            "Part 'word/document.xml': The package's document is 'application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml', not a workbook: the file is not an .xlsx.",
            refusal.Message);
        Assert.Equal(XlsxRefusal.NotAWorkbook, (await RefusalAsync(plain)).Reason);
    }

    [Fact] // ADR-0330 §4: a workbook with macros (.xlsm) is refused by its content type
    public async Task A_macro_enabled_workbook_is_refused()
    {
        var xlsm = Zip(
            ("[Content_Types].xml", ContentTypes("application/vnd.ms-excel.sheet.macroEnabled.main+xml",
                """<Default Extension="bin" ContentType="application/vnd.ms-office.vbaProject"/>""")),
            ("_rels/.rels", RootRels), ("xl/workbook.xml", WorkbookXml()), ("xl/_rels/workbook.xml.rels", WorkbookRels()),
            ("xl/worksheets/sheet1.xml", OneCell), ("xl/vbaProject.bin", "VBA"));

        var refusal = await RefusalAsync(xlsm);

        Assert.Equal(XlsxRefusal.MacroEnabled, refusal.Reason);
        Assert.Equal(
            "Part 'xl/workbook.xml': The file is a workbook with macros (.xlsm), which is not read. Save it from Excel as an Excel Workbook (.xlsx), which leaves the macros out.",
            refusal.Message);
    }

    [Fact] // ADR-0330 §4: a binary workbook (.xlsb) is refused, though its content type also says macro-enabled
    public async Task A_binary_workbook_is_refused()
    {
        var xlsb = Zip(
            ("[Content_Types].xml", ContentTypes("application/vnd.ms-excel.sheet.binary.macroEnabled.main").Replace("/xl/workbook.xml", "/xl/workbook.bin")),
            ("_rels/.rels", RootRels.Replace("xl/workbook.xml", "xl/workbook.bin")),
            ("xl/workbook.bin", "binary"));

        var refusal = await RefusalAsync(xlsb);

        Assert.Equal(XlsxRefusal.Binary, refusal.Reason);
        Assert.Equal("xl/workbook.bin", refusal.Part);
    }

    [Fact] // ADR-0330 §4: an Excel 97–2003 workbook (.xls), an OLE compound file, is refused as legacy
    public async Task A_legacy_workbook_is_refused()
    {
        var refusal = await RefusalAsync(Ole("Root Entry", "Workbook", "\u0005SummaryInformation"));

        Assert.Equal(XlsxRefusal.Legacy, refusal.Reason);
        Assert.Equal(
            "The file is an Excel 97–2003 workbook (.xls), or another Office binary file, not an .xlsx. Save it from Excel as an Excel Workbook (.xlsx).",
            refusal.Message);
    }

    [Fact] // ADR-0330 §4: an encrypted workbook — an OLE compound file holding an EncryptedPackage — is refused as encrypted, not as legacy
    public async Task An_encrypted_workbook_is_refused()
    {
        var refusal = await RefusalAsync(Ole("Root Entry", "EncryptionInfo", "EncryptedPackage", "\u0006DataSpaces"));

        Assert.Equal(XlsxRefusal.Encrypted, refusal.Reason);
        Assert.StartsWith("The file is an encrypted workbook.", refusal.Message);
    }

    [Fact] // ADR-0330 §4: an encrypted workbook is told from a legacy one when its directory spans more than one sector
    public async Task An_encrypted_workbook_is_found_past_the_first_directory_sector()
    {
        var refusal = await RefusalAsync(Ole("Root Entry", "\u0006DataSpaces", "Version", "DataSpaceMap", "DataSpaceInfo", "EncryptedPackage"));

        Assert.Equal(XlsxRefusal.Encrypted, refusal.Reason);
    }

    [Fact] // ADR-0330 §4: Strict Open XML, whose names differ from the transitional ones, is refused rather than read as empty
    public async Task Strict_open_xml_is_refused()
    {
        var strict = Zip(
            ("[Content_Types].xml", ContentTypes()),
            ("_rels/.rels", RootRels.Replace("http://schemas.openxmlformats.org/officeDocument/2006/relationships", "http://purl.oclc.org/ooxml/officeDocument/relationships")),
            ("xl/workbook.xml", WorkbookXml()));

        var refusal = await RefusalAsync(strict);

        Assert.Equal(XlsxRefusal.StrictOpenXml, refusal.Reason);
    }

    // ---- what a part may not do ----------------------------------------------------------------

    private const string Laughs = """
        <?xml version="1.0"?>
        <!DOCTYPE lolz [<!ENTITY lol "lol"><!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;">]>
        """;

    [Fact] // ADR-0330 §9: a worksheet that declares a DTD is refused, naming the Sheet and the part; no entity is expanded
    public async Task A_dtd_in_a_worksheet_is_refused()
    {
        var sheet = Laughs + $"""<worksheet xmlns="{Main}"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>&lol2;</t></is></c></row></sheetData></worksheet>""";

        var refusal = await RefusalAsync(Book(sheet));

        Assert.Equal(XlsxRefusal.Dtd, refusal.Reason);
        Assert.Equal("Sheet1", refusal.Sheet);
        Assert.Equal("xl/worksheets/sheet1.xml", refusal.Part);
        Assert.Equal("Sheet 'Sheet1': The part declares a document type (<!DOCTYPE), which a workbook never needs; it is not read.", refusal.Message);
    }

    [Fact] // ADR-0330 §9: a DTD in the shared strings, or in the package's own parts, is refused at opening
    public async Task A_dtd_in_any_part_is_refused()
    {
        var sst = await RefusalAsync(Book(OneCell, sharedStrings: Laughs + $"""<sst xmlns="{Main}"><si><t>&lol2;</t></si></sst>"""));
        var types = await RefusalAsync(Zip(("[Content_Types].xml", Laughs + "<Types/>"), ("_rels/.rels", RootRels)));

        Assert.Equal(XlsxRefusal.Dtd, sst.Reason);
        Assert.Equal("xl/sharedStrings.xml", sst.Part);
        Assert.Equal(XlsxRefusal.Dtd, types.Reason);
        Assert.Equal("[Content_Types].xml", types.Part);
    }

    [Fact] // ADR-0330 §4: a worksheet that is not well-formed is refused, naming the Sheet
    public async Task A_part_that_is_not_well_formed_is_refused()
    {
        var refusal = await RefusalAsync(Book(Sheet("""<row r="1"><c r="A1"><v>1</v></row>""")));

        Assert.Equal(XlsxRefusal.Malformed, refusal.Reason);
        Assert.Equal("Sheet1", refusal.Sheet);
        Assert.StartsWith("Sheet 'Sheet1': The part is not well-formed XML: ", refusal.Message);
    }

    public static TheoryData<string, string, string?> MalformedCells => new()
    {
        { """<row r="1"><c r="A1" t="s"><v>3</v></c></row>""", "Sheet 'Sheet1', cell A1: The cell names the shared string '3', which the workbook does not hold.", "A1" },
        { """<row r="2"><c r="B2" t="x"><v>3</v></c></row>""", "Sheet 'Sheet1', cell B2: 'x' is not a cell type.", "B2" },
        { """<row r="1"><c r="XFE1"><v>3</v></c></row>""", "Sheet 'Sheet1': 'XFE1' is not a cell of the sheet.", null },
        { """<row r="1048577"><c><v>3</v></c></row>""", "Sheet 'Sheet1': Row 1048577 lies outside the sheet's rows.", null },
        { """<row r="1"><c r="C1"><f t="shared">A1</f><v>3</v></c></row>""", "Sheet 'Sheet1', cell C1: A shared formula names no group (si).", "C1" },
    };

    [Theory] // ADR-0330 §4: a cell the reader cannot place or read is refused, naming the Sheet and, when it has one, the cell
    [MemberData(nameof(MalformedCells))]
    public async Task A_cell_that_cannot_be_read_is_refused_by_name(string rows, string message, string? cell)
    {
        var refusal = await RefusalAsync(Book(Sheet(rows), sharedStrings: $"""<sst xmlns="{Main}"><si><t>only</t></si></sst>"""));

        Assert.Equal(XlsxRefusal.Malformed, refusal.Reason);
        Assert.Equal(message, refusal.Message);
        Assert.Equal(cell, refusal.Cell);
    }

    // ---- limits --------------------------------------------------------------------------------

    [Fact] // ADR-0330 §9: a file whose parts hold more than the size limit is refused at opening, before any is read
    public async Task Beyond_the_size_limit_is_refused()
    {
        var book = Book(Sheet(string.Concat(Enumerable.Range(1, 2_000).Select(i => $"""<row r="{i}"><c r="A{i}"><v>{i}</v></c></row>"""))));

        var refusal = await RefusalAsync(book, new XlsxReadOptions { MaxUncompressedBytes = 50_000 });
        var forward = await Assert.ThrowsAsync<XlsxException>(async () =>
            await XlsxReader.OpenAsync(new Forward(book), new XlsxReadOptions { MaxUncompressedBytes = 1_000 }, Token));

        Assert.Equal(XlsxRefusal.TooLarge, refusal.Reason);
        Assert.Equal("The file holds more than the 50,000 uncompressed bytes a read is allowed (XlsxReadOptions.MaxUncompressedBytes).", refusal.Message);
        Assert.Equal(XlsxRefusal.TooLarge, forward.Reason);
        Assert.Equal("The file is larger than the 1,000 uncompressed bytes a read is allowed (XlsxReadOptions.MaxUncompressedBytes).", forward.Message);
    }

    [Fact] // ADR-0330 §9: a part whose zip entry declares fewer bytes than it inflates to — a zip bomb's lie — is refused, not read on
    public async Task A_part_that_inflates_past_its_declared_size_is_refused()
    {
        var sheet = Sheet(string.Concat(Enumerable.Range(1, 2_000).Select(i => $"""<row r="{i}"><c r="A{i}"><v>{i}</v></c></row>""")));
        var book = Book(sheet);
        var declared = Encoding.UTF8.GetByteCount(sheet);
        Lie(book, "xl/worksheets/sheet1.xml", declared, 1_000);

        var refusal = await RefusalAsync(book);

        // The part is inflated no further than it declares, so what it holds past that is never read: the XML is cut
        // short, and the sheet is refused as not well-formed.
        Assert.Equal(XlsxRefusal.Malformed, refusal.Reason);
        Assert.Equal("xl/worksheets/sheet1.xml", refusal.Part);
        Assert.Equal("Sheet1", refusal.Sheet);
    }

    [Fact] // ADR-0330 §9: past the cell limit the file is refused, naming the Sheet and the first cell past it
    public async Task Beyond_the_cell_limit_is_refused()
    {
        var book = Book(Sheet("""<row r="1"><c r="A1"><v>1</v></c><c r="B1"><v>2</v></c></row><row r="2"><c r="A2"><v>3</v></c><c r="B2"><v>4</v></c></row>"""));

        var refusal = await RefusalAsync(book, new XlsxReadOptions { MaxCells = 3 });
        using var reader = await OpenAsync(book, new XlsxReadOptions { MaxCells = 4 });

        Assert.Equal(XlsxRefusal.TooManyCells, refusal.Reason);
        Assert.Equal("Sheet1", refusal.Sheet);
        Assert.Equal("B2", refusal.Cell);
        Assert.Equal("Sheet 'Sheet1', cell B2: The workbook holds more than the 3 cells a read is allowed (XlsxReadOptions.MaxCells).", refusal.Message);
        Assert.Equal(4, (await reader.ReadSheetAsync(0)).Cells.Count);
    }

    [Fact] // ADR-0330 §9: the limits default to the values the measurement recorded in ticket 01 set
    public void The_limits_default_to_the_measured_values()
    {
        var options = new XlsxReadOptions();

        Assert.Equal(XlsxReadOptions.DefaultMaxUncompressedBytes, options.MaxUncompressedBytes);
        Assert.Equal(XlsxReadOptions.DefaultMaxCells, options.MaxCells);
    }

    // ---- files built for the refusals ------------------------------------------------------------

    /// <summary>An OLE compound file (MS-CFB, version 3) whose directory lists <paramref name="names"/>, four to a sector.</summary>
    private static byte[] Ole(params string[] names)
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

    /// <summary>Rewrites the uncompressed size the zip declares for <paramref name="part"/>, in its local header and its central
    /// directory entry, from <paramref name="actual"/> to <paramref name="declared"/>.</summary>
    private static void Lie(byte[] zip, string part, int actual, int declared)
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
}
