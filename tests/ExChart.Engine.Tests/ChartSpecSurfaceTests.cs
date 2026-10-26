using ExChart.Engine;

namespace ExChart.Engine.Tests;

/// <summary>
/// Ticket 77: a 3D surface and a series' intervals said in a Chart Spec (ADR-0320). What the reader reads and
/// refuses is shared with @ex-grid/chart as tests/cases/chart/spec-surface.json (ADR-0370, sections 2 and 3);
/// what stays here is what a case cannot say: the answer in words, printing, and Chart Sources.
/// </summary>
public class ChartSpecSurfaceTests
{
    // An FX volatility grid as a capability would return it: a row a tenor and delta, one hole.
    private static readonly ChartData Vols = ChartData.Of(
        ("tenor", new object?[] { "1W", "1W", "1M", "1M", "1Y", "1Y" }),
        ("delta", new object?[] { 25, 50, 25, 50, 25, 50 }),
        ("vol", new object?[] { 8.1m, 7.9m, 8.4m, null, 9.0m, 8.7m }),
        ("filled", new object?[] { false, true, false, false, false, true }));

    private static readonly ChartData Quotes = ChartData.Of(
        ("x", new object?[] { 1.0, 2.0, 3.0 }),
        ("y", new object?[] { 10m, 12m, 11m }),
        ("lo", new object?[] { 9m, null, 10m }),
        ("hi", new object?[] { 11m, 13m, 12m }));

    private static IChartData? Tables(string h) => h switch
    {
        "vols" => Vols,
        "quotes" => Quotes,
        _ => null,
    };

    private const string Grid = """
        { "version": 1, "title": "EURUSD volatility",
          "surface": { "data": "vols", "x": "tenor", "y": "delta", "z": "vol", "xTitle": "Tenor", "zTitle": "Volatility" },
          "notes": ["Mid quotes."] }
        """;

    [Fact] // ADR-0274: a surface's reading prints what it holds, never the definition it has not
    public void A_surface_reading_prints_without_reading_its_definition() =>
        Assert.StartsWith("ChartSpecReading { Title = EURUSD volatility, Surface = ", ChartSpec.Read(Grid, Tables).ToString());

    [Fact] // ADR-0320: the tool answers a surface with the surface said in words
    public void A_surface_is_answered_in_words()
    {
        var answer = ChartSpec.Answer(Grid, Tables);
        Assert.True(answer.Accepted);
        Assert.StartsWith("EURUSD volatility, a 3D surface of Volatility over Tenor from 1W to 1Y", answer.Text);
        Assert.Contains("with 1 hole", answer.Text);
    }

    [Fact] // ADR-0344: a surface needs every point, which a Chart Source does not thin
    public async Task A_surface_is_not_read_through_a_chart_source()
    {
        var e = await Assert.ThrowsAsync<ChartSpecRefusedException>(() => ChartSpec.ReadAsync(Grid, _ => ChartSource.Over(Vols), 100, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("surface", e.Path);
    }

    [Fact] // ADR-0303, ADR-0320: the answer says a level's interval by its meaning
    public void A_levels_interval_is_said_in_the_answer()
    {
        const string spec = """
            { "version": 1, "axis": { "values": "numbers" },
              "series": [ { "id": "c", "name": "Curve", "kind": "level", "data": "quotes", "x": "x", "y": "y",
                            "interval": { "lower": "lo", "upper": "hi", "meaning": "Bid–Ask", "style": "band" } } ] }
            """;
        Assert.Contains("Bid–Ask", ChartSpec.Answer(spec, Tables).Text);
    }
}
