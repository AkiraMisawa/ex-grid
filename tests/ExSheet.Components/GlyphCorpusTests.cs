using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// The corpus the per-class estimate is checked against (ADR-0016; tickets 82 and 83):
/// what ExSheet's built-in Number Formats paint, under 24 cultures. <c>tests/GlyphWidths</c>
/// keeps it with the painted widths of every string, which a browser measured, and the layer-1
/// tests in ExGrid.Tests and ExGrid.MudBlazor.Tests hold every string's estimate at or over its
/// painted width. A format that changes what it paints changes the corpus, and its new strings
/// have never been measured, so this test fails until they are.
/// </summary>
public class GlyphCorpusTests
{
    private static readonly string[] Cultures =
    [
        "en-US", "en-GB", "de-DE", "fr-FR", "ja-JP", "zh-CN", "ko-KR", "ru-RU", "he-IL", "en-IN",
        "tr-TR", "pl-PL", "de-CH", "sv-SE", "pt-BR", "es-MX", "en-PH", "en-NG", "vi-VN", "cs-CZ",
        "th-TH", "uk-UA", "az-Latn-AZ", "hu-HU",
    ];

    private static readonly double[] Amounts =
        [0, 0.5, 1, 9, 100, 999.99, 1234.5, -1234.5, 99999.99, 1234567.5, -1234567.5, 123456789012.5, -123456789012.5];

    private static readonly double[] Fractions = [0.1234, -0.1234, 1, 12.3456, 0.5];

    private static readonly double[] Scientific = [123456, 0.000123, -987654321, 1e100];

    // Every month, so every month name each culture has; and times either side of noon.
    private static readonly double[] Dates =
    [
        .. Enumerable.Range(1, 12).Select(m => new DateTime(2026, m, m == 2 ? 28 : 30).ToOADate()),
        new DateTime(2026, 9, 30, 23, 59, 59).ToOADate(),
        new DateTime(2026, 12, 31, 0, 0, 0).ToOADate(),
        new DateTime(2026, 1, 1, 12, 34, 56).ToOADate(),
        new DateTime(2026, 11, 11, 11, 11, 11).ToOADate(),
        new DateTime(2026, 5, 8, 8, 8, 8).ToOADate(),
    ];

    /// <summary>
    /// Every text the built-in formats paint for the values above: Number and Currency in each
    /// negative style, at 0 and 2 places, Percentage, Scientific, General, the culture's built-in
    /// currency, and the Date and Time types. Ordinal order, no duplicates.
    /// </summary>
    internal static SortedSet<string> Generate()
    {
        var all = new SortedSet<string>(StringComparer.Ordinal);
        var address = new CellAddress(0, 0);
        foreach (var name in Cultures)
        {
            var culture = CultureInfo.GetCultureInfo(name);
            var sheet = new Sheet(culture);
            void Add(string code, IEnumerable<double> values)
            {
                sheet.SetNumberFormat(address, NumberFormat.Parse(code));
                foreach (var value in values)
                {
                    sheet.SetEntry(address, Entry.FromValue(Value.FromNumber(value)));
                    var shown = sheet.GetDisplay(address);
                    if (!shown.CannotShow && shown.Text.Length > 0) all.Add(shown.Text);
                }
            }
            foreach (var places in new[] { 0, 2 })
            {
                for (var negative = 0; negative < NumberFormatCodes.NumberNegativeStyles; negative++)
                {
                    Add(NumberFormatCodes.Number(places, true, negative), Amounts);
                    Add(NumberFormatCodes.Number(places, false, negative), Amounts);
                }
                for (var negative = 0; negative < NumberFormatCodes.CurrencyNegativeStyles; negative++)
                    Add(NumberFormatCodes.Currency(places, negative, culture), Amounts);
                Add(NumberFormatCodes.Percentage(places), Fractions);
                Add(NumberFormatCodes.Scientific(places), Scientific);
            }
            Add(NumberFormat.BuiltInCurrency(culture).Code, Amounts);
            Add("General", [.. Amounts, .. Fractions, .. Scientific]);
            foreach (var code in NumberFormatCodes.DateTypes.Concat(NumberFormatCodes.TimeTypes)) Add(code, Dates);
        }
        return all;
    }

    [Fact] // ADR-0016, principle 1: the measured corpus is still what the built-in formats paint
    public void The_glyph_width_corpus_is_what_the_built_in_formats_paint()
    {
        var recorded = JsonSerializer.Deserialize<string[]>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "GlyphWidths", "corpus.json")))!;
        var generated = Generate();

        var missing = generated.Except(recorded, StringComparer.Ordinal).ToArray();
        var gone = recorded.Except(generated, StringComparer.Ordinal).ToArray();
        if (missing.Length == 0 && gone.Length == 0) return;

        // Written beside the test binary, for tests/GlyphWidths/README.md's steps: copy it over
        // the recorded corpus and measure the faces again.
        var written = Path.Combine(AppContext.BaseDirectory, "corpus.generated.json");
        File.WriteAllText(written, JsonSerializer.Serialize(generated, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        }) + "\n");
        Assert.Fail(
            $"The built-in formats paint {missing.Length} string(s) the corpus does not hold and no longer paint "
            + $"{gone.Length} it does. New: {string.Join(" | ", missing.Take(20))}. Gone: {string.Join(" | ", gone.Take(20))}. "
            + $"The corpus they make is at {written}; tests/GlyphWidths/README.md says how to measure it.");
    }
}
