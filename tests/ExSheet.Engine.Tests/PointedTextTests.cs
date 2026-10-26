using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// What a Pointing Scope writes (ADR-0058, SH-32's layer 1 half), read back. What it writes for each key and column is
/// in <c>tests/cases/sheet/pointed-text.json</c>; what stays here, mirrored in the Script Core's <c>surface.test.ts</c>,
/// feeds what <see cref="FormulaEntry.LookupText(string, string, Value, string)"/> writes into a Sheet, so that the two
/// halves cannot drift apart.
/// </summary>
public class PointedTextTests
{
    [Fact] // ADR-0058 / SH-32: what is written reads the cell it was written for, for a key of each kind
    public void ADR0058_what_is_written_reads_the_pressed_cell()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("T", ["Key", "Rate#", "PV"], key: "Key");
        sheet.PushLinkedTable("T",
        [
            [Value.FromText("R-\"5\""), Value.FromNumber(1), Value.FromNumber(10)],
            [Value.FromNumber(0.1), Value.FromNumber(2), Value.FromNumber(20)],
            [Value.FromBoolean(true), Value.FromNumber(3), Value.FromNumber(30)],
            [Value.FromText("1250"), Value.FromNumber(4), Value.FromNumber(40)],
        ]);

        Assert.Equal(Value.FromNumber(10), sheet.Evaluate("=" + FormulaEntry.LookupText("T", "Key", Value.FromText("R-\"5\""), "PV")));
        Assert.Equal(Value.FromNumber(2), sheet.Evaluate("=" + FormulaEntry.LookupText("T", "Key", Value.FromNumber(0.1), "Rate#")));
        Assert.Equal(Value.FromNumber(30), sheet.Evaluate("=" + FormulaEntry.LookupText("T", "Key", Value.FromBoolean(true), "PV")));
        Assert.Equal(Value.FromNumber(40), sheet.Evaluate("=" + FormulaEntry.LookupText("T", "Key", Value.FromText("1250"), "PV")));
        Assert.Equal(Value.FromNumber(100), sheet.Evaluate("=SUM(" + FormulaEntry.StructuredReferenceText("T", "PV") + ")"));
    }
}
