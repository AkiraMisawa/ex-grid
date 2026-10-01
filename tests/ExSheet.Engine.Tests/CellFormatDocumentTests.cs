using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0063, ADR-0048 (SH-38): the Sheet Document records Font, Fill and Borders for cells, rows and
/// columns at version 8; an older version reads with none of them; a colour of any other kind than
/// Automatic or RGB is refused by name.
/// </summary>
public class CellFormatDocumentTests
{
    private const string Head = "{\"version\":8,\"culture\":\"en-US\",\"name\":\"Sheet1\"";

    private static CellAddress At(string address) => CellAddress.Parse(address);

    private static SheetStep Set(Sheet sheet, CellFormatChange change, params string[] ranges) =>
        sheet.Do(SheetEdit.SetCellFormat([.. ranges.Select(CellRange.Parse)], change));

    [Fact] // ADR-0063, ADR-0048 (SH-38): Font, Fill and Borders are recorded for cells, rows and columns, and round-trip
    public void Font_fill_and_borders_round_trip()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Italic = true, Fill = CellFill.Solid(CellColour.FromRgb(0xFFFF00)) }, "B:B");
        Set(sheet, new CellFormatChange { Bold = true, FontColour = CellColour.FromRgb(0xFF0000) }, "3:3");
        Set(sheet, new CellFormatChange
        {
            Underline = true,
            Strikethrough = true,
            Fill = CellFill.None,
            Borders = new BorderChange { Top = new BorderLine(BorderLineStyle.Thin), Bottom = new BorderLine(BorderLineStyle.Double, CellColour.FromRgb(0x0000FF)) },
        }, "B5");

        var json = sheet.ToDocument().ToJson();

        Assert.Equal(
            Head + ""","columns":[{"at":"B:B","font":{"italic":true},"fill":"#FFFF00"}]"""
            + ""","rows":[{"at":"3:3","font":{"color":"#FF0000","bold":true}}]"""
            + ""","cells":[{"at":"B3","font":{"color":"#FF0000","bold":true,"italic":true}},"""
            + """{"at":"B5","font":{"italic":true,"underline":true,"strikethrough":true},"fill":"none","borders":{"top":{"style":"thin"},"bottom":{"style":"double","color":"#0000FF"}}}]}""",
            json);
        var reopened = Sheet.Open(SheetDocument.FromJson(json));
        Assert.Equal(json, reopened.ToDocument().ToJson());
        foreach (var cell in new[] { "B3", "B4", "B5", "B6", "B9", "C3", "C5" })
        {
            Assert.Equal(sheet.GetCellFormat(At(cell)), reopened.GetCellFormat(At(cell)));
        }
    }

    [Fact] // ADR-0063, ADR-0048 (SH-38): every line style is written by the name Excel's own file gives it, and reads back
    public void Every_line_style_round_trips()
    {
        var sheet = NewSheet();
        var styles = Enum.GetValues<BorderLineStyle>().Where(s => s != BorderLineStyle.None).ToList();
        for (var i = 0; i < styles.Count; i++)
        {
            Set(sheet, new CellFormatChange { Borders = new BorderChange { Bottom = new BorderLine(styles[i], CellColour.FromRgb(i)) } }, $"B{i + 2}");
        }

        var json = sheet.ToDocument().ToJson();

        foreach (var name in new[] { "hair", "thin", "medium", "thick", "double", "dotted", "dashed", "dashDot", "dashDotDot", "mediumDashed", "mediumDashDot", "mediumDashDotDot", "slantDashDot" })
        {
            Assert.Contains($$"""{"style":"{{name}}",""", json);
        }
        var reopened = Sheet.Open(SheetDocument.FromJson(json));
        for (var i = 0; i < styles.Count; i++)
        {
            Assert.Equal(new BorderLine(styles[i], CellColour.FromRgb(i)), reopened.GetBorders(At($"B{i + 2}")).Bottom);
            // The cell below shows the same line on its top, which it shares (case 11-7).
            Assert.Equal(new BorderLine(styles[i], CellColour.FromRgb(i)), reopened.GetBorders(At($"B{i + 3}")).Top);
        }
    }

    [Fact] // ADR-0063, ADR-0048 (SH-38), the twelfth Windows run: a document ticket 55's code wrote records both sides of each edge it set, and they agree, so it shows the same edges from either side under Excel's rule, at every level, and reads back unchanged
    public void A_document_recording_both_sides_of_an_edge_shows_the_same_edges()
    {
        // B2's bottom and B3's top, C5's right and D5's left, row 7's bottom and row 8's top, and
        // column F's right and column G's left, each written on both sides.
        var json = Head
            + ""","columns":[{"at":"F:F","borders":{"right":{"style":"thick"}}},{"at":"G:G","borders":{"left":{"style":"thick"}}}]"""
            + ""","rows":[{"at":"7:7","borders":{"bottom":{"style":"double","color":"#0000FF"}}},{"at":"8:8","borders":{"top":{"style":"double","color":"#0000FF"}}}]"""
            + ""","cells":[{"at":"B2","borders":{"bottom":{"style":"thin"}}},{"at":"B3","borders":{"top":{"style":"thin"}}},"""
            + """{"at":"C5","borders":{"right":{"style":"medium","color":"#FF0000"}}},{"at":"D5","borders":{"left":{"style":"medium","color":"#FF0000"}}}]}""";
        var thin = new BorderLine(BorderLineStyle.Thin);
        var mediumRed = new BorderLine(BorderLineStyle.Medium, CellColour.FromRgb(0xFF0000));
        var thick = new BorderLine(BorderLineStyle.Thick);
        var doubleBlue = new BorderLine(BorderLineStyle.Double, CellColour.FromRgb(0x0000FF));

        var sheet = Sheet.Open(SheetDocument.FromJson(json));

        Assert.Equal(new CellBorders(Bottom: thin), sheet.GetBorders(At("B2")));
        Assert.Equal(new CellBorders(Top: thin), sheet.GetBorders(At("B3")));
        Assert.Equal(new CellBorders(Right: mediumRed), sheet.GetBorders(At("C5")));
        Assert.Equal(new CellBorders(Left: mediumRed), sheet.GetBorders(At("D5")));
        Assert.Equal(new CellBorders(Right: thick), sheet.GetBorders(At("F2")));
        Assert.Equal(new CellBorders(Left: thick), sheet.GetBorders(At("G1048576")));
        Assert.Equal(new CellBorders(Bottom: doubleBlue), sheet.GetBorders(At("A7")));
        Assert.Equal(new CellBorders(Top: doubleBlue), sheet.GetBorders(At("XFD8")));
        Assert.Equal(json, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0063 (SH-38): a part recorded at its default is written, so it still hides the level under it
    public void A_part_recorded_at_its_default_round_trips()
    {
        var json = Head + ""","columns":[{"at":"B:B","font":{"bold":true},"borders":{"left":{"style":"thin"}}}],"cells":[{"at":"B2","font":{},"borders":{}}]}""";

        var sheet = Sheet.Open(SheetDocument.FromJson(json));

        Assert.Equal(CellFont.Default, sheet.GetFont(At("B2")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("B2")));
        Assert.Equal(new CellFont(Bold: true), sheet.GetFont(At("B3")));
        Assert.Equal(json, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0048 (SH-38): the reader takes an explicit Automatic colour, false emphasis and either case of hex
    public void The_reader_takes_what_version_8_defines()
    {
        var document = SheetDocument.FromJson(Head + ""","cells":[{"at":"A1","font":{"color":"automatic","bold":false,"italic":true},"fill":"#c0ffee","borders":{"left":{"style":"hair","color":"automatic"}}}]}""");

        var cell = Assert.Single(document.Cells);
        Assert.Equal(new CellFont(Italic: true), cell.Font);
        Assert.Equal(CellFill.Solid(CellColour.FromRgb(0xC0FFEE)), cell.Fill);
        Assert.Equal(new CellBorders(Left: new BorderLine(BorderLineStyle.Hair)), cell.Borders);
    }

    [Fact] // ADR-0048 (SH-38): a document of an older version reads with no Font, Fill or Borders
    public void An_older_version_reads_with_none()
    {
        var document = SheetDocument.FromJson("""{"version":6,"culture":"en-US","name":"Sheet1","columns":[{"at":"B:B","format":"0.00"}],"cells":[{"at":"B2","number":1,"align":"center"}]}""");

        var sheet = Sheet.Open(document);

        Assert.Equal((CellFont?)null, document.Cells[0].Font);
        Assert.Null(document.Columns[0].Fill);
        var shown = sheet.GetCellFormat(At("B2"));
        Assert.Equal((CellFont.Default, CellFill.None, CellBorders.None), (shown.Font, shown.Fill, shown.Borders));
        Assert.Equal("0.00", shown.NumberFormat.Code);
        Assert.StartsWith(Head, sheet.ToDocument().ToJson());
    }

    [Theory] // ADR-0048 (SH-38): an older version does not define Font, Fill or Borders, so a document of one holding them is refused
    [InlineData("""{"version":6,"culture":"en-US","name":"Sheet1","cells":[{"at":"A1","font":{"bold":true}}]}""")]
    [InlineData("""{"version":6,"culture":"en-US","name":"Sheet1","cells":[{"at":"A1","fill":"#FFFF00"}]}""")]
    [InlineData("""{"version":6,"culture":"en-US","name":"Sheet1","rows":[{"at":"2:2","borders":{}}],"cells":[]}""")]
    [InlineData("""{"version":7,"culture":"en-US","name":"Sheet1","cells":[{"at":"A1","font":{"bold":true}}]}""")]
    [InlineData("""{"version":3,"culture":"en-US","name":"Sheet1","columns":[{"at":"A:A","fill":"none"}],"cells":[]}""")]
    public void An_older_version_holding_them_is_refused(string json)
    {
        Assert.Throws<SheetDocumentException>(() => SheetDocument.FromJson(json));
    }

    [Theory] // ADR-0063, ADR-0048 (SH-38): a colour of any kind but Automatic or RGB is refused by name, never guessed at
    [InlineData(""","cells":[{"at":"A1","font":{"color":{"theme":4,"tint":0.4}}}]}""", "theme")]
    [InlineData(""","cells":[{"at":"A1","font":{"color":"red"}}]}""", "red")]
    [InlineData(""","cells":[{"at":"A1","font":{"color":"#F00"}}]}""", "#F00")]
    [InlineData(""","cells":[{"at":"A1","font":{"color":"#FF00000"}}]}""", "#FF00000")]
    [InlineData(""","cells":[{"at":"A1","font":{"color":"FF0000"}}]}""", "FF0000")]
    [InlineData(""","cells":[{"at":"A1","fill":"automatic"}]}""", "automatic")]
    [InlineData(""","cells":[{"at":"A1","fill":{"theme":1}}]}""", "theme")]
    [InlineData(""","columns":[{"at":"B:B","fill":"#GGGGGG"}],"cells":[]}""", "#GGGGGG")]
    [InlineData(""","cells":[{"at":"A1","borders":{"top":{"style":"thin","color":{"indexed":10}}}}]}""", "indexed")]
    public void A_colour_of_another_kind_is_refused_by_name(string rest, string named)
    {
        var refusal = Assert.Throws<SheetDocumentException>(() => SheetDocument.FromJson(Head + rest));

        Assert.Contains(named, refusal.Message, StringComparison.Ordinal);
    }

    [Theory] // ADR-0063, ADR-0048 (SH-38): anything else version 8 does not define in a Font, Fill or Borders is refused
    [InlineData(""","cells":[{"at":"A1","font":{"size":14}}]}""")]
    [InlineData(""","cells":[{"at":"A1","font":{"bold":"yes"}}]}""")]
    [InlineData(""","cells":[{"at":"A1","font":{"underline":"double"}}]}""")]
    [InlineData(""","cells":[{"at":"A1","font":"bold"}]}""")]
    [InlineData(""","cells":[{"at":"A1","borders":{"diagonal":{"style":"thin"}}}]}""")]
    [InlineData(""","cells":[{"at":"A1","borders":{"top":{"style":"none"}}}]}""")]
    [InlineData(""","cells":[{"at":"A1","borders":{"top":{"style":"Thin"}}}]}""")]
    [InlineData(""","cells":[{"at":"A1","borders":{"top":{"color":"#000000"}}}]}""")]
    [InlineData(""","cells":[{"at":"A1","borders":{"top":{"style":"thin","width":2}}}]}""")]
    [InlineData(""","cells":[{"at":"A1","borders":{"top":"thin"}}]}""")]
    [InlineData(""","cells":[{"at":"A1","borders":[]}]}""")]
    [InlineData(""","rows":[{"at":"2:2","pattern":"gray125"}],"cells":[]}""")]
    public void Anything_else_is_refused(string rest)
    {
        Assert.Throws<SheetDocumentException>(() => SheetDocument.FromJson(Head + rest));
    }
}
