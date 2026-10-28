using System.Text.Json;
using Microsoft.JSInterop;

namespace ExGrid.Script;

/// <summary>
/// The calls a Blazor Script adapter's module makes into .NET, in the order it made them, .NET's half (ADR-0005, as
/// amended 2026-10-28): Blazor Server closes the circuit on a message to .NET larger than its hub takes, 32 KB unless
/// the application raises it, so the browser holds a call whose arguments are larger than half that, and sends only
/// its method, its number and its length (<c>Held</c>). .NET takes the arguments from the module's handle, as a
/// stream, gzip-compressed where the browser could compress, and makes the call in its turn: every call that arrived
/// after it waits until it is made, so a held paste is handed before the selection that followed it, and a held slot
/// mounted before the update that names it. Compiled into each adapter, so every one crosses a large call alike; the
/// browser's half is the Shared Script's <c>crossingCalls</c> (<c>src/npm/blazor/src/held-messages.ts</c>).
/// </summary>
/// <remarks>
/// Used on the component's dispatcher only, as Blazor calls a <see cref="JSInvokableAttribute"/> method. A call is
/// made once every held call before it is: what it starts runs on, and only the taking of the arguments is waited for.
/// </remarks>
internal sealed class HeldCalls
{
    // The calls that arrived while a held call before them was being taken, in the order they arrived.
    private readonly Queue<Func<Task>> _waiting = new();
    private readonly TaskCompletionSource<IJSObjectReference> _handle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _taking;

    /// <summary>The module's handle, which a held call's arguments are taken from, once it is attached: a call held as
    /// the module attached waits for it.</summary>
    public void Attached(IJSObjectReference handle) => _handle.TrySetResult(handle);

    /// <summary>The component let go of the module: a held call not yet taken never is, and is cancelled.</summary>
    public void Stopped() => _handle.TrySetCanceled();

    /// <summary>A call that crossed as it is: made now, or, while a held call before it is being taken, once it has
    /// been made.</summary>
    public Task<T> InOrder<T>(Func<Task<T>> call)
    {
        if (!_taking)
            return Started(call);
        var started = new TaskCompletionSource<Task<T>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiting.Enqueue(() =>
        {
            started.SetResult(Started(call));
            return Task.CompletedTask;
        });
        return started.Task.Unwrap();
    }

    /// <summary>A call that crossed as it is and answers nothing, made in its turn as <see cref="InOrder{T}"/> makes one.</summary>
    public Task InOrder(Func<Task> call) => InOrder<bool>(async () =>
    {
        await call();
        return true;
    });

    /// <summary>A call that crossed as it is, answers nothing and is done once made, made in its turn.</summary>
    public Task InOrder(Action call) => InOrder<bool>(() =>
    {
        call();
        return Task.FromResult(true);
    });

    /// <summary>A held call, <paramref name="crossed"/> its number and length: its arguments, a JSON array, are taken
    /// and handed to <paramref name="call"/> ahead of every call that arrived after it.</summary>
    public Task<object?> Held(JsonElement crossed, Func<JsonElement, Task<object?>> call)
    {
        var started = new TaskCompletionSource<Task<object?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiting.Enqueue(async () =>
        {
            JsonElement arguments;
            try
            {
                arguments = await TakeAsync(await _handle.Task, crossed);
            }
            catch (OperationCanceledException)
            {
                // The component let go of the module first: the call is cancelled, not failed.
                started.SetCanceled();
                return;
            }
            catch (Exception ex)
            {
                started.SetException(ex);
                return;
            }
            started.SetResult(Started(() => call(arguments)));
        });
        if (!_taking)
            _ = TakeInTurnAsync();
        return started.Task.Unwrap();
    }

    private async Task TakeInTurnAsync()
    {
        _taking = true;
        try
        {
            while (_waiting.TryDequeue(out var next))
                await next();
        }
        finally
        {
            _taking = false;
        }
    }

    // The call started: what it throws before its first await is its task's failure, as an async method's is.
    private static Task<T> Started<T>(Func<Task<T>> call)
    {
        try
        {
            return call();
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }

    /// <summary>Whether <paramref name="crossed"/>, an answer from the browser, is a held message's number and length
    /// in place of the answer itself, to be taken with <see cref="TakeAsync"/>.</summary>
    public static bool IsHeld(JsonElement crossed) => crossed.ValueKind == JsonValueKind.Object && crossed.TryGetProperty("held", out _);

    /// <summary>The message <paramref name="crossed"/> stands for, taken from <paramref name="handle"/> by its
    /// number.</summary>
    public static async Task<JsonElement> TakeAsync(IJSObjectReference handle, JsonElement crossed)
    {
        var held = crossed.GetProperty("held").GetInt32();
        var length = crossed.GetProperty("bytes").GetInt64();
        if (length is < 0 or > MostHeldBytes)
            throw new InvalidDataException($"A call of {length} bytes is more than the {MostHeldBytes} a held call may carry, and is not taken.");
        // Taken whatever the circuit's timeout: a large message crosses at about one chunk per round trip.
        var reference = await handle.InvokeAsync<IJSStreamReference>("take", CancellationToken.None, [held]);
        await using (reference)
        {
            var text = await CrossedText.ReadAsync(reference, length, "Internal: a held message", CancellationToken.None);
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
    }

    // The most a held message may declare: what stops one client filling the server's memory with one call, and well
    // above the most a paste under the default cap of 16 MB takes once written as JSON.
    private const long MostHeldBytes = 256L * 1024 * 1024;
}
