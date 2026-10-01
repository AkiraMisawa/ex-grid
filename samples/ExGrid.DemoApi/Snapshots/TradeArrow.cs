using System.Diagnostics;
using ExGrid.Data;
using ExGrid.Data.Arrow;

namespace ExGrid.DemoApi;

/// <summary>The trades as an Arrow IPC stream, read at one Source Version.</summary>
/// <param name="Version">The Source Version of the read the stream was written from.</param>
/// <param name="Trades">How many trades it holds.</param>
/// <param name="Bytes">The stream, uncompressed (ADR-0064).</param>
internal sealed record TradeArrowStream(string Version, long Trades, byte[] Bytes);

/// <summary>
/// "Database → Snapshot" (ADR-0063, ADR-0064, ADR-0068): the trades read through a
/// <c>DbDataReader</c> into a Snapshot, under one read transaction, and written as Arrow's IPC
/// stream for a page to read into its own Snapshot. The bytes are kept for their Source Version,
/// so every page asking at one version is served the same bytes, built once; a live tick makes a
/// new version, and the next request builds again.
/// </summary>
internal sealed class TradeArrow(TradeStore store, ILogger<TradeArrow> logger)
{
    /// <summary>
    /// The trades as the Snapshot holds them, in <c>TradeId</c> order. <c>Month</c> is computed
    /// here, as the Pivot Source's is (<see cref="TradePivotFields"/>), so a pivot over the Snapshot
    /// can offer the same fields as the server's source.
    /// </summary>
    public const string Sql = """
        SELECT TradeId, Region, Desk, Book, Product, Currency, substr(TradeDate, 1, 7) AS Month,
               TradeDate, Notional, Pnl, Quantity, Confirmed
        FROM trades
        ORDER BY TradeId
        """;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile TradeArrowStream? _latest;
    private int _builds;

    /// <summary>How many times the stream has been built: once per Source Version asked for.</summary>
    public int Builds => Volatile.Read(ref _builds);

    /// <summary>
    /// The columns, each of its kind (ADR-0063). SQLite hands back what it stores, and the reader
    /// says so: the date is text, money is integer cents and the flag is 0 or 1, which a Snapshot
    /// would take as Text and Integer. Each is therefore declared with the conversion that reads it:
    /// cents as an exact decimal, never through a double; ISO text as a date; 0 and 1 as false and
    /// true. The other columns are read by their own types, and <c>TradeId</c> is the Record Key.
    /// The captions are the Pivot Fields', which a field over the Snapshot takes by default.
    /// </summary>
    public static SnapshotDataReaderBuilder Declaration() => new SnapshotDataReaderBuilder()
        .Column("TradeId", caption: "Trade ID")
        .Column("Region")
        .Column("Desk")
        .Column("Book")
        .Column("Product")
        .Column("Currency")
        .Column("Month")
        .Date<string>("TradeDate", text => TradeValues.ParseDate(text), caption: "Trade date")
        .Decimal<long>("Notional", cents => Cents.ToDecimal(cents))
        .Decimal<long>("Pnl", cents => Cents.ToDecimal(cents), caption: "P&L")
        .Column("Quantity")
        .Boolean<long>("Confirmed", flag => flag != 0)
        .Key("TradeId");

    /// <summary>The stream at the current Source Version: the one kept, or built now.</summary>
    public async Task<TradeArrowStream> GetAsync(CancellationToken cancellationToken)
    {
        if (_latest is { } latest && latest.Version == store.Version)
            return latest;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Another request may have built it while this one waited.
            if (_latest is { } built && built.Version == store.Version)
                return built;
            var stream = await store.ReadAsync((read, token) =>
                _latest is { } held && held.Version == read.Version ? Task.FromResult(held) : BuildAsync(read, token),
                cancellationToken);
            _latest = stream;
            return stream;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Reads the trades into a Snapshot and writes it, both inside the one read, so the stream holds
    // one state of the data and is that read's version.
    private async Task<TradeArrowStream> BuildAsync(TradeRead read, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        Snapshot snapshot;
        await using (var command = read.Command(Sql))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            snapshot = await Declaration().BuildAsync(reader, cancellationToken: cancellationToken);
        var readTime = Stopwatch.GetElapsedTime(started);

        using var buffer = new MemoryStream();
        await SnapshotArrow.WriteAsync(snapshot, buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var writeTime = Stopwatch.GetElapsedTime(started) - readTime;
        Interlocked.Increment(ref _builds);
        logger.LogInformation(
            "The trades' Arrow stream at version {Version}: {Trades} trades read into a Snapshot in {ReadMs:0} ms and written in {WriteMs:0} ms, {Megabytes:0.0} MB.",
            read.Version, snapshot.RowCount, readTime.TotalMilliseconds, writeTime.TotalMilliseconds, bytes.Length / 1_048_576.0);
        return new TradeArrowStream(read.Version, snapshot.RowCount, bytes);
    }
}
