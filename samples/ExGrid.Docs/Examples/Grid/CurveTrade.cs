using ExGrid.Validation;

namespace ExGrid.Docs.Examples.Grid;

/// <summary>A trade discounted on its CSA's curve, for the rule over the whole collection (ADR-0381).</summary>
public sealed record CurveTrade(string Id, string Csa, string Counterparty, string Curve)
{
    /// <summary>Sixty trades over five CSAs. CSA ABC's trades stand far apart, so a breach reaches rows
    /// outside the Window.</summary>
    public static List<CurveTrade> Sample()
    {
        string[] csas = ["ABC", "DEF", "GHI", "JKL", "MNO"];
        string[] counterparties = ["Northwind Bank", "Contoso Capital", "Fabrikam Securities", "Tailspin Partners", "Adatum Trust"];
        string[] curves = ["USD-SOFR", "EUR-ESTR", "GBP-SONIA", "JPY-TONA", "USD-SOFR"];
        return [.. Enumerable.Range(0, 60).Select(i => new CurveTrade(
            $"T-{i + 1:0000}", csas[i % 5], counterparties[i % 5], curves[i % 5]))];
    }

    /// <summary>One discount curve per CSA: every trade of a CSA with two or more curves breaches, and the
    /// message names the other side by trade id, never by row position, which a sort changes.</summary>
    public static IEnumerable<Breach> OneCurvePerCsa(IReadOnlyList<CurveTrade> trades)
    {
        foreach (var csa in trades.GroupBy(t => t.Csa))
        {
            var curves = csa.GroupBy(t => t.Curve).OrderByDescending(g => g.Count()).ToList();
            if (curves.Count < 2)
                continue;
            var told = string.Join(", ", curves.Select(g => g.Count() > 3
                ? $"{g.Key} on {g.Count()} trades"
                : $"{g.Key} on {string.Join(" ", g.Select(t => t.Id))}"));
            yield return new Breach("one-curve-per-csa", $"CSA {csa.Key} has {curves.Count} discount curves: {told}.",
                [.. csa.Select(t => new BreachCell(t.Id, "Curve"))]);
        }
    }
}
