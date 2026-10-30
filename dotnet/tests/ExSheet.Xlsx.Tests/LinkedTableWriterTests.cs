using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// What a Linked Table's write holds that a case cannot (ADR-0330 §6, Q5): the snapshot taken when the call is made, and a table
/// of as many rows as a worksheet holds. What each Linked Table writes, and what is refused, is held by the shared cases
/// (<c>tests/cases/sheet-xlsx/linked-tables.json</c>).
/// </summary>
public class LinkedTableWriterTests
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

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
}
