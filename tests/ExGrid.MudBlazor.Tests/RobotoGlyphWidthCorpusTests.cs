using ExGrid.Columns;
using GlyphWidths;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// No glyph a Number Format can emit is charged below its width (ADR-0016's note of 2026-10-01;
/// principle 1; ADR-0030: the widths are the Wrapper's metrics-bearing obligation): under Roboto's
/// widths, as this Wrapper cascades them, no text ExSheet's built-in formats paint is wider than its
/// estimate. The painted widths are <c>tests/GlyphWidths</c>' measurement of Roboto in this
/// package's family stack, at 14px and at 12px — the core scales the widths to Excel's 12px —
/// at weights 400, 500 and 600 under the regular widths and 700 under the bold ones.
/// </summary>
public class RobotoGlyphWidthCorpusTests
{
    public static TheoryData<string, GridDensity, int> Cases()
    {
        var cases = new TheoryData<string, GridDensity, int>();
        foreach (var file in GlyphWidthRecord.FilesOf("roboto"))
            foreach (var density in Enum.GetValues<GridDensity>())
                foreach (var weight in GlyphWidthRecord.Weights)
                    cases.Add(file, density, weight);
        return cases;
    }

    public static TheoryData<string> Files() => [.. GlyphWidthRecord.FilesOf("roboto")];

    private static (CellTextMetrics Metrics, int Size) MetricsFor(GridDensity density, int weight)
    {
        var resolved = GridMetrics.Resolve(density, defaults: MudExGridPresentation.Roboto);
        return (weight == 700 ? resolved.CellMetrics.Bold : resolved.CellMetrics, (int)resolved.FontSizePx);
    }

    [Theory, MemberData(nameof(Cases))] // ADR-0016, principle 1, ADR-0030: a built-in format's text is never wider than its estimate in Roboto
    public void No_text_a_built_in_format_paints_is_wider_than_its_estimate(string file, GridDensity density, int weight)
    {
        var record = GlyphWidthRecord.Load(file);
        var (metrics, size) = MetricsFor(density, weight);

        var cut = GlyphWidthRecord.Corpus
            .Select((text, i) => (Text: text, Painted: record.StringPx(size, weight, i), Charged: metrics.TextWidthPx(text)))
            .Where(s => s.Charged < s.Painted)
            .ToArray();

        Assert.True(cut.Length == 0,
            $"{cut.Length} of {GlyphWidthRecord.Corpus.Count} strings paint wider than their estimate in Roboto at "
            + $"{size}px, weight {weight}: "
            + string.Join("; ", cut.Take(12).Select(s => $"\"{s.Text}\" paints {s.Painted:0.###} and is charged {s.Charged:0.###}")));
    }

    [Theory, MemberData(nameof(Cases))] // ADR-0016, principle 1, ADR-0030: each class is at least every glyph it charges for in Roboto
    public void Every_glyph_is_charged_at_least_its_width(string file, GridDensity density, int weight)
    {
        var record = GlyphWidthRecord.Load(file);
        var (metrics, size) = MetricsFor(density, weight);

        var under = record.Glyphs
            .Select((glyph, i) => (Glyph: glyph, Painted: record.GlyphPx(size, weight, i), Charged: metrics.TextWidthPx(glyph),
                Painter: record.Painters[weight][i]))
            .Where(g => g.Charged < g.Painted)
            .ToArray();

        Assert.True(under.Length == 0,
            $"{under.Length} glyphs are charged under their width in Roboto at {size}px, weight {weight}: "
            + string.Join("; ", under.Select(g => $"'{g.Glyph}' (U+{char.ConvertToUtf32(g.Glyph, 0):X4}, {g.Painter}) is {g.Painted:0.###} and is charged {g.Charged:0.###}")));
    }

    [Theory, MemberData(nameof(Files))] // ADR-0016: a record holds this corpus's widths, or it holds none
    public void The_measurement_is_of_this_corpus(string file)
    {
        var record = GlyphWidthRecord.Load(file);

        Assert.Equal(GlyphWidthRecord.CorpusSha256, record.MeasuredCorpusSha256);
    }
}
