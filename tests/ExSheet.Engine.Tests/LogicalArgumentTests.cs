using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// What <c>AND</c>, <c>OR</c> and <c>XOR</c> make of text typed as an argument (ADR-0047: a function
/// answers as Excel, or not at all). The seventeenth Windows run read Excel's <c>AND("abc",TRUE)</c> as
/// TRUE (AND-011): text that spells neither TRUE nor FALSE is passed over, as text inside a range is.
/// The three read their arguments alike, so the rule holds for each; OR-009 and XOR-006 ask Excel.
/// </summary>
public class LogicalArgumentTests
{
    [Theory] // ADR-0047 (AND-011, the seventeenth Windows run): typed text that spells no truth is passed over
    [InlineData("=AND(\"abc\",TRUE)", true)]
    [InlineData("=AND(TRUE,\"abc\",FALSE)", false)]
    [InlineData("=AND(\"\",1)", true)]
    [InlineData("=OR(\"abc\",FALSE)", false)]
    [InlineData("=OR(\"abc\",TRUE)", true)]
    [InlineData("=XOR(\"abc\",TRUE)", true)]
    [InlineData("=XOR(\"abc\",TRUE,TRUE)", false)]
    public void ADR0047_and_or_xor_pass_over_typed_text_that_spells_no_truth(string formula, bool expected)
    {
        var sheet = NewSheet();

        Assert.Equal(expected, sheet.Evaluate(formula).Boolean);
    }

    [Theory] // ADR-0047 (AND-011, AND-014): typed text alone leaves nothing that counts, which is #VALUE!
    [InlineData("=AND(\"abc\")")]
    [InlineData("=OR(\"abc\",\"\")")]
    [InlineData("=XOR(\"abc\")")]
    public void ADR0047_and_or_xor_over_typed_text_alone_are_value_errors(string formula)
    {
        var sheet = NewSheet();

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    // ADR-0047: the engine's answer, waiting on AND-015 and OR-010. Typed text that spells TRUE or FALSE, in
    // either case, counts; Excel was seen only to pass over text that spells neither (AND-011), and AND-010
    // is TRUE whether "TRUE" counts or is passed over, so no observation settles this half of the rule yet.
    [Theory]
    [InlineData("=AND(\"FALSE\",TRUE)", false)]
    [InlineData("=OR(\"true\",FALSE)", true)]
    [InlineData("=XOR(\"True\",FALSE)", true)]
    public void ADR0047_and_or_xor_count_typed_text_that_spells_a_truth(string formula, bool expected)
    {
        var sheet = NewSheet();

        Assert.Equal(expected, sheet.Evaluate(formula).Boolean);
    }

    [Theory] // ADR-0047 (AND-013): passing text over never hides an Error Value, wherever it stands
    [InlineData("=AND(\"abc\",1/0)")]
    [InlineData("=OR(1/0,\"abc\",TRUE)")]
    [InlineData("=XOR(\"abc\",TRUE,1/0)")]
    public void ADR0047_and_or_xor_give_an_error_beside_passed_over_text(string formula)
    {
        var sheet = NewSheet();

        Assert.Equal(ErrorValue.Div0, sheet.Evaluate(formula).Error);
    }
}
