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
/// R1C1 forms the two area passes of 2026-10-12 observed (INDIRECT-036 to 038, 044 and 051 to 066:
/// offsets, whole rows and columns, an R or a C with no number, a zero or signed offset, a leading 0,
/// parts of two kinds joined, <c>R1C1#</c> and structured text) and those that follow from them by the
/// rule they settle. The forms those leave open — a row or column number 0, an offset past the Sheet's
/// edge — are refused with <c>#VALUE!</c> and asked by the corpus.
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

    [Fact] // ADR-0049 (rule 2), ADR-0124, decided with the user 2026-10-12: a declared Linked Table's bare name is #VALUE!, in R1C1 style as in A1 style, though Excel reads it as the table's data (INDIRECT-043, 053 to 057 record Excel's answers): a Linked Table is read by key, never by position; any other name is #REF!
    public void ADR0049_a_linked_tables_bare_name_is_refused_and_any_other_name_ref()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=INDIRECT(\"Positions\")");
        sheet.Enter("A2", "=SUM(INDIRECT(\"positions\"))");
        sheet.Enter("A3", "=INDIRECT(\"Positions\",FALSE)");
        sheet.Enter("A4", "=INDEX(INDIRECT(\"Positions\"),2,1)");
        foreach (var cell in new[] { "A1", "A2", "A3", "A4" }) Assert.Equal(ErrorValue.Ref, sheet.Error(cell));

        // The declaration is a change, and INDIRECT is volatile: it reads its text again.
        sheet.DeclareLinkedTable("Positions", ["Id", "PV"]);
        foreach (var cell in new[] { "A1", "A2", "A3", "A4" }) Assert.Equal(ErrorValue.Value, sheet.Error(cell));

        // A table with its data, a row of which Excel's INDEX would read by position, is refused alike.
        sheet.PushLinkedTable("Positions", [[Value.FromText("R-1"), Value.FromNumber(250.5)], [Value.FromText("R-2"), Value.FromNumber(50)]]);
        foreach (var cell in new[] { "A1", "A2", "A3", "A4" }) Assert.Equal(ErrorValue.Value, sheet.Error(cell));
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

    [Theory] // ADR-0124 (2026-10-12), the second area pass (INDIRECT-044, 051, 059, 063 to 066; 067 to 072 ask what follows): an R or a C with no number is the Formula's own row or column, an offset in brackets may be 0, -0 or signed with a +, and a number or an offset may be written with leading 0s
    [InlineData("C2", "=INDIRECT(\"RC[-1]\",FALSE)", 1.333)]
    [InlineData("C2", "=INDIRECT(\"rc[-1]\",FALSE)", 1.333)]
    [InlineData("C2", "=SUM(INDIRECT(\"RC[-1]:R[1]C[-1]\",FALSE))", 46.333)]
    [InlineData("C2", "=SUM(INDIRECT(\"R[1]C[-1]:RC[-1]\",FALSE))", 46.333)]
    [InlineData("B1", "=INDIRECT(\"R[2]C\",FALSE)", 45)]
    [InlineData("C3", "=INDIRECT(\"RC2\",FALSE)", 45)]
    [InlineData("B5", "=INDIRECT(\"R2C\",FALSE)", 1.333)]
    [InlineData("C2", "=INDIRECT(\"R[0]C[-1]\",FALSE)", 1.333)]
    [InlineData("C2", "=INDIRECT(\"R[-0]C[-1]\",FALSE)", 1.333)]
    [InlineData("C2", "=INDIRECT(\"R[+0]C[-1]\",FALSE)", 1.333)]
    [InlineData("C2", "=INDIRECT(\"R[00]C[-1]\",FALSE)", 1.333)]
    [InlineData("B1", "=INDIRECT(\"R[1]C[0]\",FALSE)", 1.333)]
    [InlineData("B1", "=INDIRECT(\"R[1]C[-0]\",FALSE)", 1.333)]
    [InlineData("C2", "=INDIRECT(\"R[+1]C[-1]\",FALSE)", 45)]
    [InlineData("C2", "=INDIRECT(\"R[01]C[-1]\",FALSE)", 45)]
    [InlineData("C2", "=INDIRECT(\"R[+01]C[-01]\",FALSE)", 45)]
    [InlineData("C3", "=INDIRECT(\"R[-01]C[-1]\",FALSE)", 1.333)]
    [InlineData("ZZ1000", "=INDIRECT(\"R03C2\",FALSE)", 45)]
    [InlineData("ZZ1000", "=INDIRECT(\"R3C02\",FALSE)", 45)]
    [InlineData("ZZ1000", "=INDIRECT(\"R0003C0002\",FALSE)", 45)]
    [InlineData("ZZ1000", "=INDIRECT(\"R00000000000003C2\",FALSE)", 45)]
    [InlineData("ZZ1000", "=SUM(INDIRECT(\"R02:R03\",FALSE))", 46.333)]
    [InlineData("ZZ1000", "=SUM(INDIRECT(\"C02\",FALSE))", 118.333)]
    [InlineData("C2", "=SUM(INDIRECT(\"R[+1]\",FALSE))", 45)]
    [InlineData("C2", "=SUM(INDIRECT(\"C[-01]\",FALSE))", 118.333)]
    public void ADR0124_indirect_reads_the_r1c1_forms_the_second_area_pass_observed(string cell, string formula, double expected)
    {
        var sheet = MicrosoftsExample();
        sheet.Enter(cell, formula);

        Assert.Equal(expected, sheet.Number(cell), 9);
    }

    [Theory] // ADR-0124 (2026-10-12), ADR-0047, SH-59: an R or a C with no number is the Formula's own row or column, so R alone is its whole row, C alone its whole column and RC its own cell, each reaching the Formula: #CIRC!
    [InlineData("=SUM(INDIRECT(\"R\",FALSE))")]
    [InlineData("=SUM(INDIRECT(\"C\",FALSE))")]
    [InlineData("=INDIRECT(\"RC\",FALSE)")]
    [InlineData("=SUM(INDIRECT(\"R[-1]:R\",FALSE))")]
    [InlineData("=SUM(INDIRECT(\"C:C[1]\",FALSE))")]
    [InlineData("=INDIRECT(\"R[0]C[0]\",FALSE)")]
    public void ADR0124_r1c1_text_with_no_number_reaches_the_formulas_own_row_or_column(string formula)
    {
        var sheet = MicrosoftsExample();
        sheet.Enter("C2", formula);

        Assert.Equal(ErrorValue.Circ, sheet.Error("C2"));
    }

    [Theory] // ADR-0124 (2026-10-12): a row or column number 0 is refused with #VALUE!, since no run has asked one and the observed forms do not say whether it names no place (INDIRECT-073 asks it)
    [InlineData("R0C2")]
    [InlineData("R00C2")]
    [InlineData("r0c2")]
    [InlineData("R3C0")]
    [InlineData("R0")]
    [InlineData("C0")]
    [InlineData("R0:R3")]
    [InlineData("C2:C00")]
    [InlineData("R0C[-1]")]
    [InlineData("R[1]C0")]
    [InlineData("R0C2:R3C2")]
    [InlineData("R0C1#")]
    [InlineData("Sheet1!R0C2")]
    public void ADR0124_indirect_refuses_a_row_or_column_number_0(string text)
    {
        var sheet = MicrosoftsExample();
        sheet.Enter("C2", $"=INDIRECT(\"{text}\",FALSE)");

        Assert.Equal(ErrorValue.Value, sheet.Error("C2"));
    }

    [Theory] // ADR-0124 (2026-10-10, 2026-10-12), ADR-0046: R1C1 text that names no place is #REF!, as A1 text's is: A1 text read as R1C1 (INDIRECT-035), another Sheet (INDIRECT-008), a space, parts of two kinds joined (INDIRECT-058, 062; 075 to 077 ask the other orders), a # after one cell that does not spill (A1#'s INDIRECT-042; 079 asks it), and a table never declared; and the engine's #REF!, which no run has observed, for a number past the Sheet's edge, as XFE1 is in A1 style (INDIRECT-017; 074 asks it); with an offset or a number 0 beside the number, the Sheet or the join, whatever they would read
    [InlineData("B3")]
    [InlineData("R1048577C2")]
    [InlineData("R3C16385")]
    [InlineData("R3C2:R99999999C2")]
    [InlineData("R1048577")]
    [InlineData("C16385")]
    [InlineData("R2:R1048577")]
    [InlineData("C99999999999")]
    [InlineData("R1048577C[-1]")]
    [InlineData("R[-5]C16385")]
    [InlineData("R[-5]C[-1]:R1048577C2")]
    [InlineData("R[-5]:R1048577")]
    [InlineData("Sheet2!R3")]
    [InlineData("Sheet2!C2")]
    [InlineData("Sheet2!R3C2")]
    [InlineData("Sheet2!R[1]C[-1]")]
    [InlineData("Sheet2!R[-5]C[-1]")]
    [InlineData(" R3C2")]
    [InlineData("R3C2 ")]
    [InlineData("R3C2:")]
    [InlineData("")]
    [InlineData("no place")]
    [InlineData("R2C2:R3")]
    [InlineData("R3C2:R5")]
    [InlineData("R3:R5C2")]
    [InlineData("R3:C2")]
    [InlineData("C2:R3")]
    [InlineData("R2C2:C3")]
    [InlineData("C2:RC")]
    [InlineData("RC[-1]:R")]
    [InlineData("Sheet1!R3C2:C2")]
    [InlineData("R0C2:R3")]
    [InlineData("R[-5]C[-1]:C2")]
    [InlineData("R3C2#")]
    [InlineData("R2C2:R3#")]
    [InlineData("R1048577C2#")]
    [InlineData("Sheet2!R1C1#")]
    [InlineData("#")]
    [InlineData("Positions[PV]")]
    [InlineData("Positions")]
    public void ADR0124_indirect_r1c1_text_that_names_no_place_is_ref(string text)
    {
        var sheet = MicrosoftsExample();
        sheet.Enter("C2", $"=INDIRECT(\"{text}\",FALSE)");

        Assert.Equal(ErrorValue.Ref, sheet.Error("C2"));
    }

    [Theory] // ADR-0124 (2026-10-12), ADR-0047: a # after an R1C1 range, row or column is refused with #VALUE!: the second area pass observed it after one cell only (INDIRECT-060), and the readings part, no place or the Spill Range of a cell it names or the cells themselves (INDIRECT-090, 091 ask it)
    [InlineData("C2", "R2C2:R3C2#")]
    [InlineData("C2", "R1C1:R2C1#")]
    [InlineData("C2", "R3#")]
    [InlineData("C2", "C1#")]
    [InlineData("C2", "R2:R3#")]
    [InlineData("C2", "C1:C2#")]
    [InlineData("C2", "R[1]#")]
    [InlineData("C2", "C[-1]#")]
    [InlineData("C2", "RC[-1]:R[1]C[-1]#")]
    [InlineData("C2", "r3#")]
    [InlineData("C2", "Sheet1!R3#")]
    [InlineData("ZZ1000", "R1C1:R2C1#")]
    public void ADR0124_indirect_refuses_a_hash_after_an_r1c1_range_row_or_column(string cell, string text)
    {
        var sheet = MicrosoftsExample();
        sheet.Enter("A1", "=SEQUENCE(3)");
        sheet.Enter(cell, $"=SUM(INDIRECT(\"{text}\",FALSE))");

        Assert.Equal(ErrorValue.Value, sheet.Error(cell));
        // In A1 style a # after a range is the engine's #REF!, which no run has observed (INDIRECT-092 asks it).
        Assert.Equal(ErrorValue.Ref, sheet.Evaluate("=SUM(INDIRECT(\"A1:A2#\"))").Error);
    }

    [Theory] // ADR-0124 (2026-10-12), the area passes (INDIRECT-036, 045, 050): relative R1C1 text counts from the Formula's own cell, R and C each with a number or an offset in brackets
    [InlineData("C2", "=INDIRECT(\"R[1]C[-1]\",FALSE)", 45)]
    [InlineData("C3", "=INDIRECT(\"R[-1]C[-1]\",FALSE)", 1.333)]
    [InlineData("C2", "=INDIRECT(\"R3C[-1]\",FALSE)", 45)]
    [InlineData("C2", "=INDIRECT(\"R[2]C2\",FALSE)", 10)]
    [InlineData("A5", "=INDIRECT(\"R[-3]C[1]\",FALSE)", 1.333)]
    [InlineData("C2", "=INDIRECT(\"r[1]c[-1]\",FALSE)", 45)]
    [InlineData("C2", "=INDIRECT(\"Sheet1!R[1]C[-1]\",FALSE)", 45)]
    [InlineData("C2", "=INDIRECT(\"R[1]C[-1]\",0)", 45)]
    [InlineData("C2", "=INDIRECT(\"R[7]C[-1]\",FALSE)", 0)]
    public void ADR0124_indirect_reads_relative_r1c1_text_from_its_own_cell(string cell, string formula, double expected)
    {
        var sheet = MicrosoftsExample();
        sheet.Enter(cell, formula);

        Assert.Equal(expected, sheet.Number(cell), 9);
    }

    [Theory] // ADR-0124 (2026-10-12), the area passes (INDIRECT-037, 038, 046 to 049): a row part alone is a whole row, a column part alone a whole column, and two parts of the same kind joined by ':' the rectangle they span
    [InlineData("ZZ1000", "=SUM(INDIRECT(\"R3\",FALSE))", 45)]
    [InlineData("ZZ1000", "=SUM(INDIRECT(\"C2\",FALSE))", 118.333)]
    [InlineData("C2", "=SUM(INDIRECT(\"R[1]\",FALSE))", 45)]
    [InlineData("C2", "=SUM(INDIRECT(\"C[-1]\",FALSE))", 118.333)]
    [InlineData("ZZ1000", "=SUM(INDIRECT(\"R2:R3\",FALSE))", 46.333)]
    [InlineData("ZZ1000", "=SUM(INDIRECT(\"R3:R2\",FALSE))", 46.333)]
    [InlineData("ZZ1000", "=SUM(INDIRECT(\"C1:C2\",FALSE))", 118.333)]
    [InlineData("C2", "=SUM(INDIRECT(\"R[1]C[-1]:R[2]C[-1]\",FALSE))", 55)]
    [InlineData("C2", "=SUM(INDIRECT(\"R[2]C[-1]:R[1]C[-1]\",FALSE))", 55)]
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

    [Theory] // ADR-0124 (2026-10-12), ADR-0047: an offset past the Sheet's edge, on either side and on either axis, is refused with #VALUE!: Excel reads one as a place (INDIRECT-052 is 0, a blank cell, not #REF!), but no run has told whether it wraps round to the Sheet's far side or stops at the edge (INDIRECT-082 to 084 ask it)
    [InlineData("C2", "R[-5]C[-1]")]
    [InlineData("C2", "R[-2]C[-1]")]
    [InlineData("C2", "R[-1]C[-3]")]
    [InlineData("C2", "R[1048575]C[-1]")]
    [InlineData("XFD1", "R1C[1]")]
    [InlineData("A1048576", "R[1]C1")]
    [InlineData("C2", "R[-2]")]
    [InlineData("C2", "C[-3]")]
    [InlineData("C2", "R[-1]:R[-2]")]
    [InlineData("C2", "R[-5]C[-1]:R3C2")]
    [InlineData("C2", "Sheet1!R[-5]C[-1]")]
    [InlineData("C2", "R[9999999999]C[-1]")]
    [InlineData("C2", "RC[-3]")]
    [InlineData("C2", "R[-2]C")]
    [InlineData("C2", "R[-5]C[-1]#")]
    [InlineData("C2", "R2C[-5]")]
    public void ADR0124_indirect_r1c1_offset_past_the_sheets_edge_is_refused(string cell, string text)
    {
        var sheet = MicrosoftsExample();
        sheet.Enter(cell, $"=INDIRECT(\"{text}\",FALSE)");

        Assert.Equal(ErrorValue.Value, sheet.Error(cell));
    }

    [Fact] // ADR-0124 (2026-10-12), ADR-0047, SH-59: relative R1C1 text that reaches the Formula's own cell, row or column is #CIRC!
    public void ADR0124_relative_r1c1_text_to_its_own_place_is_circ()
    {
        var sheet = MicrosoftsExample();
        sheet.Enter("C2", "=SUM(INDIRECT(\"R[-1]C[-1]:R[1]C[1]\",FALSE))");
        sheet.Enter("D2", "=SUM(INDIRECT(\"R[-1]:R[1]\",FALSE))");
        sheet.Enter("E2", "=SUM(INDIRECT(\"C[-1]:C[1]\",FALSE))");
        sheet.Enter("F2", "=SUM(INDIRECT(\"R2C[-4]:R[3]C6\",FALSE))");

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

    [Fact] // ADR-0124 (2026-10-12): the same relative R1C1 text in each cell of a column reads from each cell
    public void ADR0124_the_same_relative_r1c1_text_reads_from_each_cell()
    {
        var sheet = MicrosoftsExample();
        foreach (var cell in new[] { "C3", "C4", "C5", "C6" }) sheet.Enter(cell, "=INDIRECT(\"R[-1]C[-1]\",FALSE)*2");

        Assert.Equal(2.666, sheet.Number("C3"), 9);
        Assert.Equal(90, sheet.Number("C4"));
        Assert.Equal(20, sheet.Number("C5"));
        Assert.Equal(124, sheet.Number("C6"));
    }

    [Fact] // ADR-0124 (2026-10-12), ADR-0125: relative R1C1 text naming a range spills where it is the result
    public void ADR0124_relative_r1c1_range_spills()
    {
        var sheet = MicrosoftsExample();
        sheet.Enter("D1", "=INDIRECT(\"R[1]C[-2]:R[2]C[-2]\",FALSE)");

        Assert.Equal(1.333, sheet.Number("D1"));
        Assert.Equal(45, sheet.Number("D2"));
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

    [Theory] // ADR-0124 (2026-10-10, 2026-10-12), ADR-0049, seventeenth Windows run and the second area pass (INDIRECT-025, 061; 080 asks what follows): structured reference text reads the Linked Table's column as Table[Column] typed reads it, whatever a1 says
    [InlineData("=INDIRECT(\"Positions[PV]\")", 250.5)]
    [InlineData("=INDIRECT(\"Positions[[PV]]\")", 250.5)]
    [InlineData("=INDIRECT(\"positions[pv]\")", 250.5)]
    [InlineData("=SUM(INDIRECT(\"Positions[PV]\"))", 350.5)]
    [InlineData("=ROWS(INDIRECT(\"Positions[PV]\"))", 2)]
    [InlineData("=SUM(Positions[PV])", 350.5)]
    [InlineData("=INDIRECT(\"Positions[PV]\",FALSE)", 250.5)]
    [InlineData("=INDIRECT(\"Positions[[PV]]\",0)", 250.5)]
    [InlineData("=SUM(INDIRECT(\"positions[pv]\",FALSE))", 350.5)]
    [InlineData("=ROWS(INDIRECT(\"Positions[Id]\",))", 2)]
    public void ADR0124_indirect_reads_structured_reference_text(string formula, double expected)
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Positions", ["Id", "PV"]);
        sheet.PushLinkedTable("Positions", [[Value.FromText("R-1"), Value.FromNumber(250.5)], [Value.FromText("R-2"), Value.FromNumber(100)]]);

        Assert.Equal(expected, sheet.Evaluate(formula).Number);
    }

    [Theory] // ADR-0124 (2026-10-10, 2026-10-12), ADR-0049: structured reference text naming a table never declared, or a column the table does not have, names no place: #REF!, where the typed one is #NAME? and #REF!; a form a Formula does not take is refused; in R1C1 style as in A1 style (INDIRECT-061; 081 asks what follows)
    [InlineData("=INDIRECT(\"Missing[PV]\",FALSE)", ErrorValue.Ref)]
    [InlineData("=INDIRECT(\"Positions[Delta]\",FALSE)", ErrorValue.Ref)]
    [InlineData("=INDIRECT(\"Positions[#All]\",FALSE)", ErrorValue.Value)]
    [InlineData("=INDIRECT(\"Positions[PV]x\",FALSE)", ErrorValue.Value)]
    [InlineData("=INDIRECT(\"Positions[PV]#\",FALSE)", ErrorValue.Value)]
    [InlineData("=INDIRECT(\"Sheet1!Positions[PV]\",FALSE)", ErrorValue.Ref)]
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

    [Fact] // ADR-0124 (2026-10-12), ADR-0125, the second area pass (INDIRECT-060; 078 and 079 ask what follows): R1C1 text naming a cell, followed by #, reads that cell's Spill Range as A1# does, follows it as it grows, is #REF! where it does not spill, and is #CIRC! where it reaches its own Formula
    public void ADR0124_indirect_reads_a_spill_range_in_r1c1_text()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=SEQUENCE(3)");
        sheet.Enter("C1", "=SUM(INDIRECT(\"R1C1#\",FALSE))");
        sheet.Enter("D1", "=SUM(INDIRECT(\"RC[-3]#\",FALSE))");
        sheet.Enter("E1", "=ROWS(INDIRECT(\"Sheet1!r1c1#\",FALSE))");
        sheet.Enter("F1", "=SUM(INDIRECT(\"R2C1#\",FALSE))");

        Assert.Equal(6, sheet.Number("C1"));
        Assert.Equal(6, sheet.Number("D1"));
        Assert.Equal(3, sheet.Number("E1"));
        // A2 is a cell of the Spill Range, not its Anchor: it spills nothing, as A2# typed does not.
        Assert.Equal(ErrorValue.Ref, sheet.Error("F1"));

        sheet.Enter("A1", "=SEQUENCE(4)");

        Assert.Equal(10, sheet.Number("C1"));
        Assert.Equal(4, sheet.Number("E1"));

        sheet.Enter("A1", "7");

        Assert.Equal(ErrorValue.Ref, sheet.Error("C1"));

        sheet.Enter("G1", "=SEQUENCE(2,1,SUM(INDIRECT(\"RC#\",FALSE)))");
        Assert.Equal(ErrorValue.Circ, sheet.Error("G1"));
    }
}
