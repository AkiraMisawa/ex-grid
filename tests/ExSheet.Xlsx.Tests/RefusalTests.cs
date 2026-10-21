using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>What is refused by name, naming the Sheet, the cell and what it held, so that nothing opens (ADR-0330 §4).</summary>
public class RefusalTests
{
    private static async Task<XlsxOpenException> RefusedFormulaAsync(string stored, XlsxWriteOptions? options = null)
    {
        var file = await WriteAsync(options, new Page("Trades", [Number("A1", "1"), Formula("B7", stored)]));
        return await RefusedAsync(file);
    }

    [Theory] // ADR-0330 §4: a Formula the engine cannot read is refused by name
    [InlineData("SUM({1,2,3})", "=SUM({1,2,3})")]
    [InlineData("SUM((A1,A1))", "=SUM((A1,A1))")]
    [InlineData("A1:A2 A1:B1", "=A1:A2 A1:B1")]
    public async Task ADR0330_a_Formula_the_engine_cannot_read_is_refused(string stored, string held)
    {
        var refused = await RefusedFormulaAsync(stored);

        Assert.Equal(XlsxOpenRefusal.UnreadableFormula, refused.Reason);
        Assert.Equal("Trades", refused.Sheet);
        Assert.Equal("B7", refused.Cell);
        Assert.Equal(held, refused.Held);
        Assert.StartsWith($"Sheet 'Trades', cell B7 ({held}): The engine cannot read the Formula", refused.Message);
    }

    [Fact] // ADR-0330 §4: the implicit intersection operator (_xlfn.SINGLE, shown as @) is refused, not read as an unknown function
    public async Task ADR0330_implicit_intersection_is_refused()
    {
        var refused = await RefusedFormulaAsync("_xlfn.SINGLE(A1:A3)*2");

        Assert.Equal(XlsxOpenRefusal.UnreadableFormula, refused.Reason);
        Assert.Equal("B7", refused.Cell);
        Assert.Contains("implicit intersection", refused.Message);
    }

    [Fact] // ADR-0330 §4: a What-If data table is refused
    public async Task ADR0330_a_data_table_is_refused()
    {
        var file = await WriteAsync(new Page("Trades",
            [Cell("C3", XlsxCellType.Number, "1") with { Formula = new XlsxFormula("TABLE(,A1)", XlsxFormulaKind.DataTable, "C3:C5") }]));

        var refused = await RefusedAsync(file);

        Assert.Equal(XlsxOpenRefusal.UnreadableFormula, refused.Reason);
        Assert.Equal(("Trades", "C3"), (refused.Sheet, refused.Cell));
    }

    [Fact] // ADR-0330 §4: a reference to a defined name is refused: ExSheet defines none, and would show #NAME?
    public async Task ADR0330_a_reference_to_a_defined_name_is_refused()
    {
        var refused = await RefusedFormulaAsync("Rate*A1", new XlsxWriteOptions { DefinedNames = [new XlsxDefinedName("Rate", "Trades!$A$1")] });

        Assert.Equal(XlsxOpenRefusal.DefinedName, refused.Reason);
        Assert.Equal(("Trades", "B7", "=Rate*A1"), (refused.Sheet, refused.Cell, refused.Held));
        Assert.Contains("'Rate'", refused.Message);
    }

    [Fact] // ADR-0330 §4: a defined name called as a function (a LAMBDA kept in Name Manager) is refused as a defined name
    public async Task ADR0330_a_defined_name_called_as_a_function_is_refused()
    {
        var refused = await RefusedFormulaAsync("Double(A1)", new XlsxWriteOptions { DefinedNames = [new XlsxDefinedName("Double", "_xlfn.LAMBDA(_xlpm.x,_xlpm.x*2)")] });

        Assert.Equal(XlsxOpenRefusal.DefinedName, refused.Reason);
        Assert.Contains("'Double'", refused.Message);
    }

    [Theory] // ADR-0330 §4: a reference to another workbook is refused, however it is written
    [InlineData("[1]Sheet1!A1*2")]
    [InlineData("SUM('[1]Other Sheet'!A1:A3)")]
    [InlineData("[1]!Rate")]
    public async Task ADR0330_a_reference_to_another_workbook_is_refused(string stored)
    {
        var refused = await RefusedFormulaAsync(stored);

        Assert.Equal(XlsxOpenRefusal.OtherWorkbook, refused.Reason);
        Assert.Equal(("Trades", "B7", "=" + stored), (refused.Sheet, refused.Cell, refused.Held));
    }

    [Theory] // ADR-0330 §4, §6: a reference to an Excel Table is refused, in every structured form
    [InlineData("SUM(Positions[PV])")]
    [InlineData("Positions[[#This Row],[PV]]*2")]
    [InlineData("SUM(Positions[[PV]:[Delta]])")]
    public async Task ADR0330_a_reference_to_an_Excel_Table_is_refused(string stored)
    {
        var refused = await RefusedFormulaAsync(stored);

        Assert.Equal(XlsxOpenRefusal.ExcelTable, refused.Reason);
        Assert.Equal(("Trades", "B7"), (refused.Sheet, refused.Cell));
    }

    [Fact] // ADR-0330 §4: a name the workbook does not define is refused where it holds Excel Tables, which a bare name can read
    public async Task ADR0330_an_undefined_name_is_refused_where_the_workbook_holds_Excel_Tables()
    {
        // A2's LET binds its own names, which are no table's: only B7 is refused.
        var file = await WriteAsync(new Page("Trades", [Number("A1", "1"), Formula("A2", "_xlfn.LET(_xlpm.x,A1,_xlpm.x*2)"), Formula("B7", "SUM(Positions)")]));
        var withTable = Add(file, "xl/tables/table1.xml", """<?xml version="1.0" encoding="UTF-8"?><table xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" id="1" name="Positions" displayName="Positions" ref="A1:A2"/>""");

        var refused = await RefusedAsync(withTable);
        var (document, _) = await ReadAsync(file);

        Assert.Equal(XlsxOpenRefusal.ExcelTable, refused.Reason);
        Assert.Equal(("Trades", "B7", "=SUM(Positions)"), (refused.Sheet, refused.Cell, refused.Held));
        // With no Excel Table and no defined name, the name is #NAME? in Excel too, and is carried.
        Assert.Equal("=SUM(Positions)", document.Sheet("Trades").EntryAt("B7")!.Formula);
    }

    [Fact] // ADR-0330 §4: a LAMBDA's parameters (stored _xlpm.) are no names, so a workbook holding Excel Tables still opens a MAP over a LAMBDA
    public async Task ADR0330_LAMBDA_parameters_are_not_refused_where_the_workbook_holds_Excel_Tables()
    {
        var file = await WriteAsync(new Page("Trades",
            [Number("A1", "1"), Number("A2", "2"), Formula("B1", "_xlfn.MAP(A1:A2,_xlfn.LAMBDA(_xlpm.v,_xlpm.v*2))")]));
        var withTable = Add(file, "xl/tables/table1.xml", """<?xml version="1.0" encoding="UTF-8"?><table xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" id="1" name="v" displayName="v" ref="A1:A2"/>""");

        var (document, _) = await ReadAsync(withTable);

        Assert.Equal("=MAP(A1:A2,LAMBDA(v,v*2))", document.Sheet("Trades").EntryAt("B1")!.Formula);
    }

    [Fact] // ADR-0330 §4: a legacy array formula (Ctrl+Shift+Enter) is refused, its braces shown
    public async Task ADR0330_a_legacy_array_formula_is_refused()
    {
        var file = await WriteAsync(new Page("Trades",
        [
            Number("A1", "1"), Number("A2", "2"),
            Cell("B1", XlsxCellType.Number, "2") with { Formula = new XlsxFormula("A1:A2*2", XlsxFormulaKind.Array, "B1:B2") },
            Number("B2", "4"),
        ]));

        var refused = await RefusedAsync(file);

        Assert.Equal(XlsxOpenRefusal.LegacyArrayFormula, refused.Reason);
        Assert.Equal(("Trades", "B1", "{=A1:A2*2}"), (refused.Sheet, refused.Cell, refused.Held));
    }

    [Fact] // ADR-0330 §4: a one-cell legacy array formula is refused too: Excel shows its first value, ExSheet would spill
    public async Task ADR0330_a_one_cell_legacy_array_formula_is_refused()
    {
        var file = await WriteAsync(new Page("Trades",
            [Cell("B1", XlsxCellType.Number, "2") with { Formula = new XlsxFormula("SUM(A1:A2*2)", XlsxFormulaKind.Array, "B1") }]));

        Assert.Equal(XlsxOpenRefusal.LegacyArrayFormula, (await RefusedAsync(file)).Reason);
    }

    [Fact] // ADR-0330 §4: a refusal in a shared formula's master names its first cell
    public async Task ADR0330_a_refused_shared_formula_names_its_first_cell()
    {
        var file = await WriteAsync(new Page("Trades",
        [
            Formula("C2", "") with { Formula = new XlsxFormula("Positions[PV]", XlsxFormulaKind.Shared, "C2:C3", 0) },
            Formula("C3", "") with { Formula = new XlsxFormula("", XlsxFormulaKind.Shared, SharedIndex: 0) },
        ]));

        var refused = await RefusedAsync(file);

        Assert.Equal(("Trades", "C2", XlsxOpenRefusal.ExcelTable), (refused.Sheet, refused.Cell, refused.Reason));
    }

    [Theory] // ADR-0330 §4: an error cell holds one of Excel's Error Values as written; anything else — ExSheet's own, a Formula, a number — is refused, not read
    [InlineData("#GETTING_DATA")]
    [InlineData("#CIRC!")]
    [InlineData("=SUM(1)")]
    [InlineData("=SUM(")]
    [InlineData("1")]
    [InlineData("#n/a")]
    public async Task ADR0330_an_Error_Value_ExSheet_cannot_hold_is_refused(string held)
    {
        var file = await WriteAsync(new Page("Trades", [Error("D4", held)]));

        var refused = await RefusedAsync(file);

        Assert.Equal(XlsxOpenRefusal.UnreadableConstant, refused.Reason);
        Assert.Equal(("Trades", "D4", held), (refused.Sheet, refused.Cell, refused.Held));
    }

    [Theory] // ADR-0330 §4: each of Excel's Error Values is carried from an error cell
    [InlineData("#NULL!", ErrorValue.Null)]
    [InlineData("#REF!", ErrorValue.Ref)]
    [InlineData("#NAME?", ErrorValue.Name)]
    [InlineData("#NUM!", ErrorValue.Num)]
    [InlineData("#VALUE!", ErrorValue.Value)]
    [InlineData("#SPILL!", ErrorValue.Spill)]
    [InlineData("#CALC!", ErrorValue.Calc)]
    public async Task ADR0330_Excels_Error_Values_are_carried(string held, ErrorValue error)
    {
        var file = await WriteAsync(new Page("Trades", [Error("D4", held)]));

        var document = (await ReadAsync(file)).Document;

        Assert.Equal(Value.FromError(error), document.Sheet("Trades").EntryAt("D4")!.Constant);
    }

    [Fact] // ADR-0330 §4: what the file layer refuses is refused here, its reason kept
    public async Task ADR0330_what_the_file_layer_refuses_is_refused()
    {
        var refused = await RefusedAsync("not a workbook"u8.ToArray());

        Assert.Equal(XlsxOpenRefusal.File, refused.Reason);
        Assert.Equal(XlsxRefusal.NotAZip, refused.FileRefusal);
        Assert.IsType<XlsxException>(refused.InnerException);
        Assert.Equal(refused.InnerException!.Message, refused.Message);
    }

    [Fact] // ADR-0330 §4, ADR-0331 §4: two worksheets whose names differ only in case are refused by name, both named
    public async Task ADR0330_worksheet_names_differing_only_in_case_are_refused()
    {
        var file = await WriteAsync(new Page("Trades", [Number("A1", "1")]), new Page("Other", [Number("A1", "2")]));
        var renamed = Edit(file, "xl/workbook.xml", xml => xml.Replace("name=\"Other\"", "name=\"TRADES\"", StringComparison.Ordinal));

        var refused = await RefusedAsync(renamed);

        Assert.Equal(XlsxOpenRefusal.Sheets, refused.Reason);
        Assert.Equal("TRADES", refused.Sheet);
        Assert.Contains("'Trades'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("'TRADES'", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0330 §4, ADR-0331 §4: a worksheet named as no Sheet can be is refused by name
    public async Task ADR0330_a_worksheet_name_no_Sheet_can_have_is_refused()
    {
        var file = await WriteAsync(new Page("Sheet1", [Number("A1", "1")]));
        var renamed = Edit(file, "xl/workbook.xml", xml => xml.Replace("name=\"Sheet1\"", "name=\"History\"", StringComparison.Ordinal));

        var refused = await RefusedAsync(renamed);

        Assert.Equal(XlsxOpenRefusal.Sheets, refused.Reason);
        Assert.Equal("History", refused.Sheet);
    }
}
