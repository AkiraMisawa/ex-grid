using ExPivot.Engine;
using System.Text.Json;

namespace ExGrid.DemoApi;

/// <summary>What <c>GET</c> and <c>POST /api/pivot/window-gate</c> answer.</summary>
/// <param name="Hold">Whether a report's Window question waits at the gate.</param>
/// <param name="Windows">The Window each report's Window question named, oldest first, since the
/// last <c>/api/reset</c>: at most <see cref="PivotWindowGate.Kept"/> of the latest.</param>
/// <param name="Baselined">For each of <paramref name="Windows"/>, whether its question named a
/// Baseline Window, asking for Window Changes; a question without one asks for a complete Window.</param>
/// <param name="Complete">How many questions for a complete Window have arrived since the last
/// <c>/api/reset</c>: a count, which <paramref name="Windows"/>' length stops being once it is full.</param>
/// <param name="Spoil">How many answers of Window Changes are still to be sent with a wrong digest.</param>
/// <param name="Spoiled">How many have been, since the last <c>/api/reset</c>.</param>
/// <param name="Fail">How many questions for a complete Window are still to be refused with 503.</param>
/// <param name="Failed">How many have been, since the last <c>/api/reset</c>.</param>
internal sealed record PivotWindowGateState(bool Hold, IReadOnlyList<JsonElement> Windows, IReadOnlyList<bool> Baselined,
    int Complete, int Spoil, int Spoiled, int Fail, int Failed);

/// <summary>What <c>POST /api/pivot/window-gate</c> takes: <c>{ "hold": true }</c>, and or instead
/// <c>{ "spoil": 1 }</c> or <c>{ "fail": 1 }</c>, each a count from now on (LV-27).</summary>
internal sealed record PivotWindowGateRequest(bool? Hold, int? Spoil = null, int? Fail = null);

/// <summary>
/// A gate in front of every report's Window question (<c>POST /api/pivot/reports/{id}/window</c>),
/// for a layer-3 test that must keep a question in flight for as long as it says, and read the
/// Window it named (LV-34, FN-3a). On WebAssembly the browser sends the question and a test can
/// hold it at a route; on the Server host the server sends it, where no browser route reaches, so
/// the gate is here, where both hosts' questions arrive. It is opened by the test, never by a timer
/// (CLAUDE.md, principle 6). A question is recorded as it arrives, before it waits; one the caller
/// gives up on stops waiting. <c>/api/reset</c> opens the gate and forgets what it recorded, so a
/// test that failed while holding leaves nothing held for the next.
///
/// <para>It also spoils and refuses on request (LV-27): the next answers of Window Changes carry a
/// digest that is not their Window's, which the client must refuse and recover from by asking for a
/// complete Window, and the next questions for a complete Window are refused with 503 as they arrive,
/// so a recovery that cannot be had leaves the report stale with Retry. Only a question for a complete
/// Window is refused, so Window Changes keep arriving to be spoiled, whichever question a notice
/// supersedes. Counts, never a time.</para>
/// </summary>
internal sealed class PivotWindowGate
{
    /// <summary>How many of the latest Windows asked the gate keeps: a demo server left running
    /// with the gate unused does not grow.</summary>
    public const int Kept = 256;

    private readonly Lock _lock = new();
    private readonly Queue<(JsonElement Window, bool Baselined)> _asked = new();
    private TaskCompletionSource? _closed;
    private int _complete;
    private int _spoil;
    private int _spoiled;
    private int _fail;
    private int _failed;

    /// <summary>Whether questions wait, the Windows asked, and what is spoiled and refused.</summary>
    public PivotWindowGateState State
    {
        get
        {
            lock (_lock)
                return new(_closed is not null, _asked.Select(a => a.Window).ToArray(), _asked.Select(a => a.Baselined).ToArray(),
                    _complete, _spoil, _spoiled, _fail, _failed);
        }
    }

    /// <summary>Arms the spoils and the refusals where given, then holds the questions that arrive from
    /// now on or lets every waiting one through, where given: a question let through meets what was
    /// armed with it.</summary>
    public PivotWindowGateState Set(bool? hold, int? spoil = null, int? fail = null)
    {
        TaskCompletionSource? open = null;
        lock (_lock)
        {
            if (spoil is { } s) _spoil = Math.Max(0, s);
            if (fail is { } f) _fail = Math.Max(0, f);
            if (hold == true)
                _closed ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            else if (hold == false)
                (open, _closed) = (_closed, null);
        }
        open?.TrySetResult();
        return State;
    }

    /// <summary><paramref name="update"/>, or, when it is Window Changes and a spoil is asked for, the
    /// same changes with a digest that is not their Window's.</summary>
    public PivotReportUpdate Spoiled(PivotReportUpdate update)
    {
        if (update.Rows is not null || update.WindowDigest is not { } digest) return update;
        lock (_lock)
        {
            if (_spoil == 0) return update;
            _spoil--;
            _spoiled++;
        }
        return update with { WindowDigest = "spoiled-" + digest };
    }

    /// <summary>Opens the gate and forgets the Windows asked, the spoils and the refusals (<c>/api/reset</c>).</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _asked.Clear();
            _complete = _spoil = _spoiled = _fail = _failed = 0;
        }
        Set(false);
    }

    /// <summary>Records the Window <paramref name="document"/> names, and answers false for a question
    /// for a complete Window while a refusal is asked for; otherwise waits while the gate is held, and
    /// answers true. A document with no Window is let through unrecorded: reading it is the endpoint's.</summary>
    public async Task<bool> PassAsync(string document, CancellationToken cancellationToken)
    {
        JsonElement? window = null;
        var baselined = false;
        try
        {
            using var parsed = JsonDocument.Parse(document);
            if (parsed.RootElement.ValueKind == JsonValueKind.Object
                && parsed.RootElement.TryGetProperty("window", out var named))
            {
                window = named.Clone();
                baselined = parsed.RootElement.TryGetProperty("baseline", out var baseline) && baseline.ValueKind != JsonValueKind.Null;
            }
        }
        catch (JsonException)
        {
            // Not JSON: the endpoint refuses it by name.
        }
        Task wait;
        lock (_lock)
        {
            if (window is { } asked)
            {
                _asked.Enqueue((asked, baselined));
                while (_asked.Count > Kept)
                    _asked.Dequeue();
                if (!baselined)
                {
                    _complete++;
                    if (_fail > 0)
                    {
                        _fail--;
                        _failed++;
                        return false;
                    }
                }
            }
            wait = _closed is { } closed ? closed.Task.WaitAsync(cancellationToken) : Task.CompletedTask;
        }
        await wait;
        return true;
    }
}
