using ExChart.Engine;

namespace ExGrid.Docs.Examples.Chart;

/// <summary>
/// A table too large to hand a chart whole: two hundred thousand ticks, as a capability would hold them,
/// read through a Chart Source that thins them to the plot's pixels. A spike at one tick stands for a bad print.
/// </summary>
public static class SourcedTicks
{
    /// <summary>How many ticks the table holds.</summary>
    public const int Count = 200_000;

    /// <summary>The ticks: the second each was printed at, and its price.</summary>
    public static readonly IChartData Table = Make();

    /// <summary>The page's source: the reference implementation over the table, held in the process.</summary>
    public static readonly IChartSource Source = ChartSource.Over(Table, version: 1);

    /// <summary>The host's lookup from a Data Handle to its source; null for a handle it does not know.</summary>
    public static IChartSource? Lookup(string handle) => handle == "ticks" ? Source : null;

    /// <summary>The spec, which names the table and its columns, never a number.</summary>
    public const string Spec = """
        {
          "version": 1,
          "axis": { "values": "numbers", "title": "Second" },
          "series": [ { "id": "price", "name": "Price", "kind": "level", "data": "ticks", "x": "second", "y": "price", "fromZero": false } ]
        }
        """;

    private static IChartData Make()
    {
        var random = new Random(7);
        var seconds = new object?[Count];
        var prices = new object?[Count];
        var price = 100.0;
        for (var i = 0; i < Count; i++)
        {
            price += (random.NextDouble() - 0.5) * 0.04;
            seconds[i] = (double)i;
            prices[i] = Math.Round(i == 141_421 ? price * 1.08 : price, 3);
        }
        return global::ExChart.Engine.ChartData.Of(("second", seconds), ("price", prices));
    }
}
