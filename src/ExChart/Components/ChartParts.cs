using System.Globalization;
using System.Text;
using global::ExChart.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace ExChart.Components;

// The parts of a chart that change on their own (ADR-0252): each compares the immutable piece of
// the scene it was given with the one it last rendered — recorded as it renders, since the first
// render asks no ShouldRender — and renders only when that piece changed. The scene's pieces are records
// over EquatableList, so an unchanged piece compares by reference first and costs nothing.

/// <summary>Numbers written into SVG attributes: invariant, to a hundredth of a pixel.</summary>
internal static class Svg
{
    internal static string N(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>A marker's outline at (x, y): a circle, square, diamond or triangle of radius r, as one subpath.</summary>
    internal static void Marker(StringBuilder d, MarkerShape shape, double x, double y, double r)
    {
        switch (shape)
        {
            case MarkerShape.Square:
                d.Append('M').Append(N(x - r)).Append(',').Append(N(y - r)).Append('h').Append(N(2 * r)).Append('v').Append(N(2 * r)).Append('h').Append(N(-2 * r)).Append('Z');
                break;
            case MarkerShape.Diamond:
                d.Append('M').Append(N(x)).Append(',').Append(N(y - r * 1.25)).Append('L').Append(N(x + r * 1.25)).Append(',').Append(N(y))
                    .Append('L').Append(N(x)).Append(',').Append(N(y + r * 1.25)).Append('L').Append(N(x - r * 1.25)).Append(',').Append(N(y)).Append('Z');
                break;
            case MarkerShape.Triangle:
                d.Append('M').Append(N(x)).Append(',').Append(N(y - r * 1.3)).Append('L').Append(N(x + r * 1.2)).Append(',').Append(N(y + r * 0.8))
                    .Append('L').Append(N(x - r * 1.2)).Append(',').Append(N(y + r * 0.8)).Append('Z');
                break;
            default:
                d.Append('M').Append(N(x - r)).Append(',').Append(N(y)).Append("a").Append(N(r)).Append(',').Append(N(r)).Append(" 0 1,0 ").Append(N(2 * r)).Append(",0")
                    .Append("a").Append(N(r)).Append(',').Append(N(r)).Append(" 0 1,0 ").Append(N(-2 * r)).Append(",0Z");
                break;
        }
    }

    /// <summary>The classes a look is painted by: the stylesheet's Visual Tokens say what each slot looks like (ADR-0256).</summary>
    internal static string LookClasses(SeriesLook look) =>
        string.Create(CultureInfo.InvariantCulture, $"ex-chart-colour-{look.Colour} ex-chart-line-{look.LineStyle}");
}

/// <summary>A panel's plot frame, grid lines, zero line, break mark and vertical axes; and under the last panel, the horizontal axis.</summary>
internal sealed class ChartAxesView : ComponentBase
{
    [Parameter, EditorRequired] public PanelScene Panel { get; set; } = null!;
    [Parameter, EditorRequired] public EquatableList<(double Position, double X, string? Label)> XTicks { get; set; } = null!;
    [Parameter] public bool Last { get; set; }
    [Parameter] public double LineHeight { get; set; }
    [Parameter] public int Angle { get; set; }

    private PanelScene? _panel;
    private EquatableList<(double Position, double X, string? Label)>? _ticks;
    private bool _last;
    private double _lineHeight;
    private int _angle;

    protected override bool ShouldRender()
        => !(Equals(_panel, Panel) && Equals(_ticks, XTicks) && _last == Last && _lineHeight == LineHeight && _angle == Angle);

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        (_panel, _ticks, _last, _lineHeight, _angle) = (Panel, XTicks, Last, LineHeight, Angle);
        var p = Panel.Plot;
        var right = p.X + p.Width;
        var bottom = p.Y + p.Height;
        b.OpenElement(0, "g");
        b.AddAttribute(1, "class", "ex-chart-axes");
        b.OpenElement(2, "rect");
        b.AddAttribute(3, "class", "ex-chart-plot");
        b.AddAttribute(4, "x", Svg.N(p.X));
        b.AddAttribute(5, "y", Svg.N(p.Y));
        b.AddAttribute(6, "width", Svg.N(p.Width));
        b.AddAttribute(7, "height", Svg.N(p.Height));
        b.CloseElement();

        // Vertical grid lines at the horizontal axis' ticks, in every panel, so a date can be followed down.
        foreach (var t in XTicks)
        {
            b.OpenElement(10, "line");
            b.AddAttribute(11, "class", "ex-chart-grid");
            b.AddAttribute(12, "x1", Svg.N(t.X));
            b.AddAttribute(13, "x2", Svg.N(t.X));
            b.AddAttribute(14, "y1", Svg.N(p.Y));
            b.AddAttribute(15, "y2", Svg.N(bottom));
            b.CloseElement();
        }

        if (Panel.Left is { } left)
            Vertical(b, 20, left, p.X - LineHeight / 4, "end", p);
        if (Panel.Right is { } r)
            Vertical(b, 40, r, right + LineHeight / 4, "start", p);

        if (Panel.ZeroY is { } zero)
        {
            b.OpenElement(60, "line");
            b.AddAttribute(61, "class", "ex-chart-zero");
            b.AddAttribute(62, "x1", Svg.N(p.X));
            b.AddAttribute(63, "x2", Svg.N(right));
            b.AddAttribute(64, "y1", Svg.N(zero));
            b.AddAttribute(65, "y2", Svg.N(zero));
            b.CloseElement();
        }

        if (Last)
        {
            foreach (var t in XTicks)
            {
                if (t.Label is null) continue;
                b.OpenElement(70, "text");
                b.AddAttribute(71, "class", "ex-chart-tick ex-chart-tick-x");
                b.AddAttribute(72, "x", Svg.N(t.X));
                b.AddAttribute(73, "y", Svg.N(bottom + LineHeight));
                b.AddAttribute(74, "text-anchor", Angle == 0 ? "middle" : "end");
                if (Angle != 0)
                    b.AddAttribute(75, "transform", string.Create(CultureInfo.InvariantCulture, $"rotate({-Angle} {Svg.N(t.X)} {Svg.N(bottom + LineHeight)})"));
                b.AddContent(76, t.Label);
                b.CloseElement();
            }
        }
        b.CloseElement();
    }

    private void Vertical(RenderTreeBuilder b, int seq, VerticalAxis axis, double x, string anchor, Box p)
    {
        b.OpenRegion(seq);
        foreach (var (_, y, label) in axis.Ticks)
        {
            if (axis.Side == AxisSide.Left)
            {
                b.OpenElement(0, "line");
                b.AddAttribute(1, "class", "ex-chart-grid");
                b.AddAttribute(2, "x1", Svg.N(p.X));
                b.AddAttribute(3, "x2", Svg.N(p.X + p.Width));
                b.AddAttribute(4, "y1", Svg.N(y));
                b.AddAttribute(5, "y2", Svg.N(y));
                b.CloseElement();
            }
            b.OpenElement(6, "text");
            b.AddAttribute(7, "class", "ex-chart-tick");
            b.AddAttribute(8, "x", Svg.N(x));
            b.AddAttribute(9, "y", Svg.N(y));
            b.AddAttribute(10, "dy", "0.35em");
            b.AddAttribute(11, "text-anchor", anchor);
            b.AddContent(12, label);
            b.CloseElement();
        }
        if (axis.Broken)
        {
            // The axis does not reach zero (ADR-0253): a break mark at its foot says so.
            var edge = axis.Side == AxisSide.Left ? p.X : p.X + p.Width;
            var s = LineHeight / 4;
            var foot = p.Y + p.Height;
            b.OpenElement(13, "path");
            b.AddAttribute(14, "class", "ex-chart-break");
            b.AddAttribute(15, "d", string.Create(CultureInfo.InvariantCulture,
                $"M{Svg.N(edge - s)},{Svg.N(foot - s * 1.5)}l{Svg.N(2 * s)},{Svg.N(-s)}M{Svg.N(edge - s)},{Svg.N(foot - s * 0.5)}l{Svg.N(2 * s)},{Svg.N(-s)}"));
            b.CloseElement();
        }
        b.CloseRegion();
    }
}

/// <summary>One series: its lines as one path, its markers as two (filled and open), its extrapolated ranges. Its node count does not grow with its points (CH-3).</summary>
internal sealed class ChartSeriesView : ComponentBase
{
    [Parameter, EditorRequired] public SeriesPiece Piece { get; set; } = null!;
    [Parameter] public Box Plot { get; set; }
    [Parameter] public double LineHeight { get; set; }

    private SeriesPiece? _piece;
    private Box _plot;
    private double _lineHeight;

    protected override bool ShouldRender()
        => !(Equals(_piece, Piece) && _plot == Plot && _lineHeight == LineHeight);

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        (_piece, _plot, _lineHeight) = (Piece, Plot, LineHeight);
        var piece = Piece;
        b.OpenElement(0, "g");
        b.AddAttribute(1, "class", "ex-chart-series " + Svg.LookClasses(piece.Look) + (piece.Interpolated ? " ex-chart-interpolated" : ""));
        b.AddAttribute(2, "data-series", piece.Id);
        foreach (var (from, to) in piece.Extrapolated)
        {
            b.OpenElement(3, "rect");
            b.AddAttribute(4, "class", "ex-chart-extrapolated");
            b.AddAttribute(5, "x", Svg.N(from));
            b.AddAttribute(6, "y", Svg.N(Plot.Y));
            b.AddAttribute(7, "width", Svg.N(Math.Max(0, to - from)));
            b.AddAttribute(8, "height", Svg.N(Plot.Height));
            b.CloseElement();
        }

        if (piece.Edges.Count > 0)
        {
            // A node's edges, under its markers (ADR-0321).
            var e = new StringBuilder();
            foreach (var edge in piece.Edges)
                e.Append('M').Append(Svg.N(edge.X1)).Append(',').Append(Svg.N(edge.Y1)).Append('L').Append(Svg.N(edge.X2)).Append(',').Append(Svg.N(edge.Y2));
            b.OpenRegion(9);
            b.OpenElement(0, "path");
            b.AddAttribute(1, "class", "ex-chart-edges");
            b.AddAttribute(2, "d", e.ToString());
            b.CloseElement();
            b.CloseRegion();
        }

        var d = new StringBuilder();
        foreach (var line in piece.Lines)
            for (var i = 0; i < line.Count; i++)
                d.Append(i == 0 ? 'M' : 'L').Append(Svg.N(line[i].X)).Append(',').Append(Svg.N(line[i].Y));
        b.OpenElement(10, "path");
        b.AddAttribute(11, "class", "ex-chart-path");
        b.AddAttribute(12, "d", d.ToString());
        b.CloseElement();

        var r = LineHeight / 5;
        var filled = new StringBuilder();
        var open = new StringBuilder();
        // A marker coloured from a column is filled with its band's Visual Token (ADR-0320).
        var banded = new SortedDictionary<int, StringBuilder>();
        foreach (var m in piece.Markers)
        {
            var into = !m.Filled ? open
                : m.Band is { } band ? banded.TryGetValue(band, out var sb) ? sb : banded[band] = new StringBuilder()
                : filled;
            Svg.Marker(into, piece.Look.Shape, m.X, m.Y, m.Radius ?? r);
        }
        b.OpenElement(20, "path");
        b.AddAttribute(21, "class", "ex-chart-markers");
        b.AddAttribute(22, "d", filled.ToString());
        b.CloseElement();
        b.OpenElement(23, "path");
        b.AddAttribute(24, "class", "ex-chart-markers ex-chart-markers-open");
        b.AddAttribute(25, "d", open.ToString());
        b.CloseElement();
        foreach (var (band, d2) in banded)
        {
            b.OpenElement(26, "path");
            b.SetKey(band);
            b.AddAttribute(27, "class", string.Create(CultureInfo.InvariantCulture, $"ex-chart-markers ex-chart-{(piece.ColourDiverging ? "diverge" : "heat")}-{band}"));
            b.AddAttribute(28, "d", d2.ToString());
            b.CloseElement();
        }
        if (piece.Candles.Count > 0 || piece.Volumes.Count > 0) Candles(b, piece);
        if (piece.Whiskers.Count > 0 || piece.Bands.Count > 0) Intervals(b, piece, r);
        if (piece.Markers.Any(m => m.YBounds is not null || m.XBounds is not null)) MarkIntervals(b, piece, r);
        b.CloseElement();
    }

    private static void Path(RenderTreeBuilder b, int seq, string cls, StringBuilder d)
    {
        if (d.Length == 0) return;
        b.OpenElement(seq, "path");
        b.AddAttribute(seq + 1, "class", cls);
        b.AddAttribute(seq + 2, "d", d.ToString());
        b.CloseElement();
    }

    private static void Rect(StringBuilder d, double x0, double y0, double x1, double y1) =>
        d.Append('M').Append(Svg.N(x0)).Append(',').Append(Svg.N(Math.Min(y0, y1))).Append('H').Append(Svg.N(x1))
            .Append('V').Append(Svg.N(Math.Max(y0, y1))).Append('H').Append(Svg.N(x0)).Append('Z');

    private static void VLine(StringBuilder d, double x, double y0, double y1) =>
        d.Append('M').Append(Svg.N(x)).Append(',').Append(Svg.N(y0)).Append('V').Append(Svg.N(y1));

    // A candle's wick runs from its high to the body and from the body to its low, never through a
    // hollow body; a High-Low-Close bar is its range with the close ticked to the right (ADR-0300).
    private static void Candles(RenderTreeBuilder b, SeriesPiece piece)
    {
        var rising = new StringBuilder();
        var falling = new StringBuilder();
        var unchanged = new StringBuilder();
        var hlc = new StringBuilder();
        foreach (var c in piece.Candles)
        {
            if (c.OpenY is not { } o)
            {
                VLine(hlc, c.X, c.HighY, c.LowY);
                hlc.Append('M').Append(Svg.N(c.X)).Append(',').Append(Svg.N(c.CloseY)).Append('H').Append(Svg.N(c.X + c.HalfWidth));
                continue;
            }
            if (c.Unchanged)
            {
                VLine(unchanged, c.X, c.HighY, c.LowY);
                unchanged.Append('M').Append(Svg.N(c.X - c.HalfWidth)).Append(',').Append(Svg.N(o)).Append('H').Append(Svg.N(c.X + c.HalfWidth));
                continue;
            }
            var d = c.Rising ? rising : falling;
            var (top, bottom) = (Math.Min(o, c.CloseY), Math.Max(o, c.CloseY));
            VLine(d, c.X, c.HighY, top);
            VLine(d, c.X, bottom, c.LowY);
            Rect(d, c.X - c.HalfWidth, top, c.X + c.HalfWidth, bottom);
        }
        foreach (var v in piece.Volumes)
            Rect(v.Unchanged ? unchanged : v.Rising ? rising : falling, v.X - v.HalfWidth, v.TopY, v.X + v.HalfWidth, v.BaseY);
        Path(b, 30, "ex-chart-candles ex-chart-rising", rising);
        Path(b, 33, "ex-chart-candles ex-chart-falling", falling);
        Path(b, 36, "ex-chart-candles ex-chart-unchanged", unchanged);
        Path(b, 39, "ex-chart-candles ex-chart-hlc", hlc);
    }

    // An Interval: whiskers with a cap at each bound, or a band between the bounds over each run of
    // neighbours; a run of one is drawn as a whisker, never dropped (ADR-0303).
    private static void Intervals(RenderTreeBuilder b, SeriesPiece piece, double r)
    {
        var whiskers = new StringBuilder();
        var band = new StringBuilder();
        void Whisker(DrawnWhisker w)
        {
            VLine(whiskers, w.X, w.Lower, w.Upper);
            whiskers.Append('M').Append(Svg.N(w.X - r)).Append(',').Append(Svg.N(w.Lower)).Append('h').Append(Svg.N(2 * r));
            whiskers.Append('M').Append(Svg.N(w.X - r)).Append(',').Append(Svg.N(w.Upper)).Append('h').Append(Svg.N(2 * r));
        }
        foreach (var w in piece.Whiskers) Whisker(w);
        foreach (var run in piece.Bands)
        {
            if (run.Count == 1) { Whisker(run[0]); continue; }
            for (var i = 0; i < run.Count; i++)
                band.Append(i == 0 ? 'M' : 'L').Append(Svg.N(run[i].X)).Append(',').Append(Svg.N(run[i].Upper));
            for (var i = run.Count - 1; i >= 0; i--)
                band.Append('L').Append(Svg.N(run[i].X)).Append(',').Append(Svg.N(run[i].Lower));
            band.Append('Z');
        }
        Path(b, 42, "ex-chart-interval-band", band);
        Path(b, 45, "ex-chart-whiskers", whiskers);
    }

    // An Observation's intervals: a whisker up and down and one across, each capped, through its mark;
    // a merged mark's is the union of its points' (ADR-0304).
    private static void MarkIntervals(RenderTreeBuilder b, SeriesPiece piece, double r)
    {
        var d = new StringBuilder();
        foreach (var m in piece.Markers)
        {
            if (m.YBounds is { } y)
            {
                VLine(d, m.X, y.Lower, y.Upper);
                d.Append('M').Append(Svg.N(m.X - r)).Append(',').Append(Svg.N(y.Lower)).Append('h').Append(Svg.N(2 * r));
                d.Append('M').Append(Svg.N(m.X - r)).Append(',').Append(Svg.N(y.Upper)).Append('h').Append(Svg.N(2 * r));
            }
            if (m.XBounds is { } x)
            {
                d.Append('M').Append(Svg.N(x.Lower)).Append(',').Append(Svg.N(m.Y)).Append('H').Append(Svg.N(x.Upper));
                d.Append('M').Append(Svg.N(x.Lower)).Append(',').Append(Svg.N(m.Y - r)).Append('v').Append(Svg.N(2 * r));
                d.Append('M').Append(Svg.N(x.Upper)).Append(',').Append(Svg.N(m.Y - r)).Append('v').Append(Svg.N(2 * r));
            }
        }
        Path(b, 48, "ex-chart-whiskers", d);
    }
}

/// <summary>A panel's legend (ADR-0271): every series of the panel, a hidden one faded; a click or Enter hides or shows it.</summary>
internal sealed class ChartLegendView : ComponentBase
{
    [Parameter, EditorRequired] public EquatableList<LegendEntry> Entries { get; set; } = null!;
    [Parameter] public double LineHeight { get; set; }
    [Parameter] public EventCallback<string> Toggle { get; set; }

    private EquatableList<LegendEntry>? _entries;
    private double _lineHeight;

    protected override bool ShouldRender()
        => !(Equals(_entries, Entries) && _lineHeight == LineHeight);

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        (_entries, _lineHeight) = (Entries, LineHeight);
        if (Entries.Count == 0) return;
        var lh = LineHeight;
        b.OpenElement(0, "g");
        b.AddAttribute(1, "class", "ex-chart-legend");
        foreach (var e in Entries)
        {
            var id = e.Id;
            var box = e.Box;
            var mid = box.Y + box.Height / 2;
            var swatch = lh * 1.5;
            b.OpenElement(2, "g");
            b.SetKey(id);
            b.AddAttribute(3, "class", "ex-chart-legend-entry " + Svg.LookClasses(e.Look) + (e.Hidden ? " ex-chart-hidden" : ""));
            b.AddAttribute(4, "role", "button");
            b.AddAttribute(5, "tabindex", "0");
            b.AddAttribute(6, "aria-pressed", e.Hidden ? "false" : "true");
            b.AddAttribute(7, "data-series", id);
            b.AddAttribute(8, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => Toggle.InvokeAsync(id)));
            b.AddAttribute(9, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, k =>
                k.Key is "Enter" or " " ? Toggle.InvokeAsync(id) : Task.CompletedTask));
            b.OpenElement(10, "rect");
            b.AddAttribute(11, "class", "ex-chart-legend-hit");
            b.AddAttribute(12, "x", Svg.N(box.X));
            b.AddAttribute(13, "y", Svg.N(box.Y));
            b.AddAttribute(14, "width", Svg.N(box.Width));
            b.AddAttribute(15, "height", Svg.N(box.Height));
            b.CloseElement();
            b.OpenElement(16, "line");
            b.AddAttribute(17, "class", "ex-chart-path");
            b.AddAttribute(18, "x1", Svg.N(box.X));
            b.AddAttribute(19, "x2", Svg.N(box.X + swatch));
            b.AddAttribute(20, "y1", Svg.N(mid));
            b.AddAttribute(21, "y2", Svg.N(mid));
            b.CloseElement();
            var marker = new StringBuilder();
            Svg.Marker(marker, e.Look.Shape, box.X + swatch / 2, mid, lh / 5);
            b.OpenElement(22, "path");
            b.AddAttribute(23, "class", "ex-chart-markers");
            b.AddAttribute(24, "d", marker.ToString());
            b.CloseElement();
            b.OpenElement(25, "text");
            b.AddAttribute(26, "class", "ex-chart-legend-name");
            b.AddAttribute(27, "x", Svg.N(box.X + swatch + lh / 4));
            b.AddAttribute(28, "y", Svg.N(mid));
            b.AddAttribute(29, "dy", "0.35em");
            b.AddContent(30, e.Name);
            b.CloseElement();
            b.CloseElement();
        }
        b.CloseElement();
    }
}

/// <summary>The vertical guide at the pointer's position, across every panel, and the dots of the pointer's panel (ADR-0270).</summary>
internal sealed class ChartGuideView : ComponentBase
{
    [Parameter] public double? X { get; set; }
    [Parameter] public double Top { get; set; }
    [Parameter] public double Bottom { get; set; }
    [Parameter] public EquatableList<(double Y, SeriesLook Look)> Dots { get; set; } = EquatableList<(double, SeriesLook)>.Empty;
    [Parameter] public double LineHeight { get; set; }

    private (double? X, double Top, double Bottom, EquatableList<(double, SeriesLook)>? Dots, double LineHeight) _last;

    protected override bool ShouldRender()
    {
        return !(_last.X == X && _last.Top == Top && _last.Bottom == Bottom && Equals(_last.Dots, Dots) && _last.LineHeight == LineHeight);
    }

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        _last = (X, Top, Bottom, Dots, LineHeight);
        if (X is not { } x) return;
        b.OpenElement(0, "g");
        b.AddAttribute(1, "class", "ex-chart-guide");
        b.OpenElement(2, "line");
        b.AddAttribute(3, "class", "ex-chart-guide-line");
        b.AddAttribute(4, "x1", Svg.N(x));
        b.AddAttribute(5, "x2", Svg.N(x));
        b.AddAttribute(6, "y1", Svg.N(Top));
        b.AddAttribute(7, "y2", Svg.N(Bottom));
        b.CloseElement();
        foreach (var (y, look) in Dots)
        {
            b.OpenElement(8, "circle");
            b.AddAttribute(9, "class", "ex-chart-dot " + Svg.LookClasses(look));
            b.AddAttribute(10, "cx", Svg.N(x));
            b.AddAttribute(11, "cy", Svg.N(y));
            b.AddAttribute(12, "r", Svg.N(LineHeight / 5));
            b.CloseElement();
        }
        b.CloseElement();
    }
}

/// <summary>The band a drag across the plot draws (ADR-0273).</summary>
internal sealed class ChartBandView : ComponentBase
{
    [Parameter] public (double From, double To)? Span { get; set; }
    [Parameter] public double Top { get; set; }
    [Parameter] public double Bottom { get; set; }

    private ((double From, double To)? Span, double Top, double Bottom) _last;

    protected override bool ShouldRender()
    {
        return _last != (Span, Top, Bottom);
    }

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        _last = (Span, Top, Bottom);
        if (Span is not { } s) return;
        b.OpenElement(0, "rect");
        b.AddAttribute(1, "class", "ex-chart-band");
        b.AddAttribute(2, "x", Svg.N(Math.Min(s.From, s.To)));
        b.AddAttribute(3, "y", Svg.N(Top));
        b.AddAttribute(4, "width", Svg.N(Math.Abs(s.To - s.From)));
        b.AddAttribute(5, "height", Svg.N(Bottom - Top));
        b.CloseElement();
    }
}

/// <summary>The tooltip (ADR-0270): the position, then a line per series, then "and M more series".</summary>
internal sealed class ChartTooltipView : ComponentBase
{
    [Parameter] public ChartReading? Reading { get; set; }
    [Parameter] public double Left { get; set; }
    [Parameter] public double Top { get; set; }
    [Parameter] public bool Flip { get; set; }
    [Parameter] public double LineHeight { get; set; }

    private (ChartReading? Reading, double Left, double Top, bool Flip, double LineHeight) _last;

    protected override bool ShouldRender()
    {
        return !(Equals(_last.Reading, Reading) && _last.Left == Left && _last.Top == Top && _last.Flip == Flip && _last.LineHeight == LineHeight);
    }

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        _last = (Reading, Left, Top, Flip, LineHeight);
        if (Reading is not { } reading) return;
        var lh = LineHeight;
        b.OpenElement(0, "div");
        b.AddAttribute(1, "class", "ex-chart-tooltip" + (Flip ? " ex-chart-tooltip-flip" : ""));
        b.AddAttribute(2, "style", string.Create(CultureInfo.InvariantCulture, $"left:{Svg.N(Left)}px;top:{Svg.N(Top)}px;line-height:{Svg.N(lh)}px"));
        b.OpenElement(3, "div");
        b.AddAttribute(4, "class", "ex-chart-tooltip-position");
        b.AddContent(5, reading.Position);
        b.CloseElement();
        foreach (var line in reading.Lines)
        {
            b.OpenElement(6, "div");
            b.SetKey(line.Id);
            b.AddAttribute(7, "class", "ex-chart-tooltip-line " + Svg.LookClasses(line.Look));
            b.AddAttribute(8, "data-series", line.Id);
            b.OpenElement(9, "svg");
            b.AddAttribute(10, "class", "ex-chart-swatch");
            b.AddAttribute(11, "width", Svg.N(lh * 1.5));
            b.AddAttribute(12, "height", Svg.N(lh));
            b.AddAttribute(13, "aria-hidden", "true");
            b.OpenElement(14, "line");
            b.AddAttribute(15, "class", "ex-chart-path");
            b.AddAttribute(16, "x1", "0");
            b.AddAttribute(17, "x2", Svg.N(lh * 1.5));
            b.AddAttribute(18, "y1", Svg.N(lh / 2));
            b.AddAttribute(19, "y2", Svg.N(lh / 2));
            b.CloseElement();
            var marker = new StringBuilder();
            Svg.Marker(marker, line.Look.Shape, lh * 0.75, lh / 2, lh / 5);
            b.OpenElement(20, "path");
            b.AddAttribute(21, "class", "ex-chart-markers");
            b.AddAttribute(22, "d", marker.ToString());
            b.CloseElement();
            b.CloseElement();
            b.OpenElement(23, "span");
            b.AddAttribute(24, "class", "ex-chart-tooltip-name");
            b.AddContent(25, line.Name);
            b.CloseElement();
            b.OpenElement(26, "span");
            b.AddAttribute(27, "class", "ex-chart-tooltip-value");
            b.AddContent(28, line.Value ?? "–");
            b.CloseElement();
            if (line.Note is { } note)
            {
                b.OpenElement(29, "span");
                b.AddAttribute(30, "class", "ex-chart-tooltip-note");
                b.AddContent(31, note);
                b.CloseElement();
            }
            b.CloseElement();
        }
        if (reading.More > 0)
        {
            b.OpenElement(32, "div");
            b.AddAttribute(33, "class", "ex-chart-tooltip-more");
            b.AddContent(34, reading.More == 1 ? "and 1 more series" : string.Create(CultureInfo.InvariantCulture, $"and {reading.More} more series"));
            b.CloseElement();
        }
        b.CloseElement();
    }
}

/// <summary>
/// The keys of colours and sizes read from a column, beside the plot (ADR-0320): a colour bar of the
/// bands with its ticks, and a size key of markers at round values. Always drawn with what they key.
/// </summary>
internal sealed class ChartKeysView : ComponentBase
{
    [Parameter, EditorRequired] public EquatableList<ColourKey> Colours { get; set; } = EquatableList<ColourKey>.Empty;
    [Parameter, EditorRequired] public EquatableList<SizeKey> Sizes { get; set; } = EquatableList<SizeKey>.Empty;
    [Parameter] public double LineHeight { get; set; }

    private EquatableList<ColourKey>? _colours;
    private EquatableList<SizeKey>? _sizes;
    private double _lineHeight;

    protected override bool ShouldRender() => !(Equals(_colours, Colours) && Equals(_sizes, Sizes) && _lineHeight == LineHeight);

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        (_colours, _sizes, _lineHeight) = (Colours, Sizes, LineHeight);
        var lh = LineHeight;
        foreach (var key in Colours)
        {
            var box = key.Box;
            b.OpenElement(0, "g");
            b.SetKey("colour:" + key.SeriesId);
            b.AddAttribute(1, "class", "ex-chart-key ex-chart-colour-key");
            b.AddAttribute(2, "data-series", key.SeriesId);
            Title(b, 3, key.Title, box.X, box.Y - lh / 2);
            var band = box.Height / key.Bands;
            for (var i = 0; i < key.Bands; i++)
            {
                b.OpenElement(10, "rect");
                b.AddAttribute(11, "class", string.Create(CultureInfo.InvariantCulture, $"ex-chart-key-band ex-chart-{(key.Diverging ? "diverge" : "heat")}-{i}"));
                b.AddAttribute(12, "x", Svg.N(box.X));
                b.AddAttribute(13, "y", Svg.N(box.Y + box.Height - (i + 1) * band));
                b.AddAttribute(14, "width", Svg.N(box.Width));
                b.AddAttribute(15, "height", Svg.N(band));
                b.CloseElement();
            }
            b.OpenElement(20, "rect");
            b.AddAttribute(21, "class", "ex-chart-key-frame");
            b.AddAttribute(22, "x", Svg.N(box.X));
            b.AddAttribute(23, "y", Svg.N(box.Y));
            b.AddAttribute(24, "width", Svg.N(box.Width));
            b.AddAttribute(25, "height", Svg.N(box.Height));
            b.CloseElement();
            foreach (var t in key.Ticks)
            {
                b.OpenElement(30, "text");
                b.AddAttribute(31, "class", "ex-chart-tick");
                b.AddAttribute(32, "x", Svg.N(box.X + box.Width + lh / 3));
                b.AddAttribute(33, "y", Svg.N(t.Y));
                b.AddAttribute(34, "dy", "0.35em");
                b.AddContent(35, t.Label);
                b.CloseElement();
            }
            b.CloseElement();
        }
        foreach (var key in Sizes)
        {
            var box = key.Box;
            b.OpenElement(40, "g");
            b.SetKey("size:" + key.SeriesId);
            b.AddAttribute(41, "class", "ex-chart-key ex-chart-size-key");
            b.AddAttribute(42, "data-series", key.SeriesId);
            Title(b, 43, key.Title, box.X, box.Y - lh / 2);
            var y = box.Y;
            var cx = box.X + box.Width / 2;
            foreach (var e in key.Entries)
            {
                var row = Math.Max(2 * e.Radius, lh);
                var cy = y + row / 2;
                b.OpenElement(50, "circle");
                b.AddAttribute(51, "class", "ex-chart-key-size");
                b.AddAttribute(52, "cx", Svg.N(cx));
                b.AddAttribute(53, "cy", Svg.N(cy));
                b.AddAttribute(54, "r", Svg.N(e.Radius));
                b.CloseElement();
                b.OpenElement(55, "text");
                b.AddAttribute(56, "class", "ex-chart-tick");
                b.AddAttribute(57, "x", Svg.N(box.X + box.Width + lh / 3));
                b.AddAttribute(58, "y", Svg.N(cy));
                b.AddAttribute(59, "dy", "0.35em");
                b.AddContent(60, e.Label);
                b.CloseElement();
                y += row + lh / 4;
            }
            b.CloseElement();
        }
    }

    private static void Title(RenderTreeBuilder b, int seq, string title, double x, double y)
    {
        b.OpenElement(seq, "text");
        b.AddAttribute(seq + 1, "class", "ex-chart-key-title");
        b.AddAttribute(seq + 2, "x", Svg.N(x));
        b.AddAttribute(seq + 3, "y", Svg.N(y));
        b.AddContent(seq + 4, title);
        b.CloseElement();
    }
}
