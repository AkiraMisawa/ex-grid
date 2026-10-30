using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// The observed Windows cases of VLOOKUP, HLOOKUP and MATCH, run through the C# corpus reader (ADR-0047). What they do
/// beyond those cases is in the shared cases (<c>tests/cases/sheet/legacy-lookup.json</c>).
/// </summary>
public class LegacyLookupTests
{
    [Theory]
    [InlineData("vlookup")]
    [InlineData("hlookup")]
    [InlineData("match")]
    public void ADR0047_Observed_Windows_lookup_cases_match_Excel_or_recorded_refusal(string area)
    {
        var differences = ExcelCorpus.Cases(area).SelectMany(c => ExcelCorpus.Run(c)
            .Select(d => $"{c.GetProperty("id").GetString()}: {d}"));

        Assert.Empty(differences);
    }
}
