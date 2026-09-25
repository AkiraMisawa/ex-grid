using Microsoft.JSInterop;

namespace ExGrid.DemoPages;

/// <summary>
/// What layer 3 reads from the host that the browser cannot report. The WebAssembly
/// host's managed heap lives in the module's linear memory, which CDP's JS heap metrics
/// do not break down, so the soak asks the runtime itself (MEM-6). Called by the test
/// through Blazor's own <c>DotNet.invokeMethodAsync</c>: no script of the DemoHost's,
/// and nothing of the component's, is involved.
/// </summary>
public static class HostCounters
{
    /// <summary>The managed heap after a full, blocking collection.</summary>
    [JSInvokable]
    public static long ManagedHeapBytes() => GC.GetTotalMemory(forceFullCollection: true);
}
