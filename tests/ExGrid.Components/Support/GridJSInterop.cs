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
    private readonly JSRuntimeInvocationHandler _blur;

    private GridJSInterop(
        JSRuntimeInvocationHandler<ScrollOffset> offset,
        JSRuntimeInvocationHandler setOffset,
        JSRuntimeInvocationHandler blur,
        JSRuntimeInvocationHandler dispose)
    {
        _offset = offset;
        _setOffset = setOffset;
        _blur = blur;
        Dispose = dispose;
    }

    /// <summary>How many times Escape's Leave has released the grid's focus — the way
    /// "the grid was not blurred" is observable without a browser (ADR-0012).</summary>
    internal int BlurCount => _blur.Invocations.Count;

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
        // Asked once at attach: which modifier this platform's users reach for (ADR-0012).
        // The tests drive OnKeyAsync directly and pass it per key, so the answer here only
        // has to exist.
        var metaIsPrimary = handle.Setup<bool>("metaIsPrimary");
        metaIsPrimary.SetResult(false);
        var offset = handle.Setup<ScrollOffset>("getScrollOffset");
        offset.SetResult(default);
        var setOffset = handle.SetupVoid("setScrollOffset", _ => true);
        setOffset.SetVoidResult();
        var blur = handle.SetupVoid("blur");
        blur.SetVoidResult();

        // The Cell Editor's mode reaching the key gate (ADR-0010). The tests drive
        // OnKeyAsync directly, so the mode only has to be accepted here.
        var setEditing = handle.SetupVoid("setEditing", _ => true);
        setEditing.SetVoidResult();
        // Whether any column edits, re-told when a parameter change flips it
        // (ADR-0010/0020) — accepted for the same reason.
        var setCanEdit = handle.SetupVoid("setCanEdit", _ => true);
        setCanEdit.SetVoidResult();
        var dispose = handle.SetupVoid("dispose");
        dispose.SetVoidResult();
        return new GridJSInterop(offset, setOffset, blur, dispose);
    }

    /// <summary>What the next <c>getScrollOffset</c> answers — the browser scroll
    /// position the grid is about to read, on both axes at once.</summary>
    internal void SetScrollOffset(double top, double left)
        => _offset.SetResult(new ScrollOffset(top, left));
}
