using Microsoft.JSInterop;

namespace ExGrid.Components.Tests.Support;

/// <summary>
/// A JavaScript runtime that counts what bUnit's does not: every object reference the grid
/// is handed, and how many times each is disposed (MEM-3). Each <c>import</c> answers a new
/// module and each <c>attach</c> a new handle, so a second import or attach shows as a
/// second reference that has to be given back too. Either answer can be held back, so a
/// test can dispose the grid while one is still on its way — the races in which a reference
/// is easiest to lose.
/// </summary>
internal sealed class CountingJSRuntime : IJSRuntime
{
    private readonly List<CountingModule> _modules = [];
    private readonly List<CountingHandle> _handles = [];

    internal IReadOnlyList<CountingModule> Modules => _modules;

    internal IReadOnlyList<CountingHandle> Handles => _handles;

    /// <summary>When set, <c>import</c> answers only once this completes.</summary>
    internal TaskCompletionSource? ImportGate { get; set; }

    /// <summary>When set, <c>attach</c> answers only once this completes.</summary>
    internal TaskCompletionSource? AttachGate { get; set; }

    /// <summary>Every .NET reference the grid handed to <c>attach</c>.</summary>
    internal List<object?> Selves { get; } = [];

    /// <summary>What <c>getScrollOffset</c> answers.</summary>
    internal ScrollOffset ScrollOffset { get; set; }

    /// <summary>A Blazor Server circuit going away: every JS call still pending is
    /// canceled, a reference's own dispose among them.</summary>
    internal bool CircuitGone { get; set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public async ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (identifier != "import")
            throw new InvalidOperationException($"The grid called '{identifier}' on the runtime itself.");
        var module = new CountingModule(this);
        _modules.Add(module);
        if (ImportGate is { } gate)
            await gate.Task;
        return (TValue)(object)module;
    }

    internal CountingHandle Attach(object?[]? args)
    {
        // attach(root, scroller, dotNetRef, …) — the order ex-grid.js takes them in.
        Selves.Add(args![2]);
        var handle = new CountingHandle(this);
        _handles.Add(handle);
        return handle;
    }
}

/// <summary>A JavaScript object reference that counts its disposals.</summary>
internal abstract class CountingReference(CountingJSRuntime runtime) : IJSObjectReference
{
    protected CountingJSRuntime Runtime => runtime;

    internal int Disposals { get; private set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public abstract ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args);

    public ValueTask DisposeAsync()
    {
        Disposals++;
        return runtime.CircuitGone
            ? ValueTask.FromException(new TaskCanceledException())
            : ValueTask.CompletedTask;
    }
}

/// <summary>The imported module: it answers <c>attach</c> and nothing else.</summary>
internal sealed class CountingModule(CountingJSRuntime runtime) : CountingReference(runtime)
{
    public override async ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (identifier != "attach")
            throw new InvalidOperationException($"The grid called '{identifier}' on the module.");
        var handle = Runtime.Attach(args);
        if (Runtime.AttachGate is { } gate)
            await gate.Task;
        return (TValue)(object)handle;
    }
}

/// <summary>The per-instance handle <c>attach</c> returns (ADR-0018).</summary>
internal sealed class CountingHandle(CountingJSRuntime runtime) : CountingReference(runtime)
{
    public override ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
        => ValueTask.FromResult(identifier switch
        {
            "metaIsPrimary" => (TValue)(object)false,
            "getScrollOffset" => (TValue)(object)Runtime.ScrollOffset,
            // A void call (InvokeVoidAsync asks for IJSVoidResult) needs no answer; any other
            // question this stand-in has not been taught is refused, rather than answered
            // with a default the grid would take for the browser's.
            _ when typeof(TValue) == typeof(Microsoft.JSInterop.Infrastructure.IJSVoidResult) => default!,
            _ => throw new InvalidOperationException($"The grid asked the handle '{identifier}', which this stand-in does not answer."),
        });
}

/// <summary>A clock that counts each timer it hands out, and each disposal of one.</summary>
internal sealed class CountingTimeProvider(TimeProvider inner) : TimeProvider
{
    private readonly List<CountingTimer> _timers = [];

    internal IReadOnlyList<CountingTimer> Timers => _timers;

    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

    public override long GetTimestamp() => inner.GetTimestamp();

    public override long TimestampFrequency => inner.TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new CountingTimer(inner.CreateTimer(callback, state, dueTime, period), callback.Method.Name);
        _timers.Add(timer);
        return timer;
    }
}

internal sealed class CountingTimer(ITimer inner, string name) : ITimer
{
    /// <summary>The callback's name — which of the grid's timers this is.</summary>
    internal string Name => name;

    internal int Disposals { get; private set; }

    public bool Change(TimeSpan dueTime, TimeSpan period) => inner.Change(dueTime, period);

    public void Dispose()
    {
        Disposals++;
        inner.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Disposals++;
        return inner.DisposeAsync();
    }
}
