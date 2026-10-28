using System.Globalization;

namespace ExChart.Engine;

/// <summary>
/// One axis of a 3D surface (ADR-0274): its title and its values in order — strikes, deltas or
/// tenors — each written through <see cref="Format"/>. A grid's lines are evenly spaced, one per value,
/// unless <see cref="Scale"/> places them by value (ADR-0312).
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

    /// <summary>
    /// An Excel number format code in the invariant spelling, such as <c>0.0%</c> or <c>yyyy-mm-dd</c>, read by the
    /// family's <see cref="ExGrid.Data.FormatCode"/> (ADR-0420): it writes the axis' values and its ticks between them.
    /// It stands where a <see cref="Format"/> would, and is what a surface drawn in the browser takes, where no .NET
    /// function can be called. A number code writes an axis of numbers, a date code an axis of dates.
    /// </summary>
    /// <exception cref="ChartRefusedException">The reader does not read the code, the axis has a <see cref="Format"/>
    /// too, or its values are not of the kind the code writes.</exception>
    public string? FormatCode
    {
        get;
        init
        {
            var (code, read, refusal) = ChartCodes.Read(value);
            if (code is not null) ChartCodes.CheckSurfaceAxis(this, code, read, refusal);
            (field, _code) = (code, read);
        }
    }

    private readonly ExGrid.Data.FormatCode? _code;

    /// <summary>
    /// How the axis places its values (ADR-0312): <see cref="AxisScale.Categories"/>, the default, spaces a
    /// grid's lines evenly, one per value, as a market quotes tenors and deltas;
    /// <see cref="AxisScale.Proportional"/> and <see cref="AxisScale.Logarithmic"/> place numbers by their
    /// value or its logarithm, rising along the axis, and a logarithmic axis' values are above zero.
    /// </summary>
    public AxisScale Scale { get; init; } = AxisScale.Categories;

    /// <summary>Writes the value at <paramref name="index"/> through <see cref="Format"/> or <see cref="FormatCode"/>.</summary>
    public string Write(int index) => Write(Values[index]);

    /// <summary>Writes <paramref name="value"/> through <see cref="Format"/> or <see cref="FormatCode"/>.</summary>
    public string Write(object value) =>
        Format is { } f ? f(value) : _code is { } code ? code.Format(value, CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    /// <summary>
    /// Writes a position that need not be one of <see cref="Values"/>, such as a tick: through a number
    /// <see cref="FormatCode"/>, through <see cref="Format"/> when it is exactly a value of the values' own type,
    /// invariantly otherwise.
    /// </summary>
    public string WriteTick(double position)
    {
        if (_code is { IsDate: false } code)
            return code.Format(position, CultureInfo.InvariantCulture);
        if (Format is not null && Values.Count > 0 && IsNumber(Values[0]))
        {
            try
            {
                var typed = Convert.ChangeType(position, Values[0].GetType(), CultureInfo.InvariantCulture);
                if (Convert.ToDouble(typed, CultureInfo.InvariantCulture) == position) return Write(typed);
            }
            catch (Exception e) when (e is OverflowException or InvalidCastException)
            {
                // Not a value of the axis' type: written invariantly below.
            }
        }
        return SurfaceMesh.Plain(position);
    }

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

    // An axis of numbers a factory made, written by the Consumer's Format, by a code, or plainly, as its ticks are.
    internal static SurfaceAxis Made(string title, IEnumerable<double> values, Func<double, string>? format, string? formatCode, AxisScale scale)
    {
        var axis = Of(title, values, format ?? (formatCode is null ? SurfaceMesh.Plain : null)) with { Scale = scale };
        return formatCode is null ? axis : axis with { FormatCode = formatCode };
    }

    internal static bool IsNumber(object? value) =>
        value is double or float or decimal or int or long or short or byte or sbyte or uint or ulong or ushort;

    internal static bool IsDate(object? value) => value is DateOnly or DateTime or DateTimeOffset;

    internal double Number(int index) => Convert.ToDouble(Values[index], CultureInfo.InvariantCulture);
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
/// <param name="Pitch">The tilt from level, in degrees, -90 (seen from below) to 90 (seen from above).</param>
/// <param name="Zoom">The zoom, 1 fitting the surface to its box.</param>
/// <param name="PanX">The centre's horizontal offset, as a fraction of the width.</param>
/// <param name="PanY">The centre's vertical offset, as a fraction of the height.</param>
public sealed record SurfaceView(double Yaw = -35, double Pitch = 28, double Zoom = 1, double PanX = 0, double PanY = 0)
{
    /// <summary>The view a surface is first drawn at.</summary>
    public static SurfaceView Default { get; } = new();
}

/// <summary>A node of a Consumer's triangle mesh (ADR-0311): its place in the plane and its value there, or null where it has none.</summary>
/// <param name="X">Its x position.</param>
/// <param name="Y">Its y position.</param>
/// <param name="Z">Its value, or null: a node without a value is a hole, and no triangle at it is drawn.</param>
public readonly record struct SurfaceNode(double X, double Y, ChartValue? Z);

/// <summary>A triangle of a Consumer's mesh (ADR-0311): the indices of its three nodes.</summary>
/// <param name="A">The first corner's index.</param>
/// <param name="B">The second corner's index.</param>
/// <param name="C">The third corner's index.</param>
public readonly record struct SurfaceTriangle(int A, int B, int C);

/// <summary>
/// A 3D surface in the data's terms (ADR-0274): x values × y values × a table of z, one row per y
/// value and one column per x value. A missing value is a hole, drawn as one: never 0 and never filled
/// from its neighbours. Scattered points are refused by name (<see cref="FromPoints{TX, TY}"/>).
/// <para>
/// Interpolation is the Consumer's (ADR-0257): a surface may be handed in as a function z(x, y), which
/// ExChart samples (<see cref="Sampled(string, double, double, double, double, Func{double, double, ChartValue?}, string, string, string, int, Func{double, string}?, Func{double, string}?, Func{object, string}?, AxisScale, AxisScale, string?, string?)"/>),
/// or as a grid whose interpolated values are marked; either way it names its method, which the
/// tooltip and the Notes say. A Consumer's own triangle mesh is drawn as its triangles
/// (<see cref="FromTriangles"/>, ADR-0311).
/// </para>
/// </summary>
public sealed record SurfaceDefinition
{
    /// <summary>The method a slice through a Consumer's triangles reads between its nodes with (ADR-0311).</summary>
    public const string TrianglesMethod = "linear on the Consumer's triangles (P1)";

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

    private SurfaceDefinition(SurfaceAxis x, SurfaceAxis y, IReadOnlyList<SurfaceNode> nodes, IReadOnlyList<SurfaceTriangle> triangles, string zTitle,
        Func<object, string>? zFormat, IReadOnlyList<string>? notes)
    {
        X = x;
        Y = y;
        Z = [];
        ZTitle = zTitle;
        ZFormat = zFormat;
        Notes = notes ?? [];
        Nodes = nodes;
        Triangles = triangles;
        ValidateMesh();
    }

    /// <summary>The x axis: the table's columns, or a mesh's x positions in order.</summary>
    public SurfaceAxis X { get; }

    /// <summary>The y axis: the table's rows, or a mesh's y positions in order.</summary>
    public SurfaceAxis Y { get; }

    /// <summary>A grid's values, one row per y value, null where the grid has a hole; empty for a mesh.</summary>
    public IReadOnlyList<IReadOnlyList<ChartValue?>> Z { get; }

    /// <summary>What the values are, such as "Volatility".</summary>
    public string ZTitle { get; }

    /// <summary>Writes a value as the cell it came from writes it; null writes it invariantly.</summary>
    public Func<object, string>? ZFormat { get; }

    /// <summary>
    /// An Excel number format code in the invariant spelling, such as <c>0.00%</c>, read by the family's
    /// <see cref="ExGrid.Data.FormatCode"/> (ADR-0420): it writes the values the surface holds and the ones a slice
    /// reads between them. It stands where <see cref="ZFormat"/> would, and is what a surface drawn in the browser
    /// takes, where no .NET function can be called.
    /// </summary>
    /// <exception cref="ChartRefusedException">The reader does not read the code, it writes dates, or the surface has a
    /// <see cref="ZFormat"/> too.</exception>
    public string? ZFormatCode
    {
        get;
        init
        {
            var (code, read, refusal) = ChartCodes.Read(value);
            if (code is not null) ChartCodes.CheckZ(this, code, read, refusal);
            (field, _zCode) = (code, read);
        }
    }

    private readonly ExGrid.Data.FormatCode? _zCode;

    /// <summary>The method the interpolated values were made with, or null when the surface claims none.</summary>
    public string? Method { get; }

    /// <summary>Which values are interpolated, in the table's shape; null with a <see cref="Method"/> marks every value.</summary>
    public IReadOnlyList<IReadOnlyList<bool>>? Interpolated { get; }

    /// <summary>The Consumer's Notes (ADR-0257).</summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>A mesh's nodes, or null for a grid (ADR-0311).</summary>
    public IReadOnlyList<SurfaceNode>? Nodes { get; }

    /// <summary>A mesh's triangles, or null for a grid (ADR-0311).</summary>
    public IReadOnlyList<SurfaceTriangle>? Triangles { get; }

    /// <summary>Whether the surface is a Consumer's triangle mesh rather than a grid (ADR-0311).</summary>
    public bool IsMesh => Triangles is not null;

    /// <summary>
    /// The vertical scale (ADR-0312): proportional, or logarithmic. A value at or below zero, or a
    /// <see cref="Centre"/> at or below zero, keeps it proportional, with a Note naming it, as a 2D
    /// chart's vertical axis does (ADR-0290).
    /// </summary>
    /// <exception cref="ChartRefusedException">Set to <see cref="AxisScale.Categories"/>: values are never spaced as categories.</exception>
    public AxisScale ZScale
    {
        get;
        init => field = value == AxisScale.Categories
            ? throw new ChartRefusedException("A surface's values are placed proportionally or logarithmically, never as categories.")
            : value;
    } = AxisScale.Proportional;

    /// <summary>
    /// The value the colours diverge from, such as 0 for a P&amp;L or 1 for a ratio (ADR-0310): values
    /// below it take one hue and values above it another, deepening alike with the distance from it, and
    /// the band holding it is neutral. Null colours the values in one run from the lowest to the highest.
    /// </summary>
    /// <exception cref="ChartRefusedException">The centre is not a finite number.</exception>
    public double? Centre
    {
        get;
        init => field = value is { } c && !double.IsFinite(c)
            ? throw new ChartRefusedException("The value the colours diverge from is a finite number.")
            : value;
    }

    /// <summary>The vertical scale as drawn: <see cref="ZScale"/>, unless a value or the <see cref="Centre"/> cannot lie on it.</summary>
    public AxisScale EffectiveZScale => ZScaleNote is null ? ZScale : AxisScale.Proportional;

    // Why a logarithmic vertical scale stays proportional, or null when it does not.
    private string? ZScaleNote
    {
        get
        {
            if (ZScale != AxisScale.Logarithmic) return null;
            var low = Present().MinBy(v => v.ToDouble());
            if (!(low.ToDouble() > 0))
                return $"The vertical scale stays proportional: {Name(ZTitle, "the values")} reach {WriteZ(low)}, which a logarithmic scale cannot place.";
            if (Centre is { } c && !(c > 0))
                return $"The vertical scale stays proportional: the colours diverge from {SurfaceMesh.Plain(c)}, which a logarithmic scale cannot place.";
            return null;
        }
    }

    /// <summary>
    /// The lines shown under the surface: the Consumer's Notes, the method that made its interpolated
    /// values (ADR-0257), and why a logarithmic scale stays proportional (ADR-0312).
    /// </summary>
    public IReadOnlyList<string> AllNotes
    {
        get
        {
            var lines = new List<string>(Notes);
            if (Method is not null) lines.Add($"Interpolated by the Consumer: {Method}.");
            if (ZScaleNote is { } note) lines.Add(note);
            return lines;
        }
    }

    /// <summary>Whether a grid's value at x index <paramref name="x"/> and y index <paramref name="y"/> is interpolated.</summary>
    /// <exception cref="InvalidOperationException">The surface is a mesh, which has no grid indices.</exception>
    public bool IsInterpolated(int x, int y) => GridOnly() && Method is not null && (Interpolated is null || Interpolated[y][x]);

    /// <summary>A grid's value at x index <paramref name="x"/> and y index <paramref name="y"/>, or null at a hole.</summary>
    /// <exception cref="InvalidOperationException">The surface is a mesh, which has no grid indices.</exception>
    public ChartValue? At(int x, int y) => GridOnly() ? Z[y][x] : null;

    // A grid's indices on a mesh would read the wrong value or none; refused by name (spine rule 1).
    private bool GridOnly() => !IsMesh ? true
        : throw new InvalidOperationException("A mesh has no grid to index: read its points through Node, counted as NodeCount counts them (ADR-0311).");

    /// <summary>How many points the surface holds: a grid's points, row by row, or a mesh's nodes.</summary>
    public int NodeCount => Nodes?.Count ?? X.Values.Count * Y.Values.Count;

    /// <summary>
    /// The point at <paramref name="index"/>, counted as <see cref="NodeCount"/> counts: its x and y as the
    /// axes hold them, and its value, or null at a hole.
    /// </summary>
    public (object X, object Y, ChartValue? Z) Node(int index)
    {
        if (Nodes is { } nodes) return (nodes[index].X, nodes[index].Y, nodes[index].Z);
        var (i, j) = (index % X.Values.Count, index / X.Values.Count);
        return (X.Values[i], Y.Values[j], Z[j][i]);
    }

    /// <summary>Whether the point at <paramref name="index"/> is interpolated; a mesh's nodes are its values.</summary>
    public bool IsInterpolatedNode(int index) => Nodes is null && IsInterpolated(index % X.Values.Count, index / X.Values.Count);

    /// <summary>Writes a value through <see cref="ZFormat"/> or <see cref="ZFormatCode"/>.</summary>
    public string WriteZ(ChartValue value) =>
        ZFormat is { } f ? f(value.Value) : _zCode is { } code ? code.Format(value.Value, CultureInfo.InvariantCulture) : value.ToInvariantString();

    internal IEnumerable<ChartValue> Present() =>
        Nodes is { } nodes ? nodes.Select(n => n.Z).OfType<ChartValue>() : Z.SelectMany(r => r).OfType<ChartValue>();

    /// <summary>
    /// A surface from points, each at an x value and a y value. The points must lie on a grid: every x
    /// value they name must hold a value at two y values at least, and every y value at two x values,
    /// or the point stands alone and could join a surface only by interpolation, which ExChart does not
    /// compute (ADR-0257, ADR-0274). Where the grid has no point, it has a hole.
    /// </summary>
    /// <exception cref="ChartRefusedException">The points are scattered, or two share a place.</exception>
    public static SurfaceDefinition FromPoints<TX, TY>(IEnumerable<(TX X, TY Y, ChartValue Z)> points, string xTitle, string yTitle, string zTitle = "",
        Func<TX, string>? xFormat = null, Func<TY, string>? yFormat = null, Func<object, string>? zFormat = null,
        string? xFormatCode = null, string? yFormatCode = null)
        where TX : notnull where TY : notnull
    {
        var list = points.ToList();
        var xs = list.Select(p => p.X).Distinct().Order().ToList();
        var ys = list.Select(p => p.Y).Distinct().Order().ToList();
        var xAxis = Coded(SurfaceAxis.Of(xTitle, xs, xFormat), xFormatCode);
        var yAxis = Coded(SurfaceAxis.Of(yTitle, ys, yFormat), yFormatCode);
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
        "scattered points form no grid, and a surface through them is an interpolation, which ExChart does not choose. Interpolate them, with a method of ExChart.Engine.Interpolation or your own, and hand the surface in as a function that names its method, or hand in your own triangles (ADR-0257, ADR-0274, ADR-0311).";

    /// <summary>
    /// A surface drawn on the Consumer's own triangle mesh (ADR-0311) — a finite element solution, or a
    /// triangulation of quotes the Consumer chose — as its triangles: each is drawn flat through its
    /// three corners, never resampled onto a grid. The nodes are the values: a dot marks each, and the
    /// tooltip reads the nearest. A slice cuts the triangles exactly, reading between nodes linearly on
    /// them (<see cref="TrianglesMethod"/>), which it says. The axes place the nodes by their position,
    /// proportionally unless <paramref name="xScale"/> or <paramref name="yScale"/> is logarithmic.
    /// </summary>
    /// <exception cref="ChartRefusedException">The mesh cannot be drawn as asked: a triangle names a node it does not hold, has no area, or two nodes share a place.</exception>
    public static SurfaceDefinition FromTriangles(IReadOnlyList<SurfaceNode> nodes, IReadOnlyList<SurfaceTriangle> triangles, string xTitle, string yTitle, string zTitle = "",
        Func<double, string>? xFormat = null, Func<double, string>? yFormat = null, Func<object, string>? zFormat = null,
        AxisScale xScale = AxisScale.Proportional, AxisScale yScale = AxisScale.Proportional, IReadOnlyList<string>? notes = null,
        string? xFormatCode = null, string? yFormatCode = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(triangles);
        if (xScale == AxisScale.Categories || yScale == AxisScale.Categories)
            throw new ChartRefusedException("A mesh's nodes are placed by their position: its axes are proportional or logarithmic, never categories (ADR-0311).");
        foreach (var n in nodes)
            if (!double.IsFinite(n.X) || !double.IsFinite(n.Y))
                throw new ChartRefusedException($"A node lies at ({n.X}, {n.Y}), which is no place in the plane.");
        var xAxis = Coded(SurfaceAxis.Of(xTitle, nodes.Select(n => n.X).Distinct().Order(), xFormat) with { Scale = xScale }, xFormatCode);
        var yAxis = Coded(SurfaceAxis.Of(yTitle, nodes.Select(n => n.Y).Distinct().Order(), yFormat) with { Scale = yScale }, yFormatCode);
        return new SurfaceDefinition(xAxis, yAxis, nodes.ToArray(), triangles.ToArray(), zTitle, zFormat, notes);
    }

    /// <summary>
    /// A surface handed in as a function z(x, y): the Consumer's interpolation, which names its method
    /// (ADR-0257). ExChart samples it on a grid of <paramref name="samples"/> values an axis across the
    /// rectangle — evenly in the axis' own scale, so geometrically on a logarithmic one (ADR-0312) — and
    /// draws a hole where it returns null, outside the points it was made from, as
    /// <see cref="Interpolation.Interpolate"/>'s methods do. Every value is marked interpolated.
    /// </summary>
    /// <exception cref="ChartRefusedException">The function names no method, or has no value on the rectangle.</exception>
    public static SurfaceDefinition Sampled(string method, double xFrom, double xTo, double yFrom, double yTo, Func<double, double, ChartValue?> z,
        string xTitle, string yTitle, string zTitle = "", int samples = 25, Func<double, string>? xFormat = null, Func<double, string>? yFormat = null, Func<object, string>? zFormat = null,
        AxisScale xScale = AxisScale.Categories, AxisScale yScale = AxisScale.Categories, string? xFormatCode = null, string? yFormatCode = null)
    {
        if (string.IsNullOrWhiteSpace(method))
            throw new ChartRefusedException("A surface handed in as a function names its method, so the chart can say how its values were made (ADR-0257).");
        if (!(xFrom < xTo) || !(yFrom < yTo))
            throw new ChartRefusedException("A surface handed in as a function is sampled over a rectangle that is not empty.");
        if (samples < 2)
            throw new ArgumentOutOfRangeException(nameof(samples), samples, "A surface is sampled at two values an axis at least.");
        if ((xScale == AxisScale.Logarithmic && !(xFrom > 0)) || (yScale == AxisScale.Logarithmic && !(yFrom > 0)))
            throw new ChartRefusedException("A surface sampled on a logarithmic axis is sampled from above zero, which is where a logarithmic axis begins.");
        double At(double from, double to, AxisScale scale, int k) => k == samples - 1 ? to
            : scale == AxisScale.Logarithmic ? from * Math.Pow(to / from, (double)k / (samples - 1))
            : from + (to - from) * k / (samples - 1);
        var xs = Enumerable.Range(0, samples).Select(k => At(xFrom, xTo, xScale, k)).ToArray();
        var ys = Enumerable.Range(0, samples).Select(k => At(yFrom, yTo, yScale, k)).ToArray();
        var table = ys.Select(v => (IReadOnlyList<ChartValue?>)xs.Select(u => z(u, v)).ToArray()).ToArray();
        return new SurfaceDefinition(SurfaceAxis.Made(xTitle, xs, xFormat, xFormatCode, xScale), SurfaceAxis.Made(yTitle, ys, yFormat, yFormatCode, yScale),
            table, zTitle, zFormat, method);
    }

    /// <summary>A surface interpolated by one of <see cref="Interpolation.Interpolate"/>'s methods, sampled across its rectangle under its method's name.</summary>
    public static SurfaceDefinition Sampled(Interpolation.Interpolant2D surface, string xTitle, string yTitle, string zTitle = "", int samples = 25,
        Func<double, string>? xFormat = null, Func<double, string>? yFormat = null, Func<object, string>? zFormat = null,
        AxisScale xScale = AxisScale.Categories, AxisScale yScale = AxisScale.Categories, string? xFormatCode = null, string? yFormatCode = null) =>
        Sampled(surface.Method, surface.XFrom, surface.XTo, surface.YFrom, surface.YTo, surface.ValueAt, xTitle, yTitle, zTitle, samples, xFormat, yFormat, zFormat,
            xScale, yScale, xFormatCode, yFormatCode);

    /// <summary>
    /// The slice as a 2D chart (ADR-0274): the values along the other axis, where they are read exactly —
    /// as categories on an evenly spaced axis, as numbers on the axis' own scale otherwise (ADR-0312). A
    /// hole is a gap. A mesh's slice cuts its triangles, at any position between its first node and its
    /// last, and reads between nodes linearly on them, which the slice says (ADR-0311). Null when the
    /// slice's value is not on this surface.
    /// </summary>
    public ChartDefinition? SliceChart(SurfaceSlice slice)
    {
        var fixedAxis = slice.Axis == SurfaceSliceAxis.X ? X : Y;
        var along = slice.Axis == SurfaceSliceAxis.X ? Y : X;
        if (IsMesh)
        {
            if (Cut(slice) is not { Count: > 0 } runs) return null;
            var cut = new List<ChartPoint>();
            foreach (var run in runs)
            {
                // A gap between runs is a point without a value, where no triangle is.
                if (cut.Count > 0) cut.Add(new ChartPoint((cut[^1].X + run[0].Along) / 2, null));
                cut.AddRange(run.Select(p => new ChartPoint(p.Along, p.Z)));
            }
            var meshSeries = ChartSeries.Of("slice", $"{Name(ZTitle, "Value")} at {fixedAxis.Write(Convert.ToDouble(slice.Value, CultureInfo.InvariantCulture))}", SeriesKind.Level, cut)
                with { FromZero = false, Format = ZFormat, FormatCode = ZFormatCode, Interpolated = true, Segments = [new Segment(cut[0].X, cut[^1].X, TrianglesMethod)] };
            return new ChartDefinition(NumberAxis(along), [meshSeries]);
        }

        var at = fixedAxis.IndexOf(slice.Value);
        if (at < 0) return null;
        var placed = along.Scale != AxisScale.Categories;
        var points = Enumerable.Range(0, along.Values.Count)
            .Select(k => new ChartPoint(placed ? along.Number(k) : k, slice.Axis == SurfaceSliceAxis.X ? At(at, k) : At(k, at)));
        var name = $"{Name(ZTitle, "Value")} at {fixedAxis.Write(at)}";
        var series = ChartSeries.Of("slice", name, SeriesKind.Level, points) with { FromZero = false, Format = ZFormat, FormatCode = ZFormatCode };
        var interpolated = Enumerable.Range(0, along.Values.Count).Any(k => slice.Axis == SurfaceSliceAxis.X ? IsInterpolated(at, k) : IsInterpolated(k, at));
        if (interpolated)
            series = series with
            {
                Interpolated = true,
                Segments = [new Segment(placed ? along.Number(0) : 0, placed ? along.Number(along.Values.Count - 1) : along.Values.Count - 1, Method!)],
            };
        var axis = placed ? NumberAxis(along) : ChartAxis.Categories(Enumerable.Range(0, along.Values.Count).Select(along.Write).ToArray(), along.Title);
        return new ChartDefinition(axis, [series]);
    }

    private static ChartAxis NumberAxis(SurfaceAxis along) =>
        ChartAxis.Numbers(along.Title) with { Scale = along.Scale, Format = along.WriteTick };

    /// <summary>
    /// A mesh cut at the slice's value (ADR-0311): the runs of positions along the other axis and the
    /// values there, linear on each triangle, broken where no triangle is. Null when the value is not a
    /// number within the mesh's positions.
    /// </summary>
    internal IReadOnlyList<IReadOnlyList<(double Along, ChartValue Z)>>? Cut(SurfaceSlice slice)
    {
        if (Nodes is not { } nodes || Triangles is not { } triangles || !SurfaceAxis.IsNumber(slice.Value)) return null;
        var c = Convert.ToDouble(slice.Value, CultureInfo.InvariantCulture);
        var onX = slice.Axis == SurfaceSliceAxis.X;
        double Fixed(SurfaceNode n) => onX ? n.X : n.Y;
        double Along(SurfaceNode n) => onX ? n.Y : n.X;
        var fixedAxis = onX ? X : Y;
        if (c < fixedAxis.Number(0) || c > fixedAxis.Number(fixedAxis.Values.Count - 1)) return null;

        var alongAxis = onX ? Y : X;
        var span = Math.Max(alongAxis.Number(alongAxis.Values.Count - 1) - alongAxis.Number(0), double.Epsilon);
        var segments = new List<((double U, ChartValue Z) From, (double U, ChartValue Z) To)>();
        foreach (var t in triangles)
        {
            SurfaceNode[] corners = [nodes[t.A], nodes[t.B], nodes[t.C]];
            if (corners.Any(n => n.Z is null)) continue;
            var hits = new List<(double U, ChartValue Z)>();
            for (var e = 0; e < 3; e++)
            {
                var p = corners[e];
                var q = corners[(e + 1) % 3];
                var (dp, dq) = (Fixed(p) - c, Fixed(q) - c);
                if (dp == 0) hits.Add((Along(p), p.Z!.Value));
                if (dp * dq < 0)
                {
                    var w = dp / (dp - dq);
                    hits.Add((Along(p) + w * (Along(q) - Along(p)), Mix(p.Z!.Value, q.Z!.Value, w)));
                }
            }
            if (hits.Count < 2) continue;
            var lo = hits.MinBy(h => h.U);
            var hi = hits.MaxBy(h => h.U);
            if (hi.U > lo.U) segments.Add((lo, hi));
        }

        // Neighbouring triangles meet on an edge, so their segments meet end to start; one that starts
        // past the run's end begins another run, the gap between them a hole.
        var runs = new List<List<(double Along, ChartValue Z)>>();
        foreach (var (from, to) in segments.OrderBy(s => s.From.U).ThenBy(s => s.To.U))
        {
            var run = runs.Count > 0 ? runs[^1] : null;
            var tolerance = 1e-9 * span;
            if (run is not null && from.U <= run[^1].Along + tolerance)
            {
                if (to.U > run[^1].Along + tolerance) run.Add(to);
                continue;
            }
            runs.Add([from, to]);
        }
        return runs;
    }

    // A value a fraction w of the way from a to b, kept a decimal where both are.
    private static ChartValue Mix(ChartValue a, ChartValue b, double w) =>
        a.IsDecimal && b.IsDecimal
            ? ChartValue.Of((decimal)a.Value + (decimal)w * ((decimal)b.Value - (decimal)a.Value))
            : ChartValue.Of(a.ToDouble() + w * (b.ToDouble() - a.ToDouble()));

    // An axis given a code, where the factory was handed one.
    private static SurfaceAxis Coded(SurfaceAxis axis, string? formatCode) => formatCode is null ? axis : axis with { FormatCode = formatCode };

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
        Placed(X, "x");
        Placed(Y, "y");
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

    private void ValidateMesh()
    {
        var nodes = Nodes!;
        var triangles = Triangles!;
        if (triangles.Count == 0)
            throw new ChartRefusedException("A mesh needs a triangle (ADR-0311).");
        var places = new Dictionary<(double, double), int>();
        for (var k = 0; k < nodes.Count; k++)
            if (!places.TryAdd((nodes[k].X, nodes[k].Y), k))
                throw new ChartRefusedException($"Nodes {places[(nodes[k].X, nodes[k].Y)]} and {k} both lie at {Name(X.Title, "x")} {X.WriteTick(nodes[k].X)}, {Name(Y.Title, "y")} {Y.WriteTick(nodes[k].Y)}; a place in the plane holds one value.");
        var span = Math.Max(nodes.Max(n => n.X) - nodes.Min(n => n.X), nodes.Max(n => n.Y) - nodes.Min(n => n.Y));
        for (var t = 0; t < triangles.Count; t++)
        {
            var (a, b, c) = (triangles[t].A, triangles[t].B, triangles[t].C);
            if (a < 0 || b < 0 || c < 0 || a >= nodes.Count || b >= nodes.Count || c >= nodes.Count)
                throw new ChartRefusedException($"Triangle {t} names a node the mesh does not hold; it has {nodes.Count} nodes.");
            var (p, q, r) = (nodes[a], nodes[b], nodes[c]);
            var area = (q.X - p.X) * (r.Y - p.Y) - (r.X - p.X) * (q.Y - p.Y);
            if (!(Math.Abs(area) > 1e-12 * span * span))
                throw new ChartRefusedException($"Triangle {t} has no area: its corners {a}, {b} and {c} lie on one line, so it holds no plane to draw.");
        }
        Placed(X, "x");
        Placed(Y, "y");
        if (nodes.All(n => n.Z is null))
            throw new ChartRefusedException("The surface holds no value.");
    }

    // An axis that places its values by value holds numbers, rising, above zero when logarithmic (ADR-0312).
    private static void Placed(SurfaceAxis axis, string fallback)
    {
        if (axis.Scale == AxisScale.Categories) return;
        for (var i = 0; i < axis.Values.Count; i++)
        {
            if (!SurfaceAxis.IsNumber(axis.Values[i]))
                throw new ChartRefusedException($"{Name(axis.Title, fallback)} {axis.Write(i)} is not a number, which a {Scaled(axis)} axis cannot place; leave the axis evenly spaced (ADR-0312).");
            var v = axis.Number(i);
            if (axis.Scale == AxisScale.Logarithmic && !(v > 0))
                throw new ChartRefusedException($"{Name(axis.Title, fallback)} {axis.Write(i)} is at or below zero, which a logarithmic axis cannot place: every position must be above zero.");
            if (i > 0 && !(v > axis.Number(i - 1)))
                throw new ChartRefusedException($"{Name(axis.Title, fallback)} {axis.Write(i)} comes after {axis.Write(i - 1)}; a {Scaled(axis)} axis' values rise along it.");
        }
    }

    private static string Scaled(SurfaceAxis axis) => axis.Scale == AxisScale.Logarithmic ? "logarithmic" : "proportional";

    private static void Distinct(SurfaceAxis axis, string fallback)
    {
        for (var i = 0; i < axis.Values.Count; i++)
            for (var k = 0; k < i; k++)
                if (Equals(axis.Values[i], axis.Values[k]))
                    throw new ChartRefusedException($"{Name(axis.Title, fallback)} {axis.Write(i)} appears twice; each grid line needs a value of its own.");
    }
}
