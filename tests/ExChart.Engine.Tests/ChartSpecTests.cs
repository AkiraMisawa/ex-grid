using ExChart.Engine;

namespace ExChart.Engine.Tests;

// What the Chart Spec reader reads and refuses is shared with @ex-grid/chart (ADR-0370, sections 2 and 3):
// tests/cases/chart/spec*.json, run by Cases/ChartCaseTests.cs. What stays here is what a case cannot say.
public class ChartSpecTests
{
    [Fact] // ADR-0251: a decimal or a whole number read from a column stays exact, which a case's numbers do not show
    public void A_decimal_or_an_integer_read_from_a_column_stays_exact()
    {
        var table = ChartData.Of(("bucket", new object?[] { "1Y", "5Y" }), ("dv01", new object?[] { 1.25m, -2 }));
        var reading = ChartSpec.Read("""
            { "version": 1, "axis": { "values": "labels" },
              "series": [ { "id": "d", "name": "DV01", "kind": "level", "data": "risk", "x": "bucket", "y": "dv01" } ] }
            """, h => table);
        var points = reading.Definition.Series[0].Points;
        Assert.True(points[0].Y!.Value.IsDecimal);
        Assert.Equal("1.25", points[0].Y!.Value.ToInvariantString());
        Assert.True(points[1].Y!.Value.IsDecimal); // an integer is exact
    }

    [Fact] // ADR-0320: a reference table refuses columns of different lengths
    public void A_table_refuses_columns_of_different_lengths() =>
        Assert.Throws<ArgumentException>(() => ChartData.Of(("a", new object?[] { 1 }), ("b", new object?[] { 1, 2 })));
}

public class ChartSpecToolTests
{
    private static readonly ChartData Rates = ChartData.Of(
        ("date", new object?[] { new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 6) }),
        ("rate", new object?[] { 1.25m, null, 1.5m }));

    [Fact] // ADR-0320: the tool definition's schema says exactly what the reader reads
    public void The_schema_names_what_the_reader_reads()
    {
        using var schema = System.Text.Json.JsonDocument.Parse(ChartSpec.Tool.InputSchema);
        var root = schema.RootElement;
        string[] Keys(System.Text.Json.JsonElement o) => o.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(ChartSpec.RootKeys.Order(), Keys(root));
        Assert.Equal(ChartSpec.AxisKeys.Order(), Keys(root.GetProperty("$defs").GetProperty("axis")));
        Assert.Equal(ChartSpec.SeriesKeys.Order(), Keys(root.GetProperty("$defs").GetProperty("series")));
        Assert.Equal(ChartSpec.ColourKeys.Order(), Keys(root.GetProperty("$defs").GetProperty("colour")));
        Assert.Equal(ChartSpec.SizeKeys.Order(), Keys(root.GetProperty("$defs").GetProperty("size")));
        Assert.Equal(ChartSpec.EdgeKeys.Order(), Keys(root.GetProperty("$defs").GetProperty("edges")));
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(ChartSpec.KindNames.Order(),
            root.GetProperty("$defs").GetProperty("series").GetProperty("properties").GetProperty("kind").GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).Order());
        Assert.Equal(ChartSpec.Version, root.GetProperty("properties").GetProperty("version").GetProperty("const").GetInt32());
        Assert.Equal(ChartSpec.IntervalKeys.Order(), Keys(root.GetProperty("$defs").GetProperty("interval")));
        Assert.Equal(ChartSpec.SurfaceKeys.Order(), Keys(root.GetProperty("$defs").GetProperty("surface")));
        Assert.Equal(ChartSpec.TriangleKeys.Order(), Keys(root.GetProperty("$defs").GetProperty("triangles")));
        Assert.Equal(["version"], root.GetProperty("required").EnumerateArray().Select(e => e.GetString()!));
        // A spec draws series on an axis, or a surface (ticket 77).
        Assert.Equal([["axis", "series"], ["surface"]], root.GetProperty("oneOf").EnumerateArray()
            .Select(o => o.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToArray()));
        Assert.Equal("draw_chart", ChartSpec.Tool.Name);
        Assert.Contains("Data Handle", ChartSpec.Tool.Description);
    }

    [Fact] // ADR-0320: an accepted spec is answered with the chart said in words, its thinned pixels and its gaps
    public void An_accepted_spec_is_answered_with_its_summary()
    {
        var answer = ChartSpec.Answer("""
            { "version": 1, "title": "Policy rate", "axis": { "values": "dates" },
              "series": [ { "id": "r", "name": "Rate", "kind": "level", "data": "rates", "x": "date", "y": "rate", "fromZero": false } ] }
            """, h => h == "rates" ? Rates : null);
        Assert.True(answer.Accepted);
        Assert.NotNull(answer.Reading);
        Assert.StartsWith("Policy rate.", answer.Text);
        Assert.Contains("Rate: maximum 1.5 at 2026-01-06.", answer.Text);
        Assert.Contains("1 point is missing, drawn as a gap.", answer.Text);
        Assert.Contains("No pixel holds more than one point.", answer.Text); // none is said, never left unsaid
    }

    [Fact] // ADR-0254: past the plot's pixels, the answer says how many pixels hold more than one point
    public void The_answer_says_how_many_pixels_were_thinned()
    {
        var xs = Enumerable.Range(0, 5000).Select(i => (object?)(double)i).ToArray();
        var ys = Enumerable.Range(0, 5000).Select(i => (object?)Math.Sin(i)).ToArray();
        var data = ChartData.Of(("x", xs), ("y", ys));
        var answer = ChartSpec.Answer("""
            { "version": 1, "axis": { "values": "numbers" },
              "series": [ { "id": "s", "name": "Noise", "kind": "level", "data": "d", "x": "x", "y": "y" } ] }
            """, h => data, new ChartMetrics(400, 300));
        Assert.Matches(@"\d+ pixels each hold more than one point", answer.Text);
    }

    [Fact] // ADR-0320: a refused spec is answered with its refusal, which the AI acts on
    public void A_refused_spec_is_answered_with_the_refusal()
    {
        var answer = ChartSpec.Answer("{ \"version\": 1 }", h => null);
        Assert.False(answer.Accepted);
        Assert.Null(answer.Reading);
        Assert.StartsWith("axis is missing", answer.Text);
    }
}


public class ChartSpecEdgeTests
{
    private static readonly ChartData Nodes = ChartData.Of(
        ("id", new object?[] { 0, 1, 2 }), ("step", new object?[] { 0, 1, 1 }), ("price", new object?[] { 100m, 90m, 110m }),
        ("probability", new object?[] { 1m, 0.5m, null }), ("exercise", new object?[] { "no", "yes", "no" }));

    private static readonly ChartData Links = ChartData.Of(("from", new object?[] { "0", "0" }), ("to", new object?[] { "1", "2" }));

    private static IChartData? Tables(string h) => h switch { "nodes" => Nodes, "links" => Links, _ => null };

    private const string Spec = """
        { "version": 1, "axis": { "values": "numbers", "title": "Step" },
          "series": [ { "id": "t", "name": "Tree", "kind": "observation", "data": "nodes", "x": "step", "y": "price", "key": "id",
                        "tooltip": ["probability"], "edges": { "data": "links", "from": "from", "to": "to" } } ] }
        """;

    [Fact] // ADR-0321: a node's tooltip and the table say the columns the spec names, and no other (what is read: spec-edges.json)
    public void A_nodes_tooltip_says_the_columns_the_spec_names()
    {
        var d = ChartSpec.Read(Spec, Tables).Definition;
        var scene = ChartScene.Build(d, new ChartMetrics(600, 400));
        var mark = scene.Marks.ToList().FindIndex(m => m.Point == 1);
        var note = ChartReadings.OfMark(d, scene, mark).Lines[0].Note;
        Assert.Contains("probability 0.5", note);
        Assert.DoesNotContain("exercise", note);
        Assert.Contains("probability has no value", ChartReadings.OfMark(d, scene, scene.Marks.ToList().FindIndex(m => m.Point == 2)).Lines[0].Note);
        Assert.Contains("probability", ChartCsv.WideTable(d, new ChartMetrics(600, 400)).Header);
    }
}
