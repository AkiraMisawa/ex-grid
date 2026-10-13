using System.Globalization;
using System.Security;
using System.Text;

namespace ExposureChart.Charting;

/// <summary>
/// Writes a <see cref="ChartModel"/> as one SVG document, in C#. No JavaScript: the hover guide
/// and the readout are CSS <c>:hover</c> and a native <c>&lt;title&gt;</c>, painted from markup
/// written once, so nothing crosses the circuit while the pointer moves. The same string is the
/// page's picture and the downloadable file, and the same model always writes the same bytes.
/// </summary>
public static class ChartSvg
{
    private const double Width = 960;
    private const double Left = 72, Right = 72, Top = 34, Gap = 18;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // Defaults for a file opened on its own; on the page the Visual Tokens win.
    private const string Style = """
        .ex-chart-root { font: 11px system-ui, sans-serif; fill: var(--ex-chart-text, #374151); }
        .ex-chart-title { font-weight: 600; font-size: 12px; }
        .ex-chart-grid { stroke: var(--ex-chart-grid, #e5e7eb); stroke-width: 1; }
        .ex-chart-root .ex-chart-minor { stroke-opacity: 0.45; }
        .ex-chart-zero { stroke: var(--ex-chart-zero, #9ca3af); stroke-width: 1; }
        .ex-chart-frame { fill: none; stroke: var(--ex-chart-grid, #e5e7eb); }
        .ex-chart-root .ex-chart-line { fill: none; stroke-width: 1.75; stroke-linejoin: round; }
        .ex-chart-slot-0 { stroke: var(--ex-chart-slot-0, #9ca3af); fill: var(--ex-chart-slot-0, #9ca3af); }
        .ex-chart-slot-1 { stroke: var(--ex-chart-slot-1, #2563eb); fill: var(--ex-chart-slot-1, #2563eb); }
        .ex-chart-slot-2 { stroke: var(--ex-chart-slot-2, #d97706); fill: var(--ex-chart-slot-2, #d97706); }
        .ex-chart-slot-3 { stroke: var(--ex-chart-slot-3, #059669); fill: var(--ex-chart-slot-3, #059669); }
        .ex-chart-slot-4 { stroke: var(--ex-chart-slot-4, #be185d); fill: var(--ex-chart-slot-4, #be185d); }
        .ex-chart-slot-5 { stroke: var(--ex-chart-slot-5, #7c3aed); fill: var(--ex-chart-slot-5, #7c3aed); }
        .ex-chart-slot-6 { stroke: var(--ex-chart-slot-6, #0891b2); fill: var(--ex-chart-slot-6, #0891b2); }
        .ex-chart-slot-7 { stroke: var(--ex-chart-slot-7, #65a30d); fill: var(--ex-chart-slot-7, #65a30d); }
        .ex-chart-line.ex-chart-group-1 { stroke-dasharray: 6 3; }
        .ex-chart-line.ex-chart-group-2 { stroke-dasharray: 2 2; }
        .ex-chart-line.ex-chart-group-3 { stroke-dasharray: 8 3 2 3; }
        .ex-chart-secondary-label { font-style: italic; }
        .ex-chart-unit { font-size: 11px; fill: var(--ex-chart-text-muted, #6b7280); }
        .ex-chart-hit rect { fill: transparent; }
        .ex-chart-hit .ex-chart-hover { visibility: hidden; }
        .ex-chart-hit:hover .ex-chart-hover { visibility: visible; }
        .ex-chart-guide { stroke: var(--ex-chart-guide, #6b7280); stroke-width: 1; stroke-dasharray: 3 3; }
        .ex-chart-root .ex-chart-marker { stroke-width: 1.3; }
        .ex-chart-root .ex-chart-open { fill: var(--ex-chart-surface, #fff); }
        .ex-chart-dot { stroke: var(--ex-chart-surface, #fff); stroke-width: 1.5; }
        """;

    /// <summary>Where a tooltip stands, as fractions of the drawn chart, so the page places it
    /// with percentages and never measures the SVG.</summary>
    public readonly record struct TipAnchor(double X, double Top, bool OnLeft);

    public static TipAnchor Anchor(ChartModel model, int index)
    {
        var (_, height, legendHeight) = Heights(model, legend: false);
        var x = XOf(model, model.Xs[index]);
        return new TipAnchor(x / Width, (legendHeight + Top) / height, x / Width > 0.6);
    }

    // The page draws its own legend, in HTML, where a click can hide a series; a file carries one
    // inside the drawing.
    private static (double PanelHeight, double Height, double Legend) Heights(ChartModel model, bool legend)
    {
        var panels = model.Panels.Count;
        double panelHeight = panels == 1 ? 360 : 190;
        double legendHeight = legend ? LegendRows(model) * 18 + 6 : 4;
        var bottom = XAxis.Depth(TicksOf(model), model.XAxis.Angle);
        return (panelHeight, legendHeight + panels * (Top + panelHeight) + (panels - 1) * Gap + bottom, legendHeight);
    }

    private static double XOf(ChartModel model, double position)
    {
        var (from, to) = model.Domain;
        var span = to - from > 0 ? to - from : 1;
        return Left + (Width - Left - Right) * (position - from) / span;
    }

    public static IReadOnlyList<XTick> TicksOf(ChartModel model) =>
        model.Xs.Count == 0 ? [] : XAxis.Ticks(model.Xs, model.Domain, model.Scale, model.XAxis, v => XOf(model, v));

    /// <param name="version">Stamped on the drawing, so that a pointer report taken against an
    /// older drawing is recognised and dropped.</param>
    /// <param name="forFile">A file opened on its own has no page to draw a tooltip: it carries
    /// a native <c>&lt;title&gt;</c> per date instead.</param>
    public static string Write(ChartModel model, int version = 0, bool forFile = true)
    {
        var panels = model.Panels.Count;
        if (panels == 0 || model.Xs.Count == 0)
            return Empty(model, panels);
        var (panelHeight, height, legendHeight) = Heights(model, legend: forFile);
        var plotWidth = Width - Left - Right;
        var svg = new StringBuilder();
        svg.Append(Invariant, $"""<svg xmlns="http://www.w3.org/2000/svg" class="ex-chart-root" data-ex-chart-version="{version}" viewBox="0 0 {Width} {height}" width="{Width}" height="{height}" role="img">""");
        svg.Append("<style>").Append(Style).Append("</style>");


        double X(double position) => XOf(model, position);

        if (forFile) WriteLegend(svg, model, legendHeight);

        var dateTicks = TicksOf(model);
        var layouts = new List<(ChartPanel Panel, double Top, ValueAxis Left, ValueAxis? Right)>();
        var y0 = (double)legendHeight;
        foreach (var panel in model.Panels)
        {
            var leftAxis = Axis.Nice(panel.Series.Where(s => !s.Secondary).SelectMany(Values));
            ValueAxis? rightAxis = null;
            if (panel.Series.Any(s => s.Secondary))
            {
                var right = Axis.Nice(panel.Series.Where(s => s.Secondary).SelectMany(Values));
                (leftAxis, right) = Axis.AlignZeros(leftAxis, right);
                rightAxis = right;
            }
            layouts.Add((panel, y0 + Top, leftAxis, rightAxis));
            y0 += Top + panelHeight + Gap;
        }

        foreach (var (panel, top, leftAxis, rightAxis) in layouts)
        {
            var bottom = top + panelHeight;
            double Y(ValueAxis axis, decimal v) => bottom - panelHeight * axis.Fraction(v);

            if (panel.Title.Length > 0)
                Text(svg, Left, top - 10, panel.Title, "start", "ex-chart-title");
            // Each axis says its unit, from the series that read against it.
            var mid = top + panelHeight / 2;
            if (UnitsOf(panel, secondary: false) is { Length: > 0 } leftUnit)
                svg.Append(Invariant, $"""<text class="ex-chart-unit" x="14" y="{mid:0.##}" text-anchor="middle" transform="rotate(-90 14 {mid:0.##})">{Escape(leftUnit)}</text>""");
            if (UnitsOf(panel, secondary: true) is { Length: > 0 } rightUnit)
                svg.Append(Invariant, $"""<text class="ex-chart-unit" x="{Width - 12}" y="{mid:0.##}" text-anchor="middle" transform="rotate(90 {Width - 12} {mid:0.##})">{Escape(rightUnit)}</text>""");
            foreach (var tick in leftAxis.Ticks())
            {
                var y = Y(leftAxis, tick);
                Line(svg, Left, y, Width - Right, y, tick == 0 ? "ex-chart-zero" : "ex-chart-grid");
                Text(svg, Left - 6, y + 4, Axis.Compact(tick, leftAxis), "end", null);
            }
            if (rightAxis is { } r)
            {
                foreach (var tick in r.Ticks())
                    Text(svg, Width - Right + 6, Y(r, tick) + 4, Axis.Compact(tick, r), "start", "ex-chart-secondary-label");
                var names = string.Join(", ", panel.Series.Where(s => s.Secondary).Select(s => panel.Title.Length > 0 ? s.Leaf : s.Group.Length > 0 ? s.Group : s.Name).Distinct());
                Text(svg, Width - Right, top - 10, $"{names} → right axis", "end", "ex-chart-secondary-label");
            }
            foreach (var tick in dateTicks)
                Line(svg, X(tick.X), top, X(tick.X), bottom, tick.Label is null ? "ex-chart-grid ex-chart-minor" : "ex-chart-grid");
            svg.Append(Invariant, $"""<rect class="ex-chart-frame" x="{Left}" y="{top:0.##}" width="{plotWidth}" height="{panelHeight}"/>""");

            foreach (var series in panel.Series)
            {
                var axis = series.Secondary ? rightAxis!.Value : leftAxis;
                var dash = series.GroupIndex % 4;
                WriteSeries(svg, series, dash, X, v => Y(axis, v));
            }
        }

        // The date labels once, under the last panel.
        var lastBottom = layouts[^1].Top + panelHeight;
        var angle = model.XAxis.Angle;
        foreach (var tick in dateTicks)
        {
            Line(svg, X(tick.X), lastBottom, X(tick.X), lastBottom + 4, "ex-chart-zero");
            if (tick.Label is not { } label) continue;
            if (angle == 0)
                Text(svg, X(tick.X), lastBottom + 16, label, "middle", null);
            else
                // Turned about its own end, which stands under the tick: read upwards, as Excel does.
                svg.Append(Invariant, $"""<text x="{X(tick.X):0.##}" y="{lastBottom + 10:0.##}" text-anchor="end" dominant-baseline="middle" transform="rotate({-angle} {X(tick.X):0.##} {lastBottom + 10:0.##})">{Escape(label)}</text>""");
        }

        // One hover band per date across every panel: the guide, a dot per series and the readout.
        var plotTop = layouts[0].Top;
        for (var i = 0; i < model.Xs.Count; i++)
        {
            var date = model.Xs[i];
            var from = i == 0 ? Left : (X(model.Xs[i - 1]) + X(date)) / 2;
            var to = i == model.Xs.Count - 1 ? Width - Right : (X(date) + X(model.Xs[i + 1])) / 2;
            svg.Append(Invariant, $"""<g class="ex-chart-hit" data-ex-chart-band="{i}">""");
            svg.Append(Invariant, $"""<rect x="{from:0.##}" y="{plotTop:0.##}" width="{Math.Max(1, to - from):0.##}" height="{lastBottom - plotTop:0.##}"/>""");
            svg.Append("""<g class="ex-chart-hover">""");
            Line(svg, X(date), plotTop, X(date), lastBottom, "ex-chart-guide");
            var readout = new StringBuilder($"{model.XHeader}: {model.Scale.Format(date)}");
            foreach (var (panel, top, leftAxis, rightAxis) in layouts)
                foreach (var series in panel.Series)
                {
                    var point = series.At(date);
                    readout.Append('\n').Append(series.Name).Append(": ")
                        .Append(point is null ? "—" : point.Value.Y is { } v ? series.Text(v) : "(no value)");
                    if (point?.Y is not { } value) continue;
                    var axis = series.Secondary ? rightAxis!.Value : leftAxis;
                    var cy = top + panelHeight - panelHeight * axis.Fraction(value);
                    svg.Append(Invariant, $"""<circle class="ex-chart-dot ex-chart-slot-{series.Slot % 8}" cx="{X(date):0.##}" cy="{cy:0.##}" r="3.5"/>""");
                }
            svg.Append("</g>");
            if (forFile) svg.Append("<title>").Append(Escape(readout.ToString())).Append("</title>");
            svg.Append("</g>");
        }

        return svg.Append("</svg>").ToString();
    }

    private static List<(string Name, int Slot, int Group, SeriesStyle? Style)> LegendEntries(ChartModel model) =>
        model.Panels is [{ Title.Length: 0 }]
            ? model.Panels[0].Series.Select(s => (s.Name, s.Slot, s.GroupIndex, (SeriesStyle?)s.Style)).ToList()
            : model.Leaves.Select((leaf, i) => (leaf, i, 0, (SeriesStyle?)null)).ToList();

    // Text is not measured (ADR-0021 keeps measurement out of JS): an estimate per character,
    // generous enough that entries never collide.
    private static double EntryWidth(string name) => 27 + name.Length * 6.4 + 18;

    private static int LegendRows(ChartModel model)
    {
        var (rows, x) = (1, Left);
        foreach (var (name, _, _, _) in LegendEntries(model))
        {
            if (x + EntryWidth(name) > Width - Right && x > Left) (rows, x) = (rows + 1, Left);
            x += EntryWidth(name);
        }
        return rows;
    }

    /// <summary>A legend entry's sample, drawn as the series is drawn, as a small SVG of its own.</summary>
    public static string Sample(ChartSeries series)
    {
        var svg = new StringBuilder();
        svg.Append("""<svg xmlns="http://www.w3.org/2000/svg" class="ex-chart-root ex-chart-sample" viewBox="-4 -7 30 14" width="30" height="14" aria-hidden="true">""");
        svg.Append("<style>").Append(Style).Append("</style>");
        WriteSample(svg, series.Style, series.Slot % 8, series.GroupIndex, 0, 0);
        return svg.Append("</svg>").ToString();
    }

    private static void WriteSample(StringBuilder svg, SeriesStyle? style, int s, int group, double x, double y)
    {
        // The sample is drawn as the series is: a line, points, or a step's open and filled ends.
        if (style is not { Draw: SeriesDraw.Points })
            svg.Append(Invariant, $"""<line class="ex-chart-line ex-chart-slot-{s} ex-chart-group-{group % 4}" x1="{x:0.##}" y1="{y}" x2="{x + 22:0.##}" y2="{y}"/>""");
        if (style is { IsStep: true })
        {
            Marker(svg, style.Marker, s, x + 2, y, open: style.Draw == SeriesDraw.StepPreceding, size: 2.6);
            Marker(svg, style.Marker, s, x + 20, y, open: style.Draw == SeriesDraw.StepFollowing, size: 2.6);
        }
        else if (style is { Draw: SeriesDraw.LinePoints or SeriesDraw.Points })
            Marker(svg, style.Marker, s, x + 11, y, open: false, size: 3);
    }

    private static string Empty(ChartModel model, int panels)
    {
        var why = panels == 0 ? "Every series is filtered out." : "No charted date is in the filter's range, or in the data.";
        var svg = new StringBuilder();
        svg.Append(Invariant, $"""<svg xmlns="http://www.w3.org/2000/svg" class="ex-chart-root" viewBox="0 0 {Width} 120" width="{Width}" height="120" role="img">""");
        svg.Append("<style>").Append(Style).Append("</style>");
        Text(svg, Width / 2, 64, why, "middle", "ex-chart-title");
        return svg.Append("</svg>").ToString();
    }

    private static void WriteLegend(StringBuilder svg, ChartModel model, double height)
    {
        // Panels by group: the colours are the leaves, the same in every panel. One axis: every
        // series, its colour the leaf and its dash the group.
        var (x, y) = (Left, 12.0);
        foreach (var (name, slot, group, style) in LegendEntries(model))
        {
            if (x + EntryWidth(name) > Width - Right && x > Left) (x, y) = (Left, y + 18);
            WriteSample(svg, style, slot % 8, group, x, y);
            Text(svg, x + 27, y + 4, name, "start", null);
            x += EntryWidth(name);
        }
    }

    private static string UnitsOf(ChartPanel panel, bool secondary) =>
        string.Join(", ", panel.Series.Where(s => s.Secondary == secondary).Select(s => s.Unit).OfType<string>().Distinct());

    private static IEnumerable<decimal> Values(ChartSeries series) =>
        series.Points.Where(p => p.Y.HasValue).Select(p => p.Y!.Value);

    private static void WriteSeries(StringBuilder svg, ChartSeries series, int dash, Func<double, double> x, Func<decimal, double> y)
    {
        var slot = series.Slot % 8;
        var style = series.Style;
        var title = $"<title>{Escape(series.Name)}</title>";
        if (style.Draw is SeriesDraw.Line or SeriesDraw.LinePoints)
            svg.Append(Invariant, $"""<path class="ex-chart-line ex-chart-slot-{slot} ex-chart-group-{dash}" d="{PathOf(series.Points, x, y)}">{title}</path>""");
        if (style.Draw is SeriesDraw.LinePoints or SeriesDraw.Points)
            foreach (var p in series.Points)
                if (p.Y is { } v) Marker(svg, style.Marker, slot, x(p.X), y(v), open: false, size: 3.2);
        if (style.IsStep) WriteSteps(svg, series, slot, dash, x, y);
    }

    /// <summary>
    /// A step function as mathematics draws one: a level per interval, a filled marker at the end
    /// that takes the value and an open one at the end that does not. Where the next level is the
    /// same the function is continuous, and no open marker is drawn. No vertical riser: the
    /// function takes no value between the two levels, and a riser would draw one.
    /// </summary>
    private static void WriteSteps(StringBuilder svg, ChartSeries series, int slot, int dash, Func<double, double> x, Func<decimal, double> y)
    {
        var points = series.Points;
        var preceding = series.Style.Draw == SeriesDraw.StepPreceding;
        var d = new StringBuilder();
        var opens = new List<(double X, double Y)>();
        var closed = new List<(double X, double Y)>();
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].Y is not { } v) continue;
            var level = y(v);
            closed.Add((x(points[i].X), level));
            // (tᵢ₋₁, tᵢ] runs back to the data's previous date; [tᵢ, tᵢ₊₁) forward to its next —
            // the bucket is the data's, whether this series is charted at its other end or not.
            if ((preceding ? points[i].Previous : points[i].Next) is not { } end) continue;
            var otherEnd = x(end);
            var clipped = Math.Clamp(otherEnd, Left, Width - Right);
            d.Append(Invariant, $"M{clipped:0.##} {level:0.##}H{x(points[i].X):0.##}");
            // Open where the function takes another value at that end — or none in this series.
            if (clipped == otherEnd && series.At(end)?.Y != v) opens.Add((otherEnd, level));
        }
        svg.Append(Invariant, $"""<path class="ex-chart-line ex-chart-slot-{slot} ex-chart-group-{dash}" d="{d}"><title>{Escape(series.Name)}</title></path>""");
        foreach (var (cx, cy) in opens) Marker(svg, series.Style.Marker, slot, cx, cy, open: true, size: 2.6);
        foreach (var (cx, cy) in closed) Marker(svg, series.Style.Marker, slot, cx, cy, open: false, size: 2.6);
    }

    private static void Marker(StringBuilder svg, MarkerShape shape, int slot, double cx, double cy, bool open, double size)
    {
        var cls = $"ex-chart-marker ex-chart-slot-{slot}{(open ? " ex-chart-open" : "")}";
        var s = size;
        switch (shape)
        {
            case MarkerShape.Circle:
                svg.Append(Invariant, $"""<circle class="{cls}" cx="{cx:0.##}" cy="{cy:0.##}" r="{s:0.##}"/>""");
                break;
            case MarkerShape.Square:
                svg.Append(Invariant, $"""<rect class="{cls}" x="{cx - s:0.##}" y="{cy - s:0.##}" width="{2 * s:0.##}" height="{2 * s:0.##}"/>""");
                break;
            case MarkerShape.Diamond:
                var r = s * 1.3;
                svg.Append(Invariant, $"""<polygon class="{cls}" points="{cx:0.##},{cy - r:0.##} {cx + r:0.##},{cy:0.##} {cx:0.##},{cy + r:0.##} {cx - r:0.##},{cy:0.##}"/>""");
                break;
            case MarkerShape.Triangle:
                var t = s * 1.35;
                svg.Append(Invariant, $"""<polygon class="{cls}" points="{cx:0.##},{cy - t:0.##} {cx + t:0.##},{cy + t * 0.8:0.##} {cx - t:0.##},{cy + t * 0.8:0.##}"/>""");
                break;
        }
    }

    // A missing value ends the run and the next value starts a new one: a gap, not a dive to zero.
    private static string PathOf(IReadOnlyList<ChartPoint> points, Func<double, double> x, Func<decimal, double> y)
    {
        var d = new StringBuilder();
        double? penAt = null;
        foreach (var point in points)
        {
            if (point.Y is not { } v) { penAt = null; continue; }
            // Joined only to the data's previous date: a series charted over two stretches of
            // rows is not drawn across the rows between them, whose values it does not show.
            var joined = penAt is { } at && point.Previous == at;
            d.Append(joined ? 'L' : 'M').Append(x(point.X).ToString("0.##", Invariant)).Append(' ')
                .Append(y(v).ToString("0.##", Invariant)).Append(' ');
            penAt = point.X;
        }
        return d.ToString().TrimEnd();
    }

    private static void Line(StringBuilder svg, double x1, double y1, double x2, double y2, string cls) =>
        svg.Append(Invariant, $"""<line class="{cls}" x1="{x1:0.##}" y1="{y1:0.##}" x2="{x2:0.##}" y2="{y2:0.##}"/>""");

    private static void Text(StringBuilder svg, double x, double y, string text, string anchor, string? cls) =>
        svg.Append(Invariant, $"""<text{(cls is null ? "" : $" class=\"{cls}\"")} x="{x:0.##}" y="{y:0.##}" text-anchor="{anchor}">{Escape(text)}</text>""");

    private static string Escape(string text) => SecurityElement.Escape(text) ?? "";
}
