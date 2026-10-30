using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ExGrid.Columns;

namespace ExGrid.Components;

/// <summary>
/// The widths a Consumer hands a grid, written as the Script Core takes them (ADR-0016, ADR-0030): a component's
/// <see cref="CellTextMetrics"/> and a Wrapper's cascaded <see cref="GridPresentationDefaults"/>, each whole, as numbers
/// (packages/blazor/src/wire-metrics.ts reads them). Whoever sets the font owes its widths (ADR-0027), so a Script grid
/// in a Wrapper that sets one judges <c>####</c> by them. Only the metrics' public surface is read,
/// so every adapter it is compiled into writes them alike. A Wrapper's glyph table holds hundreds of glyphs and the
/// options are written on every render, so each value is written once and its text kept while the value lives.
/// </summary>
internal static class ScriptMetricsJson
{
    private static readonly ConditionalWeakTable<GridPresentationDefaults, byte[]> Defaults = new();
    // By the glyph table the metrics carry: the last metrics written with it, and their text, replaced whole, since
    // circuits write at once on Blazor Server.
    private static readonly ConditionalWeakTable<GlyphWidthTable, StrongBox<Written?>> Metrics = new();

    private sealed record Written(CellTextMetrics Metrics, byte[] Text);

    /// <summary>Writes <paramref name="metrics"/> as the property <paramref name="name"/>. A glyph table is written as
    /// each glyph's charge at the size the metrics are true at, regular and bold, so the browser charges what .NET
    /// would: a table measured at one pixel, scaled to one.</summary>
    public static void WriteCellMetrics(Utf8JsonWriter json, string name, CellTextMetrics metrics)
    {
        json.WritePropertyName(name);
        if (metrics.GlyphWidths is not { } table)
        {
            json.WriteRawValue(TextOf(writer => WriteWidths(writer, metrics)), skipInputValidation: true);
            return;
        }
        var held = Metrics.GetValue(table, _ => new StrongBox<Written?>());
        var written = held.Value;
        if (written is null || written.Metrics != metrics)
        {
            written = new Written(metrics, TextOf(writer => WriteWidths(writer, metrics)));
            held.Value = written;
        }
        json.WriteRawValue(written.Text, skipInputValidation: true);
    }

    /// <summary>Writes <paramref name="defaults"/> as the property <paramref name="name"/>: the font's widths, its glyph
    /// table at the size it was measured at, and the Wrapper's Density, hover band and Row Stripes where it names
    /// them.</summary>
    public static void WritePresentationDefaults(Utf8JsonWriter json, string name, GridPresentationDefaults defaults)
    {
        json.WritePropertyName(name);
        json.WriteRawValue(Defaults.GetValue(defaults, made => TextOf(writer => WriteDefaults(writer, made))), skipInputValidation: true);
    }

    private static void WriteWidths(Utf8JsonWriter json, CellTextMetrics metrics)
    {
        json.WriteStartObject();
        json.WriteNumber("wideWidthPx", metrics.WideWidthPx);
        json.WriteNumber("digitWidthPx", metrics.DigitWidthPx);
        json.WriteNumber("narrowWidthPx", metrics.NarrowWidthPx);
        json.WriteNumber("fullWidthPx", metrics.FullWidthPx);
        json.WriteNumber("cellHorizontalPaddingPx", metrics.CellHorizontalPaddingPx);
        json.WriteNumber("boldWideWidthPx", metrics.BoldWideWidthPx);
        json.WriteNumber("boldDigitWidthPx", metrics.BoldDigitWidthPx);
        json.WriteNumber("boldNarrowWidthPx", metrics.BoldNarrowWidthPx);
        json.WriteNumber("otherWidthPx", metrics.OtherWidthPx);
        json.WriteNumber("boldOtherWidthPx", metrics.BoldOtherWidthPx);
        if (metrics.GlyphWidths is not null)
        {
            WriteGlyphs(json, metrics, measuredAtPx: 1);
            json.WriteNumber("fontSizePx", 1);
        }
        json.WriteEndObject();
    }

    private static void WriteDefaults(Utf8JsonWriter json, GridPresentationDefaults defaults)
    {
        json.WriteStartObject();
        json.WriteNumber("wideWidthPx", defaults.WideWidthPx);
        json.WriteNumber("digitWidthPx", defaults.DigitWidthPx);
        json.WriteNumber("narrowWidthPx", defaults.NarrowWidthPx);
        json.WriteNumber("fontSizePx", defaults.FontSizePx);
        json.WriteNumber("boldWideWidthPx", defaults.BoldWideWidthPx);
        json.WriteNumber("boldDigitWidthPx", defaults.BoldDigitWidthPx);
        json.WriteNumber("boldNarrowWidthPx", defaults.BoldNarrowWidthPx);
        json.WriteNumber("otherWidthPx", defaults.OtherWidthPx);
        json.WriteNumber("boldOtherWidthPx", defaults.BoldOtherWidthPx);
        // The table as it was measured: each glyph read at the size it was measured at, which the browser scales to each
        // Density's as .NET does.
        if (defaults.GlyphWidths is { } table)
            WriteGlyphs(json, new CellTextMetrics(defaults.DigitWidthPx, 0).WithGlyphWidths(table, table.MeasuredAtPx), table.MeasuredAtPx);
        if (defaults.Density is { } density)
            json.WriteString("density", JsonNamingPolicy.CamelCase.ConvertName(density.ToString()));
        if (defaults.HighlightHoverRow is { } hover)
            json.WriteBoolean("highlightHoverRow", hover);
        if (defaults.StripeRows is { } stripes)
            json.WriteBoolean("stripeRows", stripes);
        json.WriteEndObject();
    }

    // Each glyph the table holds, as `metrics` charge it at the regular weights and the bold one. A glyph a class names
    // is charged its class's width here, as both cores charge it whatever the table holds.
    private static void WriteGlyphs(Utf8JsonWriter json, CellTextMetrics metrics, double measuredAtPx)
    {
        var bold = metrics.Bold.ExportGlyphWidths();
        json.WriteStartObject("glyphWidths");
        json.WriteNumber("measuredAtPx", measuredAtPx);
        json.WriteStartArray("glyphs");
        foreach (var (point, regularPx) in metrics.ExportGlyphWidths().OrderBy(entry => entry.Key))
        {
            json.WriteStartArray();
            json.WriteStringValue(char.ConvertFromUtf32(point));
            json.WriteNumberValue(regularPx);
            json.WriteNumberValue(bold[point]);
            json.WriteEndArray();
        }
        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static byte[] TextOf(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
            write(writer);
        return buffer.WrittenSpan.ToArray();
    }
}
