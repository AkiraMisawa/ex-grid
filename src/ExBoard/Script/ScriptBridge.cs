using System.Text.Json;
using Microsoft.JSInterop;

namespace ExBoard.Script;

/// <summary>
/// What the bridge in the browser calls in .NET (ADR-0370 section 1): every event the Board raised, the Snapshots and
/// sources it let go, and each question it asks a source held where the data lives (ADR-0343), each handed to the
/// component it was made for. Its own object, so the component's public surface carries no
/// <see cref="JSInvokableAttribute"/> member a Consumer could call.
/// </summary>
internal sealed class ScriptBridge(ExBoard board)
{
    // Each text the browser sends crosses inline, or as a stream when it is too long for one message of a Blazor Server
    // circuit: exactly one of the two is given.
    [JSInvokable]
    public Task Heard(string kind, JsonElement payload, IJSStreamReference? streamed) => board.HeardAsync(kind, payload, streamed);

    [JSInvokable]
    public Task Release(int[] numbers) => board.ReleaseSnapshotsAsync(numbers);

    [JSInvokable]
    public Task ReleaseSources(int[] numbers) => board.ReleaseSourcesAsync(numbers);

    [JSInvokable]
    public Task<string> Shape(int number, long call) => board.ShapeAsync(number, call);

    [JSInvokable]
    public Task<SourcePage> Fetch(int number, long call, string? question, IJSStreamReference? streamed) => board.FetchAsync(number, call, question, streamed);

    [JSInvokable]
    public Task<string> Thin(int number, long call, string? question, IJSStreamReference? streamed) => board.ThinAsync(number, call, question, streamed);

    [JSInvokable]
    public void Cancel(long call) => board.Cancel(call);
}

/// <summary>A page of a grid's rows as it crosses to the browser: where it starts, how many rows the whole result holds,
/// and the rows as their Arrow stream (ADR-0065), which Blazor hands the browser as bytes.</summary>
internal sealed record SourcePage(int Start, int TotalCount, byte[] Rows);
