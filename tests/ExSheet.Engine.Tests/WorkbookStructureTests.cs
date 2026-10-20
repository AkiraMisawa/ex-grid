using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class WorkbookStructureTests
{
    private static Workbook NewWorkbook(params string[] names) => new(EnUs, names);

    private static void Enter(Workbook workbook, string sheet, string address, string typed) =>
        workbook.Do(sheet, SheetEdit.Enter(CellAddress.Parse(address), typed));

    private static string? Formula(Sheet sheet, string address) => sheet.GetEntry(CellAddress.Parse(address))?.Formula;

    [Fact] // ADR-0331, ADR-0046: a row inserted on one Sheet keeps another Sheet's References naming the same cells
    public void An_insertion_rewrites_references_on_every_sheet()
    {
        var workbook = NewWorkbook("Rates", "Deposit");
        Enter(workbook, "Rates", "A2", "4");
        Enter(workbook, "Rates", "A3", "6");
        Enter(workbook, "Deposit", "A1", "=Rates!A2*Rates!$A$3");
        Enter(workbook, "Deposit", "A2", "=SUM(Rates!A1:A3)");
        Enter(workbook, "Deposit", "A3", "=Rates!A1+A1");

        var step = workbook.Do("Rates", SheetEdit.InsertRows(1));

        var deposit = workbook.Sheets[1];
        Assert.Equal("=Rates!A3*Rates!$A$4", Formula(deposit, "A1"));
        Assert.Equal("=SUM(Rates!A1:A4)", Formula(deposit, "A2"));
        Assert.Equal("=Rates!A1+A1", Formula(deposit, "A3"));
        Assert.Equal(24, deposit.Number("A1"));
        Assert.Equal(10, deposit.Number("A2"));
        Assert.Contains(deposit, step.Change.Sheets);
        Assert.Contains(CellAddress.Parse("A1"), step.Change.For(deposit).Recalculated);

        // The References rewritten are followed: a change at the cell's new place reaches them.
        Enter(workbook, "Rates", "A3", "5");
        Assert.Equal(30, deposit.Number("A1"));
        Assert.Equal(11, deposit.Number("A2"));
    }

    [Fact] // ADR-0331, ADR-0047: a row deleted on one Sheet makes another Sheet's Reference to it #REF!, and undoing it gives the Formula back exactly
    public void A_deletion_rewrites_references_on_every_sheet_and_undoes_exactly()
    {
        var workbook = NewWorkbook("Rates", "Deposit");
        Enter(workbook, "Rates", "A1", "1");
        Enter(workbook, "Rates", "A2", "4");
        Enter(workbook, "Rates", "A3", "6");
        Enter(workbook, "Deposit", "A1", "=Rates!A2*2");
        Enter(workbook, "Deposit", "A2", "=SUM(Rates!A1:A3)");
        Enter(workbook, "Deposit", "A3", "=Rates!A3");

        var step = workbook.Do("Rates", SheetEdit.DeleteRows(1));

        var deposit = workbook.Sheets[1];
        Assert.Equal("=Rates!#REF!*2", Formula(deposit, "A1"));
        Assert.Equal("=SUM(Rates!A1:A2)", Formula(deposit, "A2"));
        Assert.Equal("=Rates!A2", Formula(deposit, "A3"));
        Assert.Equal(ErrorValue.Ref, deposit.Error("A1"));
        Assert.Equal(7, deposit.Number("A2"));

        step.Undo();

        Assert.Equal("=Rates!A2*2", Formula(deposit, "A1"));
        Assert.Equal("=SUM(Rates!A1:A3)", Formula(deposit, "A2"));
        Assert.Equal("=Rates!A3", Formula(deposit, "A3"));
        Assert.Equal(8, deposit.Number("A1"));
        Assert.Equal(11, deposit.Number("A2"));
        Enter(workbook, "Rates", "A2", "5");
        Assert.Equal(10, deposit.Number("A1"));

        step.Redo();
        Assert.Equal("=Rates!#REF!*2", Formula(deposit, "A1"));
    }

    [Fact] // ADR-0331, ADR-0046: a column inserted on one Sheet moves another Sheet's References; a whole row keeps its text and reads again
    public void A_column_insertion_reaches_every_sheet()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        Enter(workbook, "Sheet1", "A1", "2");
        Enter(workbook, "Sheet1", "B1", "3");
        Enter(workbook, "Sheet2", "A1", "=Sheet1!B1");
        Enter(workbook, "Sheet2", "A2", "=SUM(Sheet1!1:1)");

        workbook.Do("Sheet1", SheetEdit.InsertColumns(1));
        Enter(workbook, "Sheet1", "B1", "10");

        var sheet2 = workbook.Sheets[1];
        Assert.Equal("=Sheet1!C1", Formula(sheet2, "A1"));
        Assert.Equal(3, sheet2.Number("A1"));
        Assert.Equal("=SUM(Sheet1!1:1)", Formula(sheet2, "A2"));
        Assert.Equal(15, sheet2.Number("A2"));
    }

    [Fact] // ADR-0331, ADR-0048: a block copied on one Sheet and pasted on another shifts its relative References, as Excel's copy does
    public void Copying_between_sheets_shifts_relative_references()
    {
        var workbook = NewWorkbook("Sheet1", "Sheet2");
        Enter(workbook, "Sheet1", "B1", "=A1+Sheet2!A1+$A$1");
        var block = workbook.Sheets[0].Copy(CellRange.Parse("B1")).Block!;

        workbook.Do("Sheet2", SheetEdit.Paste(block, CellAddress.Parse("C3")));

        Assert.Equal("=B3+Sheet2!B3+$A$1", Formula(workbook.Sheets[1], "C3"));
    }
}
