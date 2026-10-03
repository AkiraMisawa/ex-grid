using Microsoft.AspNetCore.Components;

namespace ExSheet.Components;

/// <summary>
/// Renders nothing, and completes a wait once the browser has answered a render of it: on a
/// circuit, the browser's acknowledgement of the batch, after every event the browser sent before
/// it had that batch; in a browser, the render itself. ExSheet waits on it before it hears a
/// <c>focusout</c> as the keyboard leaving (ADR-0058): the <c>focusin</c> of a move inside the Sheet
/// is raised in the same task, but on a circuit it reaches the server as a message of its own.
/// </summary>
internal sealed class BrowserTurn : ComponentBase, IDisposable
{
    private TaskCompletionSource? _answer;

    /// <summary>Completes once the browser has answered a render asked for now; at once if this
    /// component is gone.</summary>
    public Task AnsweredAsync()
    {
        if (_answer is null)
        {
            _answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            StateHasChanged();
        }
        return _answer.Task;
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        var answer = _answer;
        _answer = null;
        answer?.TrySetResult();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _answer?.TrySetResult();
        _answer = null;
    }
}
