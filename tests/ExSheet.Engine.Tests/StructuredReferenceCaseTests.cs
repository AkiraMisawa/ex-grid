using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// Ticket 180 (exsheet), ADR-0047, ADR-0049: a structured reference is stored in its Linked Table's declared case,
/// the table's name and the column's, as Excel writes one back, and keeps it when the Formula is written again.
/// </summary>
public class StructuredReferenceCaseTests
{
    private static Workbook Limits()
    {
        var workbook = new Workbook(EnUs);
        workbook.DeclareLinkedTable("LIMIT", ["Line", "Net PV", "Rate#"]);
        workbook.PushLinkedTable("LIMIT",
        [
            [Value.FromNumber(3), Value.FromNumber(10), Value.FromNumber(0.5)],
            [Value.FromNumber(4), Value.FromNumber(20), Value.FromNumber(1)],
        ]);
        return workbook;
    }

    private static void Enter(Workbook workbook, string address, string typed) =>
        workbook.Do(Sheet.DefaultName, SheetEdit.Enter(CellAddress.Parse(address), typed));

    private static string? Formula(Workbook workbook, string address) =>
        workbook.Sheets[0].GetEntry(CellAddress.Parse(address))?.Formula;

    [Theory] // Ticket 180, ADR-0047: typed in another case, written in the declared case of the table and of the column
    [InlineData("=sum(limit[line])", "=SUM(LIMIT[Line])", 7)]
    [InlineData("=SUM(Limit[[net pv]])", "=SUM(LIMIT[Net PV])", 30)]
    [InlineData("=sum(limit[RATE'#])*2", "=SUM(LIMIT[Rate'#])*2", 3)]
    [InlineData("=SUM(LIMIT[Line])", "=SUM(LIMIT[Line])", 7)]
    public void A_structured_reference_is_written_in_the_declared_case(string typed, string written, double value)
    {
        var workbook = Limits();

        Enter(workbook, "B2", typed);

        Assert.Equal(written, Formula(workbook, "B2"));
        Assert.Equal(value, workbook.Sheets[0].Number("B2"));
    }

    [Fact] // Ticket 180, ADR-0048: pasted as text, and entered with Ctrl+Enter, a Formula is written in the declared case too
    public void A_pasted_or_ctrl_entered_formula_is_written_in_the_declared_case()
    {
        var workbook = Limits();

        workbook.Do(Sheet.DefaultName, SheetEdit.PasteText([["=sum(limit[line])"]], CellAddress.Parse("B2")));
        workbook.Do(Sheet.DefaultName, SheetEdit.EnterInto([CellRange.Parse("C2:C3")], CellAddress.Parse("C2"), "=sum(limit[net pv])"));

        Assert.Equal("=SUM(LIMIT[Line])", Formula(workbook, "B2"));
        Assert.Equal("=SUM(LIMIT[Net PV])", Formula(workbook, "C2"));
        Assert.Equal("=SUM(LIMIT[Net PV])", Formula(workbook, "C3"));
    }

    [Fact] // Ticket 180: a table or a column not declared is left as typed, and is #NAME? or #REF! as before
    public void An_undeclared_table_or_column_is_left_as_typed()
    {
        var workbook = Limits();

        Enter(workbook, "B2", "=sum(other[line])");
        Enter(workbook, "B3", "=sum(limit[nothing])");

        Assert.Equal("=SUM(other[line])", Formula(workbook, "B2"));
        Assert.Equal(ErrorValue.Name, workbook.Sheets[0].Error("B2"));
        Assert.Equal("=SUM(limit[nothing])", Formula(workbook, "B3"));
        Assert.Equal(ErrorValue.Ref, workbook.Sheets[0].Error("B3"));
    }

    [Fact] // Ticket 180: written again by an insertion and a fill, the declared case is kept
    public void The_declared_case_is_kept_when_the_formula_is_written_again()
    {
        var workbook = Limits();
        Enter(workbook, "B2", "=sum(limit[line])+a1");

        workbook.Do(Sheet.DefaultName, SheetEdit.InsertRows(0));
        workbook.Do(Sheet.DefaultName, SheetEdit.Fill(CellRange.Parse("B3"), CellRange.Parse("B3:B4"), FillDirection.Down));

        Assert.Equal("=SUM(LIMIT[Line])+A2", Formula(workbook, "B3"));
        Assert.Equal("=SUM(LIMIT[Line])+A3", Formula(workbook, "B4"));
    }

    [Fact] // Ticket 180: a reopened Workbook's text is the declared case
    public void A_reopened_workbook_keeps_the_declared_case()
    {
        var workbook = Limits();
        Enter(workbook, "B2", "=sum(limit[net pv])");

        var reopened = Workbook.Open(WorkbookDocument.FromJson(workbook.ToDocument().ToJson()));

        Assert.Equal("=SUM(LIMIT[Net PV])", reopened.Sheets[0].GetEntry(CellAddress.Parse("B2"))?.Formula);
    }
}
