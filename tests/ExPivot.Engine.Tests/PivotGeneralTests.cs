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
/// every culture, as ExSheet's General writes it.
/// </summary>
public class PivotGeneralTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory] // ADR-0060, item 16: the values Excel's PivotTable showed in General (expivot-cells.md e05a, e05b, e08, e16)
    [InlineData(1.4142135623730951, "1.414213562")]
    [InlineData(1.6329931618554521, "1.632993162")]
    [InlineData(2.6666666666666665, "2.666666667")]
    [InlineData(0.30000000000000004, "0.3")]
    [InlineData(123456789012.345, "1.23457E+11")]
    [InlineData(1.23456789012345, "1.23456789")]
    [InlineData(1.23456789012345E-05, "1.23457E-05")]
    [InlineData(123456789013.87959, "1.23457E+11")]
    [InlineData(23.2, "23.2")]
    public void ADR0060_General_shows_what_Excels_PivotTable_showed(double number, string shown)
        => Assert.Equal(shown, PivotGeneral.Text(number, Invariant));

    [Fact] // ADR-0060, item 16: a report over e16's records shows each value as Excel's PivotTable did, at most eleven characters
    public void ADR0060_A_report_shows_General_at_eleven_characters()
    {
        Obs[] records =
        [
            new("A", 0.1), new("A", 0.2), new("B", 123456789012.345678), new("C", 1.23456789012345678),
            new("D", 0.000012345678901234567),
        ];

        var report = PivotEngine.Compute(records, ObsFields,
            new PivotLayout { Rows = [P("Group")], Values = [Sum("Amount")] }, EnUs);

        Assert.Equal(["0.3", "1.23457E+11", "1.23456789", "1.23457E-05", "1.23457E+11"],
            report.Rows.Select(row => report.ValueAt(row, 0)!.Text));
    }

    [Fact] // ADR-0060, PV-7: the text is General's; a copy still carries the full-precision number
    public void ADR0060_PV7_A_copy_carries_the_whole_number()
    {
        var report = PivotEngine.Compute([new Obs("C", 1), new Obs("C", 3)], ObsFields,
            new PivotLayout { Rows = [P("Group")], Values = [new PivotValueField("Amount", PivotAggregation.StdDev)] }, EnUs);
        var value = report.ValueAt(report.Rows[0], 0)!;

        Assert.Equal("1.414213562", value.Text);
        Assert.Equal("1.4142135623730951", value.ToString(null, Invariant));
        Assert.Equal(Math.Sqrt(2), value.Number);
    }

    [Fact] // ADR-0060: a number format of the Value Field's own is applied as written, never fitted to eleven characters
    public void ADR0060_A_number_format_of_its_own_is_not_General()
    {
        var report = PivotEngine.Compute([new Obs("B", 123456789012.345678)], ObsFields,
            new PivotLayout { Rows = [P("Group")], Values = [Sum("Amount") with { NumberFormat = "#,##0.00" }] }, EnUs);

        Assert.Equal("123,456,789,012.35", report.ValueAt(report.Rows[0], 0)!.Text);
    }

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

    [Theory] // ADR-0060 (SH-20's rule): rounding that carries an integer part into a twelfth digit goes scientific
    [InlineData(99999999999.4, "99999999999")]
    [InlineData(99999999999.5, "1E+11")]
    [InlineData(9999999999.5, "10000000000")]
    [InlineData(999999999.94, "999999999.9")]
    [InlineData(999999999.96, "1000000000")]
    [InlineData(0.99999999996, "1")]
    public void ADR0060_Rounding_carries_as_ExSheets_General(double number, string shown)
        => Assert.Equal(shown, PivotGeneral.Text(number, Invariant));

    [Theory] // ADR-0060 (SH-20's rule): a minus sign is not one of the eleven characters — a reading, as ExSheet's: the run asked no negative number
    [InlineData(-1.4142135623730951, "-1.414213562")]
    [InlineData(-123456789012.0, "-1.23457E+11")]
    [InlineData(-12345678901.0, "-12345678901")]
    [InlineData(-0.000012345678901234567, "-1.23457E-05")]
    public void ADR0060_reading_A_minus_sign_is_not_one_of_the_eleven(double number, string shown)
        => Assert.Equal(shown, PivotGeneral.Text(number, Invariant));

    [Theory] // ADR-0060: General writes the culture's decimal separator
    [InlineData("en-US", "1.414213562")]
    [InlineData("de-DE", "1,414213562")]
    [InlineData("ja-JP", "1.414213562")]
    [InlineData("fr-FR", "1,414213562")]
    public void ADR0060_General_writes_the_cultures_separator(string culture, string shown)
        => Assert.Equal(shown, PivotGeneral.Text(Math.Sqrt(2), CultureInfo.GetCultureInfo(culture)));

    [Theory] // ADR-0060 (by generalisation: no run read Excel under these cultures): the minus sign is '-' in every culture, as ExSheet's General writes it, never the culture's U+2212, which G15 wrote
    [InlineData("sv-SE")]
    [InlineData("fi-FI")]
    [InlineData("nb-NO")]
    public void ADR0060_The_minus_sign_is_a_hyphen_minus_in_every_culture(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        // The culture's own sign is another character, so the case tells the two apart.
        Assert.Equal("\u2212", culture.NumberFormat.NegativeSign);
        Assert.Equal("\u22121,4142135623731", (-Math.Sqrt(2)).ToString("G15", culture));

        Assert.Equal("-1,414213562", PivotGeneral.Text(-Math.Sqrt(2), culture));
        Assert.Equal("-1,23457E+11", PivotGeneral.Text(-123456789012.345, culture));
        Assert.Equal("-1,23457E-05", PivotGeneral.Text(-0.000012345678901234567, culture));
        Assert.Equal("-1234,5678", PivotGeneral.Text(-1234.5678m, culture));

        // And as a report shows it.
        var report = PivotEngine.Compute([new Obs("A", -1.4142135623730951)], ObsFields,
            new PivotLayout { Rows = [P("Group")], Values = [Sum("Amount")] }, new PivotOptions { Culture = culture });
        Assert.Equal("-1,414213562", report.ValueAt(report.Rows[0], 0)!.Text);
    }

    [Fact] // ADR-0060: zero, and a negative zero, are 0
    public void ADR0060_Zero_is_0()
    {
        Assert.Equal("0", PivotGeneral.Text(0.0, Invariant));
        Assert.Equal("0", PivotGeneral.Text(-0.0, Invariant));
        Assert.Equal("0", PivotGeneral.Text(0m, Invariant));
    }

    [Theory] // ADR-0060 ("Money stays exact"): an exact decimal is shown from its own digits, under the same rule
    [InlineData("0.3", "0.3")]
    [InlineData("123456789012.345678", "1.23457E+11")]
    [InlineData("1.23456789012345678", "1.23456789")]
    [InlineData("0.000012345678901234567", "1.23457E-05")]
    [InlineData("12345678901", "12345678901")]
    [InlineData("0.00001", "0.00001")]
    [InlineData("-1234.5678", "-1234.5678")]
    [InlineData("0.0000000000000000000000000001", "1E-28")]
    [InlineData("79228162514264337593543950335", "7.92282E+28")]
    [InlineData("3.3333333333333333333333333333", "3.333333333")]
    [InlineData("40.714285714285714285714285714", "40.71428571")]
    public void ADR0060_An_exact_decimal_is_shown_by_the_same_rule(string number, string shown)
        => Assert.Equal(shown, PivotGeneral.Text(decimal.Parse(number, NumberStyles.Float, Invariant), Invariant));

    [Theory] // ADR-0060: the closer form is judged from the fifteen digits both forms are built from, never from a decimal's further ones — these showed 1E-01 and 1E+00
    [InlineData("0.10000000049999999", "0.100000001")]
    [InlineData("1.00000000049999999", "1.000000001")]
    [InlineData("-0.10000000049999999", "-0.100000001")]
    public void ADR0060_A_decimals_closer_form_is_judged_from_its_fifteen_digits(string number, string shown)
    {
        var exact = decimal.Parse(number, NumberStyles.Float, Invariant);

        Assert.Equal(shown, PivotGeneral.Text(exact, Invariant));
        Assert.Equal(PivotGeneral.Text((double)exact, Invariant), PivotGeneral.Text(exact, Invariant));
    }

    [Fact] // ADR-0060 (the edge, as stated): a double is judged against its own value, as ExSheet's General judges it, so where a decimal's fifteen digits tie between the two forms, its double can choose the other
    public void ADR0060_A_decimal_and_its_double_can_part_where_fifteen_digits_tie()
    {
        // 0.1000000004999999 is 0.100000000500000 at fifteen digits, as near 0.100000001 as 1E-01.
        Assert.Equal("0.100000001", PivotGeneral.Text(0.1000000004999999m, Invariant));
        Assert.Equal("1E-01", PivotGeneral.Text(0.1000000004999999, Invariant));
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

    private sealed record Obs(string Group, double? Amount);

    private static readonly PivotField<Obs>[] ObsFields =
    [
        new("Group", PivotFieldType.Text, o => o.Group),
        new("Amount", PivotFieldType.Number, o => o.Amount),
    ];

    private static double NumberOf(string cell)
        => cell.StartsWith('=') ? Formulas[cell] : double.Parse(cell, NumberStyles.Float, Invariant);

    /// <summary>The corpus's General cases at a column of twelve characters or wider: id, the cell, the
    /// culture, and what Excel shows.</summary>
    private static IEnumerable<(string Id, string Cell, string Culture, string Text)> WideCases()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx")))
            directory = directory.Parent;
        Assert.True(directory is not null, $"no ExGrid.slnx above {AppContext.BaseDirectory}");
        var file = Path.Combine(directory!.FullName, "tests", "ExSheet.Engine.Tests", "ExcelCases", "general-width.json");
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
