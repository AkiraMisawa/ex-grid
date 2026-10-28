using System.Text.Json;
using global::ExGrid.Script;
using Microsoft.JSInterop;

namespace ExChart.Script;

/// <summary>
/// What the bridge in the browser calls in .NET (ADR-0370 section 1): every event the chart or the surface raised, and
/// the Chrome slots the chart's Razor fills (ADR-0410), handed to the component it was made for in the order the
/// browser made them, a call too large for a message of its own among them (<see cref="Held"/>). Its own object, so the
/// components' public surface carries no <see cref="JSInvokableAttribute"/> member a Consumer could call.
/// </summary>
internal sealed class ScriptBridge(ScriptMount mount)
{
    private HeldCalls Calls => mount.Calls;

    [JSInvokable]
    public Task Heard(string kind, JsonElement payload) => Calls.InOrder(() => mount.HeardAsync(kind, payload));

    [JSInvokable]
    public Task MountSlot(long id, int version, string name, string context) => Calls.InOrder(() => mount.MountSlot(id, version, name, context));

    [JSInvokable]
    public Task UpdateSlot(long id, int version, string context) => Calls.InOrder(() => mount.UpdateSlot(id, version, context));

    [JSInvokable]
    public Task UnmountSlot(long id) => Calls.InOrder(() => mount.UnmountSlot(id));

    /// <summary>A call the browser held, too large for a message of its own: <paramref name="method"/>, with the
    /// arguments taken from the browser by <paramref name="crossed"/>'s number, made in its turn. Only a slot's view,
    /// which carries every category of a category axis, is held.</summary>
    [JSInvokable]
    public Task<object?> Held(string method, JsonElement crossed) => Calls.Held(crossed, arguments =>
    {
        JsonElement At(int index) => arguments[index];
        string Text(int index) => At(index).GetString() ?? throw new InvalidOperationException($"Internal: the browser held a call to {method} with no text at {index}.");
        switch (method)
        {
            case nameof(MountSlot):
                mount.MountSlot(At(0).GetInt64(), At(1).GetInt32(), Text(2), Text(3));
                return Task.FromResult<object?>(null);
            case nameof(UpdateSlot):
                mount.UpdateSlot(At(0).GetInt64(), At(1).GetInt32(), Text(2));
                return Task.FromResult<object?>(null);
            default:
                throw new InvalidOperationException($"Internal: the browser held a call to {method}, which carries nothing that large.");
        }
    });
}
