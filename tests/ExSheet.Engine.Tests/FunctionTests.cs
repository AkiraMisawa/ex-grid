using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047 admits a function only with Excel's result, edge cases included (SH-7). What each
/// function answers is in the Excel case corpus (<c>ExcelCases/*.json</c>, run by
/// <see cref="ExcelCaseTests"/> and by the Excel oracle); what stays here is the engine's own
/// surface: dependencies, entry-time refusals and the declared list.
/// </summary>
public class FunctionTests
{
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

    [Theory] // ADR-0047: XLOOKUP's match_mode 3 accepts only constructs PCRE2 and .NET read alike; any other pattern is #VALUE!
    [InlineData("(?:a)")]      // a non-capturing group
    [InlineData("(?i)a")]      // an inline option
    [InlineData("(?<n>a)")]    // a named group
    [InlineData("a*+")]        // a possessive quantifier
    [InlineData("a{,2}")]      // an upper bound alone: PCRE2 10.43 reads it, .NET does not
    [InlineData("a{x}")]       // a brace that is not a quantifier
    [InlineData("a}")]
    [InlineData("a]")]
    [InlineData("[[:alpha:]]")] // a POSIX class
    [InlineData("[a-[b]]")]    // .NET's class subtraction
    [InlineData("[a-c-e]")]    // a hyphen after a range
    [InlineData("[\\d-z]")]    // a range from a class
    [InlineData("[z-a]")]      // a range out of order
    [InlineData("[]")]
    [InlineData("\\D")]
    [InlineData("\\W")]
    [InlineData("\\S")]
    [InlineData("\\B")]
    [InlineData("\\n")]
    [InlineData("\\x41")]
    [InlineData("\\A")]
    [InlineData("*a")]         // a quantifier with nothing to repeat
    [InlineData("^*")]
    [InlineData("a**")]
    [InlineData("(a")]
    [InlineData("a)")]
    [InlineData("a\\")]
    [InlineData("\U0001F600")] // outside the Basic Multilingual Plane
    public void A_regular_expression_outside_the_accepted_set_is_refused(string pattern)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "a");
        sheet.Enter("B1", "x");
        sheet.Enter("C1", "'" + pattern);

        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=XLOOKUP(C1,A1:A1,B1:B1,,3)").Error);
    }

    [Theory] // ADR-0047: the accepted constructs, with PCRE2's meaning where .NET's would differ
    [InlineData("^a.c$", "abc", true)]
    [InlineData("^a.c$", "a\nc", false)]              // . does not match a newline
    [InlineData("^.$", "\U0001F600", true)]           // . matches a code point, as PCRE2 in UTF mode
    [InlineData("^[^a]$", "\U0001F600", true)]        // so does a negated class
    [InlineData("^\\d+$", "١٢", false)]     // \d is ASCII: Arabic-Indic digits are not digits
    [InlineData("^\\w+$", "café", false)]        // \w is ASCII
    [InlineData("\\bcaf\\b", "café", true)]      // so \b sits before an accented letter
    [InlineData("^\\s$", " ", false)]           // \s is ASCII whitespace
    [InlineData("^[a\\-z]+$", "a-z", true)]
    [InlineData("^[-a]+$", "-a", true)]
    [InlineData("^[a-]+$", "a-", true)]
    [InlineData("^a{2,3}?$", "aaa", true)]
    [InlineData("^\\(1\\)$", "(1)", true)]
    [InlineData("^a b#$", "a b#", true)]              // spaces and # are literal
    [InlineData("A", "a", false)]                     // case-sensitive
    public void A_regular_expression_in_the_accepted_set_matches_as_PCRE2(string pattern, string text, bool matches)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "'" + text);
        sheet.Enter("B1", "x");
        sheet.Enter("C1", "'" + pattern);

        var result = sheet.Evaluate("=XLOOKUP(C1,A1:A1,B1:B1,,3)");
        Assert.Equal(matches ? Value.FromText("x") : Value.FromError(ErrorValue.NA), result);
    }
}
