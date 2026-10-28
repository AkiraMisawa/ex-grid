using System.Text.Json;
using Microsoft.JSInterop;

namespace ExBoard.Script;

/// <summary>
/// What the bridge in the browser calls in .NET (ADR-0370 section 1): every event the Board raised, and the Snapshots it
/// let go, each handed to the component it was made for. Its own object, so the component's public surface carries no
/// <see cref="JSInvokableAttribute"/> member a Consumer could call.
/// </summary>
internal sealed class ScriptBridge(ExBoard board)
{
    [JSInvokable]
    public Task Heard(string kind, JsonElement payload) => board.HeardAsync(kind, payload);

    [JSInvokable]
    public Task Release(int[] numbers) => board.ReleaseSnapshotsAsync(numbers);
}
