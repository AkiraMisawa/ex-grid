using System.Globalization;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class SheetDocumentTests
{
    [Fact] // ADR-0048 (SH-12), ADR-0046: the document carries its version, its culture and the Sheet's name
    public void The_document_carries_version_culture_and_name()
    {
        var json = NewSheet().ToDocument().ToJson();

        Assert.Equal("""{"version":7,"culture":"en-US","name":"Sheet1","cells":[]}""", json);
    }

    [Theory] // ADR-0048 (SH-12): a document of an unknown version is refused, not guessed at
    [InlineData("""{"version":8,"culture":"en-US","name":"Sheet1","cells":[]}""", 8)]
    [InlineData("""{"version":0,"culture":"en-US","cells":[]}""", 0)]
    [InlineData("""{"version":8,"culture":"en-US","cells":[{"at":"A1","number":1}],"sheets":[]}""", 8)]
    public void An_unknown_version_is_refused(string json, int version)
    {
        var refusal = Assert.Throws<SheetDocumentException>(() => SheetDocument.FromJson(json));

        Assert.Equal(version, refusal.DocumentVersion);
    }

    [Theory] // ADR-0048 (SH-12): a document that does not say its version, or holds anything version 1 does not define, is refused
    [InlineData("""{"culture":"en-US","cells":[]}""")]
    [InlineData("""{"version":"1","culture":"en-US","cells":[]}""")]
    [InlineData("""{"version":1.5,"culture":"en-US","cells":[]}""")]
    [InlineData("""{"version":1,"cells":[]}""")]
    [InlineData("""{"version":1,"culture":"en-US","cells":[],"values":[]}""")]
    [InlineData("""{"version":1,"culture":"en-US","cells":[{"at":"A1","number":1,"value":1}]}""")]
    [InlineData("""{"version":1,"culture":"en-US","cells":[{"at":"A1","number":1,"text":"x"}]}""")]
    [InlineData("""{"version":1,"culture":"en-US","cells":[{"at":"A1"}]}""")]
    [InlineData("""{"version":1,"culture":"en-US","cells":[{"number":1}]}""")]
    [InlineData("""{"version":1,"culture":"en-US","cells":[{"at":"XFE1","number":1}]}""")]
    [InlineData("""{"version":1,"culture":"en-US","cells":[{"at":"A1","number":1},{"at":"A1","number":2}]}""")]
    [InlineData("""{"version":1,"culture":"en-US","cells":[{"at":"A1","formula":"=1+"}]}""")]
    [InlineData("""{"version":1,"culture":"en-US","cells":[{"at":"A1","error":"#CIRC!"}]}""")]
    [InlineData("""{"version":1,"culture":"en-US","cells":[{"at":"A1","number":1,"format":"[<10]0"}]}""")]
    [InlineData("""{"version":1,"culture":"en-US","cells":[{"at":"A1","number":1,"align":"justify"}]}""")]
    [InlineData("""{"version":1,"culture":"xx-NOPE","cells":[]}""")]
    [InlineData("""{"version":1,"culture":"en-US","name":"Sheet1","cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":1,"cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":"","cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":"a/b","cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":"Sheet1","cells":[],"values":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":"Sheet1","columns":[{"at":"A:A","format":"0"}],"cells":[]}""")]
    [InlineData("""{"version":2,"culture":"en-US","name":"Sheet1","cells":[{"at":"A1","number":1,"align":"general"}]}""")]
    [InlineData("""{"version":3,"culture":"en-US","name":"Sheet1","columns":[{"at":"A:A"}],"cells":[]}""")]
    [InlineData("""{"version":3,"culture":"en-US","name":"Sheet1","columns":[{"at":"1:1","format":"0"}],"cells":[]}""")]
    [InlineData("""{"version":3,"culture":"en-US","name":"Sheet1","columns":[{"at":"A:B","format":"0"},{"at":"B:C","format":"0"}],"cells":[]}""")]
    [InlineData("""{"version":3,"culture":"en-US","name":"Sheet1","rows":[{"at":"A:A","format":"0"}],"cells":[]}""")]
    [InlineData("""{"version":3,"culture":"en-US","name":"Sheet1","rows":[{"at":"1:1","format":"0","colour":"red"}],"cells":[]}""")]
    [InlineData("""{"version":3,"culture":"en-US","name":"Sheet1","rows":[{"at":"2:2","align":"justify"}],"cells":[]}""")]
    [InlineData("""{"version":3,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":20}],"cells":[]}""")]
    [InlineData("""{"version":4,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B"}],"cells":[]}""")]
    [InlineData("""{"version":4,"culture":"en-US","name":"Sheet1","columnWidths":[{"width":20}],"cells":[]}""")]
    [InlineData("""{"version":4,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"2:2","width":20}],"cells":[]}""")]
    [InlineData("""{"version":4,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B2","width":20}],"cells":[]}""")]
    [InlineData("""{"version":4,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":0}],"cells":[]}""")]
    [InlineData("""{"version":4,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":-1}],"cells":[]}""")]
    [InlineData("""{"version":4,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":256}],"cells":[]}""")]
    [InlineData("""{"version":4,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":"20"}],"cells":[]}""")]
    [InlineData("""{"version":4,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:C","width":20},{"at":"C:D","width":12}],"cells":[]}""")]
    [InlineData("""{"version":4,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":20,"hidden":true}],"cells":[]}""")]
    [InlineData("""{"version":4,"culture":"en-US","name":"Sheet1","columnWidths":{"B:B":20},"cells":[]}""")]
    [InlineData("""{"version":4,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":20,"custom":true}],"cells":[]}""")]
    [InlineData("""{"version":5,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":20}],"cells":[]}""")]
    [InlineData("""{"version":5,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":20,"custom":"yes"}],"cells":[]}""")]
    [InlineData("""{"version":5,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":20,"custom":1}],"cells":[]}""")]
    [InlineData("""{"version":5,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":20,"custom":null}],"cells":[]}""")]
    [InlineData("""{"version":5,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":20,"kind":"setByUser"}],"cells":[]}""")]
    [InlineData("""{"version":6,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":20}],"cells":[]}""")]
    [InlineData("""{"version":6,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":20,"custom":true}],"cells":[]}""")]
    [InlineData("""{"version":6,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":20,"kind":"automatic"}],"cells":[]}""")]
    [InlineData("""{"version":6,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":20,"kind":true}],"cells":[]}""")]
    [InlineData("""[1]""")]
    [InlineData("""not json""")]
    public void Anything_its_version_does_not_define_is_refused(string json)
    {
        Assert.Throws<SheetDocumentException>(() => SheetDocument.FromJson(json));
    }

    [Fact] // ADR-0048 (SH-12): number formats and alignment round-trip, a formatted blank cell included
    public void Formatting_round_trips()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1234.5");
        sheet.SetFormat(CellAddress.Parse("A1"), NumberFormat.Parse("#,##0.00"));
        sheet.SetAlignment(CellAddress.Parse("A1"), HorizontalAlignment.Center);
        sheet.Enter("B1", "9/26/2026");
        sheet.SetFormat(CellAddress.Parse("C1"), NumberFormat.Parse("0%"));
        sheet.SetAlignment(CellAddress.Parse("D1"), HorizontalAlignment.Right);

        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));

        foreach (var name in new[] { "A1", "B1", "C1", "D1" })
        {
            var address = CellAddress.Parse(name);
            Assert.Equal(sheet.GetFormat(address), reopened.GetFormat(address));
            Assert.Equal(sheet.GetAlignment(address), reopened.GetAlignment(address));
            Assert.Equal(sheet.GetDisplay(address), reopened.GetDisplay(address));
        }
        Assert.Equal("1,234.50", reopened.GetDisplay(CellAddress.Parse("A1")).Text);
        reopened.Enter(CellAddress.Parse("C1"), "25"); // a number typed into a percent cell is a percentage (ADR-0047, second run)
        Assert.Equal("25%", reopened.GetDisplay(CellAddress.Parse("C1")).Text);
    }

    [Fact] // ADR-0048 (SH-12, SH-17): a document holds no Values; the engine alone computes the same ones again
    public void The_engine_alone_computes_a_saved_documents_values()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "3");
        sheet.Enter("A2", "4");
        sheet.Enter("A3", "=SUM(A1:A2)*ROUND(1.25,1)");
        sheet.Enter("A4", "=XLOOKUP(4,A1:A2,A1:A2)&\" found\"");
        var json = sheet.ToDocument().ToJson();

        Assert.DoesNotContain("4 found", json);
        Assert.DoesNotContain("9.1", json);

        var reopened = Sheet.Open(SheetDocument.FromJson(json));

        Assert.Equal(sheet.GetValue(CellAddress.Parse("A3")), reopened.GetValue(CellAddress.Parse("A3")));
        Assert.Equal("4 found", reopened.GetValue(CellAddress.Parse("A4"))!.Value.Text);
    }

    [Fact] // ADR-0048: a number round-trips exactly through the document, every bit
    public void Numbers_round_trip_exactly()
    {
        var sheet = NewSheet();
        var values = new[] { 0.1 + 0.2, 1.0 / 3, double.Epsilon, double.MaxValue, -123456.789e-200 };
        for (var i = 0; i < values.Length; i++)
        {
            sheet.SetEntry(new CellAddress(i, 0), Entry.FromValue(Value.FromNumber(values[i])));
        }

        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));

        for (var i = 0; i < values.Length; i++)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(values[i]), BitConverter.DoubleToInt64Bits(reopened.GetValue(new CellAddress(i, 0))!.Value.Number));
        }
    }

    [Fact] // ADR-0048: text with any characters round-trips
    public void Text_round_trips()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "'=not a formula");
        sheet.Enter("A2", "say \"hi\" \\ 日本語");
        sheet.Enter("A3", "'");

        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));

        foreach (var name in new[] { "A1", "A2", "A3" })
        {
            Assert.Equal(sheet.Value(name), reopened.Value(name));
        }
        Assert.Equal("", reopened.Value("A3")!.Value.Text);
    }

    [Fact] // ADR-0048, ADR-0046: a version 1 document still opens, as a Sheet named Sheet1
    public void A_version_1_document_opens_named_sheet1()
    {
        var document = SheetDocument.FromJson("""{"version":1,"culture":"en-US","cells":[{"at":"A1","number":2},{"at":"A2","formula":"=A1*3"}]}""");

        var sheet = Sheet.Open(document);

        Assert.Equal("Sheet1", sheet.Name);
        Assert.Equal(6, sheet.Number("A2"));
        Assert.StartsWith("""{"version":7,"culture":"en-US","name":"Sheet1",""", sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0048, ADR-0047: a version 2 document still opens; its cells' formats are the cells' own
    public void A_version_2_document_opens_with_its_cell_formats()
    {
        var document = SheetDocument.FromJson("""
            {"version":2,"culture":"en-US","name":"Book","cells":[
              {"at":"A1","number":0.25,"format":"0%","align":"center"},
              {"at":"B1","format":"General","align":"right"}]}
            """);

        var sheet = Sheet.Open(document);

        Assert.Equal("25%", sheet.GetDisplay(CellAddress.Parse("A1")).Text);
        Assert.Equal(HorizontalAlignment.Center, sheet.GetAlignment(CellAddress.Parse("A1")));
        Assert.Null(document.Cells[1].Format);
        Assert.Empty(document.Columns);
        Assert.Empty(document.Rows);
        Assert.Equal(
            """{"version":7,"culture":"en-US","name":"Book","cells":[{"at":"A1","number":0.25,"format":"0%","align":"center"},{"at":"B1","align":"right"}]}""",
            sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0047 (SH-21): formats on whole columns and rows are one entry each, adjacent ones one run, and round-trip
    public void Column_and_row_formats_are_recorded_as_runs()
    {
        var sheet = NewSheet();
        sheet.SetFormat(CellRange.Parse("B:D"), NumberFormat.Parse("0.00"));
        sheet.SetAlignment(CellRange.Parse("F:F"), HorizontalAlignment.Center);
        sheet.SetFormat(CellRange.Parse("3:4"), NumberFormat.Parse("0%"));
        sheet.Enter("C3", "50%");
        sheet.Enter("C5", "0.5");
        sheet.SetFormat(CellAddress.Parse("C5"), NumberFormat.General);

        var json = sheet.ToDocument().ToJson();

        Assert.Equal(
            """{"version":7,"culture":"en-US","name":"Sheet1","columns":[{"at":"B:D","format":"0.00"},{"at":"F:F","align":"center"}],"rows":[{"at":"3:4","format":"0%"}],"cells":[{"at":"C3","number":0.5},{"at":"C5","number":0.5,"format":"General"}]}""",
            json);
        var reopened = Sheet.Open(SheetDocument.FromJson(json));
        Assert.Equal("50%", reopened.GetDisplay(CellAddress.Parse("C3")).Text);
        Assert.Equal("0.5", reopened.GetDisplay(CellAddress.Parse("C5")).Text);
        Assert.Equal("0.00", reopened.GetFormat(CellAddress.Parse("D1000")).Code);
        Assert.Equal(HorizontalAlignment.Center, reopened.GetAlignment(CellAddress.Parse("F7")));
        Assert.Equal(json, reopened.ToDocument().ToJson());
    }

    [Fact] // ADR-0046: the Sheet's name round-trips, and a Reference qualified with it reads the Sheet when reopened
    public void The_name_round_trips()
    {
        var sheet = new Sheet(EnUs, "Risk 'Q3' book");
        sheet.Enter("A1", "7");
        sheet.Enter("B1", "='Risk ''Q3'' book'!A1+1");

        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));

        Assert.Equal("Risk 'Q3' book", reopened.Name);
        Assert.Equal(8, reopened.Number("B1"));
    }

    [Fact] // ADR-0048: the invariant culture can be declared and recorded too
    public void The_invariant_culture_round_trips()
    {
        var sheet = new Sheet(CultureInfo.InvariantCulture);
        sheet.Enter(CellAddress.Parse("A1"), "1.5");

        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));

        Assert.Equal(CultureInfo.InvariantCulture.Name, reopened.Culture.Name);
        Assert.Equal(1.5, reopened.GetValue(CellAddress.Parse("A1"))!.Value.Number);
    }
}
