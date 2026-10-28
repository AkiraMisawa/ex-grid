using System.Text.Json;
using Microsoft.JSInterop;

namespace ExGrid.Script;

/// <summary>
/// What the bridge in the browser calls in .NET (ADR-0350 section 7): the source's questions and every event the
/// grid raised, each handed to the component it was made for. Its own object, so the component's public surface
/// carries no <see cref="JSInvokableAttribute"/> member a Consumer could call. Each call is made in the order the
/// browser made it, a call too large for a message of its own among them (<see cref="Held"/>).
/// </summary>
internal sealed class ScriptBridge<TRow>(ExGrid<TRow> grid)
    where TRow : class
{
    private HeldCalls Calls => grid.Calls;

    [JSInvokable]
    public Task<string?> Heard(string kind, JsonElement payload) => Calls.InOrder(() => grid.HeardAsync(kind, payload));

    [JSInvokable]
    public Task<string?> Validate(long number, int columnsVersion, long row, string column, string text)
        => Calls.InOrder(() => grid.ValidateAsync(number, columnsVersion, row, column, text));

    [JSInvokable]
    public Task<string?> Message(long row, string column) => Calls.InOrder(() => grid.MessageAsync(row, column));

    [JSInvokable]
    public Task<string?> FindError(long number, int generation, JsonElement request) => Calls.InOrder(() => grid.FindErrorAsync(number, generation, request));

    [JSInvokable]
    public Task<string?> Find(long number, int generation, JsonElement request) => Calls.InOrder(() => grid.FindAsync(number, generation, request));

    [JSInvokable]
    public Task<string?> ContextCommands(long number, int generation, int columnsVersion, long row, string column, JsonElement selection, int rowSequenceVersion)
        => Calls.InOrder(() => grid.ContextCommandsAsync(number, generation, columnsVersion, row, column, selection, rowSequenceVersion));

    [JSInvokable]
    public Task<string?> RunContextCommand(long number, int index) => Calls.InOrder(() => grid.RunContextCommandAsync(number, index));

    [JSInvokable]
    public Task CancelAnswer(long number) => Calls.InOrder(() => grid.CancelAnswer(number));

    [JSInvokable]
    public Task SetSorts(int generation, string query) => Calls.InOrder(() => grid.SetSortsAsync(generation, query));

    [JSInvokable]
    public Task SetFilter(int generation, string query) => Calls.InOrder(() => grid.SetFilterAsync(generation, query));

    [JSInvokable]
    public Task NeedRange(int generation, int start, int count) => Calls.InOrder(() => grid.NeedRangeAsync(generation, start, count));

    [JSInvokable]
    public Task<string> Rows(int generation, long id, int start, int count) => Calls.InOrder(() => grid.RowsAsync(generation, id, start, count));

    [JSInvokable]
    public Task<string> DistinctValues(int generation, long id, string column) => Calls.InOrder(() => grid.DistinctValuesAsync(generation, id, column));

    [JSInvokable]
    public Task Cancel(long id) => Calls.InOrder(() => grid.Cancel(id));

    [JSInvokable]
    public Task Release(long[] ids, long seen) => Calls.InOrder(() => grid.Release(ids, seen));

    [JSInvokable]
    public Task MountSlot(long id, int version, string name, string context) => Calls.InOrder(() => grid.MountSlot(id, version, name, context));

    [JSInvokable]
    public Task UpdateSlot(long id, int version, string context) => Calls.InOrder(() => grid.UpdateSlot(id, version, context));

    [JSInvokable]
    public Task UnmountSlot(long id) => Calls.InOrder(() => grid.UnmountSlot(id));

    /// <summary>A call the browser held, too large for a message of its own: <paramref name="method"/>, with the
    /// arguments taken from the browser by <paramref name="crossed"/>'s number, made in its turn. Only a call that can
    /// carry what the user or the Consumer made, a text, a paste, a list of values, a slot's view or a Selection of
    /// many ranges, is held.</summary>
    [JSInvokable]
    public Task<object?> Held(string method, JsonElement crossed) => Calls.Held(crossed, async arguments =>
    {
        JsonElement At(int index) => arguments[index];
        string Text(int index) => At(index).GetString() ?? throw new InvalidOperationException($"Internal: the browser held a call to {method} with no text at {index}.");
        switch (method)
        {
            case nameof(Heard):
                return await grid.HeardAsync(Text(0), At(1));
            case nameof(Validate):
                return await grid.ValidateAsync(At(0).GetInt64(), At(1).GetInt32(), At(2).GetInt64(), Text(3), Text(4));
            case nameof(Find):
                return await grid.FindAsync(At(0).GetInt64(), At(1).GetInt32(), At(2));
            case nameof(ContextCommands):
                return await grid.ContextCommandsAsync(At(0).GetInt64(), At(1).GetInt32(), At(2).GetInt32(), At(3).GetInt64(), Text(4), At(5), At(6).GetInt32());
            case nameof(FindError):
                return await grid.FindErrorAsync(At(0).GetInt64(), At(1).GetInt32(), At(2));
            case nameof(SetSorts):
                await grid.SetSortsAsync(At(0).GetInt32(), Text(1));
                return null;
            case nameof(SetFilter):
                await grid.SetFilterAsync(At(0).GetInt32(), Text(1));
                return null;
            case nameof(Release):
                grid.Release([.. At(0).EnumerateArray().Select(id => id.GetInt64())], At(1).GetInt64());
                return null;
            case nameof(MountSlot):
                grid.MountSlot(At(0).GetInt64(), At(1).GetInt32(), Text(2), Text(3));
                return null;
            case nameof(UpdateSlot):
                grid.UpdateSlot(At(0).GetInt64(), At(1).GetInt32(), Text(2));
                return null;
            default:
                throw new InvalidOperationException($"Internal: the browser held a call to {method}, which carries nothing that large.");
        }
    });
}
