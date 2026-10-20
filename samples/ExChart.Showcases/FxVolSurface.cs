using System.Globalization;
using ExChart.Engine;
using ExChart.Engine.Interpolation;
using ExChart.Showcases.Data;

namespace ExChart.Showcases;

/// <summary>
/// FX volatility's surface, made by the page from the quotes (ADR-0280): a missing quote that lies
/// between two quotes of its expiry is filled by a natural cubic spline across the smile, with
/// ExChart.Engine's Interpolation Helper, and marked interpolated, so the surface's tooltip and Notes
/// say so (ADR-0257, ADR-0274). One outside them stays a hole: the helper never extrapolates. The
/// method is this page's choice, not the chart's, and not a claim about how a desk builds a surface.
/// </summary>
public static class FxVolSurface
{
    /// <summary>The method the filled quotes were made with, as the surface names it.</summary>
    public const string Method = "natural cubic spline across delta, per expiry";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Writes a volatility as the grid and the charts write it.</summary>
    public static readonly Func<object, string> Percent = v => ((double)v).ToString("0.00", Invariant) + "%";

    /// <summary>The tenors along, in the order of <see cref="FxVolData.Quotes"/>.</summary>
    public static readonly string[] Tenors = [.. FxVolData.Quotes.Select(r => r.Tenor)];

    /// <summary>The volatilities: each expiry's quotes, its missing quotes filled where they lie between quotes.</summary>
    public static readonly IReadOnlyList<IReadOnlyList<double?>> Filled = [.. FxVolData.Quotes.Select(Fill)];

    /// <summary>The surface: deltas across, tenors along, the filled values marked.</summary>
    public static readonly SurfaceDefinition Surface = new(
        SurfaceAxis.Of("Delta", FxVolData.Deltas), SurfaceAxis.Of("Tenor", Tenors),
        [.. Filled.Select(row => (IReadOnlyList<ChartValue?>)[.. row.Select(v => v is { } x ? (ChartValue?)ChartValue.Of(x) : null)])],
        "Volatility", Percent, Method,
        [.. FxVolData.Quotes.Select((r, j) => (IReadOnlyList<bool>)[.. r.Vols.Select((v, i) => v is null && Filled[j][i] is not null)])],
        [.. Holes()]);

    private static IReadOnlyList<double?> Fill(FxVolRow row)
    {
        var quoted = row.Vols.Select((v, i) => (Delta: FxVolData.DeltaValues[i], Vol: v)).Where(p => p.Vol is not null).ToArray();
        var spline = Interpolate.NaturalCubicSpline([.. quoted.Select(p => p.Delta)], [.. quoted.Select(p => p.Vol!.Value)]);
        return [.. row.Vols.Select((v, i) => v ?? spline.Evaluate(FxVolData.DeltaValues[i]))];
    }

    private static IEnumerable<string> Holes()
    {
        for (var j = 0; j < Tenors.Length; j++)
            for (var i = 0; i < FxVolData.Deltas.Length; i++)
                if (Filled[j][i] is null)
                    yield return $"{FxVolData.Deltas[i]} at {Tenors[j]} has no quote and lies outside its expiry's quotes, so it is a hole: nothing is extrapolated.";
    }

    /// <summary>
    /// A slice of the surface as a 2D chart on category axes (ADR-0274): the quotes, and where the slice
    /// holds a filled value, the slice itself declared Interpolated under the same colour, so the legend
    /// shows one entry and the tooltip says which values are quoted (ADR-0257).
    /// </summary>
    public static ChartDefinition Slice(SurfaceSlice slice, string name)
    {
        var chart = Surface.SliceChart(slice) ?? throw new ArgumentException("The slice is not on the surface.", nameof(slice));
        var keys = new AppearanceKeys(name);
        var surface = chart.Series[0] with { Name = name, Keys = keys };
        if (!surface.Interpolated) return new ChartDefinition(chart.Axis, [surface], chart.Notes);
        var at = slice.Axis == SurfaceSliceAxis.X ? Array.IndexOf(FxVolData.Deltas, slice.Value) : Array.IndexOf(Tenors, slice.Value);
        var quotes = slice.Axis == SurfaceSliceAxis.X
            ? FxVolData.Quotes.Select(r => r.Vols[at])
            : FxVolData.Quotes[at].Vols;
        var quoted = ChartSeries.Of("quoted", name, SeriesKind.Level,
                quotes.Select((v, k) => new ChartPoint(k, v is { } x ? ChartValue.Of(x) : null)))
            with { FromZero = false, Format = Percent, Keys = keys };
        return new ChartDefinition(chart.Axis, [quoted, surface with { Id = "interpolated" }], chart.Notes);
    }
}
