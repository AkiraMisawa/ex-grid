using System.Text.Json;
using Microsoft.JSInterop;

namespace ExPivot.Script;

/// <summary>
/// What the bridge in the browser calls in .NET (ADR-0370 section 1): the report's questions to the Pivot Source and
/// every event the pivot raised, each handed to the component it was made for. Its own object, so the component's
/// public surface carries no <see cref="JSInvokableAttribute"/> member a Consumer could call.
/// </summary>
internal sealed class ScriptBridge(ExPivot pivot)
{
    [JSInvokable]
    public Task Heard(string kind, JsonElement payload) => pivot.HeardAsync(kind, payload);

    [JSInvokable]
    public Task<string> Aggregate(int generation, long call, string query) => pivot.AskAsync("aggregate", generation, call, query);

    [JSInvokable]
    public Task<string> Items(int generation, long call, string query) => pivot.AskAsync("items", generation, call, query);

    [JSInvokable]
    public Task<string> Details(int generation, long call, string query) => pivot.AskAsync("details", generation, call, query);

    [JSInvokable]
    public Task<string> ItemCounts(int generation, long call, string? query) => pivot.AskAsync("itemCounts", generation, call, query);

    [JSInvokable]
    public Task Refresh(int generation) => pivot.RefreshAsync(generation);

    [JSInvokable]
    public void Release(int generation) => pivot.Release(generation);

    [JSInvokable]
    public void Cancel(long call) => pivot.Cancel(call);
}
