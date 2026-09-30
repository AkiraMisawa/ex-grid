using System.Globalization;
using Microsoft.Data.Sqlite;

namespace ExGrid.DemoApi;

/// <summary>
/// A trade as the API answers with it: <c>DemoPivotTrade</c>'s shape, with money as
/// <see cref="decimal"/>, exact to the cent, and a Blank currency as null.
/// </summary>
internal sealed record Trade(
    string TradeId,
    string Region,
    string Desk,
    string Book,
    string Product,
    string? Currency,
    DateOnly TradeDate,
    decimal Notional,
    decimal Pnl,
    int Quantity,
    bool Confirmed)
{
    /// <summary>Reads the current row of a query that selected <see cref="TradeDatabase.TradeColumns"/>.</summary>
    public static Trade Read(SqliteDataReader reader) => new(
        TradeId: reader.GetString(0),
        Region: reader.GetString(1),
        Desk: reader.GetString(2),
        Book: reader.GetString(3),
        Product: reader.GetString(4),
        Currency: reader.IsDBNull(5) ? null : reader.GetString(5),
        TradeDate: DateOnly.ParseExact(reader.GetString(6), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        Notional: Cents.ToDecimal(reader.GetInt64(7)),
        Pnl: Cents.ToDecimal(reader.GetInt64(8)),
        Quantity: reader.GetInt32(9),
        Confirmed: reader.GetInt64(10) != 0);
}

/// <summary>Money as the database stores it: a whole number of cents (ADR-0068).</summary>
internal static class Cents
{
    /// <summary>The amount, exactly, with two decimal places: 123456 cents is 1234.56. Never
    /// through a double, which would round it.</summary>
    public static decimal ToDecimal(long cents) => cents * 0.01m;
}
