using ExChart.Engine;

namespace ExChart.Engine.Tests;

public class ChartSpecTests
{
    private static readonly ChartData Rates = ChartData.Of(
        ("date", new object?[] { new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 6) }),
        ("rate", new object?[] { 1.25m, null, 1.5m }),
        ("spread", new object?[] { 0.1, 0.2, 0.3 }));

    private static IChartData? Tables(string handle) => handle == "rates" ? Rates : null;

    private static ChartSpecReading Read(string json) => ChartSpec.Read(json, Tables);

    private static string Refusal(string json) => Assert.Throws<ChartSpecRefusedException>(() => Read(json)).Message;

    private const string Level = """
        {
          "version": 1,
          "title": "Policy rate",
          "axis": { "values": "dates", "title": "Date" },
          "series": [
            { "id": "rate", "name": "Rate", "kind": "level", "data": "rates", "x": "date", "y": "rate", "fromZero": false }
          ],
          "notes": ["Source: the central bank."]
        }
        """;

    [Fact] // ADR-0320: a Chart Spec names its data's columns and becomes a Chart Definition
    public void A_spec_reads_its_points_from_the_named_columns()
    {
        var reading = Read(Level);
        var s = Assert.Single(reading.Definition.Series);
        Assert.Equal("Policy rate", reading.Title);
        Assert.Equal(AxisValues.Dates, reading.Definition.Axis.Values);
        Assert.Equal("Date", reading.Definition.Axis.Title);
        Assert.Equal(SeriesKind.Level, s.Kind);
        Assert.False(s.ReachesZero);
        Assert.Equal(new DateOnly(2026, 1, 2).DayNumber, s.Points[0].X);
        Assert.Equal("1.25", s.Points[0].Y!.Value.ToInvariantString());
        Assert.True(s.Points[0].Y!.Value.IsDecimal);
        Assert.Null(s.Points[1].Y); // a null is a gap, never a zero
        Assert.Equal(["Source: the central bank."], reading.Definition.Notes);
    }

    [Fact] // ADR-0320: the version is required, and an unknown one is refused, never read as the nearest
    public void The_version_is_required_and_an_unknown_one_is_refused()
    {
        Assert.StartsWith("version is missing", Refusal(Level.Replace("\"version\": 1,", "")));
        Assert.StartsWith("version 2 is not known", Refusal(Level.Replace("\"version\": 1", "\"version\": 2")));
    }

    [Fact] // ADR-0320 (AI-Q4): every series writes its kind, with no default
    public void A_series_without_a_kind_is_refused_by_its_path() =>
        Assert.StartsWith("series[0].kind is missing", Refusal(Level.Replace("\"kind\": \"level\", ", "")));

    [Fact] // ADR-0320: a kind written as null is missing, refused by its path, never a crash
    public void A_null_kind_is_refused_by_its_path() =>
        Assert.StartsWith("series[0].kind is missing", Refusal(Level.Replace("\"kind\": \"level\"", "\"kind\": null")));

    [Fact] // ADR-0320: an unknown key is named; only numbers written in are told a spec holds none
    public void An_unknown_key_says_numbers_only_when_numbers_were_written()
    {
        var typo = Refusal(Level.Replace("\"fromZero\": false", "\"color\": \"red\""));
        Assert.StartsWith("series[0].color is not part of a Chart Spec", typo);
        Assert.DoesNotContain("numbers", typo);
        Assert.Contains("a spec holds no numbers", Refusal(Level.Replace("\"fromZero\": false", "\"values\": [1, 2]")));
    }

    [Fact] // ADR-0320: a definition's refusal names the series it belongs to, however the series are named
    public void A_definition_refusal_names_the_series_by_its_index()
    {
        var two = Level.Replace("\"fromZero\": false }", "\"fromZero\": false },\n    { \"id\": \"other\", \"name\": \"Rate\", \"kind\": \"observation\", \"data\": \"rates\", \"x\": \"date\", \"y\": \"rate\", \"colour\": { \"column\": \"spread\", \"scale\": \"sequential\" } }");
        var refusal = Assert.Throws<ChartSpecRefusedException>(() => Read(two.Replace("\"kind\": \"observation\"", "\"kind\": \"level\"")));
        Assert.Equal("series[1]", refusal.Path);
        var duplicate = Assert.Throws<ChartSpecRefusedException>(() => Read(two.Replace("\"id\": \"other\"", "\"id\": \"rate\"")));
        Assert.Equal("series[1].id", duplicate.Path);
    }

    [Fact] // ADR-0320: an unknown kind is named, with the kinds that exist
    public void An_unknown_kind_is_refused_with_the_kinds_that_exist()
    {
        var m = Refusal(Level.Replace("\"kind\": \"level\"", "\"kind\": \"line\""));
        Assert.StartsWith("series[0].kind \"line\" is not a Series Kind", m);
        Assert.Contains("rateOverInterval", m);
    }

    [Fact] // ADR-0320: a Data Handle the host's lookup does not know is refused by name
    public void An_unknown_handle_is_refused_by_name() =>
        Assert.StartsWith("series[0].data \"curves\" is not known", Refusal(Level.Replace("\"data\": \"rates\"", "\"data\": \"curves\"")));

    [Fact] // ADR-0320: a column the table does not have is refused, naming the columns it has
    public void An_unknown_column_is_refused_naming_the_columns()
    {
        var m = Refusal(Level.Replace("\"y\": \"rate\"", "\"y\": \"yield\""));
        Assert.StartsWith("series[0].y: \"rates\" has no column \"yield\"", m);
        Assert.Contains("date, rate, spread", m);
    }

    [Fact] // ADR-0320: a spec holds no numbers; anything it does not know, values included, is refused
    public void A_property_the_spec_does_not_know_is_refused()
    {
        var m = Refusal(Level.Replace("\"y\": \"rate\"", "\"y\": \"rate\", \"values\": [1, 2, 3]"));
        Assert.StartsWith("series[0].values is not part of a Chart Spec", m);
        Assert.Contains("holds no numbers", m);
    }

    [Fact] // ADR-0320: a value of the wrong type in a column is refused naming the row
    public void A_value_of_the_wrong_type_is_refused_naming_the_row()
    {
        var table = ChartData.Of(("date", new object?[] { new DateOnly(2026, 1, 2), "soon" }), ("rate", new object?[] { 1m, 2m }));
        var e = Assert.Throws<ChartSpecRefusedException>(() => ChartSpec.Read(Level, h => table));
        Assert.StartsWith("series[0].x: row 2 of \"rates\" holds \"soon\"", e.Message);
        Assert.Contains("a date", e.Message);
    }

    [Fact] // ADR-0251: a NaN is not a gap; the data says null for a missing value
    public void A_NaN_is_refused_rather_than_drawn_as_a_gap()
    {
        var table = ChartData.Of(("date", new object?[] { new DateOnly(2026, 1, 2) }), ("rate", new object?[] { double.NaN }));
        Assert.StartsWith("series[0].y: row 1 of \"rates\" is NaN", Assert.Throws<ChartSpecRefusedException>(() => ChartSpec.Read(Level, h => table)).Message);
    }

    [Fact] // ADR-0320: the definition's own refusal comes back with the series' path
    public void The_definitions_refusal_names_the_series_path()
    {
        var m = Refusal(Level.Replace("\"kind\": \"level\"", "\"kind\": \"amountPerPeriod\"").Replace(", \"fromZero\": false", ", \"fromZero\": false"));
        Assert.StartsWith("series[0]: ", m);
        Assert.Contains("always drawn from zero", m);
    }

    [Fact] // ADR-0320: the JSON itself being broken is said, with where
    public void Broken_JSON_is_refused() =>
        Assert.StartsWith("The Chart Spec is not JSON", Refusal("{ \"version\": 1, "));

    [Fact] // ADR-0251: labels are categories in the data's order of first appearance
    public void A_label_axis_takes_its_categories_in_the_datas_order()
    {
        var table = ChartData.Of(("bucket", new object?[] { "1Y", "5Y", "10Y" }), ("dv01", new object?[] { 3, -2, 7 }));
        var reading = ChartSpec.Read("""
            { "version": 1, "axis": { "values": "labels" },
              "series": [ { "id": "d", "name": "DV01", "kind": "level", "data": "risk", "x": "bucket", "y": "dv01" } ] }
            """, h => table);
        Assert.Equal(["1Y", "5Y", "10Y"], reading.Definition.Axis.Labels);
        Assert.Equal([0d, 1, 2], reading.Definition.Series[0].Points.Select(p => p.X));
        Assert.True(reading.Definition.Series[0].Points[1].Y!.Value.IsDecimal); // an integer is exact
    }

    [Fact] // ADR-0300: a Price Range is read from its four columns, and a fifth for volume
    public void A_price_range_reads_open_high_low_close_and_volume()
    {
        var table = ChartData.Of(
            ("d", new object?[] { new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 5) }),
            ("o", new object?[] { 10m, 11m }), ("h", new object?[] { 12m, 12m }), ("l", new object?[] { 9m, 10m }),
            ("c", new object?[] { 11m, 10.5m }), ("v", new object?[] { 1000L, 1200L }));
        var reading = ChartSpec.Read("""
            { "version": 1, "axis": { "values": "dates" },
              "series": [ { "id": "px", "name": "Index", "kind": "priceRange", "data": "px", "x": "d",
                            "open": "o", "high": "h", "low": "l", "close": "c", "volume": "v" } ] }
            """, h => table);
        var s = reading.Definition.Series[0];
        Assert.Equal(SeriesKind.PriceRange, s.Kind);
        Assert.Equal("10.5", s.Bars[1].Close!.Value.ToInvariantString());
        Assert.Equal("1200", s.Bars[1].Volume!.Value.ToInvariantString());
    }

    [Fact] // ADR-0300: a Price Range has no y, and another kind has no open
    public void The_columns_belong_to_their_kind()
    {
        Assert.StartsWith("series[0].close is missing", Refusal(Level.Replace("\"kind\": \"level\"", "\"kind\": \"priceRange\"").Replace("\"y\": \"rate\"", "\"high\": \"rate\", \"low\": \"rate\"")));
        Assert.StartsWith("series[0].y belongs to a series of one value a row", Refusal(Level.Replace("\"kind\": \"level\"", "\"kind\": \"priceRange\"")));
        Assert.StartsWith("series[0].open belongs to a priceRange", Refusal(Level.Replace("\"y\": \"rate\"", "\"y\": \"rate\", \"open\": \"rate\"")));
    }

    [Fact] // ADR-0290: an Observation's points may carry names from a column
    public void An_observation_names_its_points_from_a_column()
    {
        var table = ChartData.Of(("lev", new object?[] { 2.5, 4.0 }), ("spr", new object?[] { 120m, 310m }), ("name", new object?[] { "Acme", "Globex" }));
        var reading = ChartSpec.Read("""
            { "version": 1, "axis": { "values": "numbers", "title": "Leverage" },
              "series": [ { "id": "n", "name": "Names", "kind": "observation", "data": "credit", "x": "lev", "y": "spr", "label": "name" } ] }
            """, h => table);
        Assert.Equal(["Acme", "Globex"], reading.Definition.Series[0].Points.Select(p => p.Name));
    }

    [Fact] // ADR-0320: panels and the right axis are said by name
    public void Panels_and_sides_are_read()
    {
        var reading = Read(Level.Replace("\"fromZero\": false", "\"fromZero\": false, \"panel\": \"Rates\", \"axis\": \"right\""));
        Assert.Equal("Rates", reading.Definition.Series[0].Panel);
        Assert.Equal(AxisSide.Right, reading.Definition.Series[0].Axis);
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
        Assert.Equal(["axis", "series", "version"], root.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).Order());
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

public class ChartSpecScaleTests
{
    private static readonly ChartData Credit = ChartData.Of(
        ("lev", new object?[] { 2.5, 4.0, 3.0 }), ("spr", new object?[] { 120m, 310m, 200m }),
        ("pnl", new object?[] { -1m, 10m, 0m }), ("notional", new object?[] { 5m, 20m, 10m }));

    private static string Spec(string scales) => $$"""
        { "version": 1, "axis": { "values": "numbers" },
          "series": [ { "id": "n", "name": "Names", "kind": "observation", "data": "credit", "x": "lev", "y": "spr"{{scales}} } ] }
        """;

    private static ChartSeries Read(string scales) => ChartSpec.Read(Spec(scales), h => Credit).Definition.Series[0];

    private static string Refusal(string scales) => Assert.Throws<ChartSpecRefusedException>(() => ChartSpec.Read(Spec(scales), h => Credit)).Message;

    [Fact] // ADR-0320: colour and size are read from columns, never written as values
    public void Colour_and_size_are_read_from_columns()
    {
        var s = Read(""", "colour": { "column": "pnl", "scale": "diverging", "centre": 0, "title": "P&L" }, "size": { "column": "notional" }""");
        Assert.Equal("P&L", s.ColourBy!.Title);
        Assert.Equal(0, s.ColourBy.Centre);
        Assert.Equal("10", s.ColourBy.Values[1]!.Value.ToInvariantString());
        Assert.Equal("notional", s.SizeBy!.Title);
    }

    [Fact] // ADR-0310: a diverging scale says its centre, and a sequential one has none
    public void A_diverging_scale_needs_its_centre()
    {
        Assert.StartsWith("series[0].colour.centre is missing", Refusal(""", "colour": { "column": "pnl", "scale": "diverging" }"""));
        Assert.StartsWith("series[0].colour.centre belongs to a diverging scale", Refusal(""", "colour": { "column": "pnl", "scale": "sequential", "centre": 0 }"""));
    }

    [Fact] // ADR-0320: a colour value is not part of a spec
    public void A_colour_value_is_refused() =>
        Assert.StartsWith("series[0].colour.value is not part of a Chart Spec", Refusal(""", "colour": { "column": "pnl", "scale": "sequential", "value": "#ff0000" }"""));
}

public class ChartSpecEdgeTests
{
    private static readonly ChartData Nodes = ChartData.Of(
        ("id", new object?[] { 0, 1, 2 }), ("step", new object?[] { 0, 1, 1 }), ("price", new object?[] { 100m, 90m, 110m }));

    private static readonly ChartData Links = ChartData.Of(("from", new object?[] { "0", "0" }), ("to", new object?[] { "1", "2" }));

    private static IChartData? Tables(string h) => h switch { "nodes" => Nodes, "links" => Links, _ => null };

    private const string Spec = """
        { "version": 1, "axis": { "values": "numbers", "title": "Step" },
          "series": [ { "id": "t", "name": "Tree", "kind": "observation", "data": "nodes", "x": "step", "y": "price", "key": "id",
                        "edges": { "data": "links", "from": "from", "to": "to" } } ] }
        """;

    [Fact] // ADR-0321: the nodes and the edges are two tables; keys meet as text
    public void Nodes_and_edges_are_read_from_two_tables()
    {
        var s = ChartSpec.Read(Spec, Tables).Definition.Series[0];
        Assert.Equal(2, s.Edges.Count);
        Assert.Equal("0", s.Points[0].Row);
        Assert.Equal(new ChartEdge("0", "2"), s.Edges[1]);
    }

    [Fact] // ADR-0321: edges need the nodes' keys
    public void Edges_without_node_keys_are_refused() =>
        Assert.StartsWith("series[0].key is missing", Assert.Throws<ChartSpecRefusedException>(() => ChartSpec.Read(Spec.Replace("\"key\": \"id\",", ""), Tables)).Message);

    [Fact] // ADR-0321: an edge to a missing node comes back with the series' path
    public void An_edge_to_a_missing_node_names_the_series()
    {
        var bad = ChartData.Of(("from", new object?[] { "0" }), ("to", new object?[] { "7" }));
        var m = Assert.Throws<ChartSpecRefusedException>(() => ChartSpec.Read(Spec, h => h == "links" ? bad : Tables(h))).Message;
        Assert.StartsWith("series[0]: ", m);
        Assert.Contains("no node is keyed 7", m);
    }
}
