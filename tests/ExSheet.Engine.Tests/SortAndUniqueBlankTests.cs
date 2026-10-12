using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// A blank among <c>SORT</c>'s and <c>SORTBY</c>'s keys and in <c>UNIQUE</c>'s array, as the area passes of
/// 2026-10-12 read Excel (ADR-0125): a blank key sorts after every other key whichever the order, blanks keep
/// their order, and a blank spills as 0; in <c>UNIQUE</c> a blank is one of a kind only with another blank.
/// Read inside the Formula, the blank <c>SORT</c> and <c>UNIQUE</c> keep is the empty value a blank cell holds:
/// <c>COUNTA</c> counts it in the array (SORT-021, UNIQUE-017), and taken out by <c>INDEX</c> it is blank to
/// <c>ISBLANK</c> and equal to empty text (SORT-022, UNIQUE-018). <c>SORTBY</c>, <c>FILTER</c> and
/// <c>TRANSPOSE</c> are taken to keep the same blank, which no run has read (SORTBY-011 to 013, FILTER-012 and
/// 013, TRANSPOSE-005 and 006 ask). The other functions and operators that read one Value read it as a blank
/// cell's, a generalisation of <c>ISBLANK</c>'s and <c>=</c>'s answers; <c>SUM</c>, <c>SUMPRODUCT</c> and
/// <c>CONCAT</c> take it because every reading of it gives them the one answer, and a lookup takes an array
/// holding it where it never reads it. Where a function's reading of it parts (<c>AVERAGE</c>, <c>MIN</c>,
/// <c>COUNT</c>, <c>COUNTA</c> given it as one Value, a search through it, another of the five) the Formula is
/// refused with <c>#VALUE!</c> (ADR-0125, ADR-0202). An Error Value key is answered in <c>SORT</c> only where it
/// already follows every number, the shape of SORT-015, where sorting it after the numbers and keeping its
/// place give the one order; it is refused with <c>#VALUE!</c> elsewhere (ADR-0047). The cases themselves are
/// in <c>ExcelCases/sort.json</c>, <c>sortby.json</c>, <c>unique.json</c>, <c>filter.json</c> and
/// <c>transpose.json</c>; the rule across the forms is here.
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

    [Theory] // ADR-0047 (SORT-015, 027): SORT answers an Error Value key that already follows every number, where sorting it after the numbers and keeping its place give the one order
    [InlineData("=SORT(A1:A3)", "7", "3", "=NA()", new object[] { 3d, 7d, ErrorValue.NA })]
    [InlineData("=SORT(A1:A3)", "7", "=NA()", "=NA()", new object[] { 7d, ErrorValue.NA, ErrorValue.NA })]
    [InlineData("=SORT(A1:A3)", "=NA()", "=NA()", "=NA()", new object[] { ErrorValue.NA, ErrorValue.NA, ErrorValue.NA })]
    [InlineData("=SORT(A1:A3,1,-1)", "=1/0", "=1/0", "=1/0", new object[] { ErrorValue.Div0, ErrorValue.Div0, ErrorValue.Div0 })]
    [InlineData("=SORT(A1:B3)", "7", "3", "=NA()", new object[] { 3d, 7d, ErrorValue.NA })]
    public void ADR0047_sort_answers_an_error_key_that_follows_every_number(string formula, string a1, string a2, string a3, object[] expected)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", a1);
        sheet.Enter("A2", a2);
        sheet.Enter("A3", a3);

        sheet.Enter("ZZ1000", formula);

        Assert.Equal(expected, Spilled(sheet, 3));
    }

    [Fact] // ADR-0047 (SORT-015): the rest of an Error Value key's line goes with it, and the lines of the numbers before it are sorted
    public void ADR0047_an_error_keys_line_goes_with_it()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "9");
        sheet.Enter("B1", "x");
        sheet.Enter("A2", "3");
        sheet.Enter("B2", "y");
        sheet.Enter("A3", "=NA()");
        sheet.Enter("B3", "z");

        sheet.Enter("ZZ1000", "=SORT(A1:B3)");

        Assert.Equal(["y", "x", "z"], new[] { "AAA1000", "AAA1001", "AAA1002" }.Select(a => sheet.Value(a)!.Value.Text));
    }

    [Theory] // ADR-0047 (SORT-026, 028 to 032, SORTBY-010): an Error Value key stays refused with #VALUE! where the readings SORT-015 leaves part: a number after it, which sorting it after the numbers moves and keeping its place does not, another kind beside it, a descending sort, two Error Values, and SORTBY
    [InlineData("=SORT(A1:A2)", "=NA()", "7", "")]
    [InlineData("=SORT(A1:A3)", "7", "=NA()", "3")]
    [InlineData("=SORT(A1:A3)", "=NA()", "7", "=NA()")]
    [InlineData("=SORT(A1:A2,1,-1)", "7", "=NA()", "")]
    [InlineData("=SORT(A1:A2)", "a", "=NA()", "")]
    [InlineData("=SORT(A1:A2)", "TRUE", "=NA()", "")]
    [InlineData("=SORT(A1:A2)", "=NA()", "", "")]
    [InlineData("=SORT(A1:A2)", "=NA()", "=1/0", "")]
    [InlineData("=SORT(A1:A2,1,-1)", "=NA()", "=1/0", "")]
    [InlineData("=SORTBY(B1:B2,A1:A2)", "7", "=NA()", "")]
    [InlineData("=SORTBY(B1:B2,A1:A2,-1)", "=NA()", "=NA()", "")]
    [InlineData("=SORTBY(B1:B2,C1:C2,1,A1:A2,1)", "7", "=NA()", "")]
    public void ADR0047_an_error_key_is_refused_where_the_readings_part(string formula, string a1, string a2, string a3)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", a1);
        if (a2 != "") sheet.Enter("A2", a2);
        if (a3 != "") sheet.Enter("A3", a3);
        sheet.Enter("B1", "1");
        sheet.Enter("B2", "2");
        sheet.Enter("C1", "x");
        sheet.Enter("C2", "x");

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    [Theory] // ADR-0047, ADR-0202: the refusal of an Error Value key is the Formula's, so nothing around it reads it as an answer: IFERROR would show one 0 where SORT spills two Values, and COUNTA would count one of SORT's two
    [InlineData("=IFERROR(SORT(A1:A2),0)")]
    [InlineData("=IFERROR(INDEX(SORT(A1:A2),1),0)")]
    [InlineData("=ISERROR(SORT(A1:A2))")]
    [InlineData("=COUNTA(SORT(A1:A2))")]
    [InlineData("=IFERROR(SORTBY(B1:B2,A1:A2),0)")]
    public void ADR0047_an_error_keys_refusal_cannot_be_caught(string formula)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=NA()");
        sheet.Enter("A2", "7");
        sheet.Enter("B1", "1");
        sheet.Enter("B2", "2");

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    [Fact] // ADR-0047: an Error Value in a column SORT does not sort by is no key, so it is answered as any Value is
    public void ADR0047_an_error_outside_the_key_is_carried()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "2");
        sheet.Enter("B1", "=NA()");
        sheet.Enter("A2", "1");
        sheet.Enter("B2", "x");

        sheet.Enter("ZZ1000", "=SORT(A1:B2)");

        Assert.Equal("x", sheet.Value("AAA1000")!.Value.Text);
        Assert.Equal(ErrorValue.NA, sheet.Error("AAA1001"));
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

    /// <summary>D1 5, D2 blank, D3 7: numbers with a blank between them.</summary>
    private static Sheet WithNumbersAndABlank()
    {
        var sheet = NewSheet();
        sheet.Enter("D1", "5");
        sheet.Enter("D3", "7");
        return sheet;
    }

    [Theory] // ADR-0125 (SORT-021, UNIQUE-017; SORTBY-011, FILTER-012, TRANSPOSE-005 ask): COUNTA counts a blank kept in the array, as Excel counted SORT's and UNIQUE's
    [InlineData("=COUNTA(UNIQUE(A1:A3))", 3d)]
    [InlineData("=COUNTA(SORT(A1:A3))", 3d)]
    [InlineData("=COUNTA(SORTBY(A1:A3,B1:B3))", 3d)]
    [InlineData("=COUNTA(FILTER(A1:A3,B1:B3>0))", 3d)]
    [InlineData("=COUNTA(FILTER(A1:C3,B1:B3<3))", 6d)]
    [InlineData("=COUNTA(TRANSPOSE(A1:A3))", 3d)]
    [InlineData("=COUNTA(TRANSPOSE(B1:C3))", 6d)]
    [InlineData("=COUNTA(UNIQUE(B1:C3))", 6d)]
    [InlineData("=COUNTA(SORT(A1:A3),A1:A3)", 5d)]
    [InlineData("=LET(x,UNIQUE(A1:A3),COUNTA(x))", 3d)]
    [InlineData("=COUNTA(INDEX(SORT(A1:C3),0,1))", 3d)]
    public void ADR0125_counta_counts_a_kept_blank_in_the_array(string formula, double expected)
    {
        var sheet = WithABlank();

        Assert.Equal(expected, sheet.Evaluate(formula).Number);
    }

    [Theory] // ADR-0125 (SORT-022, UNIQUE-018; UNIQUE-020, SORTBY-012, FILTER-013, TRANSPOSE-006 ask): taken out by INDEX, the kept blank is the empty value a blank cell holds, and one-Value readers read it so
    [InlineData("=ISBLANK(INDEX(SORT(A1:A3),3))", true)]
    [InlineData("=ISBLANK(INDEX(UNIQUE(A1:A3),2))", true)]
    [InlineData("=ISBLANK(INDEX(SORTBY(A1:A3,B1:B3),2))", true)]
    [InlineData("=ISBLANK(INDEX(FILTER(A1:A3,B1:B3>0),2))", true)]
    [InlineData("=ISBLANK(INDEX(TRANSPOSE(A1:A3),1,2))", true)]
    [InlineData("=INDEX(FILTER(C1:C3,B1:B3>0),2)&\"x\"", "x")]
    [InlineData("=LEN(INDEX(TRANSPOSE(A1:A3),1,2))", 0d)]
    [InlineData("=INDEX(UNIQUE(A1:A3),2)=\"\"", true)]
    [InlineData("=INDEX(UNIQUE(A1:A3),2)=0", true)]
    [InlineData("=ISTEXT(INDEX(SORT(A1:A3),3))", false)]
    [InlineData("=ISNUMBER(INDEX(SORT(A1:A3),3))", false)]
    [InlineData("=ISERROR(INDEX(UNIQUE(A1:A3),2))", false)]
    [InlineData("=LEN(INDEX(UNIQUE(A1:A3),2))", 0d)]
    [InlineData("=INDEX(SORT(A1:A3),3)+1", 1d)]
    [InlineData("=INDEX(SORT(A1:A3),3)&\"x\"", "x")]
    [InlineData("=INDEX(SORT(A1:A3),3)", 0d)]
    [InlineData("=INDEX(SORT(A1:A3),1)", "a")]
    [InlineData("=INDEX(SORT(B1:C3),1,2)", "y")]
    [InlineData("=ISBLANK(INDEX(SORT(B1:C3),2,2))", true)]
    [InlineData("=ISBLANK(INDEX(UNIQUE(A2),1))", true)]
    public void ADR0125_a_kept_blank_taken_out_is_read_as_a_blank_cells_value(string formula, object expected)
    {
        var sheet = WithABlank();

        Assert.Equal(expected, Plain(sheet.Evaluate(formula)));
    }

    [Theory] // ADR-0125 (UNIQUE-018, 019, 024): an operator reads each Value of the array, the kept blank as a blank cell's
    [InlineData("=SUM(--(UNIQUE(A1:A3)=\"\"))", 1d)]
    [InlineData("=SUM(--(SORT(A1:A3)=0))", 1d)]
    [InlineData("=SUMPRODUCT(--(SORTBY(A1:A3,B1:B3)<>\"\"))", 2d)]
    public void ADR0125_an_operator_reads_a_kept_blank_as_a_blank_cells_value(string formula, double expected)
    {
        var sheet = WithABlank();

        Assert.Equal(expected, sheet.Evaluate(formula).Number);
    }

    [Fact] // ADR-0125 (UNIQUE-024): & reads the kept blank as empty text, element by element, and the result spills
    public void ADR0125_concatenation_spills_a_kept_blank_as_empty_text()
    {
        var sheet = WithABlank();

        sheet.Enter("ZZ1000", "=UNIQUE(A1:A3)&\"\"");

        Assert.Equal(["a", "", "b"], Spilled(sheet, 3));
    }

    [Theory] // ADR-0125 (SORT-033): SUM, SUMPRODUCT and CONCAT add nothing for a kept blank, whichever way they read it, so they take it
    [InlineData("=SUM(UNIQUE(D1:D3))", 12d)]
    [InlineData("=SUM(SORT(D1:D3),1)", 13d)]
    [InlineData("=SUM(INDEX(SORT(D1:D3),3))", 0d)]
    [InlineData("=SUM(UNIQUE(D2))", 0d)]
    [InlineData("=SUMPRODUCT(SORT(D1:D3),SORT(D1:D3))", 74d)]
    [InlineData("=CONCAT(SORTBY(D1:D3,D1:D3,-1))", "75")]
    [InlineData("=SUM(FILTER(D1:D3,D1:D3<>1))", 12d)]
    [InlineData("=SUMPRODUCT(TRANSPOSE(D1:D3),TRANSPOSE(D1:D3))", 74d)]
    [InlineData("=CONCAT(FILTER(D1:D3,D1:D3<>1))", "57")]
    public void ADR0125_sum_sumproduct_and_concat_take_a_kept_blank(string formula, object expected)
    {
        var sheet = WithNumbersAndABlank();

        Assert.Equal(expected, Plain(sheet.Evaluate(formula)));
    }

    [Theory] // ADR-0125, ADR-0047 (SORT-033, 037): SUM refuses where passing over a kept blank and adding it as 0 give different sums: the last addition decides whether a sum that nearly cancels is 0
    [InlineData("=SUM(SORT(E1:E4))", null)]
    [InlineData("=SUM(SORT(E1:E4,1,-1))", null)]
    [InlineData("=SUM(SORT(E1:E4),0)", 2.7755575615628914E-17)]
    [InlineData("=SUM(SORTBY(E1:E4,F1:F4))", 0d)]
    [InlineData("=SUM(E1:E4)", 0d)]
    [InlineData("=SUM(SORT(E1:E3))", 0d)]
    public void ADR0125_sum_refuses_where_a_kept_blank_moves_its_last_addition(string formula, double? expected)
    {
        // E: 0.1, 0.2, -0.3, blank. F: the keys 2, 3, 1, 0, which put the blank first.
        var sheet = NewSheet();
        sheet.Enter("E1", "0.1");
        sheet.Enter("E2", "0.2");
        sheet.Enter("E3", "-0.3");
        sheet.Enter("F1", "2");
        sheet.Enter("F2", "3");
        sheet.Enter("F3", "1");
        sheet.Enter("F4", "0");

        var value = sheet.Evaluate(formula);

        if (expected is { } number) Assert.Equal(number, value.Number);
        else Assert.Equal(ErrorValue.Value, value.Error);
    }

    [Theory] // ADR-0125, ADR-0202 (SORT-034, 035, UNIQUE-021 to 023): where a function's reading of a kept blank parts, the Formula is refused with #VALUE!
    [InlineData("=AVERAGE(UNIQUE(D1:D3))")]
    [InlineData("=MIN(SORT(D1:D3))")]
    [InlineData("=MAX(SORTBY(D1:D3,D1:D3))")]
    [InlineData("=COUNT(UNIQUE(D1:D3))")]
    [InlineData("=PRODUCT(SORT(D1:D3))")]
    [InlineData("=AND(SORT(D1:D3))")]
    [InlineData("=TEXTJOIN(\",\",FALSE,SORT(D1:D3))")]
    [InlineData("=COUNTA(INDEX(SORT(D1:D3),3))")]
    [InlineData("=COUNTA(UNIQUE(D2))")]
    [InlineData("=AVERAGE(INDEX(SORT(D1:D3),3),1)")]
    [InlineData("=MATCH(7,SORT(D1:D3),0)")]
    [InlineData("=XLOOKUP(7,D1:D3,SORT(D1:D3))")]
    [InlineData("=SORT(UNIQUE(D1:D3))")]
    [InlineData("=UNIQUE(SORT(D1:D3))")]
    [InlineData("=TRANSPOSE(UNIQUE(D1:D3))")]
    [InlineData("=FILTER(SORT(D1:D3),D1:D3>0)")]
    [InlineData("=AVERAGE(FILTER(D1:D3,D1:D3<>1))")]
    [InlineData("=MIN(TRANSPOSE(D1:D3))")]
    [InlineData("=COUNT(FILTER(D1:D3,D1:D3<>1))")]
    [InlineData("=COUNTA(SORT(FILTER(D1:D3,D1:D3<>1)))")]
    [InlineData("=SORT(TRANSPOSE(D1:D3),1,1,TRUE)")]
    [InlineData("=MATCH(7,TRANSPOSE(D1:D3),0)")]
    [InlineData("=XLOOKUP(7,FILTER(D1:D3,D1:D3<>1),D1:D3)")]
    [InlineData("=IF(TRUE,SORT(D1:D3))")]
    [InlineData("=CHOOSE(1,UNIQUE(D1:D3))")]
    [InlineData("=LEN(UNIQUE(D1:D3))")]
    [InlineData("=ISBLANK(SORT(D1:D3))")]
    public void ADR0125_a_kept_blank_read_where_the_readings_part_is_refused(string formula)
    {
        var sheet = WithNumbersAndABlank();

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    [Theory] // ADR-0125, ADR-0202: the refusal is the Formula's, so IFERROR cannot catch it, nor ISERROR or ISBLANK read it as an answer
    [InlineData("=IFERROR(AVERAGE(UNIQUE(D1:D3)),0)")]
    [InlineData("=IFERROR(LEN(UNIQUE(D1:D3)),0)")]
    [InlineData("=ISERROR(MIN(SORT(D1:D3)))")]
    [InlineData("=IFNA(COUNT(SORTBY(D1:D3,D1:D3)),\"x\")")]
    [InlineData("=IF(ISERROR(COUNTA(INDEX(SORT(D1:D3),3))),1,2)")]
    [InlineData("=IFERROR(INDEX(SORT(D1:D3),3),0)")]
    [InlineData("=IFERROR(AVERAGE(FILTER(D1:D3,D1:D3<>1)),0)")]
    [InlineData("=ISERROR(MIN(TRANSPOSE(D1:D3)))")]
    public void ADR0125_the_refusal_cannot_be_caught(string formula)
    {
        var sheet = WithNumbersAndABlank();

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    /// <summary>A1 to A3 the keys 1, 2, 3; B1 10, B2 blank, B3 30.</summary>
    private static Sheet WithKeysAndABlankValue()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("A3", "3");
        sheet.Enter("B1", "10");
        sheet.Enter("B3", "30");
        return sheet;
    }

    [Theory] // ADR-0125: a lookup over an array that holds a kept blank answers where it never reads the blank, every reading of it giving the one answer
    [InlineData("=VLOOKUP(1,SORT(A1:B3),2,FALSE)", 10d)]
    [InlineData("=VLOOKUP(3,SORT(A1:B3),2,TRUE)", 30d)]
    [InlineData("=HLOOKUP(3,TRANSPOSE(A1:B3),2,FALSE)", 30d)]
    [InlineData("=XLOOKUP(3,A1:A3,FILTER(B1:B3,A1:A3>0))", 30d)]
    [InlineData("=SUM(XLOOKUP(1,A1:A3,FILTER(A1:B3,A1:A3>0)))", 11d)]
    public void ADR0125_a_lookup_answers_where_it_never_reads_a_kept_blank(string formula, double expected)
    {
        var sheet = WithKeysAndABlankValue();

        Assert.Equal(expected, sheet.Evaluate(formula).Number);
    }

    [Theory] // ADR-0125, ADR-0202: a lookup is refused where the kept blank is what it returns, or among the keys it searches: how either reads it no run has read
    [InlineData("=VLOOKUP(2,SORT(A1:B3),2,FALSE)")]
    [InlineData("=HLOOKUP(2,TRANSPOSE(A1:B3),2,FALSE)")]
    [InlineData("=VLOOKUP(30,SORT(B1:B3),1,FALSE)")]
    [InlineData("=XLOOKUP(2,A1:A3,FILTER(B1:B3,A1:A3>0))")]
    [InlineData("=SUM(XLOOKUP(2,A1:A3,FILTER(A1:B3,A1:A3>0)))")]
    [InlineData("=XLOOKUP(30,FILTER(B1:B3,A1:A3>0),A1:A3)")]
    [InlineData("=VLOOKUP(1,UNIQUE(B2),1,FALSE)")]
    public void ADR0125_a_lookup_that_reads_a_kept_blank_is_refused(string formula)
    {
        var sheet = WithKeysAndABlankValue();

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    [Fact] // ADR-0125 (SORT-021, UNIQUE-017): what the verifier found contradicting itself now agrees: COUNTA counts the spilled 0 and the kept blank alike
    public void ADR0125_counta_counts_the_spill_and_the_kept_blank_alike()
    {
        var sheet = WithABlank();
        sheet.Enter("D1", "=UNIQUE(A1:A3)");
        sheet.Enter("E1", "=COUNTA(D1#)");
        sheet.Enter("E2", "=COUNTA(UNIQUE(A1:A3))");

        Assert.Equal(0, sheet.Number("D2"));
        Assert.Equal(3, sheet.Number("E1"));
        Assert.Equal(3, sheet.Number("E2"));
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

    [Theory] // ADR-0125 (SORT-012, UNIQUE-010, SORTBY-013): the Formula's whole expression spills its blank as 0, parentheses or a LET around it or not
    [InlineData("=UNIQUE(A1:A3)", new object[] { "a", 0d, "b" })]
    [InlineData("=(UNIQUE(A1:A3))", new object[] { "a", 0d, "b" })]
    [InlineData("=((SORT(A1:A3)))", new object[] { "a", "b", 0d })]
    [InlineData("=SORTBY(A1:A3,B1:B3)", new object[] { "b", 0d, "a" })]
    [InlineData("=LET(x,SORT(A1:A3),x)", new object[] { "a", "b", 0d })]
    [InlineData("=INDEX(SORT(A1:C3),0,3)", new object[] { "x", "y", 0d })]
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
    [InlineData("=AVERAGE(SORT(B1:B3))", 2d)]
    [InlineData("=MIN(UNIQUE(B1:B3))", 1d)]
    public void ADR0125_a_result_without_a_blank_is_read_inside_the_formula(string formula, object expected)
    {
        var sheet = WithABlank();

        Assert.Equal(expected, Plain(sheet.Evaluate(formula)));
    }

    [Theory] // ADR-0125, ADR-0181: a call never evaluated reads nothing, so it refuses nothing
    [InlineData("=IF(FALSE,AVERAGE(UNIQUE(A1:A3)),1)")]
    [InlineData("=LET(x,SORT(A1:A3),y,MIN(x),1)")]
    [InlineData("=IFERROR(1,COUNT(SORT(A1:A3)))")]
    public void ADR0125_a_call_never_evaluated_refuses_nothing(string formula)
    {
        var sheet = WithABlank();

        Assert.Equal(1, sheet.Evaluate(formula).Number);
    }

    [Fact] // ADR-0125, ADR-0124: a Formula computed on demand inside another keeps its own refusal
    public void ADR0125_a_formula_computed_on_demand_keeps_its_own_refusal()
    {
        var sheet = NewSheet();
        sheet.Enter("D1", "1");
        sheet.Enter("D3", "2");
        sheet.Enter("A6", "=AVERAGE(UNIQUE(D1:D3))");
        sheet.Enter("A7", "=ROWS(UNIQUE(D1:D3))");
        sheet.Enter("A8", "=UNIQUE(D1:D3)");
        sheet.Enter("B1", "=ROWS(UNIQUE(D1:D3))+ISERROR(INDIRECT(\"A6\"))");
        sheet.Enter("B2", "=AVERAGE(UNIQUE(D1:D3))+INDIRECT(\"A7\")");
        sheet.Enter("B3", "=INDIRECT(\"A7\")+ROWS(INDIRECT(\"A8#\"))");

        sheet.Enter("D1", "3");

        Assert.Equal(ErrorValue.Value, sheet.Error("A6"));
        Assert.Equal(3, sheet.Number("A7"));
        Assert.Equal(0, sheet.Number("A9"));
        Assert.Equal(4, sheet.Number("B1"));
        Assert.Equal(ErrorValue.Value, sheet.Error("B2"));
        Assert.Equal(6, sheet.Number("B3"));
    }
}
