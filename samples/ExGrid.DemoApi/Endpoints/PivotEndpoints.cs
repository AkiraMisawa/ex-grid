namespace ExGrid.DemoApi;

/// <summary>
/// Where the server will answer as a Pivot Source (ADR-0065; issue 18, blocked by the Pivot
/// Source's contract in <c>ExPivot.Engine</c>, issue 10): the three questions <c>PivotJson</c>
/// carries — Aggregate, Items and Details — answered by SQL written by hand over <c>trades</c>
/// (<see cref="TradeDatabase.Schema"/>). What they are to be built on:
/// <list type="bullet">
/// <item>Each answer is computed inside one <see cref="TradeStore.ReadAsync{T}"/>, and carries that
/// read's <see cref="TradeRead.Version"/> as its Source Version.</item>
/// <item>Items and Details are asked under a version. Compare it with the read's own
/// <see cref="TradeRead.Version"/>, and refuse when they differ: the data has moved on.</item>
/// <item>Hidden Items become <c>WHERE</c>. Mind SQL's nulls: <c>Currency NOT IN ('EUR')</c> also drops
/// every Blank currency, so a Blank that is not hidden needs <c>Currency IS NULL OR …</c>, or
/// the totals come out quietly short.</item>
/// <item>Month is <c>substr(TradeDate, 1, 7)</c>. Details are pages:
/// <c>ORDER BY TradeId LIMIT … OFFSET …</c>.</item>
/// <item>Money is integer cents. <c>SUM</c> is exact, divided by 100 as a decimal. <c>AVG</c>
/// answers in floating point, so an Average is <c>SUM</c> over <c>COUNT</c>. The features leave
/// out what SQLite cannot answer exactly, such as Product and the variances.</item>
/// <item>PV-22 holds the answers to <c>PivotSource.From</c>'s over the same trades, in
/// <c>tests/ExGrid.DemoApi.Tests</c>.</item>
/// </list>
/// </summary>
internal static class PivotEndpoints
{
    /// <summary>Maps the Pivot Source's routes under <c>/api/pivot</c>. None yet.</summary>
    public static IEndpointRouteBuilder MapPivotEndpoints(this IEndpointRouteBuilder app) => app;
}
