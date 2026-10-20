using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// The Settled Mark (ADR-0220; ADR-0021's note of 2026-10-16). The grid's root carries
// `data-ex-settled` while every input the key gate has taken has been handled and its effect is in
// the DOM. The script writes the mark; its half of the conditions is what the gate already knows —
// no key held, no hand-off under way, no answer outstanding, a reveal's write and repaint over. The
// core's half is here, written by a render of its own on the root's first child (SettledStamp), so
// it cannot run ahead of the DOM:
//
// - data-ex-answered: the number of the last input whose handling is over, every input before it
//   included. The script numbers each input it sends — a key, a press on the rows, a paste, a
//   composition's start and text, a copy — and the input carries its number. An interop answer
//   alone would not do: Blazor Server stops rendering while ten batches are unacknowledged
//   (CircuitOptions.MaxBufferedUnacknowledgedRenderBatches) and sends an answer at once, so the
//   answer can reach the browser ahead of the render it answers for. A number the render carries
//   cannot. Nor is the render that carries an input's effect the end of it: what that render owes
//   the script — the editing mode, DOM focus for the editor, a reveal's offset — is told after it,
//   from OnAfterRenderAsync, once the browser has applied it. So the number is written once no
//   render of the root is waiting for its OnAfterRenderAsync to finish: at once, for an input whose
//   handling rendered nothing and owes nothing; otherwise at the end of that OnAfterRenderAsync
//   (StampAnsweredInputs).
// - data-ex-owing: written while a render the grid owes is waiting on a timer of its own — the
//   error message after the Focus stands still (PopoverDelay), the description of a selection
//   (SettleDelay), and the Placeholders filling after a scroll (SettleDelay); and while a sentence
//   said again is put back by the render after the one that emptied it (Announce).
// - data-ex-escape-leaves: an Escape on the root would leave the grid now, releasing Tab — no popover
//   or menu, no Interactive cell, no Copied Range and no edit to peel first (ADR-0230 section 1). Said
//   with the inputs answered, so the gate trusts it for an Escape typed once every input that could
//   add a layer is answered, and takes the release at the keydown.
// - data-ex-focus-owed: the core owes DOM focus to a control it will focus itself — an edit's surface
//   not yet painted, a popover's first control, a filter's control while its values are on their way.
//   With the inputs a wait began behind all answered and nothing owed, a keyboard that has not
//   arrived will not, and the script ends the wait on that rather than on a clock (ADR-0240 section 4).
//
// Neither reaches a row, nor renders the root: the stamp renders alone, and the rows skip their
// render as before (ADR-0003). Nothing per cell crosses, and no message is sent for them (ADR-0027's
// P1-P9). Rendered by the root instead, they cost two renders of the whole grid per input, which the
// measurement of 2026-10-16 saw on WebAssembly.
public partial class ExGrid<TRow>
{
    // The element that says the core's half (data-ex-answered, data-ex-owing).
    private SettledStamp? _settledStamp;

    // The stamp as the root's markup places it: one fragment, kept, so the root's renders find the same
    // component with no parameter to give it, and never render it.
    private RenderFragment? _settledStampFragment;

    private RenderFragment SettledStampFragment => _settledStampFragment ??= builder =>
    {
        builder.OpenComponent<SettledStamp>(0);
        builder.AddComponentReferenceCapture(1, stamp => _settledStamp = (SettledStamp)stamp);
        builder.CloseComponent();
    };

    // The last input answered, every one before it included.
    private int _answeredUpTo;

    // Inputs answered ahead of one before them still being answered: an input whose handling
    // waited on a Consumer can finish after one sent behind it.
    private readonly HashSet<int> _inputsAnsweredAhead = [];

    // The root's renders built (counted in its markup), and those whose OnAfterRenderAsync has run to
    // its end. While the first is ahead, a render's after-work is still to be told.
    private int _rendersBuilt;
    private int _rendersAfterRendered;

    // The renders a timer of the grid's own owes (data-ex-owing).
    private bool _messageOwed;
    private bool _announcementOwed;
    private bool _placeholdersOwed;

    /// <summary>Whether a render is waiting on one of the grid's own timers (data-ex-owing).</summary>
    private bool RenderOwed => _messageOwed || _announcementOwed || _placeholdersOwed || _announcePending is not null;

    /// <summary>
    /// The input the script numbered <paramref name="input"/> has been handled: the stamp says so once
    /// every input before it has been handled too, and once whatever its handling rendered has had its
    /// after-work told. An input the script did not number (0) is no one's.
    /// </summary>
    private void InputAnswered(int input)
    {
        if (input <= 0 || _disposed || input <= _answeredUpTo)
            return;
        if (input != _answeredUpTo + 1)
        {
            _inputsAnsweredAhead.Add(input);
            return;
        }
        _answeredUpTo = input;
        while (_inputsAnsweredAhead.Remove(_answeredUpTo + 1))
            _answeredUpTo++;
        // State that only the root's next render tells: that render, whose OnAfterRenderAsync then
        // says the input was answered.
        if (_rendersBuilt == _rendersAfterRendered && OwesAfterRenderWork())
        {
            _suppressRender = false;
            StateHasChanged();
            return;
        }
        ShowSettledStamp();
    }

    // The Input Order (ADR-0240): the numbered inputs, handled one at a time by number. It tells each
    // number, in order, once handled or voided (InputAnswered).
    private InputOrder? _inputOrder;

    private InputOrder Inputs => _inputOrder ??= new InputOrder(InputAnswered,
        (Services.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger(InputOrder.JournalCategory), _idPrefix);

    /// <summary>The input's handling, in its place in the Input Order (ADR-0240): started once every
    /// input numbered before it has been handled, and finished before any numbered after it starts.
    /// Its number is then answered, whether it succeeded or threw: a failure has been reported on its
    /// own path, and the mark is not held by it. An input with no number (0) is handled at once.</summary>
    /// <remarks>Every call of the script's that carries a number reaches here as it arrives, so the number
    /// is also marked as having reached the core (InputReached).</remarks>
    private Task AnsweredAsync(Func<Task> handling, int input)
    {
        InputReached(input);
        return Inputs.RunAsync(input, handling);
    }

    /// <summary><see cref="AnsweredAsync(Func{Task}, int)"/> for a handling that answers a value.</summary>
    private Task<T> AnsweredAsync<T>(Func<Task<T>> handling, int input)
    {
        InputReached(input);
        return Inputs.RunAsync(input, handling);
    }

    /// <summary>An input handled at once, with nothing to wait on, in its place in the Input Order.</summary>
    private Task AnsweredAsync(Action handling, int input)
        => AnsweredAsync(() => { handling(); return Task.CompletedTask; }, input);

    /// <summary>A message carrying the input numbered <paramref name="input"/> has reached the core
    /// (ADR-0240 section 11). Called first by every call of the script's that carries a number, before
    /// anything waits: a number before it that has not reached the core was sent while the circuit was
    /// down, and never will (InputsLost).</summary>
    private void InputReached(int input)
    {
        if (input > 0 && !_disposed && Inputs.Reached(input) is { } lost)
            InputsLost(lost.First, lost.Last);
    }

    /// <summary>
    /// The script had sent every input up to <paramref name="input"/> when it sent this, and is holding the
    /// ones it has taken since behind an answer (ADR-0240 section 11). An input sent while the Server circuit
    /// was down never reaches the core, and the answer it is held behind never comes; with nothing numbered
    /// arriving behind it, this is how the core learns that it was lost.
    /// </summary>
    /// <param name="input">The last number the script had sent.</param>
    /// <remarks>Not the contract (ADR-0201): the grid's script calls it, so it is public, but it is
    /// hidden from a Consumer's editor and may change in any version.</remarks>
    [JSInvokable, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void InputsSent(int input)
    {
        if (input > 0 && !_disposed && Inputs.Reached(input, carried: false) is { } lost)
            InputsLost(lost.First, lost.Last);
    }

    /// <summary>
    /// The inputs numbered <paramref name="first"/> to <paramref name="last"/> were sent while the Server
    /// circuit was down and never reached the core (ADR-0240 section 11). They are void, so the inputs behind
    /// them go on; they are refused aloud, in their place, never replayed against whatever stands now; and
    /// the script is told, so that what it waits on for them ends with nothing.
    /// </summary>
    private void InputsLost(int first, int last)
    {
        _ = Inputs.AfterReceivedAsync(() =>
        {
            if (_disposed)
                return Task.CompletedTask;
            var words = RefusalWords(KeyRefusalLabelIds.LostOnTheWay);
            if (!string.IsNullOrWhiteSpace(words))
            {
                Announce(words);
                _suppressRender = false;
                StateHasChanged();
            }
            return Task.CompletedTask;
        });
        _ = TellInputsLostAsync(first, last);
    }

    private async Task TellInputsLostAsync(int first, int last)
    {
        if (_scrollHandle is not { } handle)
            return;
        try
        {
            await handle.InvokeVoidAsync("inputsLost", first, last);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
        }
    }

    /// <summary>The input numbered <paramref name="input"/> will not be handled: told of ahead of an
    /// event that never came. The Input Order moves past it (ADR-0240).</summary>
    private void InputVoid(int input)
    {
        if (input > 0)
            Inputs.Void(input);
    }

    /// <summary>Called last in OnAfterRenderAsync, with the renders built when it began: those have
    /// told what they owed, and the stamp says what is answered and owed now.</summary>
    private void StampAnsweredInputs(int rendersAfterRendered)
    {
        if (rendersAfterRendered > _rendersAfterRendered)
            _rendersAfterRendered = rendersAfterRendered;
        ShowSettledStamp();
    }

    /// <summary>The stamp says what is answered and owed, unless a render of the root is still
    /// waiting for its after-work: its OnAfterRenderAsync says it then.</summary>
    private void ShowSettledStamp()
    {
        if (_disposed || _rendersBuilt != _rendersAfterRendered)
            return;
        _settledStamp?.Show(_answeredUpTo, RenderOwed, EscapeLeaves, FocusOwed);
    }

    /// <summary>Whether the core owes DOM focus to a control it will focus itself (data-ex-focus-owed):
    /// the edit's surface, or a control of one of its popovers — or that a substituted Chrome's popover
    /// contents, asked to take it, have not yet (TakePopoverFocus).</summary>
    private bool FocusOwed => _editorNeedsFocus || _popoverFocusPending != PopoverFocus.None
        || (_chromeFocusOwed && AnyPopoverOpen) || (_consumerFocusOwed && _consumerPopover is not null);

    /// <summary>A timer's render is no longer owed.</summary>
    private void OwedRenderDone(ref bool owed)
    {
        if (!owed)
            return;
        owed = false;
        ShowSettledStamp();
    }
}
