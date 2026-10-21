using Xunit;
using static ExGrid.Data.Xlsx.Tests.Fixtures;

namespace ExGrid.Data.Xlsx.Tests;

/// <summary>
/// The shared strings, the one part held whole (ADR-0330 §9): a text in runs is its runs' text, marked rich; a phonetic
/// guide is not text; Excel's <c>_xHHHH_</c> escapes are undone on reading and made on writing; and the writer shares each
/// text once.
/// </summary>
public class SharedStringTests
{
    private const string Strings = $"""
        <sst xmlns="{Main}" count="6" uniqueCount="5">
          <si><t>EMEA</t></si>
          <si><r><rPr><b/><sz val="11"/></rPr><t>Bold</t></r><r><t xml:space="preserve"> and plain</t></r></si>
          <si><t>東京</t><rPh sb="0" eb="2"><t>トウキョウ</t></rPh><phoneticPr fontId="1"/></si>
          <si><t>line_x000D_
        break and _x005F_x0041_ kept</t></si>
          <si><t/></si>
        </sst>
        """;

    [Fact] // ADR-0330 §9: a cell's shared string is its text: runs joined and marked rich, a phonetic guide left out, escapes undone
    public async Task Shared_strings_are_read_as_their_text()
    {
        var sheet = Sheet("""<row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c><c r="C1" t="s"><v>2</v></c><c r="D1" t="s"><v>3</v></c><c r="E1" t="s"><v>4</v></c><c r="F1" t="s"><v>0</v></c></row>""");
        using var reader = await OpenAsync(Book(sheet, sharedStrings: Strings));

        var cells = (await reader.ReadSheetAsync(0)).Cells;

        Assert.Equal(["EMEA", "Bold and plain", "東京", "line\r\nbreak and _x0041_ kept", "", "EMEA"], cells.Select(c => c.Text));
        Assert.Equal([false, true, false, false, false, false], cells.Select(c => c.IsRichText));
        Assert.Equal(5, reader.SharedStrings.Count);
        Assert.True(reader.SharedStrings.IsRich(1));
    }

    [Fact] // ADR-0330 §9: an inline string is read as a shared one is, runs and all
    public async Task Inline_strings_are_read_as_their_text()
    {
        var sheet = Sheet("""<row r="1"><c r="A1" t="inlineStr"><is><t>plain</t></is></c><c r="B1" t="inlineStr"><is><r><t>a</t></r><r><rPr><i/></rPr><t>b</t></r></is></c><c r="C1" t="inlineStr"><is/></c></row>""");
        using var reader = await OpenAsync(Book(sheet));

        var cells = (await reader.ReadSheetAsync(0)).Cells;

        Assert.Equal(["plain", "ab", ""], cells.Select(c => c.Text));
        Assert.Equal([false, true, false], cells.Select(c => c.IsRichText));
    }

    [Fact] // ADR-0330 §9: text XML cannot carry, and text that looks escaped, round-trip through the writer's escapes
    public async Task Escapes_round_trip()
    {
        string[] texts = ["a\rb", "tab\there", "_x0041_", "bell\u0007", " lead", "trail ", "multi\nline", "plain"];
        var file = await WriteAsync(book =>
        {
            var sheet = book.AddSheet("S");
            for (var i = 0; i < texts.Length; i++)
                sheet.Write(new XlsxCell(i, 0, XlsxCellType.SharedString, null, texts[i]));
            return Task.CompletedTask;
        });
        using var reader = await OpenAsync(file);

        var cells = (await reader.ReadSheetAsync(0)).Cells;

        Assert.Equal(texts, cells.Select(c => c.Text));
    }

    [Fact] // ADR-0330 §9: the writer shares each text once, however many cells hold it
    public async Task The_writer_shares_each_text_once()
    {
        var file = await WriteAsync(async book =>
        {
            var sheet = book.AddSheet("S");
            await sheet.WriteAsync(Enumerable.Range(0, 1_000).Select(i => new XlsxCell(i, 0, XlsxCellType.SharedString, null, i % 2 == 0 ? "even" : "odd")), Token);
        });
        using var reader = await OpenAsync(file);

        var cells = (await reader.ReadSheetAsync(0)).Cells;

        Assert.Equal(["even", "odd"], reader.SharedStrings);
        Assert.Equal(1_000, cells.Count);
        Assert.Equal("odd", cells[999].Text);
    }

    [Theory] // ADR-0330 §9: Excel's escapes, decoded as ECMA-376 §22.9.2.19 says, and only where they are whole
    [InlineData("_x000D_", "\r")]
    [InlineData("_x005F_x000D_", "_x000D_")]
    [InlineData("_x00", "_x00")]
    [InlineData("_xZZZZ_", "_xZZZZ_")]
    [InlineData("a_x0041_b_x0042_", "aAbB")]
    public void Escapes_decode(string written, string text)
    {
        Assert.Equal(text, XString.Decode(written));
    }
}
