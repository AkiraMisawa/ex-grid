using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class ErrorPropagationTests
{
    [Theory] // ADR-0047: an Error Value in a cell flows through every operator that uses it
    [InlineData("#N/A", ErrorValue.NA)]
    [InlineData("#DIV/0!", ErrorValue.Div0)]
    [InlineData("#VALUE!", ErrorValue.Value)]
    [InlineData("#REF!", ErrorValue.Ref)]
    [InlineData("#NAME?", ErrorValue.Name)]
    [InlineData("#NUM!", ErrorValue.Num)]
    [InlineData("#NULL!", ErrorValue.Null)]
    public void An_error_value_propagates_through_every_operator(string typed, ErrorValue expected)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", typed);

        foreach (var formula in new[] { "=A1+1", "=1-A1", "=A1*2", "=A1/2", "=A1^2", "=-A1", "=A1%", "=A1&\"x\"", "=A1=1", "=\"x\"<A1" })
        {
            Assert.Equal(expected, sheet.Evaluate(formula).Error);
        }
    }

    [Fact] // ADR-0047: an error travels down a chain of Formulas
    public void An_error_travels_down_a_chain()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "0");
        sheet.Enter("B1", "=1/A1");
        sheet.Enter("C1", "=B1+1");
        sheet.Enter("D1", "=C1*2");

        Assert.Equal(ErrorValue.Div0, sheet.Error("D1"));

        sheet.Enter("A1", "4");

        Assert.Equal(2.5, sheet.Number("D1"));
    }

    [Fact] // ADR-0047: with two errors, the left operand's is the one carried
    public void The_left_error_wins()
    {
        var sheet = NewSheet();

        Assert.Equal(ErrorValue.NA, sheet.Evaluate("=#N/A+1/0").Error);
        Assert.Equal(ErrorValue.Div0, sheet.Evaluate("=1/0+#N/A").Error);
    }

    [Fact] // ADR-0047: a typed Error Value is a constant, an Error Value like any other
    public void A_typed_error_is_a_constant()
    {
        var sheet = NewSheet();

        sheet.Enter("A1", "#n/a");

        Assert.Equal(ErrorValue.NA, sheet.Error("A1"));
        Assert.Equal("#N/A", sheet.GetEntryText(CellAddress.Parse("A1")));
    }

    [Theory] // ADR-0047/0049: #CIRC! and #GETTING_DATA are states of a computation; typed, they are text
    [InlineData("#CIRC!")]
    [InlineData("#GETTING_DATA")]
    public void Typed_state_errors_are_text(string typed)
    {
        var sheet = NewSheet();

        sheet.Enter("A1", typed);

        Assert.Equal(ValueKind.Text, sheet.Value("A1")!.Value.Kind);
        Assert.Throws<ArgumentException>(() => Entry.FromValue(Value.FromError(ErrorValue.Circ)));
        Assert.Throws<ArgumentException>(() => Entry.FromValue(Value.FromError(ErrorValue.GettingData)));
    }
}
