using ExChart.Engine;
using ExChart.Showcases;

namespace ExGrid.Docs.Examples.Chart;

/// <summary>
/// What a pricing capability would hand back as two Data Handles: an American put's lattice, a row a node,
/// and its branches, a row an edge. The Chart Spec names these tables and their columns, never a number.
/// </summary>
public static class LatticeTables
{
    /// <summary>The option the tables price: an American put, at the money, on eight steps.</summary>
    public static readonly OptionTerms Terms = new(Spot: 100, Strike: 100, Rate: 0.05, Volatility: 0.3, Maturity: 1, Put: true, American: true);

    /// <summary>The lattice's steps.</summary>
    public const int Steps = 8;

    /// <summary>The node table: node, step, spot, value, the risk-neutral chance of reaching it, and whether it is exercised early.</summary>
    public static readonly IChartData Nodes = MakeNodes();

    /// <summary>The branch table: each node to the two it moves to.</summary>
    public static readonly IChartData Branches = MakeBranches();

    /// <summary>The host's lookup from a Data Handle to its table; null for a handle it does not know.</summary>
    public static IChartData? Lookup(string handle) => handle switch
    {
        "lattice" => Nodes,
        "branches" => Branches,
        "fx-vols" => Vols,
        _ => null,
    };

    /// <summary>A third table: an FX volatility grid, a row a tenor and delta.</summary>
    public static readonly IChartData Vols = VolSurface.Table();

    private static IChartData MakeNodes()
    {
        var (_, nodes) = BinomialLattice.Price(Terms, Steps);
        var dt = Terms.Maturity / Steps;
        var u = Math.Exp(Terms.Volatility * Math.Sqrt(dt));
        var p = (Math.Exp(Terms.Rate * dt) - 1 / u) / (u - 1 / u);
        double Reach(LatticeNode n) => Choose(n.Step, n.Up) * Math.Pow(p, n.Up) * Math.Pow(1 - p, n.Step - n.Up);
        return global::ExChart.Engine.ChartData.Of(
            ("node", nodes.Select(n => n.Key).ToArray()),
            ("step", nodes.Select(n => n.Step).ToArray()),
            ("spot", nodes.Select(n => Math.Round(n.Spot, 2)).ToArray()),
            ("value", nodes.Select(n => Math.Round(n.Value, 2)).ToArray()),
            ("reach", nodes.Select(Reach).ToArray()),
            ("exercised", nodes.Select(n => n.Early ? "early" : "no").ToArray()));
    }

    private static IChartData MakeBranches()
    {
        var from = new List<string>();
        var to = new List<string>();
        for (var i = 0; i < Steps; i++)
            for (var j = 0; j <= i; j++)
            {
                from.Add($"{i}:{j}"); to.Add($"{i + 1}:{j}");
                from.Add($"{i}:{j}"); to.Add($"{i + 1}:{j + 1}");
            }
        return global::ExChart.Engine.ChartData.Of(("from", from.ToArray()), ("to", to.ToArray()));
    }

    private static double Choose(int n, int k)
    {
        double c = 1;
        for (var i = 1; i <= k; i++) c = c * (n - k + i) / i;
        return c;
    }

    /// <summary>The spec an AI would write for the lattice: colour by value, size by reach, the branches as edges.</summary>
    public const string LatticeSpec = """
        {
          "version": 1,
          "title": "American put on an 8-step lattice",
          "axis": { "values": "numbers", "title": "Step" },
          "series": [
            {
              "id": "nodes", "name": "Nodes", "kind": "observation",
              "data": "lattice", "x": "step", "y": "spot", "key": "node", "label": "node",
              "colour": { "column": "value", "scale": "sequential", "title": "Value" },
              "size": { "column": "reach", "title": "Chance of reaching" },
              "tooltip": ["exercised"],
              "edges": { "data": "branches", "from": "from", "to": "to" }
            }
          ],
          "notes": ["Cox–Ross–Rubinstein: S 100, K 100, r 5%, σ 30%, T 1 year."]
        }
        """;

    /// <summary>The spec of a 3D surface: the grid's x, y and z columns, its colours diverging from a centre.</summary>
    public const string SurfaceSpec = """
        {
          "version": 1,
          "title": "EURUSD volatility",
          "surface": {
            "data": "fx-vols", "x": "delta", "y": "tenor", "z": "vol",
            "xTitle": "Delta", "yTitle": "Tenor", "zTitle": "Volatility (%)",
            "centre": 10.5
          },
          "notes": ["Mid quotes; 25C at 5Y has none."]
        }
        """;

    /// <summary>A spec that writes its numbers in, as a model sometimes tries: refused, with the path and the reason.</summary>
    public const string NumbersSpec = """
        {
          "version": 1,
          "axis": { "values": "numbers" },
          "series": [
            { "id": "nodes", "name": "Nodes", "kind": "observation", "data": "lattice", "x": "step", "y": "spot",
              "values": [100, 109.1, 91.7] }
          ]
        }
        """;
}
