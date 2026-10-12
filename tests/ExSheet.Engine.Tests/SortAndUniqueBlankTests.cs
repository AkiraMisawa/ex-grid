using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// A blank among <c>SORT</c>'s and <c>SORTBY</c>'s keys and in <c>UNIQUE</c>'s array, as the area pass of
/// 2026-10-12 read Excel (ADR-0125): a blank key sorts after every other key whichever the order, blanks keep
/// their order, and a blank spills as 0; in <c>UNIQUE</c> a blank is one of a kind only with another blank.
/// An Error Value among the keys stays refused with <c>#VALUE!</c> (ADR-0047). A blank in their array read
/// inside the Formula — by another function, an operator or a <c>LET</c>, anything but the spill and the
/// extent <c>ROWS</c> and <c>COLUMNS</c> read — has answers no run has observed, so the Formula is refused
/// with <c>#VALUE!</c> there (ADR-0125, ADR-0202). The cases themselves are in <c>ExcelCases/sort.json</c>,
/// <c>sortby.json</c> and <c>unique.json</c>; the rule across the forms is here.
/// </summary>
public class SortAndUniqueBlankTests
{
    /// <summary>The Values the Formula in ZZ1000 spills down its first column, a blank read as null.</summary>
    private static object?[] Spilled(Sheet sheet, int count) =>
        [.. Enumerable.Range(1000, count).Select(row => Plain(sheet.Value($"ZZ{row}")))];

    private static object? Plain(Value? value) => value switch
    {
        null => null,
        { Kind: ValueKind.Number } v => v.Number,
        { Kind: ValueKind.Text } v => v.Text,
        { Kind: ValueKind.Boolean } v => v.Boolean,
        { } v => v.Error,
    };

    [Theory] // ADR-0125 (SORT-011 to 014): a blank key sorts after every other key, in either order, and spills as 0
    [InlineData("=SORT(A1:A3)", "7", "", "3", new object[] { 3d, 7d, 0d })]
    [InlineData("=SORT(A1:A3,1,-1)", "7", "", "3", new object[] { 7d, 3d, 0d })]
    [InlineData("=SORT(A1:A3)", "b", "", "a", new object[] { "a", "b", 0d })]
    [InlineData("=SORT(A1:A3,1,-1)", "b", "", "a", new object[] { "b", "a", 0d })]
    [InlineData("=SORT(A1:A3)", "1", "", "TRUE", new object[] { 1d, true, 0d })]
    [InlineData("=SORT(A1:A3,1,-1)", "1", "", "TRUE", new object[] { true, 1d, 0d })]
    [InlineData("=SORT(A1:A3)", "", "0", "", new object[] { 0d, 0d, 0d })]
    public void ADR0125_a_blank_key_sorts_after_every_other_key_in_either_order(string formula, string a1, string a2, string a3, object[] expected)
    {
        var sheet = NewSheet();
        if (a1 != "") sheet.Enter("A1", a1);
        if (a2 != "") sheet.Enter("A2", a2);
        if (a3 != "") sheet.Enter("A3", a3);

        sheet.Enter("ZZ1000", formula);

        Assert.Equal(expected, Spilled(sheet, 3));
    }

    [Fact] // ADR-0125 (SORT-012, 014): a blank key is neither 0 nor empty text, either of which would sort first
    public void ADR0125_a_blank_key_sorts_after_0_and_after_empty_text()
    {
        var sheet = NewSheet();
        sheet.Enter("A2", "0");
        sheet.Enter("A3", "=\"\"");

        sheet.Enter("ZZ1000", "=SORT(A1:A3)");

        Assert.Equal(0, sheet.Number("ZZ1000"));
        Assert.Equal("", sheet.Value("ZZ1001")!.Value.Text);
        Assert.Equal(0, sheet.Number("ZZ1002"));
    }

    [Theory] // ADR-0125: blank keys keep their order, as equal keys do, whichever the order; the rest of each row moves with its key
    [InlineData("=SORT(A1:B3)")]
    [InlineData("=SORT(A1:B3,1,-1)")]
    public void ADR0125_blank_keys_keep_their_order(string formula)
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "x");
        sheet.Enter("B2", "y");
        sheet.Enter("A3", "1");
        sheet.Enter("B3", "z");

        sheet.Enter("ZZ1000", formula);

        Assert.Equal(1, sheet.Number("ZZ1000"));
        Assert.Equal(["z", "x", "y"], new[] { "AAA1000", "AAA1001", "AAA1002" }.Select(a => sheet.Value(a)!.Value.Text));
        Assert.Equal(0, sheet.Number("ZZ1001"));
        Assert.Equal(0, sheet.Number("ZZ1002"));
    }

    [Fact] // ADR-0125 (SORT-020): by_col, a blank key in the row sorts its column last
    public void ADR0125_a_blank_key_sorts_its_column_last_by_col()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "2");
        sheet.Enter("C1", "1");

        sheet.Enter("ZZ1000", "=SORT(A1:C1,1,1,TRUE)");

        Assert.Equal([1d, 2d, 0d], new[] { "ZZ1000", "AAA1000", "AAB1000" }.Select(sheet.Number));
    }

    [Theory] // ADR-0125 (SORTBY-007): SORTBY's blank keys sort as SORT's, last in either order
    [InlineData("=SORTBY(A1:A2,B1:B2)")]
    [InlineData("=SORTBY(A1:A2,B1:B2,-1)")]
    public void ADR0125_sortby_puts_a_blank_key_last_in_either_order(string formula)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "10");
        sheet.Enter("A2", "20");
        sheet.Enter("B2", "7");

        sheet.Enter("ZZ1000", formula);

        Assert.Equal([20d, 10d], Spilled(sheet, 2));
    }

    [Theory] // ADR-0125 (SORTBY-008): among equal first keys, a blank second key sorts last in either order; a blank first key sorts its line last whatever the second key
    [InlineData("=SORTBY(A1:A3,B1:B3,1,C1:C3,1)", new object[] { 20d, 10d, 30d })]
    [InlineData("=SORTBY(A1:A3,B1:B3,1,C1:C3,-1)", new object[] { 20d, 10d, 30d })]
    [InlineData("=SORTBY(A1:A3,C1:C3,1,B1:B3,1)", new object[] { 30d, 20d, 10d })]
    [InlineData("=SORTBY(A1:A3,C1:C3,-1,B1:B3,1)", new object[] { 20d, 30d, 10d })]
    public void ADR0125_sortby_places_blanks_key_by_key(string formula, object[] expected)
    {
        // A: the values 10, 20, 30. B: x, x, blank. C: blank, 5, 1.
        var sheet = NewSheet();
        sheet.Enter("A1", "10");
        sheet.Enter("A2", "20");
        sheet.Enter("A3", "30");
        sheet.Enter("B1", "x");
        sheet.Enter("B2", "x");
        sheet.Enter("C2", "5");
        sheet.Enter("C3", "1");

        sheet.Enter("ZZ1000", formula);

        Assert.Equal(expected, Spilled(sheet, 3));
    }

    [Theory] // ADR-0125 (SORTBY-009): two blank first keys are equal, so the second key orders them, in either of its orders
    [InlineData(-1, new object[] { 30d, 20d, 10d })]
    [InlineData(1, new object[] { 30d, 10d, 20d })]
    public void ADR0125_sortby_orders_blank_first_keys_by_the_next_key(int order, object[] expected)
    {
        // A: the values 10, 20, 30. B: blank, blank, 1. C: 1, 2, blank.
        var sheet = NewSheet();
        sheet.Enter("A1", "10");
        sheet.Enter("A2", "20");
        sheet.Enter("A3", "30");
        sheet.Enter("B3", "1");
        sheet.Enter("C1", "1");
        sheet.Enter("C2", "2");

        sheet.Enter("ZZ1000", $"=SORTBY(A1:A3,B1:B3,1,C1:C3,{order})");

        Assert.Equal(expected, Spilled(sheet, 3));
    }

    [Theory] // ADR-0047: an Error Value among SORT's and SORTBY's keys stays refused with #VALUE! until Excel is asked (SORT-015)
    [InlineData("=SORT(A1:A2)")]
    [InlineData("=SORT(A1:A2,1,-1)")]
    [InlineData("=SORTBY(B1:B2,A1:A2)")]
    public void ADR0047_an_error_value_among_the_keys_is_refused(string formula)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "7");
        sheet.Enter("A2", "=NA()");
        sheet.Enter("B1", "1");
        sheet.Enter("B2", "2");

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    [Theory] // ADR-0125 (UNIQUE-011 to 013): a blank is one of a kind only with another blank, never with 0 or empty text
    [InlineData("", "0", 2)]
    [InlineData("0", "", 2)]
    [InlineData("", "=\"\"", 2)]
    [InlineData("", "", 1)]
    public void ADR0125_unique_counts_a_blank_one_of_a_kind_only_with_a_blank(string a1, string a2, double rows)
    {
        var sheet = NewSheet();
        if (a1 != "") sheet.Enter("A1", a1);
        if (a2 != "") sheet.Enter("A2", a2);

        Assert.Equal(rows, sheet.Evaluate("=ROWS(UNIQUE(A1:A2))").Number);
    }

    [Fact] // ADR-0125 (UNIQUE-009, 010, 012): UNIQUE keeps a blank where it first appears, and it spills as 0
    public void ADR0125_unique_spills_a_blank_as_0()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "7");
        sheet.Enter("A3", "7");
        sheet.Enter("A5", "3");

        sheet.Enter("ZZ1000", "=UNIQUE(A1:A5)");

        Assert.Equal([7d, 0d, 3d, null], Spilled(sheet, 4));
    }

    [Fact] // ADR-0125 (UNIQUE-015): rows are compared whole, a blank against a blank only
    public void ADR0125_unique_compares_rows_with_blanks_whole()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "a");
        sheet.Enter("A2", "a");
        sheet.Enter("B2", "0");
        sheet.Enter("A3", "a");

        Assert.Equal(2, sheet.Evaluate("=ROWS(UNIQUE(A1:B3))").Number);
        Assert.Equal(2, sheet.Evaluate("=COLUMNS(UNIQUE(A1:B3))").Number);
    }

    [Fact] // ADR-0125 (UNIQUE-014): exactly_once counts blanks as one kind, so two blanks are left out
    public void ADR0125_unique_exactly_once_leaves_out_repeated_blanks()
    {
        var sheet = NewSheet();
        sheet.Enter("A3", "1");

        sheet.Enter("ZZ1000", "=UNIQUE(A1:A3,,TRUE)");

        Assert.Equal([1d, null], Spilled(sheet, 2));
        Assert.Equal(ErrorValue.Calc, sheet.Evaluate("=UNIQUE(A1:A2,,TRUE)").Error);
        Assert.Equal(0, sheet.Evaluate("=UNIQUE(A2:A3,,TRUE)").Number);
    }

    [Fact] // ADR-0125 (UNIQUE-016): one blank cell spills as 0, in UNIQUE and in SORT
    public void ADR0125_one_blank_cell_is_0()
    {
        var sheet = NewSheet();

        Assert.Equal(0, sheet.Evaluate("=UNIQUE(A1)").Number);
        Assert.Equal(0, sheet.Evaluate("=SORT(A1)").Number);
    }

    /// <summary>A1 a, A2 blank, A3 b; B1 to B3 the keys 3, 2, 1; C1 to C3 x, blank, y.</summary>
    private static Sheet WithABlank()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "a");
        sheet.Enter("A3", "b");
        sheet.Enter("B1", "3");
        sheet.Enter("B2", "2");
        sheet.Enter("B3", "1");
        sheet.Enter("C1", "x");
        sheet.Enter("C3", "y");
        return sheet;
    }

    [Theory] // ADR-0125, ADR-0202: a blank in UNIQUE's, SORT's or SORTBY's array read inside the Formula is refused with #VALUE!, by a function, an operator or a LET
    [InlineData("=COUNTA(UNIQUE(A1:A3))")]
    [InlineData("=COUNTA(SORT(A1:A3))")]
    [InlineData("=COUNTA(SORTBY(A1:A3,B1:B3))")]
    [InlineData("=INDEX(UNIQUE(A1:A3),2)=\"\"")]
    [InlineData("=ISBLANK(INDEX(SORT(A1:A3),3))")]
    [InlineData("=SUM(--(LEN(UNIQUE(A1:A3))>0))")]
    [InlineData("=UNIQUE(A1:A3)&\"\"")]
    [InlineData("=SORT(A1:A3)+0")]
    [InlineData("=-SORTBY(A1:A3,B1:B3)")]
    [InlineData("=SORT(UNIQUE(A1:A3))")]
    [InlineData("=UNIQUE(SORT(A1:A3))")]
    [InlineData("=TRANSPOSE(UNIQUE(A1:A3))")]
    [InlineData("=LET(x,UNIQUE(A1:A3),x)")]
    [InlineData("=LET(x,SORT(A1:A3),ROWS(x))")]
    [InlineData("=INDEX(SORT(A1:A3),1)")]
    public void ADR0125_a_blank_read_inside_the_formula_is_refused(string formula)
    {
        var sheet = WithABlank();

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    [Theory] // ADR-0125, ADR-0202: a blank in a column SORT or SORTBY does not sort by is in the result too, and is refused there as a blank key is
    [InlineData("=INDEX(SORT(B1:C3),1,2)")]
    [InlineData("=INDEX(SORTBY(C1:C3,B1:B3),1)")]
    [InlineData("=COUNTA(UNIQUE(B1:C3))")]
    public void ADR0125_a_blank_anywhere_in_the_result_is_refused_inside_the_formula(string formula)
    {
        var sheet = WithABlank();

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    [Theory] // ADR-0125, ADR-0202: the refusal is the Formula's, so IFERROR cannot catch it, nor ISERROR, ISBLANK or COUNTA read it as an answer
    [InlineData("=IFERROR(COUNTA(UNIQUE(A1:A3)),0)")]
    [InlineData("=IFERROR(SORT(A1:A3)+0,0)")]
    [InlineData("=ISERROR(INDEX(UNIQUE(A1:A3),2))")]
    [InlineData("=IFNA(SORTBY(A1:A3,B1:B3)&\"\",\"x\")")]
    [InlineData("=IF(ISERROR(COUNTA(SORT(A1:A3))),1,2)")]
    public void ADR0125_the_refusal_cannot_be_caught(string formula)
    {
        var sheet = WithABlank();

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    [Fact] // ADR-0125: what the verifier found contradicting itself now answers once: COUNTA reads the spilled 0, and is refused where it would read the blank inside the Formula
    public void ADR0125_counta_reads_the_spill_and_refuses_the_blank_inside_the_formula()
    {
        var sheet = WithABlank();
        sheet.Enter("D1", "=UNIQUE(A1:A3)");
        sheet.Enter("E1", "=COUNTA(D1#)");
        sheet.Enter("E2", "=COUNTA(UNIQUE(A1:A3))");

        Assert.Equal(0, sheet.Number("D2"));
        Assert.Equal(3, sheet.Number("E1"));
        Assert.Equal(ErrorValue.Value, sheet.Error("E2"));
    }

    [Theory] // ADR-0125 (SORT-012, UNIQUE-010, 011): the spill, and the extent ROWS and COLUMNS read, are answered as the area pass read them
    [InlineData("=ROWS(UNIQUE(A1:A3))", 3)]
    [InlineData("=ROWS(SORT(A1:A3))", 3)]
    [InlineData("=COLUMNS(SORTBY(A1:A3,B1:B3))", 1)]
    [InlineData("=ROWS(UNIQUE(A1:A3))+1", 4)]
    [InlineData("=ROWS((UNIQUE(A1:A3)))", 3)]
    [InlineData("=IF(TRUE,ROWS(SORT(A1:A3)))", 3)]
    public void ADR0125_rows_and_columns_count_a_blank_without_reading_it(string formula, double expected)
    {
        var sheet = WithABlank();

        Assert.Equal(expected, sheet.Evaluate(formula).Number);
    }

    [Theory] // ADR-0125 (SORT-012, UNIQUE-010): the Formula's whole expression spills its blank as 0, parentheses around it or not
    [InlineData("=UNIQUE(A1:A3)", new object[] { "a", 0d, "b" })]
    [InlineData("=(UNIQUE(A1:A3))", new object[] { "a", 0d, "b" })]
    [InlineData("=((SORT(A1:A3)))", new object[] { "a", "b", 0d })]
    [InlineData("=SORTBY(A1:A3,B1:B3)", new object[] { "b", 0d, "a" })]
    public void ADR0125_the_formulas_whole_expression_spills_its_blank(string formula, object[] expected)
    {
        var sheet = WithABlank();

        sheet.Enter("ZZ1000", formula);

        Assert.Equal(expected, Spilled(sheet, 3));
    }

    [Theory] // ADR-0125: a result without a blank has nothing to refuse, a blank key among SORTBY's included, which orders the lines and is not in what SORTBY gives
    [InlineData("=INDEX(SORTBY(B1:B3,C1:C3),3)", 2d)]
    [InlineData("=INDEX(UNIQUE(B1:B3),2)", 2d)]
    [InlineData("=INDEX(SORT(A1:A2&\"\"),1)", "")]
    [InlineData("=COUNTA(UNIQUE(A1:A3&\"\"))", 3d)]
    public void ADR0125_a_result_without_a_blank_is_read_inside_the_formula(string formula, object expected)
    {
        var sheet = WithABlank();

        Assert.Equal(expected, Plain(sheet.Evaluate(formula)));
    }

    [Theory] // ADR-0125, ADR-0181: a call never evaluated reads nothing, so it refuses nothing
    [InlineData("=IF(FALSE,COUNTA(UNIQUE(A1:A3)),1)")]
    [InlineData("=LET(x,SORT(A1:A3),1)")]
    [InlineData("=IFERROR(1,COUNTA(SORT(A1:A3)))")]
    public void ADR0125_a_call_never_evaluated_refuses_nothing(string formula)
    {
        var sheet = WithABlank();

        Assert.Equal(1, sheet.Evaluate(formula).Number);
    }

    [Fact] // ADR-0125, ADR-0124: a Formula computed on demand inside another keeps its own refusal and its own whole expression
    public void ADR0125_a_formula_computed_on_demand_keeps_its_own_refusal()
    {
        var sheet = NewSheet();
        sheet.Enter("D1", "a");
        sheet.Enter("D3", "b");
        sheet.Enter("A6", "=COUNTA(UNIQUE(D1:D3))");
        sheet.Enter("A7", "=ROWS(UNIQUE(D1:D3))");
        sheet.Enter("A8", "=UNIQUE(D1:D3)");
        sheet.Enter("B1", "=ROWS(UNIQUE(D1:D3))+ISERROR(INDIRECT(\"A6\"))");
        sheet.Enter("B2", "=COUNTA(UNIQUE(D1:D3))+INDIRECT(\"A7\")");
        sheet.Enter("B3", "=INDIRECT(\"A7\")+ROWS(INDIRECT(\"A8#\"))");

        sheet.Enter("D1", "c");

        Assert.Equal(ErrorValue.Value, sheet.Error("A6"));
        Assert.Equal(3, sheet.Number("A7"));
        Assert.Equal(0, sheet.Number("A9"));
        Assert.Equal(4, sheet.Number("B1"));
        Assert.Equal(ErrorValue.Value, sheet.Error("B2"));
        Assert.Equal(6, sheet.Number("B3"));
    }
}
