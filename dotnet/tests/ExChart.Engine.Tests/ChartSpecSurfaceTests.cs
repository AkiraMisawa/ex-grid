using ExChart.Engine;

namespace ExChart.Engine.Tests;

/// <summary>
/// Ticket 77: a 3D surface and a series' intervals said in a Chart Spec (ADR-0320). What the reader reads and
/// refuses is shared with @ex-grid/chart as tests/cases/chart/spec-surface.json (ADR-0370, sections 2 and 3);
/// what stays here is what a case cannot say: printing, and Chart Sources. What the tool answers is said by the cases too.
/// </summary>
public class ChartSpecSurfaceTests
{
    // An FX volatility grid as a capability would return it: a row a tenor and delta, one hole.
    private static readonly ChartData Vols = ChartData.Of(
        ("tenor", new object?[] { "1W", "1W", "1M", "1M", "1Y", "1Y" }),
        ("delta", new object?[] { 25, 50, 25, 50, 25, 50 }),
        ("vol", new object?[] { 8.1m, 7.9m, 8.4m, null, 9.0m, 8.7m }),
        ("filled", new object?[] { false, true, false, false, false, true }));

    private static IChartData? Tables(string h) => h switch
    {
        "vols" => Vols,
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

    [Fact] // ADR-0344: a surface needs every point, which a Chart Source does not thin
    public async Task A_surface_is_not_read_through_a_chart_source()
    {
        var e = await Assert.ThrowsAsync<ChartSpecRefusedException>(() => ChartSpec.ReadAsync(Grid, _ => ChartSource.Over(Vols), 100, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("surface", e.Path);
    }
}
