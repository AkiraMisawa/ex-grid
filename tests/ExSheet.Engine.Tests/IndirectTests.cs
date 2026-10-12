using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// <c>INDIRECT</c> (ADR-0124's note of 2026-10-10, docs/specs/exsheet-functions ticket 18, SH-59,
/// SH-68): a volatile function, so what the corpus cannot state is here — the place its text names
/// read as it stands after each recalculation, whatever the change, and a Formula that reaches itself
/// through it. The forms it reads and refuses are in <c>ExcelCases/indirect.json</c>; the forms the
/// seventeenth Windows run observed (R1C1, structured and <c>A1#</c> text, INDIRECT-023 to 026) are
/// read here as the Formula forms they name are, cycles, waits and spills included, and so are the
/// relative and whole-row and whole-column R1C1 text the area pass of 2026-10-12 observed
/// (INDIRECT-036 to 038).
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

    private static Sheet MicrosoftsExample()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "1.333");
        sheet.Enter("B3", "45");
        sheet.Enter("B4", "10");
        sheet.Enter("B5", "62");
        return sheet;
    }

    [Theory] // ADR-0124 (2026-10-10), seventeenth Windows run (INDIRECT-023, 024): R1C1 text naming an absolute cell or a range of two is read when a1 is FALSE, 0, a blank or left empty
    [InlineData("=INDIRECT(\"R3C2\",FALSE)", 45)]
    [InlineData("=INDIRECT(\"R3C2\",0)", 45)]
    [InlineData("=INDIRECT(\"R3C2\",D9)", 45)]
    [InlineData("=INDIRECT(\"R3C2\",)", 45)]
    [InlineData("=INDIRECT(\"R3C2\",\"FALSE\")", 45)]
    [InlineData("=INDIRECT(\"r3c2\",FALSE)", 45)]
    [InlineData("=INDIRECT(\"Sheet1!R3C2\",FALSE)", 45)]
    [InlineData("=INDIRECT(\"'Sheet1'!R3C2\",FALSE)", 45)]
    [InlineData("=SUM(INDIRECT(\"R2C2:R5C2\",FALSE))", 118.333)]
    [InlineData("=SUM(INDIRECT(\"R5C2:R2C2\",FALSE))", 118.333)]
    [InlineData("=ROWS(INDIRECT(\"R2C2:R5C2\",FALSE))", 4)]
    [InlineData("=INDIRECT(\"R9C2\",FALSE)", 0)]
    public void ADR0124_indirect_reads_absolute_r1c1_text(string formula, double expected)
    {
        var sheet = MicrosoftsExample();

        Assert.Equal(expected, sheet.Evaluate(formula).Number, 9);
        // The same text read in A1 style names no place (INDIRECT-022).
        Assert.Equal(ErrorValue.Ref, sheet.Evaluate("=INDIRECT(\"R3C2\",TRUE)").Error);
    }

    [Theory] // ADR-0124 (2026-10-10, 2026-10-12): R1C1 text in a form no Windows run has observed — a number with a leading 0 or a +, a cell joined to a whole row or column, a row to a column — is refused with #VALUE!, as are structured and A1# text in R1C1 style (INDIRECT-058 to 061 ask them)
    [InlineData("R03C2")]
    [InlineData("R0C2")]
    [InlineData("R3C02")]
    [InlineData("R[01]C2")]
    [InlineData("R[+1]C2")]
    [InlineData("R3C2:R5")]
    [InlineData("R3:R5C2")]
    [InlineData("R3:C2")]
    [InlineData("C2:RC")]
    [InlineData("Sheet1!R3C2:C2")]
    [InlineData("R3C2#")]
    [InlineData("Positions[PV]")]
    public void ADR0124_indirect_refuses_r1c1_text_no_run_has_observed(string text)
    {
        var sheet = MicrosoftsExample();
        sheet.DeclareLinkedTable("Positions", ["Id", "PV"]);
        sheet.PushLinkedTable("Positions", [[Value.FromText("R-1"), Value.FromNumber(250.5)]]);

        Assert.Equal(ErrorValue.Value, sheet.Evaluate($"=INDIRECT(\"{text}\",FALSE)").Error);
    }

    [Theory] // ADR-0124 (2026-10-10, 2026-10-12), ADR-0046: R1C1 text that names no place is #REF!, as A1 text's is: A1 text read as R1C1, a row or column past the Sheet's edge, another Sheet, a space
    [InlineData("B3")]
    [InlineData("R1048577C2")]
    [InlineData("R3C16385")]
    [InlineData("R3C2:R99999999C2")]
    [InlineData("R1048577")]
    [InlineData("C16385")]
    [InlineData("R2:R1048577")]
    [InlineData("C99999999999")]
    [InlineData("Sheet2!R3")]
    [InlineData("Sheet2!C2")]
    [InlineData("Sheet2!R3C2")]
    [InlineData(" R3C2")]
    [InlineData("R3C2 ")]
    [InlineData("R3C2:")]
    [InlineData("")]
    [InlineData("no place")]
    public void ADR0124_indirect_r1c1_text_that_names_no_place_is_ref(string text)
    {
        var sheet = MicrosoftsExample();

        Assert.Equal(ErrorValue.Ref, sheet.Evaluate($"=INDIRECT(\"{text}\",FALSE)").Error);
    }

    [Theory] // ADR-0124 (2026-10-12), the area pass (INDIRECT-036, 044, 045, 050): relative R1C1 text counts from the Formula's own cell, R and C each with a number, an offset in brackets or nothing
    [InlineData("C2", "=INDIRECT(\"R[1]C[-1]\",FALSE)", 45)]
    [InlineData("C2", "=INDIRECT(\"RC[-1]\",FALSE)", 1.333)]
    [InlineData("C3", "=INDIRECT(\"R[-1]C[-1]\",FALSE)", 1.333)]
    [InlineData("C2", "=INDIRECT(\"R3C[-1]\",FALSE)", 45)]
    [InlineData("C2", "=INDIRECT(\"R[2]C2\",FALSE)", 10)]
    [InlineData("A5", "=INDIRECT(\"RC[1]\",FALSE)", 62)]
    [InlineData("C5", "=INDIRECT(\"R[0]C[-1]\",FALSE)", 62)]
    [InlineData("C5", "=INDIRECT(\"R[-0]C[-1]\",FALSE)", 62)]
    [InlineData("C2", "=INDIRECT(\"rc[-1]\",FALSE)", 1.333)]
    [InlineData("C2", "=INDIRECT(\"Sheet1!R[1]C[-1]\",FALSE)", 45)]
    [InlineData("C2", "=INDIRECT(\"R[1]C[-1]\",0)", 45)]
    [InlineData("C2", "=INDIRECT(\"R[7]C[-1]\",FALSE)", 0)]
    public void ADR0124_indirect_reads_relative_r1c1_text_from_its_own_cell(string cell, string formula, double expected)
    {
        var sheet = MicrosoftsExample();
        sheet.Enter(cell, formula);

        Assert.Equal(expected, sheet.Number(cell), 9);
    }

    [Theory] // ADR-0124 (2026-10-12), the area pass (INDIRECT-037, 038, 046 to 049, 051): a row part alone is a whole row, a column part alone a whole column, and two parts of the same kind joined by ':' the rectangle they span
    [InlineData("ZZ1000", "=SUM(INDIRECT(\"R3\",FALSE))", 45)]
    [InlineData("ZZ1000", "=SUM(INDIRECT(\"C2\",FALSE))", 118.333)]
    [InlineData("C2", "=SUM(INDIRECT(\"R[1]\",FALSE))", 45)]
    [InlineData("C2", "=SUM(INDIRECT(\"C[-1]\",FALSE))", 118.333)]
    [InlineData("ZZ1000", "=SUM(INDIRECT(\"R2:R3\",FALSE))", 46.333)]
    [InlineData("ZZ1000", "=SUM(INDIRECT(\"R3:R2\",FALSE))", 46.333)]
    [InlineData("ZZ1000", "=SUM(INDIRECT(\"C1:C2\",FALSE))", 118.333)]
    [InlineData("C2", "=SUM(INDIRECT(\"RC[-1]:R[1]C[-1]\",FALSE))", 46.333)]
    [InlineData("C2", "=SUM(INDIRECT(\"R[1]C[-1]:RC[-1]\",FALSE))", 46.333)]
    [InlineData("C2", "=SUM(INDIRECT(\"R2C2:R[3]C[-1]\",FALSE))", 118.333)]
    [InlineData("ZZ1000", "=ROWS(INDIRECT(\"R2:R3\",FALSE))", 2)]
    [InlineData("ZZ1000", "=COLUMNS(INDIRECT(\"R3\",FALSE))", 16384)]
    [InlineData("ZZ1000", "=ROWS(INDIRECT(\"C2\",FALSE))", 1048576)]
    [InlineData("ZZ1000", "=COLUMNS(INDIRECT(\"C1:C2\",FALSE))", 2)]
    [InlineData("ZZ1000", "=SUM(INDIRECT(\"Sheet1!R3\",FALSE))", 45)]
    [InlineData("ZZ1000", "=SUM(INDIRECT(\"r3\",FALSE))", 45)]
    public void ADR0124_indirect_reads_whole_rows_columns_and_rectangles_of_r1c1_text(string cell, string formula, double expected)
    {
        var sheet = MicrosoftsExample();
        sheet.Enter(cell, formula);

        Assert.Equal(expected, sheet.Number(cell), 9);
    }

    [Theory] // ADR-0124 (2026-10-12), ADR-0046 (INDIRECT-052): an offset past the Sheet's edge names no place, #REF!, on either side and on either axis
    [InlineData("C2", "R[-5]C")]
    [InlineData("C2", "R[-2]C[-1]")]
    [InlineData("C2", "RC[-3]")]
    [InlineData("C2", "R[1048575]C")]
    [InlineData("XFD1", "RC[1]")]
    [InlineData("C2", "R[-2]")]
    [InlineData("C2", "C[-3]")]
    [InlineData("C2", "R[-1]:R[-2]")]
    [InlineData("C2", "R[9999999999]C")]
    public void ADR0124_indirect_r1c1_offset_past_the_sheets_edge_is_ref(string cell, string text)
    {
        var sheet = MicrosoftsExample();
        sheet.Enter(cell, $"=INDIRECT(\"{text}\",FALSE)");

        Assert.Equal(ErrorValue.Ref, sheet.Error(cell));
    }

    [Fact] // ADR-0124 (2026-10-12), ADR-0047, SH-59: relative R1C1 text that reaches the Formula's own cell, row or column is #CIRC!
    public void ADR0124_relative_r1c1_text_to_its_own_place_is_circ()
    {
        var sheet = MicrosoftsExample();
        sheet.Enter("C2", "=INDIRECT(\"RC\",FALSE)");
        sheet.Enter("D2", "=SUM(INDIRECT(\"R\",FALSE))");
        sheet.Enter("E2", "=SUM(INDIRECT(\"C\",FALSE))");
        sheet.Enter("F2", "=SUM(INDIRECT(\"RC[-3]:R[3]C\",FALSE))");

        Assert.Equal(ErrorValue.Circ, sheet.Error("C2"));
        Assert.Equal(ErrorValue.Circ, sheet.Error("D2"));
        Assert.Equal(ErrorValue.Circ, sheet.Error("E2"));
        Assert.Equal(ErrorValue.Circ, sheet.Error("F2"));
    }

    [Fact] // ADR-0124 (2026-10-12), SH-68: relative R1C1 text counts from where its Formula stands now: an insertion moves the Formula and leaves the text, and its undo moves it back
    public void ADR0124_relative_r1c1_text_counts_from_the_formulas_cell_after_it_moves()
    {
        var sheet = MicrosoftsExample();
        sheet.Enter("D5", "=INDIRECT(\"R[-2]C[-2]\",FALSE)");
        Assert.Equal(45, sheet.Number("D5"));

        // A row inserted above row 4 moves the Formula to D6 and B4's 10 to B5; B3 stays, and two rows
        // up from D6 is the new, blank B4.
        var step = sheet.Do(SheetEdit.InsertRows(3));

        Assert.Equal("=INDIRECT(\"R[-2]C[-2]\",FALSE)", sheet.GetEntry(CellAddress.Parse("D6"))!.Formula);
        Assert.Equal(0, sheet.Number("D6"));
        Assert.Equal(10, sheet.Number("B5"));

        step.Undo();

        Assert.Equal(45, sheet.Number("D5"));
    }

    [Fact] // ADR-0124 (2026-10-12): the same relative R1C1 text in each cell of a column reads the cell beside each
    public void ADR0124_the_same_relative_r1c1_text_reads_beside_each_cell()
    {
        var sheet = MicrosoftsExample();
        foreach (var cell in new[] { "C2", "C3", "C4", "C5" }) sheet.Enter(cell, "=INDIRECT(\"RC[-1]\",FALSE)*2");

        Assert.Equal(2.666, sheet.Number("C2"), 9);
        Assert.Equal(90, sheet.Number("C3"));
        Assert.Equal(20, sheet.Number("C4"));
        Assert.Equal(124, sheet.Number("C5"));
    }

    [Fact] // ADR-0124 (2026-10-12), ADR-0125: relative R1C1 text naming a range spills where it is the result
    public void ADR0124_relative_r1c1_range_spills()
    {
        var sheet = MicrosoftsExample();
        sheet.Enter("D2", "=INDIRECT(\"RC[-2]:R[1]C[-2]\",FALSE)");

        Assert.Equal(1.333, sheet.Number("D2"));
        Assert.Equal(45, sheet.Number("D3"));
    }

    [Fact] // ADR-0124 (2026-10-10), ADR-0047, SH-59: a Formula that reaches itself through R1C1 text is #CIRC!, and R1C1 text follows a change to the cell it names
    public void ADR0124_indirect_r1c1_text_to_its_own_cell_is_circ()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=INDIRECT(\"R1C1\",FALSE)+1");
        sheet.Enter("B1", "=COUNT(INDIRECT(\"R1C2:R3C2\",FALSE))");
        sheet.Enter("C1", "=INDIRECT(\"R2C3\",FALSE)");
        sheet.Enter("C2", "5");

        Assert.Equal(ErrorValue.Circ, sheet.Error("A1"));
        Assert.Equal(ErrorValue.Circ, sheet.Error("B1"));
        Assert.Equal(5, sheet.Number("C1"));

        sheet.Enter("C2", "6");

        Assert.Equal(6, sheet.Number("C1"));
    }

    [Fact] // ADR-0124 (2026-10-10), ADR-0125: R1C1 text naming a range spills where it is the result, as A1 text's does
    public void ADR0124_indirect_r1c1_range_spills()
    {
        var sheet = MicrosoftsExample();
        sheet.Enter("D1", "=INDIRECT(\"R2C2:R3C2\",FALSE)");

        Assert.Equal(1.333, sheet.Number("D1"));
        Assert.Equal(45, sheet.Number("D2"));
    }

    [Theory] // ADR-0124 (2026-10-10), ADR-0049, seventeenth Windows run (INDIRECT-025): structured reference text reads the Linked Table's column as Table[Column] typed reads it
    [InlineData("=INDIRECT(\"Positions[PV]\")", 250.5)]
    [InlineData("=INDIRECT(\"Positions[[PV]]\")", 250.5)]
    [InlineData("=INDIRECT(\"positions[pv]\")", 250.5)]
    [InlineData("=SUM(INDIRECT(\"Positions[PV]\"))", 350.5)]
    [InlineData("=ROWS(INDIRECT(\"Positions[PV]\"))", 2)]
    [InlineData("=SUM(Positions[PV])", 350.5)]
    public void ADR0124_indirect_reads_structured_reference_text(string formula, double expected)
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Positions", ["Id", "PV"]);
        sheet.PushLinkedTable("Positions", [[Value.FromText("R-1"), Value.FromNumber(250.5)], [Value.FromText("R-2"), Value.FromNumber(100)]]);

        Assert.Equal(expected, sheet.Evaluate(formula).Number);
    }

    [Theory] // ADR-0124 (2026-10-10), ADR-0049: structured reference text naming a table never declared, or a column the table does not have, names no place: #REF!, where the typed one is #NAME? and #REF!; a form a Formula does not take is refused
    [InlineData("=INDIRECT(\"Missing[PV]\")", ErrorValue.Ref)]
    [InlineData("=INDIRECT(\"Positions[Delta]\")", ErrorValue.Ref)]
    [InlineData("=Positions[Delta]", ErrorValue.Ref)]
    [InlineData("=Missing[PV]", ErrorValue.Name)]
    [InlineData("=INDIRECT(\"Positions[#All]\")", ErrorValue.Value)]
    [InlineData("=INDIRECT(\"Positions[@PV]\")", ErrorValue.Value)]
    [InlineData("=INDIRECT(\"Positions[[#This Row],[PV]]\")", ErrorValue.Value)]
    [InlineData("=INDIRECT(\"Positions[PV]x\")", ErrorValue.Value)]
    [InlineData("=INDIRECT(\"Sheet1!Positions[PV]\")", ErrorValue.Ref)]
    public void ADR0124_indirect_structured_text_that_names_no_table_column_is_ref(string formula, ErrorValue expected)
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Positions", ["Id", "PV"]);
        sheet.PushLinkedTable("Positions", [[Value.FromText("R-1"), Value.FromNumber(250.5)]]);

        Assert.Equal(expected, sheet.Evaluate(formula).Error);
    }

    [Theory] // ADR-0124 (2026-10-10), ADR-0049: structured reference text naming a table still waiting for its data waits, as the typed one does, though what reads it passes over or catches an Error Value; and reads the column once the data arrives, and again when it changes
    [InlineData("=INDIRECT(\"Rates[Mid]\")", 1.08, 1.1)]
    [InlineData("=IFERROR(INDIRECT(\"Rates[Mid]\"),0)", 1.08, 1.1)]
    [InlineData("=ISERROR(INDIRECT(\"Rates[Mid]\"))+1", 1, 1)]
    [InlineData("=COUNT(INDIRECT(\"Rates[Mid]\"))", 1, 1)]
    [InlineData("=AGGREGATE(9,6,INDIRECT(\"Rates[Mid]\"))", 1.08, 1.1)]
    public void ADR0124_indirect_structured_text_naming_a_waiting_table_waits(string formula, double withData, double changed)
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"]);
        sheet.Enter("A1", formula);

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));

        sheet.PushLinkedTable("Rates", [[Value.FromText("EURUSD"), Value.FromNumber(1.08)]]);
        Assert.Equal(withData, sheet.Number("A1"), 9);

        sheet.PushLinkedTable("Rates", [[Value.FromText("EURUSD"), Value.FromNumber(1.1)]]);
        Assert.Equal(changed, sheet.Number("A1"), 9);
    }

    [Fact] // ADR-0124 (2026-10-10), ADR-0049: text built to name a table's column follows the text, and a table declared after the Formula is read once it has its data
    public void ADR0124_indirect_structured_text_follows_its_text_and_a_later_declaration()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "Positions");
        sheet.Enter("A1", "=SUM(INDIRECT(B1&\"[PV]\"))");

        Assert.Equal(ErrorValue.Ref, sheet.Error("A1"));

        sheet.DeclareLinkedTable("Positions", ["Id", "PV"]);
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));

        sheet.PushLinkedTable("Positions", [[Value.FromText("R-1"), Value.FromNumber(250.5)]]);
        Assert.Equal(250.5, sheet.Number("A1"));

        sheet.DeclareLinkedTable("Trades", ["Id", "PV"]);
        sheet.PushLinkedTable("Trades", [[Value.FromText("T-1"), Value.FromNumber(7)]]);
        sheet.Enter("B1", "Trades");
        Assert.Equal(7, sheet.Number("A1"));
    }

    [Fact] // ADR-0124 (2026-10-10), ADR-0125, seventeenth Windows run (INDIRECT-026): A1# text reads the Spill Range of the Formula in A1, as typed A1# does, and follows it as it grows
    public void ADR0124_indirect_reads_a_spill_range_text()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=SEQUENCE(3)");
        sheet.Enter("C1", "=SUM(INDIRECT(\"A1#\"))");
        sheet.Enter("D1", "=ROWS(INDIRECT(\"Sheet1!A1#\"))");
        sheet.Enter("E1", "=SUM(A1#)");

        Assert.Equal(6, sheet.Number("C1"));
        Assert.Equal(3, sheet.Number("D1"));
        Assert.Equal(6, sheet.Number("E1"));

        sheet.Enter("A1", "=SEQUENCE(4)");

        Assert.Equal(10, sheet.Number("C1"));
        Assert.Equal(4, sheet.Number("D1"));
    }

    [Fact] // ADR-0124 (2026-10-10), ADR-0125: A1# text is #REF! where typed A1# is — no Formula, one Value, or a spill in the way — and reads the spill again once the obstacle is cleared
    public void ADR0124_indirect_spill_range_text_to_no_spill_is_ref_as_typed()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("E1", "=SUM(INDIRECT(\"C1#\"))");
        sheet.Enter("F1", "=SUM(C1#)");
        Assert.Equal(ErrorValue.Ref, sheet.Error("E1"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("F1"));

        sheet.Enter("C1", "=A1");
        Assert.Equal(ErrorValue.Ref, sheet.Error("E1"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("F1"));

        sheet.Enter("C1", "=A1:A2");
        Assert.Equal(3, sheet.Number("E1"));
        Assert.Equal(3, sheet.Number("F1"));

        sheet.Enter("C2", "x");
        Assert.Equal(ErrorValue.Ref, sheet.Error("F1"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("E1"));

        sheet.Enter("C2", "");
        Assert.Equal(3, sheet.Number("F1"));
        Assert.Equal(3, sheet.Number("E1"));

        Assert.Equal(ErrorValue.Ref, sheet.Evaluate("=SUM(INDIRECT(\"Sheet2!C1#\"))").Error);
    }

    [Fact] // ADR-0124 (2026-10-10), ADR-0125: INDIRECT's text naming a spilled cell reads it as the layout leaves it, as a typed Reference does
    public void ADR0124_indirect_reads_a_spilled_cell_as_the_layout_leaves_it()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=SEQUENCE(3)");
        sheet.Enter("C1", "=INDIRECT(\"A3\")");
        sheet.Enter("D1", "=A3");
        Assert.Equal(3, sheet.Number("D1"));
        Assert.Equal(3, sheet.Number("C1"));

        sheet.Enter("A2", "x");
        Assert.Equal(0, sheet.Number("D1"));
        Assert.Equal(0, sheet.Number("C1"));

        sheet.Enter("A2", "");
        Assert.Equal(3, sheet.Number("D1"));
        Assert.Equal(3, sheet.Number("C1"));
    }

    [Fact] // ADR-0124 (2026-10-10), ADR-0125: A1# text spills where it is the result, as typed A1# does, and a Formula that reaches itself through it is #CIRC!
    public void ADR0124_indirect_spill_range_text_spills_and_cycles()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=SEQUENCE(2)");
        sheet.Enter("C1", "=INDIRECT(\"A1#\")*10");
        Assert.Equal(10, sheet.Number("C1"));
        Assert.Equal(20, sheet.Number("C2"));

        sheet.Enter("E1", "=SEQUENCE(2,1,SUM(INDIRECT(\"E1#\")))");
        Assert.Equal(ErrorValue.Circ, sheet.Error("E1"));
    }
}
