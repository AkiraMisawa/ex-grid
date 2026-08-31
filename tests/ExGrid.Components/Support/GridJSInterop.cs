using Bunit;

namespace ExGrid.Components.Tests.Support;

/// <summary>
/// Stands in for wwwroot/ex-grid.js: the module, the per-instance handle
/// <c>attach</c> returns, and the offset that handle reads (ADR-0018/0021). bUnit's
/// JSInterop is strict, so an unstubbed call fails the test — which is the signal
/// wanted here: the day the component reaches for JavaScript somewhere new, the tests
/// say so, and the allowlist gets consulted before an ADR is quietly bypassed.
/// </summary>
internal sealed class GridJSInterop
{
    /// <summary>Must match the path ExGrid imports.</summary>
    internal const string ModulePath = "./_content/ExGrid/ex-grid.js";

    private readonly JSRuntimeInvocationHandler<double> _scrollTop;

    private GridJSInterop(JSRuntimeInvocationHandler<double> scrollTop, JSRuntimeInvocationHandler dispose)
    {
        _scrollTop = scrollTop;
        Dispose = dispose;
    }

    /// <summary>The handle's own dispose — asserted by the teardown test (ADR-0018).</summary>
    internal JSRuntimeInvocationHandler Dispose { get; }

    internal static GridJSInterop Setup(BunitContext context)
    {
        var module = context.JSInterop.SetupModule(ModulePath);
        var handle = module.SetupModule("attach", _ => true);
        var scrollTop = handle.Setup<double>("getScrollTop");
        scrollTop.SetResult(0);
        var dispose = handle.SetupVoid("dispose");
        dispose.SetVoidResult();
        return new GridJSInterop(scrollTop, dispose);
    }

    /// <summary>What the next <c>getScrollTop</c> answers — the browser scroll position
    /// the grid is about to read.</summary>
    internal void SetScrollTop(double px) => _scrollTop.SetResult(px);
}
