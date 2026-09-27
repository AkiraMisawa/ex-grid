using ExGrid.Cells;
using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;

namespace ExGrid.Components;

/// <summary>
/// The grid (ADR-0001). This part holds formula entry's aids, as ExGrid mechanism a Consumer
/// switches on and gives meaning to (ADR-0051): the editor's text and caret reported as the
/// user types, the Consumer's candidates and hint painted by Chrome as the editor's Inner
/// Popup, and the keys that work the list. The grid does not know what a Formula is.
/// </summary>
/// <typeparam name="TRow">The Consumer's row type.</typeparam>
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Consumer declaration (ADR-0051): completion and the argument hint. While an edit is
    /// open, the editor's text and the caret are handed to this as the user types — in the Cell
    /// Editor and in the Formula Bar alike — and the answer is painted beneath the editor
    /// surface as a list of candidates and a hint. ↑/↓ choose, Tab accepts the chosen
    /// candidate, and Escape closes the list and leaves the edit open.
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

    // Where the caret stands in the uncommitted text, as far as the core knows it: at the end
    // of what an input event wrote, or where a key carried it (ADR-0051).
    private int _editCaret;

    // The answer being shown, the candidate chosen in it, and the number of the last question
    // asked — an answer to any other is stale and dropped (ADR-0051, DC-18).
    private EditorCompletion? _completion;
    private int _completionSelected;
    private int _completionAsked;
    private Action<int>? _acceptFromChrome;

    /// <summary>Whether a list of candidates is showing — the state in which ↑/↓, Tab and
    /// Escape are the list's (ADR-0051). A hint alone takes no key.</summary>
    private bool CompletionListOpen
        => _editMode != EditMode.None && _completion is { Candidates.Count: > 0 };

    /// <summary>The key gate's set for the current state (ADR-0010): Caret leaves the arrows
    /// to the editor, except while a list is open, whose ↑/↓ are the core's.</summary>
    private string GateMode() => _editMode switch
    {
        EditMode.Overwrite => "overwrite",
        EditMode.Caret => CompletionListOpen ? "overwrite" : "caret",
        _ => "none",
    };

    /// <summary>
    /// The user changed the uncommitted text in either surface (ADR-0051): the caret is where
    /// the edit left it, the list shown for the old text goes — it answers text that is no
    /// longer there — and the new text is reported.
    /// </summary>
    private void TextTyped(string text)
    {
        if (text == _editText)
            return;
        _editCaret = EditorTextRules.InferCaret(_editText, text);
        _editText = text;
        RequestCompletion();
    }

    /// <summary>
    /// Hands the current text and caret to the Consumer (ADR-0051). Whatever is showing is
    /// taken down first, and every earlier question is made stale: nothing answers the new
    /// text until its own answer comes. A synchronous answer is shown in the render already
    /// on its way.
    /// </summary>
    private void RequestCompletion()
    {
        var wasOpen = CompletionListOpen;
        _completion = null;
        var asked = ++_completionAsked;
        if (CompleteEditorText is not { } complete || _editMode == EditMode.None)
        {
            GateMayHaveMoved(wasOpen);
            return;
        }
        var text = _editText;
        ValueTask<EditorCompletion?> answer;
        try
        {
            answer = complete(text, _editCaret);
        }
        catch (Exception ex)
        {
            GateMayHaveMoved(wasOpen);
            _ = DispatchExceptionAsync(ex);
            return;
        }
        if (answer.IsCompletedSuccessfully)
            ShowCompletion(asked, text, answer.Result);
        else
            _ = AwaitCompletionAsync(asked, text, answer);
        GateMayHaveMoved(wasOpen);
    }

    private async Task AwaitCompletionAsync(int asked, string text, ValueTask<EditorCompletion?> answer)
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
            var wasOpen = CompletionListOpen;
            if (!ShowCompletion(asked, text, result))
                return;
            GateMayHaveMoved(wasOpen);
            // Not a UI event: an armed suppression would swallow this render (the OnKeyAsync
            // lesson).
            _suppressRender = false;
            StateHasChanged();
        });
    }

    /// <summary>Shows an answer, unless it is stale: asked before a later question, for text
    /// that is no longer the editor's, or after the edit closed (ADR-0051, DC-18).</summary>
    /// <returns>Whether anything was shown.</returns>
    private bool ShowCompletion(int asked, string text, EditorCompletion? answer)
    {
        if (_disposed || asked != _completionAsked || _editMode == EditMode.None
            || !string.Equals(text, _editText, StringComparison.Ordinal)
            || answer is null || answer.IsEmpty)
        {
            return false;
        }
        _completion = answer;
        _completionSelected = answer.Candidates.Count > 0 ? 0 : -1;
        return true;
    }

    /// <summary>Takes the list and the hint down, and makes any answer still on its way
    /// stale.</summary>
    private void CloseCompletion()
    {
        var wasOpen = CompletionListOpen;
        _completion = null;
        _completionSelected = -1;
        _completionAsked++;
        GateMayHaveMoved(wasOpen);
    }

    /// <summary>In Caret the gate claims the arrows only while a list is open; a change is
    /// told after the render (ADR-0010).</summary>
    private void GateMayHaveMoved(bool wasOpen)
    {
        if (_editMode == EditMode.Caret && wasOpen != CompletionListOpen)
            _jsEditingDirty = true;
    }

    /// <summary>
    /// A key the gate forwarded while a list is open (ADR-0051): ↑/↓ choose, Tab accepts,
    /// Escape closes the list and leaves the edit open. In Caret the gate claimed the other
    /// caret keys too, only because the list was open: they close it. Every other key keeps
    /// its meaning, and whatever it does to the edit takes the list with it.
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
            case "ArrowLeft" or "ArrowRight" or "Home" or "End" when _editMode == EditMode.Caret:
                CloseCompletion();
                StateHasChanged();
                return true;
        }
        return false;
    }

    /// <summary>
    /// Accepts a candidate (ADR-0051): its span of the text is replaced by what it writes, and
    /// the caret stands after it. The new text is reported like any other, so the hint for
    /// what was just accepted can follow.
    /// </summary>
    private void AcceptCandidate(int index)
    {
        if (!CompletionListOpen || index < 0 || index >= _completion!.Candidates.Count)
            return;
        var (text, caret) = EditorTextRules.Accept(_editText, _completion.Candidates[index]);
        _editText = text;
        _editCaret = caret;
        RequestCompletion();
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
