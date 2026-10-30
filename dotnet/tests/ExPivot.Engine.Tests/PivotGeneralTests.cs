using System.Globalization;
using System.Text.Json;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// ADR-0060: a value cell with no number format of its own shows Excel's General as a PivotTable
/// shows it, at most eleven characters besides a minus sign, at the PivotTable's width and at forty
/// characters alike (the seventeenth Windows run, <c>verification/2026-10-11-windows-17</c>, item 16).
/// The rule is ExSheet's General at twelve characters or wider, where a negative number too has its
/// eleven besides the minus sign (ADR-0047, SH-20), ported because the engine references no ExSheet
/// package (PV-1); the same cases pin both, so they cannot drift apart. Its minus sign is <c>-</c> in
/// every culture, as ExSheet's General writes it. What it shows of each number Excel showed, and of the
/// edges of its rule, is held on both engines by the shared cases (<c>tests/cases/pivot/general.texts.json</c>,
/// <c>general.json</c>); these hold the C# rule to ExSheet's corpus and over random numbers.
/// </summary>
public class PivotGeneralTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// ExSheet's general-width corpus at a column of twelve characters or wider, where General is the
    /// same eleven characters besides a minus sign a pivot shows (ADR-0047). Each case's cell, as a number: a typed constant
    /// is read as it is typed, and a Formula is named here, so a new case the corpus gains fails
    /// <see cref="ADR0060_ADR0047_Every_wide_general_width_case_is_pinned"/> until it is pinned.
    /// </summary>
    private static readonly Dictionary<string, double> Formulas = new(StringComparer.Ordinal)
    {
        ["=22/7"] = 22.0 / 7,
        ["=1/300000"] = 1.0 / 300000,
        ["=1E+100*1"] = 1E+100,
    };

    public static TheoryData<string, double, string, string> WideGeneralWidthCases()
    {
        var data = new TheoryData<string, double, string, string>();
        foreach (var (id, cell, culture, text) in WideCases())
            data.Add(id, NumberOf(cell), culture, text);
        return data;
    }

    [Theory] // ADR-0060/ADR-0047 (SH-20): ExSheet's General in a column twelve characters wide or wider is the pivot's General
    [MemberData(nameof(WideGeneralWidthCases))]
    public void ADR0060_ADR0047_General_is_ExSheets_at_twelve_characters_or_wider(string id, double number, string culture, string shown)
        => Assert.True(shown == PivotGeneral.Text(number, CultureInfo.GetCultureInfo(culture)),
            $"{id}: ExSheet shows {shown}, ExPivot {PivotGeneral.Text(number, CultureInfo.GetCultureInfo(culture))}");

    [Fact] // ADR-0060/ADR-0047: the corpus's wide cases are all here, so a new one cannot pass by unseen
    public void ADR0060_ADR0047_Every_wide_general_width_case_is_pinned()
    {
        var cases = WideCases().ToArray();

        Assert.True(cases.Length >= 7, $"only {cases.Length} wide General cases were read from the corpus");
        Assert.All(cases, c => Assert.True(c.Cell.StartsWith('=') ? Formulas.ContainsKey(c.Cell) : double.TryParse(c.Cell, NumberStyles.Float, Invariant, out _),
            $"{c.Id}: {c.Cell} is not pinned for ExPivot; add it to {nameof(Formulas)}"));
    }

    [Fact] // ADR-0060: an exact decimal and the double of the same digits show the same text
    public void ADR0060_A_decimal_and_its_double_agree()
    {
        var random = new Random(20261012);
        for (var i = 0; i < 20_000; i++)
        {
            var digits = random.NextInt64(1, 1_000_000_000_000_000);
            var scale = random.Next(0, 28);
            var number = new decimal(unchecked((int)digits), unchecked((int)(digits >> 32)), 0, random.Next(2) == 0, (byte)scale);
            Assert.True(PivotGeneral.Text(number, Invariant) == PivotGeneral.Text((double)number, Invariant),
                $"{number}: {PivotGeneral.Text(number, Invariant)} as a decimal, {PivotGeneral.Text((double)number, Invariant)} as a double");
        }
    }

    [Fact] // ADR-0060: General never writes more than eleven characters besides a minus sign
    public void ADR0060_General_never_writes_more_than_eleven_characters()
    {
        var random = new Random(11);
        for (var i = 0; i < 20_000; i++)
        {
            var number = BitConverter.Int64BitsToDouble(random.NextInt64());
            if (!double.IsFinite(number)) continue;
            var text = PivotGeneral.Text(number, Invariant);
            Assert.True(text.TrimStart('-').Length <= PivotGeneral.Characters, $"{number:R} is shown {text}");
        }
    }

    private static double NumberOf(string cell)
        => cell.StartsWith('=') ? Formulas[cell] : double.Parse(cell, NumberStyles.Float, Invariant);

    /// <summary>The corpus's General cases at a column of twelve characters or wider: id, the cell, the
    /// culture, and what Excel shows.</summary>
    private static IEnumerable<(string Id, string Cell, string Culture, string Text)> WideCases()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "dotnet", "ExGrid.slnx")))
            directory = directory.Parent;
        Assert.True(directory is not null, $"no dotnet/ExGrid.slnx above {AppContext.BaseDirectory}");
        var file = Path.Combine(directory!.FullName, "dotnet", "tests", "ExSheet.Engine.Tests", "ExcelCases", "general-width.json");
        using var corpus = JsonDocument.Parse(File.ReadAllText(file));
        foreach (var c in corpus.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (!c.TryGetProperty("columnWidth", out var width) || width.GetDouble() < PivotGeneral.Characters + 1) continue;
            if (c.TryGetProperty("formats", out _)) continue;
            if (!c.GetProperty("expect").TryGetProperty("text", out var text)) continue;
            var check = c.GetProperty("check").GetString()!;
            var culture = c.TryGetProperty("culture", out var named) ? named.GetString()! : "en-US";
            yield return (c.GetProperty("id").GetString()!, c.GetProperty("cells").GetProperty(check).GetString()!, culture, text.GetString()!);
        }
    }
}
