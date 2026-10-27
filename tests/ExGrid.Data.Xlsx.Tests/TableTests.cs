using System.IO.Compression;
using System.Xml.Linq;
using Xunit;
using static ExGrid.Data.Xlsx.Tests.Fixtures;

namespace ExGrid.Data.Xlsx.Tests;

/// <summary>
/// An Excel Table written on a worksheet (ADR-0330 §6, xlsx ticket 05), where the shared cases cannot hold it (they are
/// <c>tests/cases/xlsx/write.json</c>): a table's columns are taken when it is added, so a list the program changes afterwards
/// changes nothing.
/// </summary>
public class TableTests
{
    private static readonly XNamespace S = Main;

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

    [Fact] // ADR-0330 §6: the table's columns are taken when it is added, so a list changed afterwards changes neither its part nor what its header holds
    public async Task ADR0330_a_tables_columns_are_taken_when_it_is_added()
    {
        var columns = new List<string> { "Desk", "PV" };
        var file = await WriteAsync(book =>
        {
            var sheet = book.AddSheet("Sheet1");
            sheet.AddTable(new XlsxTable("T", 0, 0, columns, 1));
            columns[1] = "Changed";
            sheet.Write(Header(0, 0, "Desk"));
            Assert.Throws<ArgumentException>(() => sheet.Write(Header(0, 1, "Changed")));
            sheet.Write(Header(0, 1, "PV"));
            return Task.CompletedTask;
        });

        Assert.Equal(["Desk", "PV"], Parts(file)["xl/tables/table1.xml"].Descendants(S + "tableColumn").Select(c => (string)c.Attribute("name")!));
    }

}
