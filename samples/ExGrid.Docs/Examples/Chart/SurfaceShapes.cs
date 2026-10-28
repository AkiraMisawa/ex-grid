using System.Globalization;
using ExChart.Engine;

namespace ExGrid.Docs.Examples.Chart;

/// <summary>Fixed surfaces for the 3D Examples of a centre, a triangle mesh and logarithmic axes.</summary>
public static class SurfaceShapes
{
    private static readonly double[] SpotShifts = [-10, -5, -2, 0, 2, 5, 10];
    private static readonly double[] VolShifts = [-4, -2, 0, 2, 4];

    /// <summary>
    /// A book's P&amp;L, in thousands, over spot and volatility shifts: short volatility and a little long
    /// spot. Coloured from zero, so a loss and a gain of the same size are equally deep.
    /// </summary>
    public static SurfaceDefinition Pnl() => new SurfaceDefinition(
        SurfaceAxis.Of("Spot shift (%)", SpotShifts) with { Scale = AxisScale.Proportional },
        SurfaceAxis.Of("Vol shift (pts)", VolShifts) with { Scale = AxisScale.Proportional },
        VolShifts.Select(v => (IReadOnlyList<ChartValue?>)SpotShifts.Select(s => (ChartValue?)ChartValue.Of(Math.Round(12 * s - 3 * s * s - 40 * v, 1))).ToArray()).ToArray(),
        "P&L (k)", v => ((double)v).ToString("#,0.0", CultureInfo.InvariantCulture))
    {
        Centre = 0,
    };

    /// <summary>
    /// A field on the page's own triangles: a lattice on the unit square with its inner nodes moved off
    /// it, each cell cut along an alternating diagonal, and one cell left out of the domain.
    /// </summary>
    public static SurfaceDefinition Field()
    {
        var nodes = new List<SurfaceNode>();
        for (var j = 0; j <= 6; j++)
            for (var i = 0; i <= 6; i++)
            {
                var inner = i is > 0 and < 6 && j is > 0 and < 6;
                var x = Math.Round(i / 6.0 + (inner ? 0.03 * ((i + 2 * j) % 3 - 1) : 0), 4);
                var y = Math.Round(j / 6.0 + (inner ? 0.03 * ((2 * i + j) % 3 - 1) : 0), 4);
                nodes.Add(new SurfaceNode(x, y, ChartValue.Of(Math.Round(Math.Sin(Math.PI * x) * Math.Cos(Math.PI * y), 4))));
            }
        var triangles = new List<SurfaceTriangle>();
        for (var j = 0; j < 6; j++)
            for (var i = 0; i < 6; i++)
            {
                if (i == 2 && j == 2) continue; // the domain's hole
                int a = j * 7 + i, b = a + 1, c = a + 8, d = a + 7;
                if ((i + j) % 2 == 0) { triangles.Add(new(a, b, c)); triangles.Add(new(a, c, d)); }
                else { triangles.Add(new(a, b, d)); triangles.Add(new(b, c, d)); }
            }
        return SurfaceDefinition.FromTriangles(nodes, triangles, "x", "y", "u",
            xFormatCode: "0.00", yFormatCode: "0.00");
    }

    private static readonly double[] Years = [7 / 365.0, 1 / 12.0, 0.25, 0.5, 1, 2, 5, 10];

    /// <summary>
    /// Total implied variance, σ²T, over deltas and expiries: the expiries placed by the logarithm of their
    /// years and written as quoted, and the variance, which spans three decades, on a logarithmic scale.
    /// </summary>
    public static SurfaceDefinition Variance()
    {
        var vol = VolSurface.Quoted();
        var z = Years.Select((t, j) => (IReadOnlyList<ChartValue?>)Enumerable.Range(0, VolSurface.Deltas.Length)
            .Select(i => vol.At(i, j) is { } q ? (ChartValue?)ChartValue.Of(Math.Round(q.ToDouble() * q.ToDouble() / 10_000 * t, 6)) : null).ToArray()).ToArray();
        return new SurfaceDefinition(
            SurfaceAxis.Of("Delta", VolSurface.Deltas),
            SurfaceAxis.Of("Expiry", Years, y => VolSurface.Tenors[Array.IndexOf(Years, y)]) with { Scale = AxisScale.Logarithmic },
            z, "Total variance", v => ((double)v).ToString("0.#####", CultureInfo.InvariantCulture))
        {
            ZScale = AxisScale.Logarithmic,
        };
    }
}
