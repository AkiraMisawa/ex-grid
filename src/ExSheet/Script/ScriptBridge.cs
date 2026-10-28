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

    /// <summary>A call the browser held, too large for a message of its own: <paramref name="method"/>, with the
    /// arguments taken from the browser by <paramref name="crossed"/>'s number, made in its turn. Only a call that can
    /// carry a whole Workbook is held: an event that carries its documents, and a save's write.</summary>
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
            default:
                throw new InvalidOperationException($"Internal: the browser held a call to {method}, which carries nothing that large.");
        }
    });
}
