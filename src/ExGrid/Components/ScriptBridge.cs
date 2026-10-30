using System.Text.Json;
using Microsoft.JSInterop;

namespace ExGrid.Components;

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
    public Task<string?> Complete(string text, int caret) => Calls.InOrder(() => grid.CompleteAsync(text, caret));

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
    public Task<string?> Copy(long number, JsonElement request) => Calls.InOrder(() => grid.CopyAsync(number, request));

    [JSInvokable]
    public Task<string?> Summarize(long number, int? generation, JsonElement request) => Calls.InOrder(() => grid.SummarizeAsync(number, generation, request));

    /// <summary>Heard as it arrives, ahead of the calls before it: it calls off one of them, which may still be being
    /// taken (ADR-0240 section 12).</summary>
    [JSInvokable]
    public void CancelAnswer(long number) => grid.CancelAnswer(number);

    /// <summary>The browser reached the turn of work .NET placed in its Input Order (ADR-0441 section 3). Made in the order
    /// it arrived, never behind the turns of the grid's events: an event's handler may be awaiting it.</summary>
    [JSInvokable]
    public Task<string> RunWork(int number) => Calls.InOrder(() => grid.RunWorkAsync(number));

    /// <summary>An Escape cancelled work .NET placed; heard as it arrives, ahead of the calls before it.</summary>
    [JSInvokable]
    public void CancelWork(int number) => grid.CancelWork(number);

    /// <summary>The browser numbered an event on a control of the Consumer's that claims it (ADR-0441 section 4): heard as
    /// it arrives, ahead of the event Blazor dispatches after it, whose handler takes the number.</summary>
    [JSInvokable]
    public void ClaimTold(string kind, int number) => grid.ClaimTold(kind, number);

    /// <summary>The dispatch of the event the browser numbered is over: heard as it arrives, as its telling was.</summary>
    [JSInvokable]
    public void ClaimPassed(int number) => grid.ClaimPassed(number);

    [JSInvokable]
    public Task<string> RunClaim(int number) => Calls.InOrder(() => grid.RunClaimAsync(number));

    [JSInvokable]
    public Task PopoverClosed(int opening) => Calls.InOrder(() => grid.PopoverClosed(opening));

    [JSInvokable]
    public Task WorkCancelledTooLate(int number, bool landed) => Calls.InOrder(() => grid.WorkCancelledTooLateAsync(number, landed));

    [JSInvokable]
    public Task ForgetWork(int number) => Calls.InOrder(() => grid.ForgetWork(number));

    [JSInvokable]
    public Task NeedRange(int generation, int start, int count) => Calls.InOrder(() => grid.NeedRangeAsync(generation, start, count));

    [JSInvokable]
    public Task HoldRow(int generation, long? row) => Calls.InOrder(() => grid.HoldRowAsync(generation, row));

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

    /// <summary>A call that carries an input of the user's that .NET carries out, numbered <paramref name="input"/> by the
    /// browser (ADR-0240 section 11): <paramref name="method"/>, with <paramref name="arguments"/>, made in its turn once
    /// the grid has noted that the number arrived, so it finds any number sent before it that never will.</summary>
    [JSInvokable]
    public Task<object?> Call(long input, string method, JsonElement arguments) => Calls.InOrder(() =>
    {
        grid.Reached(input);
        return Dispatch(method, arguments);
    });

    /// <summary>The browser had sent every numbered input up to <paramref name="input"/> as the user made another behind
    /// one still unanswered (ADR-0240 section 11): heard in its turn, so any that has not arrived by then never will.</summary>
    [JSInvokable]
    public Task InputsSent(long input) => Calls.InOrder(() => grid.InputsSent(input));

    /// <summary>A call the browser held, too large for a message of its own: <paramref name="method"/>, with the
    /// arguments taken from the browser by <paramref name="crossed"/>'s number, made in its turn. Only a call that can
    /// carry what the user or the Consumer made, a text, a paste, a list of values, a slot's view or a Selection of
    /// many ranges, is held, a numbered <see cref="Call"/> among them.</summary>
    [JSInvokable]
    public Task<object?> Held(string method, JsonElement crossed) => Calls.Held(crossed, arguments =>
    {
        if (method != nameof(Call))
            return Dispatch(method, arguments);
        grid.Reached(arguments[0].GetInt64());
        return Dispatch(arguments[1].GetString() ?? throw new InvalidOperationException("Internal: the browser held a numbered call without its method."), arguments[2]);
    });

    // A call whose arguments came as one JSON array, held or numbered: made as the method of that name makes it. A Sort
    // or Filter crosses only numbered, so it has no method of its own.
    private async Task<object?> Dispatch(string method, JsonElement arguments)
    {
        JsonElement At(int index) => arguments[index];
        string Text(int index) => At(index).GetString() ?? throw new InvalidOperationException($"Internal: the browser called {method} with no text at {index}.");
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
            case nameof(RunContextCommand):
                return await grid.RunContextCommandAsync(At(0).GetInt64(), At(1).GetInt32());
            case nameof(Complete):
                return await grid.CompleteAsync(Text(0), At(1).GetInt32());
            case nameof(Copy):
                return await grid.CopyAsync(At(0).GetInt64(), At(1));
            case nameof(Summarize):
                return await grid.SummarizeAsync(At(0).GetInt64(), At(1).ValueKind == JsonValueKind.Null ? null : At(1).GetInt32(), At(2));
            case nameof(FindError):
                return await grid.FindErrorAsync(At(0).GetInt64(), At(1).GetInt32(), At(2));
            case "SetSorts":
                await grid.SetSortsAsync(At(0).GetInt32(), Text(1), At(2).GetInt64());
                return null;
            case "SetFilter":
                await grid.SetFilterAsync(At(0).GetInt32(), Text(1), At(2).GetInt64());
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
                throw new InvalidOperationException($"Internal: the browser called {method} by its arguments, which the bridge never does.");
        }
    }
}
