using Microsoft.JSInterop;

namespace ExPivot.MudBlazor;

/// <summary>Takes DOM focus for ExPivot's requests (ADR-0060): once for each request number not yet
/// acted on, as the built-in views do, through the control's own <c>FocusAsync</c>.</summary>
internal static class MudPivotFocus
{
    /// <summary>Focuses through <paramref name="focus"/> when <paramref name="request"/> is new.</summary>
    internal static Task TakeAsync(int request, ref int acted, Func<ValueTask>? focus)
    {
        if (request == 0 || request == acted || focus is null)
            return Task.CompletedTask;
        acted = request;
        return FocusAsync(focus);
    }

    private static async Task FocusAsync(Func<ValueTask> focus)
    {
        try
        {
            await focus();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
            // The circuit or the control went away first; there is nothing left to focus.
        }
    }
}
