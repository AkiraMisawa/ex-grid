using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// The engine's API rebuilt around the Workbook (ADR-0331 §9; exsheet-workbook ticket 13): a Workbook is
/// made, opened and saved where a Sheet was, and the Sheet Day and the clock are the Workbook's, read by
/// every Sheet it holds, one added later too.
/// </summary>
public class WorkbookApiTests
{
    private static readonly CellAddress A1 = CellAddress.Parse("A1");

    private static void Enter(Workbook workbook, string sheet, string address, string typed) =>
        workbook.Do(sheet, SheetEdit.Enter(CellAddress.Parse(address), typed));

    [Fact] // ADR-0331 §9: a Workbook made from a culture alone holds one Sheet, Sheet1
    public void A_workbook_starts_with_sheet1()
    {
        var workbook = new Workbook(EnUs);

        Assert.Equal([Sheet.DefaultName], workbook.Sheets.Select(s => s.Name));
        Assert.Equal(EnUs.Name, workbook.Culture.Name);
    }

    [Fact] // ADR-0331 §9, ADR-0048: a Sheet Document opens as a Workbook of that one Sheet, and a Sheet of a Workbook is saved as one
    public void A_sheet_document_opens_and_saves_through_the_workbook()
    {
        var workbook = new Workbook(EnUs, ["Rates"]);
        Enter(workbook, "Rates", "A1", "2");
        Enter(workbook, "Rates", "A2", "=A1*3");

        var document = workbook.ToDocument("rates");
        var opened = Workbook.Open(document);

        Assert.Equal("Rates", document.Name);
        Assert.Equal("Rates", Assert.Single(opened.Sheets).Name);
        Assert.Equal(Value.FromNumber(6), opened.Sheets[0].GetValue(CellAddress.Parse("A2")));
        Assert.Throws<ArgumentException>(() => workbook.ToDocument("Missing"));
    }

    [Fact] // ADR-0331 §9, ADR-0121: the Sheet Day is the Workbook's: one setting recalculates TODAY on every Sheet, and a Sheet added later reads it
    public void The_sheet_day_is_the_workbooks()
    {
        var workbook = new Workbook(EnUs, ["One", "Two"]);
        Enter(workbook, "One", "A1", "=TODAY()");
        Enter(workbook, "Two", "A1", "=TODAY()");
        Assert.Equal(Value.FromError(ErrorValue.GettingData), workbook.Sheets[1].GetValue(A1));

        var change = workbook.SetToday(new DateOnly(2026, 10, 20));

        var day = workbook.Sheets[0].GetValue(A1);
        Assert.Equal(day, workbook.Sheets[1].GetValue(A1));
        Assert.NotEqual(Value.FromError(ErrorValue.GettingData), day);
        Assert.Equal([A1], change.For(workbook.Sheets[1]).ValueChanges);
        workbook.Do("Two", SheetEdit.InsertSheet("Three"));
        Enter(workbook, "Three", "A1", "=TODAY()");
        Assert.Equal(day, workbook.Find("Three")!.GetValue(A1));
        Assert.Equal(new DateOnly(2026, 10, 20), workbook.Today);
    }

    [Fact] // ADR-0331 §9, ADR-0124: the moment is read once for a recalculation of the whole Workbook, so every NOW on every Sheet answers it
    public void Now_is_read_once_for_the_whole_workbook()
    {
        var workbook = new Workbook(EnUs, ["One", "Two"]);
        var reads = 0;
        var moment = new DateTime(2026, 10, 20, 9, 30, 0);
        workbook.NowSource = () =>
        {
            reads++;
            return moment.AddMinutes(reads);
        };
        Enter(workbook, "One", "A1", "=NOW()");
        Enter(workbook, "Two", "A1", "=NOW()");
        reads = 0;

        workbook.RecalculateVolatile();

        Assert.Equal(1, reads);
        Assert.True(workbook.HasVolatileFormulas);
        Assert.Equal(workbook.Sheets[0].GetValue(A1), workbook.Sheets[1].GetValue(A1));
    }

    [Fact] // ADR-0331 §9, ADR-0049: a Linked Table is declared and pushed on the Workbook, with one key column, and read from any Sheet
    public void A_linked_table_is_the_workbooks()
    {
        var workbook = new Workbook(EnUs, ["One", "Two"]);
        workbook.DeclareLinkedTable("Positions", ["Id", "PV"], key: "Id");
        Enter(workbook, "Two", "A1", "=XLOOKUP(\"R-1\", Positions[Id], Positions[PV])");

        workbook.PushLinkedTable("Positions", [[Value.FromText("R-1"), Value.FromNumber(250.5)]]);

        Assert.Equal(["Id"], Assert.Single(workbook.LinkedTables).KeyColumns);
        Assert.Equal(Value.FromNumber(250.5), workbook.Sheets[1].GetValue(A1));
    }
}
