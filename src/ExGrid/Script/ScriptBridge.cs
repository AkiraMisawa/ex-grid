using System.Text.Json;
using Microsoft.JSInterop;

namespace ExGrid.Script;

/// <summary>
/// What the bridge in the browser calls in .NET (ADR-0350 section 7): the source's questions and every event the
/// grid raised, each handed to the component it was made for. Its own object, so the component's public surface
/// carries no <see cref="JSInvokableAttribute"/> member a Consumer could call.
/// </summary>
internal sealed class ScriptBridge<TRow>(ExGrid<TRow> grid)
    where TRow : class
{
    [JSInvokable]
    public Task<string?> Heard(string kind, JsonElement payload) => grid.HeardAsync(kind, payload);

    [JSInvokable]
    public Task<string?> Validate(long number, int columnsVersion, long row, string column, string text)
        => grid.ValidateAsync(number, columnsVersion, row, column, text);

    [JSInvokable]
    public Task<string?> Message(long row, string column) => grid.MessageAsync(row, column);

    [JSInvokable]
    public Task<string?> FindError(long number, int generation, JsonElement request) => grid.FindErrorAsync(number, generation, request);

    [JSInvokable]
    public void CancelAnswer(long number) => grid.CancelAnswer(number);

    [JSInvokable]
    public Task SetSorts(int generation, string query) => grid.SetSortsAsync(generation, query);

    [JSInvokable]
    public Task SetFilter(int generation, string query) => grid.SetFilterAsync(generation, query);

    [JSInvokable]
    public Task NeedRange(int generation, int start, int count) => grid.NeedRangeAsync(generation, start, count);

    [JSInvokable]
    public Task<string> Rows(int generation, long id, int start, int count) => grid.RowsAsync(generation, id, start, count);

    [JSInvokable]
    public Task<string> DistinctValues(int generation, long id, string column) => grid.DistinctValuesAsync(generation, id, column);

    [JSInvokable]
    public void Cancel(long id) => grid.Cancel(id);

    [JSInvokable]
    public void Release(long[] ids, long seen) => grid.Release(ids, seen);

    [JSInvokable]
    public void MountSlot(long id, int version, string name, string context) => grid.MountSlot(id, version, name, context);

    [JSInvokable]
    public void UpdateSlot(long id, int version, string context) => grid.UpdateSlot(id, version, context);

    [JSInvokable]
    public void UnmountSlot(long id) => grid.UnmountSlot(id);
}
