using System.Text.Json;

namespace ExGrid.DemoApi;

/// <summary>What <c>GET</c> and <c>POST /api/pivot/window-gate</c> answer.</summary>
/// <param name="Hold">Whether a report's Window question waits at the gate.</param>
/// <param name="Windows">The Window each report's Window question named, oldest first, since the
/// last <c>/api/reset</c>: at most <see cref="PivotWindowGate.Kept"/> of the latest.</param>
internal sealed record PivotWindowGateState(bool Hold, IReadOnlyList<JsonElement> Windows);

/// <summary>What <c>POST /api/pivot/window-gate</c> takes: <c>{ "hold": true }</c>.</summary>
internal sealed record PivotWindowGateRequest(bool? Hold);

/// <summary>
/// A gate in front of every report's Window question (<c>POST /api/pivot/reports/{id}/window</c>),
/// for a layer-3 test that must keep a question in flight for as long as it says, and read the
/// Window it named (LV-34, FN-3a). On WebAssembly the browser sends the question and a test can
/// hold it at a route; on the Server host the server sends it, where no browser route reaches, so
/// the gate is here, where both hosts' questions arrive. It is opened by the test, never by a timer
/// (CLAUDE.md, principle 6). A question is recorded as it arrives, before it waits; one the caller
/// gives up on stops waiting. <c>/api/reset</c> opens the gate and forgets what it recorded, so a
/// test that failed while holding leaves nothing held for the next.
/// </summary>
internal sealed class PivotWindowGate
{
    /// <summary>How many of the latest Windows asked the gate keeps: a demo server left running
    /// with the gate unused does not grow.</summary>
    public const int Kept = 256;

    private readonly Lock _lock = new();
    private readonly Queue<JsonElement> _windows = new();
    private TaskCompletionSource? _closed;

    /// <summary>Whether questions wait, and the Windows asked.</summary>
    public PivotWindowGateState State
    {
        get
        {
            lock (_lock) return new(_closed is not null, _windows.ToArray());
        }
    }

    /// <summary>Holds the questions that arrive from now on, or lets every waiting one through.</summary>
    public PivotWindowGateState Set(bool hold)
    {
        TaskCompletionSource? open = null;
        lock (_lock)
        {
            if (hold)
                _closed ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            else
                (open, _closed) = (_closed, null);
        }
        open?.TrySetResult();
        return State;
    }

    /// <summary>Opens the gate and forgets the Windows asked (<c>/api/reset</c>).</summary>
    public void Reset()
    {
        Set(false);
        lock (_lock) _windows.Clear();
    }

    /// <summary>Records the Window <paramref name="document"/> names and waits while the gate is
    /// held. A document with no Window is let through unrecorded: reading it is the endpoint's.</summary>
    public Task PassAsync(string document, CancellationToken cancellationToken)
    {
        JsonElement? window = null;
        try
        {
            using var parsed = JsonDocument.Parse(document);
            if (parsed.RootElement.ValueKind == JsonValueKind.Object
                && parsed.RootElement.TryGetProperty("window", out var named))
                window = named.Clone();
        }
        catch (JsonException)
        {
            // Not JSON: the endpoint refuses it by name.
        }
        lock (_lock)
        {
            if (window is { } asked)
            {
                _windows.Enqueue(asked);
                while (_windows.Count > Kept)
                    _windows.Dequeue();
            }
            return _closed is { } closed ? closed.Task.WaitAsync(cancellationToken) : Task.CompletedTask;
        }
    }
}
