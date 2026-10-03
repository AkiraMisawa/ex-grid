using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>Text functions observed on Windows Excel, 2026-10-03, through the public Sheet.</summary>
public class ObservedTextTests
{
    public static IEnumerable<object[]> CaseIds()
    {
        foreach (var area in new[] { "upper", "lower", "proper", "search" })
            foreach (var c in ExcelCorpus.Cases(area))
                yield return new object[] { area, c.GetProperty("id").GetString()! };
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void Adr0047_Observed_text_case_matches_Excel_or_its_recorded_refusal(string area, string id)
    {
        Assert.Empty(ExcelCorpus.Run(ExcelCorpus.Case(area, id)));
    }

    [Theory]
    [InlineData("=UPPER(\"日本語\")")]
    [InlineData("=LOWER(\"Ω\")")]
    [InlineData("=LOWER(\"Σa\")")]
    [InlineData("=LOWER(\"Σ,\")")]
    [InlineData("=LOWER(\"Σσ\")")]
    [InlineData("=PROPER(\"ΣΣ\")")]
    [InlineData("=PROPER(\"Σa\")")]
    [InlineData("=SEARCH(\"a\",\"a日本語\")")]
    [InlineData("=SEARCH(\"\",\"a😀\")")]
    public void Adr0047_Text_outside_the_admitted_alphabet_or_context_is_refused(string formula)
    {
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate(formula));
    }

    [Fact]
    public void Adr0047_Text_functions_recalculate_from_changed_references()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "straße");
        sheet.Enter("B1", "=UPPER(A1)");
        sheet.Enter("C1", "=LOWER(B1)");
        sheet.Enter("D1", "=PROPER(C1)");
        sheet.Enter("E1", "=SEARCH(\"STR*E\",D1)");
        Assert.Equal(Value.FromText("Straße"), sheet.Value("D1"));
        Assert.Equal(1, sheet.Number("E1"));

        var change = sheet.Enter("A1", "hello-world 42foo o'NEILL");

        Assert.Equal(Value.FromText("Hello-World 42Foo O'Neill"), sheet.Value("D1"));
        Assert.Equal(ErrorValue.Value, sheet.Error("E1"));
        Assert.Equal(new[] { "B1", "C1", "D1", "E1" }, change.Recalculated.Addresses().Order());
    }

    [Theory]
    [InlineData("UPPER")]
    [InlineData("LOWER")]
    [InlineData("PROPER")]
    [InlineData("SEARCH")]
    public void Adr0047_Text_functions_preserve_strict_cycle_errors(string function)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", function == "SEARCH" ? "=SEARCH(A1,\"x\")" : $"={function}(A1)");
        Assert.Equal(ErrorValue.Circ, sheet.Error("A1"));
    }
}
