using ExChart.Engine;

namespace ExChart.Engine.Tests;

public class SurfaceTests
{
    private static readonly string[] Deltas = ["10P", "25P", "ATM", "25C"];
    private static readonly string[] Tenors = ["1M", "3M", "1Y"];

    private static SurfaceDefinition Vol(params double?[][] rows) =>
        new(SurfaceAxis.Of("Delta", Deltas), SurfaceAxis.Of("Tenor", Tenors),
            rows.Select(r => (IReadOnlyList<ChartValue?>)r.Select(v => v is { } d ? ChartValue.Of(d) : (ChartValue?)null).ToArray()).ToArray(),
            "Volatility", v => ((double)v).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "%");

    private static SurfaceDefinition Full() => Vol(
        [10.5, 9.8, 9.2, 9.9],
        [10.9, 10.1, 9.6, 10.2],
        [11.4, 10.7, 10.3, 10.8]);

    [Fact] // CH-7, ADR-0274: scattered points are refused by name
    public void Scattered_points_are_refused_by_name()
    {
        (double, double, ChartValue)[] points = [(1.10, 0.1, 9.1), (1.12, 0.4, 9.4), (1.15, 0.9, 9.9), (1.21, 1.6, 10.3)];
        var e = Assert.Throws<ChartRefusedException>(() => SurfaceDefinition.FromPoints(points, "Strike", "Tenor", "Volatility"));
        Assert.Contains("scattered points", e.Message);
        Assert.Contains("Strike 1.1", e.Message);
        Assert.Contains("interpolation", e.Message);
    }

    [Fact] // CH-7, ADR-0274: points on a grid make a surface, and where the grid has no point it has a hole, never 0
    public void Points_on_a_grid_make_a_surface_with_a_hole_where_none_was_given()
    {
        (int, int, ChartValue)[] points = [(1, 1, 9.0), (2, 1, 9.5), (1, 2, 10.0), (2, 2, 10.5), (3, 2, 11.0), (3, 1, 9.8), (1, 3, 12.0), (2, 3, 12.5)];
        var s = SurfaceDefinition.FromPoints(points, "Strike", "Tenor");
        Assert.Equal(3, s.X.Values.Count);
        Assert.Equal(3, s.Y.Values.Count);
        Assert.Null(s.At(2, 2));
        Assert.Equal(10.5, s.At(1, 1)!.Value.ToDouble());
    }

    [Fact] // ADR-0274: two points at one grid point are refused, not averaged
    public void Two_points_at_one_place_are_refused()
    {
        (int, int, ChartValue)[] points = [(1, 1, 9.0), (1, 1, 9.1), (2, 1, 9.5), (1, 2, 10.0), (2, 2, 10.5)];
        Assert.Throws<ChartRefusedException>(() => SurfaceDefinition.FromPoints(points, "Strike", "Tenor"));
    }

    [Fact] // CH-7, ADR-0274: a missing value is a hole, never 0 — the faces around it are not drawn
    public void A_missing_value_is_a_hole_and_the_faces_round_it_are_not_drawn()
    {
        var mesh = SurfaceMesh.Of(Vol(
            [10.5, 9.8, 9.2, 9.9],
            [10.9, null, 9.6, 10.2],
            [11.4, 10.7, 10.3, 10.8]));
        Assert.Null(mesh.Nodes[1 * 4 + 1].Z);
        Assert.Equal(1, mesh.Holes);
        // Of the six cells, the four with the hole at a corner are no faces.
        Assert.Equal(2, mesh.Faces.Count);
        Assert.All(mesh.Faces, f => Assert.DoesNotContain(1 * 4 + 1, f.Corners));
        // The vertical scale is the values' own: a hole taken as 0 would pull it down to zero.
        Assert.True(mesh.ZMin >= 9);
    }

    [Fact] // ADR-0274: a row of the wrong length is refused — a hole is handed in as null, never left out
    public void A_row_of_the_wrong_length_is_refused()
    {
        var e = Assert.Throws<ChartRefusedException>(() => Vol([10.5, 9.8, 9.2, 9.9], [10.9, 10.1, 9.6], [11.4, 10.7, 10.3, 10.8]));
        Assert.Contains("Tenor 3M", e.Message);
        Assert.Contains("null", e.Message);
    }

    [Fact] // ADR-0274: a table with a row too few is refused
    public void A_table_with_a_row_too_few_is_refused() =>
        Assert.Throws<ChartRefusedException>(() => Vol([10.5, 9.8, 9.2, 9.9], [10.9, 10.1, 9.6, 10.2]));

    [Fact] // ADR-0274: a grid line named twice is refused
    public void A_value_named_twice_on_an_axis_is_refused()
    {
        var e = Assert.Throws<ChartRefusedException>(() => new SurfaceDefinition(SurfaceAxis.Of("Delta", new[] { "ATM", "ATM" }), SurfaceAxis.Of("Tenor", new[] { "1M", "3M" }),
            [[9.0, 9.1], [9.2, 9.3]]));
        Assert.Contains("ATM appears twice", e.Message);
    }

    [Fact] // ADR-0274: a surface of holes only is refused
    public void A_surface_with_no_value_is_refused() =>
        Assert.Throws<ChartRefusedException>(() => Vol([null, null, null, null], [null, null, null, null], [null, null, null, null]));

    [Fact] // ADR-0255: .NET decides the colour scale; the lowest face takes the first band and the highest the last
    public void Faces_are_banded_from_low_to_high()
    {
        var mesh = SurfaceMesh.Of(Full());
        var bands = mesh.Faces.Select(f => f.Band).ToArray();
        Assert.Equal(6, bands.Length);
        Assert.All(bands, b => Assert.InRange(b, 0, SurfaceMesh.BandCount - 1));
        Assert.True(bands[3] > bands[2]);
        Assert.Equal(["10P", "25P", "ATM", "25C"], mesh.XLines.Select(t => t.Label));
        Assert.True(mesh.ZMin <= 9.2 && mesh.ZMax >= 11.4);
        Assert.Equal(mesh.ZMin, mesh.ZTicks[0].Value);
        Assert.Equal(mesh.ZMax, mesh.ZTicks[^1].Value);
    }

    [Fact] // ADR-0274: a slice is a 2D chart of the values along the other axis, read exactly; a hole is a gap
    public void A_slice_is_a_chart_along_the_other_axis_with_a_hole_as_a_gap()
    {
        var s = Vol(
            [10.5, 9.8, 9.2, 9.9],
            [10.9, null, 9.6, 10.2],
            [11.4, 10.7, 10.3, 10.8]);
        var smile = s.SliceChart(SurfaceSlice.AtY("3M"))!;
        Assert.Equal(Deltas, smile.Axis.Labels);
        Assert.Equal("Volatility at 3M", smile.Series[0].Name);
        Assert.Null(smile.Series[0].Points[1].Y);
        Assert.Equal("10.90%", smile.Series[0].Write(smile.Series[0].Points[0].Y!.Value));

        var term = s.SliceChart(SurfaceSlice.AtX("ATM"))!;
        Assert.Equal(Tenors, term.Axis.Labels);
        Assert.Equal([9.2, 9.6, 10.3], term.Series[0].Points.Select(p => p.Y!.Value.ToDouble()));
    }

    [Fact] // ADR-0272: a slice is named in the data's terms; one the new data does not hold is no slice
    public void A_slice_the_surface_does_not_hold_draws_nothing() =>
        Assert.Null(Full().SliceChart(SurfaceSlice.AtY("5Y")));

    [Fact] // ADR-0275: the surface is said in words, holes included
    public void The_summary_names_the_grid_the_range_and_the_holes()
    {
        var text = SurfaceMesh.Summary(Vol(
            [10.5, 9.8, 9.2, 9.9],
            [10.9, null, 9.6, 10.2],
            [11.4, 10.7, 10.3, 10.8]), "EURUSD", null);
        Assert.Equal("EURUSD, a 3D surface of Volatility over Delta from 10P to 25C and Tenor from 1M to 1Y; 4 by 3 points, from 9.20% to 11.40%, with 1 hole where the data has no value.", text);
    }
}
