using System.Text.Json;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere). What a refusal is, and that
/// nothing catches it, is in the shared cases (<c>tests/cases/sheet/refusal.json</c>). What stays here reads the Excel
/// corpus, which only this runner reads, and a refused Value's .NET equality and its use as an Entry.
/// </summary>
public class RefusalTests
{
    [Fact] // ADR-0047 (2026-10-13): a refusal is #VALUE! outside the engine: equal to Excel's own as a Value, with its hash, and only the cell's change says they differ
    public void ADR0047_a_refusal_still_equals_value_error()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=MROUND(6.05,0.1)");
        var refused = sheet.Value("B1")!.Value;

        Assert.Equal(Value.FromError(ErrorValue.Value), refused);
        Assert.Equal(Value.FromError(ErrorValue.Value).GetHashCode(), refused.GetHashCode());
    }

    [Fact] // ADR-0047 (2026-10-12): a refused cell's #VALUE! is no constant, which a Formula could catch; Excel's own #VALUE! is one
    public void ADR0047_a_refusal_is_no_constant_entry()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=MROUND(6.05,0.1)");

        Assert.Throws<ArgumentException>(() => Entry.FromValue(sheet.Value("B1")!.Value));
        Assert.Equal(ErrorValue.Value, Entry.FromValue(Value.FromError(ErrorValue.Value)).Constant!.Value.Error);
    }

    [Fact] // ADR-0047 (2026-10-12): the corpus and the engine agree on which #VALUE! is a refusal
    public void ADR0047_every_refusal_in_the_corpus_is_the_formulas_and_every_excel_value_error_is_not()
    {
        // Refused although Excel was seen to give #VALUE! too: the engine cannot tell a pattern PCRE2
        // rejects from one it reads otherwise, so every pattern PortableRegex does not take is refused.
        var refusedWhereExcelGaveValueError = new HashSet<string>(StringComparer.Ordinal) { "XLOOKUP-103", "XLOOKUP-157" };
        var wrong = new List<string>();
        foreach (var area in ExcelCorpus.AreaFiles)
        {
            foreach (var c in ExcelCorpus.Cases(area))
            {
                if (!c.GetProperty("expect").TryGetProperty("value2", out var value2)) continue;
                if (value2.ValueKind != JsonValueKind.String || value2.GetString() != "#VALUE!") continue;
                var id = c.GetProperty("id").GetString()!;
                // The engine's #VALUE! is a refusal where no run has observed Excel's answer, or where it differs from it by decision.
                var refusal = c.GetProperty("source").GetString() == "uncertain" || c.TryGetProperty("engineDiffersByDecision", out _)
                    || refusedWhereExcelGaveValueError.Contains(id);
                if ((ExcelCorpus.CheckedValue(c) is { IsRefused: true }) != refusal) wrong.Add($"{id}: expected {(refusal ? "a refusal" : "an Error Value a Formula can catch")}");
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    [Fact] // ADR-0047 (2026-10-13, decided with the user): a corpus #NAME? is refused exactly where Excel answers otherwise, a function Excel has (NPV, IRR) or a declared table's bare name (TABLE-024); an unknown name is Excel's own #NAME?
    public void ADR0047_every_refused_name_in_the_corpus_is_the_formulas_and_every_excel_name_error_is_not()
    {
        // #NAME? where Excel refuses the Formula on entry, a table never declared: no answer of Excel's to differ from.
        var excelRefusesEntry = new HashSet<string>(StringComparer.Ordinal) { "TABLE-013" };
        var wrong = new List<string>();
        var refused = 0;
        foreach (var area in ExcelCorpus.AreaFiles)
        {
            foreach (var c in ExcelCorpus.Cases(area))
            {
                if (!c.GetProperty("expect").TryGetProperty("value2", out var value2)) continue;
                if (value2.ValueKind != JsonValueKind.String || value2.GetString() != "#NAME?") continue;
                var id = c.GetProperty("id").GetString()!;
                var refusal = (c.GetProperty("source").GetString() == "uncertain" || c.TryGetProperty("engineDiffersByDecision", out _)) && !excelRefusesEntry.Contains(id);
                if (refusal) refused++;
                if ((ExcelCorpus.CheckedValue(c) is { IsRefused: true }) != refusal) wrong.Add($"{id}: expected {(refusal ? "a refusal" : "an Error Value a Formula can catch")}");
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        Assert.True(refused >= 24, $"{refused} refused #NAME? cases");
    }
}
