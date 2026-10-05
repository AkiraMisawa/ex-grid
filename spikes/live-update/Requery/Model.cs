using ExGrid;

namespace Requery;

/// <summary>A row as a live screen holds it: immutable, so a change is a new instance (ADR-0003).
/// A class, not a record, so that two value-equal rows are still two rows.</summary>
public sealed class Trade(int id, string? book, string? desk, decimal? pnl, decimal? notional, DateTime? tradeDate, bool? confirmed)
{
    public int Id { get; } = id;
    public string? Book { get; } = book;
    public string? Desk { get; } = desk;
    public decimal? Pnl { get; } = pnl;
    public decimal? Notional { get; } = notional;
    public DateTime? TradeDate { get; } = tradeDate;
    public bool? Confirmed { get; } = confirmed;

    public Trade With(string? book = null, string? desk = null, decimal? pnl = null, decimal? notional = null,
        DateTime? tradeDate = null, bool? confirmed = null, bool pnlBlank = false)
        => new(Id, book ?? Book, desk ?? Desk, pnlBlank ? null : pnl ?? Pnl, notional ?? Notional, tradeDate ?? TradeDate, confirmed ?? Confirmed);

    public override string ToString() => $"#{Id} {Book}/{Desk} pnl={Pnl} n={Notional} d={TradeDate:yyyy-MM-dd} c={Confirmed}";
}

public static class TradeColumns
{
    // The accessor delegates are made once, as a grid that caches its columns makes them.
    public static readonly IReadOnlyList<ColumnInfo<Trade>> All =
    [
        new("Id", ColumnType.Number, t => t.Id),
        new("Book", ColumnType.Text, t => t.Book),
        new("Desk", ColumnType.Text, t => t.Desk),
        new("Pnl", ColumnType.Number, t => t.Pnl),
        new("Notional", ColumnType.Number, t => t.Notional),
        new("TradeDate", ColumnType.Date, t => t.TradeDate),
        new("Confirmed", ColumnType.Boolean, t => t.Confirmed),
    ];
}

/// <summary>A Change Batch over rows, as candidate 3's source would take one: each replaced row as
/// the instance it replaces and the new instance, rows added (appended to the base) and rows
/// removed.</summary>
public sealed record RowBatch(
    IReadOnlyList<(Trade Old, Trade New)> Replaced,
    IReadOnlyList<Trade> Added,
    IReadOnlyList<Trade> Removed)
{
    public static RowBatch Replacing(IReadOnlyList<(Trade Old, Trade New)> replaced) => new(replaced, [], []);
}
