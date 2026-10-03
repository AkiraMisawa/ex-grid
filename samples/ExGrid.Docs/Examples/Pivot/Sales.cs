using ExPivot.Engine;

namespace ExGrid.Docs.Examples.Pivot;

/// <summary>One invented sale: a line of an order, as the ExPivot Examples pivot it.</summary>
public sealed record Sale(
    long Id,
    string Region,
    string Country,
    string Category,
    string Product,
    string Channel,
    DateOnly Date,
    int Units,
    decimal Revenue,
    decimal Cost)
{
    /// <summary>What the sale earned over its cost.</summary>
    public decimal Profit => Revenue - Cost;
}

/// <summary>The sales, and the Pivot Fields every ExPivot Example declares over them.</summary>
public static class Sales
{
    // Each field once, with a typed accessor: the declaration makes both the column the sales are
    // read into and the Pivot Field over it. Year, Quarter and Month are parts of the date.
    public static readonly PivotFields<Sale> Fields = PivotFields.Of<Sale>()
        .Key("Id", s => s.Id)                                    // the Record Key, not offered as a field
        .Text("Region", s => s.Region)
        .Text("Country", s => s.Country)
        .Text("Category", s => s.Category)
        .Text("Product", s => s.Product)
        .Text("Channel", s => s.Channel, itemOrder: ["Online", "Retail", "Partner", "Direct"])
        .Date("Date", s => s.Date, format: "yyyy-MM-dd")
        .Year("Year", of: "Date")
        .Quarter("Quarter", of: "Date")
        .Month("Month", of: "Date")
        .Number("Units", s => s.Units)                           // int: an Integer column
        .Number("Revenue", s => s.Revenue, format: "#,##0.00")   // decimal: summed exactly
        .Number("Cost", s => s.Cost, format: "#,##0.00")
        .Number("Profit", s => s.Profit, format: "#,##0.00");

    private static readonly (string Region, (string Country, int Weight)[] Countries)[] Regions =
    [
        ("North America", [("United States", 30), ("Canada", 6), ("Mexico", 4)]),
        ("Europe", [("Germany", 9), ("United Kingdom", 8), ("France", 7), ("Italy", 4), ("Spain", 4), ("Netherlands", 3)]),
        ("Asia Pacific", [("Japan", 8), ("Australia", 5), ("South Korea", 4), ("Singapore", 2), ("India", 3)]),
        ("Latin America", [("Brazil", 5), ("Chile", 2), ("Colombia", 2)]),
    ];

    private static readonly (string Category, (string Product, decimal Price)[] Products)[] Catalog =
    [
        ("Computers", [("Laptop Pro 14", 1899m), ("Laptop Air 13", 1199m), ("Desktop Tower", 1499m), ("Mini PC", 649m)]),
        ("Displays", [("27\" 4K Monitor", 429m), ("34\" Ultrawide", 749m), ("Portable Monitor", 249m)]),
        ("Accessories", [("Wireless Mouse", 49m), ("Mechanical Keyboard", 129m), ("USB-C Dock", 199m), ("Webcam HD", 89m)]),
        ("Audio", [("Noise-Cancelling Headphones", 299m), ("Wireless Earbuds", 179m), ("Conference Speaker", 349m)]),
    ];

    // A channel's share of the sales, the most units a line of it carries, and its discount.
    private static readonly (string Channel, int Weight, int MaxUnits, decimal Discount)[] Channels =
    [
        ("Online", 40, 4, 0.00m),
        ("Retail", 30, 6, 0.04m),
        ("Partner", 20, 40, 0.12m),
        ("Direct", 10, 120, 0.18m),
    ];

    private static readonly DateOnly First = new(2025, 1, 1);
    private static readonly int Days = new DateOnly(2026, 9, 30).DayNumber - First.DayNumber + 1;

    /// <summary><paramref name="count"/> invented sales from January 2025 to September 2026, the
    /// same on every call: they come from a fixed seed.</summary>
    public static Sale[] Sample(int count = 20_000)
    {
        var random = new Random(20261003);
        var countries = Regions.SelectMany(r => r.Countries.Select(c => (r.Region, c.Country, c.Weight))).ToArray();
        var sales = new Sale[count];
        for (var i = 0; i < count; i++)
        {
            var (region, country, _) = Pick(random, countries, c => c.Weight);
            var (category, products) = Catalog[random.Next(Catalog.Length)];
            var (product, price) = products[random.Next(products.Length)];
            var (channel, _, maxUnits, discount) = Pick(random, Channels, c => c.Weight);
            var date = First.AddDays(Day(random));
            var units = random.Next(1, maxUnits + 1);
            var revenue = Math.Round(units * price * (1 - discount - (decimal)(random.NextDouble() * 0.05)), 2);
            var cost = Math.Round(units * price * (0.52m + (decimal)(random.NextDouble() * 0.16)), 2);
            sales[i] = new Sale(100_001 + i, region, country, category, product, channel, date, units, revenue, cost);
        }
        return sales;
    }

    /// <summary>Amends <paramref name="count"/> of <paramref name="sales"/> in place, as an order
    /// system would — units changed, revenue and cost with them — and returns the amended ones.</summary>
    public static Sale[] Amend(Sale[] sales, Random random, int count)
    {
        var amended = new Dictionary<int, Sale>();
        while (amended.Count < count)
        {
            var at = random.Next(sales.Length);
            var sale = sales[at];
            var units = Math.Max(1, sale.Units + random.Next(-2, 4));
            var scale = (decimal)units / sale.Units;
            amended[at] = sales[at] = sale with
            {
                Units = units,
                Revenue = Math.Round(sale.Revenue * scale, 2),
                Cost = Math.Round(sale.Cost * scale, 2),
            };
        }
        return [.. amended.Values];
    }

    // A day, more of them later on (the business grows) and in the last quarter of a year.
    private static int Day(Random random)
    {
        while (true)
        {
            var day = random.Next(Days);
            var month = First.AddDays(day).Month;
            var weight = (0.6 + (0.4 * day / Days)) * (month >= 10 ? 1.3 : 1.0);
            if (random.NextDouble() * 1.3 < weight)
                return day;
        }
    }

    private static T Pick<T>(Random random, T[] items, Func<T, int> weight)
    {
        var at = random.Next(items.Sum(weight));
        foreach (var item in items)
        {
            at -= weight(item);
            if (at < 0)
                return item;
        }
        return items[^1];
    }
}
