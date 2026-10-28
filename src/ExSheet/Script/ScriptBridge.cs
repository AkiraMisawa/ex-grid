using System.Text.Json;
using Microsoft.JSInterop;

namespace ExSheet.Script;

/// <summary>
/// What the bridge in the browser calls in .NET (ADR-0370 section 1): every event the Sheet raised, and the read or
/// write of a file in its turn, handed to the component it was made for. Its own object, so the component's public surface carries no
/// <see cref="JSInvokableAttribute"/> member a Consumer could call.
/// </summary>
internal sealed class ScriptBridge(ExSheet sheet)
{
    [JSInvokable]
    public Task Heard(string kind, JsonElement payload) => sheet.HeardAsync(kind, payload);

    [JSInvokable]
    public Task<string> Read(int id) => sheet.ReadAsync(id);

    [JSInvokable]
    public void CancelRead(int id) => sheet.CancelRead(id);

    [JSInvokable]
    public Task<string> Write(int id, string saved) => sheet.WriteAsync(id, saved);
}
