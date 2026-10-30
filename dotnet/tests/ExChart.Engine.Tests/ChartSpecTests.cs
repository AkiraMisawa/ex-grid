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

// What the tool answers the AI is said by the shared cases too, which hold it beside each reading and refusal.
public class ChartSpecToolTests
{
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
}

// The tree of ADR-0321, its nodes and edges, is read by the shared cases of tests/cases/chart/spec-edges.json, and what its
// tooltip and its table say by reading.json and csv.json.
