using System.Globalization;
using Bunit;
using ExGrid.Selection;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// The clipboard as ExSheet wires it (ticket 14, ADR-0048/0050/0005): a paste may spill from one
/// cell and the block becomes the Selection; every pasted field is taken as if typed under the
/// Sheet's culture; a paste is one undo step; a copy is the engine's, answered through ExGrid's
/// copy answer (ADR-0050 item 9): the Values outward, the Entries kept for a paste back.
/// </summary>
public class ClipboardWiringTests : SheetTestContext
{
    private static SheetDocument DocumentOf(params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    private static Task PasteAsync(IRenderedComponent<ExSheet> cut, string text, string? html = null)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnPasteAsync(text, html));
    }

    private static string FormulaBar(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-formula-bar-text").GetAttribute("value") ?? "";

    private static string Notice(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-sheet-notice").TextContent;

    [Fact] // ADR-0048: a Formula and a number pasted from another program are taken as typed under the culture
    public async Task Fields_from_outside_are_taken_as_typed()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "41"))));
        await GoToAsync(cut, "B1:C1");

        await PasteAsync(cut, "=A1+1\t\"1,234\"\r\n");

        Assert.Equal("42", CellText(cut, "B1"));
        Assert.Equal("1,234", CellText(cut, "C1")); // thousands separators give #,##0, as Excel gives them (TYPED-025)
        await GoToAsync(cut, "B1");
        Assert.Equal("=A1+1", FormulaBar(cut));
    }

    [Fact] // ADR-0050 item 3: a 3×3 block pasted onto one cell writes 3×3 and becomes the Selection
    public async Task A_block_onto_one_cell_spills_and_is_selected()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps.Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "B2");

        await PasteAsync(cut, "1\t2\t3\r\n4\t5\t6\r\n7\t8\t9\r\n");

        Assert.Equal("1", CellText(cut, "B2"));
        Assert.Equal("9", CellText(cut, "D4"));
        Assert.Equal(new SelectionRange(1, 1, 3, 3), Assert.Single(selections[^1].Ranges));
        Assert.Equal(new CellPosition(1, 1), selections[^1].Focus);
    }

    [Fact] // ADR-0048: a paste is one undo step, however many cells it writes
    public async Task A_paste_is_one_undo_step()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("B2", "old"))));
        await GoToAsync(cut, "B2");
        await PasteAsync(cut, "1\t2\r\n3\t4\r\n");

        Assert.True(await cut.Instance.UndoAsync());

        Assert.Equal("old", CellText(cut, "B2"));
        Assert.Equal("", CellText(cut, "C3"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0014: one value over a selected range fills every cell of it
    public async Task One_value_fills_the_selected_range()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "A1:A3");

        await PasteAsync(cut, "7\r\n");

        Assert.Equal("7", CellText(cut, "A1"));
        Assert.Equal("7", CellText(cut, "A3"));
    }

    [Fact] // ADR-0050 item 3: a spill past the Sheet's edge is refused by name, and nothing is written
    public async Task A_spill_past_the_edge_is_refused_by_name()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "A1048576");

        await PasteAsync(cut, "1\r\n2\r\n");

        Assert.Contains("past the Sheet's edge", Notice(cut));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0048 (observed in Excel): pasted text that cannot be read as a Formula is taken as text, not refused
    public async Task An_unreadable_formula_is_pasted_as_text()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "A1:B1");

        await PasteAsync(cut, "5\t=SUM(\r\n");

        Assert.Equal("5", CellText(cut, "A1"));
        Assert.Equal("=SUM(", CellText(cut, "B1"));
        Assert.True(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0048 (observed in Excel): a pasted run of # is a value too wide for its source column; the paste is refused by name, nothing written
    public async Task A_pasted_run_of_hashes_is_refused_by_name()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("B1", "old"))));
        await GoToAsync(cut, "A1:C1");

        await PasteAsync(cut, "6\t########\t1,234.50\r\n");

        Assert.Equal("", CellText(cut, "A1"));
        Assert.Equal("old", CellText(cut, "B1"));
        Assert.Equal("", CellText(cut, "C1"));
        Assert.False(cut.Instance.CanUndo);
        Assert.Contains("B1", Notice(cut));
        Assert.Contains("'########'", Notice(cut));
        Assert.Contains("source column was too narrow to show the value", Notice(cut));
    }

    [Fact] // ADR-0048 (observed in Excel, behaviours item 18): Excel's HTML carries the shown #### too, with no x:num; refused the same way
    public async Task A_run_of_hashes_in_excels_html_is_refused()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "A1");

        await PasteAsync(cut, "#####\r\n", "<table><tr><td class=xl65>#####</td></tr></table>");

        Assert.Equal("", CellText(cut, "A1"));
        Assert.Contains("source column was too narrow to show the value", Notice(cut));
    }

    /// <summary>
    /// Excel's HTML flavour for one column of three cells, as the browser's <c>paste</c> event
    /// hands it over on Windows (verification/2026-09-27-windows-excel-2,
    /// clipboard-probe-chrome.json): the head and its style sheet as Excel writes them, each
    /// cell's shown text, and no <c>x:num</c>.
    /// </summary>
    private static string ExcelColumnHtml(int width, string first, string second, string third) =>
        "<html xmlns:v=\"urn:schemas-microsoft-com:vml\"\r\nxmlns:o=\"urn:schemas-microsoft-com:office:office\"\r\n" +
        "xmlns:x=\"urn:schemas-microsoft-com:office:excel\"\r\nxmlns=\"http://www.w3.org/TR/REC-html40\">\r\n\r\n<head>\r\n" +
        "<meta http-equiv=Content-Type content=\"text/html; charset=utf-8\">\r\n<meta name=ProgId content=Excel.Sheet>\r\n" +
        "<meta name=Generator content=\"Microsoft Excel 15\">\r\n<style>\r\n<!--table\r\n\t{mso-displayed-decimal-separator:\"\\.\";\r\n" +
        "\tmso-displayed-thousand-separator:\"\\,\";}\r\ntd\r\n\t{mso-number-format:General;\r\n\twhite-space:nowrap;}\r\n" +
        ".xl65\r\n\t{mso-number-format:\"Short Date\";}\r\n.xl66\r\n\t{mso-number-format:Standard;}\r\n-->\r\n</style>\r\n</head>\r\n\r\n" +
        "<body link=\"#467886\" vlink=\"#96607D\">\r\n\r\n" +
        $"<table border=0 cellpadding=0 cellspacing=0 width={width} style='border-collapse:\r\n collapse'>\r\n<!--StartFragment-->\r\n" +
        $" <col width={width} style='mso-width-source:userset'>\r\n" +
        $" <tr height=19 style='height:14.5pt'>\r\n  <td height=19 align=right width={width} style='height:14.5pt'>{first}</td>\r\n </tr>\r\n" +
        $" <tr height=19 style='height:14.5pt'>\r\n  <td height=19 class=xl65 align=center style='height:14.5pt'>{second}</td>\r\n </tr>\r\n" +
        $" <tr height=19 style='height:14.5pt'>\r\n  <td height=19 class=xl66 align=center style='height:14.5pt'>{third}</td>\r\n </tr>\r\n" +
        "<!--EndFragment-->\r\n</table>\r\n\r\n</body>\r\n\r\n</html>\r\n";

    private const string ExcelColumnText = "6\r\n26/09/2026\r\n1,234.50\r\n";

    [Fact] // ADR-0048, ADR-0050 item 3, DC-38 (observed on Windows, 2026-09-27): Excel's too-narrow column pasted onto one cell is refused by name, and the Selection stays where it was
    public async Task A_too_narrow_excel_column_pasted_onto_one_cell_is_refused_by_name()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps.Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "F7");
        var before = selections.Count;

        await PasteAsync(cut, ExcelColumnText, ExcelColumnHtml(49, "6", "######", "######"));

        // The Sheet refused the spill (GridPasteIntent.Refuse), so the grid did not select the
        // unwritten block: no selection change, F7 alone, as Excel leaves it.
        Assert.Equal(before, selections.Count);
        Assert.Equal(new SelectionRange(6, 5, 1, 1), Assert.Single(selections[^1].Ranges));
        Assert.Equal(new CellPosition(6, 5), selections[^1].Focus);

        Assert.Equal("", CellText(cut, "F7"));
        Assert.Equal("", CellText(cut, "F8"));
        Assert.Equal("", CellText(cut, "F9"));
        Assert.False(cut.Instance.CanUndo);
        Assert.Contains("F8", Notice(cut));
        Assert.Contains("'######'", Notice(cut));
        Assert.Contains("source column was too narrow to show the value", Notice(cut));
    }

    [Fact] // ADR-0048: the notice a refused spill raised stands until the user's own next action, and that action clears it
    public async Task The_refused_spill_notice_is_cleared_by_the_next_selection()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "F7");
        await PasteAsync(cut, ExcelColumnText, ExcelColumnHtml(49, "6", "######", "######"));
        Assert.Contains("too narrow", Notice(cut));

        await GoToAsync(cut, "A1");

        Assert.Equal("", Notice(cut));
    }

    [Fact] // ADR-0048, ADR-0050 item 3: the same flavours from a column wide enough are pasted, and the spill is selected
    public async Task A_wide_enough_excel_column_pasted_onto_one_cell_spills()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps.Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "F2");

        await PasteAsync(cut, ExcelColumnText, ExcelColumnHtml(74, "6", "26/09/2026", "1,234.50"));

        Assert.Equal("6", CellText(cut, "F2"));
        Assert.NotEqual("", CellText(cut, "F3"));
        Assert.Equal("1,234.50", CellText(cut, "F4"));
        Assert.Equal(new SelectionRange(1, 5, 3, 1), Assert.Single(selections[^1].Ranges));
        Assert.Equal("", Notice(cut));
    }

    [Fact] // ADR-0048: a run of # in another Sheet's invariant table is that Sheet's text Value, and is pasted as text
    public async Task A_run_of_hashes_in_an_invariant_table_is_text()
    {
        var source = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "'###"))));
        await GoToAsync(source, "A1");
        var payload = Grid(source).Instance.BuildCopyPayload();
        var target = RenderSheet();
        await GoToAsync(target, "A1");

        await PasteAsync(target, payload.Text!, payload.Html);

        Assert.Equal("###", CellText(target, "A1"));
    }

    [Fact] // ADR-0005/0048: outward, the text flavour is the Values as shown and the HTML flavour the unformatted Values
    public async Task A_copy_carries_the_values_shown_and_unformatted()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "50%"), ("B1", "hello"), ("C1", "=2-1"))));
        await GoToAsync(cut, "A1:C1");

        var payload = Grid(cut).Instance.BuildCopyPayload();

        Assert.Equal("data", payload.Kind);
        Assert.Equal("50%\thello\t1\r\n", payload.Text);
        Assert.Equal("<table data-ex-grid=\"invariant\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"><tr><td x:num=\"0.5\" style='mso-number-format:\"0%\"'>0.5</td><td>hello</td><td x:num=\"1\">1</td></tr></table>", payload.Html);
    }

    [Fact] // ADR-0048 (observed in Excel): a copy to Excel carries formats — x:num with the unformatted Value, mso-number-format with the code — and stays ExGrid's invariant table
    public async Task A_copy_carries_each_cells_format_in_excels_markup()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1"), "9/26/2026");
        sheet.Enter(CellAddress.Parse("B1"), "1234.5");
        sheet.SetFormat([CellAddress.Parse("B1")], NumberFormat.Parse("#,##0.00"));
        sheet.Enter(CellAddress.Parse("C1"), "$5");
        sheet.Enter(CellAddress.Parse("D1"), "a <b> & c");
        sheet.Enter(CellAddress.Parse("E1"), "7");
        sheet.SetFormat([CellAddress.Parse("E1")], NumberFormat.Parse("0 \"it's\""));
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));
        await GoToAsync(cut, "A1:E1");

        var payload = Grid(cut).Instance.BuildCopyPayload();

        Assert.Equal("9/26/2026\t1,234.50\t$5 \ta <b> & c\t7 it's\r\n", payload.Text);
        Assert.Equal(
            "<table data-ex-grid=\"invariant\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"><tr>"
            + "<td x:num=\"46291\" style='mso-number-format:\"m\\/d\\/yyyy\"'>46291</td>"
            + "<td x:num=\"1234.5\" style='mso-number-format:\"\\#\\,\\#\\#0\\.00\"'>1234.5</td>"
            + "<td x:num=\"5\" style='mso-number-format:\"\\$\\#\\,\\#\\#0_\\)\\;\\[Red\\]\\(\\$\\#\\,\\#\\#0\\)\"'>5</td>"
            + "<td>a &lt;b&gt; &amp; c</td>"
            + "<td x:num=\"7\" style='mso-number-format:\"0 \\0022it\\0027s\\0022\"'>7</td>"
            + "</tr></table>",
            payload.Html);

        // The grid's own paste parse reads it back as invariant fields, the x:num Values, so a
        // paste back into this Sheet is still recognised as this copy.
        var parsed = ExGrid.Clipboard.ClipboardParse.ParseBlock(payload.Html, payload.Text)!;
        Assert.Equal(["46291", "1234.5", "5", "a <b> & c", "7"], parsed.Values[0]);
        Assert.All(parsed.Origins[0], o => Assert.Equal(ExGrid.Clipboard.PasteFieldOrigin.Invariant, o));
        Assert.True(cut.Instance.OwnCopy!.IsPastedAs(parsed.Values));
    }

    [Fact] // ADR-0050 item 9, ADR-0005: a copy running beyond the Window is answered by the engine on the synchronous route
    public async Task A_copy_beyond_the_window_is_answered_by_the_engine()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "first"), ("A1000", "last"))));
        await GoToAsync(cut, "A1:A1000");

        var payload = Grid(cut).Instance.BuildCopyPayload();

        Assert.Equal("data", payload.Kind);
        var lines = payload.Text!.Split("\r\n");
        Assert.Equal("first", lines[0]);
        Assert.Equal("last", lines[999]);
    }

    [Fact] // ADR-0050 item 9, DC-32: the menu's asynchronous route asks the same answer
    public async Task The_asynchronous_copy_route_writes_the_engines_copy()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "50%"), ("A2", "=1/3"))));
        await GoToAsync(cut, "A1:A2");

        var grid = Grid(cut);
        var payload = await grid.InvokeAsync(() => grid.Instance.BuildCopyPayloadAsync());

        Assert.Equal("50%\r\n0.333333333333333\r\n", payload!.Text);
        Assert.Equal("<table data-ex-grid=\"invariant\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"><tr><td x:num=\"0.5\" style='mso-number-format:\"0%\"'>0.5</td></tr><tr><td x:num=\"0.3333333333333333\">0.3333333333333333</td></tr></table>", payload.Html);
    }

    [Fact] // ADR-0048, ADR-0050 item 9: a copy keeps its Entries, and the fields the grid's paste would read back from it
    public async Task A_copy_keeps_its_entries_and_the_fields_it_wrote()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "2"), ("B1", "=A1*2"))));
        await GoToAsync(cut, "A1:B1");

        Grid(cut).Instance.BuildCopyPayload();

        var own = cut.Instance.OwnCopy!;
        Assert.Equal(CellRange.Parse("A1:B1"), own.Block.Source);
        Assert.Equal("=A1*2", own.Block.EntryAt(0, 1)!.Formula);
        Assert.Equal(["2", "4"], Assert.Single(own.Fields));
    }

    [Fact] // ADR-0049, SH-16, DC-32: a copy reaching a waiting cell is refused in the engine's words, and nothing is written
    public async Task A_copy_reaching_getting_data_is_refused_in_the_engines_words()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "=SUM(Positions[PV])"))));
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["PV"]);
        await GoToAsync(cut, "A1:A2");

        var payload = Grid(cut).Instance.BuildCopyPayload();

        Assert.Equal("none", payload.Kind);
        cut.WaitForAssertion(() => Assert.Contains("A2 is waiting for a Linked Table's data (#GETTING_DATA)", Notice(cut)));
        Assert.Null(cut.Instance.OwnCopy);
    }

    [Fact] // ADR-0049: a refused copy leaves the last copy's Entries standing, as the clipboard still holds it
    public async Task A_refused_copy_keeps_the_last_copy()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "=SUM(Positions[PV])"))));
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["PV"]);
        await GoToAsync(cut, "A1");
        Grid(cut).Instance.BuildCopyPayload();
        await GoToAsync(cut, "A2");

        Assert.Equal("none", Grid(cut).Instance.BuildCopyPayload().Kind);

        Assert.Equal(CellRange.Parse("A1"), cut.Instance.OwnCopy!.Block.Source);
    }

    [Fact] // ADR-0005, ADR-0016: a number no format can show goes out as its Value, never as ####
    public async Task A_number_that_cannot_be_shown_goes_out_as_its_value()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1"), "-5");
        sheet.SetFormat([CellAddress.Parse("A1")], NumberFormat.Parse("yyyy-mm-dd"));
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));
        await GoToAsync(cut, "A1");

        var payload = Grid(cut).Instance.BuildCopyPayload();

        Assert.Equal("-5\r\n", payload.Text);
        Assert.DoesNotContain("#", payload.Html!, StringComparison.Ordinal);
    }

    [Fact] // ADR-0005, ADR-0048: ranges combined into one block carry their Values, and no Entries
    public async Task Several_ranges_carry_values_and_no_entries()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("C1", "=A1+2"))));
        await GoToAsync(cut, "A1");
        await CtrlClickAsync(cut, "C1");

        var payload = Grid(cut).Instance.BuildCopyPayload();

        Assert.Equal("1\t3\r\n", payload.Text);
        Assert.Equal("<table data-ex-grid=\"invariant\"><tr><td>1</td><td>3</td></tr></table>", payload.Html);
        Assert.Null(cut.Instance.OwnCopy);
    }

    private static async Task CopyAndPasteAsync(IRenderedComponent<ExSheet> cut, string from, string to)
    {
        await GoToAsync(cut, from);
        var payload = Grid(cut).Instance.BuildCopyPayload();
        await GoToAsync(cut, to);
        await PasteAsync(cut, payload.Text!, payload.Html);
    }

    private static SheetDocument GermanDocument() => new Sheet(CultureInfo.GetCultureInfo("de-DE")).ToDocument();

    [Fact] // ADR-0048, SH-14: inside ExSheet a copy carries Entries, and relative References shift by the distance pasted
    public async Task Copying_a_formula_from_B1_to_B2_pastes_the_shifted_formula()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "5"), ("B1", "=A1"))));

        await CopyAndPasteAsync(cut, "B1", "B2");

        Assert.Equal("5", CellText(cut, "B2"));
        await GoToAsync(cut, "B2");
        Assert.Equal("=A2", FormulaBar(cut));
    }

    [Fact] // ADR-0048, ADR-0050 item 3: the own copy spills from one cell, with its formats, and the block is selected
    public async Task The_own_copy_spills_with_its_entries_and_formats()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1"), "0.25");
        sheet.SetFormat([CellAddress.Parse("A1")], NumberFormat.Parse("0%"));
        sheet.Enter(CellAddress.Parse("B1"), "=A1*2");
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()).Add(s => s.SelectionChanged, selections.Add));

        await CopyAndPasteAsync(cut, "A1:B1", "C3");

        Assert.Equal("25%", CellText(cut, "C3"));
        Assert.Equal(new SelectionRange(2, 2, 1, 2), Assert.Single(selections[^1].Ranges));
        await GoToAsync(cut, "D3");
        Assert.Equal("=C3*2", FormulaBar(cut));
    }

    [Fact] // ADR-0048, ADR-0014: the own copy repeats over a range that is a whole multiple of it, shifted for each place
    public async Task The_own_copy_repeats_over_a_whole_multiple()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "2"), ("A3", "3"), ("B1", "=A1*10"))));

        await CopyAndPasteAsync(cut, "B1", "B2:B3");

        Assert.Equal("20", CellText(cut, "B2"));
        Assert.Equal("30", CellText(cut, "B3"));
    }

    [Fact] // ADR-0048, ADR-0014: one copied cell over several ranges is one paste, undone by one Ctrl+Z
    public async Task The_own_copy_over_several_ranges_is_one_undo_step()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "2"), ("C2", "3"), ("B1", "=A1"))));
        await GoToAsync(cut, "B1");
        var payload = Grid(cut).Instance.BuildCopyPayload();
        await GoToAsync(cut, "B2");
        await CtrlClickAsync(cut, "D2");

        await PasteAsync(cut, payload.Text!, payload.Html);

        Assert.Equal("2", CellText(cut, "B2"));
        Assert.Equal("3", CellText(cut, "D2"));
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("", CellText(cut, "B2"));
        Assert.Equal("", CellText(cut, "D2"));
        Assert.False(cut.Instance.CanUndo);
        Assert.True(await cut.Instance.RedoAsync());
        Assert.Equal("3", CellText(cut, "D2"));
    }

    [Fact] // ADR-0048: the own copy's paste is one undo step
    public async Task The_own_copys_paste_is_one_undo_step()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("B1", "=A1"), ("B2", "old"))));

        await CopyAndPasteAsync(cut, "A1:B1", "A2");
        Assert.True(await cut.Instance.UndoAsync());

        Assert.Equal("old", CellText(cut, "B2"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0050 item 9: a block that differs from the last copy by one field is another program's, and is taken as typed
    public async Task A_block_that_differs_from_the_own_copy_is_taken_as_typed()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "=2+3"))));
        await GoToAsync(cut, "A1");
        Grid(cut).Instance.BuildCopyPayload();
        await GoToAsync(cut, "A2");

        await PasteAsync(cut, "6\r\n");

        Assert.Equal("6", CellText(cut, "A2"));
        await GoToAsync(cut, "A2");
        Assert.Equal("6", FormulaBar(cut));
    }

    [Fact] // ADR-0050 item 10, SH-14: Excel's x:num is invariant, so 1234.5 stays 1234.5 under de-DE
    public async Task Excels_invariant_number_is_read_as_that_number_under_de_DE()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, GermanDocument()));
        await GoToAsync(cut, "A1");

        await PasteAsync(cut, "1.234,50\r\n", "<table><tr><td x:num=\"1234.5\">1.234,50</td></tr></table>");

        Assert.Equal("1234,5", CellText(cut, "A1"));
    }

    [Fact] // ADR-0048, ADR-0050 item 10: an invariant number keeps every digit its double holds, not the fifteen a typed number keeps
    public async Task An_invariant_number_keeps_every_digit_of_its_double()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "A1");

        await PasteAsync(cut, "0.123456789012346\r\n", "<table><tr><td x:num=\"0.12345678901234567\">0.123456789012346</td></tr></table>");

        var expected = double.Parse("0.12345678901234567", CultureInfo.InvariantCulture);
        Assert.NotEqual(double.Parse("0.123456789012346", CultureInfo.InvariantCulture), expected);
        Assert.Equal(expected, NumberAt(cut, "A1"));
    }

    [Fact] // ADR-0048: typed fields and exact numbers pasted together are one undo step
    public async Task Typed_fields_and_exact_numbers_are_one_step()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "old"), ("B1", "old"), ("C1", "old"))));
        await GoToAsync(cut, "A1");

        await PasteAsync(cut, "", "<table><tr><td x:num=\"1.0000000000000002\">1</td><td>=A1*2</td><td>50%</td></tr></table>");

        Assert.Equal(1.0000000000000002, NumberAt(cut, "A1"));
        Assert.Equal("2", CellText(cut, "B1"));
        Assert.Equal("50%", CellText(cut, "C1"));
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("old", CellText(cut, "A1"));
        Assert.Equal("old", CellText(cut, "B1"));
        Assert.Equal("old", CellText(cut, "C1"));
        Assert.False(cut.Instance.CanUndo);
    }

    private static double? NumberAt(IRenderedComponent<ExSheet> cut, string address) =>
        cut.Instance.ToDocument().Cells.Single(c => c.Address == CellAddress.Parse(address)).Entry?.Constant?.Number;

    [Fact] // ADR-0050 item 10: shown text is read as typed under the Sheet's culture
    public async Task Shown_text_is_read_as_typed_under_de_DE()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, GermanDocument()));
        await GoToAsync(cut, "A1");

        await PasteAsync(cut, "1.234,5\r\n");

        Assert.Equal("1.234,50", CellText(cut, "A1")); // thousands separators with decimals give #,##0.00 (TYPED-008)
    }

    [Fact] // ADR-0050 item 10, ADR-0048: another Sheet's copy is invariant, so an en-US Sheet's 0.5 is 0.5 in a de-DE one
    public async Task Another_sheets_copy_keeps_its_numbers_across_cultures()
    {
        var source = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "0.5"), ("B1", "1234.5"))));
        await GoToAsync(source, "A1:B1");
        var payload = Grid(source).Instance.BuildCopyPayload();
        var target = RenderSheet(ps => ps.Add(s => s.Document, GermanDocument()));
        await GoToAsync(target, "A1");

        await PasteAsync(target, payload.Text!, payload.Html);

        Assert.Equal("0,5", CellText(target, "A1"));
        Assert.Equal("1234,5", CellText(target, "B1"));
    }

    [Fact] // ADR-0050 item 10: an invariant ISO date is that date, and TRUE a boolean, in any culture
    public async Task Invariant_dates_and_booleans_are_read_as_themselves()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, GermanDocument()));
        await GoToAsync(cut, "A1");

        const string marker = global::ExGrid.Clipboard.ClipboardData.InvariantMarker;
        await PasteAsync(cut, "", $"<table {marker}><tr><td>2026-09-26</td><td>TRUE</td><td>text</td></tr></table>");

        await GoToAsync(cut, "A1");
        Assert.Equal("26.09.2026", FormulaBar(cut));
        Assert.Equal("TRUE", CellText(cut, "B1"));
        Assert.Equal("text", CellText(cut, "C1"));
    }

    private static async Task CtrlClickAsync(IRenderedComponent<ExSheet> cut, string address)
    {
        var at = CellAddress.Parse(address);
        var heading = double.Parse(cut.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(), CultureInfo.InvariantCulture);
        await cut.Find(".ex-viewport").MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs
        {
            Button = 0, Buttons = 1, CtrlKey = true,
            OffsetX = heading + at.Column * SheetColumns.DefaultWidthPx + 5,
            OffsetY = at.Row * ExSheet.DefaultRowHeightPx + 5,
        });
        await cut.Find(".ex-viewport").MouseUpAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { Button = 0 });
    }
}
