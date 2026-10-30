using System.Text.Json;
using Microsoft.JSInterop;

namespace ExPivot.Components;

/// <summary>
/// What the bridge in the browser calls in .NET (ADR-0370 section 1): the report's questions to the Pivot Source and
/// every event the pivot raised, each handed to the component it was made for. Its own object, so the component's
/// public surface carries no <see cref="JSInvokableAttribute"/> member a Consumer could call. Each call is made in the
/// order the browser made it, a call too large for a message of its own among them (<see cref="Held"/>).
/// </summary>
internal sealed class ScriptBridge(ExPivot pivot)
{
    private global::ExGrid.Components.HeldCalls Calls => pivot.Calls;

    [JSInvokable]
    public Task<bool> Heard(string kind, JsonElement payload) => Calls.InOrder(() => pivot.HeardAsync(kind, payload));

    [JSInvokable]
    public Task<string> Aggregate(int generation, long call, string query) => Calls.InOrder(() => pivot.AskAsync("aggregate", generation, call, query));

    [JSInvokable]
    public Task<string> Items(int generation, long call, string query) => Calls.InOrder(() => pivot.AskAsync("items", generation, call, query));

    [JSInvokable]
    public Task<string> Details(int generation, long call, string query) => Calls.InOrder(() => pivot.AskAsync("details", generation, call, query));

    [JSInvokable]
    public Task<string> ItemCounts(int generation, long call, string? query) => Calls.InOrder(() => pivot.AskAsync("itemCounts", generation, call, query));

    [JSInvokable]
    public Task Refresh(int generation) => Calls.InOrder(() => pivot.RefreshAsync(generation));

    [JSInvokable]
    public Task Release(int generation) => Calls.InOrder(() => pivot.Release(generation));

    [JSInvokable]
    public Task Cancel(long call) => Calls.InOrder(() => pivot.Cancel(call));

    [JSInvokable]
    public Task MountSlot(long id, int version, string name, string context) => Calls.InOrder(() => pivot.MountSlot(id, version, name, context));

    [JSInvokable]
    public Task UpdateSlot(long id, int version, string context) => Calls.InOrder(() => pivot.UpdateSlot(id, version, context));

    [JSInvokable]
    public Task UnmountSlot(long id) => Calls.InOrder(() => pivot.UnmountSlot(id));

    // A held call that answers nothing, made.
    private static object? Done(Action call)
    {
        call();
        return null;
    }

    /// <summary>A call the browser held, too large for a message of its own: <paramref name="method"/>, with the
    /// arguments taken from the browser by <paramref name="crossed"/>'s number, made in its turn. Only a call that can
    /// carry what the user or the Consumer made, an event, a query over a layout's items or a slot's view, is held.</summary>
    [JSInvokable]
    public Task<object?> Held(string method, JsonElement crossed) => Calls.Held(crossed, async arguments =>
    {
        JsonElement At(int index) => arguments[index];
        string Text(int index) => At(index).GetString() ?? throw new InvalidOperationException($"Internal: the browser held a call to {method} with no text at {index}.");
        return method switch
        {
            nameof(Heard) => await pivot.HeardAsync(Text(0), At(1)),
            nameof(Aggregate) => await pivot.AskAsync("aggregate", At(0).GetInt32(), At(1).GetInt64(), Text(2)),
            nameof(Items) => await pivot.AskAsync("items", At(0).GetInt32(), At(1).GetInt64(), Text(2)),
            nameof(Details) => await pivot.AskAsync("details", At(0).GetInt32(), At(1).GetInt64(), Text(2)),
            nameof(ItemCounts) => await pivot.AskAsync("itemCounts", At(0).GetInt32(), At(1).GetInt64(), At(2).GetString()),
            nameof(MountSlot) => Done(() => pivot.MountSlot(At(0).GetInt64(), At(1).GetInt32(), Text(2), Text(3))),
            nameof(UpdateSlot) => Done(() => pivot.UpdateSlot(At(0).GetInt64(), At(1).GetInt32(), Text(2))),
            _ => throw new InvalidOperationException($"Internal: the browser held a call to {method}, which carries nothing that large."),
        };
    });
}
