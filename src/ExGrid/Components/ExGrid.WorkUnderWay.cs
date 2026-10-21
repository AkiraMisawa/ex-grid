using Microsoft.JSInterop;

namespace ExGrid.Components;

// Work of the Consumer's own in the Input Order (ADR-0240 section 6; ADR-0330 section 10). A Consumer's
// command that no control numbered — an opening of a file, a save — takes one place in the order: after
// every input received so far, before any received after it. One that the user may cancel is placed from
// the moment it takes its place until it ends, and the Settled Stamp says so (data-ex-work-placed, the
// number of the latest such work, and no data-ex-escape-leaves). Once its turn starts, the root is busy
// (aria-busy), names it (data-ex-work) and is painted as a paste under way is.
//
// An Escape cancels it as an Escape cancels a paste (ADR-0240 section 12), decided by what the Escape was
// taken against — the stamp the script read at its keydown — never by when it arrives:
// - A plain Escape (no modifier, no repeat) typed against a stamp that named placed work is numbered at its
//   keydown as a cancel naming that work, held or not, and heard at once (OnWorkCancelAsync). The core
//   cancels the first work placed up to that one that is not cancelled yet; when every such work still
//   placed is cancelled already, the Escape is consumed and answered as the work ends; when it has ended,
//   the Escape says it came too late (it landed) or found nothing to cancel. So a second Escape gets the
//   same answer whether it arrives before the work ends or after. That Escape does nothing else.
// - An Escape typed against a stamp that named no work is an ordinary key (OnKeyAsync), however much work
//   has been placed by the time it arrives: it waits behind that work, and does what it was typed to do.
// The work's token is cancelled; work that has not passed the point where it lands ends in
// OperationCanceledException and is answered as cancelled, and work that lands regardless was past it. In
// the Escape's turn the Consumer is told when the cancel came too late (the work landed) or found nothing
// to cancel: never swallowed silently.
public partial class ExGrid<TRow>
{
    /// <summary>Cancellable work, from the place it took in the order until it ends: its number, what
    /// cancels it, and what says that a cancel came too late.</summary>
    private sealed class WorkUnderWay(int id, Func<bool, Task> tooLate) : IDisposable
    {
        private readonly CancellationTokenSource _cancel = new();

        public int Id { get; } = id;

        public Func<bool, Task> TooLate { get; } = tooLate;

        public CancellationToken Token => _cancel.Token;

        public bool Cancelled => _cancel.IsCancellationRequested;

        /// <summary>The Escape whose cancel this was, by its number; 0 for none, or for the grid's disposal.</summary>
        public int CancelledBy { get; private set; }

        /// <summary>The Escapes, by number, that named it once it was cancelled already: answered as it ends,
        /// as they would be had they come after.</summary>
        public List<int> NamedAfterCancel { get; } = [];

        public void Cancel(int input)
        {
            if (_cancel.IsCancellationRequested)
                return;
            CancelledBy = input;
            _cancel.Cancel();
        }

        public void Dispose() => _cancel.Dispose();
    }

    /// <summary>How a cancellable work ended.</summary>
    private enum WorkEnd
    {
        /// <summary>It ran to its end: what it does has been done.</summary>
        Landed,

        /// <summary>It ended in <see cref="OperationCanceledException"/> on its cancelled token, changing nothing.</summary>
        Cancelled,

        /// <summary>It failed otherwise, or never ran (the grid was disposed).</summary>
        Failed,
    }

    // The cancellable works that have taken their place and not ended, in the order they took it; the one
    // whose turn is under way; and the count that numbers each.
    private readonly List<WorkUnderWay> _works = [];
    private WorkUnderWay? _workUnderWay;
    private int _worksStarted;

    // The works that ended lately, by number, with how and what says a cancel came after: a cancel naming one
    // (the stamp's data-ex-work-placed, read at the Escape's keydown) is answered from here.
    private readonly Dictionary<int, (bool Landed, Func<bool, Task> TooLate)> _worksEnded = [];

    // The Escapes, by number, whose cancel came too late (the work landed: true) or found nothing to cancel
    // (false), with what says so in their turn.
    private readonly Dictionary<int, (bool Landed, Func<bool, Task> TooLate)> _workCancelsTooLate = [];

    /// <summary>The number the root names cancellable work under way by (data-ex-work); null for none.</summary>
    private int? WorkUnderWayId => _workUnderWay?.Id;

    /// <summary>Whether cancellable work has taken its place and not ended: an Escape cancels it first.</summary>
    private bool WorkCancellable => _works.Count > 0;

    /// <summary>The number of the latest cancellable work placed and not ended (the stamp's
    /// data-ex-work-placed), which an Escape typed now names; 0 for none.</summary>
    private int WorkPlaced => _works.Count > 0 ? _works[^1].Id : 0;

    /// <summary>
    /// Runs <paramref name="handling"/>, work of the Consumer's own that no control numbered — a command
    /// from a button outside the grid, a service's call — in its place in the Input Order (ADR-0240
    /// section 6): after every input received so far, and before any received after it, which wait for it
    /// to end, its <c>await</c>s included. Unlike <see cref="InOrderAsync(Chrome.GridInputKind, Func{Task})"/>,
    /// it never claims a number told for a control's event. Called from inside the handling of an input
    /// under way, it runs at once, as part of that input. It may be called from any thread: it is taken on
    /// the renderer's synchronisation context, as a render is.
    /// </summary>
    /// <param name="handling">The work, run on the renderer's synchronisation context. The inputs behind it
    /// wait until it has finished.</param>
    /// <returns>A task that completes once <paramref name="handling"/> has run; it ends with nothing done
    /// once the grid is disposed.</returns>
    public Task InOrderAsync(Func<Task> handling)
    {
        ArgumentNullException.ThrowIfNull(handling);
        return InvokeAsync(() => _disposed ? Task.CompletedTask : Inputs.AfterReceivedAsync(handling, holds: true));
    }

    /// <summary>
    /// Runs <paramref name="handling"/>, work of the Consumer's own that may take a while — an opening of
    /// a file, still being read — in its place in the Input Order, as <see cref="InOrderAsync(Func{Task})"/>
    /// does, and lets the user cancel it with Escape (ADR-0240 section 12, ADR-0330 section 10). From the
    /// moment it takes its place — waiting for its turn or under way — until it ends, the Settled Stamp names
    /// it (<c>data-ex-work-placed</c>), no longer says that an Escape leaves the grid, and the Settled Mark is
    /// off. From its turn's start until it ends, the grid's root is busy (<c>aria-busy</c>), names it
    /// (<c>data-ex-work</c>) and is painted as a paste under way is (<c>--ex-pasting-bar</c>,
    /// <c>--ex-pasting-bar-height</c>, <c>--ex-pasting-cursor</c>). A plain Escape typed in the grid against a
    /// page whose stamp named the work cancels <paramref name="handling"/>'s token and does nothing else: it
    /// closes no editor and no popover. An Escape typed against a page whose stamp did not yet name it is an
    /// ordinary key, decided by what it was typed against and not by when it arrives: it waits behind the work
    /// and does what it was typed to do. It may be called from any thread: it is taken on the renderer's
    /// synchronisation context.
    ///
    /// <para>The work decides when it lands: it looks at the token up to that point, and ends in
    /// <see cref="OperationCanceledException"/> once it is cancelled, changing nothing; past that point it
    /// lands whatever the token says, synchronously, so no cancel falls between the look and the landing.
    /// What it does after landing — raising a Consumer's events — must not end in
    /// <see cref="OperationCanceledException"/>, which would read as cancelled; catch it and fail otherwise.
    /// An Escape that found the work landed is answered in its own turn by <paramref name="cancelledTooLate"/>
    /// with <see langword="true"/>; one that found nothing to cancel — the work had failed, had ended before
    /// the Escape came, or was cancelled already by an Escape before it — with <see langword="false"/>. Saying what the work did is the Consumer's: the
    /// grid says nothing of it. This is the one outcome that depends on timing, accepted because a cancel
    /// exists to race the work it cancels, and both outcomes are said.</para>
    ///
    /// <para>One cancellable work cannot run inside another: called from within <paramref name="handling"/>
    /// of work under way, it throws <see cref="InvalidOperationException"/>. Disposing of the grid cancels
    /// the token of the work under way, and a work still waiting never runs.</para>
    /// </summary>
    /// <param name="handling">The work, handed the token an Escape cancels, and run on the renderer's
    /// synchronisation context. The inputs behind it wait until it has finished.</param>
    /// <param name="cancelledTooLate">Says, in that Escape's turn, that an Escape came after the work had
    /// landed (<see langword="true"/>) or found nothing to cancel (<see langword="false"/>).</param>
    /// <returns>A task answering <see langword="true"/> once the work has run to its end, and
    /// <see langword="false"/> when an Escape cancelled it (it ended in
    /// <see cref="OperationCanceledException"/> on the token), or once the grid is disposed with nothing
    /// done. Any other failure of the work is the task's.</returns>
    /// <exception cref="InvalidOperationException">Called from within cancellable work under way.</exception>
    public async Task<bool> CancellableInOrderAsync(Func<CancellationToken, Task> handling, Func<bool, Task> cancelledTooLate)
    {
        ArgumentNullException.ThrowIfNull(handling);
        ArgumentNullException.ThrowIfNull(cancelledTooLate);
        var ran = false;
        await InvokeAsync(async () => ran = await CancellableAsync(handling, cancelledTooLate));
        return ran;
    }

    private async Task<bool> CancellableAsync(Func<CancellationToken, Task> handling, Func<bool, Task> cancelledTooLate)
    {
        if (_disposed)
            return false;
        if (_workUnderWay is not null && Inputs.InFlow)
        {
            throw new InvalidOperationException(
                "Cancellable work cannot run inside cancellable work under way: one Escape could not say which it cancels. Run it after the work under way has ended, or as part of it.");
        }
        var work = new WorkUnderWay(++_worksStarted, cancelledTooLate);
        _works.Add(work);
        // The stamp names the work, stops saying that an Escape leaves, and owes the Mark, from here on.
        ShowSettledStamp();
        var end = WorkEnd.Failed;
        try
        {
            await Inputs.AfterReceivedAsync(async () =>
            {
                if (_disposed)
                    return;
                StartWork(work);
                try
                {
                    await handling(work.Token);
                    end = WorkEnd.Landed;
                }
                catch (OperationCanceledException) when (work.Cancelled)
                {
                    end = WorkEnd.Cancelled;
                }
                finally
                {
                    // Ended in its own turn, so the Escapes behind it find how it ended.
                    EndWork(work, end);
                }
            }, holds: true);
        }
        finally
        {
            // One that never ran: the grid went first.
            if (_works.Contains(work))
                EndWork(work, WorkEnd.Failed);
        }
        return end == WorkEnd.Landed;
    }

    /// <summary>
    /// A plain Escape typed against a page whose Settled Stamp named <paramref name="work"/> as the latest
    /// cancellable work placed (<c>data-ex-work-placed</c>, read at its keydown, held or not), numbered
    /// <paramref name="input"/> as a cancel there (ADR-0240 section 12, ADR-0330 section 10). Heard at once,
    /// outside the Input Order, and decided only from what it names: the first work placed up to
    /// <paramref name="work"/> that is not cancelled yet is cancelled; when every such work still placed is
    /// cancelled already, the Escape is consumed and answered as that work ends; when it has ended, its turn
    /// says the cancel came too late (it landed) or found nothing to cancel. The Escape does
    /// nothing else.
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="work">The number the stamp named the latest placed work by (<c>data-ex-work-placed</c>)
    /// at the Escape's keydown.</param>
    /// <param name="input">The number the script gave the Escape, which the root names once it has been
    /// answered (ADR-0220); 0 for none.</param>
    /// <param name="fieldText">The browser field's text carried by this input (ADR-0240 section 5).</param>
    /// <returns>A task that completes once the Escape's turn has been taken.</returns>
    /// <remarks>Not the contract (ADR-0201): the grid's script calls it, so it is public, but it is
    /// hidden from a Consumer's editor and may change in any version.</remarks>
    [JSInvokable, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public Task OnWorkCancelAsync(int work, int input = 0, GridInputText? fieldText = null)
    {
        if (!_disposed && work > 0 && input > 0)
            CancelNamed(work, input);
        return AnsweredAsync(() => WorkCancelTurnAsync(input), input, fieldText);
    }

    /// <summary>The cancel an Escape numbered <paramref name="input"/> makes of the work placed up to
    /// <paramref name="work"/>, decided from that number alone.</summary>
    private void CancelNamed(int work, int input)
    {
        WorkUnderWay? cancelledAlready = null;
        foreach (var placed in _works)
        {
            if (placed.Id > work)
                break;
            if (!placed.Cancelled)
            {
                // Answered once the work has ended (EndWork): too late if it landed regardless.
                placed.Cancel(input);
                return;
            }
            cancelledAlready ??= placed;
        }
        // Consumed: what it names was cancelled already, by an Escape before it. Answered as the work ends,
        // as it would be once the work had ended: whether it arrives before or after, the same.
        if (cancelledAlready is not null)
            cancelledAlready.NamedAfterCancel.Add(input);
        else if (_worksEnded.TryGetValue(work, out var ended))
            _workCancelsTooLate[input] = (ended.Landed, ended.TooLate);
    }

    /// <summary>The turn of an Escape that named work: after the work, it says when the cancel came too late
    /// or found nothing to cancel, and does nothing else.</summary>
    private async Task WorkCancelTurnAsync(int input)
    {
        if (_disposed || !_workCancelsTooLate.Remove(input, out var tooLate))
            return;
        await tooLate.TooLate(tooLate.Landed);
    }

    /// <summary>The work starts in its turn: the root is busy, names it, and is painted so.</summary>
    private void StartWork(WorkUnderWay work)
    {
        _workUnderWay = work;
        _suppressRender = false;
        StateHasChanged();
    }

    /// <summary>The work has ended: the root is no longer busy. A cancel that found it landed, or found it
    /// ended otherwise than by that cancel, came too late, and its Escape's turn says so; as does one that
    /// arrives from here on naming it.</summary>
    private void EndWork(WorkUnderWay work, WorkEnd end)
    {
        _works.Remove(work);
        var underWay = ReferenceEquals(_workUnderWay, work);
        if (underWay)
            _workUnderWay = null;
        var landed = end == WorkEnd.Landed;
        if (work.CancelledBy > 0 && end != WorkEnd.Cancelled)
            _workCancelsTooLate[work.CancelledBy] = (landed, work.TooLate);
        foreach (var input in work.NamedAfterCancel)
            _workCancelsTooLate[input] = (landed, work.TooLate);
        _worksEnded[work.Id] = (landed, work.TooLate);
        _worksEnded.Remove(work.Id - 64);
        work.Dispose();
        if (_disposed || !underWay)
            return;
        _suppressRender = false;
        StateHasChanged();
        ShowSettledStamp();
    }

    /// <summary>The grid is going: the work under way is cancelled, and a work still waiting never runs.</summary>
    private void CancelWorksOnDispose()
    {
        // A copy: a cancel runs the token's callbacks inline, and a work they end leaves the list (EndWork).
        foreach (var work in _works.ToArray())
            work.Cancel(0);
    }
}
