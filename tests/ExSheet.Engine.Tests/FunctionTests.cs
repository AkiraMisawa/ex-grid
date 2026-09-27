using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047 admits a function only with Excel's result, edge cases included (SH-7). Each table
/// below transcribes Microsoft's documentation for the function — how it treats blanks, text,
/// booleans and Error Values typed as arguments and found in ranges. A case whose Excel result is
/// not certain is left out, not guessed.
/// </summary>
public class FunctionTests
{
    /// <summary>
    /// The shared data:
    /// A1 1 · A2 2 · A3 '3 (text) · A4 TRUE · A5 blank · A6 abc · A7 4 ·
    /// C1 10 · C2 =1/0 · C3 5 · D1 ="" · E1 0 · E2 10.
    /// </summary>
    private static Sheet Data()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("A3", "'3");
        sheet.Enter("A4", "TRUE");
        sheet.Enter("A6", "abc");
        sheet.Enter("A7", "4");
        sheet.Enter("C1", "10");
        sheet.Enter("C2", "=1/0");
        sheet.Enter("C3", "5");
        sheet.Enter("D1", "=\"\"");
        sheet.Enter("E1", "0");
        sheet.Enter("E2", "10");
        return sheet;
    }

    private static void AssertValue(Value actual, object expected)
    {
        switch (expected)
        {
            case ErrorValue error:
                Assert.Equal(ValueKind.Error, actual.Kind);
                Assert.Equal(error, actual.Error);
                break;
            case bool boolean:
                Assert.Equal(ValueKind.Boolean, actual.Kind);
                Assert.Equal(boolean, actual.Boolean);
                break;
            case string text:
                Assert.Equal(ValueKind.Text, actual.Kind);
                Assert.Equal(text, actual.Text);
                break;
            default:
                Assert.Equal(ValueKind.Number, actual.Kind);
                Assert.Equal(Convert.ToDouble(expected, System.Globalization.CultureInfo.InvariantCulture), actual.Number, 14);
                break;
        }
    }

    [Theory] // ADR-0047: SUM — only numbers count in a range; typed numbers, booleans and numeric text count; errors are the result
    [InlineData("=SUM(A1:A7)", 7)]
    [InlineData("=SUM(1,2,3)", 6)]
    [InlineData("=SUM(\"3\",2,TRUE)", 6)]
    [InlineData("=SUM(A3)", 0)]
    [InlineData("=SUM(A4)", 0)]
    [InlineData("=SUM(A5)", 0)]
    [InlineData("=SUM(A1:A2,10)", 13)]
    [InlineData("=SUM(A:A)", 7)]
    [InlineData("=SUM(C1:C3)", ErrorValue.Div0)]
    [InlineData("=SUM(\"abc\")", ErrorValue.Value)]
    [InlineData("=SUM(1,#N/A)", ErrorValue.NA)]
    [InlineData("=SUM(D1)", 0)]
    public void Sum_matches_excel(string formula, object expected) => AssertValue(Data().Evaluate(formula), expected);

    [Theory] // ADR-0047: AVERAGE — zeros count, blanks, text and booleans in a range do not; nothing to average is #DIV/0!
    [InlineData("=AVERAGE(A1:A7)", 7.0 / 3)]
    [InlineData("=AVERAGE(E1:E2)", 5)]
    [InlineData("=AVERAGE(TRUE,3)", 2)]
    [InlineData("=AVERAGE(\"4\",2)", 3)]
    [InlineData("=AVERAGE(A5)", ErrorValue.Div0)]
    [InlineData("=AVERAGE(A3:A6)", ErrorValue.Div0)]
    [InlineData("=AVERAGE(C1:C3)", ErrorValue.Div0)]
    [InlineData("=AVERAGE(\"abc\",1)", ErrorValue.Value)]
    [InlineData("=AVERAGE(1,#N/A)", ErrorValue.NA)]
    public void Average_matches_excel(string formula, object expected) => AssertValue(Data().Evaluate(formula), expected);

    [Theory] // ADR-0047: MIN and MAX — no numbers gives 0; typed booleans count; untranslatable text is #VALUE!
    [InlineData("=MIN(A1:A7)", 1)]
    [InlineData("=MAX(A1:A7)", 4)]
    [InlineData("=MAX(A6)", 0)]
    [InlineData("=MIN(A5:A6)", 0)]
    [InlineData("=MAX(TRUE,-1)", 1)]
    [InlineData("=MIN(\"5\",7)", 5)]
    [InlineData("=MAX(-3,-7)", -3)]
    [InlineData("=MAX(\"abc\")", ErrorValue.Value)]
    [InlineData("=MIN(C1:C3)", ErrorValue.Div0)]
    [InlineData("=MAX(C1:C3)", ErrorValue.Div0)]
    public void Min_and_max_match_excel(string formula, object expected) => AssertValue(Data().Evaluate(formula), expected);

    [Theory] // ADR-0047: COUNT — numbers only in a range; typed booleans and numeric text count; errors are not counted and not errors
    [InlineData("=COUNT(A1:A7)", 3)]
    [InlineData("=COUNT(1,\"2\",TRUE,\"abc\")", 3)]
    [InlineData("=COUNT(C1:C3)", 2)]
    [InlineData("=COUNT(#N/A,1)", 1)]
    [InlineData("=COUNT(A5)", 0)]
    [InlineData("=COUNT(D1)", 0)]
    [InlineData("=COUNT(A:A)", 3)]
    public void Count_matches_excel(string formula, object expected) => AssertValue(Data().Evaluate(formula), expected);

    [Theory] // ADR-0047: COUNTA — everything but a blank cell, Error Values and empty text included
    [InlineData("=COUNTA(A1:A7)", 6)]
    [InlineData("=COUNTA(C1:C3)", 3)]
    [InlineData("=COUNTA(D1)", 1)]
    [InlineData("=COUNTA(1,\"a\",TRUE)", 3)]
    [InlineData("=COUNTA(A5)", 0)]
    [InlineData("=COUNTA(A1:E2)", 7)]
    public void CountA_matches_excel(string formula, object expected) => AssertValue(Data().Evaluate(formula), expected);

    [Theory] // ADR-0047: IF — a blank or 0 is FALSE, text is #VALUE!, an error is the result, and the branch not taken is not evaluated
    [InlineData("=IF(TRUE,1,2)", 1)]
    [InlineData("=IF(FALSE,1,2)", 2)]
    [InlineData("=IF(FALSE,1)", false)]
    [InlineData("=IF(1,\"y\",\"n\")", "y")]
    [InlineData("=IF(0,\"y\",\"n\")", "n")]
    [InlineData("=IF(-0.5,\"y\",\"n\")", "y")]
    [InlineData("=IF(A5,1,2)", 2)]
    [InlineData("=IF(A1>0,\"pos\",\"neg\")", "pos")]
    [InlineData("=IF(1/0,1,2)", ErrorValue.Div0)]
    [InlineData("=IF(\"abc\",1,2)", ErrorValue.Value)]
    [InlineData("=IF(TRUE,1,1/0)", 1)]
    [InlineData("=IF(FALSE,1/0,3)", 3)]
    [InlineData("=IF(A2=2,A7,A1)", 4)]
    public void If_matches_excel(string formula, object expected) => AssertValue(Data().Evaluate(formula), expected);

    [Theory] // ADR-0047: ROUND — half away from zero, negative digits round left of the point (Microsoft's own examples first)
    [InlineData("=ROUND(2.15,1)", 2.2)]
    [InlineData("=ROUND(2.149,1)", 2.1)]
    [InlineData("=ROUND(-1.475,2)", -1.48)]
    [InlineData("=ROUND(21.5,-1)", 20)]
    [InlineData("=ROUND(626.3,-3)", 1000)]
    [InlineData("=ROUND(1.98,-1)", 0)]
    [InlineData("=ROUND(-50.55,-2)", -100)]
    [InlineData("=ROUND(2.5,0)", 3)]
    [InlineData("=ROUND(-2.5,0)", -3)]
    [InlineData("=ROUND(0.5,0)", 1)]
    [InlineData("=ROUND(3.14159,3)", 3.142)]
    [InlineData("=ROUND(1234.5678,-2)", 1200)]
    [InlineData("=ROUND(0,2)", 0)]
    [InlineData("=ROUND(A5,2)", 0)]
    [InlineData("=ROUND(\"abc\",1)", ErrorValue.Value)]
    [InlineData("=ROUND(1/0,1)", ErrorValue.Div0)]
    [InlineData("=ROUND(1,1/0)", ErrorValue.Div0)]
    public void Round_matches_excel(string formula, object expected) => AssertValue(Data().Evaluate(formula), expected);

    [Theory] // ADR-0047: IFERROR — an Error Value gives the fallback; an empty cell is taken as "" (Microsoft's documentation)
    [InlineData("=IFERROR(1/0,\"x\")", "x")]
    [InlineData("=IFERROR(5,\"x\")", 5)]
    [InlineData("=IFERROR(#N/A,0)", 0)]
    [InlineData("=IFERROR(\"abc\"+1,-1)", -1)]
    [InlineData("=IFERROR(C2,\"bad\")", "bad")]
    [InlineData("=IFERROR(A6,\"x\")", "abc")]
    [InlineData("=IFERROR(A5,\"x\")", "")]
    [InlineData("=IFERROR(1/0,A5)", "")]
    public void IfError_matches_excel(string formula, object expected) => AssertValue(Data().Evaluate(formula), expected);

    [Theory] // ADR-0047: ISERROR — TRUE for any of Excel's Error Values, FALSE for everything else
    [InlineData("=ISERROR(1/0)", true)]
    [InlineData("=ISERROR(#N/A)", true)]
    [InlineData("=ISERROR(#REF!)", true)]
    [InlineData("=ISERROR(C2)", true)]
    [InlineData("=ISERROR(1)", false)]
    [InlineData("=ISERROR(A5)", false)]
    [InlineData("=ISERROR(\"x\")", false)]
    [InlineData("=ISERROR(A6)", false)]
    public void IsError_matches_excel(string formula, object expected) => AssertValue(Data().Evaluate(formula), expected);

    [Fact] // ADR-0047: a cycle is #CIRC! in every dependent; IFERROR and ISERROR cannot turn it into a value
    public void IfError_and_IsError_do_not_hide_a_cycle()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=B1");
        sheet.Enter("B1", "=A1");

        Assert.Equal(ErrorValue.Circ, sheet.Evaluate("=IFERROR(A1,0)").Error);
        Assert.Equal(ErrorValue.Circ, sheet.Evaluate("=ISERROR(A1)").Error);
        Assert.Equal(ErrorValue.Circ, sheet.Evaluate("=IF(TRUE,1,A1)").Error);
    }

    [Fact] // ADR-0047: a range where one Value is wanted is refused, and IFERROR does not turn the refusal into a fallback
    public void A_range_where_one_value_is_wanted_is_refused_uncaught()
    {
        var sheet = Data();

        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=IFERROR(A1:A2,0)").Error);
        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=ISERROR(A1:A2)").Error);
        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=IF(A1:A2,1,2)").Error);
    }

    /// <summary>
    /// G1:G5 Apple, Banana, Cherry, Banana, Date · H1:H5 10…50 ·
    /// J1:J5 5, 15, 25, 35, 45 · K1:K5 a…e · M1:O1 x, y, z · M2:O2 1, 2, 3 ·
    /// P1:P2 k1, k2 · Q1 7 · Q2 blank.
    /// </summary>
    private static Sheet LookupData()
    {
        var sheet = NewSheet();
        string[] fruit = ["Apple", "Banana", "Cherry", "Banana", "Date"];
        for (var i = 0; i < 5; i++)
        {
            sheet.Enter($"G{i + 1}", fruit[i]);
            sheet.Enter($"H{i + 1}", ((i + 1) * 10).ToString(System.Globalization.CultureInfo.InvariantCulture));
            sheet.Enter($"J{i + 1}", (5 + i * 10).ToString(System.Globalization.CultureInfo.InvariantCulture));
            sheet.Enter($"K{i + 1}", ((char)('a' + i)).ToString());
        }
        sheet.Enter("M1", "x");
        sheet.Enter("N1", "y");
        sheet.Enter("O1", "z");
        sheet.Enter("M2", "1");
        sheet.Enter("N2", "2");
        sheet.Enter("O2", "3");
        sheet.Enter("P1", "k1");
        sheet.Enter("P2", "k2");
        sheet.Enter("Q1", "7");
        return sheet;
    }

    [Theory] // ADR-0047/0049: XLOOKUP — Microsoft's documented match and search modes
    [InlineData("=XLOOKUP(\"Banana\",G1:G5,H1:H5)", 20)]
    [InlineData("=XLOOKUP(\"banana\",G1:G5,H1:H5)", 20)]
    [InlineData("=XLOOKUP(\"Banana\",G1:G5,H1:H5,,0,-1)", 40)]
    [InlineData("=XLOOKUP(\"Banana\",G1:G5,H1:H5,,0,1)", 20)]
    [InlineData("=XLOOKUP(\"Fig\",G1:G5,H1:H5)", ErrorValue.NA)]
    [InlineData("=XLOOKUP(\"Fig\",G1:G5,H1:H5,\"none\")", "none")]
    [InlineData("=XLOOKUP(\"Fig\",G1:G5,H1:H5,0)", 0)]
    [InlineData("=XLOOKUP(20,J1:J5,K1:K5,,-1)", "b")]
    [InlineData("=XLOOKUP(20,J1:J5,K1:K5,,1)", "c")]
    [InlineData("=XLOOKUP(25,J1:J5,K1:K5,,-1)", "c")]
    [InlineData("=XLOOKUP(25,J1:J5,K1:K5,,1)", "c")]
    [InlineData("=XLOOKUP(1,J1:J5,K1:K5,,-1)", ErrorValue.NA)]
    [InlineData("=XLOOKUP(99,J1:J5,K1:K5,,1)", ErrorValue.NA)]
    [InlineData("=XLOOKUP(20,J1:J5,K1:K5)", ErrorValue.NA)]
    [InlineData("=XLOOKUP(\"B*\",G1:G5,H1:H5,,2)", 20)]
    [InlineData("=XLOOKUP(\"?herry\",G1:G5,H1:H5,,2)", 30)]
    [InlineData("=XLOOKUP(\"*e\",G1:G5,H1:H5,,2)", 10)]
    [InlineData("=XLOOKUP(\"*e\",G1:G5,H1:H5,,2,-1)", 50)]
    [InlineData("=XLOOKUP(\"B*\",G1:G5,H1:H5)", ErrorValue.NA)]
    [InlineData("=XLOOKUP(\"y\",M1:O1,M2:O2)", 2)]
    [InlineData("=XLOOKUP(\"k2\",P1:P2,Q1:Q2)", 0)]
    [InlineData("=XLOOKUP(\"Cherry\",G:G,H:H)", 30)]
    [InlineData("=XLOOKUP(\"5\",J1:J5,K1:K5)", ErrorValue.NA)]
    [InlineData("=XLOOKUP(1/0,G1:G5,H1:H5)", ErrorValue.Div0)]
    [InlineData("=XLOOKUP(\"Banana\",G1:G5,H1:H4)", ErrorValue.Value)]
    public void XLookup_matches_excel(string formula, object expected) => AssertValue(LookupData().Evaluate(formula), expected);

    [Fact] // ADR-0047: a wildcard's ~ makes * and ? literal
    public void XLookup_tilde_escapes_a_wildcard()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "a*b");
        sheet.Enter("A2", "axb");
        sheet.Enter("B1", "1");
        sheet.Enter("B2", "2");

        Assert.Equal(2, sheet.Evaluate("=XLOOKUP(\"a?b\",A2:A2,B2:B2,,2)").Number);
        Assert.Equal(1, sheet.Evaluate("=XLOOKUP(\"a~*b\",A1:A2,B1:B2,,2,-1)").Number);
    }

    /// <summary>
    /// For XLOOKUP's binary search. A1:A5 5, 15, 25, 35, 45 (ascending) · B1:B5 a…e ·
    /// C1:C5 45, 35, 25, 15, 5 (descending) · D1:D5 apple, Banana, cherry, Date, fig (ascending
    /// without regard to case) · F1:F5 5, 15, 15, 35, 45 (a duplicated key) · G1:G5 5, 25, 15, 35, 45 (unsorted) ·
    /// H1:H5 5, "x", 25, 35, 45 (mixed kinds) · I1:I5 5, 15, blank, 35, 45 (a blank) ·
    /// J1:J2 FALSE, TRUE · K1:K3 "a-b", "a-c", "a-d" (text Excel collates in its own way).
    /// </summary>
    private static Sheet BinaryData()
    {
        var sheet = NewSheet();
        string[] fruit = ["apple", "Banana", "cherry", "Date", "fig"];
        double[] unsorted = [5, 25, 15, 35, 45];
        double[] duplicated = [5, 15, 15, 35, 45];
        for (var i = 0; i < 5; i++)
        {
            var n = 5 + (i * 10);
            sheet.Enter($"A{i + 1}", n.ToString(System.Globalization.CultureInfo.InvariantCulture));
            sheet.Enter($"B{i + 1}", ((char)('a' + i)).ToString());
            sheet.Enter($"C{i + 1}", (50 - n).ToString(System.Globalization.CultureInfo.InvariantCulture));
            sheet.Enter($"D{i + 1}", fruit[i]);
            sheet.Enter($"F{i + 1}", duplicated[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
            sheet.Enter($"G{i + 1}", unsorted[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
            sheet.Enter($"H{i + 1}", i == 1 ? "x" : n.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (i != 2) sheet.Enter($"I{i + 1}", n.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        sheet.Enter("J1", "FALSE");
        sheet.Enter("J2", "TRUE");
        sheet.Enter("K1", "a-b");
        sheet.Enter("K2", "a-c");
        sheet.Enter("K3", "a-d");
        return sheet;
    }

    [Theory] // ADR-0047: XLOOKUP's binary search answers over data sorted as its mode says, with a single matching key
    [InlineData("=XLOOKUP(25,A1:A5,B1:B5,,0,2)", "c")]
    [InlineData("=XLOOKUP(5,A1:A5,B1:B5,,0,2)", "a")]
    [InlineData("=XLOOKUP(45,A1:A5,B1:B5,,0,2)", "e")]
    [InlineData("=XLOOKUP(20,A1:A5,B1:B5,,0,2)", ErrorValue.NA)]
    [InlineData("=XLOOKUP(20,A1:A5,B1:B5,\"none\",0,2)", "none")]
    [InlineData("=XLOOKUP(20,A1:A5,B1:B5,,-1,2)", "b")]
    [InlineData("=XLOOKUP(20,A1:A5,B1:B5,,1,2)", "c")]
    [InlineData("=XLOOKUP(1,A1:A5,B1:B5,,-1,2)", ErrorValue.NA)]
    [InlineData("=XLOOKUP(99,A1:A5,B1:B5,,1,2)", ErrorValue.NA)]
    [InlineData("=XLOOKUP(99,A1:A5,B1:B5,,-1,2)", "e")]
    [InlineData("=XLOOKUP(25,C1:C5,B1:B5,,0,-2)", "c")]
    [InlineData("=XLOOKUP(20,C1:C5,B1:B5,,-1,-2)", "d")]
    [InlineData("=XLOOKUP(20,C1:C5,B1:B5,,1,-2)", "c")]
    [InlineData("=XLOOKUP(20,C1:C5,B1:B5,,0,-2)", ErrorValue.NA)]
    [InlineData("=XLOOKUP(\"CHERRY\",D1:D5,B1:B5,,0,2)", "c")]
    [InlineData("=XLOOKUP(\"coconut\",D1:D5,B1:B5,,-1,2)", "c")]
    [InlineData("=XLOOKUP(\"coconut\",D1:D5,B1:B5,,1,2)", "d")]
    [InlineData("=XLOOKUP(TRUE,J1:J2,B1:B2,,0,2)", "b")]
    [InlineData("=XLOOKUP(35,F1:F5,B1:B5,,0,2)", "d")]
    [InlineData("=XLOOKUP(20,F1:F5,B1:B5,,1,2)", "d")]
    public void XLookup_binary_search_over_sorted_data_matches_excel(string formula, object expected) =>
        AssertValue(BinaryData().Evaluate(formula), expected);

    [Theory] // ADR-0047: XLOOKUP's binary search is refused (#VALUE!) wherever its answer would depend on how Excel searches
    [InlineData("=XLOOKUP(25,C1:C5,B1:B5,,0,2)")]      // descending data, ascending mode
    [InlineData("=XLOOKUP(25,A1:A5,B1:B5,,0,-2)")]     // ascending data, descending mode
    [InlineData("=XLOOKUP(25,G1:G5,B1:B5,,0,2)")]      // unsorted
    [InlineData("=XLOOKUP(35,G1:G5,B1:B5,,0,2)")]      // unsorted, even where the key is present
    [InlineData("=XLOOKUP(25,H1:H5,B1:B5,,0,2)")]      // mixed kinds: Excel's ordering across kinds is not pinned
    [InlineData("=XLOOKUP(25,I1:I5,B1:B5,,0,2)")]      // a blank in the lookup array
    [InlineData("=XLOOKUP(\"x\",A1:A5,B1:B5,,0,2)")] // a lookup value of another kind
    [InlineData("=XLOOKUP(A99,A1:A5,B1:B5,,0,2)")]     // a blank lookup value
    [InlineData("=XLOOKUP(\"a-c\",K1:K3,B1:B3,,0,2)")] // text whose collation in Excel is not pinned
    [InlineData("=XLOOKUP(\"b*\",D1:D5,B1:B5,,2,2)")] // wildcard match
    [InlineData("=XLOOKUP(25,A1:A5,B1:B5,,0,3)")]      // not a search mode
    public void XLookup_binary_search_is_refused_where_excels_answer_is_not_pinned(string formula) =>
        Assert.Equal(ErrorValue.Value, BinaryData().Evaluate(formula).Error);

    [Fact] // ADR-0047: which of several equal keys Excel's binary search returns is UNVERIFIED (verify-on-windows.md, Part A, item 9) — refused, not guessed
    public void XLookup_binary_search_over_a_duplicated_key_is_refused()
    {
        var sheet = BinaryData();

        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=XLOOKUP(15,F1:F5,B1:B5,,0,2)").Error);
        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=XLOOKUP(20,F1:F5,B1:B5,,-1,2)").Error);
        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=XLOOKUP(10,F1:F5,B1:B5,,1,2)").Error);

        // Keys equal without regard to case are duplicates too.
        sheet.Enter("D2", "Banana");
        sheet.Enter("D3", "BANANA");
        sheet.Enter("D4", "cherry");
        sheet.Enter("D5", "Date");
        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=XLOOKUP(\"banana\",D1:D5,B1:B5,,0,2)").Error);
        Assert.Equal("a", sheet.Evaluate("=XLOOKUP(\"apple\",D1:D5,B1:B5,,0,2)").Text);
    }

    [Fact] // ADR-0047: a return array more than one cell across would spill, which ExSheet does not do
    public void XLookup_that_would_spill_is_refused()
    {
        Assert.Equal(ErrorValue.Value, LookupData().Evaluate("=XLOOKUP(\"Apple\",G1:G5,H1:J5)").Error);
    }

    [Fact] // ADR-0047: XLOOKUP's result is a Reference, so the aggregates read it as one
    public void XLookup_result_is_a_reference_to_the_aggregates()
    {
        var sheet = LookupData();

        Assert.Equal(20, sheet.Evaluate("=SUM(XLOOKUP(\"Banana\",G1:G5,H1:H5))").Number);
    }

    [Fact] // ADR-0047: a range argument is a dependency — changing a cell inside it recomputes, outside it does not
    public void A_range_is_a_dependency()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("B1", "=SUM(A1:A3)");

        var inside = sheet.Enter("A3", "4");
        var outside = sheet.Enter("A4", "8");

        Assert.Equal(7, sheet.Number("B1"));
        Assert.Equal(["B1"], inside.Recalculated.Addresses());
        Assert.Empty(outside.Recalculated);
    }

    [Fact] // ADR-0047 (SH-9): a range that includes its own cell is a cycle
    public void A_range_over_its_own_cell_is_circ()
    {
        var sheet = NewSheet();
        sheet.Enter("A2", "5");

        sheet.Enter("A1", "=SUM(A:A)");

        Assert.Equal(ErrorValue.Circ, sheet.Error("A1"));
    }

    [Theory] // ADR-0047: Excel refuses a call with the wrong number of arguments when it is entered
    [InlineData("=ROUND(1)")]
    [InlineData("=ROUND(1,2,3)")]
    [InlineData("=IF(TRUE)")]
    [InlineData("=IFERROR(1)")]
    [InlineData("=ISERROR()")]
    [InlineData("=XLOOKUP(1,A1:A2)")]
    [InlineData("=SUM()")]
    public void A_call_with_the_wrong_number_of_arguments_is_refused(string formula)
    {
        Assert.Throws<FormulaSyntaxException>(() => Entry.FromFormula(formula));
    }

    [Fact] // ADR-0047: a declared function is written back in upper case
    public void A_declared_function_is_written_in_upper_case()
    {
        Assert.Equal("=SUM(A1:B2)+XLOOKUP(1,A:A,B:B)", Entry.FromFormula("=sum(a1:b2)+xLookup(1,a:a,b:b)").Formula);
    }

    [Fact] // ADR-0047/0051: the declared list is exposed for completion and hints, and is exactly ADR-0047's set
    public void The_declared_functions_are_exactly_adr_0047s_set()
    {
        Assert.Equal(
            ["AVERAGE", "COUNT", "COUNTA", "IF", "IFERROR", "ISERROR", "MAX", "MIN", "ROUND", "SUM", "XLOOKUP"],
            DeclaredFunction.All.Select(f => f.Name));
        Assert.Equal("SUM(number1, [number2], ...)", DeclaredFunction.Find("sum")!.Signature);
        Assert.Equal(
            "XLOOKUP(lookup_value, lookup_array, return_array, [if_not_found], [match_mode], [search_mode])",
            DeclaredFunction.Find("XLOOKUP")!.Signature);
        Assert.Null(DeclaredFunction.Find("VLOOKUP"));
        Assert.All(DeclaredFunction.All, f => Assert.False(string.IsNullOrWhiteSpace(f.Description)));
    }
}
