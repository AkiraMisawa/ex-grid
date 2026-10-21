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

    // ---- what the first cut let through -----------------------------------------------------------

    [Fact] // ADR-0330 §9: a part whose compressed bytes cannot be inflated is refused by name, as malformed, wherever it is read
    public async Task A_part_that_cannot_be_inflated_is_refused_by_name()
    {
        var sst = $"""<sst xmlns="{Main}"><si><t>only</t></si></sst>""";

        var sheet = await RefusalAsync(Damage(Book(OneCell), "xl/worksheets/sheet1.xml"));
        var strings = await RefusalAsync(Damage(Book(OneCell, sharedStrings: sst), "xl/sharedStrings.xml"));
        var types = await RefusalAsync(Damage(Book(OneCell), "[Content_Types].xml"));
        var rels = await RefusalAsync(Damage(Book(OneCell), "xl/_rels/workbook.xml.rels"));

        Assert.Equal((XlsxRefusal.Malformed, "xl/worksheets/sheet1.xml", "Sheet1"), (sheet.Reason, sheet.Part, sheet.Sheet));
        Assert.StartsWith("Sheet 'Sheet1': The part's compressed bytes cannot be inflated: ", sheet.Message);
        Assert.IsType<InvalidDataException>(sheet.InnerException);
        Assert.Equal((XlsxRefusal.Malformed, "xl/sharedStrings.xml"), (strings.Reason, strings.Part));
        Assert.Equal((XlsxRefusal.Malformed, "[Content_Types].xml"), (types.Reason, types.Part));
        Assert.Equal((XlsxRefusal.Malformed, "xl/_rels/workbook.xml.rels"), (rels.Reason, rels.Part));
    }

    public static TheoryData<string, string, string?> MisplacedCells => new()
    {
        { """<row r="1"><c r="A1"><v>1</v></c><c r="A1"><v>2</v></c></row>""", "Sheet 'Sheet1', cell A1: The cell comes after A1: a row's cells are in order, each once.", "A1" },
        { """<row r="1"><c r="B1"><v>1</v></c><c r="A1"><v>2</v></c></row>""", "Sheet 'Sheet1', cell A1: The cell comes after B1: a row's cells are in order, each once.", "A1" },
        { """<row r="1"><c><v>1</v></c><c r="A1"><v>2</v></c></row>""", "Sheet 'Sheet1', cell A1: The cell comes after A1: a row's cells are in order, each once.", "A1" },
        { """<row r="1"><c r="A2"><v>1</v></c></row>""", "Sheet 'Sheet1', cell A2: The cell is written inside row 1, not its own.", "A2" },
        { """<row r="2"><c r="A2"><v>1</v></c></row><row r="1"><c r="A1"><v>2</v></c></row>""", "Sheet 'Sheet1': Row 1 comes after row 2: a worksheet's rows are in order, each once.", null },
        { """<row r="1"><c r="A1"><v>1</v></c></row><row r="1"><c r="B1"><v>2</v></c></row>""", "Sheet 'Sheet1': Row 1 comes after row 1: a worksheet's rows are in order, each once.", null },
    };

    [Theory] // ADR-0330 §9: a cell written twice, out of order or inside another row is refused by name, never read into a place it does not hold
    [MemberData(nameof(MisplacedCells))]
    public async Task A_cell_out_of_place_is_refused_by_name(string rows, string message, string? cell)
    {
        var refusal = await RefusalAsync(Book(Sheet(rows)));

        Assert.Equal(XlsxRefusal.Malformed, refusal.Reason);
        Assert.Equal(message, refusal.Message);
        Assert.Equal(cell, refusal.Cell);
    }

    private static string Styles(string xf, string numFmts = "")
        => $"""
           <styleSheet xmlns="{Main}">{numFmts}<fonts count="1"><font/></fonts><fills count="2"><fill/><fill/></fills>
           <borders count="1"><border/></borders><cellXfs count="2"><xf/>{xf}</cellXfs></styleSheet>
           """;

    [Fact] // ADR-0330 §4: a cell naming a cell format the styles do not hold, or a cell format naming a font, fill, border or custom Number Format they do not, is refused by name
    public async Task A_style_the_styles_do_not_hold_is_refused_by_name()
    {
        var past = await RefusalAsync(Book(Sheet("""<row r="1"><c r="A1" s="2"><v>1</v></c></row>"""), styles: Styles("<xf/>")));
        var negative = await RefusalAsync(Book(Sheet("""<row r="1"><c r="B1" s="-1"><v>1</v></c></row>""")));
        var column = await RefusalAsync(Book(Sheet("", head: """<cols><col min="1" max="2" style="3"/></cols>"""), styles: Styles("<xf/>")));
        var font = await RefusalAsync(Book(OneCell, styles: Styles("""<xf fontId="1"/>""")));
        var fill = await RefusalAsync(Book(OneCell, styles: Styles("""<xf fillId="-1"/>""")));
        var border = await RefusalAsync(Book(OneCell, styles: Styles("""<xf borderId="1"/>""")));
        var format = await RefusalAsync(Book(OneCell, styles: Styles("""<xf numFmtId="200"/>""")));
        var builtIn = Book(Sheet("""<row r="1"><c r="A1" s="1"><v>1</v></c></row>"""), styles: Styles("""<xf numFmtId="14"/>"""));
        var custom = Book(Sheet("""<row r="1"><c r="A1" s="1"><v>1</v></c></row>"""),
            styles: Styles("""<xf numFmtId="200"/>""", """<numFmts count="1"><numFmt numFmtId="200" formatCode="0.000"/></numFmts>"""));

        Assert.Equal((XlsxRefusal.Malformed, "A1"), (past.Reason, past.Cell));
        Assert.Equal("Sheet 'Sheet1', cell A1: The cell names the cell format 2, which the styles do not hold.", past.Message);
        Assert.Equal((XlsxRefusal.Malformed, "B1"), (negative.Reason, negative.Cell));
        Assert.Equal("Sheet 'Sheet1': The columns 1 to 2 name the cell format 3, which the styles do not hold.", column.Message);
        Assert.Equal((XlsxRefusal.Malformed, "xl/styles.xml"), (font.Reason, font.Part));
        Assert.Equal("Part 'xl/styles.xml': The cell format 1 names the font 1, which the styles do not hold.", font.Message);
        Assert.Equal("Part 'xl/styles.xml': The cell format 1 names the fill -1, which the styles do not hold.", fill.Message);
        Assert.Equal("Part 'xl/styles.xml': The cell format 1 names the border 1, which the styles do not hold.", border.Message);
        Assert.Equal("Part 'xl/styles.xml': The cell format 1 names the Number Format 200, which the styles do not hold.", format.Message);
        using (var reader = await OpenAsync(builtIn))
            Assert.Equal(1, Assert.Single((await reader.ReadSheetAsync(0)).Cells).StyleIndex);
        using (var reader = await OpenAsync(custom))
            Assert.Equal("0.000", reader.Styles.NumberFormats[200]);
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

    [Fact] // ADR-0330 §9: a stored part, which the platform reads to its compressed size, is refused past the size its zip declares, naming the part and the Sheet
    public async Task A_stored_part_that_holds_more_than_it_declares_is_refused()
    {
        var sheet = Sheet(string.Concat(Enumerable.Range(1, 2_000).Select(i => $"""<row r="{i}"><c r="A{i}"><v>{i}</v></c></row>""")));
        var book = Stored(Book(sheet));
        Lie(book, "xl/worksheets/sheet1.xml", Encoding.UTF8.GetByteCount(sheet), 1_000);

        var refusal = await RefusalAsync(book);

        Assert.Equal(XlsxRefusal.TooLarge, refusal.Reason);
        Assert.Equal(("xl/worksheets/sheet1.xml", "Sheet1"), (refusal.Part, refusal.Sheet));
        Assert.Equal("Sheet 'Sheet1': The part inflates to more than the 1,000 bytes the zip declares for it.", refusal.Message);
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

    /// <summary>The same zip, every part stored rather than deflated.</summary>
    private static byte[] Stored(byte[] zip)
    {
        using var source = new System.IO.Compression.ZipArchive(new MemoryStream(zip), System.IO.Compression.ZipArchiveMode.Read);
        using var memory = new MemoryStream();
        using (var target = new System.IO.Compression.ZipArchive(memory, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                using var from = entry.Open();
                using var to = target.CreateEntry(entry.FullName, System.IO.Compression.CompressionLevel.NoCompression).Open();
                from.CopyTo(to);
            }
        }
        return memory.ToArray();
    }

    /// <summary>Makes <paramref name="part"/>'s deflate stream one that cannot be inflated: its first block's type is the
    /// reserved one.</summary>
    private static byte[] Damage(byte[] zip, string part)
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
