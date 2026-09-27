using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// Excel's operators. Each row is Excel's documented or observed result; a case whose Excel result
/// is not certain is left out rather than guessed (ADR-0047).
/// </summary>
public class OperatorTests
{
    [Theory] // ADR-0047: Excel's precedence — negation, %, ^, * /, + -, &, comparison
    [InlineData("=-2^2", 4)]            // negation binds before ^ in Excel
    [InlineData("=2^3^2", 64)]          // ^ is left-associative in Excel
    [InlineData("=2^-1", 0.5)]
    [InlineData("=2*3^2", 18)]
    [InlineData("=50%", 0.5)]
    [InlineData("=10+50%", 10.5)]
    [InlineData("=2^200%", 4)]
    [InlineData("=-50%", -0.5)]
    [InlineData("=1+2*3^2", 19)]
    public void Arithmetic_precedence_is_excels(string formula, double expected)
    {
        Assert.Equal(expected, NewSheet().Evaluate(formula).Number, 15);
    }

    [Theory] // ADR-0047: comparison binds loosest, & below + -
    [InlineData("=1+1=2", true)]
    [InlineData("=1&2=\"12\"", true)]
    public void Comparison_binds_loosest(string formula, bool expected)
    {
        Assert.Equal(expected, NewSheet().Evaluate(formula).Boolean);
    }

    [Theory] // ADR-0047: & joins text; a number is written as General, a boolean as TRUE or FALSE, a blank as nothing
    [InlineData("=\"a\"&\"b\"", "ab")]
    [InlineData("=\"a\"&1+1", "a2")]
    [InlineData("=1&2", "12")]
    [InlineData("=1.5&\"\"", "1.5")]
    [InlineData("=TRUE&\"\"", "TRUE")]
    [InlineData("=A1&\"x\"", "x")]
    [InlineData("=1/4&\"\"", "0.25")]
    public void Concatenation_writes_text(string formula, string expected)
    {
        Assert.Equal(expected, NewSheet().Evaluate(formula).Text);
    }

    [Theory] // ADR-0047: Excel's comparisons — numbers before text before booleans, text without regard to case
    [InlineData("=1=1", true)]
    [InlineData("=1<2", true)]
    [InlineData("=2<=2", true)]
    [InlineData("=3>=4", false)]
    [InlineData("=1<>1", false)]
    [InlineData("=\"a\"=\"A\"", true)]
    [InlineData("=\"apple\"<\"Banana\"", true)]
    [InlineData("=\"abc\"<\"abd\"", true)]
    [InlineData("=1=\"1\"", false)]
    [InlineData("=TRUE=1", false)]
    [InlineData("=TRUE>FALSE", true)]
    [InlineData("=TRUE>1", true)]
    [InlineData("=\"a\">1", true)]
    [InlineData("=TRUE>\"a\"", true)]
    [InlineData("=A1=0", true)]         // a blank equals 0 against a number
    [InlineData("=A1=\"\"", true)]      // a blank equals "" against text
    [InlineData("=A1=FALSE", true)]     // a blank equals FALSE against a boolean
    [InlineData("=A1=A2", true)]
    public void Comparisons_follow_excel(string formula, bool expected)
    {
        Assert.Equal(expected, NewSheet().Evaluate(formula).Boolean);
    }

    [Fact] // ADR-0047: text that reads as a number is that number in arithmetic
    public void Numeric_text_is_coerced_in_arithmetic()
    {
        var sheet = NewSheet();

        Assert.Equal(3, sheet.Evaluate("=\"2\"+1").Number);
        Assert.Equal(-2, sheet.Evaluate("=-\"2\"").Number);
        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=\"abc\"+1").Error);
        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=-\"abc\"").Error);
    }

    [Theory] // ADR-0047: ^ refuses what has no real answer, as Excel's does
    [InlineData("=0^0", ErrorValue.Num)]
    [InlineData("=0^-1", ErrorValue.Div0)]
    [InlineData("=(-8)^(1/3)", ErrorValue.Num)]
    [InlineData("=10^400", ErrorValue.Num)]
    public void Power_errors_are_excels(string formula, ErrorValue expected)
    {
        Assert.Equal(expected, NewSheet().Evaluate(formula).Error);
    }
}
