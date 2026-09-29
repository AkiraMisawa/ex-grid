using ExGrid.Clipboard;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// Where each pasted field came from (ADR-0050, item 10 / DC-33): an invariant number from
/// Excel's <c>x:num</c> or ExGrid's own unformatted HTML, shown text from everything else.
/// The Excel samples are the shape Excel for Windows puts on the clipboard as CF_HTML, as the
/// browser's <c>paste</c> event hands it over: the Office namespaces, the <c>mso-</c> style
/// block with the displayed separators, the table's own attributes, the fragment comments,
/// unquoted attributes, and a cell's attributes wrapped onto a second line.
/// </summary>
public class PasteOriginTests
{
    /// <summary>Excel under en-US: a formatted number, a General number (bare <c>x:num</c>),
    /// text, a date (its serial on <c>x:num</c>), a percentage, and a Formula's result.</summary>
    private const string ExcelEnUs = """
        <html xmlns:v="urn:schemas-microsoft-com:vml"
        xmlns:o="urn:schemas-microsoft-com:office:office"
        xmlns:x="urn:schemas-microsoft-com:office:excel"
        xmlns="http://www.w3.org/TR/REC-html40">

        <head>
        <meta http-equiv=Content-Type content="text/html; charset=utf-8">
        <meta name=ProgId content=Excel.Sheet>
        <meta name=Generator content="Microsoft Excel 15">
        <link id=Main-File rel=Main-File
        href="file:///C:/Users/user/AppData/Local/Temp/msohtmlclip1/01/clip.htm">
        <link rel=File-List
        href="file:///C:/Users/user/AppData/Local/Temp/msohtmlclip1/01/clip_filelist.xml">
        <style>
        <!--table
        	{mso-displayed-decimal-separator:"\.";
        	mso-displayed-thousand-separator:"\,";}
        @page
        	{margin:.75in .7in .75in .7in;
        	mso-header-margin:.3in;
        	mso-footer-margin:.3in;}
        td
        	{padding-top:1px;
        	mso-number-format:General;
        	white-space:nowrap;}
        .xl65
        	{mso-number-format:"\#\,\#\#0\.00";}
        .xl66
        	{mso-number-format:"Short Date";}
        .xl67
        	{mso-number-format:"0\.00%";}
        -->
        </style>
        </head>

        <body link="#0563C1" vlink="#954F72">

        <table border=0 cellpadding=0 cellspacing=0 width=192 style='border-collapse:
         collapse;width:144pt'>
        <!--StartFragment-->
         <col width=64 span=3 style='width:48pt'>
         <tr height=20 style='height:15.0pt'>
          <td height=20 class=xl65 align=right width=64 style='height:15.0pt;width:48pt'
          x:num="1234.5">1,234.50</td>
          <td align=right width=64 style='width:48pt' x:num>42</td>
          <td width=64 style='width:48pt'>Alpha</td>
         </tr>
         <tr height=20 style='height:15.0pt'>
          <td height=20 class=xl66 align=right style='height:15.0pt' x:num="45292">1/1/2024</td>
          <td class=xl67 align=right x:num="0.125">12.50%</td>
          <td align=right x:num="0.33333333333333331" x:fmla="=1/3">0.333333</td>
         </tr>
        <!--EndFragment-->
        </table>

        </body>

        </html>
        """;

    /// <summary>Excel under de-DE: the comma is the decimal separator and the dot groups, so
    /// the text shown is <c>1.234,50</c> while <c>x:num</c> still carries <c>1234.5</c>.</summary>
    private const string ExcelDeDe = """
        <html xmlns:v="urn:schemas-microsoft-com:vml"
        xmlns:o="urn:schemas-microsoft-com:office:office"
        xmlns:x="urn:schemas-microsoft-com:office:excel"
        xmlns="http://www.w3.org/TR/REC-html40">

        <head>
        <meta http-equiv=Content-Type content="text/html; charset=utf-8">
        <meta name=ProgId content=Excel.Sheet>
        <meta name=Generator content="Microsoft Excel 15">
        <style>
        <!--table
        	{mso-displayed-decimal-separator:"\,";
        	mso-displayed-thousand-separator:"\.";}
        .xl65
        	{mso-number-format:"\#\,\#\#0\.00";}
        -->
        </style>
        </head>

        <body link="#0563C1" vlink="#954F72">

        <table border=0 cellpadding=0 cellspacing=0 width=128 style='border-collapse:
         collapse;width:96pt'>
        <!--StartFragment-->
         <col width=64 span=2 style='width:48pt'>
         <tr height=20 style='height:15.0pt'>
          <td height=20 class=xl65 align=right width=64 style='height:15.0pt;width:48pt'
          x:num="1234.5">1.234,50</td>
          <td align=right width=64 style='width:48pt' x:num>1,5</td>
         </tr>
        <!--EndFragment-->
        </table>

        </body>

        </html>
        """;

    [Fact] // ADR-0050 item 10 / DC-33: Excel's x:num is an invariant number; its text content is shown text
    public void Excel_x_num_fields_are_invariant_and_the_rest_are_shown_text()
    {
        var block = ClipboardParse.ParseBlock(ExcelEnUs, "1,234.50\t42\tAlpha\r\n1/1/2024\t12.50%\t0.333333\r\n");

        Assert.NotNull(block);
        Assert.Equal([["1234.5", "42", "Alpha"], ["45292", "0.125", "0.33333333333333331"]], block!.Values);
        Assert.Equal(
            [
                [PasteFieldOrigin.Invariant, PasteFieldOrigin.ShownText, PasteFieldOrigin.ShownText],
                [PasteFieldOrigin.Invariant, PasteFieldOrigin.Invariant, PasteFieldOrigin.Invariant],
            ],
            block.Origins);
    }

    [Fact] // ADR-0050 item 10 / DC-33: under de-DE, x:num still carries 1234.5 — the field that would be misread is marked invariant
    public void Excel_under_a_comma_decimal_culture_marks_x_num_invariant_and_the_bare_form_shown()
    {
        var block = ClipboardParse.ParseBlock(ExcelDeDe, "1.234,50\t1,5\r\n");

        Assert.NotNull(block);
        Assert.Equal([["1234.5", "1,5"]], block!.Values);
        // The bare x:num says only "a number", and its text is written in the source's
        // culture: reading it as invariant would turn 1,5 into fifteen.
        Assert.Equal([[PasteFieldOrigin.Invariant, PasteFieldOrigin.ShownText]], block.Origins);
    }

    [Fact] // ADR-0050 item 10 / DC-33: ExGrid's own unformatted HTML is invariant, every field of it
    public void The_grids_own_html_is_invariant_throughout()
    {
        var grid = new GridExtent(10, 3);
        var plan = ClipboardRules.PlanCopy(GridSelection.Empty.Click(new(0, 0), grid).ExtendTo(new(1, 2), grid)).Plan;
        string[][] raw = [["1234.5", "2026-01-05T00:00:00", "TRUE"], ["-7", "Alpha", ""]];
        var (text, html) = ClipboardData.Assemble(plan, (r, c) => "shown", (r, c) => raw[r][c]);

        var block = ClipboardParse.ParseBlock(html, text);

        Assert.NotNull(block);
        Assert.Equal(raw, block!.Values);
        Assert.All(block.Origins.SelectMany(row => row), origin => Assert.Equal(PasteFieldOrigin.Invariant, origin));
    }

    [Fact] // ADR-0050 item 10 / DC-33: a table without the grid's mark is some page's shown text, whatever it looks like
    public void A_foreign_table_without_the_mark_is_shown_text()
    {
        var block = ClipboardParse.ParseBlock("<table><tr><td>1.234</td><td>x</td></tr></table>", null);

        Assert.Equal([[PasteFieldOrigin.ShownText, PasteFieldOrigin.ShownText]], block!.Origins);
    }

    [Fact] // ADR-0050 item 10 / DC-33: the text flavour is shown text; padding is too
    public void The_text_flavour_is_shown_text_throughout()
    {
        var block = ClipboardParse.ParseBlock(null, "1234.5\t2\r\n3\r\n");

        Assert.Equal([["1234.5", "2"], ["3", ""]], block!.Values);
        Assert.All(block.Origins.SelectMany(row => row), origin => Assert.Equal(PasteFieldOrigin.ShownText, origin));
    }

    [Fact] // ADR-0050 item 10: Parse is the block's values, unchanged for the Consumers that read it
    public void Parse_answers_the_same_values_as_the_block()
    {
        Assert.Equal(ClipboardParse.ParseBlock(ExcelEnUs, null)!.Values, ClipboardParse.Parse(ExcelEnUs, null));
        Assert.Null(ClipboardParse.ParseBlock(null, null));
    }

    [Fact] // ADR-0014 (amended 2026-09-29): a block read from a table in text/html (Excel's, the grid's, any page's) is from a table
    public void A_block_read_from_an_html_table_is_from_a_table()
    {
        Assert.True(ClipboardParse.ParseBlock(ExcelEnUs, "1,234.50\r\n")!.IsFromTable);
        Assert.True(ClipboardParse.ParseBlock("<table data-ex-grid=\"invariant\"><tr><td>7</td></tr></table>", "7\r\n")!.IsFromTable);
        Assert.True(ClipboardParse.ParseBlock("<table><tr><td>7</td></tr></table>", null)!.IsFromTable);
    }

    [Fact] // ADR-0014 (amended 2026-09-29): plain text alone, or HTML that holds no table, is not from a table
    public void Plain_text_alone_or_html_without_a_table_is_not_from_a_table()
    {
        Assert.False(ClipboardParse.ParseBlock(null, "=A1")!.IsFromTable);
        Assert.False(ClipboardParse.ParseBlock("", "=A1")!.IsFromTable);
        Assert.False(ClipboardParse.ParseBlock("<p>=A1</p>", "=A1")!.IsFromTable);
    }
}
