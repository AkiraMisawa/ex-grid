namespace ExChart.Showcases;

/// <summary>One node of a Cox–Ross–Rubinstein lattice: step i, up-moves j, the underlying there and the option's value.</summary>
/// <param name="Step">The step i, from 0.</param>
/// <param name="Up">The up-moves j taken in those steps.</param>
/// <param name="Spot">The underlying after them.</param>
/// <param name="Value">The option's value at the node.</param>
/// <param name="Exercise">What exercising there pays.</param>
/// <param name="Early">Whether an American option is exercised there before maturity.</param>
public sealed record LatticeNode(int Step, int Up, double Spot, double Value, double Exercise, bool Early)
{
    /// <summary>Net up-moves, 2j − i: the node's height in the lattice.</summary>
    public int Net => 2 * Up - Step;

    /// <summary>The node's key, i:j.</summary>
    public string Key => $"{Step}:{Up}";
}

/// <summary>An option's terms.</summary>
/// <param name="Spot">The underlying today, S₀.</param>
/// <param name="Strike">The strike, K.</param>
/// <param name="Rate">The continuously compounded risk-free rate, r.</param>
/// <param name="Volatility">The volatility, σ.</param>
/// <param name="Maturity">The time to maturity in years, T.</param>
/// <param name="Put">A put; otherwise a call.</param>
/// <param name="American">Exercisable at any node; otherwise only at maturity.</param>
public sealed record OptionTerms(double Spot, double Strike, double Rate, double Volatility, double Maturity, bool Put, bool American);

/// <summary>The page's pricing: a CRR lattice and Black–Scholes. The page computes; the grid and the charts only show.</summary>
public static class BinomialLattice
{
    /// <summary>Prices <paramref name="o"/> on an <paramref name="n"/>-step lattice, returning the price and every node.</summary>
    public static (double Price, LatticeNode[] Nodes) Price(OptionTerms o, int n)
    {
        var dt = o.Maturity / n;
        var u = Math.Exp(o.Volatility * Math.Sqrt(dt));
        var d = 1 / u;
        var p = (Math.Exp(o.Rate * dt) - d) / (u - d);
        var disc = Math.Exp(-o.Rate * dt);
        double Payoff(double s) => Math.Max(o.Put ? o.Strike - s : s - o.Strike, 0);
        double Spot(int i, int j) => o.Spot * Math.Pow(u, j) * Math.Pow(d, i - j);
        static int Index(int i, int j) => i * (i + 1) / 2 + j;

        var nodes = new LatticeNode[(n + 1) * (n + 2) / 2];
        var v = new double[n + 1];
        for (var j = 0; j <= n; j++)
        {
            v[j] = Payoff(Spot(n, j));
            nodes[Index(n, j)] = new LatticeNode(n, j, Spot(n, j), v[j], v[j], false);
        }
        for (var i = n - 1; i >= 0; i--)
            for (var j = 0; j <= i; j++)
            {
                var hold = disc * (p * v[j + 1] + (1 - p) * v[j]);
                var exercise = Payoff(Spot(i, j));
                var early = o.American && exercise > hold;
                v[j] = early ? exercise : hold;
                nodes[Index(i, j)] = new LatticeNode(i, j, Spot(i, j), v[j], exercise, early);
            }
        return (v[0], nodes);
    }

    /// <summary>The Black–Scholes price of the European option on the same terms.</summary>
    public static double BlackScholes(OptionTerms o)
    {
        var sd = o.Volatility * Math.Sqrt(o.Maturity);
        var d1 = (Math.Log(o.Spot / o.Strike) + (o.Rate + o.Volatility * o.Volatility / 2) * o.Maturity) / sd;
        var d2 = d1 - sd;
        var df = Math.Exp(-o.Rate * o.Maturity);
        return o.Put ? o.Strike * df * Cdf(-d2) - o.Spot * Cdf(-d1) : o.Spot * Cdf(d1) - o.Strike * df * Cdf(d2);
    }

    /// <summary>Delta, gamma, vega per volatility point and theta per day, read from an <paramref name="n"/>-step lattice.</summary>
    public static (double Delta, double Gamma, double Vega, double Theta) Greeks(OptionTerms o, int n)
    {
        var (_, nodes) = Price(o, n);
        LatticeNode At(int i, int j) => nodes[i * (i + 1) / 2 + j];
        double Slope(LatticeNode a, LatticeNode b) => (b.Value - a.Value) / (b.Spot - a.Spot);
        var delta = Slope(At(1, 0), At(1, 1));
        var gamma = (Slope(At(2, 1), At(2, 2)) - Slope(At(2, 0), At(2, 1))) / ((At(2, 2).Spot - At(2, 0).Spot) / 2);
        var theta = (At(2, 1).Value - At(0, 0).Value) / (2 * o.Maturity / n) / 365;
        var vega = (Price(o with { Volatility = o.Volatility + 0.01 }, n).Price - Price(o with { Volatility = o.Volatility - 0.01 }, n).Price) / 2;
        return (delta, gamma, vega, theta);
    }

    // Abramowitz–Stegun 26.2.17, |error| < 7.5e-8.
    private static double Cdf(double x)
    {
        var t = 1 / (1 + 0.2316419 * Math.Abs(x));
        var poly = t * (0.319381530 + t * (-0.356563782 + t * (1.781477937 + t * (-1.821255978 + t * 1.330274429))));
        var tail = Math.Exp(-x * x / 2) / Math.Sqrt(2 * Math.PI) * poly;
        return x >= 0 ? 1 - tail : tail;
    }
}
