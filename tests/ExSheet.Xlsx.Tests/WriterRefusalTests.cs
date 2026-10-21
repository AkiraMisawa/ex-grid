using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// What a write refuses, by name and with nothing reaching the stream (ADR-0330 §6, the spine's first rule), and what the cells
/// where formatted rows cross formatted columns cost (ticket 04's bound on ticket 03's crossings).
/// </summary>
public class WriterRefusalTests
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static CellAddress At(string at) => CellAddress.Parse(at);

    private static Workbook Entered(params (string At, Entry? Entry)[] cells)
    {
        var workbook = new Workbook(EnUs);
        workbook.Do("Sheet1", SheetEdit.SetEntries(cells.Select(c => KeyValuePair.Create(At(c.At), c.Entry))));
        return workbook;
    }

    /// <summary>The refusal, and that nothing reached the stream.</summary>
    private static async Task<XlsxSaveException> RefusedAsync(Workbook workbook, XlsxSaveOptions? options = null)
    {
        using var stream = new MemoryStream();
        var refused = await Assert.ThrowsAsync<XlsxSaveException>(async () => await XlsxWorkbookWriter.WriteAsync(workbook, stream, options, Token));
        Assert.Equal(0, stream.Length);
        return refused;
    }

    [Fact] // ADR-0330 §6: a Workbook whose Linked Table has not answered is refused: its rows are written as they stand, and it has none (ticket 05)
    public async Task ADR0330_a_workbook_with_a_linked_table_not_answered_is_refused()
    {
        var workbook = new Workbook(EnUs);
        workbook.DeclareLinkedTable("Positions", ["Desk", "PV"]);
        workbook.Do("Sheet1", SheetEdit.Enter(At("A1"), "=SUM(Positions[PV])"));

        var refused = await RefusedAsync(workbook);

        Assert.Equal(XlsxSaveRefusal.LinkedTable, refused.Reason);
        Assert.Contains("'Positions'", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0330 §6, ADR-0047: a cell that shows #CIRC! is refused by name: a file holds no such Error Value, and Excel would show 0
    public async Task ADR0330_a_circular_reference_is_refused_by_name()
    {
        var workbook = new Workbook(EnUs);
        workbook.Do("Sheet1", SheetEdit.Enter(new Dictionary<CellAddress, string> { [At("A1")] = "1", [At("B2")] = "=C2+1", [At("C2")] = "=B2+A1" }));

        var refused = await RefusedAsync(workbook);

        Assert.Equal((XlsxSaveRefusal.CircularReference, "Sheet1", "B2", "=C2+1"), (refused.Reason, refused.Sheet, refused.Cell, refused.Held));
        Assert.StartsWith("Sheet 'Sheet1', cell B2 (=C2+1): ", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0330 §6: a Formula longer than Excel's 8,192 characters is refused by name
    public async Task ADR0330_a_formula_longer_than_excel_holds_is_refused()
    {
        // Two hundred and forty arguments, each a Reference qualified with a 31-character Sheet name: 8,881 characters.
        var formula = "=SUM(" + string.Join(",", Enumerable.Repeat("'A Sheet name of 31 characters.'!A1", 240)) + ")";
        Assert.True(formula.Length - 1 > XlsxWorkbookWriter.MaxFormulaLength);
        var refused = await RefusedAsync(Entered(("C3", Entry.Parse(formula, EnUs))));

        Assert.Equal((XlsxSaveRefusal.Formula, "C3"), (refused.Reason, refused.Cell));
    }

    [Fact] // ADR-0330 §6: a Formula that reads a Sheet the Workbook does not have is refused by name: Excel would read it as another workbook's
    public async Task ADR0330_a_formula_reading_a_sheet_the_workbook_lacks_is_refused()
    {
        var workbook = new Workbook(EnUs, ["Trades", "Desks"]);
        workbook.Do("Trades", SheetEdit.Enter(new Dictionary<CellAddress, string> { [At("A1")] = "=desks!B2*2", [At("A2")] = "='Old desks'!B2*2" }));

        var refused = await RefusedAsync(workbook);

        Assert.Equal((XlsxSaveRefusal.Formula, "Trades", "A2", "='Old desks'!B2*2"), (refused.Reason, refused.Sheet, refused.Cell, refused.Held));
    }

    [Fact] // ADR-0330 §6: text longer than a cell of Excel holds, or holding half of a surrogate pair, is refused by name
    public async Task ADR0330_text_a_cell_of_excel_cannot_hold_is_refused()
    {
        var tooLong = await RefusedAsync(Entered(("A1", Entry.FromValue(Value.FromText(new string('x', XlsxWorkbookWriter.MaxTextLength + 1))))));
        var half = await RefusedAsync(Entered(("B2", Entry.FromValue(Value.FromText("a\uD800b")))));

        Assert.Equal((XlsxSaveRefusal.Text, "A1"), (tooLong.Reason, tooLong.Cell));
        Assert.Equal((XlsxSaveRefusal.Text, "B2"), (half.Reason, half.Cell));
    }

    [Fact] // ADR-0330 §6: text of exactly the most a cell of Excel holds is written, and a pair of surrogates is one character
    public async Task ADR0330_text_up_to_what_excel_holds_is_written()
    {
        var longest = new string('x', XlsxWorkbookWriter.MaxTextLength);
        var file = await Saved.WriteAsync(Entered(("A1", Entry.FromValue(Value.FromText(longest))), ("A2", Entry.FromValue(Value.FromText("💹 desk")))));

        var sheet = (await ReadAsync(file)).Document.Sheet("Sheet1");

        Assert.Equal(longest, sheet.EntryAt("A1")!.Constant!.Value.Text);
        Assert.Equal("💹 desk", sheet.EntryAt("A2")!.Constant!.Value.Text);
    }

    [Fact] // ADR-0330 §6, §9: a workbook that would write more cells than a file is read with is refused before anything is written
    public async Task ADR0330_more_cells_than_the_limit_are_refused()
    {
        var workbook = Entered(("A1", Entry.FromValue(Value.FromNumber(1))), ("A2", Entry.Parse("=SEQUENCE(3)", EnUs)));

        var refused = await RefusedAsync(workbook, new XlsxSaveOptions { MaxCells = 3 });
        using var stream = new MemoryStream();
        await XlsxWorkbookWriter.WriteAsync(workbook, stream, new XlsxSaveOptions { MaxCells = 4 }, Token);

        // Four cells hold a Value or an Entry: A1, the anchor and the two cells it spills into.
        Assert.Equal((XlsxSaveRefusal.TooManyCells, "Sheet1"), (refused.Reason, refused.Sheet));
        Assert.True(stream.Length > 0);
        Assert.Equal(XlsxReadOptions.DefaultMaxCells, new XlsxSaveOptions().MaxCells);
    }

    // ---- crossings -------------------------------------------------------------------------------------------------------

    private static readonly CellFill Yellow = CellFill.Solid(CellColour.FromRgb(0xFFFF00));
    private static readonly CellFill Red = CellFill.Solid(CellColour.FromRgb(0xFF0000));

    private static Workbook Formatted(IReadOnlyList<SheetDocumentAxisFormat> columns, IReadOnlyList<SheetDocumentAxisFormat> rows, params SheetDocumentCell[] cells) =>
        Workbook.Open(new WorkbookDocument(EnUs, "Sheet1", [new SheetDocument(EnUs, "Sheet1", cells) { Columns = columns, Rows = rows }]));

    private static async Task<List<XElement>> CellsAsync(byte[] file)
    {
        using var zip = new ZipArchive(new MemoryStream(file));
        await using var part = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        return [.. (await XDocument.LoadAsync(part, LoadOptions.None, Token)).Descendants(S + "c")];
    }

    [Fact] // ADR-0330 §4, ticket 04: where a formatted row crosses a formatted column that records a part the row leaves to it, a cell of no value shows the two together
    public async Task ADR0330_a_formatted_row_crossing_a_formatted_column_writes_a_cell_there()
    {
        var workbook = Formatted(
            [new SheetDocumentAxisFormat(1, 3, null, null, null, Yellow, null)],
            [new SheetDocumentAxisFormat(1, 2, null, null, new CellFont(Bold: true), null, null)],
            new SheetDocumentCell(At("C2"), Entry.FromValue(Value.FromNumber(7)), null, null, null, null, null));

        var file = await Saved.WriteAsync(workbook);
        var cells = await CellsAsync(file);
        var read = Workbook.Open((await ReadAsync(file)).Document).Sheets[0];

        Assert.Equal(["B2", "C2", "D2", "B3", "C3", "D3"], cells.Select(c => c.Attribute("r")!.Value));
        Assert.Equal(["7"], cells.Select(c => c.Element(S + "v")?.Value).OfType<string>());
        Assert.Single(cells.Select(c => c.Attribute("s")!.Value).Distinct());
        var both = new CellFormat(NumberFormat.General, HorizontalAlignment.General, new CellFont(Bold: true), Yellow, CellBorders.None);
        foreach (var at in new[] { "B2", "C2", "D3" }) Assert.Equal(both, read.GetCellFormat(At(at)));
        Assert.Equal(new CellFont(Bold: true), read.GetCellFormat(At("A2")).Font);
        Assert.Equal(CellFill.None, read.GetCellFormat(At("E3")).Fill);
        Assert.Equal(Yellow, read.GetCellFormat(At("C9")).Fill);
        Assert.Equal(CellFont.Default, read.GetCellFormat(At("C9")).Font);
    }

    [Fact] // ADR-0330 §4, ticket 04: a row that records every part its columns record stands in place of them, and crosses nothing
    public async Task ADR0330_a_row_recording_what_its_columns_record_writes_no_crossing()
    {
        var workbook = Formatted(
            [new SheetDocumentAxisFormat(0, 16_383, null, null, null, Yellow, null)],
            [new SheetDocumentAxisFormat(4, 4, null, null, null, Red, null), new SheetDocumentAxisFormat(6, 6, null, null, null, CellFill.None, null)]);

        var file = await Saved.WriteAsync(workbook);
        var read = Workbook.Open((await ReadAsync(file)).Document).Sheets[0];

        Assert.Empty(await CellsAsync(file));
        Assert.Equal(Red, read.GetCellFormat(At("XFD5")).Fill);
        Assert.Equal(CellFill.None, read.GetCellFormat(At("B7")).Fill);
        Assert.Equal(Yellow, read.GetCellFormat(At("B8")).Fill);
    }

    [Fact] // ADR-0330 §6, ticket 04: crossings are counted from the runs, so a Sheet formatted on every row and every column is refused at once, by name, not written cell by cell
    public async Task ADR0330_crossings_beyond_the_limit_are_refused_before_any_is_written()
    {
        var workbook = Formatted(
            [new SheetDocumentAxisFormat(0, 16_383, null, null, null, Yellow, null)],
            [new SheetDocumentAxisFormat(0, Sheet.RowCount - 1, null, null, new CellFont(Bold: true), null, null)]);

        var refused = await RefusedAsync(workbook);

        Assert.Equal((XlsxSaveRefusal.TooManyCells, "Sheet1"), (refused.Reason, refused.Sheet));
        Assert.Contains((16_384L * Sheet.RowCount).ToString("N0", CultureInfo.InvariantCulture), refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0330 §6, ticket 04: the crossings count toward the limit with the Sheet's own cells, a recorded cell at a crossing once
    public async Task ADR0330_crossings_count_toward_the_limit()
    {
        var workbook = Formatted(
            [new SheetDocumentAxisFormat(1, 3, null, null, null, Yellow, null)],
            [new SheetDocumentAxisFormat(1, 2, null, null, new CellFont(Bold: true), null, null)],
            new SheetDocumentCell(At("C2"), Entry.FromValue(Value.FromNumber(7)), null, null, null, null, null),
            new SheetDocumentCell(At("H9"), Entry.FromValue(Value.FromNumber(8)), null, null, null, null, null));

        var refused = await RefusedAsync(workbook, new XlsxSaveOptions { MaxCells = 6 });
        var file = await Saved.WriteAsync(workbook, new XlsxSaveOptions { MaxCells = 7 });

        Assert.Equal(XlsxSaveRefusal.TooManyCells, refused.Reason);
        Assert.Equal(7, (await CellsAsync(file)).Count);
    }

    [Theory] // ADR-0330 §6: a Formula Excel would not take is refused by name, with its Sheet, cell and text: a text constant over 255 characters, a call of more than 255 arguments, a bound name called
    [InlineData("long text", "255 characters")]
    [InlineData("many arguments", "255 arguments")]
    [InlineData("bound call", "LET binds or a LAMBDA takes")]
    public async Task ADR0330_a_formula_excel_would_not_take_is_refused_by_name(string kind, string said)
    {
        var formula = kind switch
        {
            "long text" => "=LEN(\"" + new string('x', 256) + "\")",
            "many arguments" => "=HSTACK(" + string.Join(",", Enumerable.Repeat("1", 256)) + ")",
            _ => "=LET(f,LAMBDA(x,x+1),f(2))",
        };

        var refused = await RefusedAsync(Entered(("D4", Entry.Parse(formula, EnUs))));

        Assert.Equal((XlsxSaveRefusal.Formula, "Sheet1", "D4", formula), (refused.Reason, refused.Sheet, refused.Cell, refused.Held));
        Assert.Contains(said, refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0330 §6: a Formula holding half of a surrogate pair is refused by name, not left to the file layer's unnamed ArgumentException
    public async Task ADR0330_a_formula_a_file_cannot_carry_is_refused_by_name()
    {
        var refused = await RefusedAsync(Entered(("A1", Entry.Parse("=LEN(\"a\uD800\")", EnUs))));

        Assert.Equal((XlsxSaveRefusal.Formula, "Sheet1", "A1"), (refused.Reason, refused.Sheet, refused.Cell));
    }

    [Theory] // ADR-0330 §6: a Sheet's name a worksheet's name cannot carry in a file is refused by name, naming the Sheet and no cell
    // The character as a number: an attribute's string cannot hold half of a surrogate pair.
    [InlineData(0x0001)]
    [InlineData(0xD800)]
    [InlineData(0xDC00)]
    [InlineData(0xFFFE)]
    [InlineData(0xFFFF)]
    public async Task ADR0330_a_sheet_name_a_file_cannot_carry_is_refused_by_name(int character)
    {
        var name = "Desk" + (char)character;

        var refused = await RefusedAsync(new Workbook(EnUs, ["Trades", name]));

        Assert.Equal((XlsxSaveRefusal.Text, name, null), (refused.Reason, refused.Sheet, refused.Cell));
    }

    [Fact] // ADR-0330 §6: the C1 controls (U+007F to U+009F) are characters XML carries, and a Sheet named with one is written and read back
    public async Task ADR0330_a_sheet_name_with_a_c1_control_is_written()
    {
        var workbook = new Workbook(EnUs, ["Desk\u0085", "Book\u007F"]);
        workbook.Do("Desk\u0085", SheetEdit.Enter(At("A1"), "1"));

        var file = await Saved.WriteAsync(workbook);

        Assert.Equal(["Desk\u0085", "Book\u007F"], (await ReadAsync(file)).Document.Sheets.Select(s => s.Name));
    }

    [Fact] // ADR-0330 §6, §9: a workbook whose parts would hold more uncompressed bytes than a file is read with is refused by name, naming the Sheet whose worksheet went past it
    public async Task ADR0330_more_uncompressed_bytes_than_the_limit_are_refused_at_a_worksheet()
    {
        var workbook = new Workbook(EnUs, ["Trades"]);
        workbook.Do("Trades", SheetEdit.Enter(Enumerable.Range(1, 500).ToDictionary(r => new CellAddress(r, 0), r => r.ToString(CultureInfo.InvariantCulture))));

        var refused = await RefusedAsync(workbook, new XlsxSaveOptions { MaxUncompressedBytes = 4_000 });

        Assert.Equal((XlsxSaveRefusal.TooLarge, "Trades", null), (refused.Reason, refused.Sheet, refused.Cell));
        Assert.Contains("xl/worksheets/sheet1.xml", refused.Message, StringComparison.Ordinal);
        Assert.Equal(XlsxReadOptions.DefaultMaxUncompressedBytes, new XlsxSaveOptions().MaxUncompressedBytes);
    }

    [Fact] // ADR-0330 §6, §9: the parts written last count too: text that fills the shared strings past the limit is refused, and no Sheet is named
    public async Task ADR0330_more_uncompressed_bytes_than_the_limit_are_refused_at_the_shared_strings()
    {
        var workbook = Entered(("A1", Entry.FromValue(Value.FromText(new string('x', 30_000)))));

        var refused = await RefusedAsync(workbook, new XlsxSaveOptions { MaxUncompressedBytes = 20_000 });

        Assert.Equal((XlsxSaveRefusal.TooLarge, null), (refused.Reason, refused.Sheet));
        Assert.Contains("xl/sharedStrings.xml", refused.Message, StringComparison.Ordinal);
    }

    [Theory] // ADR-0330 §9: the limit is 1 byte to the most a file is read with by default, since the file waits in memory until it is handed over
    [InlineData(0L)]
    [InlineData(XlsxReadOptions.DefaultMaxUncompressedBytes + 1)]
    public async Task ADR0330_an_uncompressed_byte_limit_out_of_range_is_refused(long limit)
    {
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await XlsxWorkbookWriter.WriteAsync(new Workbook(EnUs), stream, new XlsxSaveOptions { MaxUncompressedBytes = limit }, Token));
        Assert.Equal(0, stream.Length);
    }
}
