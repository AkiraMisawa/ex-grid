using System.Text.Json;

namespace ExChart.Engine;

/// <summary>
/// What a chart asks a <see cref="IChartSource"/> (ADR-0322, ADR-0344): one column of values, read
/// across another, from <see cref="From"/> to <see cref="To"/>, at <see cref="Pixels"/> columns.
/// </summary>
/// <param name="Data">The Data Handle the series reads.</param>
/// <param name="X">The column across.</param>
/// <param name="Y">The column of values.</param>
/// <param name="Values">What the axis places: dates, as their day numbers, or numbers.</param>
/// <param name="From">The first position asked for, in the axis' own values; null for the data's first.</param>
/// <param name="To">The last, or null for the data's last.</param>
/// <param name="Pixels">How many columns the range is cut into, each keeping its first, last, lowest and highest point.</param>
public sealed record ChartSourceQuery(string Data, string X, string Y, AxisValues Values, double? From, double? To, int Pixels);

/// <summary>
/// A <see cref="IChartSource"/>'s answer (ADR-0344): the rows kept, in order across, as a table with
/// the query's <see cref="ChartSourceQuery.X"/> and <see cref="ChartSourceQuery.Y"/> columns, what each
/// thinned pixel held, and the version of the data it was read from.
/// </summary>
/// <param name="Version">The version of the data the answer was read from; an answer for an older one is never drawn over a newer one.</param>
/// <param name="Points">The rows kept, in order across.</param>
/// <param name="Thinned">What each pixel that held more than one row held, in the data's positions; empty when nothing was thinned.</param>
public sealed record ChartSourceAnswer(long Version, IChartData Points, IReadOnlyList<PixelSummary> Thinned);

/// <summary>
/// Where a chart reads a series' points (ADR-0322): thinned where the data lives. A host whose data is
/// on a server thins it there and sends only the points kept; <see cref="ChartSource.Over"/> thins data
/// held in the process.
/// </summary>
public interface IChartSource
{
    /// <summary>Answers <paramref name="query"/> with the points the chart draws.</summary>
    ValueTask<ChartSourceAnswer> ThinAsync(ChartSourceQuery query, CancellationToken cancellationToken = default);
}

/// <summary>A Chart Spec read through Chart Sources (ADR-0344): its reading, and the version each Data Handle was read at.</summary>
/// <param name="Reading">The title and the definition.</param>
/// <param name="Versions">The version each Data Handle's answers named.</param>
public sealed record ChartSourceReading(ChartSpecReading Reading, IReadOnlyDictionary<string, long> Versions);

/// <summary>
/// The reference implementation of a Chart Source (ADR-0322, ADR-0344), and the specification a server's
/// source answers as: the range cut into equal columns, each keeping its first, last, lowest and highest
/// row (ADR-0254), never a row picked by LTTB; a row with no value kept, so a gap stays a gap.
/// </summary>
public static class ChartSource
{
    /// <summary>A Chart Source over a table held in the process, thinning it as it is asked.</summary>
    /// <param name="data">The table.</param>
    /// <param name="version">The version its answers name.</param>
    public static IChartSource Over(IChartData data, long version = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new InMemory(data, version);
    }

    /// <summary>Thins <paramref name="data"/> as <paramref name="query"/> asks.</summary>
    /// <exception cref="ArgumentException">The table has no such column, or the query asks for no pixels or a labels axis.</exception>
    /// <exception cref="ChartSpecRefusedException">A row's position is not the axis' kind, or a value is no number.</exception>
    public static ChartSourceAnswer Thin(IChartData data, ChartSourceQuery query, long version = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(query);
        if (query.Pixels < 1)
            throw new ArgumentException("A Chart Source is asked for one pixel or more.", nameof(query));
        if (query.Values == AxisValues.Labels)
            throw new ArgumentException("A labels axis needs every category, which a Chart Source does not thin.", nameof(query));
        foreach (var column in new[] { query.X, query.Y })
        {
            if (!data.Columns.Contains(column))
                throw new ArgumentException($"\"{query.Data}\" has no column \"{column}\"; its columns are {string.Join(", ", data.Columns)}.", nameof(query));
        }
        // The rows in the range, in order across; a row's place in the data breaks a tie, as a line joins them.
        var rows = new List<(double X, double? Y, object? Value, int Row)>();
        for (var r = 0; r < data.RowCount; r++)
        {
            var x = PositionOf(data.Value(query.X, r), query.Values, query.Data, r);
            if ((query.From is { } from && x < from) || (query.To is { } to && x > to))
                continue;
            var value = data.Value(query.Y, r);
            rows.Add((x, NumberOf(value, query.Data, r), value, r));
        }
        rows.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Row.CompareTo(b.Row));
        var xs = new List<object?>();
        var ys = new List<object?>();
        var thinned = new List<PixelSummary>();
        void Keep(int i)
        {
            xs.Add(data.Value(query.X, rows[i].Row));
            ys.Add(rows[i].Value);
        }
        if (rows.Count > 0)
        {
            var low = query.From ?? rows[0].X;
            var high = query.To ?? rows[^1].X;
            var width = high - low;
            int Column(double x) => width <= 0 ? 0 : Math.Clamp((int)Math.Floor((x - low) / width * query.Pixels), 0, query.Pixels - 1);
            var i = 0;
            while (i < rows.Count)
            {
                var column = Column(rows[i].X);
                var j = i;
                while (j + 1 < rows.Count && Column(rows[j + 1].X) == column)
                    j++;
                // Runs of rows with a value, each thinned on its own: a row with none is a gap, kept where it is.
                var start = i;
                for (var k = i; k <= j + 1; k++)
                {
                    if (k <= j && rows[k].Y is not null)
                        continue;
                    if (k > start)
                        KeepRun(start, k - 1);
                    if (k <= j)
                        Keep(k);
                    start = k + 1;
                }
                i = j + 1;
            }
        }
        return new ChartSourceAnswer(version, ChartData.Of((query.X, xs), (query.Y, ys)), thinned);

        void KeepRun(int from, int to)
        {
            if (from == to)
            {
                Keep(from);
                return;
            }
            var lo = from;
            var hi = from;
            for (var k = from; k <= to; k++)
            {
                if (rows[k].Y < rows[lo].Y) lo = k;
                if (rows[k].Y > rows[hi].Y) hi = k;
            }
            var kept = new SortedSet<int> { from, lo, hi, to };
            foreach (var k in kept)
                Keep(k);
            // Said only where rows were left out: a pixel whose rows were all kept is drawn as they are.
            if (kept.Count < to - from + 1)
                thinned.Add(new PixelSummary(rows[from].X, rows[to].X, rows[lo].Y!.Value, rows[hi].Y!.Value, to - from + 1));
        }
    }

    // A position as a Chart Spec reads one (ChartSpec): a date's day number, or a number.
    private static double PositionOf(object? value, AxisValues values, string data, int row)
    {
        switch (values)
        {
            case AxisValues.Dates when value is DateOnly d:
                return d.DayNumber;
            case AxisValues.Dates when value is DateTime t && t.TimeOfDay == TimeSpan.Zero:
                return DateOnly.FromDateTime(t).DayNumber;
            case AxisValues.Numbers when NumberOf(value, data, row) is { } n:
                return n;
        }
        var expected = values == AxisValues.Dates ? "a date" : "a number";
        throw new ChartSpecRefusedException("", $"Row {row + 1} of \"{data}\" holds \"{Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)}\" across, and the axis needs {expected}.");
    }

    private static double? NumberOf(object? value, string data, int row) => value switch
    {
        null => null,
        decimal m => (double)m,
        double d when double.IsFinite(d) => d,
        float f when float.IsFinite(f) => f,
        int n => n,
        long n => n,
        short n => n,
        byte n => n,
        _ => throw new ChartSpecRefusedException("", $"Row {row + 1} of \"{data}\" holds \"{Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)}\", which is not a number."),
    };

    private sealed class InMemory(IChartData data, long version) : IChartSource
    {
        public ValueTask<ChartSourceAnswer> ThinAsync(ChartSourceQuery query, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Thin(data, query, version));
    }

    // The keys a series read from a Chart Source may carry: its line, read from x and y (ADR-0344).
    internal static readonly string[] SeriesKeys = ["id", "name", "kind", "data", "x", "y", "panel", "axis", "fromZero", "step"];

    // The kinds a thinned line stands for.
    internal static readonly string[] Kinds = ["level", "rateOverInterval", "amountPerPeriod"];

    /// <summary>What a spec asks of its sources, checked before any is asked (ADR-0344).</summary>
    internal static (AxisValues Values, IReadOnlyList<(string Data, string X, string Y)> Series) Asked(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("surface", out _))
            throw new ChartSpecRefusedException("surface", "surface needs every point of its grid or its mesh, which a Chart Source does not thin; a chart read from one draws series.");
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("axis", out var axis) || axis.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("series", out var series) || series.ValueKind != JsonValueKind.Array)
            return (AxisValues.Numbers, []);
        var values = axis.TryGetProperty("values", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        if (values == "labels")
            throw new ChartSpecRefusedException("axis.values", "axis.values \"labels\" needs every category, which only the whole column knows; a chart read from a Chart Source places dates or numbers.");
        var asked = new List<(string, string, string)>();
        var i = 0;
        foreach (var s in series.EnumerateArray())
        {
            var path = $"series[{i++}]";
            if (s.ValueKind != JsonValueKind.Object)
                throw new ChartSpecRefusedException(path, $"{path} is not an object: each series names its id, name, kind, data, x and y.");
            foreach (var p in s.EnumerateObject())
            {
                if (!SeriesKeys.Contains(p.Name) && ChartSpec.SeriesKeys.Contains(p.Name))
                    throw new ChartSpecRefusedException($"{path}.{p.Name}", $"{path}.{p.Name} is one value a point, and a point read from a Chart Source stands for every row of its pixel; a series read from one draws its line from x and y.");
            }
            var kind = s.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
            // A kind no Chart Spec knows is the reader's to refuse; one it knows and a thinned line cannot stand for, here.
            if (kind is not null && !Kinds.Contains(kind) && ChartSpec.KindNames.Contains(kind))
                throw new ChartSpecRefusedException($"{path}.kind", kind == "observation"
                    ? $"{path}.kind \"observation\" draws each row as a point of its own, which a Chart Source does not thin; a series read from one is a level, a rateOverInterval or an amountPerPeriod."
                    : $"{path}.kind \"{kind}\" does not draw a line a Chart Source can thin; a series read from one is a level, a rateOverInterval or an amountPerPeriod.");
            string Text(string key) => s.TryGetProperty(key, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString()!
                : throw new ChartSpecRefusedException($"{path}.{key}", $"{path}.{key} names a column, and is needed: a series read from a Chart Source is asked for its \"data\", its \"x\" and its \"y\".");
            var (data, x, y) = (Text("data"), Text("x"), Text("y"));
            asked.Add((data, x, y));
        }
        return (values == "dates" ? AxisValues.Dates : AxisValues.Numbers, asked);
    }
}
