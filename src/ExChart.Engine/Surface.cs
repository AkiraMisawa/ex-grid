using System.Globalization;

namespace ExChart.Engine;

/// <summary>
/// One axis of a 3D surface's grid (ADR-0274): its title and its values in order — strikes, deltas or
/// tenors — each written through <see cref="Format"/>. The grid's lines are evenly spaced, one per value.
/// </summary>
public sealed record SurfaceAxis
{
    /// <summary>An axis of the given values, in the order the grid holds them.</summary>
    public SurfaceAxis(string title, IReadOnlyList<object> values, Func<object, string>? format = null)
    {
        Title = title;
        Values = values;
        Format = format;
    }

    /// <summary>The axis' title.</summary>
    public string Title { get; }

    /// <summary>The values, one per grid line, in order.</summary>
    public IReadOnlyList<object> Values { get; }

    /// <summary>Writes a value as the column it came from writes it; null writes it invariantly.</summary>
    public Func<object, string>? Format { get; }

    /// <summary>Writes the value at <paramref name="index"/> through <see cref="Format"/>.</summary>
    public string Write(int index) => Write(Values[index]);

    /// <summary>Writes <paramref name="value"/> through <see cref="Format"/>.</summary>
    public string Write(object value) =>
        Format is { } f ? f(value) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    /// <summary>The index of <paramref name="value"/> among <see cref="Values"/>, or -1.</summary>
    public int IndexOf(object value)
    {
        for (var i = 0; i < Values.Count; i++)
            if (Equals(Values[i], value)) return i;
        return -1;
    }

    /// <summary>An axis of typed values, written through a typed format.</summary>
    public static SurfaceAxis Of<T>(string title, IEnumerable<T> values, Func<T, string>? format = null) where T : notnull =>
        new(title, values.Select(v => (object)v).ToArray(), format is null ? null : o => format((T)o));
}

/// <summary>Which of a surface's two axes a slice holds fixed.</summary>
public enum SurfaceSliceAxis
{
    /// <summary>An x value held fixed: the slice runs along y, such as a term structure at one strike.</summary>
    X,

    /// <summary>A y value held fixed: the slice runs along x, such as a smile at one tenor.</summary>
    Y,
}

/// <summary>
/// A slice of a surface, named in the data's terms (ADR-0272, ADR-0274): the axis held fixed and its
/// value there, so new data with the same tenor keeps the slice.
/// </summary>
/// <param name="Axis">The axis held fixed.</param>
/// <param name="Value">Its value, as the axis holds it.</param>
public sealed record SurfaceSlice(SurfaceSliceAxis Axis, object Value)
{
    /// <summary>The slice at x value <paramref name="value"/>, running along y.</summary>
    public static SurfaceSlice AtX(object value) => new(SurfaceSliceAxis.X, value);

    /// <summary>The slice at y value <paramref name="value"/>, running along x.</summary>
    public static SurfaceSlice AtY(object value) => new(SurfaceSliceAxis.Y, value);
}

/// <summary>
/// How a surface is seen, in its own terms (ADR-0272): the angles in degrees, the zoom, and the
/// centre's offset as fractions of the drawing's width and height. Never pixels.
/// </summary>
/// <param name="Yaw">The turn about the vertical, in degrees.</param>
/// <param name="Pitch">The tilt from level, in degrees, 0 to 90.</param>
/// <param name="Zoom">The zoom, 1 fitting the surface to its box.</param>
/// <param name="PanX">The centre's horizontal offset, as a fraction of the width.</param>
/// <param name="PanY">The centre's vertical offset, as a fraction of the height.</param>
public sealed record SurfaceView(double Yaw = -35, double Pitch = 28, double Zoom = 1, double PanX = 0, double PanY = 0)
{
    /// <summary>The view a surface is first drawn at.</summary>
    public static SurfaceView Default { get; } = new();
}

/// <summary>
/// A 3D surface in the data's terms (ADR-0274): x values × y values × a table of z, one row per y
/// value and one column per x value. A missing value is a hole, drawn as one: never 0 and never filled
/// from its neighbours. Scattered points are refused by name (<see cref="FromPoints{TX, TY}"/>).
/// <para>
/// Interpolation is the Consumer's (ADR-0257): a surface may be handed in as a function z(x, y), which
/// ExChart samples (<see cref="Sampled(string, double, double, double, double, Func{double, double, ChartValue?}, string, string, string, int, Func{double, string}?, Func{double, string}?, Func{object, string}?)"/>),
/// or as a grid whose interpolated values are marked; either way it names its method, which the
/// tooltip and the Notes say. The grid is the shape ExChart draws; other inputs become one.
/// </para>
/// </summary>
public sealed record SurfaceDefinition
{
    /// <summary>A surface of the given grid; <paramref name="z"/> holds one row per y value, each with one value per x value.</summary>
    /// <exception cref="ChartRefusedException">The grid cannot be drawn as asked.</exception>
    /// <param name="x">The x axis: the table's columns.</param>
    /// <param name="y">The y axis: the table's rows.</param>
    /// <param name="z">The values, one row per y value, null at a hole.</param>
    /// <param name="zTitle">What the values are.</param>
    /// <param name="zFormat">Writes a value as its cell writes it.</param>
    /// <param name="method">The method the interpolated values were made with, as the tooltip and the Notes say it.</param>
    /// <param name="interpolated">Which values are interpolated, in the table's shape; null with a method marks every value.</param>
    /// <param name="notes">The Consumer's Notes, shown under the surface (ADR-0257).</param>
    public SurfaceDefinition(SurfaceAxis x, SurfaceAxis y, IReadOnlyList<IReadOnlyList<ChartValue?>> z, string zTitle = "", Func<object, string>? zFormat = null,
        string? method = null, IReadOnlyList<IReadOnlyList<bool>>? interpolated = null, IReadOnlyList<string>? notes = null)
    {
        X = x;
        Y = y;
        Z = z;
        ZTitle = zTitle;
        ZFormat = zFormat;
        Method = string.IsNullOrWhiteSpace(method) ? null : method;
        Interpolated = interpolated;
        Notes = notes ?? [];
        Validate();
    }

    /// <summary>The x axis: the table's columns.</summary>
    public SurfaceAxis X { get; }

    /// <summary>The y axis: the table's rows.</summary>
    public SurfaceAxis Y { get; }

    /// <summary>The values, one row per y value, null where the grid has a hole.</summary>
    public IReadOnlyList<IReadOnlyList<ChartValue?>> Z { get; }

    /// <summary>What the values are, such as "Volatility".</summary>
    public string ZTitle { get; }

    /// <summary>Writes a value as the cell it came from writes it; null writes it invariantly.</summary>
    public Func<object, string>? ZFormat { get; }

    /// <summary>The method the interpolated values were made with, or null when the surface claims none.</summary>
    public string? Method { get; }

    /// <summary>Which values are interpolated, in the table's shape; null with a <see cref="Method"/> marks every value.</summary>
    public IReadOnlyList<IReadOnlyList<bool>>? Interpolated { get; }

    /// <summary>The Consumer's Notes (ADR-0257).</summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>
    /// The lines shown under the surface: the Consumer's Notes, then the method that made its
    /// interpolated values (ADR-0257).
    /// </summary>
    public IReadOnlyList<string> AllNotes => Method is null ? Notes : [.. Notes, $"Interpolated by the Consumer: {Method}."];

    /// <summary>Whether the value at x index <paramref name="x"/> and y index <paramref name="y"/> is interpolated.</summary>
    public bool IsInterpolated(int x, int y) => Method is not null && (Interpolated is null || Interpolated[y][x]);

    /// <summary>The value at x index <paramref name="x"/> and y index <paramref name="y"/>, or null at a hole.</summary>
    public ChartValue? At(int x, int y) => Z[y][x];

    /// <summary>Writes a value through <see cref="ZFormat"/>.</summary>
    public string WriteZ(ChartValue value) => ZFormat is { } f ? f(value.Value) : value.ToInvariantString();

    /// <summary>
    /// A surface from points, each at an x value and a y value. The points must lie on a grid: every x
    /// value they name must hold a value at two y values at least, and every y value at two x values,
    /// or the point stands alone and could join a surface only by interpolation, which ExChart does not
    /// compute (ADR-0257, ADR-0274). Where the grid has no point, it has a hole.
    /// </summary>
    /// <exception cref="ChartRefusedException">The points are scattered, or two share a place.</exception>
    public static SurfaceDefinition FromPoints<TX, TY>(IEnumerable<(TX X, TY Y, ChartValue Z)> points, string xTitle, string yTitle, string zTitle = "",
        Func<TX, string>? xFormat = null, Func<TY, string>? yFormat = null, Func<object, string>? zFormat = null)
        where TX : notnull where TY : notnull
    {
        var list = points.ToList();
        var xs = list.Select(p => p.X).Distinct().Order().ToList();
        var ys = list.Select(p => p.Y).Distinct().Order().ToList();
        var xAxis = SurfaceAxis.Of(xTitle, xs, xFormat);
        var yAxis = SurfaceAxis.Of(yTitle, ys, yFormat);
        foreach (var x in xs)
            if (list.Count(p => Equals(p.X, x)) < 2)
                throw new ChartRefusedException($"{Name(xTitle, "x")} {xAxis.Write(x)} has a value at only one {Name(yTitle, "y")}: {ScatteredReason}");
        foreach (var y in ys)
            if (list.Count(p => Equals(p.Y, y)) < 2)
                throw new ChartRefusedException($"{Name(yTitle, "y")} {yAxis.Write(y)} has a value at only one {Name(xTitle, "x")}: {ScatteredReason}");

        var xIndex = xs.Select((v, i) => (v, i)).ToDictionary(t => t.v, t => t.i);
        var yIndex = ys.Select((v, i) => (v, i)).ToDictionary(t => t.v, t => t.i);
        var z = ys.Select(_ => new ChartValue?[xs.Count]).ToArray();
        foreach (var p in list)
        {
            ref var cell = ref z[yIndex[p.Y]][xIndex[p.X]];
            if (cell is not null)
                throw new ChartRefusedException($"Two points lie at {Name(xTitle, "x")} {xAxis.Write(p.X)} and {Name(yTitle, "y")} {yAxis.Write(p.Y)}; a grid point holds one value.");
            cell = p.Z;
        }
        return new SurfaceDefinition(xAxis, yAxis, z, zTitle, zFormat);
    }

    private const string ScatteredReason =
        "scattered points form no grid, and a surface through them is an interpolation, which ExChart does not choose. Interpolate them, with a method of ExChart.Engine.Interpolation or your own, and hand the surface in as a function that names its method (ADR-0257, ADR-0274).";

    /// <summary>
    /// A surface handed in as a function z(x, y): the Consumer's interpolation, which names its method
    /// (ADR-0257). ExChart samples it on a grid of <paramref name="samples"/> values an axis across the
    /// rectangle, and draws a hole where it returns null — outside the points it was made from, as
    /// <see cref="Interpolation.Interpolate"/>'s methods do. Every value is marked interpolated.
    /// </summary>
    /// <exception cref="ChartRefusedException">The function names no method, or has no value on the rectangle.</exception>
    public static SurfaceDefinition Sampled(string method, double xFrom, double xTo, double yFrom, double yTo, Func<double, double, ChartValue?> z,
        string xTitle, string yTitle, string zTitle = "", int samples = 25, Func<double, string>? xFormat = null, Func<double, string>? yFormat = null, Func<object, string>? zFormat = null)
    {
        if (string.IsNullOrWhiteSpace(method))
            throw new ChartRefusedException("A surface handed in as a function names its method, so the chart can say how its values were made (ADR-0257).");
        if (!(xFrom < xTo) || !(yFrom < yTo))
            throw new ChartRefusedException("A surface handed in as a function is sampled over a rectangle that is not empty.");
        if (samples < 2)
            throw new ArgumentOutOfRangeException(nameof(samples), samples, "A surface is sampled at two values an axis at least.");
        double At(double from, double to, int k) => k == samples - 1 ? to : from + (to - from) * k / (samples - 1);
        var xs = Enumerable.Range(0, samples).Select(k => At(xFrom, xTo, k)).ToArray();
        var ys = Enumerable.Range(0, samples).Select(k => At(yFrom, yTo, k)).ToArray();
        var table = ys.Select(v => (IReadOnlyList<ChartValue?>)xs.Select(u => z(u, v)).ToArray()).ToArray();
        return new SurfaceDefinition(SurfaceAxis.Of(xTitle, xs, xFormat ?? Plain), SurfaceAxis.Of(yTitle, ys, yFormat ?? Plain), table, zTitle, zFormat, method);
    }

    /// <summary>A surface interpolated by one of <see cref="Interpolation.Interpolate"/>'s methods, sampled across its rectangle under its method's name.</summary>
    public static SurfaceDefinition Sampled(Interpolation.Interpolant2D surface, string xTitle, string yTitle, string zTitle = "", int samples = 25,
        Func<double, string>? xFormat = null, Func<double, string>? yFormat = null, Func<object, string>? zFormat = null) =>
        Sampled(surface.Method, surface.XFrom, surface.XTo, surface.YFrom, surface.YTo, surface.ValueAt, xTitle, yTitle, zTitle, samples, xFormat, yFormat, zFormat);

    private static string Plain(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>
    /// The slice as a 2D chart (ADR-0274): the values along the other axis, as categories, where they
    /// are read exactly. A hole is a gap. Null when the slice's value is not on this surface.
    /// </summary>
    public ChartDefinition? SliceChart(SurfaceSlice slice)
    {
        var fixedAxis = slice.Axis == SurfaceSliceAxis.X ? X : Y;
        var along = slice.Axis == SurfaceSliceAxis.X ? Y : X;
        var at = fixedAxis.IndexOf(slice.Value);
        if (at < 0) return null;
        var labels = Enumerable.Range(0, along.Values.Count).Select(along.Write).ToArray();
        var points = Enumerable.Range(0, along.Values.Count)
            .Select(k => new ChartPoint(k, slice.Axis == SurfaceSliceAxis.X ? At(at, k) : At(k, at)));
        var name = $"{Name(ZTitle, "Value")} at {fixedAxis.Write(at)}";
        var series = ChartSeries.Of("slice", name, SeriesKind.Level, points) with { FromZero = false, Format = ZFormat };
        var interpolated = Enumerable.Range(0, along.Values.Count).Any(k => slice.Axis == SurfaceSliceAxis.X ? IsInterpolated(at, k) : IsInterpolated(k, at));
        if (interpolated)
            series = series with { Interpolated = true, Segments = [new Segment(0, along.Values.Count - 1, Method!)] };
        return new ChartDefinition(ChartAxis.Categories(labels, along.Title), [series]);
    }

    private static string Name(string title, string fallback) => string.IsNullOrWhiteSpace(title) ? fallback : title;

    private void Validate()
    {
        if (X.Values.Count < 2 || Y.Values.Count < 2)
            throw new ChartRefusedException($"A surface needs two {Name(X.Title, "x")} values and two {Name(Y.Title, "y")} values at least; this one has {X.Values.Count} and {Y.Values.Count}.");
        if (Z.Count != Y.Values.Count)
            throw new ChartRefusedException($"The surface's table has {Z.Count} rows for {Y.Values.Count} {Name(Y.Title, "y")} values; it needs one row per value.");
        for (var j = 0; j < Z.Count; j++)
            if (Z[j].Count != X.Values.Count)
                throw new ChartRefusedException($"The surface's row at {Name(Y.Title, "y")} {Y.Write(j)} has {Z[j].Count} values for {X.Values.Count} {Name(X.Title, "x")} values; a hole is handed in as null, never left out.");
        Distinct(X, "x");
        Distinct(Y, "y");
        if (Z.All(row => row.All(v => v is null)))
            throw new ChartRefusedException("The surface holds no value.");
        if (Interpolated is not null)
        {
            if (Method is null)
                throw new ChartRefusedException("The surface marks values interpolated but names no method; the chart says how its values were made (ADR-0257).");
            if (Interpolated.Count != Y.Values.Count || Interpolated.Any(r => r.Count != X.Values.Count))
                throw new ChartRefusedException("The surface's marks of interpolated values need the table's shape: one row per y value, one mark per x value.");
        }
    }

    private static void Distinct(SurfaceAxis axis, string fallback)
    {
        for (var i = 0; i < axis.Values.Count; i++)
            for (var k = 0; k < i; k++)
                if (Equals(axis.Values[i], axis.Values[k]))
                    throw new ChartRefusedException($"{Name(axis.Title, fallback)} {axis.Write(i)} appears twice; each grid line needs a value of its own.");
    }
}

/// <summary>A tick on a surface's vertical axis: its value and its label.</summary>
/// <param name="Value">The value.</param>
/// <param name="Label">The label, written invariantly as a 2D axis writes its ticks.</param>
public sealed record SurfaceTick(double Value, string Label);

/// <summary>
/// What the 3D entry is handed to draw a surface (ADR-0255): the labels, the values with their holes,
/// the vertical scale and its ticks, and each face's colour band. .NET decides all of it; the module
/// only projects, orders and draws it.
/// </summary>
/// <param name="XTitle">The x axis' title.</param>
/// <param name="YTitle">The y axis' title.</param>
/// <param name="ZTitle">The values' title.</param>
/// <param name="X">The x values, written.</param>
/// <param name="Y">The y values, written.</param>
/// <param name="Values">The values as positions, one row per y value, null at a hole.</param>
/// <param name="Bands">Each face's colour band, one row per pair of neighbouring y values and one entry per pair of x values, null where a corner is a hole.</param>
/// <param name="ZMin">The bottom of the vertical scale.</param>
/// <param name="ZMax">The top of the vertical scale.</param>
/// <param name="ZTicks">The vertical scale's ticks.</param>
/// <param name="Holes">How many grid points are holes.</param>
/// <param name="Interpolated">Which grid points are interpolated, which the 3D entry draws no dot at; null when none is.</param>
public sealed record SurfaceMesh(string XTitle, string YTitle, string ZTitle, IReadOnlyList<string> X, IReadOnlyList<string> Y,
    IReadOnlyList<IReadOnlyList<double?>> Values, IReadOnlyList<IReadOnlyList<int?>> Bands, double ZMin, double ZMax, IReadOnlyList<SurfaceTick> ZTicks, int Holes,
    IReadOnlyList<IReadOnlyList<bool>>? Interpolated)
{
    /// <summary>How many colour bands a face is coloured from, low to high; each is a Visual Token, <c>--ex-chart-heat-0</c> on.</summary>
    public const int BandCount = 9;

    /// <summary>The mesh of <paramref name="definition"/>.</summary>
    public static SurfaceMesh Of(SurfaceDefinition definition)
    {
        var values = definition.Z.Select(row => (IReadOnlyList<double?>)row.Select(v => v?.ToDouble()).ToArray()).ToArray();
        var present = values.SelectMany(r => r).OfType<double>().ToArray();
        var lo = present.Min();
        var hi = present.Max();
        var (min, max, step) = TickLayout.Nice(lo, hi);
        var ticks = TickLayout.Steps(min, max, step).Select(v => new SurfaceTick(v, v.ToString("0.####", CultureInfo.InvariantCulture))).ToArray();

        // A face is coloured by the mean of its four corners, through the values' own range, so the
        // bands span what the surface holds. A face with a hole at a corner is no face: not drawn.
        var bands = new IReadOnlyList<int?>[values.Length - 1];
        for (var j = 0; j < bands.Length; j++)
        {
            var row = new int?[values[j].Count - 1];
            for (var i = 0; i < row.Length; i++)
            {
                double?[] corners = [values[j][i], values[j][i + 1], values[j + 1][i + 1], values[j + 1][i]];
                if (corners.Any(c => c is null)) continue;
                var mean = corners.Sum(c => c!.Value) / 4;
                var t = hi > lo ? (mean - lo) / (hi - lo) : 0.5;
                row[i] = Math.Clamp((int)Math.Floor(t * BandCount), 0, BandCount - 1);
            }
            bands[j] = row;
        }

        var xs = Enumerable.Range(0, definition.X.Values.Count).Select(definition.X.Write).ToArray();
        var ys = Enumerable.Range(0, definition.Y.Values.Count).Select(definition.Y.Write).ToArray();
        var holes = values.Sum(r => r.Count(v => v is null));
        var interpolated = definition.Method is null ? null
            : definition.Z.Select((row, j) => (IReadOnlyList<bool>)row.Select((_, i) => definition.IsInterpolated(i, j)).ToArray()).ToArray();
        return new SurfaceMesh(definition.X.Title, definition.Y.Title, definition.ZTitle, xs, ys, values, bands, min, max, ticks, holes, interpolated);
    }

    /// <summary>
    /// The surface said in words, for a screen reader (ADR-0275's shape): what it is of, its grid, the
    /// range of its values and its holes.
    /// </summary>
    public static string Summary(SurfaceDefinition definition, string? title, string? description)
    {
        var present = definition.Z.SelectMany(r => r).OfType<ChartValue>().ToArray();
        var lo = present.MinBy(v => v.ToDouble());
        var hi = present.MaxBy(v => v.ToDouble());
        var holes = definition.Z.Sum(r => r.Count(v => v is null));
        var x = definition.X;
        var y = definition.Y;
        var z = string.IsNullOrWhiteSpace(definition.ZTitle) ? "Values" : definition.ZTitle;
        var head = string.IsNullOrWhiteSpace(title) ? "3D surface" : $"{title}, a 3D surface";
        var text = $"{head} of {z} over {Label(x, "x")} from {x.Write(0)} to {x.Write(x.Values.Count - 1)} and {Label(y, "y")} from {y.Write(0)} to {y.Write(y.Values.Count - 1)}"
            + $"; {x.Values.Count} by {y.Values.Count} points, from {definition.WriteZ(lo)} to {definition.WriteZ(hi)}"
            + (holes == 0 ? "." : $", with {holes} {(holes == 1 ? "hole" : "holes")} where the data has no value.")
            + (definition.Method is { } m ? $" Interpolated by the Consumer: {m}." : "");
        return string.IsNullOrWhiteSpace(description) ? text : $"{text} {description}";
    }

    private static string Label(SurfaceAxis axis, string fallback) => string.IsNullOrWhiteSpace(axis.Title) ? fallback : axis.Title;
}
