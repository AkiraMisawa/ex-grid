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
/// Sheet changes, and Linked Tables. Their answers to Excel's cases are in
/// <c>ExcelCases/subtotal.json</c> and <c>ExcelCases/aggregate.json</c>.
/// </summary>
public class SubtotalTests
{
    /// <summary>Names a public member of the engine would carry if a row could be hidden, filtered out or folded away.</summary>
    private static readonly Regex Hiding = new("hid(e|den|ing)|rowheight|filter(ed|s)?row|autofilter|collapse|outline(level|row)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

    [Fact] // ADR-0047 (2026-10-10): a cell that becomes a whole-Formula SUBTOTAL is passed over from then on, and one that stops is counted again
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

    [Theory] // ADR-0047 (2026-10-10), SH-68: any call of SUBTOTAL or AGGREGATE in the range but a whole-Formula one passed over is #VALUE!
    [InlineData("=SUBTOTAL(9,A1:A2)*2")]
    [InlineData("=(SUBTOTAL(9,A1:A2))")]
    [InlineData("=IF(TRUE,SUBTOTAL(9,A1:A2))")]
    [InlineData("=AGGREGATE(9,0,A1:A2)+0")]
    public void ADR0047_a_call_inside_a_larger_formula_is_refused(string nested)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("A3", nested);

        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=SUBTOTAL(9,A1:A3)").Error);
        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=AGGREGATE(9,6,A1:A3)").Error);
    }

    [Fact] // ADR-0047 (2026-10-10), ADR-0125: a cell spilled from a Formula that calls SUBTOTAL holds a value of a nested call, which is refused
    public void ADR0047_a_spill_from_a_formula_that_calls_subtotal_is_refused()
    {
        var sheet = NewSheet();
        sheet.Enter("C1", "1");
        sheet.Enter("A1", "=SEQUENCE(2,1,SUBTOTAL(9,C1))");
        Assert.Equal(2, sheet.Number("A2"));

        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=SUBTOTAL(9,A2)").Error);
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
