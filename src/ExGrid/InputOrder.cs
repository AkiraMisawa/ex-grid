namespace ExGrid;

/// <summary>
/// The Input Order of one product instance (ADR-0240): inputs are handled one at a time, by the number
/// the script gave each as it happened, never by the order their messages arrive in. An input whose
/// number comes before one still to arrive waits for it; its handler then runs to its end, every
/// <c>await</c> included, before the next one starts. A number that will never be handled — told ahead
/// of an event that did not come, or refused before its handling — is <see cref="Void"/>, and the order
/// moves past it.
/// </summary>
/// <remarks>
/// <para>Nothing here waits on a clock: a number ends by being handled, voided, or by the order being
/// disposed (ADR-0240 section 4).</para>
/// <para>A handler that, while it runs, asks for more of the instance's handling (a commit, a placement a
/// Consumer's handler asks for) is inside the order's flow, and runs at once: waiting there would wait
/// for itself. Work the instance starts itself, outside any input, goes after every input received so
/// far (<see cref="AfterReceivedAsync"/>).</para>
/// <para>Not thread-safe: it lives on the renderer's synchronisation context, as the component does.</para>
/// </remarks>
internal sealed class InputOrder
{
    private readonly Action<int> _answered;
    private readonly SortedDictionary<int, Waiting> _waiting = [];
    private readonly HashSet<int> _voided = [];
    private readonly Queue<Waiting> _unnumbered = new();
    private readonly AsyncLocal<object?> _flow = new();

    // Every number up to this one has been handled or voided.
    private int _done;

    // The highest number received, handled, waiting or voided: work the instance starts itself goes after it.
    private int _received;

    // The handling under way, and the token its flow carries.
    private object? _running;
    private bool _disposed;

    /// <param name="answered">Told each number once it has been handled or voided, in order, every number
    /// before it included (the Settled Stamp's <c>data-ex-answered</c>).</param>
    public InputOrder(Action<int> answered) => _answered = answered;

    /// <summary>The last number handled or voided, every one before it included.</summary>
    public int AnsweredUpTo => _done;

    /// <summary>Whether this runs inside the flow of the handling under way: its own code, a Consumer
    /// handler it raised, or anything those call.</summary>
    public bool InFlow => _running is { } token && ReferenceEquals(_flow.Value, token);

    /// <summary>The handling under way, when this runs inside its flow; null outside every input's flow.
    /// Names the input's flow to work it starts that outlives a call (a commit), so that what that work
    /// is asked for later from inside the same flow is told apart from what another input, or no input,
    /// asks for.</summary>
    public object? Flow => InFlow ? _running : null;

    /// <summary>Whether no input is waiting or being handled.</summary>
    public bool Idle => _running is null && _waiting.Count == 0 && _unnumbered.Count == 0;

    /// <summary>
    /// Handles the input numbered <paramref name="input"/> once every number before it has been handled
    /// or voided, and before any number after it. An input with no number (0 or less), or one asked for
    /// inside the flow of the handling under way, is handled at once. An input whose number the order has
    /// already moved past (voided ahead of it) goes after every input received so far.
    /// </summary>
    public Task RunAsync(int input, Func<Task> handling)
    {
        if (input <= 0 || InFlow || _disposed)
            return handling();
        if (input <= _done || _waiting.ContainsKey(input) || _voided.Contains(input))
            return AfterReceivedAsync(handling);
        var waiting = new Waiting(input, handling);
        _waiting.Add(input, waiting);
        if (input > _received)
            _received = input;
        Pump();
        return waiting.Completion.Task;
    }

    /// <summary><see cref="RunAsync(int, Func{Task})"/> for a handling that answers a value.</summary>
    public async Task<T> RunAsync<T>(int input, Func<Task<T>> handling)
    {
        T result = default!;
        await RunAsync(input, async () => { result = await handling(); });
        return result;
    }

    /// <summary>
    /// The input numbered <paramref name="input"/> will not be handled: the order moves past it once every
    /// number before it is done. A number already handled, waiting or voided is left as it is.
    /// </summary>
    public void Void(int input)
    {
        if (input <= _done || _waiting.ContainsKey(input) || _disposed)
            return;
        _voided.Add(input);
        if (input > _received)
            _received = input;
        Pump();
    }

    /// <summary>Runs work the instance starts itself, outside any input, after every input received so
    /// far and before any received after it. Inside the flow of the handling under way it runs at once.</summary>
    public Task AfterReceivedAsync(Func<Task> handling)
    {
        if (InFlow || _disposed || (Idle && _received == _done))
            return handling();
        var waiting = new Waiting(0, handling, After: _received);
        _unnumbered.Enqueue(waiting);
        Pump();
        return waiting.Completion.Task;
    }

    /// <summary>Every input waiting is let go: its handling never runs, and its task ends at once, as an
    /// input answered with nothing done (a disposed instance has nothing to do it on).</summary>
    public void Dispose()
    {
        _disposed = true;
        foreach (var waiting in _waiting.Values)
            waiting.Completion.TrySetResult();
        foreach (var waiting in _unnumbered)
            waiting.Completion.TrySetResult();
        _waiting.Clear();
        _unnumbered.Clear();
        _voided.Clear();
    }

    // Starts what comes next, as long as nothing is being handled. A handling that completes at once is
    // finished in this loop rather than from its continuation, so a burst handled synchronously (on
    // WebAssembly) does not nest a call per input.
    private void Pump()
    {
        while (_running is null && !_disposed)
        {
            Waiting? next = null;
            if (_unnumbered.TryPeek(out var after) && after.After <= _done)
                next = _unnumbered.Dequeue();
            else if (_voided.Remove(_done + 1))
            {
                _done++;
                _answered(_done);
                continue;
            }
            else if (_waiting.Remove(_done + 1, out var numbered))
                next = numbered;
            if (next is null)
                return;

            var token = new object();
            _running = token;
            Task task;
            var outer = _flow.Value;
            _flow.Value = token;
            try
            {
                task = next.Handling();
            }
            catch (Exception exception)
            {
                task = Task.FromException(exception);
            }
            finally
            {
                _flow.Value = outer;
            }
            if (task.IsCompleted)
                Finish(next, task);
            else
                _ = FinishWhenDoneAsync(next, task);
        }
    }

    private async Task FinishWhenDoneAsync(Waiting waiting, Task task)
    {
        try
        {
            await task;
        }
        catch (Exception)
        {
            // Reported on the waiting caller's task (Finish).
        }
        Finish(waiting, task);
        Pump();
    }

    private void Finish(Waiting waiting, Task task)
    {
        _running = null;
        if (waiting.Input > 0)
        {
            _done = waiting.Input;
            _answered(_done);
        }
        if (task.IsFaulted)
            waiting.Completion.TrySetException(task.Exception!.InnerExceptions);
        else if (task.IsCanceled)
            waiting.Completion.TrySetCanceled();
        else
            waiting.Completion.TrySetResult();
    }

    private sealed record Waiting(int Input, Func<Task> Handling, int After = 0)
    {
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
