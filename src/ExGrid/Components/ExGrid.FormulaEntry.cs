using ExGrid.Cells;
using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExGrid.Components;

/// <summary>
/// The grid (ADR-0001). Its formula entry aids live in three parts beside the markup, as ExGrid
/// mechanism a Consumer switches on and gives meaning to (ADR-0051): this one reports the
/// editor's text and caret as the user types, paints the Consumer's candidates and hint as the
/// editor's Inner Popup and works the list's keys; <c>ExGrid.Pointing.cs</c> holds Point, and
/// <c>ExGrid.ReferenceOutlines.cs</c> the Reference Outlines (ADR-0057). The grid does not know
/// what a Formula is.
/// </summary>
/// <typeparam name="TRow">The Consumer's row type.</typeparam>
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Consumer declaration (ADR-0051): completion and the argument hint. While an edit is
    /// open, the editor's text and the caret are handed to this as the user types — in the Cell
    /// Editor and in the Formula Bar alike — and the answer is painted beneath the editor
    /// surface as a list of candidates and a hint. ↑/↓ choose, Tab accepts the chosen
    /// candidate and closes the list — the text it wrote is asked about again for its hint
    /// alone, and no list is shown for it — and Escape closes the list and leaves the edit open.
    /// A list takes no other key: where <see cref="PointAt"/> says a Reference can go at the
    /// caret, ← and → point past it and close it, as they point without it (ADR-0058).
    ///
    /// <para>A <see cref="ValueTask{TResult}"/>, so the answer can be synchronous — a sheet's
    /// own parser answers at once, allocating nothing and showing the list in the render the
    /// keystroke causes — or can take a round trip of its own, such as a server holding the
    /// names. An answer is shown only while the text it answers is still the editor's text: one
    /// that arrives after the user typed on is dropped (ADR-0051). A null answer, or one with
    /// nothing in it, shows nothing. Null — the default — reports nothing and paints
    /// nothing.</para>
    /// </summary>
    [Parameter] public Func<string, int, ValueTask<EditorCompletion?>>? CompleteEditorText { get; set; }

    // Where the caret stands in the uncommitted text, as the browser reported it with an input
    // or carried it with a key, or where the core put it after writing the text itself; -1
    // while it is not known (ADR-0051: reported and set, never inferred).
    private int _editCaret;

    // The browser's report of an input whose text the core has not heard yet: the listener's
    // report and Blazor's input event are two messages, and either can arrive first.
    private string? _reportedText;
    private int _reportedCaret = -1;

    // The caret the list on show answers: a key carrying another one makes it stale.
    private int _completionCaret = -1;

    // Text the core wrote into the editor, and where the listener is to place the caret once
    // the render carrying that text has landed (ADR-0051) — or the selection, from the caret
    // to its end, that F4's rewrite answered.
    private (string Text, int Caret, int End)? _caretToPlace;

    // The placement last asked for, until the listener has carried it out. Until then the
    // browser's caret in that text is its own — where setting the value left it — and a report
    // of it is not the user moving the caret (ADR-0051: reported and set, never inferred). The
    // number tells a placement apart from one made while it was in flight.
    private (string Text, int Caret, int End)? _caretPlacing;
    private int _caretPlacements;

    // The answer being shown, the candidate chosen in it, and the number of the last question
    // asked — an answer to any other is stale and dropped (ADR-0051, DC-18).
    private EditorCompletion? _completion;
    private int _completionSelected;
    private int _completionAsked;
    private Action<int>? _acceptFromChrome;

    // The set the key gate was last told, so a change is told once (ADR-0010).
    private string? _gateTold;

    /// <summary>Whether a list of candidates is showing — the state in which ↑/↓, Tab and
    /// Escape are the list's (ADR-0051). A hint alone takes no key.</summary>
    private bool CompletionListOpen
        => _editMode != EditMode.None && _completion is { Candidates.Count: > 0 };

    /// <summary>
    /// Whether a list is open over Point (ADR-0058, "What the tenth Windows run settled"): the
    /// caret stands where the Consumer says a Reference can go, outside Caret, whose arrows are
    /// the editor's. A list takes only ↑, ↓, Tab and Escape, so ← and → then point, as they would
    /// without it — an argument's value list after <c>,,</c> — where in a list of names they move
    /// the caret.
    /// </summary>
    private bool CompletionListOverPoint
        => CompletionListOpen && _editMode != EditMode.Caret && ReferenceText is not null
           && PointAt is { } pointAt && _editCaret >= 0 && _editCaret <= _editText.Length
           && pointAt(_editText, _editCaret);

    /// <summary>The key gate's set for the current state (ADR-0010): Caret leaves the arrows
    /// to the editor. While a list is open, in any state, only its ↑/↓ are claimed beside the
    /// editing keys, so ← and → move the caret — and ← and → too while it is open over Point,
    /// where they point (ADR-0058). Point claims Overwrite's keys and the four Shift+arrows,
    /// which extend the outline (ADR-0051's second round); and, while what Point wrote was written
    /// from outside and a Consumer hears the arrows, the Primary Modifier's arrows too (ADR-0058,
    /// "The keyboard").</summary>
    private string GateMode() => _editMode switch
    {
        EditMode.None => "none",
        _ when CompletionListOpen => CompletionListOverPoint ? "completionOverPoint" : "completion",
        EditMode.Point when PointedFromOutside && OnPointArrowFromOutside.HasDelegate => "pointed",
        EditMode.Point => "point",
        EditMode.Overwrite => "overwrite",
        _ => "caret",
    };

    /// <summary>Whether the listener reports the caret with each input (ADR-0051): only where
    /// completion or pointing is declared, so a grid that declares neither sends nothing more
    /// than before (DC-1).</summary>
    private bool ReportsCaret => CompleteEditorText is not null || PointAt is not null;

    /// <summary>
    /// The user changed the uncommitted text in either surface (ADR-0051): the list shown for
    /// the old text goes — it answers text that is no longer there — and the new text is
    /// reported. The caret is the one the listener reported with this input, if its report came
    /// first; otherwise it is not known yet, and the text is reported when it comes
    /// (<see cref="OnEditorCaretAsync"/>).
    /// </summary>
    private void TextTyped(string text) => TextTyped(text, caret: null);

    /// <summary>The same, with the caret carried by a key. Typing ends pointing: the outline
    /// goes, and Point gives way to <see cref="ModeAfterPointing"/>.</summary>
    private void TextTyped(string text, int? caret)
    {
        if (text == _editText)
            return;
        if (caret is null && string.Equals(text, _reportedText, StringComparison.Ordinal))
            caret = _reportedCaret;
        _reportedText = null;
        _editCaret = caret ?? -1;
        _editText = text;
        if (_pointer is not null || _pointedFromOutside || _editMode == EditMode.Point)
        {
            EndPointing();
            if (_editMode == EditMode.Point)
                _editMode = ModeAfterPointing;
        }
        RequestCompletion();
        TellConsumerPointState();
    }

    /// <summary>What Point gives way to when typing ends it (ADR-0051): Overwrite, whose arrows
    /// point again wherever the Consumer says a Reference can go — but Caret while the edit is in
    /// the Formula Bar, which is never in Overwrite: Excel's bar is always in Edit, and there
    /// Overwrite's Home and arrows would commit the Formula and move the Focus (ADR-0051,
    /// 2026-09-30; ED-29).</summary>
    private EditMode ModeAfterPointing => _editSurface == EditSurface.Bar ? EditMode.Caret : EditMode.Overwrite;

    /// <summary>
    /// Hands the current text and caret to the Consumer (ADR-0051). Whatever is showing is
    /// taken down first, and every earlier question is made stale: nothing answers the new
    /// text until its own answer comes. A synchronous answer is shown in the render already
    /// on its way.
    /// </summary>
    /// <param name="listed">Whether the answer's candidates are shown. Not for the text Tab
    /// wrote: Tab closes the list, and the answer for that text — the name just accepted, as a
    /// rule — shows its hint alone (ADR-0058, the tenth Windows run).</param>
    private void RequestCompletion(bool listed = true)
    {
        _completion = null;
        var asked = ++_completionAsked;
        // A caret not known yet is waited for, never guessed: a list for the wrong caret would
        // replace the wrong span (ADR-0051).
        if (CompleteEditorText is not { } complete || _editMode == EditMode.None || _editCaret < 0)
        {
            MarkGateIfMoved();
            return;
        }
        var text = _editText;
        var caret = _editCaret;
        ValueTask<EditorCompletion?> answer;
        try
        {
            answer = complete(text, caret);
        }
        catch (Exception ex)
        {
            MarkGateIfMoved();
            _ = DispatchExceptionAsync(ex);
            return;
        }
        if (answer.IsCompletedSuccessfully)
            ShowCompletion(asked, text, caret, answer.Result, listed);
        else
            _ = AwaitCompletionAsync(asked, text, caret, answer, listed);
        MarkGateIfMoved();
    }

    private async Task AwaitCompletionAsync(int asked, string text, int caret, ValueTask<EditorCompletion?> answer, bool listed)
    {
        EditorCompletion? result;
        try
        {
            result = await answer;
        }
        catch (Exception ex)
        {
            await DispatchExceptionAsync(ex);
            return;
        }
        await InvokeAsync(() =>
        {
            if (!ShowCompletion(asked, text, caret, result, listed))
                return;
            MarkGateIfMoved();
            // Not a UI event: an armed suppression would swallow this render (the OnKeyAsync
            // lesson).
            _suppressRender = false;
            StateHasChanged();
        });
    }

    /// <summary>Shows an answer, unless it is stale: asked before a later question, for text
    /// that is no longer the editor's, or after the edit closed (ADR-0051, DC-18). Its hint
    /// alone where it was asked for without a list.</summary>
    /// <returns>Whether anything was shown.</returns>
    private bool ShowCompletion(int asked, string text, int caret, EditorCompletion? answer, bool listed)
    {
        if (!listed && answer is { Candidates.Count: > 0 })
            answer = answer with { Candidates = [] };
        if (_disposed || asked != _completionAsked || _editMode == EditMode.None
            || !string.Equals(text, _editText, StringComparison.Ordinal)
            || answer is null || answer.IsEmpty)
        {
            return false;
        }
        _completion = answer;
        _completionSelected = answer.Candidates.Count > 0 ? 0 : -1;
        _completionCaret = caret;
        return true;
    }

    /// <summary>
    /// The caret, reported by the grid's own listener with each input in an editor surface and
    /// whenever it moves there (ADR-0051's second round): an input event carries no caret, and
    /// inferring it from the change is ambiguous where letters repeat. Sent only where
    /// completion or pointing is declared. The report and Blazor's input event are two
    /// messages: a report for the text the core holds sets the caret — a move with the text
    /// unchanged too, which asks the Consumer again and ends a pointing outline — and asks the
    /// Consumer if the text was waiting for it; a report for text not heard yet is kept for the
    /// input event that brings it. While a placement the core asked for is in flight, the
    /// browser's other caret in that text is not the user's and is not taken.
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public only
    /// because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="text">The editor surface's value as the input left it.</param>
    /// <param name="caret">Its <c>selectionStart</c>.</param>
    /// <param name="moved">Whether the user moved the caret in this very text — a press in it,
    /// or a caret key the browser carried out. Such a caret is the user's even while a
    /// placement is in flight: it was made after the core wrote the text, the listener does not
    /// carry that placement out, and the core does not wait for it.</param>
    [JSInvokable]
    public Task OnEditorCaretAsync(string text, int caret, bool moved = false)
    {
        if (_disposed || _editMode == EditMode.None || text is null || caret < 0 || caret > text.Length)
            return Task.CompletedTask;
        if (!string.Equals(text, _editText, StringComparison.Ordinal))
        {
            _reportedText = text;
            _reportedCaret = caret;
            return Task.CompletedTask;
        }
        // The browser's own caret in text the core wrote, before the placement has landed: not
        // the user's move, and the placement that follows is reported in its turn. The user's
        // own move in that text is newer than the placement, which the listener then leaves
        // undone (found on the Server host, Windows, fourth run: a press in the Formula Bar's
        // text straight after ↓ pointed was put back after the Reference, and pointing went on).
        if (_caretPlacing is { } placing && string.Equals(placing.Text, text, StringComparison.Ordinal))
        {
            if (!moved)
            {
                if (placing.Caret != caret)
                    return Task.CompletedTask;
            }
            else
            {
                _caretPlacing = null;
                if (_caretToPlace is { } unsent && string.Equals(unsent.Text, text, StringComparison.Ordinal))
                    _caretToPlace = null;
            }
        }
        if (caret == _editCaret)
            return Task.CompletedTask;
        // A caret-only move (ADR-0051's second round: reported whenever it moves) — ← or → in
        // Caret, a click inside the text. Completion and Point act at the new caret position.
        _editCaret = caret;
        // The outline's Reference was written where the caret stood: moved away, pointing ends,
        // as typing ends it, and the next click points from the new caret position. The user
        // has taken the caret into the text, so the edit is in Caret — Excel's Edit mode — and
        // the arrows move the caret from here, not the outline and not the Focus (ADR-0051's
        // third round).
        var pointingEnded = _pointer is not null || _pointedFromOutside || _editMode == EditMode.Point;
        if (pointingEnded)
        {
            EndPointing();
            if (_editMode == EditMode.Point)
                _editMode = EditMode.Caret;
            MarkGateIfMoved();
        }
        TellConsumerPointState();
        if (CompleteEditorText is null && !pointingEnded)
            return Task.CompletedTask;
        if (CompleteEditorText is not null)
            RequestCompletion();
        // Not a UI event: an armed suppression would swallow this render (the OnKeyAsync
        // lesson).
        _suppressRender = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>The core wrote the editor's text itself (ADR-0051): once the render carrying it
    /// has landed, the listener places the caret where the core says — after the inserted
    /// text, wherever in the text that is — or, for a rewrite that answered one, the selection
    /// from the caret to <paramref name="selectionEnd"/>.</summary>
    private void PlaceCaret(int? selectionEnd = null)
    {
        _caretToPlace = (_editText, _editCaret, selectionEnd ?? _editCaret);
        _caretPlacing = _caretToPlace;
        _caretPlacements++;
    }

    /// <summary>An edit opened (ADR-0051's second round): where the caret is reported, it is
    /// placed at the end of the opening text explicitly rather than assumed to be where the
    /// browser left it. Where it is not, nothing is sent (DC-1).</summary>
    private void OpenCaret()
    {
        if (ReportsCaret)
            PlaceCaret();
        else
            ForgetCaretPlacement();
    }

    /// <summary>The edit closed: no placement is owed, and none is waited for.</summary>
    private void ForgetCaretPlacement()
    {
        _caretToPlace = null;
        _caretPlacing = null;
    }

    /// <summary>Hands the caret (or the selection) the core placed to the listener, after the
    /// render that carries its text. The listener sets it only while the surface still holds
    /// that text: typed on since, the user's own caret stands.</summary>
    private async Task PushCaretAsync()
    {
        var place = _caretToPlace;
        var placement = _caretPlacements;
        _caretToPlace = null;
        try
        {
            if (place is not { } caret || _scrollHandle is null || _disposed || _editMode == EditMode.None)
                return;
            await _scrollHandle.InvokeVoidAsync("setCaret", caret.Text, caret.Caret, caret.End);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
        }
        finally
        {
            // Carried out — or never to be. A report sent before it reached the listener has
            // arrived by now: the call's answer follows it on the same channel.
            if (placement == _caretPlacements)
                _caretPlacing = null;
        }
    }

    /// <summary>Takes the list and the hint down, and makes any answer still on its way
    /// stale.</summary>
    private void CloseCompletion()
    {
        _completion = null;
        _completionSelected = -1;
        _completionAsked++;
        MarkGateIfMoved();
    }

    /// <summary>The key gate is told a change of its set after the render (ADR-0010): in Caret
    /// it claims the arrows only while a list is open, and pointing claims them in any
    /// state.</summary>
    private void MarkGateIfMoved()
    {
        if (!string.Equals(GateMode(), _gateTold, StringComparison.Ordinal))
            _jsEditingDirty = true;
    }

    /// <summary>
    /// A key the gate forwarded while a list is open (ADR-0051): ↑/↓ choose, Tab accepts and
    /// closes the list, Escape closes the list and leaves the edit open. A list takes no other
    /// key (ADR-0058, the tenth Windows run). Open over Point, ← and → close it and are not the
    /// list's: they point, as they would without it. Elsewhere the gate leaves ←, →, Home and End
    /// to the editor while a list is open; one that arrives all the same was claimed by a gate
    /// not yet told the list opened, and its default was prevented, so it only closes the
    /// list — it neither moves the caret nor commits. Every other key keeps its meaning, and
    /// whatever it does to the edit takes the list with it.
    /// </summary>
    /// <returns>Whether the key was the list's.</returns>
    private bool OnCompletionKey(string canonical)
    {
        if (!CompletionListOpen)
            return false;
        var last = _completion!.Candidates.Count - 1;
        switch (canonical)
        {
            case "ArrowDown":
                _completionSelected = Math.Min(last, _completionSelected + 1);
                StateHasChanged();
                return true;
            case "ArrowUp":
                _completionSelected = Math.Max(0, _completionSelected - 1);
                StateHasChanged();
                return true;
            case "Tab":
                AcceptCandidate(_completionSelected);
                StateHasChanged();
                return true;
            case "Escape":
                CloseCompletion();
                StateHasChanged();
                return true;
            case "ArrowLeft" or "ArrowRight" when CompletionListOverPoint:
                CloseCompletion();
                return false;
            case "ArrowLeft" or "ArrowRight" or "Home" or "End":
                CloseCompletion();
                StateHasChanged();
                return true;
        }
        return false;
    }

    /// <summary>
    /// Accepts a candidate (ADR-0051): its span of the text is replaced by what it writes, and
    /// the caret stands after it. The list closes (ADR-0058, the tenth Windows run): the new
    /// text is reported for its hint alone, so the hint for what was just accepted can follow,
    /// and the list does not come back on the name just written — after a table's name, or a
    /// column's without its <c>]</c>, it would, and Tab could never move on.
    /// </summary>
    private void AcceptCandidate(int index)
    {
        if (!CompletionListOpen || index < 0 || index >= _completion!.Candidates.Count)
            return;
        var (text, caret) = EditorTextRules.Accept(_editText, _completion.Candidates[index]);
        _editText = text;
        _editCaret = caret;
        PlaceCaret();
        RequestCompletion(listed: false);
        TellConsumerPointState();
    }

    /// <summary>What the completion box shows, or null: an answer standing for the open
    /// edit, with room for it inside the grid's box (ADR-0040).</summary>
    private EditorCompletion? CompletionShown()
        => _editMode != EditMode.None && _completion is { } shown && CompletionPlacement() is not null
            ? shown
            : null;

    /// <summary>The completion seam's contract (ADR-0051/0010): the answer, the chosen
    /// candidate, and the core's accept as the Chrome's control calls it.</summary>
    private EditorCompletionContext BuildCompletionContext(EditorCompletion shown)
        => new(
            shown.Candidates,
            _completionSelected,
            shown.Hint,
            _acceptFromChrome ??= index => FromChrome(() => AcceptCandidate(index)),
            CompletionOptionPrefix);

    /// <summary>The instance-prefixed id of the list (ADR-0018/0033).</summary>
    private string CompletionListId => _idPrefix + "completion";

    /// <summary>The prefix of each candidate's id, which the editor names with
    /// <c>aria-activedescendant</c>.</summary>
    private string CompletionOptionPrefix => _idPrefix + "completion-";

    /// <summary><c>aria-autocomplete</c> on the core's editor surfaces: "list" where completion
    /// is declared, absent otherwise — the default markup does not change (DC-1).</summary>
    private string? EditorAutocomplete => CompleteEditorText is null ? null : "list";

    /// <summary><c>aria-controls</c> on the core's editor surfaces: the built-in list while it
    /// is shown.</summary>
    private string? EditorControls => CompletionListOpen && CompletionShown() is not null ? CompletionListId : null;

    /// <summary>The chosen candidate's id while a list is open, for the core's editor; null
    /// otherwise.</summary>
    private string? CompletionActiveDescendant
        => CompletionListOpen && _completionSelected >= 0 && CompletionShown() is not null
            ? CompletionOptionPrefix + _completionSelected.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;

    /// <summary>
    /// Where the completion box stands (ADR-0040): beneath the editor surface the user is
    /// typing in — the cell's editor, or the Formula Bar's field — on the side of it with more
    /// room inside the grid, and bounded by that room. Null when neither side has a row's
    /// height of room: a list that cannot show one candidate is not shown.
    /// </summary>
    private (double Left, double Top, double Room, bool Above)? CompletionPlacement()
    {
        if (_editSurface == EditSurface.Bar && ShowFormulaBar)
        {
            // Beneath the bar, from the text field's left edge.
            var room = Math.Max(0, _visibleHeightPx);
            return room < _metrics.RowHeightPx ? null : (_metrics.NameBoxWidthPx, FormulaBarPx, room, false);
        }
        var (left, top) = CellCorner(_editingCell);
        var cellTop = Math.Clamp(BandHeightPx + top, BandHeightPx, _visibleHeightPx);
        var below = Math.Clamp(cellTop + _metrics.RowHeightPx, 0, _visibleHeightPx);
        var roomBelow = _visibleHeightPx - below;
        // Above, the room reaches the top of the grid's box, the Formula Bar's band included.
        var roomAbove = FormulaBarPx + cellTop;
        if (roomBelow >= roomAbove)
            return roomBelow < _metrics.RowHeightPx ? null : (left, FormulaBarPx + below, roomBelow, false);
        return roomAbove < _metrics.RowHeightPx ? null : (left, FormulaBarPx + cellTop, roomAbove, true);
    }

    /// <summary>The completion box's inline style: its place, its width's clamp and its
    /// bound, from the numbers it was placed with — nothing measured (ADR-0027/0040). Above
    /// the cell, it hangs from the cell's top by its own height.</summary>
    private string CompletionStyle()
    {
        if (CompletionPlacement() is not { } placement)
            return "display: none";
        var (left, width) = PopoverAcross(placement.Left);
        return placement.Above
            ? FormattableString.Invariant(
                $"left: {left}px; top: {placement.Top}px; transform: translateY(-100%); {width}; max-height: {placement.Room}px")
            : FormattableString.Invariant(
                $"left: {left}px; top: {placement.Top}px; {width}; max-height: {placement.Room}px");
    }
}
