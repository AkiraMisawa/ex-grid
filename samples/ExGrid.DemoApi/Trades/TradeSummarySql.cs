using ExGrid.Data;
using ExGrid.Summarizing;
using Microsoft.Data.Sqlite;

namespace ExGrid.DemoApi;

/// <summary>
/// A Selection Summary answered in SQL (ADR-0130): the server's <c>COUNT</c>, <c>SUM</c>,
/// <c>MIN</c> and <c>MAX</c> over the selected cells of the trades in <c>TradeId</c> order —
/// the order <c>/api/trades</c> pages in — merged into the family's one definition of the figures.
/// It is held to <see cref="GridSummary.Of{TRow}"/> over the same trades, question for question
/// (SM-8). Money is summed in whole cents, exactly, and read back as <see cref="decimal"/>.
/// </summary>
internal static class TradeSummarySql
{
    // The columns a grid over Trade shows, by name: the SQL column, and how a number reads back.
    // A column that is not a number — text, a date, a Boolean — is counted and never summed.
    private static readonly Dictionary<string, (string Column, bool IsNumber, decimal Scale)> Columns = new(StringComparer.Ordinal)
    {
        [nameof(Trade.TradeId)] = ("TradeId", false, 1m),
        [nameof(Trade.Region)] = ("Region", false, 1m),
        [nameof(Trade.Desk)] = ("Desk", false, 1m),
        [nameof(Trade.Book)] = ("Book", false, 1m),
        [nameof(Trade.Product)] = ("Product", false, 1m),
        [nameof(Trade.Currency)] = ("Currency", false, 1m),
        [nameof(Trade.TradeDate)] = ("TradeDate", false, 1m),
        [nameof(Trade.Notional)] = ("Notional", true, 0.01m),
        [nameof(Trade.Pnl)] = ("Pnl", true, 0.01m),
        [nameof(Trade.Quantity)] = ("Quantity", true, 1m),
        [nameof(Trade.Confirmed)] = ("Confirmed", false, 1m),
    };

    /// <summary>The figures <paramref name="request"/> asks for, read inside <paramref name="read"/>.</summary>
    public static async Task<GridSummaryResult> AnswerAsync(TradeRead read, GridSummaryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(request);
        var cells = new GridSummaryCells();
        var rows = (int)Math.Min(read.Trades, int.MaxValue);
        foreach (var (top, bottom, columns) in GridSummary.Bands(request.Ranges, rows, request.Columns.Count))
        {
            foreach (var position in columns)
            {
                if (!Columns.TryGetValue(request.Columns[position], out var column))
                    continue;
                await using var command = read.Command(
                    $"SELECT COUNT(v), SUM(v), MIN(v), MAX(v) FROM (SELECT {column.Column} AS v FROM trades ORDER BY TradeId LIMIT $count OFFSET $start)");
                command.Parameters.AddWithValue("$count", bottom - top);
                command.Parameters.AddWithValue("$start", top);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                await reader.ReadAsync(cancellationToken);
                var count = reader.GetInt64(0);
                if (!column.IsNumber || count == 0)
                {
                    cells.Merge(new AggregateCounts { Values = count }, default, default);
                    continue;
                }
                cells.Merge(
                    new AggregateCounts { Values = count, Numbers = count },
                    new AggregateSum { Exact = Read(reader, 1) * column.Scale },
                    new AggregateExtremes { ExactMin = Read(reader, 2) * column.Scale, ExactMax = Read(reader, 3) * column.Scale });
            }
        }
        return cells.Answer(request.Figures);
    }

    private static decimal Read(SqliteDataReader reader, int ordinal) => reader.GetInt64(ordinal);
}
