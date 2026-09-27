using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// A press on the rows keeps its place among held keys (ADR-0021/0010, ED-22). While keys are
// held, or a change of editing mode is being answered, the listener holds a primary press on
// the rows, and its release, and replays them in order behind the keys typed before them. The
// keys typed after a press are handed on only once the core has answered it — here: the press
// may commit an open edit and wait on the Consumer hearing it, and the keys after it must be
// gated against the mode it leaves, never the one it found.
public partial class ExGrid<TRow>
{
    // The press or release on the rows the core heard last, while the core is answering it.
    private Task _pressAnswer = Task.CompletedTask;

    private Task OnMouseDown(MouseEventArgs e) => _pressAnswer = AnswerPressAsync(e);

    private Task OnMouseUp(MouseEventArgs e) => _pressAnswer = AnswerReleaseAsync(e);

    /// <summary>
    /// Completes once the press or release on the rows the core heard last has been answered
    /// in full (ADR-0021/0010): the commit it made, the Consumer hearing it, and the editing
    /// mode the key gate was told. The grid's listener asks it after replaying a held press,
    /// before it hands on the keys held behind it. The listener replays the press as an event
    /// and asks this straight after, so the press is always heard first. A press that failed
    /// has been reported on its own path, and is answered all the same.
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    [JSInvokable]
    public Task PressAnsweredAsync()
        => _disposed
            ? Task.CompletedTask
            : _pressAnswer.ContinueWith(static _ => { }, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
