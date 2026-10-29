using System.Text.Json;
using Microsoft.JSInterop;

namespace ExSheet.Script;

/// <summary>
/// What the bridge in the browser calls in .NET (ADR-0370 section 1): every event the Sheet raised, and the read or
/// write of a file in its turn, handed to the component it was made for. Its own object, so the component's public surface carries no
/// <see cref="JSInvokableAttribute"/> member a Consumer could call. Each call is made in the order the browser made
/// it, a call too large for a message of its own among them (<see cref="Held"/>).
/// </summary>
internal sealed class ScriptBridge(ExSheet sheet)
{
    private global::ExGrid.Script.HeldCalls Calls => sheet.Calls;

    [JSInvokable]
    public Task Heard(string kind, JsonElement payload) => Calls.InOrder(() => sheet.HeardAsync(kind, payload));

    [JSInvokable]
    public Task<string> Read(int id) => Calls.InOrder(() => sheet.ReadAsync(id));

    [JSInvokable]
    public Task CancelRead(int id) => Calls.InOrder(() => sheet.CancelRead(id));

    [JSInvokable]
    public Task<string> Write(int id, JsonElement saved) => Calls.InOrder(() => sheet.WriteAsync(id, saved));

    [JSInvokable]
    public Task ToolbarPressed(int button, int press) => Calls.InOrder(() => sheet.ToolbarPressedAsync(button, press));

    [JSInvokable]
    public Task MountSlot(long id, int version, string name, string context) => Calls.InOrder(() => sheet.MountSlot(id, version, name, context));

    [JSInvokable]
    public Task UpdateSlot(long id, int version, string context) => Calls.InOrder(() => sheet.UpdateSlot(id, version, context));

    [JSInvokable]
    public Task UnmountSlot(long id) => Calls.InOrder(() => sheet.UnmountSlot(id));

    /// <summary>A call the browser held, too large for a message of its own: <paramref name="method"/>, with the
    /// arguments taken from the browser by <paramref name="crossed"/>'s number, made in its turn. Only a call that can
    /// carry that much is held: an event that carries its documents, a save's write, and a Razor slot's data (ADR-0410),
    /// which the Consumer's words make as long as they are.</summary>
    [JSInvokable]
    public Task<object?> Held(string method, JsonElement crossed) => Calls.Held(crossed, async arguments =>
    {
        switch (method)
        {
            case nameof(Heard):
                await sheet.HeardAsync(arguments[0].GetString() ?? throw new InvalidOperationException("Internal: the browser held an event with no kind."), arguments[1]);
                return null;
            case nameof(Write):
                return await sheet.WriteAsync(arguments[0].GetInt32(), arguments[1]);
            case nameof(MountSlot):
                sheet.MountSlot(arguments[0].GetInt64(), arguments[1].GetInt32(), Text(arguments[2]), Text(arguments[3]));
                return null;
            case nameof(UpdateSlot):
                sheet.UpdateSlot(arguments[0].GetInt64(), arguments[1].GetInt32(), Text(arguments[2]));
                return null;
            default:
                throw new InvalidOperationException($"Internal: the browser held a call to {method}, which carries nothing that large.");
        }
    });

    private static string Text(JsonElement argument) => argument.GetString() ?? throw new InvalidOperationException("Internal: the browser held a slot's call with no text.");
}
