namespace ExGrid.DemoApi;

/// <summary>
/// Where the server will serve its trades as an Arrow stream (ADR-0064; issue 18, blocked by
/// <c>ExGrid.Data</c> and <c>ExGrid.Data.Arrow</c>): the database read into a Snapshot through a
/// <c>DbDataReader</c> and written as Arrow, for <c>/pivot-db</c>'s "database → Snapshot". What it
/// is to be built on:
/// <list type="bullet">
/// <item>The read and the writing both happen inside one <see cref="TradeStore.ReadAsync{T}"/>, so
/// the stream holds one state of the data, and the Snapshot's version is that read's
/// <see cref="TradeRead.Version"/>.</item>
/// <item>SQLite hands back what it stores. <c>Notional</c> and <c>Pnl</c> are integer cents, to be
/// read as <c>decimal</c> — cents × 0.01, never through a double (<see cref="Cents"/>).
/// <c>TradeDate</c> is ISO text, to be read as a Date. <c>Confirmed</c> is 0 or 1, to be read as a
/// Boolean. A plain <c>SqliteDataReader</c> would hand the Snapshot's reader integers and text
/// for all four.</item>
/// <item>The stream is written uncompressed and HTTP compresses it (ADR-0064). The server does not
/// compress responses yet: add response compression with the endpoint.</item>
/// </list>
/// </summary>
internal static class SnapshotEndpoints
{
    /// <summary>Maps the Arrow stream's route. None yet.</summary>
    public static IEndpointRouteBuilder MapSnapshotEndpoints(this IEndpointRouteBuilder app) => app;
}
