using System.Text.Json;
using Microsoft.JSInterop;

namespace ExBoard.Script;

/// <summary>
/// What the bridge in the browser calls in .NET (ADR-0370 section 1): every event the Board raised, the Snapshots and
/// sources it let go, each question it asks a source held where the data lives (ADR-0343), and the Chromes' slots its
/// views mounted (ADR-0410), each handed to the
/// component it was made for. Its own object, so the component's public surface carries no
/// <see cref="JSInvokableAttribute"/> member a Consumer could call. Each call is made in the order the browser made it,
/// a call too large for a message of its own among them (<see cref="Held"/>).
/// </summary>
internal sealed class ScriptBridge(ExBoard board)
{
    private global::ExGrid.Script.HeldCalls Calls => board.Calls;

    [JSInvokable]
    public Task Heard(string kind, JsonElement payload) => Calls.InOrder(() => board.HeardAsync(kind, payload));

    [JSInvokable]
    public Task Release(int[] numbers) => Calls.InOrder(() => board.ReleaseSnapshotsAsync(numbers));

    [JSInvokable]
    public Task ReleaseSources(int[] numbers) => Calls.InOrder(() => board.ReleaseSourcesAsync(numbers));

    [JSInvokable]
    public Task<string> Shape(int number, long call) => Calls.InOrder(() => board.ShapeAsync(number, call));

    [JSInvokable]
    public Task<SourcePage> Fetch(int number, long call, string question) => Calls.InOrder(() => board.FetchAsync(number, call, question));

    [JSInvokable]
    public Task<string> Thin(int number, long call, string question) => Calls.InOrder(() => board.ThinAsync(number, call, question));

    [JSInvokable]
    public Task<string> Under(int number, long call, string question) => Calls.InOrder(() => board.UnderAsync(number, call, question));

    [JSInvokable]
    public Task MakePivot(int source, int pivot, string narrowing) => Calls.InOrder(() => board.MakePivotAsync(source, pivot, narrowing));

    [JSInvokable]
    public Task<string> PivotAggregate(int pivot, long call, string query) => Calls.InOrder(() => board.PivotAskAsync("aggregate", pivot, call, query));

    [JSInvokable]
    public Task<string> PivotItems(int pivot, long call, string query) => Calls.InOrder(() => board.PivotAskAsync("items", pivot, call, query));

    [JSInvokable]
    public Task<string> PivotDetails(int pivot, long call, string query) => Calls.InOrder(() => board.PivotAskAsync("details", pivot, call, query));

    [JSInvokable]
    public Task<string> PivotItemCounts(int pivot, long call, string? query) => Calls.InOrder(() => board.PivotAskAsync("itemCounts", pivot, call, query));

    [JSInvokable]
    public Task PivotRefresh(int pivot) => Calls.InOrder(() => board.PivotRefreshAsync(pivot));

    [JSInvokable]
    public Task ReleasePivot(int pivot) => Calls.InOrder(() => board.ReleasePivot(pivot));

    [JSInvokable]
    public Task MountSlot(long id, int version, string name, string context) => Calls.InOrder(() => board.MountSlot(id, version, name, context));

    [JSInvokable]
    public Task UpdateSlot(long id, int version, string context) => Calls.InOrder(() => board.UpdateSlot(id, version, context));

    [JSInvokable]
    public Task UnmountSlot(long id) => Calls.InOrder(() => board.UnmountSlot(id));

    // A pivot's questions are numbered apart from the sources' own, and cancelled as theirs are.
    [JSInvokable]
    public Task PivotCancel(long call) => board.TryCancelPivot(call) ? Task.CompletedTask : Calls.InOrder(() => board.TryCancelPivot(call));

    // A question already asked is cancelled at once; one not yet asked, held or behind a held call, once it is.
    [JSInvokable]
    public Task Cancel(long call) => board.TryCancel(call) ? Task.CompletedTask : Calls.InOrder(() => board.TryCancel(call));

    /// <summary>A call the browser held, too large for a message of its own: <paramref name="method"/>, with the
    /// arguments taken from the browser by <paramref name="crossed"/>'s number, made in its turn. Only a call that can
    /// carry what the Board or the Consumer made, an event, a question narrowed by a link's values or naming a layout's
    /// Items, or a slot's view, is held.</summary>
    [JSInvokable]
    public Task<object?> Held(string method, JsonElement crossed) => Calls.Held(crossed, async arguments =>
    {
        JsonElement At(int index) => arguments[index];
        string Text(int index) => At(index).GetString() ?? throw new InvalidOperationException($"Internal: the browser held a call to {method} with no text at {index}.");
        switch (method)
        {
            case nameof(Heard):
                await board.HeardAsync(Text(0), At(1));
                return null;
            case nameof(Fetch):
                return await board.FetchAsync(At(0).GetInt32(), At(1).GetInt64(), Text(2));
            case nameof(Thin):
                return await board.ThinAsync(At(0).GetInt32(), At(1).GetInt64(), Text(2));
            case nameof(Under):
                return await board.UnderAsync(At(0).GetInt32(), At(1).GetInt64(), Text(2));
            case nameof(MakePivot):
                await board.MakePivotAsync(At(0).GetInt32(), At(1).GetInt32(), Text(2));
                return null;
            case nameof(PivotAggregate):
                return await board.PivotAskAsync("aggregate", At(0).GetInt32(), At(1).GetInt64(), Text(2));
            case nameof(PivotItems):
                return await board.PivotAskAsync("items", At(0).GetInt32(), At(1).GetInt64(), Text(2));
            case nameof(PivotDetails):
                return await board.PivotAskAsync("details", At(0).GetInt32(), At(1).GetInt64(), Text(2));
            case nameof(MountSlot):
                board.MountSlot(At(0).GetInt64(), At(1).GetInt32(), Text(2), Text(3));
                return null;
            case nameof(UpdateSlot):
                board.UpdateSlot(At(0).GetInt64(), At(1).GetInt32(), Text(2));
                return null;
            default:
                throw new InvalidOperationException($"Internal: the browser held a call to {method}, which carries nothing that large.");
        }
    });
}

/// <summary>A page of a grid's rows as it crosses to the browser: where it starts, how many rows the whole result holds,
/// and the rows as their Arrow stream (ADR-0065), which Blazor hands the browser as bytes.</summary>
internal sealed record SourcePage(int Start, int TotalCount, byte[] Rows);
