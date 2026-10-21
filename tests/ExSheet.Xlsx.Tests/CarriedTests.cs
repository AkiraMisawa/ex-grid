using System.Globalization;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>What a workbook carries into its Workbook Document (ADR-0330 §3, §4 "Carried", §7).</summary>
public class CarriedTests
{
    [Fact] // ADR-0330 §3: every worksheet a Sheet, in the file's order, the one shown shown
    public async Task ADR0330_every_worksheet_is_a_Sheet_in_order_and_the_shown_one_is_shown()
    {
        var file = await WriteAsync(new XlsxWriteOptions { ActiveSheet = 1 },
            new Page("Trades", [Number("A1", "1")]), new Page("Desks", [Number("A1", "2")]), new Page("Notes", [Text("A1", "n")]));

        var (document, _) = await ReadAsync(file);

        Assert.Equal(["Trades", "Desks", "Notes"], document.Sheets.Select(s => s.Name));
        Assert.Equal("Desks", document.Shown);
        Assert.Equal(WorkbookDocument.CurrentVersion, document.Version);
        Assert.Equal("Desks", Workbook.Open(document).Shown.Name);
    }

    [Fact] // ADR-0330 §4: numbers, text (shared and inline), booleans and Error Values are carried as constants
    public async Task ADR0330_constants_of_every_kind_are_carried()
    {
        var file = await WriteAsync(new Page("Sheet1",
        [
            Number("A1", "1234.5"), Text("B1", "Notional"), Inline("C1", "inline text"), Boolean("D1", true), Boolean("E1", false),
            Error("F1", "#N/A"), Error("G1", "#DIV/0!"), Number("H1", "-0.000125"), Number("I1", "1E+300"), Text("J1", "=not a formula"),
        ]));

        var sheet = (await ReadAsync(file)).Document.Sheet("Sheet1");

        Assert.Equal(Value.FromNumber(1234.5), sheet.EntryAt("A1")!.Constant);
        Assert.Equal(Value.FromText("Notional"), sheet.EntryAt("B1")!.Constant);
        Assert.Equal(Value.FromText("inline text"), sheet.EntryAt("C1")!.Constant);
        Assert.Equal(Value.FromBoolean(true), sheet.EntryAt("D1")!.Constant);
        Assert.Equal(Value.FromBoolean(false), sheet.EntryAt("E1")!.Constant);
        Assert.Equal(Value.FromError(ErrorValue.NA), sheet.EntryAt("F1")!.Constant);
        Assert.Equal(Value.FromError(ErrorValue.Div0), sheet.EntryAt("G1")!.Constant);
        Assert.Equal(Value.FromNumber(-0.000125), sheet.EntryAt("H1")!.Constant);
        Assert.Equal(Value.FromNumber(1e300), sheet.EntryAt("I1")!.Constant);
        Assert.Equal(Value.FromText("=not a formula"), sheet.EntryAt("J1")!.Constant);
    }

    [Fact] // ADR-0330 §4: a cell that holds only a style holds no Entry
    public async Task ADR0330_a_cell_with_a_style_only_holds_nothing()
    {
        var file = await WriteAsync(new Page("Sheet1", [Number("A1", "1"), Cell("B1", XlsxCellType.Number, null)]));

        var sheet = (await ReadAsync(file)).Document.Sheet("Sheet1");

        Assert.Single(sheet.Cells);
    }

    [Fact] // ADR-0330 §4, §6: a date written in ISO 8601 (t="d") is its 1900 serial, its time a fraction of a day
    public async Task ADR0330_an_ISO_date_cell_is_its_serial()
    {
        var file = await WriteAsync(new Page("Sheet1",
            [Cell("A1", XlsxCellType.Date, "2023-03-15T12:00:00"), Cell("A2", XlsxCellType.Date, "1900-01-01"), Cell("A3", XlsxCellType.Date, "1900-03-01")]));

        var sheet = (await ReadAsync(file)).Document.Sheet("Sheet1");

        Assert.Equal(Value.FromNumber(45000.5), sheet.EntryAt("A1")!.Constant);
        Assert.Equal(Value.FromNumber(1), sheet.EntryAt("A2")!.Constant);
        Assert.Equal(Value.FromNumber(61), sheet.EntryAt("A3")!.Constant);
    }

    [Fact] // ADR-0330 §4: a date cell is read invariantly: a time alone is its fraction of a day, not today's, and an offset converts nothing
    public async Task ADR0330_an_ISO_date_cell_depends_on_no_clock_or_zone()
    {
        var file = await WriteAsync(new Page("Sheet1",
            [Cell("A1", XlsxCellType.Date, "18:00:00"), Cell("A2", XlsxCellType.Date, "2023-03-15T12:00:00Z"), Cell("A3", XlsxCellType.Date, "2023-03-15T23:00:00-05:00")]));

        var sheet = (await ReadAsync(file)).Document.Sheet("Sheet1");

        Assert.Equal(Value.FromNumber(0.75), sheet.EntryAt("A1")!.Constant);
        Assert.Equal(Value.FromNumber(45000.5), sheet.EntryAt("A2")!.Constant);
        Assert.Equal(Value.FromNumber(45000 + 23.0 / 24), sheet.EntryAt("A3")!.Constant);
    }

    [Fact] // ADR-0330 §4: a date cell that is not ISO 8601 is refused, not read by the server's culture
    public async Task ADR0330_a_date_cell_that_is_not_ISO_is_refused()
    {
        var file = await WriteAsync(new Page("Sheet1", [Cell("A1", XlsxCellType.Date, "15/03/2023")]));

        var refused = await RefusedAsync(file);

        Assert.Equal(XlsxOpenRefusal.UnreadableConstant, refused.Reason);
        Assert.Equal(("Sheet1", "A1", "15/03/2023"), (refused.Sheet, refused.Cell, refused.Held));
    }

    [Fact] // ADR-0330 §4, ADR-0125: Spill Ranges behind the reader are passed, and a constant below or beside one is carried
    public async Task ADR0330_constants_past_a_spill_are_carried()
    {
        var file = await WriteAsync(new Page("Sheet1",
        [
            Cell("A1", XlsxCellType.Number, "1") with { Formula = new XlsxFormula("_xlfn.SEQUENCE(2)", XlsxFormulaKind.Array, "A1:A2", IsDynamicArray: true) },
            Number("A2", "2"), Number("A3", "30"),
            Cell("B3", XlsxCellType.Number, "1") with { Formula = new XlsxFormula("_xlfn.SEQUENCE(2)", XlsxFormulaKind.Array, "B3:B4", IsDynamicArray: true) },
            Number("B4", "2"), Number("C4", "40"), Number("B5", "50"),
        ]));

        var sheet = (await ReadAsync(file)).Document.Sheet("Sheet1");

        Assert.Null(sheet.EntryAt("A2"));
        Assert.Null(sheet.EntryAt("B4"));
        Assert.Equal(Value.FromNumber(30), sheet.EntryAt("A3")!.Constant);
        Assert.Equal(Value.FromNumber(40), sheet.EntryAt("C4")!.Constant);
        Assert.Equal(Value.FromNumber(50), sheet.EntryAt("B5")!.Constant);
    }

    [Fact] // ADR-0330 §4, §6: Formulas are carried, never the Value the file stored: opening computes every Value again
    public async Task ADR0330_Formulas_are_carried_and_every_Value_computed_again()
    {
        var file = await WriteAsync(new Page("Sheet1",
            [Number("A1", "2"), Number("A2", "3"), Formula("A3", "SUM(A1:A2)*10", cached: "-1"), Formula("B1", "Desks!A1+1")]),
            new Page("Desks", [Number("A1", "41")]));

        var document = (await ReadAsync(file)).Document;
        var workbook = Workbook.Open(document);

        Assert.Equal("=SUM(A1:A2)*10", document.Sheet("Sheet1").EntryAt("A3")!.Formula);
        Assert.Equal(Value.FromNumber(50), workbook.Sheets[0].GetValue(CellAddress.Parse("A3")));
        Assert.Equal(Value.FromNumber(42), workbook.Sheets[0].GetValue(CellAddress.Parse("B1")));
    }

    [Fact] // ADR-0330 §4: a Formula whose result is text (t="str") is carried as its Formula, not its text
    public async Task ADR0330_a_Formula_with_a_text_result_is_carried_as_the_Formula()
    {
        var file = await WriteAsync(new Page("Sheet1",
            [Text("A1", "a"), Cell("B1", XlsxCellType.FormulaString, "stale") with { Formula = new XlsxFormula("A1&\"b\"") }]));

        var document = (await ReadAsync(file)).Document;

        Assert.Equal("=A1&\"b\"", document.Sheet("Sheet1").EntryAt("B1")!.Formula);
        Assert.Equal(Value.FromText("ab"), Workbook.Open(document).Sheets[0].GetValue(CellAddress.Parse("B1")));
    }

    [Fact] // ADR-0330 §4: Excel's stored _xlfn., _xlws. and _xlpm. prefixes are taken off, outside quotes only
    public async Task ADR0330_stored_prefixes_are_taken_off()
    {
        var file = await WriteAsync(new Page("Sheet1",
        [
            Number("A1", "3"), Number("A2", "1"), Number("A3", "2"),
            Formula("B1", "_xlfn.XLOOKUP(2,A1:A3,A1:A3)"),
            Formula("B2", "_xlfn.CONCAT(\"_xlfn.\",A1)"),
            Formula("B3", "_xlfn.LET(_xlpm.x,A1,_xlpm.x*2)"),
            Formula("B4", "SUM(_xlfn._xlws.SORT(A1:A3))"),
        ]));

        var document = (await ReadAsync(file)).Document;
        var sheet = document.Sheet("Sheet1");
        var values = Workbook.Open(document).Sheets[0];

        Assert.Equal("=XLOOKUP(2,A1:A3,A1:A3)", sheet.EntryAt("B1")!.Formula);
        Assert.Equal("=CONCAT(\"_xlfn.\",A1)", sheet.EntryAt("B2")!.Formula);
        Assert.Equal("=LET(x,A1,x*2)", sheet.EntryAt("B3")!.Formula);
        Assert.Equal("=SUM(SORT(A1:A3))", sheet.EntryAt("B4")!.Formula);
        Assert.Equal(Value.FromNumber(2), values.GetValue(CellAddress.Parse("B1")));
        Assert.Equal(Value.FromText("_xlfn.3"), values.GetValue(CellAddress.Parse("B2")));
        Assert.Equal(Value.FromNumber(6), values.GetValue(CellAddress.Parse("B3")));
        Assert.Equal(Value.FromNumber(6), values.GetValue(CellAddress.Parse("B4")));
    }

    [Fact] // ADR-0330 §4: a shared formula is expanded into each cell's own, its relative references moved as Excel moves them
    public async Task ADR0330_shared_formulas_are_expanded_with_relative_references_moved()
    {
        var file = await WriteAsync(new Page("Sheet1",
        [
            Number("A1", "1"), Formula("B1", "") with { Formula = new XlsxFormula("A1*$A$1+A$1+$A1", XlsxFormulaKind.Shared, "B1:C3", 0) },
            Formula("C1", "") with { Formula = new XlsxFormula("", XlsxFormulaKind.Shared, SharedIndex: 0) },
            Number("A2", "2"), Formula("B2", "") with { Formula = new XlsxFormula("", XlsxFormulaKind.Shared, SharedIndex: 0) },
            Formula("C2", "") with { Formula = new XlsxFormula("", XlsxFormulaKind.Shared, SharedIndex: 0) },
            Number("A3", "3"), Formula("B3", "") with { Formula = new XlsxFormula("", XlsxFormulaKind.Shared, SharedIndex: 0) },
            Formula("C3", "") with { Formula = new XlsxFormula("SUM(A:A)", XlsxFormulaKind.Shared, "C3", 1) },
            Formula("D3", "") with { Formula = new XlsxFormula("", XlsxFormulaKind.Shared, SharedIndex: 1) },
        ]));

        var document = (await ReadAsync(file)).Document;
        var sheet = document.Sheet("Sheet1");

        Assert.Equal("=A1*$A$1+A$1+$A1", sheet.EntryAt("B1")!.Formula);
        Assert.Equal("=B1*$A$1+B$1+$A1", sheet.EntryAt("C1")!.Formula);
        Assert.Equal("=A2*$A$1+A$1+$A2", sheet.EntryAt("B2")!.Formula);
        Assert.Equal("=B2*$A$1+B$1+$A2", sheet.EntryAt("C2")!.Formula);
        Assert.Equal("=A3*$A$1+A$1+$A3", sheet.EntryAt("B3")!.Formula);
        Assert.Equal("=SUM(A:A)", sheet.EntryAt("C3")!.Formula);
        Assert.Equal("=SUM(B:B)", sheet.EntryAt("D3")!.Formula);
        Assert.Equal(Value.FromNumber(3 * 1 + 1 + 3), Workbook.Open(document).Sheets[0].GetValue(CellAddress.Parse("B3")));
    }

    [Fact] // ADR-0330 §4, ADR-0125: a spilled array is its anchor's Formula; the values the file stored for its spill are not carried
    public async Task ADR0330_a_spilled_array_is_its_anchors_Formula()
    {
        var file = await WriteAsync(new Page("Sheet1",
        [
            Cell("A1", XlsxCellType.Number, "1") with { Formula = new XlsxFormula("_xlfn.SEQUENCE(3)", XlsxFormulaKind.Array, "A1:A3", IsDynamicArray: true) },
            Formula("B1", "SUM(_xlfn.ANCHORARRAY(A1))"),
            Number("A2", "2"), Number("A3", "3"), Number("A4", "4"),
        ]));

        var document = (await ReadAsync(file)).Document;
        var sheet = document.Sheet("Sheet1");
        var values = Workbook.Open(document).Sheets[0];

        Assert.Equal("=SEQUENCE(3)", sheet.EntryAt("A1")!.Formula);
        Assert.Null(sheet.EntryAt("A2"));
        Assert.Null(sheet.EntryAt("A3"));
        Assert.Equal(Value.FromNumber(4), sheet.EntryAt("A4")!.Constant);
        Assert.Equal("=SUM(A1#)", sheet.EntryAt("B1")!.Formula);
        Assert.Equal(Value.FromNumber(3), values.GetValue(CellAddress.Parse("A3")));
        Assert.Equal(Value.FromNumber(6), values.GetValue(CellAddress.Parse("B1")));
    }

    [Fact] // ADR-0330 §7: the culture is the application's, and no Value read depends on it
    public async Task ADR0330_the_culture_is_the_applications_and_changes_no_Value()
    {
        var file = await WriteAsync(new Page("Sheet1", [Number("A1", "1234.5"), Formula("A2", "A1*2")]));

        var german = (await ReadAsync(file, CultureInfo.GetCultureInfo("de-DE"))).Document;
        var american = (await ReadAsync(file, CultureInfo.GetCultureInfo("en-US"))).Document;

        Assert.Equal("de-DE", german.Culture);
        Assert.Equal("en-US", american.Culture);
        Assert.Equal(american.Sheet("Sheet1").EntryAt("A1"), german.Sheet("Sheet1").EntryAt("A1"));
        var workbook = Workbook.Open(german);
        Assert.Equal("de-DE", workbook.Culture.Name);
        Assert.Equal(Value.FromNumber(2469), workbook.Sheets[0].GetValue(CellAddress.Parse("A2")));
        // Typing afterwards is read under the declared culture: 1,5 is one and a half in German.
        workbook.Do("Sheet1", SheetEdit.Enter(CellAddress.Parse("A1"), "1,5"));
        Assert.Equal(Value.FromNumber(3), workbook.Sheets[0].GetValue(CellAddress.Parse("A2")));
    }

    [Fact] // ADR-0330 §7: a culture the engine cannot name is refused before the file is read
    public async Task ADR0330_a_culture_the_engine_cannot_name_is_refused()
    {
        var file = await WriteAsync(new Page("Sheet1", [Number("A1", "1")]));

        var refused = await Assert.ThrowsAsync<ArgumentException>(async () => await ReadAsync(file, new CultureInfo("zz-ZZ")));

        Assert.Equal("culture", refused.ParamName);
    }

    [Fact] // ADR-0330 §9: the file layer's options pass through: progress is reported, and a cancelled read opens nothing
    public async Task ADR0330_progress_and_cancellation_pass_through()
    {
        var file = await WriteAsync(new Page("Sheet1", [.. Enumerable.Range(0, 5000).Select(i => Number($"A{i + 1}", i.ToString(CultureInfo.InvariantCulture)))]));
        var reports = new List<XlsxProgress>();

        await ReadAsync(file, options: new XlsxReadOptions { Progress = new Synchronous(reports.Add), SliceBudget = TimeSpan.Zero });
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        Assert.Contains(reports, r => r.Cells == 5000);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await XlsxWorkbookReader.ReadAsync(new MemoryStream(file), EnUs, null, cancelled.Token));
    }

    [Fact] // ADR-0330 §4: a hidden worksheet is a Sheet, shown; a chart sheet is not one
    public async Task ADR0330_a_hidden_worksheet_is_opened()
    {
        var file = await WriteAsync(new Page("Shown", [Number("A1", "1")]), new Page("Hidden", [Number("A1", "2")], Visibility: XlsxSheetVisibility.Hidden));

        var (document, report) = await ReadAsync(file);

        Assert.Equal(["Shown", "Hidden"], document.Sheets.Select(s => s.Name));
        Assert.Contains(report.NotCarried, n => n is { Kind: NotCarriedKind.HiddenSheet, Sheet: "Hidden" });
    }

    [Fact] // ADR-0330 §3: a sheet that is not a worksheet is not a Sheet, and is listed; when it was shown, the first Sheet is shown
    public async Task ADR0330_a_chart_sheet_is_not_a_Sheet()
    {
        var file = await WriteAsync(new Page("Trades", [Number("A1", "1")]), new Page("Desks", [Number("A1", "2")]));
        var withChart = Edit(file, "xl/workbook.xml", xml => xml
            .Replace("<sheets>", "<sheets><sheet name=\"Chart1\" sheetId=\"9\" r:id=\"rIdChart\" />", StringComparison.Ordinal));

        var (document, report) = await ReadAsync(withChart);

        Assert.Equal(["Trades", "Desks"], document.Sheets.Select(s => s.Name));
        Assert.Equal("Trades", document.Shown);
        Assert.Contains(report.NotCarried, n => n is { Kind: NotCarriedKind.ChartSheet, Sheet: "Chart1" });
    }

    [Fact] // ADR-0330 §3: a workbook with no worksheet is refused by name
    public async Task ADR0330_a_workbook_with_no_worksheet_is_refused()
    {
        var file = await WriteAsync(new Page("Trades", [Number("A1", "1")]));
        var chartOnly = Edit(file, "xl/workbook.xml", xml => System.Text.RegularExpressions.Regex.Replace(xml, "r:id=\"[^\"]+\"", "r:id=\"rIdChart\""));

        var refused = await RefusedAsync(chartOnly);

        Assert.Equal(XlsxOpenRefusal.Sheets, refused.Reason);
    }

    private sealed class Synchronous(Action<XlsxProgress> report) : IProgress<XlsxProgress>
    {
        public void Report(XlsxProgress value) => report(value);
    }
}
