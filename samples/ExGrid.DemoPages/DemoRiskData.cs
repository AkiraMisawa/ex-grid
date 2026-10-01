using System.Globalization;
using global::ExPivot.Engine;

namespace ExGrid.DemoPages;

/// <summary>
/// One rate-delta position as the /pivot-risk page pivots it: a book's PV01 on one curve at one
/// tenor, in US dollars per basis point. Invented from a fixed seed: the desks and books are made
/// up, and the curves are named after public rate indices, not after anyone's.
/// </summary>
public sealed record DemoRiskPosition(string Id, string Desk, string Curve, string Book, string Tenor, decimal Pv01);

/// <summary>The /pivot-risk page's positions, fields and layout.</summary>
public static class DemoRiskData
{
    /// <summary>Every tenor the positions are bucketed at, in maturity order. The options desk's
    /// system writes eighteen months as <c>1Y6M</c>, the others as <c>18M</c>.</summary>
    public static readonly IReadOnlyList<string> TenorBuckets =
        ["ON", "TN", "1W", "1M", "3M", "6M", "9M", "1Y", "18M", "2Y", "3Y", "5Y", "7Y", "10Y", "15Y", "20Y", "30Y"];

    private static readonly (string Desk, string[] Curves, string[] Books)[] Desks =
    [
        ("Rates Flow", ["USD SOFR", "EUR ESTR", "GBP SONIA"], ["RFL-NY-01", "RFL-LDN-02"]),
        ("Rates Options", ["USD SOFR", "EUR EURIBOR 6M"], ["ROP-NY-01", "ROP-LDN-01"]),
        ("Treasury", ["USD SOFR", "EUR ESTR", "JPY TONA"], ["TSY-LDN-01", "TSY-TKY-01"]),
    ];

    /// <summary>The positions, from a fixed seed, so every run — and every browser test — pivots the
    /// same numbers. PV01s are whole dollars, so every total is exact as painted.</summary>
    public static readonly IReadOnlyList<DemoRiskPosition> Positions = Make();

    private static DemoRiskPosition[] Make()
    {
        var random = new Random(20261001);
        var positions = new List<DemoRiskPosition>();
        foreach (var (desk, curves, books) in Desks)
        {
            foreach (var curve in curves)
            {
                for (var t = 0; t < TenorBuckets.Count; t++)
                {
                    var tenor = TenorBuckets[t];
                    // Only the treasury funds overnight. Any bucket but the overnight ones and
                    // eighteen months is left empty now and then, and so is a book.
                    if (t < 2 && desk != "Treasury")
                        continue;
                    if (tenor is not ("ON" or "TN" or "18M") && random.Next(6) == 0)
                        continue;
                    if (tenor == "18M" && desk == "Rates Options")
                        tenor = "1Y6M";
                    var booked = books.Where(_ => random.Next(3) != 0).ToList();
                    if (booked.Count == 0)
                        booked.Add(books[random.Next(books.Length)]);
                    foreach (var book in booked)
                    {
                        // Longer tenors carry more PV01 per notional: a rough scale, whole dollars.
                        var scale = 200 + (t * t * 60);
                        var pv01 = (decimal)(random.Next(-scale, scale + 1) * 10);
                        positions.Add(new DemoRiskPosition(
                            "P" + (1001 + positions.Count).ToString(CultureInfo.InvariantCulture),
                            desk, curve, book, tenor, pv01));
                    }
                }
            }
        }
        return [.. positions];
    }

    #region The code: fields
    // The tenor carries its Order Key: Items are ordered by Tenors.Months, called once per Item.
    public static readonly PivotFields<DemoRiskPosition> Fields = PivotFields.Of<DemoRiskPosition>()
        .Key("Id", p => p.Id)
        .Text("Desk", p => p.Desk)
        .Text("Curve", p => p.Curve)
        .Text("Book", p => p.Book)
        .Text("Tenor", p => p.Tenor, orderKey: Tenors.Months)          // ON, TN, 1W … 1Y6M … 30Y
        .Number("Pv01", p => p.Pv01, caption: "PV01 (USD)", format: "#,##0");

    // Desks and curves in Rows, tenors in Columns.
    public static PivotLayout Layout() => new()
    {
        Rows = [new PivotFieldPlacement("Desk"), new PivotFieldPlacement("Curve")],
        Columns = [new PivotFieldPlacement("Tenor")],
        Values = [new PivotValueField("Pv01", PivotAggregation.Sum) { NumberFormat = "#,##0" }],
    };
    #endregion

    /// <summary>The same fields with no Order Key on the tenor, whose Items then fall back to the
    /// order of their labels: what the key is there to prevent.</summary>
    public static readonly PivotFields<DemoRiskPosition> FieldsWithoutKey = PivotFields.Of<DemoRiskPosition>()
        .Key("Id", p => p.Id)
        .Text("Desk", p => p.Desk)
        .Text("Curve", p => p.Curve)
        .Text("Book", p => p.Book)
        .Text("Tenor", p => p.Tenor)
        .Number("Pv01", p => p.Pv01, caption: "PV01 (USD)", format: "#,##0");
}

#region The code: tenors
static class Tenors
{
    // Months to maturity: "1Y6M" is 18, as "18M" is, and the two stand side by side.
    public static IComparable? Months(string tenor)
    {
        if (tenor is "ON" or "TN")
            return tenor == "ON" ? -2m : -1m;
        decimal months = 0, number = 0;
        foreach (var c in tenor)
        {
            if (char.IsAsciiDigit(c))
            {
                number = (number * 10) + (c - '0');
                continue;
            }
            var unit = c switch { 'W' => 0.25m, 'M' => 1m, 'Y' => 12m, _ => 0m };
            if (unit == 0 || number == 0)
                return null;   // not a tenor: no key, so after the tenors
            months += number * unit;
            number = 0;
        }
        return number == 0 && months > 0 ? months : null;
    }
}
#endregion
