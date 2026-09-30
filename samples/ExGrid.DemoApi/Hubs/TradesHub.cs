using Microsoft.AspNetCore.SignalR;

namespace ExGrid.DemoApi;

/// <summary>
/// What the hub at <c>/hubs/trades</c> says to every page connected to it, once each change to
/// the trades is committed: both messages, in this order, for every change (ADR-0066, ADR-0067).
/// The method names are the message names a page subscribes to, so renaming one breaks the
/// pages:
/// <code>
/// connection.On&lt;string&gt;("VersionChanged", version => …);
/// connection.On&lt;string, string[]&gt;("TradesChanged", (version, tradeIds) => …);
/// </code>
/// SignalR is the demo's dependency, not a library's (ADR-0068, Q63): a page passes on what it
/// heard, and neither ExGrid nor ExPivot learns how the notice arrived.
/// </summary>
public interface ITradeNotices
{
    /// <summary>
    /// <c>VersionChanged(version)</c>: the data moved on to this Source Version. A pivot over the
    /// server's Pivot Source asks again for the whole answer (ADR-0066).
    /// </summary>
    Task VersionChanged(string version);

    /// <summary>
    /// <c>TradesChanged(version, tradeIds)</c>: moving on to this Source Version changed, added or
    /// removed these trades, named by their Record Keys in ordinal order. A grid reads them again
    /// — a key no longer found is a trade removed — and marks the cells whose values changed
    /// (ADR-0067).
    /// </summary>
    Task TradesChanged(string version, string[] tradeIds);
}

/// <summary>
/// The hub the server says its changes on (ADR-0068). A page only listens: it calls nothing on
/// the hub, and everything it could ask is on the HTTP API. Its CORS policy is
/// <see cref="DemoCors.HubPolicy"/>.
/// </summary>
public sealed class TradesHub : Hub<ITradeNotices>
{
    /// <summary>Where the hub is mapped.</summary>
    public const string Path = "/hubs/trades";
}
