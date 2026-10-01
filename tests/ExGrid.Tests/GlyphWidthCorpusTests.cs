using ExGrid.Columns;
using GlyphWidths;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// No glyph a Number Format can emit is charged below its width (ADR-0016's note of 2026-10-01;
/// principle 1): under the core's default widths, no text ExSheet's built-in formats paint is
/// wider than its estimate, so a number or date judged to fit is never cut. The painted widths are
/// <c>tests/GlyphWidths</c>' measurement of the core's two faces — <c>system-ui</c> as macOS
/// resolves it, and DejaVu Sans, which Linux resolves it to — at 14px for Compact, Standard and
/// Comfortable and 12px for Excel, at weights 400, 500 and 600 under the regular widths and 700
/// under the bold ones. README.md there says how to measure again.
/// </summary>
public class GlyphWidthCorpusTests
{
    private static readonly string[] Faces = ["system-ui", "dejavu-sans"];

    public static TheoryData<string, GridDensity, int> Cases()
    {
        var cases = new TheoryData<string, GridDensity, int>();
        foreach (var file in Faces.SelectMany(GlyphWidthRecord.FilesOf))
            foreach (var density in Enum.GetValues<GridDensity>())
                foreach (var weight in GlyphWidthRecord.Weights)
                    cases.Add(file, density, weight);
        return cases;
    }

    public static TheoryData<string> Files() => [.. Faces.SelectMany(GlyphWidthRecord.FilesOf)];

    private static (CellTextMetrics Metrics, int Size) MetricsFor(GridDensity density, int weight)
    {
        var resolved = GridMetrics.Resolve(density);
        return (weight == 700 ? resolved.CellMetrics.Bold : resolved.CellMetrics, (int)resolved.FontSizePx);
    }

    [Theory, MemberData(nameof(Cases))] // ADR-0016, principle 1: a built-in format's text is never wider than its estimate
    public void No_text_a_built_in_format_paints_is_wider_than_its_estimate(string file, GridDensity density, int weight)
    {
        var record = GlyphWidthRecord.Load(file);
        var (metrics, size) = MetricsFor(density, weight);

        var cut = GlyphWidthRecord.Corpus
            .Select((text, i) => (Text: text, Painted: record.StringPx(size, weight, i), Charged: metrics.TextWidthPx(text)))
            .Where(s => s.Charged < s.Painted)
            .ToArray();

        Assert.True(cut.Length == 0,
            $"{cut.Length} of {GlyphWidthRecord.Corpus.Count} strings paint wider than their estimate in {record.Face} at "
            + $"{size}px, weight {weight}: "
            + string.Join("; ", cut.Take(12).Select(s => $"\"{s.Text}\" paints {s.Painted:0.###} and is charged {s.Charged:0.###}")));
    }

    [Theory, MemberData(nameof(Cases))] // ADR-0016, principle 1: each class is at least every glyph it charges for
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
            $"{under.Length} glyphs are charged under their width in {record.Face} at {size}px, weight {weight}: "
            + string.Join("; ", under.Select(g => $"'{g.Glyph}' (U+{char.ConvertToUtf32(g.Glyph, 0):X4}, {g.Painter}) is {g.Painted:0.###} and is charged {g.Charged:0.###}")));
    }

    [Theory, MemberData(nameof(Files))] // ADR-0016: a record holds this corpus's widths, or it holds none
    public void The_measurement_is_of_this_corpus(string file)
    {
        var record = GlyphWidthRecord.Load(file);

        Assert.Equal(GlyphWidthRecord.CorpusSha256, record.MeasuredCorpusSha256);
    }
}
