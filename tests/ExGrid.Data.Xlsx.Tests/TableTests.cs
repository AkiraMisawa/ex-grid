using System.IO.Compression;
using System.Xml.Linq;
using Xunit;
using static ExGrid.Data.Xlsx.Tests.Fixtures;

namespace ExGrid.Data.Xlsx.Tests;

/// <summary>
/// An Excel Table written on a worksheet (ADR-0330 §6, xlsx ticket 05): its table part, the worksheet's relationship to it and
/// its <c>tableParts</c>, its content type, ids and names unique in the workbook, its range, its header written as its
/// columns' names, and an AutoFilter over it. The reader does not read a table part; the worksheet's cells read back, and its
/// <c>tableParts</c> is one of the elements it keeps unread.
/// </summary>
public class TableTests
{
    private static readonly XNamespace S = Main;
    private static readonly XNamespace R = Rel;
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    private static Dictionary<string, XDocument> Parts(byte[] file)
    {
        using var zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read);
        return zip.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var stream = e.Open();
            return XDocument.Load(stream);
        });
    }

    private static XlsxCell Header(int row, int column, string text) => new(row, column, XlsxCellType.SharedString, null, text);

    private static XlsxCell Number(int row, int column, string value) => new(row, column, XlsxCellType.Number, value);

    /// <summary>Two tables on one worksheet, one on another after it, and a worksheet of none between them.</summary>
    private static Task<byte[]> ThreeTables() => WriteAsync(book =>
    {
        var trades = book.AddSheet("Trades");
        trades.AddTable(new XlsxTable("Positions", 0, 0, ["Desk", "PV"], 2));
        trades.AddTable(new XlsxTable("Rates", 0, 3, ["Ccy"], 1) { AutoFilter = false });
        trades.Write(Header(0, 0, "Desk"));
        trades.Write(Header(0, 1, "PV"));
        trades.Write(new XlsxCell(0, 3, XlsxCellType.InlineString, null, "Ccy"));
        trades.Write(new XlsxCell(1, 0, XlsxCellType.SharedString, null, "EMEA"));
        trades.Write(Number(1, 1, "1.5"));
        trades.Write(new XlsxCell(1, 3, XlsxCellType.SharedString, null, "EUR"));
        trades.Write(Number(2, 1, "-2"));
        book.AddSheet("Plain").Write(Number(0, 0, "1"));
        var third = book.AddSheet("Third");
        third.AddTable(new XlsxTable("Line_feed.table", 4, 2, ["Two\nlines", "x_x0041_"], 1));
        third.Write(Header(4, 2, "Two\nlines"));
        third.Write(Header(4, 3, "x_x0041_"));
        return Task.CompletedTask;
    });

    [Fact] // ADR-0330 §6: each table is a part of its own, related from its worksheet, typed in the content types, and named in the worksheet's tableParts
    public async Task ADR0330_a_table_is_a_part_related_from_its_worksheet()
    {
        var parts = Parts(await ThreeTables());

        Assert.Contains("xl/tables/table1.xml", parts.Keys);
        Assert.Contains("xl/tables/table2.xml", parts.Keys);
        Assert.Contains("xl/tables/table3.xml", parts.Keys);
        Assert.DoesNotContain("xl/worksheets/_rels/sheet2.xml.rels", parts.Keys);

        var overrides = parts["[Content_Types].xml"].Root!.Elements(ContentTypes + "Override")
            .ToDictionary(o => (string)o.Attribute("PartName")!, o => (string)o.Attribute("ContentType")!);
        foreach (var n in new[] { 1, 2, 3 })
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml", overrides[$"/xl/tables/table{n}.xml"]);

        // Each worksheet's tablePart names a relationship of its own part, which targets its table.
        foreach (var (sheet, tables) in new[] { (1, new[] { 1, 2 }), (3, new[] { 3 }) })
        {
            var relationships = parts[$"xl/worksheets/_rels/sheet{sheet}.xml.rels"].Root!.Elements(PackageRelationships + "Relationship")
                .ToDictionary(r => (string)r.Attribute("Id")!);
            var tableParts = parts[$"xl/worksheets/sheet{sheet}.xml"].Root!.Element(S + "tableParts")!;
            Assert.Equal(tables.Length.ToString(), (string?)tableParts.Attribute("count"));
            var targets = tableParts.Elements(S + "tablePart").Select(p => relationships[(string)p.Attribute(R + "id")!]).ToList();
            Assert.All(targets, r => Assert.Equal("http://schemas.openxmlformats.org/officeDocument/2006/relationships/table", (string?)r.Attribute("Type")));
            Assert.Equal(tables.Select(t => $"../tables/table{t}.xml"), targets.Select(r => (string)r.Attribute("Target")!));
        }
        // tableParts comes after sheetData, last, as CT_Worksheet orders it.
        Assert.Equal(["sheetViews", "sheetFormatPr", "sheetData", "tableParts"], parts["xl/worksheets/sheet1.xml"].Root!.Elements().Select(e => e.Name.LocalName));
        Assert.Null(parts["xl/worksheets/sheet2.xml"].Root!.Element(S + "tableParts"));
    }

    [Fact] // ADR-0330 §6: a table part holds its id and name, unique in the workbook, its range, its AutoFilter, its columns and Excel's default style
    public async Task ADR0330_a_table_part_holds_its_range_and_columns()
    {
        var parts = Parts(await ThreeTables());
        var tables = Enumerable.Range(1, 3).Select(n => parts[$"xl/tables/table{n}.xml"].Root!).ToList();

        Assert.Equal(["1", "2", "3"], tables.Select(t => (string)t.Attribute("id")!));
        Assert.Equal(["Positions", "Rates", "Line_feed.table"], tables.Select(t => (string)t.Attribute("name")!));
        Assert.Equal(["Positions", "Rates", "Line_feed.table"], tables.Select(t => (string)t.Attribute("displayName")!));
        Assert.Equal(["A1:B3", "D1:D2", "C5:D6"], tables.Select(t => (string)t.Attribute("ref")!));

        var positions = tables[0];
        Assert.Equal(S + "table", positions.Name);
        Assert.Equal(["autoFilter", "tableColumns", "tableStyleInfo"], positions.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("A1:B3", (string?)positions.Element(S + "autoFilter")!.Attribute("ref"));
        var columns = positions.Element(S + "tableColumns")!;
        Assert.Equal("2", (string?)columns.Attribute("count"));
        Assert.Equal([("1", "Desk"), ("2", "PV")], columns.Elements(S + "tableColumn").Select(c => ((string)c.Attribute("id")!, (string)c.Attribute("name")!)));
        Assert.Equal("TableStyleMedium2", (string?)positions.Element(S + "tableStyleInfo")!.Attribute("name"));

        Assert.Null(tables[1].Element(S + "autoFilter"));
        // A literal _x…_ in a column's name is escaped as ST_Xstring escapes it; a line feed is carried by XML as it is.
        Assert.Equal(["Two\nlines", "x_x005F_x0041_"], tables[2].Descendants(S + "tableColumn").Select(c => (string)c.Attribute("name")!));
    }

    [Fact] // ADR-0330 §1, §6: the worksheet's cells read back, the header's as its columns' names, and tableParts is kept unread
    public async Task ADR0330_a_tables_cells_read_back()
    {
        using var reader = await OpenAsync(await ThreeTables());

        var trades = await reader.ReadSheetAsync(0);
        Assert.Equal(["Desk", "PV", "Ccy", "EMEA", null, "EUR", null], trades.Cells.Select(c => c.Text));
        Assert.Equal(["tableParts"], trades.OtherElements.Where(e => e.Name == "tableParts").Select(e => e.Name));
        var third = await reader.ReadSheetAsync(2);
        Assert.Equal(["Two\nlines", "x_x0041_"], third.Cells.Select(c => c.Text));
    }

    [Theory] // ADR-0330 §6: a name Excel does not give a Table is refused
    [InlineData("")]
    [InlineData("1Table")]
    [InlineData("Has space")]
    [InlineData("A1")]
    [InlineData("XFD1048576")]
    [InlineData("R")]
    [InlineData("r1c1")]
    [InlineData("TRUE")]
    public async Task ADR0330_a_name_excel_does_not_give_a_table_is_refused(string name)
    {
        await using var writer = XlsxWriter.Create(new MemoryStream());
        var sheet = writer.AddSheet("Sheet1");

        Assert.Throws<ArgumentException>(() => sheet.AddTable(new XlsxTable(name, 0, 0, ["a"], 1)));
    }

    [Fact] // ADR-0330 §6: a table's name is unique among the workbook's tables and defined names, in any case
    public async Task ADR0330_a_tables_name_is_unique_in_the_workbook()
    {
        await using var writer = XlsxWriter.Create(new MemoryStream(), new XlsxWriteOptions { DefinedNames = [new XlsxDefinedName("Rate", "Sheet1!$A$1")] });
        var first = writer.AddSheet("Sheet1");
        first.AddTable(new XlsxTable("Positions", 0, 0, ["a"], 1));
        first.Write(Header(0, 0, "a"));
        var second = writer.AddSheet("Sheet2");

        Assert.Throws<ArgumentException>(() => second.AddTable(new XlsxTable("POSITIONS", 0, 0, ["a"], 1)));
        Assert.Throws<ArgumentException>(() => second.AddTable(new XlsxTable("rate", 0, 0, ["a"], 1)));
    }

    [Fact] // ADR-0330 §6: columns, rows and ranges a table cannot have are refused, and two tables of a sheet do not overlap
    public async Task ADR0330_a_table_excel_cannot_hold_is_refused()
    {
        await using var writer = XlsxWriter.Create(new MemoryStream());
        var sheet = writer.AddSheet("Sheet1");

        Assert.Throws<ArgumentException>(() => sheet.AddTable(new XlsxTable("T", 0, 0, [], 1)));
        Assert.Throws<ArgumentException>(() => sheet.AddTable(new XlsxTable("T", 0, 0, ["a", ""], 1)));
        Assert.Throws<ArgumentException>(() => sheet.AddTable(new XlsxTable("T", 0, 0, ["a", "A"], 1)));
        Assert.Throws<ArgumentException>(() => sheet.AddTable(new XlsxTable("T", 0, 0, ["\uD800"], 1)));
        Assert.Throws<ArgumentException>(() => sheet.AddTable(new XlsxTable("T", 0, 0, ["a"], 0)));
        Assert.Throws<ArgumentException>(() => sheet.AddTable(new XlsxTable("T", 1, 0, ["a"], XlsxCellReference.MaxRows - 1)));
        Assert.Throws<ArgumentException>(() => sheet.AddTable(new XlsxTable("T", 0, XlsxCellReference.MaxColumns - 1, ["a", "b"], 1)));
        sheet.AddTable(new XlsxTable("T", 0, XlsxCellReference.MaxColumns - 1, ["a"], XlsxCellReference.MaxRows - 1));
        sheet.AddTable(new XlsxTable("Left", 2, 0, ["a", "b"], 2));
        Assert.Throws<ArgumentException>(() => sheet.AddTable(new XlsxTable("Overlapping", 4, 1, ["c"], 1)));
        sheet.AddTable(new XlsxTable("Below", 5, 1, ["c"], 1));
    }

    [Fact] // ADR-0330 §6: a header's cell holds its column's name as text, and a cell that does not is refused, leaving the sheet as it was
    public async Task ADR0330_a_header_cell_holds_its_columns_name()
    {
        await using var writer = XlsxWriter.Create(new MemoryStream());
        var sheet = writer.AddSheet("Sheet1");
        sheet.AddTable(new XlsxTable("T", 1, 1, ["Desk", "PV"], 1));
        sheet.Write(Number(0, 1, "1"));

        Assert.Throws<ArgumentException>(() => sheet.Write(Header(1, 1, "desk")));
        Assert.Throws<ArgumentException>(() => sheet.Write(Number(1, 1, "1")));
        Assert.Throws<ArgumentException>(() => sheet.Write(Header(1, 1, "Desk") with { Formula = new XlsxFormula("\"Desk\"") }));
        sheet.Write(Header(1, 0, "Not the table's"));
        sheet.Write(Header(1, 1, "Desk"));
        sheet.Write(Header(1, 2, "PV"));
    }

    [Fact] // ADR-0330 §6: a table is added before its header's row is begun, and a sheet that ends with its header unwritten is refused
    public async Task ADR0330_a_tables_header_is_written_whole()
    {
        await using (var writer = XlsxWriter.Create(new MemoryStream()))
        {
            var sheet = writer.AddSheet("Sheet1");
            sheet.Write(Number(2, 0, "1"));
            Assert.Throws<ArgumentException>(() => sheet.AddTable(new XlsxTable("T", 2, 1, ["a"], 1)));
            Assert.Throws<ArgumentException>(() => sheet.AddTable(new XlsxTable("T", 1, 1, ["a"], 1)));
        }

        await using (var writer = XlsxWriter.Create(new MemoryStream()))
        {
            var sheet = writer.AddSheet("Sheet1");
            sheet.AddTable(new XlsxTable("T", 0, 0, ["a", "b"], 1));
            sheet.Write(Header(0, 0, "a"));
            Assert.Throws<InvalidOperationException>(() => writer.AddSheet("Sheet2"));
        }

        await using (var writer = XlsxWriter.Create(new MemoryStream()))
        {
            writer.AddSheet("Sheet1").AddTable(new XlsxTable("T", 0, 0, ["a"], 1));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await writer.CompleteAsync(Token));
        }
    }
}
