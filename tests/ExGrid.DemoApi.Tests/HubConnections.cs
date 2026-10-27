using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace ExGrid.DemoApi.Tests;

/// <summary>
/// When the hub counts a connection among those a change is said to. A client's <c>StartAsync</c> returns once the
/// handshake is answered, and the server adds the connection to <c>Clients.All</c> only after that, so a change
/// committed between the two is said to everyone but that client. The hub's <c>OnConnectedAsync</c> runs once the
/// connection is added; a test that must hear every change from a version on waits for it (spine rule 6).
/// </summary>
internal sealed class HubConnections : IHubFilter
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _added = new();

    /// <summary>Completes once the hub says changes to the connection with this id.</summary>
    public Task AddedAsync(string connectionId, CancellationToken cancellationToken)
        => Added(connectionId).Task.WaitAsync(cancellationToken);

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        await next(context);
        Added(context.Context.ConnectionId).TrySetResult();
    }

    private TaskCompletionSource Added(string connectionId)
        => _added.GetOrAdd(connectionId, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
}
