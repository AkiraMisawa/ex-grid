using System.Collections.Frozen;
using System.Text;

namespace ExGrid.Columns;

/// <summary>
/// Measured widths for single glyphs, regular and bold, at the size they were measured at
/// (ADR-0016; ticket 83). The Cell Metrics charge a glyph the table holds its own width instead of
/// its class's: a letter or a currency sign costs what it paints, so a date's month name is not
/// charged as its widest letter. A glyph the table does not hold is charged by its class as before,
/// and a glyph no class names is charged the other class.
///
/// <para>A table speaks for one face, so it holds only glyphs that face draws itself: a glyph it
/// lacks is painted in a fallback the platform chooses, and only the other class can cover that.
/// Whoever sets the face owes the table, as they owe the class widths (ADR-0027's metrics-bearing
/// obligation); <c>tests/GlyphWidths</c> measures one.</para>
/// </summary>
public sealed class GlyphWidthTable
{
    private readonly FrozenDictionary<int, (double Regular, double Bold)> _widths;

    internal IEnumerable<int> CodePoints => _widths.Keys;

    /// <summary>
    /// A table of <paramref name="glyphs"/> measured at <paramref name="measuredAtPx"/>: each a
    /// single character (one code point), with its width at the regular weights the grid paints
    /// and at the bold one. Refuses a glyph given twice, a string that is not one code point, and
    /// a width that is not a finite, positive number of pixels.
    /// </summary>
    public GlyphWidthTable(double measuredAtPx, IEnumerable<(string Glyph, double RegularPx, double BoldPx)> glyphs)
    {
        ArgumentNullException.ThrowIfNull(glyphs);
        if (!double.IsFinite(measuredAtPx) || measuredAtPx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(measuredAtPx), measuredAtPx,
                "The size a table was measured at is a finite, positive number of pixels.");
        }
        var widths = new Dictionary<int, (double, double)>();
        foreach (var (glyph, regularPx, boldPx) in glyphs)
        {
            ArgumentNullException.ThrowIfNull(glyph, nameof(glyphs));
            if (Rune.DecodeFromUtf16(glyph, out var rune, out var consumed) != System.Buffers.OperationStatus.Done
                || consumed != glyph.Length)
            {
                throw new ArgumentException($"'{glyph}' is not one character.", nameof(glyphs));
            }
            if (!double.IsFinite(regularPx) || regularPx <= 0 || !double.IsFinite(boldPx) || boldPx <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(glyphs), $"'{glyph}' needs finite, positive widths.");
            }
            if (!widths.TryAdd(rune.Value, (regularPx, boldPx)))
                throw new ArgumentException($"'{glyph}' is in the table twice.", nameof(glyphs));
        }
        MeasuredAtPx = measuredAtPx;
        _widths = widths.ToFrozenDictionary();
    }

    /// <summary>The font size the widths are true at; the Cell Metrics scale them to theirs.</summary>
    public double MeasuredAtPx { get; }

    /// <summary>How many glyphs the table holds.</summary>
    public int Count => _widths.Count;

    /// <summary>The width of <paramref name="codePoint"/> at <see cref="MeasuredAtPx"/>, at the bold
    /// weight or the regular ones; false when the table does not hold it.</summary>
    public bool TryGetWidthPx(int codePoint, bool bold, out double widthPx)
    {
        if (_widths.TryGetValue(codePoint, out var widths))
        {
            widthPx = bold ? widths.Bold : widths.Regular;
            return true;
        }
        widthPx = 0;
        return false;
    }
}
