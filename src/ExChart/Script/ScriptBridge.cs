using System.Text.Json;
using Microsoft.JSInterop;

namespace ExChart.Script;

/// <summary>
/// What the bridge in the browser calls in .NET (ADR-0370 section 1): every event the chart or the surface raised,
/// handed to the component it was made for. Its own object, so the components' public surface carries no
/// <see cref="JSInvokableAttribute"/> member a Consumer could call.
/// </summary>
internal sealed class ScriptBridge(ScriptMount mount)
{
    [JSInvokable]
    public Task Heard(string kind, JsonElement payload) => mount.HeardAsync(kind, payload);
}
