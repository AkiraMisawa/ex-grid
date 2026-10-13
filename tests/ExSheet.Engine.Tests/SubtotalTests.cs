using System.Reflection;
using System.Text.RegularExpressions;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// <c>SUBTOTAL</c> and <c>AGGREGATE</c> (ADR-0047's note of 2026-10-10, docs/specs/exsheet-functions
/// ticket 16, SH-68): admitted while a Sheet cannot hide a row. What the Excel case corpus cannot
/// state is here: the test that fails on the day a row can be hidden, what a nested call is as the
/// Sheet changes and across the forms of a Formula that calls one (the seventeenth Windows run read
/// Excel's rule), and Linked Tables. Their answers to Excel's cases are in
/// <c>ExcelCases/subtotal.json</c> and <c>ExcelCases/aggregate.json</c>.
/// </summary>
public class SubtotalTests
{
    /// <summary>Names a public member of the engine would carry if a row could be hidden, filtered out or folded away.</summary>
    private static readonly Regex Hiding = new("hid(e|den|ing)|rowheight|visib|filter(ed|s)?row|autofilter|collapse|outline(level|row)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string RowsCanBeHidden(string what) =>
        $"A row can now be hidden: {what}. SUBTOTAL's 101 to 111 and AGGREGATE's options 1, 3, 5 and 7 were admitted only while no row could be, "
        + "and give what 1 to 11 and options 0, 2, 4 and 6 give. Open ADR-0047's note of 2026-10-10, \"The catalogue's last Decide rows\", "
        + "and decide with the user how they pass over a hidden or filtered row. Do not make this test pass by changing its expectation.";

    [Fact] // ADR-0047 (2026-10-10), SH-68: fails on the day a row can be hidden, and says to revisit SUBTOTAL and AGGREGATE
    public void ADR0047_subtotal_and_aggregate_skip_nothing_while_no_row_can_be_hidden()
    {
        var hiding = typeof(Sheet).Assembly.GetExportedTypes()
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(member => $"{type.Name}.{member.Name}")
                .Prepend(type.Name))
            .Where(name => Hiding.IsMatch(name))
            .ToList();
        Assert.True(hiding.Count == 0, RowsCanBeHidden($"the engine has {string.Join(", ", hiding)}"));

        var sheet = NewSheet();
        foreach (var (address, typed) in new[] { ("A1", "1"), ("A2", "2"), ("A3", "'3"), ("A4", "TRUE"), ("A6", "abc"), ("A7", "4"), ("A8", "=1/0") })
        {
            sheet.Enter(address, typed);
        }
        for (var function = 1; function <= 11; function++)
        {
            var shown = sheet.Evaluate($"=SUBTOTAL({function},A1:A7)");
            var skippingHidden = sheet.Evaluate($"=SUBTOTAL({100 + function},A1:A7)");
            Assert.True(shown.Equals(skippingHidden), RowsCanBeHidden($"SUBTOTAL({100 + function}) gave {skippingHidden} where SUBTOTAL({function}) gave {shown}"));
        }
        foreach (var option in new[] { 1, 3, 5, 7 })
        {
            var shown = sheet.Evaluate($"=AGGREGATE(9,{option - 1},A1:A8)");
            var skippingHidden = sheet.Evaluate($"=AGGREGATE(9,{option},A1:A8)");
            Assert.True(shown.Equals(skippingHidden), RowsCanBeHidden($"AGGREGATE's option {option} gave {skippingHidden} where option {option - 1} gave {shown}"));
        }
    }

    [Fact] // ADR-0047 (2026-10-10), SH-68: a cell that becomes a nested SUBTOTAL is passed over from then on, and one that stops is counted again
    public void ADR0047_a_cell_that_becomes_a_nested_subtotal_is_passed_over()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("A3", "3");
        sheet.Enter("B1", "=SUBTOTAL(9,A1:A3)");
        sheet.Enter("B2", "=AGGREGATE(9,0,A1:A3)");
        Assert.Equal(6, sheet.Number("B1"));
        Assert.Equal(6, sheet.Number("B2"));

        sheet.Enter("A3", "=SUBTOTAL(9,A1:A2)");

        Assert.Equal(3, sheet.Number("B1"));
        Assert.Equal(3, sheet.Number("B2"));

        sheet.Enter("A3", "=SUM(A1:A2)");

        Assert.Equal(6, sheet.Number("B1"));
        Assert.Equal(6, sheet.Number("B2"));
    }

    [Theory] // ADR-0047, SH-68 (the seventeenth Windows run: SUBTOTAL-020, 021, 035 to 037, AGGREGATE-026, 052 to 054): a cell whose Formula calls SUBTOTAL or AGGREGATE anywhere is nested; SUBTOTAL and AGGREGATE's options 0 to 3 pass over it, and options 4 to 7 count it
    [InlineData("=SUBTOTAL(9,A1:A2)")]
    [InlineData("=AGGREGATE(9,0,A1:A2)")]
    [InlineData("=SUBTOTAL(9,A1:A2)*2")]
    [InlineData("=1+SUBTOTAL(9,A1:A2)")]
    [InlineData("=(SUBTOTAL(9,A1:A2))")]
    [InlineData("=IF(TRUE,SUBTOTAL(9,A1:A2))")]
    [InlineData("=AGGREGATE(9,0,A1:A2)+0")]
    [InlineData("=LET(x,SUBTOTAL(9,A1:A2),x+1)")]
    public void ADR0047_SH68_a_call_anywhere_in_a_cells_formula_is_nested(string nested)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("A3", nested);
        var counted = 3 + sheet.Number("A3");

        Assert.Equal(3, sheet.Evaluate("=SUBTOTAL(9,A1:A3)").Number);
        Assert.Equal(3, sheet.Evaluate("=SUBTOTAL(109,A1:A3)").Number);
        Assert.Equal(2, sheet.Evaluate("=SUBTOTAL(2,A1:A3)").Number);
        for (var option = 0; option <= 3; option++)
        {
            Assert.Equal(3, sheet.Evaluate($"=AGGREGATE(9,{option},A1:A3)").Number);
        }
        for (var option = 4; option <= 7; option++)
        {
            Assert.Equal(counted, sheet.Evaluate($"=AGGREGATE(9,{option},A1:A3)").Number);
        }
    }

    [Fact] // ADR-0047, SH-68: what counts as nested is the Formula's text, so a call in a branch IF never takes is nested too (SUBTOTAL-039 asks Excel)
    public void ADR0047_SH68_a_call_in_a_branch_never_taken_is_nested_by_its_text()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("A3", "=IF(FALSE,SUBTOTAL(9,A1:A2),5)");

        Assert.Equal(3, sheet.Evaluate("=SUBTOTAL(9,A1:A3)").Number);
        Assert.Equal(8, sheet.Evaluate("=AGGREGATE(9,6,A1:A3)").Number);
    }

    [Theory] // ADR-0047, SH-68 (the seventeenth Windows run: AGGREGATE-025, 042 to 051): AGGREGATE's options 4 to 7 count a nested SUBTOTAL or AGGREGATE as its Value, in the reference form and the array form, where options 0 to 3 pass over it
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void ADR0047_SH68_aggregate_options_4_to_7_count_a_nested_cell(int option)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("A3", "=SUBTOTAL(9,A1:A2)");
        sheet.Enter("B1", "1");
        sheet.Enter("B2", "2");
        sheet.Enter("B3", "=AGGREGATE(9,0,B1:B2)");

        Assert.Equal(6, sheet.Evaluate($"=AGGREGATE(9,{option},A1:A3)").Number);
        Assert.Equal(3, sheet.Evaluate($"=AGGREGATE(14,{option},A1:A3,1)").Number);
        Assert.Equal(6, sheet.Evaluate($"=AGGREGATE(9,{option},B1:B3)").Number);
        // The option it stands beside, 0 to 3, passes over the same cells.
        Assert.Equal(3, sheet.Evaluate($"=AGGREGATE(9,{option - 4},A1:A3)").Number);
        Assert.Equal(2, sheet.Evaluate($"=AGGREGATE(14,{option - 4},A1:A3,1)").Number);
        Assert.Equal(3, sheet.Evaluate($"=AGGREGATE(9,{option - 4},B1:B3)").Number);
    }

    [Fact] // ADR-0047, ADR-0125, SH-68 (the seventeenth Windows run: SUBTOTAL-038): a cell spilled from a Formula that calls SUBTOTAL is nested, as its Anchor is
    public void ADR0047_SH68_a_cell_spilled_from_a_formula_that_calls_subtotal_is_nested()
    {
        var sheet = NewSheet();
        sheet.Enter("C1", "1");
        sheet.Enter("A1", "=SEQUENCE(2,1,SUBTOTAL(9,C1))");
        Assert.Equal(2, sheet.Number("A2"));

        Assert.Equal(0, sheet.Evaluate("=SUBTOTAL(9,A2)").Number);
        Assert.Equal(0, sheet.Evaluate("=AGGREGATE(9,2,A1:A2)").Number);
        Assert.Equal(3, sheet.Evaluate("=AGGREGATE(9,6,A1:A2)").Number);
    }

    [Theory] // ADR-0047 (the seventeenth Windows run: SUBTOTAL-027): a function number that is not whole is truncated toward zero, and the range is checked after; AGGREGATE's function number and option the same way (AGGREGATE-055, 056 ask Excel)
    [InlineData("=SUBTOTAL(9.5,A1:A3)", "=SUBTOTAL(9,A1:A3)")]
    [InlineData("=SUBTOTAL(9.99,A1:A3)", "=SUBTOTAL(9,A1:A3)")]
    [InlineData("=SUBTOTAL(1.5,A1:A3)", "=SUBTOTAL(1,A1:A3)")]
    [InlineData("=SUBTOTAL(11.9,A1:A3)", "=SUBTOTAL(11,A1:A3)")]
    [InlineData("=SUBTOTAL(109.5,A1:A3)", "=SUBTOTAL(109,A1:A3)")]
    [InlineData("=AGGREGATE(9.5,6,A1:A4)", "=AGGREGATE(9,6,A1:A4)")]
    [InlineData("=AGGREGATE(9,6.9,A1:A4)", "=AGGREGATE(9,6,A1:A4)")]
    [InlineData("=AGGREGATE(14.5,6,A1:A4,1)", "=AGGREGATE(14,6,A1:A4,1)")]
    public void ADR0047_a_function_number_that_is_not_whole_is_truncated(string fractional, string whole)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("A3", "4");
        sheet.Enter("A4", "=1/0");
        var expected = sheet.Evaluate(whole);
        Assert.Equal(ValueKind.Number, expected.Kind);

        Assert.Equal(expected, sheet.Evaluate(fractional));
    }

    [Theory] // ADR-0047: truncated, a function number outside Excel's ranges is still #VALUE!
    [InlineData("=SUBTOTAL(0.5,A1:A3)")]
    [InlineData("=SUBTOTAL(11.5+1,A1:A3)")]
    [InlineData("=SUBTOTAL(100.5,A1:A3)")]
    [InlineData("=SUBTOTAL(-9.5,A1:A3)")]
    [InlineData("=AGGREGATE(19.5+1,6,A1:A3)")]
    [InlineData("=AGGREGATE(9,7.5+1,A1:A3)")]
    [InlineData("=AGGREGATE(9,-0.5-1,A1:A3)")]
    public void ADR0047_a_truncated_function_number_outside_the_range_is_value(string formula)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    [Fact] // ADR-0047 (the seventeenth Windows run: AGGREGATE-039): the array form takes a number given directly as an array of that one Value
    public void ADR0047_the_array_form_takes_a_number_given_directly()
    {
        var sheet = NewSheet();

        Assert.Equal(5, sheet.Evaluate("=AGGREGATE(14,6,5,1)").Number);
        Assert.Equal(5, sheet.Evaluate("=AGGREGATE(15,4,2+3,1)").Number);
        Assert.Equal(ErrorValue.Num, sheet.Evaluate("=AGGREGATE(14,6,5,2)").Error);
    }

    [Theory] // ADR-0047 (the area pass of 2026-10-12: AGGREGATE-061): text or a boolean given directly to the array form is an array of that one Value, which holds no number, so LARGE and SMALL are #NUM!, under every option
    [InlineData("=AGGREGATE(14,6,\"5\",1)")]
    [InlineData("=AGGREGATE(14,4,\"5\",1)")]
    [InlineData("=AGGREGATE(15,6,\"abc\",1)")]
    [InlineData("=AGGREGATE(14,6,TRUE,1)")]
    [InlineData("=AGGREGATE(15,0,FALSE,1)")]
    [InlineData("=AGGREGATE(15,6,\"\",1)")]
    public void ADR0047_the_array_form_takes_text_or_a_boolean_given_directly_as_no_number(string formula)
    {
        Assert.Equal(ErrorValue.Num, NewSheet().Evaluate(formula).Error);
    }

    [Fact] // ADR-0047: an Error Value given directly to the array form is its result, as an Error Value argument is, even under an option that passes over Error Values, until Excel is asked (AGGREGATE-067)
    public void ADR0047_an_error_given_directly_is_the_array_forms_result()
    {
        var sheet = NewSheet();

        Assert.Equal(ErrorValue.Div0, sheet.Evaluate("=AGGREGATE(14,6,1/0,1)").Error);
        Assert.Equal(ErrorValue.NA, sheet.Evaluate("=AGGREGATE(15,4,NA(),1)").Error);
    }

    [Fact] // ADR-0047 (2026-10-10), ADR-0049: a Linked Table's column is a reference to SUBTOTAL and AGGREGATE, and they wait for its data
    public void ADR0047_subtotal_reads_a_linked_tables_column()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"]);
        sheet.Enter("A1", "=SUBTOTAL(9,Rates[Mid])");
        sheet.Enter("A2", "=AGGREGATE(4,6,Rates[Mid])");
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A2"));

        sheet.PushLinkedTable("Rates",
        [
            [Value.FromText("EURUSD"), Value.FromNumber(1.5)],
            [Value.FromText("USDJPY"), Value.FromError(ErrorValue.NA)],
            [Value.FromText("GBPUSD"), Value.FromNumber(2.5)],
        ]);

        Assert.Equal(ErrorValue.NA, sheet.Error("A1"));
        Assert.Equal(2.5, sheet.Number("A2"));
    }

    [Fact] // ADR-0047: a SUBTOTAL whose range holds its own cell reads a cycle, and is #CIRC!
    public void ADR0047_a_subtotal_over_its_own_cell_is_circ()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A3", "=SUBTOTAL(9,A1:A3)");

        Assert.Equal(ErrorValue.Circ, sheet.Error("A3"));
    }

    [Theory] // ADR-0047: Excel refuses a call with too few arguments when it is entered
    [InlineData("=SUBTOTAL(9)")]
    [InlineData("=AGGREGATE(9,6)")]
    public void ADR0047_too_few_arguments_are_refused_on_entry(string formula)
    {
        var sheet = NewSheet();

        Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A1", formula));
    }
}
