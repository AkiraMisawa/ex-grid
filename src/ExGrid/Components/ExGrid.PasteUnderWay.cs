using ExGrid.Chrome;
using ExGrid.Clipboard;
using ExGrid.Selection;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// A paste under way (ADR-0240 section 12). While a paste's streams are read and parsed, the inputs behind
// it wait in the Input Order, and on a slow circuit that is seconds: the root says so (aria-busy, and the
// ex-pasting class a Visual Token paints) and the live region says it once. An Escape typed meanwhile
// cancels it: the script numbers that Escape as a cancel naming the paste, and the call is heard at once,
// outside the order. A paste that has not raised its Edit Intent stops reading and is refused aloud
// (PasteRefusalReason.Cancelled); one that has stands, and the Escape's turn says so. The Escape's turn
// does nothing else. This is the one outcome that depends on timing, accepted because a cancel exists to
// race the work it cancels, and both outcomes are said aloud.
public partial class ExGrid<TRow>
{
    /// <summary>A paste being read or parsed: its number, what cancels its reading, and whether it has
    /// raised its Edit Intent, after which a cancel finds nothing.</summary>
    private sealed class PasteUnderWay(int input) : IDisposable
    {
        private readonly CancellationTokenSource _cancel = new();

        public int Input { get; } = input;

        public CancellationToken Token => _cancel.Token;

        public bool Cancelled => _cancel.IsCancellationRequested;

        public bool IntentRaised { get; set; }

        /// <summary>Cancels the reading, unless the Edit Intent has been raised; answers whether it did.</summary>
        public bool TryCancel()
        {
            if (IntentRaised)
                return false;
            _cancel.Cancel();
            return true;
        }

        public void Dispose() => _cancel.Dispose();
    }

    // The paste under way, from its start in the order until it lands or is refused.
    private PasteUnderWay? _pasteUnderWay;

    // Pastes cancelled before their turn came, by number, and the cancels that came too late, by the
    // Escape's number, whose turn says so.
    private readonly HashSet<int> _pastesCancelledAhead = [];
    private readonly HashSet<int> _cancelsTooLate = [];

    // What the live region said before the paste said it was under way, and the ranges it had described.
    private (string Text, bool DescribesSelection, IReadOnlyList<SelectionRange> Ranges) _saidBeforePaste;

    // The last paste the order finished, so a cancel naming it is known to have come too late.
    private int _lastPasteDone;

    /// <summary>Whether a paste is being read or parsed: the root is busy (aria-busy) and painted so.</summary>
    private bool Pasting => _pasteUnderWay is not null;

    /// <summary>
    /// An Escape typed while the paste numbered <paramref name="paste"/> was under way, numbered
    /// <paramref name="input"/> as a cancel (ADR-0240 section 12). Heard at once, outside the Input Order:
    /// a paste still reading or parsing, or still waiting for its turn, is cancelled and refused aloud as
    /// <see cref="PasteRefusalReason.Cancelled"/>. One that has raised its Edit Intent stands, and this
    /// input's turn says so. The Escape does nothing else: it closes no editor and no popover.
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="paste">The number of the paste to cancel.</param>
    /// <param name="input">The number the script gave the Escape, which the root names once it has been
    /// answered (ADR-0220); 0 for none.</param>
    /// <returns>A task that completes once the Escape's turn has been taken.</returns>
    /// <remarks>Not the contract (ADR-0201): the grid's script calls it, so it is public, but it is
    /// hidden from a Consumer's editor and may change in any version.</remarks>
    /// <param name="fieldText">The browser field's text carried by this input (ADR-0240 section 5).</param>
    [JSInvokable, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public Task OnPasteCancelAsync(int paste, int input = 0, GridInputText? fieldText = null)
    {
        if (!_disposed && paste > 0)
        {
            if (_pasteUnderWay is { } underWay && underWay.Input == paste)
            {
                if (!underWay.TryCancel())
                    _cancelsTooLate.Add(input);
            }
            else if (paste <= _lastPasteDone)
                _cancelsTooLate.Add(input);
            else
                _pastesCancelledAhead.Add(paste);
        }
        return AnsweredAsync(() =>
        {
            if (_disposed || !_cancelsTooLate.Remove(input))
                return;
            Announce(RefusalWords(GridLabelIds.PasteAlreadyLanded));
            _suppressRender = false;
            StateHasChanged();
        }, input, fieldText);
    }

    /// <summary>The paste numbered <paramref name="input"/> starts in its turn: the root is busy, and the
    /// live region says so once. Answers what cancels it, already cancelled if an Escape came first.</summary>
    private PasteUnderWay StartPaste(int input)
    {
        var underWay = new PasteUnderWay(input);
        _pasteUnderWay = underWay;
        if (input > 0 && _pastesCancelledAhead.Remove(input))
            underWay.TryCancel();
        _saidBeforePaste = (_announcement, _announcementDescribesSelection, _announcedRanges);
        Announce(RefusalWords(GridLabelIds.PasteUnderWay));
        _suppressRender = false;
        StateHasChanged();
        return underWay;
    }

    /// <summary>The paste numbered <paramref name="input"/> was refused before it was under way: a cancel
    /// that named it ahead of its turn is dropped, and one still to come finds it done.</summary>
    private void PasteDoneUnread(int input)
    {
        _pastesCancelledAhead.Remove(input);
        if (input > _lastPasteDone)
            _lastPasteDone = input;
    }

    /// <summary>The paste has landed or been refused: the root is no longer busy, and the sentence that
    /// said it was under way is taken back unless something has been said since.</summary>
    private void EndPaste(PasteUnderWay underWay)
    {
        if (!ReferenceEquals(_pasteUnderWay, underWay))
            return;
        _pasteUnderWay = null;
        if (underWay.Input > _lastPasteDone)
            _lastPasteDone = underWay.Input;
        underWay.Dispose();
        if (_disposed)
            return;
        if (_announcement == RefusalWords(GridLabelIds.PasteUnderWay))
        {
            // Nothing said since: the region goes back to what it said before, and the Selection is
            // described as if the paste had said nothing, a description it stood down included.
            (_announcement, _announcementDescribesSelection, _announcedRanges) = _saidBeforePaste;
            _announcePending = null;
            ScheduleAnnouncement();
        }
        _suppressRender = false;
        StateHasChanged();
    }
}
