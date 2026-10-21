using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// Each Linked Table written as an Excel Table on a worksheet of its name (ADR-0330 §6, Q5; xlsx ticket 05): its rows as they
/// stand, its Formulas written so that they read it in Excel, what is refused by name, and the file opened again by ExSheet,
/// which reads the worksheet's cells and refuses the Formulas that read the Excel Table (X1).
/// </summary>
public class LinkedTableWriterTests
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    private static CellAddress At(string at) => CellAddress.Parse(at);

    private static Dictionary<string, XDocument> Parts(byte[] file)
    {
        using var zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read);
        return zip.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var stream = e.Open();
            return XDocument.Load(stream);
        });
    }

    private static string[] SharedStrings(Dictionary<string, XDocument> parts) =>
        [.. parts["xl/sharedStrings.xml"].Root!.Elements(S + "si").Select(si => si.Element(S + "t")!.Value)];

    /// <summary>A worksheet's cells by reference: each one's type and what it shows, a shared string read through the shared strings.</summary>
    private static Dictionary<string, (string? Type, string? Value)> Cells(Dictionary<string, XDocument> parts, int sheet)
    {
        var strings = SharedStrings(parts);
        return parts[$"xl/worksheets/sheet{sheet}.xml"].Descendants(S + "c").ToDictionary(
            c => (string)c.Attribute("r")!,
            c =>
            {
                var type = (string?)c.Attribute("t");
                var value = (string?)c.Element(S + "v");
                return (type, type == "s" ? strings[int.Parse(value!, CultureInfo.InvariantCulture)] : value);
            });
    }

    private static Value? N(double number) => Value.FromNumber(number);

    private static Value? T(string text) => Value.FromText(text);

    /// <summary>A Workbook with two Linked Tables, both answered, and Formulas on two Sheets that read them.</summary>
    private static Workbook Booked()
    {
        var workbook = new Workbook(EnUs, ["Trades", "Summary"]);
        workbook.DeclareLinkedTable("Positions", ["Desk", "PV", "Live", "Total#"], key: "Desk");
        workbook.DeclareLinkedTable("Rates", ["Ccy", "Rate"]);
        workbook.PushLinkedTable("Positions",
        [
            [T("EMEA"), N(1_000_000.25), Value.FromBoolean(true), N(1)],
            [T("APAC"), N(-250), null, Value.FromError(ErrorValue.NA)],
            [T("0042"), null, Value.FromBoolean(false), N(2)],
        ]);
        workbook.PushLinkedTable("Rates", [[T("EUR"), N(1.08)]]);
        workbook.Do("Trades", SheetEdit.Enter(new Dictionary<CellAddress, string>
        {
            [At("A1")] = "=SUM(Positions[PV])",
            [At("A2")] = "=XLOOKUP(\"APAC\",positions[desk],Positions[[PV]])",
            [At("A3")] = "=SUM(Positions[[Total'#]])",
            [At("A4")] = "=Rates[Rate]*2",
        }));
        workbook.Do("Summary", SheetEdit.Enter(At("B2"), "=COUNTA(Positions[Desk])&\" desks\""));
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

    [Fact] // ADR-0330 §6 (Q5): each Linked Table is written after the Sheets as a worksheet of its name, in the order they were declared, the Sheet shown still shown
    public async Task ADR0330_each_linked_table_is_a_worksheet_of_its_name_after_the_sheets()
    {
        var workbook = Booked();
        workbook.Shown = workbook.Sheets[1];
        var parts = Parts(await Saved.WriteAsync(workbook));

        var sheets = parts["xl/workbook.xml"].Root!.Element(S + "sheets")!.Elements(S + "sheet").ToList();
        Assert.Equal(["Trades", "Summary", "Positions", "Rates"], sheets.Select(s => (string)s.Attribute("name")!));
        Assert.Equal(4, sheets.Select(s => (string)s.Attribute("sheetId")!).Distinct().Count());
        Assert.Equal("1", (string?)parts["xl/workbook.xml"].Root!.Element(S + "bookViews")!.Element(S + "workbookView")!.Attribute("activeTab"));
    }

    [Fact] // ADR-0330 §6 (Q5): the worksheet holds an Excel Table of the Linked Table's name over its header and rows, related and typed as Excel requires
    public async Task ADR0330_a_linked_tables_worksheet_holds_an_excel_table_of_its_name()
    {
        var parts = Parts(await Saved.WriteAsync(Booked()));

        var overrides = parts["[Content_Types].xml"].Root!.Elements(ContentTypes + "Override")
            .ToDictionary(o => (string)o.Attribute("PartName")!, o => (string)o.Attribute("ContentType")!);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml", overrides["/xl/tables/table1.xml"]);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml", overrides["/xl/tables/table2.xml"]);
        Assert.DoesNotContain(parts.Keys, p => p.StartsWith("xl/worksheets/_rels/sheet1", StringComparison.Ordinal) || p.StartsWith("xl/worksheets/_rels/sheet2", StringComparison.Ordinal));

        foreach (var (sheet, table, name, reference, columns) in new[]
        {
            (3, 1, "Positions", "A1:D4", new[] { "Desk", "PV", "Live", "Total#" }),
            (4, 2, "Rates", "A1:B2", new[] { "Ccy", "Rate" }),
        })
        {
            var worksheet = parts[$"xl/worksheets/sheet{sheet}.xml"].Root!;
            var tablePart = worksheet.Element(S + "tableParts")!.Elements(S + "tablePart").Single();
            var relationship = parts[$"xl/worksheets/_rels/sheet{sheet}.xml.rels"].Root!.Elements(PackageRelationships + "Relationship")
                .Single(r => (string?)r.Attribute("Id") == (string?)tablePart.Attribute(R + "id"));
            Assert.Equal("http://schemas.openxmlformats.org/officeDocument/2006/relationships/table", (string?)relationship.Attribute("Type"));
            Assert.Equal($"../tables/table{table}.xml", (string?)relationship.Attribute("Target"));

            var part = parts[$"xl/tables/table{table}.xml"].Root!;
            Assert.Equal((table.ToString(CultureInfo.InvariantCulture), name, name, reference),
                ((string)part.Attribute("id")!, (string)part.Attribute("name")!, (string)part.Attribute("displayName")!, (string)part.Attribute("ref")!));
            Assert.Equal(reference, (string?)part.Element(S + "autoFilter")!.Attribute("ref"));
            Assert.Equal(columns, part.Descendants(S + "tableColumn").Select(c => (string)c.Attribute("name")!));
            Assert.Equal(columns.Select((_, i) => (i + 1).ToString(CultureInfo.InvariantCulture)), part.Descendants(S + "tableColumn").Select(c => (string)c.Attribute("id")!));

            // The header's cells hold the columns' names, as Excel requires of a table's header.
            var cells = Cells(parts, sheet);
            for (var c = 0; c < columns.Length; c++)
                Assert.Equal(("s", columns[c]), cells[XlsxCellReference.Format(0, c)]);
        }
    }

    [Fact] // ADR-0330 §6 (Q5): the rows are written as they stand: numbers, text (a text of digits stays text), booleans, Error Values, and no cell for a blank
    public async Task ADR0330_a_linked_tables_rows_are_written_as_they_stand()
    {
        var cells = Cells(Parts(await Saved.WriteAsync(Booked())), 3);

        Assert.Equal(("s", "EMEA"), cells["A2"]);
        Assert.Equal((null, "1000000.25"), cells["B2"]);
        Assert.Equal(("b", "1"), cells["C2"]);
        Assert.Equal((null, "1"), cells["D2"]);
        Assert.Equal(("s", "APAC"), cells["A3"]);
        Assert.Equal((null, "-250"), cells["B3"]);
        Assert.False(cells.ContainsKey("C3"));
        Assert.Equal(("e", "#N/A"), cells["D3"]);
        Assert.Equal(("s", "0042"), cells["A4"]);
        Assert.False(cells.ContainsKey("B4"));
        Assert.Equal(("b", "0"), cells["C4"]);
        Assert.Equal(14, cells.Count);
    }

    [Fact] // ADR-0330 §6 (Q5): the rows written are the snapshot as it stands when the call is made, not one pushed while the file is handed over
    public async Task ADR0330_the_rows_written_are_those_when_the_write_is_called()
    {
        var workbook = Booked();
        using var stream = new MemoryStream();

        var writing = XlsxWorkbookWriter.WriteAsync(workbook, stream, null, Token);
        workbook.PushLinkedTable("Rates", [[T("GBP"), N(1.27)], [T("JPY"), N(0.0067)]]);
        await writing;

        var parts = Parts(stream.ToArray());
        Assert.Equal("A1:B2", (string?)parts["xl/tables/table2.xml"].Root!.Attribute("ref"));
        Assert.Equal(("s", "EUR"), Cells(parts, 4)["A2"]);
        Assert.Equal("2.16", (string?)Cells(parts, 1)["A4"].Value);
    }

    [Fact] // ADR-0330 §6 (Q5): a Formula that reads a Linked Table is written as Excel stores a structured reference, with the Value it shows
    public async Task ADR0330_a_formula_that_reads_a_linked_table_is_written_as_excel_stores_it()
    {
        var parts = Parts(await Saved.WriteAsync(Booked()));
        var trades = parts["xl/worksheets/sheet1.xml"];
        string Formula(XDocument sheet, string at) => sheet.Descendants(S + "c").Single(c => (string?)c.Attribute("r") == at).Element(S + "f")!.Value;
        string? Shown(XDocument sheet, string at) => (string?)sheet.Descendants(S + "c").Single(c => (string?)c.Attribute("r") == at).Element(S + "v");

        Assert.Equal("SUM(Positions[PV])", Formula(trades, "A1"));
        Assert.Equal("999750.25", Shown(trades, "A1"));
        Assert.Equal("_xlfn.XLOOKUP(\"APAC\",positions[desk],Positions[PV])", Formula(trades, "A2"));
        Assert.Equal("-250", Shown(trades, "A2"));
        Assert.Equal("SUM(Positions[Total'#])", Formula(trades, "A3"));
        Assert.Equal("Rates[Rate]*2", Formula(trades, "A4"));
        Assert.Equal("COUNTA(Positions[Desk])&\" desks\"", Formula(parts["xl/worksheets/sheet2.xml"], "B2"));
        Assert.Equal("3 desks", Shown(parts["xl/worksheets/sheet2.xml"], "B2"));
    }

    [Fact] // ADR-0330 §6 (X1): opened again by ExSheet, a Formula that reads the Excel Table is refused by name, until ExSheet reads Excel Tables
    public async Task ADR0330_a_formula_reading_the_written_table_is_refused_when_opened_again()
    {
        var file = await Saved.WriteAsync(Booked());

        var refused = await Book.RefusedAsync(file);

        Assert.Equal(XlsxOpenRefusal.ExcelTable, refused.Reason);
        Assert.Equal(("Trades", "A1", "=SUM(Positions[PV])"), (refused.Sheet, refused.Cell, refused.Held));
    }

    [Fact] // ADR-0330 §6 (X1): with no Formula reading it, the table's worksheet opens again as a Sheet of its cells, not as a Linked Table, and the report says so
    public async Task ADR0330_the_tables_worksheet_opens_again_as_a_sheet_of_its_cells()
    {
        var workbook = new Workbook(EnUs);
        workbook.DeclareLinkedTable("Positions", ["Desk", "PV"]);
        workbook.PushLinkedTable("Positions", [[T("EMEA"), N(12.5)], [T("APAC"), null]]);
        workbook.Do("Sheet1", SheetEdit.Enter(At("A1"), "=1+1"));

        var (document, report) = await ReadAsync(await Saved.WriteAsync(workbook));

        Assert.Empty(document.LinkedTables);
        Assert.Equal(["Sheet1", "Positions"], document.Sheets.Select(s => s.Name));
        var positions = document.Sheet("Positions");
        Assert.Equal(Value.FromText("Desk"), positions.EntryAt("A1")!.Constant);
        Assert.Equal(Value.FromText("PV"), positions.EntryAt("B1")!.Constant);
        Assert.Equal(Value.FromText("APAC"), positions.EntryAt("A3")!.Constant);
        Assert.Equal(Value.FromNumber(12.5), positions.EntryAt("B2")!.Constant);
        Assert.Null(positions.EntryAt("B3"));
        Assert.Contains(report.NotCarried, n => n.Kind == NotCarriedKind.ExcelTable && n.Sheet == "Positions");
    }

    [Fact] // ADR-0330 §6: the write is refused by name while a Linked Table has not answered
    public async Task ADR0330_a_linked_table_that_has_not_answered_is_refused()
    {
        var workbook = new Workbook(EnUs);
        workbook.DeclareLinkedTable("Positions", ["Desk", "PV"]);
        workbook.Do("Sheet1", SheetEdit.Enter(At("A1"), "=SUM(Positions[PV])"));

        var refused = await RefusedAsync(workbook);

        Assert.Equal(XlsxSaveRefusal.LinkedTable, refused.Reason);
        Assert.Contains("'Positions' has not answered", refused.Message, StringComparison.Ordinal);
        Assert.Equal("Positions", refused.Sheet);
    }

    [Fact] // ADR-0330 §6, ADR-0049: a Linked Table whose last snapshot was refused for a repeated key waits again, and is refused as one that has not answered
    public async Task ADR0330_a_linked_table_waiting_again_after_a_repeated_key_is_refused()
    {
        var workbook = new Workbook(EnUs);
        workbook.DeclareLinkedTable("Positions", ["Desk", "PV"], key: "Desk");
        workbook.PushLinkedTable("Positions", [[T("EMEA"), N(1)]]);
        Assert.Throws<RepeatedKeyException>(() => workbook.PushLinkedTable("Positions", [[T("EMEA"), N(1)], [T("emea"), N(2)]]));

        var refused = await RefusedAsync(workbook);

        Assert.Equal(XlsxSaveRefusal.LinkedTable, refused.Reason);
        Assert.Contains("has not answered", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0330 §6: a Linked Table whose name is already a Sheet's, in any case, is refused by name
    public async Task ADR0330_a_linked_table_named_as_a_sheet_is_refused()
    {
        var workbook = new Workbook(EnUs, ["Trades", "positions"]);
        workbook.DeclareLinkedTable("Positions", ["Desk"]);
        workbook.PushLinkedTable("Positions", [[T("EMEA")]]);

        var refused = await RefusedAsync(workbook);

        Assert.Equal(XlsxSaveRefusal.LinkedTable, refused.Reason);
        Assert.Contains("the Sheet 'positions' already has it", refused.Message, StringComparison.Ordinal);
    }

    [Theory] // ADR-0330 §6: a Linked Table whose name a worksheet cannot have is refused by name: the worksheet is named after it
    [InlineData("A_name_of_thirty_two_characters_")]
    [InlineData("\\Positions")]
    [InlineData("History")]
    public async Task ADR0330_a_linked_table_whose_name_a_worksheet_cannot_have_is_refused(string name)
    {
        var workbook = new Workbook(EnUs);
        workbook.DeclareLinkedTable(name, ["Desk"]);
        workbook.PushLinkedTable(name, [[T("EMEA")]]);

        var refused = await RefusedAsync(workbook);

        Assert.Equal(XlsxSaveRefusal.LinkedTable, refused.Reason);
        Assert.Contains("a worksheet cannot have it", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0330 §6: a Linked Table's name the engine takes and Excel gives no Table (A01 reads as the cell A1) is refused by name, not thrown as an argument
    public async Task ADR0330_a_linked_table_whose_name_excel_gives_no_table_is_refused()
    {
        var workbook = new Workbook(EnUs);
        workbook.DeclareLinkedTable("A01", ["Desk"]);
        workbook.PushLinkedTable("A01", [[T("EMEA")]]);

        var refused = await RefusedAsync(workbook);

        Assert.Equal((XlsxSaveRefusal.LinkedTable, "A01"), (refused.Reason, refused.Sheet));
        Assert.Contains("Excel gives a Table no such name", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0330 §6 (spine rule 1): a Linked Table's name read as a name of its own is #NAME? in ExSheet, and Excel would read the table written: refused by name
    public async Task ADR0330_a_linked_tables_bare_name_is_refused()
    {
        var workbook = new Workbook(EnUs);
        workbook.DeclareLinkedTable("Positions", ["PV"]);
        workbook.PushLinkedTable("Positions", [[N(1)]]);
        workbook.Do("Sheet1", SheetEdit.Enter(At("B2"), "=SUM(Positions)"));

        var refused = await RefusedAsync(workbook);

        Assert.Equal((XlsxSaveRefusal.Formula, "Sheet1", "B2", "=SUM(Positions)"), (refused.Reason, refused.Sheet, refused.Cell, refused.Held));
        Assert.Contains("name of its own", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0330 §6 (spine rule 1): a Linked Table of no rows is refused: an Excel Table holds one empty row, and its Formulas would answer otherwise
    public async Task ADR0330_a_linked_table_of_no_rows_is_refused()
    {
        var workbook = new Workbook(EnUs);
        workbook.DeclareLinkedTable("Positions", ["Desk", "PV"]);
        workbook.PushLinkedTable("Positions", []);
        workbook.Do("Sheet1", SheetEdit.Enter(At("A1"), "=ROWS(Positions[PV])"));
        Assert.Equal(Value.FromNumber(0), workbook.Sheets[0].GetValue(At("A1")));

        var refused = await RefusedAsync(workbook);

        Assert.Equal(XlsxSaveRefusal.LinkedTable, refused.Reason);
        Assert.Contains("'Positions' holds no rows", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0330 §6: a Linked Table of more rows than a worksheet holds under its header is refused by name; one of exactly as many is written
    public async Task ADR0330_a_linked_table_of_more_rows_than_excel_holds_is_refused()
    {
        static Workbook Of(int rows)
        {
            var workbook = new Workbook(EnUs);
            workbook.DeclareLinkedTable("Ids", ["Id"]);
            var one = N(1);
            workbook.PushLinkedTable("Ids", Enumerable.Range(0, rows).Select(_ => (IReadOnlyList<Value?>)[one]));
            return workbook;
        }

        var refused = await RefusedAsync(Of(XlsxWorkbookWriter.MaxLinkedTableRows + 1), new XlsxSaveOptions { MaxCells = long.MaxValue });
        Assert.Equal(XlsxSaveRefusal.LinkedTable, refused.Reason);
        Assert.Contains("1,048,576", refused.Message, StringComparison.Ordinal);

        var file = await Saved.WriteAsync(Of(XlsxWorkbookWriter.MaxLinkedTableRows), new XlsxSaveOptions { MaxCells = long.MaxValue });
        using var zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read);
        using var table = zip.GetEntry("xl/tables/table1.xml")!.Open();
        Assert.Equal("A1:A1048576", (string?)XDocument.Load(table).Root!.Attribute("ref"));
    }

    [Fact] // ADR-0330 §6, §9: a Linked Table's cells, its header's included, count toward MaxCells
    public async Task ADR0330_a_linked_tables_cells_count_toward_the_cell_limit()
    {
        var workbook = Booked();
        // 5 Formula cells on the Sheets; 4 + 10 for Positions, a blank in each of two rows; 2 + 2 for Rates.
        await Saved.WriteAsync(workbook, new XlsxSaveOptions { MaxCells = 23 });

        var refused = await RefusedAsync(workbook, new XlsxSaveOptions { MaxCells = 22 });

        Assert.Equal((XlsxSaveRefusal.TooManyCells, "Rates"), (refused.Reason, refused.Sheet));
    }

    [Fact] // ADR-0330 §6: text in a Linked Table a cell of Excel cannot hold is refused by name, naming the table, the column and the row
    public async Task ADR0330_text_a_cell_cannot_hold_in_a_linked_table_is_refused()
    {
        var workbook = new Workbook(EnUs);
        workbook.DeclareLinkedTable("Notes", ["Id", "Note"]);
        workbook.PushLinkedTable("Notes", [[N(1), T("short")], [N(2), T(new string('x', XlsxWorkbookWriter.MaxTextLength + 1))]]);

        var refused = await RefusedAsync(workbook);

        Assert.Equal(XlsxSaveRefusal.Text, refused.Reason);
        Assert.Contains("'Notes'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("column 'Note', row 2", refused.Message, StringComparison.Ordinal);
        Assert.Equal(("Notes", "B3"), (refused.Sheet, refused.Cell));
    }

    [Theory] // ADR-0330 §6: a structured reference to a table or column no Linked Table declares has no Excel Table to read, and is refused by name
    [InlineData("=SUM(Positions[Delta])")]
    [InlineData("=SUM(Trades[PV])")]
    public async Task ADR0330_a_structured_reference_to_no_declared_table_is_refused(string formula)
    {
        var workbook = new Workbook(EnUs);
        workbook.DeclareLinkedTable("Positions", ["Desk", "PV"]);
        workbook.PushLinkedTable("Positions", [[T("EMEA"), N(1)]]);
        workbook.Do("Sheet1", SheetEdit.Enter(At("C3"), formula));

        var refused = await RefusedAsync(workbook);

        Assert.Equal((XlsxSaveRefusal.Formula, "Sheet1", "C3", formula), (refused.Reason, refused.Sheet, refused.Cell, refused.Held));
        Assert.Contains("does not declare", refused.Message, StringComparison.Ordinal);
    }

    [Theory] // ADR-0330 §6: a structured reference to a column whose name has an outer space, which Excel does not keep, or a character whose form Excel has not been seen to write, is refused
    [InlineData("PV ", "=SUM(Positions[PV ])")]
    [InlineData("Total $", "=SUM(Positions[Total $])")]
    [InlineData("P&L", "=SUM(Positions[P&L])")]
    [InlineData("1/2", "=SUM(Positions[1/2])")]
    public async Task ADR0330_a_structured_reference_to_a_column_excel_may_read_otherwise_is_refused(string column, string formula)
    {
        var workbook = new Workbook(EnUs);
        workbook.DeclareLinkedTable("Positions", ["Desk", column]);
        workbook.PushLinkedTable("Positions", [[T("EMEA"), N(1)]]);
        workbook.Do("Sheet1", SheetEdit.Enter(At("A1"), formula));
        Assert.Equal(Value.FromNumber(1), workbook.Sheets[0].GetValue(At("A1")));

        var refused = await RefusedAsync(workbook);

        Assert.Equal((XlsxSaveRefusal.Formula, "A1"), (refused.Reason, refused.Cell));
        Assert.Contains("has not been seen to write", refused.Message, StringComparison.Ordinal);
    }
}
