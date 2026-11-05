using System.Collections.Concurrent;

namespace ExGrid.DemoHost.Server;

/// <summary>
/// The disconnect beacons this host has read to their end, under layer 3 (EXGRID_HOST_LOG), by the token the harness
/// put in each. A page that goes away tells the host with a beacon, and closing the browser context it stands in ends
/// that beacon wherever it has got to; one cut before the host read it leaves its circuit standing, and a call to the
/// browser in flight then is called off by the circuit's timeout a minute later, as an unhandled exception in whatever
/// test runs then (CON-6). So the harness sends each page's beacon itself before it closes a context, with a token of
/// its own, and waits here until the host has read that one (fixtures.mjs, leaveCircuits): the beacon, not a time.
/// </summary>
internal sealed class DisconnectsRead
{
    /// <summary>The form field the harness adds to a beacon it sends.</summary>
    public const string TokenField = "layer3";

    // One per token, whichever comes first, the beacon read or the harness asking; taken away once asked.
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _tokens = new(StringComparer.Ordinal);

    private TaskCompletionSource Of(string token) =>
        _tokens.GetOrAdd(token, static _ => new(TaskCreationOptions.RunContinuationsAsynchronously));

    /// <summary>Notes the beacon carrying <paramref name="token"/> read to its end.</summary>
    public void Add(string token) => Of(token).TrySetResult();

    /// <summary>
    /// Answers true once the beacon carrying <paramref name="token"/> has been read, or false once
    /// <paramref name="within"/> has passed first. The token is forgotten either way: the harness asks once.
    /// </summary>
    public async Task<bool> Read(string token, TimeSpan within, CancellationToken cancellation)
    {
        try
        {
            await Of(token).Task.WaitAsync(within, cancellation);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            _tokens.TryRemove(token, out _);
        }
    }
}
