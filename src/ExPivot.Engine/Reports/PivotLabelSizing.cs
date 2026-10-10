using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json.Serialization;

namespace ExPivot.Engine;

/// <summary>Explicit label geometry carried to the report computation process (ADR-0151).
/// Glyph overrides are already scaled to the client's font size and weight.</summary>
public sealed class PivotReportLabelMetrics
{
    /// <summary>Creates immutable metrics from the grid's resolved character-class widths.</summary>
    [JsonConstructor]
    public PivotReportLabelMetrics(double wideWidthPx, double digitWidthPx, double narrowWidthPx,
        double fullWidthPx, double otherWidthPx, double cellHorizontalPaddingPx,
        IReadOnlyDictionary<int, double>? glyphWidths = null)
    {
        foreach (var width in new[] { wideWidthPx, digitWidthPx, narrowWidthPx, fullWidthPx, otherWidthPx })
            if (!double.IsFinite(width) || width <= 0)
                throw new ArgumentOutOfRangeException(nameof(wideWidthPx), "Glyph widths must be finite and positive.");
        if (!double.IsFinite(cellHorizontalPaddingPx) || cellHorizontalPaddingPx < 0)
            throw new ArgumentOutOfRangeException(nameof(cellHorizontalPaddingPx));
        WideWidthPx = wideWidthPx;
        DigitWidthPx = digitWidthPx;
        NarrowWidthPx = narrowWidthPx;
        FullWidthPx = fullWidthPx;
        OtherWidthPx = otherWidthPx;
        CellHorizontalPaddingPx = cellHorizontalPaddingPx;
        var glyphs = new Dictionary<int, double>();
        foreach (var (point, width) in glyphWidths ?? new Dictionary<int, double>())
        {
            if (!Rune.IsValid(point) || !double.IsFinite(width) || width <= 0)
                throw new ArgumentException("A glyph needs a Unicode scalar and a finite positive width.", nameof(glyphWidths));
            glyphs.Add(point, width);
        }
        GlyphWidths = new ReadOnlyDictionary<int, double>(glyphs);
    }
    /// <summary>The wide character class.</summary>
    public double WideWidthPx { get; }
    /// <summary>The digit character class.</summary>
    public double DigitWidthPx { get; }
    /// <summary>The narrow character class.</summary>
    public double NarrowWidthPx { get; }
    /// <summary>The full-width character class.</summary>
    public double FullWidthPx { get; }
    /// <summary>The remaining characters' width.</summary>
    public double OtherWidthPx { get; }
    /// <summary>The padding on one side.</summary>
    public double CellHorizontalPaddingPx { get; }
    /// <summary>Resolved widths of individual glyphs outside the measured classes.</summary>
    public IReadOnlyDictionary<int, double> GlyphWidths { get; }
    /// <summary>Estimates label text including both sides' padding, without reading browser layout.</summary>
    public double EstimatePx(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var width = 2 * CellHorizontalPaddingPx;
        foreach (var rune in text.EnumerateRunes())
            width += WidthOf(rune.Value);
        return width;
    }
    internal bool SameAs(PivotReportLabelMetrics other)
        => WideWidthPx == other.WideWidthPx && DigitWidthPx == other.DigitWidthPx
        && NarrowWidthPx == other.NarrowWidthPx && FullWidthPx == other.FullWidthPx
        && OtherWidthPx == other.OtherWidthPx && CellHorizontalPaddingPx == other.CellHorizontalPaddingPx
        && GlyphWidths.Count == other.GlyphWidths.Count
        && GlyphWidths.All(pair => other.GlyphWidths.TryGetValue(pair.Key, out var width) && pair.Value == width);
    private double WidthOf(int point) => point switch
    {
        '%' or '€' or '\u2212' or '+' or '#' => WideWidthPx,
        '.' or ',' or '(' or ')' or '/' or ':' or ' ' => NarrowWidthPx,
        _ when IsFullWidth(point) => FullWidthPx,
        _ when IsDigitClass(point) => DigitWidthPx,
        _ when GlyphWidths.TryGetValue(point, out var width) => width,
        _ => OtherWidthPx,
    };
    private static bool IsDigitClass(int c) =>
        c is (>= '0' and <= '9')
            or '$' or '£' or '¥' or '\u20B9' or '\u20BA' or '\u20AB'   // ₹ ₺ ₫
            or 'E' or '-'
            or '\'' or '\u2019' or '\u00A0' or '\u202F'              // de-CH's ’, NBSP, narrow NBSP
            or '\u200E' or '\u200F' or '\u061C';                      // LRM, RLM, ALM

    private static bool IsFullWidth(int c) =>
        c is (>= 0x1100 and <= 0x115F)       // Hangul Jamo initials
            or (>= 0x2E80 and <= 0x303E)     // CJK radicals, Kangxi, ideographic space and punctuation
            or (>= 0x3041 and <= 0x33FF)     // kana, Bopomofo, Hangul compatibility, enclosed and compatibility CJK
            or (>= 0x3400 and <= 0x4DBF)     // CJK Extension A
            or (>= 0x4E00 and <= 0x9FFF)     // CJK Unified Ideographs
            or (>= 0xA000 and <= 0xA4CF)     // Yi
            or (>= 0xA960 and <= 0xA97F)     // Hangul Jamo Extended-A
            or (>= 0xAC00 and <= 0xD7A3)     // Hangul syllables
            or (>= 0xF900 and <= 0xFAFF)     // CJK Compatibility Ideographs
            or (>= 0xFE10 and <= 0xFE19)     // vertical forms
            or (>= 0xFE30 and <= 0xFE6F)     // CJK compatibility and small forms
            or (>= 0xFF01 and <= 0xFF60)     // fullwidth forms
            or (>= 0xFFE0 and <= 0xFFE6)     // fullwidth signs
            or (>= 0x1F300 and <= 0x1F64F)   // pictographs and emoticons
            or (>= 0x1F900 and <= 0x1F9FF)   // supplemental pictographs
            or (>= 0x20000 and <= 0x3FFFD);  // CJK Extensions B onward

}

// Keeps only detached keys and width counts. A numeric-only update touches no labels.
// Every step sets a row's entry to what one batch says of it, so a pass cancelled half way and
// begun again from the first batch ends where one uncancelled pass would have.
internal sealed class PivotLabelSizing
{
    private PivotReportLabelMetrics? _metrics;
    private readonly Dictionary<PivotRowKey, double[]> _rows = [];
    private SortedDictionary<double, int>[] _widths = [];

    /// <summary>The widest label of each label column of <paramref name="report"/>: every row
    /// measured again on a reset or new metrics, otherwise the rows the batches added or relabelled
    /// measured, and the rows they removed forgotten, batch by batch in order.</summary>
    internal async ValueTask<IReadOnlyList<double>> WidthsAsync(PivotReport report, PivotReportLabelMetrics metrics,
        IReadOnlyList<(IReadOnlyList<PivotReportRow> Labels, IReadOnlyList<PivotRowKey> Removed)> batches, bool reset, Slicer slicer)
    {
        if (reset || _metrics is null || !_metrics.SameAs(metrics) || _widths.Length != report.LabelColumns.Count)
        {
            _rows.Clear();
            _widths = Enumerable.Range(0, report.LabelColumns.Count).Select(_ => new SortedDictionary<double, int>()).ToArray();
            // Not the metrics until every row is measured: a pass cancelled before then is begun
            // again whole.
            _metrics = null;
            foreach (var row in report.Rows)
            {
                Add(row, metrics);
                if (slicer.Done(4)) await slicer.PauseAsync().ConfigureAwait(false);
            }
            _metrics = metrics;
        }
        else
        {
            foreach (var (labels, removed) in batches)
            {
                foreach (var key in removed)
                    Remove(key);
                foreach (var row in labels)
                {
                    Remove(row.Key);
                    Add(row, metrics);
                    if (slicer.Done(4)) await slicer.PauseAsync().ConfigureAwait(false);
                }
            }
        }
        return Array.AsReadOnly(_widths.Select(column => column.Count == 0 ? 0d : column.Last().Key).ToArray());
    }

    /// <summary>A version published without widths: the next one asked for measures every row,
    /// since the batches between were not counted.</summary>
    internal void Forget() => _metrics = null;

    private void Add(PivotReportRow row, PivotReportLabelMetrics metrics)
    {
        var widths = new double[_widths.Length];
        for (var column = 0; column < widths.Length; column++)
        {
            var label = row.Labels[column];
            var width = label.Text is null && label.Toggle is null ? 0
                : metrics.EstimatePx(label.Text ?? "") + Math.Round(metrics.FullWidthPx) * (label.Indent + (label.Toggle is null ? 0 : 1));
            widths[column] = width;
            _widths[column][width] = _widths[column].GetValueOrDefault(width) + 1;
        }
        _rows.Add(row.Key, widths);
    }
    private void Remove(PivotRowKey key)
    {
        if (!_rows.Remove(key, out var widths))
            return;
        for (var column = 0; column < widths.Length; column++)
        {
            var width = widths[column];
            if (_widths[column][width] == 1)
                _widths[column].Remove(width);
            else
                _widths[column][width]--;
        }
    }
}
