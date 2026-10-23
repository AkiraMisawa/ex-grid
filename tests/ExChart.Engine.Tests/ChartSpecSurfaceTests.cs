using ExChart.Engine;

namespace ExChart.Engine.Tests;

/// <summary>Ticket 77: a 3D surface and a series' intervals said in a Chart Spec (ADR-0320).</summary>
public class ChartSpecSurfaceTests
{
    // An FX volatility grid as a capability would return it: a row a tenor and delta, one hole.
    private static readonly ChartData Vols = ChartData.Of(
        ("tenor", new object?[] { "1W", "1W", "1M", "1M", "1Y", "1Y" }),
        ("delta", new object?[] { 25, 50, 25, 50, 25, 50 }),
        ("vol", new object?[] { 8.1m, 7.9m, 8.4m, null, 9.0m, 8.7m }),
        ("filled", new object?[] { false, true, false, false, false, true }));

    // A mesh: four nodes keyed by name, two triangles.
    private static readonly ChartData Nodes = ChartData.Of(
        ("node", new object?[] { "p", "q", "r", "s" }),
        ("strike", new object?[] { 90.0, 110.0, 90.0, 110.0 }),
        ("expiry", new object?[] { 0.5, 0.5, 1.0, 1.0 }),
        ("pnl", new object?[] { -2.0, 1.0, 0.5, 3.0 }));

    private static readonly ChartData Triangles = ChartData.Of(
        ("a", new object?[] { "p", "q" }), ("b", new object?[] { "q", "s" }), ("c", new object?[] { "r", "r" }));

    private static readonly ChartData Quotes = ChartData.Of(
        ("x", new object?[] { 1.0, 2.0, 3.0 }),
        ("y", new object?[] { 10m, 12m, 11m }),
        ("lo", new object?[] { 9m, null, 10m }),
        ("hi", new object?[] { 11m, 13m, 12m }),
        ("xlo", new object?[] { 0.9, 1.8, 2.7 }),
        ("xhi", new object?[] { 1.1, 2.2, 3.3 }));

    private static IChartData? Tables(string h) => h switch
    {
        "vols" => Vols,
        "nodes" => Nodes,
        "triangles" => Triangles,
        "quotes" => Quotes,
        _ => null,
    };

    private static string Refusal(string json) => Assert.Throws<ChartSpecRefusedException>(() => ChartSpec.Read(json, Tables)).Message;

    private const string Grid = """
        { "version": 1, "title": "EURUSD volatility",
          "surface": { "data": "vols", "x": "tenor", "y": "delta", "z": "vol", "xTitle": "Tenor", "zTitle": "Volatility" },
          "notes": ["Mid quotes."] }
        """;

    [Fact] // ADR-0320, ADR-0274: a surface spec reads a grid from its x, y and z columns, a null value a hole
    public void A_surface_spec_reads_a_grid()
    {
        var reading = ChartSpec.Read(Grid, Tables);
        var s = reading.Surface!;
        Assert.Equal("EURUSD volatility", reading.Title);
        Assert.Equal(["1W", "1M", "1Y"], s.X.Values); // labels as the data first shows them, not sorted
        Assert.Equal([25.0, 50.0], s.Y.Values);
        Assert.Equal("Tenor", s.X.Title);
        Assert.Equal("delta", s.Y.Title);
        Assert.Equal("Volatility", s.ZTitle);
        Assert.Null(s.At(1, 1));
        Assert.Equal(8.7, s.At(2, 1)!.Value.ToDouble());
        Assert.Equal(["Mid quotes."], s.Notes);
        Assert.Throws<InvalidOperationException>(() => reading.Definition);
        Assert.StartsWith("ChartSpecReading { Title = EURUSD volatility, Surface = ", reading.ToString()); // printed, not thrown
    }

    [Fact] // ADR-0320: the tool answers a surface with the surface said in words
    public void A_surface_is_answered_in_words()
    {
        var answer = ChartSpec.Answer(Grid, Tables);
        Assert.True(answer.Accepted);
        Assert.StartsWith("EURUSD volatility, a 3D surface of Volatility over Tenor from 1W to 1Y", answer.Text);
        Assert.Contains("with 1 hole", answer.Text);
    }

    [Fact] // ADR-0310, ADR-0312, ADR-0257: its colours' centre, its scales, and the method a capability interpolated with
    public void A_surface_takes_its_centre_scales_and_method()
    {
        var s = ChartSpec.Read(Grid.Replace("\"zTitle\": \"Volatility\"",
            "\"zTitle\": \"Volatility\", \"centre\": 8.5, \"yScale\": \"logarithmic\", \"zScale\": \"logarithmic\", \"method\": \"bilinear\", \"interpolated\": \"filled\""), Tables).Surface!;
        Assert.Equal(8.5, s.Centre);
        Assert.Equal(AxisScale.Logarithmic, s.Y.Scale);
        Assert.Equal(AxisScale.Logarithmic, s.ZScale);
        Assert.Equal("bilinear", s.Method);
        Assert.True(s.IsInterpolated(0, 1));
        Assert.False(s.IsInterpolated(0, 0));
    }

    [Fact] // ADR-0311: a mesh is the nodes keyed by a column and a table of triangles naming them
    public void A_surface_spec_reads_a_mesh()
    {
        const string mesh = """
            { "version": 1, "surface": { "data": "nodes", "x": "strike", "y": "expiry", "z": "pnl", "key": "node", "centre": 0,
              "triangles": { "data": "triangles", "a": "a", "b": "b", "c": "c" } } }
            """;
        var s = ChartSpec.Read(mesh, Tables).Surface!;
        Assert.True(s.IsMesh);
        Assert.Equal(4, s.Nodes!.Count);
        Assert.Equal([new SurfaceTriangle(0, 1, 2), new SurfaceTriangle(1, 3, 2)], s.Triangles);
        Assert.Equal(AxisScale.Proportional, s.X.Scale);
        Assert.Equal(0, s.Centre);
        Assert.StartsWith("surface.triangles.b: row 2 of \"triangles\" names node t, but no node is keyed t", MeshWith(mesh, ["p", "q"], ["q", "t"], ["r", "r"]));
        Assert.StartsWith("surface.key is missing", Refusal(mesh.Replace("\"key\": \"node\",", "")));
    }

    private static string MeshWith(string mesh, string[] a, string[] b, string[] c)
    {
        var bad = ChartData.Of(("a", a), ("b", b), ("c", c));
        return Assert.Throws<ChartSpecRefusedException>(() => ChartSpec.Read(mesh, h => h == "triangles" ? bad : Tables(h))).Message;
    }

    [Fact] // ADR-0274, ADR-0257: rows that form no grid are refused, never interpolated onto one
    public void Scattered_rows_are_refused()
    {
        var scattered = ChartData.Of(("x", new object?[] { 1.0, 2.0, 3.0 }), ("y", new object?[] { 1.0, 1.0, 2.0 }), ("z", new object?[] { 1.0, 2.0, 3.0 }));
        var m = Assert.Throws<ChartSpecRefusedException>(() => ChartSpec.Read("""{ "version": 1, "surface": { "data": "s", "x": "x", "y": "y", "z": "z" } }""",
            h => h == "s" ? scattered : null)).Message;
        Assert.StartsWith("surface.x: x 1 has a value at only one y", m);
    }

    [Fact] // ADR-0320: a spec draws a surface or series, and each surface key is checked by its path
    public void A_surface_is_refused_by_its_path()
    {
        Assert.StartsWith("axis belongs to a chart of series", Refusal(Grid.Replace("\"surface\":", "\"axis\": { \"values\": \"numbers\" }, \"surface\":")));
        Assert.StartsWith("surface.z: \"vols\" has no column \"price\"", Refusal(Grid.Replace("\"z\": \"vol\"", "\"z\": \"price\"")));
        Assert.StartsWith("surface.xScale: only numbers are placed by value", Refusal(Grid.Replace("\"xTitle\"", "\"xScale\": \"proportional\", \"xTitle\"")));
        Assert.StartsWith("surface.method is missing", Refusal(Grid.Replace("\"xTitle\"", "\"interpolated\": \"filled\", \"xTitle\"")));
        Assert.StartsWith("surface.colour is not part of a Chart Spec", Refusal(Grid.Replace("\"xTitle\"", "\"colour\": \"red\", \"xTitle\"")));
    }

    [Fact] // ADR-0344: a surface needs every point, which a Chart Source does not thin
    public async Task A_surface_is_not_read_through_a_chart_source()
    {
        var e = await Assert.ThrowsAsync<ChartSpecRefusedException>(() => ChartSpec.ReadAsync(Grid, _ => ChartSource.Over(Vols), 100, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("surface", e.Path);
    }

    [Fact] // ADR-0303, ADR-0320: a level's interval is read from its lower and upper columns, with its meaning and style
    public void A_levels_interval_is_read_from_columns()
    {
        const string spec = """
            { "version": 1, "axis": { "values": "numbers" },
              "series": [ { "id": "c", "name": "Curve", "kind": "level", "data": "quotes", "x": "x", "y": "y",
                            "interval": { "lower": "lo", "upper": "hi", "meaning": "Bid–Ask", "style": "band" } } ] }
            """;
        var s = ChartSpec.Read(spec, Tables).Definition.Series[0];
        Assert.Equal("Bid–Ask", s.Interval!.Meaning);
        Assert.Equal(IntervalStyle.Band, s.Interval.Style);
        Assert.Equal(new IntervalBound(2, null, ChartValue.Of(13m)), s.Interval.Bounds[1]);
        Assert.Contains("Bid–Ask", ChartSpec.Answer(spec, Tables).Text);
        Assert.StartsWith("series[0].interval.meaning is missing", Refusal(spec.Replace(", \"meaning\": \"Bid–Ask\"", "")));
        Assert.StartsWith("series[0].interval belongs to a level or an observation", Refusal(spec.Replace("\"level\"", "\"rateOverInterval\"")));
    }

    [Fact] // ADR-0304, ADR-0320: an observation's horizontal interval is read in the units across, whiskers only
    public void An_observations_intervals_are_read_both_ways()
    {
        const string spec = """
            { "version": 1, "axis": { "values": "numbers" },
              "series": [ { "id": "o", "name": "Names", "kind": "observation", "data": "quotes", "x": "x", "y": "y",
                            "interval": { "lower": "lo", "upper": "hi", "meaning": "95% CI" },
                            "xInterval": { "lower": "xlo", "upper": "xhi", "meaning": "±1σ" } } ] }
            """;
        var s = ChartSpec.Read(spec, Tables).Definition.Series[0];
        Assert.Equal(1.8, s.XInterval!.Bounds[1].Lower!.Value.ToDouble());
        Assert.Equal("±1σ", s.XInterval.Meaning);
        Assert.Equal("95% CI", s.Interval!.Meaning);
        Assert.StartsWith("series[0]: ", Refusal(spec.Replace("\"meaning\": \"95% CI\"", "\"meaning\": \"95% CI\", \"style\": \"band\"")));
        Assert.StartsWith("series[0].xInterval belongs to an observation", Refusal(spec.Replace("\"observation\"", "\"level\"").Replace(
            "\"interval\": { \"lower\": \"lo\", \"upper\": \"hi\", \"meaning\": \"95% CI\" },", "")));
    }
}
