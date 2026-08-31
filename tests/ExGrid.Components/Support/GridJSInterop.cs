using Bunit;

namespace ExGrid.Components.Tests.Support;

/// <summary>
/// Stands in for wwwroot/ex-grid.js: the module, the per-instance handle
/// <c>attach</c> returns, and the offsets that handle reads (ADR-0018/0021). bUnit's
/// JSInterop is strict, so an unstubbed call fails the test — which is the signal
/// wanted here: the day the component reaches for JavaScript somewhere new, the tests
/// say so, and the allowlist gets consulted before an ADR is quietly bypassed.
/// </summary>
internal sealed class GridJSInterop
{
    /// <summary>Must match the path ExGrid imports.</summary>
    internal const string ModulePath = "./_content/ExGrid/ex-grid.js";

    private readonly JSRuntimeInvocationHandler<ScrollOffset> _offset;
    private readonly JSRuntimeInvocationHandler _setOffset;

    private GridJSInterop(
        JSRuntimeInvocationHandler<ScrollOffset> offset,
        JSRuntimeInvocationHandler setOffset,
        JSRuntimeInvocationHandler dispose)
    {
        _offset = offset;
        _setOffset = setOffset;
        Dispose = dispose;
    }

    /// <summary>The handle's own dispose — asserted by the teardown test (ADR-0018).</summary>
    internal JSRuntimeInvocationHandler Dispose { get; }

    /// <summary>How many times the grid has read the offsets. Both axes come back in one
    /// call by design, so this counting is the contract: two reads per scroll would mean
    /// the rows and the columns were painted from different moments.</summary>
    internal int OffsetReads => _offset.Invocations.Count;

    /// <summary>Where the grid has told the browser to scroll — how Focus-follows-scroll
    /// is observed without a browser (ADR-0012).</summary>
    internal IReadOnlyList<(double Top, double Left)> ScrolledTo =>
        [.. _setOffset.Invocations
            .Select(invocation => ((double)invocation.Arguments[0]!, (double)invocation.Arguments[1]!))];

    internal static GridJSInterop Setup(BunitContext context)
    {
        var module = context.JSInterop.SetupModule(ModulePath);
        var handle = module.SetupModule("attach", _ => true);
        var offset = handle.Setup<ScrollOffset>("getScrollOffset");
        offset.SetResult(default);
        var setOffset = handle.SetupVoid("setScrollOffset", _ => true);
        setOffset.SetVoidResult();
        var blur = handle.SetupVoid("blur");
        blur.SetVoidResult();
        var dispose = handle.SetupVoid("dispose");
        dispose.SetVoidResult();
        return new GridJSInterop(offset, setOffset, dispose);
    }

    /// <summary>What the next <c>getScrollOffset</c> answers — the browser scroll
    /// position the grid is about to read, on both axes at once.</summary>
    internal void SetScrollOffset(double top, double left)
        => _offset.SetResult(new ScrollOffset(top, left));
}
