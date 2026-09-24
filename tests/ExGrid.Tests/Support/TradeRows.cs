namespace ExGrid.Tests;

/// <summary>
/// Sample Row Model for the semantics tests. Amount / TradedOn / Cleared are object so a
/// test can hand the accessor any runtime type and pin how the engine treats it.
/// </summary>
internal sealed record Trade(
    string? Book = null,
    object? Amount = null,
    object? TradedOn = null,
    object? Cleared = null);

internal static class TradeColumns
{
    public static readonly ColumnInfo<Trade> Book = new("Book", ColumnType.Text, t => t.Book);
    public static readonly ColumnInfo<Trade> Amount = new("Amount", ColumnType.Number, t => t.Amount);
    public static readonly ColumnInfo<Trade> TradedOn = new("TradedOn", ColumnType.Date, t => t.TradedOn);
    public static readonly ColumnInfo<Trade> Cleared = new("Cleared", ColumnType.Boolean, t => t.Cleared);

    public static readonly IReadOnlyList<ColumnInfo<Trade>> All = [Book, Amount, TradedOn, Cleared];

    public static GridFilter FilterOn(string column, params FilterClause[] clauses)
        => new(new Dictionary<string, FilterSpec> { [column] = new(clauses) });

    public static GridFilter FilterOnAny(string column, params FilterClause[] clauses)
        => new(new Dictionary<string, FilterSpec> { [column] = new(clauses, FilterCombinator.Or) });

    public static bool Matches(Trade row, GridFilter filter)
        => GridQueryEngine.Matches(row, All, filter);
}
