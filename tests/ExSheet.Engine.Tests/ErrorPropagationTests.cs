using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// How Error Values propagate is in the Excel case corpus (<c>ExcelCases/errors.json</c>, run by
/// <see cref="ExcelCaseTests"/>). What stays here is the engine's own surface.
/// </summary>
public class ErrorPropagationTests
{
    [Theory] // ADR-0047/0049: #CIRC! and #GETTING_DATA are states of a computation, never an Entry
    [InlineData(ErrorValue.Circ)]
    [InlineData(ErrorValue.GettingData)]
    public void A_state_error_is_never_an_entry(ErrorValue error)
    {
        Assert.Throws<ArgumentException>(() => Entry.FromValue(Value.FromError(error)));
    }
}
