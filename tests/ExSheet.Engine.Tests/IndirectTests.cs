using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// <c>INDIRECT</c> (ADR-0124's note of 2026-10-10, docs/specs/exsheet-functions ticket 18, SH-59,
/// SH-68): a volatile function, so what the corpus cannot state is here — the place its text names
/// read as it stands after each recalculation, whatever the change, and a Formula that reaches itself
/// through it. The forms it reads and refuses are in <c>ExcelCases/indirect.json</c>.
/// </summary>
public class IndirectTests
{
    [Fact] // ADR-0124 (2026-10-10), SH-59: INDIRECT reads a cell its Formula's text does not name, and follows a change to it
    public void ADR0124_indirect_follows_a_cell_its_text_does_not_name()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "5");
        sheet.Enter("B3", "7");
        sheet.Enter("C1", "2");
        sheet.Enter("A1", "=INDIRECT(\"B\"&C1)");
        Assert.Equal(5, sheet.Number("A1"));

        sheet.Enter("B2", "50");
        Assert.Equal(50, sheet.Number("A1"));

        sheet.Enter("C1", "3");
        Assert.Equal(7, sheet.Number("A1"));
    }

    [Fact] // ADR-0124 (2026-10-10), SH-59: a cell the same recalculation has still to compute is computed first, whatever the order
    public void ADR0124_indirect_reads_a_cell_computed_in_the_same_recalculation()
    {
        var sheet = NewSheet();
        sheet.Enter("D1", "1");
        sheet.Enter("A6", "=D1*2");
        sheet.Enter("B1", "=INDIRECT(\"A6\")+0");
        Assert.Equal(2, sheet.Number("B1"));

        sheet.Enter("D1", "10");

        Assert.Equal(20, sheet.Number("A6"));
        Assert.Equal(20, sheet.Number("B1"));
    }

    [Fact] // ADR-0124 (2026-10-10), ADR-0047, SH-59: a Formula that reaches itself through INDIRECT is #CIRC!
    public void ADR0124_indirect_to_its_own_cell_is_circ()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=INDIRECT(\"A1\")+1");
        sheet.Enter("B1", "=SUM(INDIRECT(\"B1:B2\"))");

        Assert.Equal(ErrorValue.Circ, sheet.Error("A1"));
        Assert.Equal(ErrorValue.Circ, sheet.Error("B1"));
    }

    [Theory] // ADR-0124 (2026-10-10), ADR-0047, SH-59: a Formula that reaches itself through a computed Reference is #CIRC!, though what it computes passes over, counts or never reads the Error Value
    [InlineData("A1", "=COUNT(INDIRECT(\"A1:A3\"))")]
    [InlineData("A1", "=COUNTA(INDIRECT(\"A1:A3\"))")]
    [InlineData("A1", "=SUBTOTAL(2,INDIRECT(\"A1:A3\"))")]
    [InlineData("A1", "=SUBTOTAL(9,INDIRECT(\"A1:A3\"))")]
    [InlineData("A1", "=AGGREGATE(9,6,INDIRECT(\"A1:A3\"))")]
    [InlineData("A1", "=AGGREGATE(9,2,INDIRECT(\"A1:A3\"))")]
    [InlineData("A1", "=AGGREGATE(14,6,INDIRECT(\"A1:A3\"),1)")]
    [InlineData("A1", "=ISERROR(INDIRECT(\"A1\"))")]
    [InlineData("A1", "=SUBTOTAL(9,OFFSET(B1,0,-1,3,1))")]
    [InlineData("A1", "=COUNT(OFFSET(B1,0,-1,3,1))")]
    public void ADR0124_a_formula_that_reaches_itself_through_a_computed_reference_is_circ_whatever_it_computes(string cell, string formula)
    {
        var sheet = NewSheet();
        sheet.Enter("A2", "1");
        sheet.Enter("A3", "2");
        sheet.Enter(cell, formula);

        Assert.Equal(ErrorValue.Circ, sheet.Error(cell));
    }

    [Theory] // ADR-0124 (2026-10-10), ADR-0047: a computed Reference over a cycle is #CIRC!, as a named one is, though what reads it passes over or counts an Error Value
    [InlineData("=SUM(B1:B3)")]
    [InlineData("=SUM(INDIRECT(\"B1:B3\"))")]
    [InlineData("=AGGREGATE(9,6,INDIRECT(\"B1:B3\"))")]
    [InlineData("=AGGREGATE(9,2,INDIRECT(\"B1:B3\"))")]
    [InlineData("=COUNT(INDIRECT(\"B1:B3\"))")]
    [InlineData("=COUNTA(INDIRECT(\"B1:B3\"))")]
    [InlineData("=IFERROR(INDIRECT(\"B1\"),0)")]
    public void ADR0124_a_computed_reference_over_a_cycle_is_circ(string formula)
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=B2");
        sheet.Enter("B2", "=B1");
        sheet.Enter("B3", "4");
        sheet.Enter("A1", formula);

        Assert.Equal(ErrorValue.Circ, sheet.Error("A1"));
    }

    [Theory] // ADR-0124 (2026-10-10), ADR-0049: a computed Reference over a waiting cell waits, as a named one does, though what reads it passes over or counts an Error Value; and reads the cell once it has its data
    [InlineData("=SUM(B1:B2)", 6.08)]
    [InlineData("=AGGREGATE(9,6,INDIRECT(\"B1:B2\"))", 6.08)]
    [InlineData("=AGGREGATE(9,3,INDIRECT(\"B1:B2\"))", 6.08)]
    [InlineData("=COUNT(INDIRECT(\"B1:B2\"))", 2)]
    [InlineData("=COUNTA(INDIRECT(\"B1:B2\"))", 2)]
    [InlineData("=SUBTOTAL(9,INDIRECT(\"B1:B2\"))", 6.08)]
    public void ADR0124_a_computed_reference_over_a_waiting_cell_waits(string formula, double withData)
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"]);
        sheet.Enter("B1", "=INDEX(Rates[Mid],1)");
        sheet.Enter("B2", "5");
        sheet.Enter("A1", formula);

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));

        sheet.PushLinkedTable("Rates", [[Value.FromText("EURUSD"), Value.FromNumber(1.08)]]);

        Assert.Equal(withData, sheet.Number("A1"), 9);
    }

    [Fact] // ADR-0124 (2026-10-10), ADR-0047: a nested SUBTOTAL reached through a computed Reference that waits makes the outer one wait, though the outer passes it over
    public void ADR0124_a_nested_subtotal_that_waits_makes_the_one_reading_it_through_indirect_wait()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"]);
        sheet.Enter("B1", "=SUBTOTAL(9,Rates[Mid])");
        sheet.Enter("B2", "5");
        sheet.Enter("A1", "=SUBTOTAL(9,INDIRECT(\"B1:B2\"))");

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));

        sheet.PushLinkedTable("Rates", [[Value.FromText("EURUSD"), Value.FromNumber(1.08)]]);

        Assert.Equal(5, sheet.Number("A1"));
    }

    [Fact] // ADR-0124 (2026-10-10), SH-68: an insertion leaves INDIRECT's text as it was, and the place it names is read again, though nothing else moved
    public void ADR0124_indirect_reads_its_text_again_after_an_insertion_and_its_undo()
    {
        var sheet = NewSheet();
        sheet.Enter("B5", "62");
        sheet.Enter("A1", "=INDIRECT(\"B5\")");
        Assert.Equal(62, sheet.Number("A1"));

        var step = sheet.Do(SheetEdit.InsertRows(3));

        Assert.Equal("=INDIRECT(\"B5\")", sheet.GetEntry(CellAddress.Parse("A1"))!.Formula);
        Assert.Equal(0, sheet.Number("A1"));

        step.Undo();

        Assert.Equal(62, sheet.Number("A1"));
    }

    [Fact] // ADR-0124 (2026-10-10), ADR-0046: text qualified with the Sheet's name names nothing once the Sheet is renamed, and its cell again when the rename is undone
    public void ADR0124_indirect_follows_a_rename_of_the_sheet()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "5");
        sheet.Enter("A1", "=INDIRECT(\"Sheet1!B2\")");
        Assert.Equal(5, sheet.Number("A1"));

        var step = sheet.Do(SheetEdit.Rename("Risk"));

        Assert.Equal(ErrorValue.Ref, sheet.Error("A1"));

        step.Undo();

        Assert.Equal(5, sheet.Number("A1"));
    }

    [Fact] // ADR-0124: a volatile Formula deleted with its row leaves no volatile Formula behind
    public void ADR0124_a_volatile_formula_deleted_with_its_row_is_forgotten()
    {
        var sheet = NewSheet();
        sheet.Enter("A3", "=INDIRECT(\"B1\")");
        Assert.True(sheet.HasVolatileFormulas);

        sheet.DeleteRows(2);

        Assert.False(sheet.HasVolatileFormulas);
    }

    [Fact] // ADR-0124 (2026-10-10), ADR-0049: INDIRECT reads a cell that waits as it stands, and IFERROR does not catch the wait
    public void ADR0124_indirect_over_a_waiting_cell_waits()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"]);
        sheet.Enter("B1", "=INDEX(Rates[Mid],1)");
        sheet.Enter("A1", "=IFERROR(INDIRECT(\"B1\"),0)");

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));

        sheet.PushLinkedTable("Rates", [[Value.FromText("EURUSD"), Value.FromNumber(1.08)]]);

        Assert.Equal(1.08, sheet.Number("A1"));
    }

    [Fact] // ADR-0124 (2026-10-10), ADR-0049: a declared Linked Table's bare name is a structured reference Excel reads, so #VALUE! until a run observes it; any other name is #REF!
    public void ADR0124_a_linked_tables_bare_name_is_value_and_any_other_name_ref()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=INDIRECT(\"Positions\")");
        sheet.Enter("A2", "=SUM(INDIRECT(\"positions\"))");
        Assert.Equal(ErrorValue.Ref, sheet.Error("A1"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("A2"));

        // The declaration is a change, and INDIRECT is volatile: it reads its text again.
        sheet.DeclareLinkedTable("Positions", ["Id", "PV"]);
        Assert.Equal(ErrorValue.Value, sheet.Error("A1"));
        Assert.Equal(ErrorValue.Value, sheet.Error("A2"));

        sheet.PushLinkedTable("Positions", [[Value.FromText("R-1"), Value.FromNumber(250.5)]]);
        Assert.Equal(ErrorValue.Value, sheet.Error("A1"));
        Assert.Equal(ErrorValue.Value, sheet.Error("A2"));
    }
}
