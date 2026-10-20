using System.Globalization;

namespace ExChart.Engine;

/// <summary>A labelled position on one of a surface's scales: its value there and its label, or none where labels would crowd.</summary>
/// <param name="Value">The position: a fraction of the floor for x and y, a value on the vertical scale for z and the colour bar.</param>
/// <param name="Label">The label, or null for a line drawn unlabelled.</param>
public sealed record SurfaceTick(double Value, string? Label);

/// <summary>A point of a surface as the 3D entry places it: x and y as fractions of the floor, z on the vertical scale, or null at a hole.</summary>
/// <param name="X">Its place across the floor, 0 to 1.</param>
/// <param name="Y">Its place along the floor, 0 to 1.</param>
/// <param name="Z">Its value on the vertical scale (its logarithm on a logarithmic one), or null at a hole.</param>
/// <param name="Interpolated">Whether its value is interpolated, so the 3D entry draws it no dot (ADR-0257).</param>
public sealed record SurfaceMeshNode(double X, double Y, double? Z, bool Interpolated);

/// <summary>A face the 3D entry draws: its corners, as indices of the nodes, and its colour band.</summary>
/// <param name="Corners">Four corners for a grid's cell, three for a mesh's triangle.</param>
/// <param name="Band">Its colour band, 0 to <see cref="SurfaceMesh.BandCount"/> - 1.</param>
public sealed record SurfaceFace(IReadOnlyList<int> Corners, int Band);

/// <summary>
/// The colour bar beside a surface (ADR-0310): the run of the vertical scale its bands cover, in
/// <see cref="SurfaceMesh.BandCount"/> equal steps, its ticks, and whether it diverges from a centre.
/// </summary>
/// <param name="From">The value at the foot of the lowest band, on the vertical scale.</param>
/// <param name="To">The value at the head of the highest band, on the vertical scale.</param>
/// <param name="Ticks">The labelled values along it, on the vertical scale.</param>
/// <param name="Diverging">Whether the bands diverge from a centre, which the middle band holds.</param>
public sealed record SurfaceColourBar(double From, double To, IReadOnlyList<SurfaceTick> Ticks, bool Diverging);

/// <summary>A point of a slice's line on the surface, placed as <see cref="SurfaceMeshNode"/> is.</summary>
/// <param name="X">Its place across the floor, 0 to 1.</param>
/// <param name="Y">Its place along the floor, 0 to 1.</param>
/// <param name="Z">Its value on the vertical scale.</param>
public sealed record SurfaceMeshPoint(double X, double Y, double Z);

/// <summary>
/// What the 3D entry is handed to draw a surface (ADR-0255): the points placed on the floor and the
/// vertical scale, the faces with their colour bands, the floor's lines and the vertical scale's ticks,
/// and the colour bar. .NET decides all of it — where each value lies on each scale (ADR-0312) and how
/// each face is coloured (ADR-0310) — and the module only projects, orders and draws it.
/// </summary>
/// <param name="XTitle">The x axis' title.</param>
/// <param name="YTitle">The y axis' title.</param>
/// <param name="ZTitle">The values' title.</param>
/// <param name="Nodes">The points, counted as <see cref="SurfaceDefinition.NodeCount"/> counts them.</param>
/// <param name="Faces">The faces to draw; a face with a hole at a corner is no face, and is not here.</param>
/// <param name="Rows">A grid's lines along x, one per y value, as node indices; empty for a mesh.</param>
/// <param name="Columns">A grid's lines along y, one per x value, as node indices; empty for a mesh.</param>
/// <param name="XLines">The floor's lines across x, at fractions of the floor, labelled where room allows.</param>
/// <param name="YLines">The floor's lines along y, at fractions of the floor, labelled where room allows.</param>
/// <param name="ZMin">The bottom of the vertical scale.</param>
/// <param name="ZMax">The top of the vertical scale.</param>
/// <param name="ZTicks">The vertical scale's ticks.</param>
/// <param name="Bar">The colour bar.</param>
/// <param name="Holes">How many points are holes.</param>
public sealed record SurfaceMesh(string XTitle, string YTitle, string ZTitle, IReadOnlyList<SurfaceMeshNode> Nodes, IReadOnlyList<SurfaceFace> Faces,
    IReadOnlyList<IReadOnlyList<int>> Rows, IReadOnlyList<IReadOnlyList<int>> Columns, IReadOnlyList<SurfaceTick> XLines, IReadOnlyList<SurfaceTick> YLines,
    double ZMin, double ZMax, IReadOnlyList<SurfaceTick> ZTicks, SurfaceColourBar Bar, int Holes)
{
    /// <summary>
    /// How many colour bands a face is coloured from, low to high; each is a Visual Token,
    /// <c>--ex-chart-heat-0</c> on, or <c>--ex-chart-diverge-0</c> on when the colours diverge (ADR-0310).
    /// </summary>
    public const int BandCount = 9;

    /// <summary>The mesh of <paramref name="definition"/>.</summary>
    public static SurfaceMesh Of(SurfaceDefinition definition)
    {
        var frame = new Frame(definition);
        var nodes = new SurfaceMeshNode[definition.NodeCount];
        for (var k = 0; k < nodes.Length; k++)
        {
            var (x, y, z) = definition.Node(k);
            nodes[k] = new SurfaceMeshNode(frame.AtX(k, x), frame.AtY(k, y), z is { } v ? frame.Scale(v.ToDouble()) : null, definition.IsInterpolatedNode(k));
        }
        var present = nodes.Select(n => n.Z).OfType<double>().ToArray();
        var lo = present.Min();
        var hi = present.Max();

        double[] zTicks;
        double zMin, zMax;
        if (frame.Logarithmic)
        {
            var (dMin, dMax) = TickLayout.Decades(Math.Pow(10, lo), Math.Pow(10, hi));
            (zMin, zMax) = (Math.Log10(dMin), Math.Log10(dMax));
            zTicks = TickLayout.LogSteps(dMin, dMax).Select(Math.Log10).ToArray();
        }
        else
        {
            var (min, max, step) = TickLayout.Nice(lo, hi);
            (zMin, zMax) = (min, max);
            zTicks = TickLayout.Steps(min, max, step).ToArray();
        }

        // A face is coloured by the mean of its corners on the vertical scale (ADR-0310): through the
        // values' own range, or by its distance from the centre, alike on both sides of it.
        var centre = definition.Centre is { } c ? frame.Scale(c) : (double?)null;
        var reach = centre is { } m ? Math.Max(Math.Abs(hi - m), Math.Abs(lo - m)) : 0;
        if (centre is not null && reach == 0) reach = frame.Logarithmic || centre == 0 ? 1 : Math.Abs(centre.Value) / 10;
        int Band(double value)
        {
            if (centre is { } mid)
            {
                var t = (value - mid) / reach;
                var depth = Math.Min(BandCount / 2, (int)Math.Floor(Math.Abs(t) * BandCount / 2 + 0.5));
                return BandCount / 2 + Math.Sign(t) * depth;
            }
            var u = hi > lo ? (value - lo) / (hi - lo) : 0.5;
            return Math.Clamp((int)Math.Floor(u * BandCount), 0, BandCount - 1);
        }

        var faces = new List<SurfaceFace>();
        foreach (var corners in definition.IsMesh
            ? definition.Triangles!.Select(t => new[] { t.A, t.B, t.C })
            : Cells(definition.X.Values.Count, definition.Y.Values.Count))
        {
            if (corners.Any(k => nodes[k].Z is null)) continue;
            faces.Add(new SurfaceFace(corners, Band(corners.Average(k => nodes[k].Z!.Value))));
        }

        var (barFrom, barTo) = centre is { } centred ? (centred - reach, centred + reach)
            : hi > lo ? (lo, hi) : (lo - (frame.Logarithmic || lo == 0 ? 1 : Math.Abs(lo) / 10), hi + (frame.Logarithmic || hi == 0 ? 1 : Math.Abs(hi) / 10));
        var bar = new SurfaceColourBar(barFrom, barTo, frame.BarTicks(barFrom, barTo, centre), centre is not null);

        var nx = definition.IsMesh ? 0 : definition.X.Values.Count;
        var ny = definition.IsMesh ? 0 : definition.Y.Values.Count;
        var rows = Enumerable.Range(0, ny).Select(j => (IReadOnlyList<int>)Enumerable.Range(0, nx).Select(i => j * nx + i).ToArray()).ToArray();
        var columns = Enumerable.Range(0, nx).Select(i => (IReadOnlyList<int>)Enumerable.Range(0, ny).Select(j => j * nx + i).ToArray()).ToArray();

        return new SurfaceMesh(definition.X.Title, definition.Y.Title, definition.ZTitle, nodes, faces, rows, columns,
            frame.FloorLines(definition.X), frame.FloorLines(definition.Y), zMin, zMax,
            zTicks.Select(v => new SurfaceTick(v, frame.Label(v))).ToArray(), bar, nodes.Count(n => n.Z is null));
    }

    private static IEnumerable<int[]> Cells(int nx, int ny)
    {
        for (var j = 0; j < ny - 1; j++)
            for (var i = 0; i < nx - 1; i++)
                yield return [j * nx + i, j * nx + i + 1, (j + 1) * nx + i + 1, (j + 1) * nx + i];
    }

    /// <summary>
    /// The slice's line on the surface, placed as the mesh's points are, in runs broken at a hole:
    /// along a grid's line, or across a mesh's triangles where the slice cuts them (ADR-0311). Empty when
    /// the slice is not on this surface.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<SurfaceMeshPoint>> SlicePath(SurfaceDefinition definition, SurfaceSlice slice)
    {
        var frame = new Frame(definition);
        var onX = slice.Axis == SurfaceSliceAxis.X;
        var runs = new List<IReadOnlyList<SurfaceMeshPoint>>();
        if (definition.IsMesh)
        {
            if (definition.Cut(slice) is not { } cut) return runs;
            var at = Convert.ToDouble(slice.Value, CultureInfo.InvariantCulture);
            var fixedAt = onX ? frame.PlaceX(at) : frame.PlaceY(at);
            foreach (var run in cut)
                runs.Add(run.Select(p => onX
                    ? new SurfaceMeshPoint(fixedAt, frame.PlaceY(p.Along), frame.Scale(p.Z.ToDouble()))
                    : new SurfaceMeshPoint(frame.PlaceX(p.Along), fixedAt, frame.Scale(p.Z.ToDouble()))).ToArray());
            return runs;
        }

        var fixedAxis = onX ? definition.X : definition.Y;
        var index = fixedAxis.IndexOf(slice.Value);
        if (index < 0) return runs;
        var nx = definition.X.Values.Count;
        var count = onX ? definition.Y.Values.Count : nx;
        var current = new List<SurfaceMeshPoint>();
        for (var k = 0; k < count; k++)
        {
            var node = onX ? k * nx + index : index * nx + k;
            var (x, y, z) = definition.Node(node);
            if (z is not { } v)
            {
                if (current.Count > 0) runs.Add(current.ToArray());
                current.Clear();
                continue;
            }
            current.Add(new SurfaceMeshPoint(frame.AtX(node, x), frame.AtY(node, y), frame.Scale(v.ToDouble())));
        }
        if (current.Count > 0) runs.Add(current.ToArray());
        return runs;
    }

    /// <summary>
    /// The surface said in words, for a screen reader (ADR-0275's shape): what it is of, its grid or its
    /// mesh, the range of its values, its holes, and how it is coloured and scaled.
    /// </summary>
    public static string Summary(SurfaceDefinition definition, string? title, string? description)
    {
        var present = definition.Present().ToArray();
        var lo = present.MinBy(v => v.ToDouble());
        var hi = present.MaxBy(v => v.ToDouble());
        var holes = definition.NodeCount - present.Length;
        var x = definition.X;
        var y = definition.Y;
        var z = string.IsNullOrWhiteSpace(definition.ZTitle) ? "Values" : definition.ZTitle;
        var head = string.IsNullOrWhiteSpace(title) ? "3D surface" : $"{title}, a 3D surface";
        var shape = definition.IsMesh
            ? $"a mesh of {definition.Nodes!.Count} nodes and {definition.Triangles!.Count} triangles"
            : $"{x.Values.Count} by {y.Values.Count} points";
        var hole = definition.IsMesh ? (holes == 1 ? "node" : "nodes") + " without a value" : (holes == 1 ? "hole" : "holes") + " where the data has no value";
        var text = $"{head} of {z} over {Label(x, "x")} from {x.Write(0)} to {x.Write(x.Values.Count - 1)} and {Label(y, "y")} from {y.Write(0)} to {y.Write(y.Values.Count - 1)}"
            + $"; {shape}, from {definition.WriteZ(lo)} to {definition.WriteZ(hi)}"
            + (holes == 0 ? "." : $", with {holes} {hole}.")
            + (definition.Centre is { } c ? $" Coloured by distance from {Plain(c)}, below and above." : "")
            + (definition.EffectiveZScale == AxisScale.Logarithmic ? " The vertical scale is logarithmic." : "")
            + (definition.Method is { } m ? $" Interpolated by the Consumer: {m}." : "");
        return string.IsNullOrWhiteSpace(description) ? text : $"{text} {description}";
    }

    private static string Label(SurfaceAxis axis, string fallback) => string.IsNullOrWhiteSpace(axis.Title) ? fallback : axis.Title;

    /// <summary>A number written invariantly, as a 2D axis writes its ticks.</summary>
    internal static string Plain(double value) =>
        value != 0 && Math.Abs(value) < 1e-4 ? value.ToString("G3", CultureInfo.InvariantCulture) : value.ToString("0.####", CultureInfo.InvariantCulture);

    // Where each value lies on each of the surface's scales (ADR-0312).
    private sealed class Frame
    {
        private readonly SurfaceDefinition _definition;

        public Frame(SurfaceDefinition definition)
        {
            _definition = definition;
            Logarithmic = definition.EffectiveZScale == AxisScale.Logarithmic;
        }

        public bool Logarithmic { get; }

        public double Scale(double value) => Logarithmic ? Math.Log10(value) : value;

        public double AtX(int node, object value) => _definition.X.Scale == AxisScale.Categories && !_definition.IsMesh
            ? Even(node % _definition.X.Values.Count, _definition.X.Values.Count)
            : PlaceX(Convert.ToDouble(value, CultureInfo.InvariantCulture));

        public double AtY(int node, object value) => _definition.Y.Scale == AxisScale.Categories && !_definition.IsMesh
            ? Even(node / _definition.X.Values.Count, _definition.Y.Values.Count)
            : PlaceY(Convert.ToDouble(value, CultureInfo.InvariantCulture));

        public double PlaceX(double value) => Place(_definition.X, value);

        public double PlaceY(double value) => Place(_definition.Y, value);

        private static double Even(int index, int count) => (double)index / (count - 1);

        private static double Place(SurfaceAxis axis, double value)
        {
            var (min, max) = (axis.Number(0), axis.Number(axis.Values.Count - 1));
            if (!(max > min)) return 0.5;
            return axis.Scale == AxisScale.Logarithmic
                ? Math.Log(value / min) / Math.Log(max / min)
                : (value - min) / (max - min);
        }

        // A grid has a line at each of its values: evenly spaced, labelled where twelve fit; placed by
        // value, labelled where a label does not crowd the last one. A mesh has no lines of its own, so
        // its floor's are at round numbers, as a 2D number axis' are (ADR-0312).
        public IReadOnlyList<SurfaceTick> FloorLines(SurfaceAxis axis)
        {
            var n = axis.Values.Count;
            if (!_definition.IsMesh && axis.Scale == AxisScale.Categories)
            {
                var step = (int)Math.Ceiling(n / 12.0);
                return Enumerable.Range(0, n).Select(i => new SurfaceTick(Even(i, n), i % step == 0 ? axis.Write(i) : null)).ToArray();
            }
            if (!_definition.IsMesh)
            {
                var lines = new SurfaceTick[n];
                var last = double.NegativeInfinity;
                for (var i = 0; i < n; i++)
                {
                    var at = Place(axis, axis.Number(i));
                    var room = at - last >= LabelRoom;
                    lines[i] = new SurfaceTick(at, room ? axis.Write(i) : null);
                    if (room) last = at;
                }
                return lines;
            }
            var (min, max) = (axis.Number(0), axis.Number(n - 1));
            if (!(max > min)) return [new SurfaceTick(0.5, axis.WriteTick(min))];
            IEnumerable<double> ticks = axis.Scale == AxisScale.Logarithmic ? TickLayout.LogSteps(min, max) : NiceWithin(min, max);
            return ticks.Select(t => new SurfaceTick(Place(axis, t), axis.WriteTick(t))).ToArray();
        }

        // The least distance, as a fraction of the floor, between two labels on an axis placed by value.
        private const double LabelRoom = 1.0 / 12;

        public IReadOnlyList<SurfaceTick> BarTicks(double from, double to, double? centre)
        {
            List<double> ticks = Logarithmic
                ? [.. TickLayout.LogSteps(Math.Pow(10, from), Math.Pow(10, to)).Select(Math.Log10)]
                : [.. NiceWithin(from, to)];
            if (centre is { } c && !ticks.Any(t => Math.Abs(t - c) <= 1e-9 * Math.Max(1, Math.Abs(c))))
                ticks.Add(c);
            return ticks.Order().Select(t => new SurfaceTick(t, Label(t))).ToArray();
        }

        public string Label(double scaled) => Plain(Logarithmic ? Math.Round(Math.Pow(10, scaled), 12) : scaled);

        // Round-number ticks between min and max, never past them.
        private static IEnumerable<double> NiceWithin(double min, double max)
        {
            var (from, to, step) = TickLayout.Nice(min, max);
            var slack = 1e-9 * Math.Max(Math.Abs(min), Math.Abs(max));
            return TickLayout.Steps(from, to, step).Where(t => t >= min - slack && t <= max + slack);
        }
    }
}
