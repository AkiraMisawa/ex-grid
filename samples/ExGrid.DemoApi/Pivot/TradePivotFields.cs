using System.Globalization;
using ExPivot.Engine;
using Microsoft.Data.Sqlite;

namespace ExGrid.DemoApi;

/// <summary>
/// How a field's stored value reads as an Item and as a record's value, and how an Item is
/// written back into SQL to be compared with the stored values (ADR-0059, ADR-0065).
/// </summary>
internal enum TradeValueKind
{
    /// <summary>Text, told apart ignoring case, as a pivot's Items are; NULL is a Blank.</summary>
    Text,

    /// <summary>Money stored as integer cents: a number Item, and an exact <see cref="decimal"/>.</summary>
    Cents,

    /// <summary>An integer: a number Item.</summary>
    Integer,

    /// <summary>A date stored as ISO text, <c>yyyy-MM-dd</c>: a date Item at midnight.</summary>
    IsoDate,

    /// <summary>0 or 1: a Boolean Item.</summary>
    Boolean,
}

/// <summary>
/// One Pivot Field of the trades as the server offers it: what the Field List shows
/// (<see cref="Field"/>), the SQL expression that reads it from <c>trades</c>, and how its stored
/// values read (<see cref="Kind"/>).
/// </summary>
/// <param name="Field">The name, type, caption and format the pages see.</param>
/// <param name="Sql">The SQL expression over <c>trades</c>: a column, or a computation of one.</param>
/// <param name="Kind">How a stored value reads as an Item, and an Item as a stored value.</param>
internal sealed record TradePivotField(PivotField Field, string Sql, TradeValueKind Kind)
{
    /// <summary>The field's name, as a question names it.</summary>
    public string Name => Field.Name;

    /// <summary>Whether every value of the field is a number, which Sum, Min and Max read. A text,
    /// date or Boolean value is counted and is never a number (ADR-0059).</summary>
    public bool IsNumber => Kind is TradeValueKind.Cents or TradeValueKind.Integer;

    /// <summary>The Item a stored value belongs to (ADR-0059): read from column
    /// <paramref name="ordinal"/> of <paramref name="reader"/>, which selected <see cref="Sql"/>.</summary>
    public PivotItemKey ItemAt(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return PivotItemKey.Blank;
        return Kind switch
        {
            TradeValueKind.Text => PivotItemKey.Text(reader.GetString(ordinal)),
            TradeValueKind.Cents => PivotItemKey.Number((double)Cents.ToDecimal(reader.GetInt64(ordinal))),
            TradeValueKind.Integer => PivotItemKey.Number(reader.GetInt64(ordinal)),
            TradeValueKind.IsoDate => PivotItemKey.Date(TradeValues.ParseDate(reader.GetString(ordinal))),
            TradeValueKind.Boolean => PivotItemKey.Boolean(reader.GetInt64(ordinal) != 0),
            _ => throw new InvalidOperationException($"Unknown kind {Kind}."),
        };
    }

    /// <summary>A stored value as a record behind a cell carries it (<see cref="PivotDetailRecord"/>):
    /// text, an exact <see cref="decimal"/>, a date at midnight, a Boolean, or null for a Blank.</summary>
    public object? ValueAt(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return null;
        return Kind switch
        {
            TradeValueKind.Text => reader.GetString(ordinal),
            TradeValueKind.Cents => Cents.ToDecimal(reader.GetInt64(ordinal)),
            TradeValueKind.Integer => (decimal)reader.GetInt64(ordinal),
            TradeValueKind.IsoDate => TradeValues.ParseDate(reader.GetString(ordinal)),
            TradeValueKind.Boolean => reader.GetInt64(ordinal) != 0,
            _ => throw new InvalidOperationException($"Unknown kind {Kind}."),
        };
    }

    /// <summary>
    /// The stored value that is <paramref name="item"/>, for a <c>WHERE</c> to compare with: false
    /// when no stored value of this field can be that Item — a text Item of a number field, a
    /// date with a time of day, a number that is no whole cent — so a condition on it matches no
    /// record, as the engine's does (ADR-0059). A Blank is <see langword="null"/>: <c>IS NULL</c>.
    /// </summary>
    public bool TryStored(PivotItemKey item, out object? stored)
    {
        stored = null;
        if (item.Kind == PivotItemKind.Blank)
            return true;
        switch (Kind, item.Kind)
        {
            case (TradeValueKind.Text, PivotItemKind.Text):
                stored = item.Value!;
                return true;
            case (TradeValueKind.Cents, PivotItemKind.Number):
                if (TradeValues.CentsOf(Number(item)) is not { } cents)
                    return false;
                stored = cents;
                return true;
            case (TradeValueKind.Integer, PivotItemKind.Number):
                var number = Number(item);
                if (number != Math.Floor(number) || number < long.MinValue || number >= 9.2233720368547758E18)
                    return false;
                stored = (long)number;
                return true;
            case (TradeValueKind.IsoDate, PivotItemKind.Date):
                var date = DateTime.ParseExact(item.Value!, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);
                if (date.TimeOfDay != TimeSpan.Zero)
                    return false;
                stored = TradeDatabase.FormatDate(DateOnly.FromDateTime(date));
                return true;
            case (TradeValueKind.Boolean, PivotItemKind.Boolean):
                stored = item.Value == "TRUE" ? 1L : 0L;
                return true;
            default:
                return false;
        }
    }

    private static double Number(PivotItemKey item) => double.Parse(item.Value!, NumberStyles.Float, CultureInfo.InvariantCulture);
}

/// <summary>
/// The trades as the server's Pivot Source offers them (ADR-0065, ADR-0068): the fields the pages
/// declare — the names, captions, types and formats of <c>DemoPivotData</c>'s, and the Record Key —
/// and the Aggregations SQLite answers exactly.
/// </summary>
internal static class TradePivotFields
{
    /// <summary>The fields, in the order the Field List shows them and a record behind a cell
    /// carries its values.</summary>
    public static readonly IReadOnlyList<TradePivotField> All =
    [
        new(new PivotField("TradeId", PivotFieldType.Text, caption: "Trade ID"), "TradeId", TradeValueKind.Text),
        new(new PivotField("Region", PivotFieldType.Text), "Region", TradeValueKind.Text),
        new(new PivotField("Desk", PivotFieldType.Text), "Desk", TradeValueKind.Text),
        new(new PivotField("Book", PivotFieldType.Text), "Book", TradeValueKind.Text),
        new(new PivotField("Product", PivotFieldType.Text), "Product", TradeValueKind.Text),
        new(new PivotField("Currency", PivotFieldType.Text), "Currency", TradeValueKind.Text),
        // Excel groups dates by month through a grouping this first version leaves out, so the
        // month is a text Item of its own, as DemoPivotData's is: 2026-01.
        new(new PivotField("Month", PivotFieldType.Text), "substr(TradeDate, 1, 7)", TradeValueKind.Text),
        new(new PivotField("TradeDate", PivotFieldType.Date, caption: "Trade date", format: "yyyy-MM-dd"), "TradeDate", TradeValueKind.IsoDate),
        new(new PivotField("Notional", PivotFieldType.Number, format: "#,##0.00"), "Notional", TradeValueKind.Cents),
        new(new PivotField("Pnl", PivotFieldType.Number, caption: "P&L", format: "#,##0.00"), "Pnl", TradeValueKind.Cents),
        new(new PivotField("Quantity", PivotFieldType.Number), "Quantity", TradeValueKind.Integer),
        new(new PivotField("Confirmed", PivotFieldType.Boolean), "Confirmed", TradeValueKind.Boolean),
    ];

    /// <summary>The fields as <see cref="PivotSource.Fields"/> offers them.</summary>
    public static readonly IReadOnlyList<PivotField> Fields = All.Select(f => f.Field).ToArray();

    /// <summary>
    /// What the source answers (ADR-0065). SQLite's <c>COUNT</c>, <c>SUM</c> over integer cents,
    /// <c>MIN</c> and <c>MAX</c> are exact, so Count, Count Numbers, Sum, Min and Max are offered,
    /// and Average, which ExPivot computes from the sum and the count. Product and the four
    /// variances are not: SQLite has no exact product, and its arithmetic for a running variance
    /// is not the engine's, so Value Field Settings… offers them disabled, with the reason. It can
    /// be refreshed: the data moves on while live updates run.
    /// </summary>
    public static readonly PivotSourceFeatures Features = new(
        [
            PivotAggregation.Sum, PivotAggregation.Count, PivotAggregation.Average,
            PivotAggregation.Max, PivotAggregation.Min, PivotAggregation.CountNumbers,
        ],
        canRefresh: true);

    private static readonly Dictionary<string, TradePivotField> ByName = All.ToDictionary(f => f.Name, StringComparer.Ordinal);

    /// <summary>The field named <paramref name="name"/>, matched ordinally as a layout names fields;
    /// null when the source has none.</summary>
    public static TradePivotField? Find(string name) => ByName.GetValueOrDefault(name);
}

/// <summary>Values as the database stores them, read and written exactly.</summary>
internal static class TradeValues
{
    /// <summary>An ISO date as the clock value of its midnight.</summary>
    public static DateTime ParseDate(string text) =>
        DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// The whole number of cents whose amount is the number Item <paramref name="number"/>, as the
    /// engine reads an amount — <c>(double)decimal</c> — or null when no amount of whole cents is
    /// that Item. The decimal nearest the double is found, and kept only when it converts back to
    /// the same double; two amounts of whole cents never share a double below 2⁵³ cents.
    /// </summary>
    public static long? CentsOf(double number)
    {
        if (!double.IsFinite(number) || Math.Abs(number) >= 9e13)
            return null;
        var amount = (decimal)number;
        var cents = amount * 100;
        if (cents != decimal.Truncate(cents))
            return null;
        var whole = (long)cents;
        return (double)Cents.ToDecimal(whole) == number ? whole : null;
    }
}
