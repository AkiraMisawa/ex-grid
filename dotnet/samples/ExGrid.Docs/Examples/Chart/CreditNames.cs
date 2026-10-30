namespace ExGrid.Docs.Examples.Chart;

/// <summary>Forty reference names, each with its leverage and five-year CDS spread. Invented; DSTR is distressed.</summary>
public static class CreditNames
{
    /// <summary>One reference name: leverage in times, spread in basis points.</summary>
    public sealed record Name(string Ticker, double Leverage, double Spread);

    /// <summary>The names, in the grid's order.</summary>
    public static readonly Name[] All =
    [
        new("N001", 1.74, 48), new("N002", 2.19, 84), new("N003", 3.34, 141), new("N004", 4.90, 297),
        new("N005", 2.25, 74), new("N006", 2.06, 111), new("N007", 1.79, 59), new("N008", 1.92, 114),
        new("N009", 3.73, 213), new("N010", 1.81, 63), new("N011", 2.42, 106), new("N012", 2.06, 63),
        new("N013", 3.12, 155), new("N014", 1.35, 149), new("N015", 1.94, 96), new("N016", 1.72, 105),
        new("N017", 2.83, 95), new("N018", 1.74, 74), new("N019", 5.09, 143), new("N020", 3.58, 124),
        new("N021", 1.49, 58), new("N022", 3.26, 122), new("N023", 3.82, 209), new("N024", 3.93, 165),
        new("N025", 1.33, 61), new("N026", 4.76, 176), new("N027", 1.20, 78), new("N028", 2.60, 83),
        new("N029", 1.19, 58), new("N030", 1.94, 102), new("N031", 1.85, 43), new("N032", 3.48, 188),
        new("N033", 5.50, 183), new("N034", 3.12, 124), new("N035", 2.61, 148), new("N036", 1.71, 92),
        new("N037", 3.85, 172), new("N038", 1.49, 55), new("N039", 1.24, 46), new("DSTR", 5.60, 2400),
    ];
}
