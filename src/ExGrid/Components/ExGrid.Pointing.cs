using ExGrid.Cells;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ExGrid.Components;

// Point, the fourth editing state (ADR-0051): while the Consumer says the caret stands where
// a Reference can go, the arrows and the mouse move a pointing outline over the grid, painted
// in the selection overlay (ADR-0008), and the Consumer's Reference text for it is written at
// the caret. The Selection and the Focus do not move.
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Consumer declaration (ADR-0051): whether, in this text with the caret here, a
    /// Reference can go at the caret — after <c>=</c>, an operator, <c>(</c> or <c>,</c> in a
    /// Formula. Asked synchronously, on the text and caret each arrow key carries from the
    /// browser and never on an earlier answer, so a fast typist is never pointed where the text
    /// no longer allows it. While it answers true in Overwrite, an arrow points instead of
    /// committing — <c>=</c> ↓ ↓ writes the Reference two rows down — and a click on a cell
    /// points in any editing state. Where it answers false, Overwrite and Caret keep the
    /// meanings ADR-0012 gives the arrows. Needs <see cref="ReferenceText"/>. Null — the
    /// default — never points.
    /// </summary>
    [Parameter] public Func<string, int, bool>? PointAt { get; set; }

    /// <summary>
    /// A Consumer declaration (ADR-0051): the Reference text for a pointed range, written at
    /// the caret while pointing — <c>A3</c>, <c>B7:C9</c> on a Sheet. The grid knows positions;
    /// what they are called is the Consumer's. Required with <see cref="PointAt"/>.
    /// </summary>
    [Parameter] public Func<SelectionRange, string>? ReferenceText { get; set; }

    /// <summary>
    /// A Consumer declaration (ADR-0051, 2026-09-29): what F4 makes of the editor's text — on a
    /// Sheet, the Reference at the caret cycled through its forms, <c>=B2</c> → <c>=$B$2</c> →
    /// <c>=B$2</c> → <c>=$B2</c> → <c>=B2</c>. Asked synchronously with the text and the
    /// selection (start, end) the F4 key carries from the browser, in the Cell Editor or the
    /// Formula Bar alike; it answers the whole new text and the selection in it, or null for
    /// nothing to change. The answer is written to both editor surfaces and its selection placed
    /// in the one being typed in. While pointing, the caret it is asked at is the end of the
    /// Reference the outline wrote, and pointing goes on over the rewritten Reference. The grid
    /// never reads a Formula. Declared, the key listener claims F4 while an edit is open, and
    /// only then; null — the default — leaves F4 to the browser everywhere.
    /// </summary>
    [Parameter] public Func<string, int, int, EditorRewrite?>? CycleReference { get; set; }

    /// <summary>
    /// Tells the Consumer where the open edit stands with respect to Point (ADR-0058): in Point
    /// while <see cref="PointAt"/> says a Reference can go at the caret or what Point wrote stands
    /// over unchanged text, and whether that was written from outside the grid
    /// (<see cref="WritePointedTextAsync"/>). Raised when the state changes and never when it does
    /// not, at the change — as the key, the typing or the press that changed it is answered, before
    /// the render that paints it — and with <see cref="PointState.None"/> when the edit ends.
    /// Raised and not waited for. A Consumer that points from other instances on the page declares
    /// them pointed at while this is not <see cref="PointState.None"/>. Nothing is raised where
    /// <see cref="PointAt"/> is not declared.
    /// </summary>
    [Parameter] public EventCallback<PointState> OnPointStateChanged { get; set; }

    // The pointing outline as a Focus and an Extent of its own — a one-range selection, so the
    // arrows, Shift and a click move it by the same transitions the Selection's own use — the
    // span of the text its Reference occupies, and the text as the core last wrote it. Pointing
    // stands only while the editor's text is still that text: anything typed ends it.
    private GridSelection? _pointer;
    private int _pointStart;
    private int _pointLength;
    private string? _pointWritten;
    private bool _pointDragging;

    // Whether what Point wrote was handed in from outside (ADR-0058): no outline stands over this
    // grid's cells for it, the Name Box is empty and F4 changes nothing while it stands. With it,
    // the edit as it was before that write, for the Consumer to take it back to.
    private bool _pointedFromOutside;
    private PointBefore? _pointBefore;

    // The state the Consumer last heard (OnPointStateChanged).
    private PointState _pointStateTold;

    /// <summary>An open edit's Point as it stood before a write from outside: what taking the write
    /// back returns it to.</summary>
    private sealed record PointBefore(
        string Text, int Caret, EditMode Mode, GridSelection? Pointer, int Start, int Length, string? Written, bool FromOutside);

    // A cell to reveal in the Focus's place: the Extent while a range is extended (ADR-0052),
    // or the pointing outline's moving end, which may be walked off screen while the Focus
    // itself stays put (ADR-0012's reveal, ADR-0051). With it, the range being extended: an
    // axis it spans end to end is not scrolled for (ADR-0052, 2026-09-29).
    private ExtentReveal? _revealTarget;

    /// <summary>The moving end to keep in view, and the range it is the end of.</summary>
    private readonly record struct ExtentReveal(CellPosition Cell, SelectionRange Range);

    /// <summary>Refuses a Point predicate without the Reference text it would need: pointing
    /// would have nothing to write (ADR-0051).</summary>
    private void ValidatePointDeclaration()
    {
        if (PointAt is not null && ReferenceText is null)
        {
            throw new ArgumentException(
                "PointAt needs ReferenceText: pointing writes the Consumer's Reference text for the pointed range, " +
                "and the grid has no name of its own for a position (ADR-0051).", nameof(ReferenceText));
        }
    }

    /// <summary>Whether what Point wrote stands — its outline over this grid's cells, or text
    /// written from outside — and the editor's text is still the text it was written into.</summary>
    private bool PointingContinues
        => (_pointer is not null || _pointedFromOutside) && string.Equals(_editText, _pointWritten, StringComparison.Ordinal);

    /// <summary>Whether what Point wrote, and still stands, was written from outside the grid
    /// (ADR-0058).</summary>
    private bool PointedFromOutside => _pointedFromOutside && _editMode != EditMode.None && PointingContinues;

    /// <summary>Where the open edit stands with respect to Point, as <see cref="OnPointStateChanged"/>
    /// tells it.</summary>
    private PointState CurrentPointState()
    {
        if (_editMode == EditMode.None || PointAt is not { } pointAt)
            return PointState.None;
        if (PointingContinues)
            return _pointedFromOutside ? PointState.WrittenFromOutside : PointState.InPoint;
        return _editCaret >= 0 && _editCaret <= _editText.Length && pointAt(_editText, _editCaret)
            ? PointState.InPoint
            : PointState.None;
    }

    /// <summary>Tells the Consumer the Point state when it is not what the Consumer last heard: at
    /// each place the edit's text, caret, mode or Point changes. Raised and not waited for, as the
    /// edit's opening and ending are (<see cref="TellConsumerWhetherEditOpen"/>).</summary>
    private void TellConsumerPointState()
    {
        if (!OnPointStateChanged.HasDelegate || _disposed)
            return;
        // A caret not known yet — typing whose caret report is still on its way — is waited for,
        // never guessed at: told as out of Point, the Consumer would stop pointing and start again
        // with every key.
        if (_editMode != EditMode.None && PointAt is not null && !PointingContinues && _editCaret < 0)
            return;
        var state = CurrentPointState();
        if (state == _pointStateTold)
            return;
        _pointStateTold = state;
        _ = RaiseUnwaitedAsync(() => OnPointStateChanged.InvokeAsync(state));
    }

    /// <summary>The pointed range while an outline stands over an open edit, for the
    /// overlay.</summary>
    private SelectionRange? PointRange
        => _editMode != EditMode.None && _pointer is { IsEmpty: false } pointer ? pointer.Ranges[0] : null;

    /// <summary>Whether a press in the body keeps DOM focus in the editor: while an edit is open
    /// where pointing is declared, a click on a cell may point, and the editor must still hold
    /// the keyboard afterwards.</summary>
    private bool PressKeepsTheEditor => _editMode != EditMode.None && PointAt is not null;

    /// <summary>The outline goes; the text written stays.</summary>
    private void EndPointing()
    {
        _pointer = null;
        _pointWritten = null;
        _pointDragging = false;
        _pointedFromOutside = false;
        _pointBefore = null;
    }

    /// <summary>
    /// A key the gate forwarded while editing, carrying the editor's text and caret
    /// (ADR-0051): the text is the browser's, as the user sees it, and is taken over as the
    /// uncommitted text. A change the core had not heard of yet is typing — it ends pointing
    /// and is reported like any other. A caret moved while a list stands — ← and → are the
    /// editor's while it is open — makes the list stale: it is asked again for the caret the
    /// key carries, and the key is decided against that answer.
    /// </summary>
    private void AdoptKeyText(string? text, int caret)
    {
        if (text is null || _editMode == EditMode.None)
            return;
        int? carried = caret >= 0 && caret <= text.Length ? caret : null;
        if (!string.Equals(text, _editText, StringComparison.Ordinal))
        {
            TextTyped(text, carried);
            return;
        }
        if (carried is not { } at || at == _editCaret)
            return;
        // A text that was waiting for its caret is asked about now, before the key is decided.
        var waited = _editCaret < 0 && CompleteEditorText is not null;
        _editCaret = at;
        if (waited || (CompletionListOpen && at != _completionCaret))
            RequestCompletion();
        TellConsumerPointState();
    }

    /// <summary>
    /// An arrow, Shift and an arrow, Home or End while editing (ADR-0051). While an outline
    /// stands over unchanged text they move it; otherwise, in Overwrite or Point, an arrow
    /// starts pointing from the edited cell when the Consumer says a Reference can go at the
    /// caret. Anywhere else the key keeps the meaning ADR-0012 gives it.
    /// </summary>
    /// <returns>Whether the key pointed.</returns>
    private bool OnPointKey(string canonical)
    {
        if (PointAt is not { } pointAt || ReferenceText is null)
            return false;
        (GridDirection Direction, int Kind)? step = canonical switch
        {
            "ArrowUp" => (GridDirection.Up, 0),
            "ArrowDown" => (GridDirection.Down, 0),
            "ArrowLeft" => (GridDirection.Left, 0),
            "ArrowRight" => (GridDirection.Right, 0),
            "Shift+ArrowUp" => (GridDirection.Up, 1),
            "Shift+ArrowDown" => (GridDirection.Down, 1),
            "Shift+ArrowLeft" => (GridDirection.Left, 1),
            "Shift+ArrowRight" => (GridDirection.Right, 1),
            "Home" => (GridDirection.Left, 2),
            "End" => (GridDirection.Right, 2),
            _ => null,
        };
        if (step is not { } move)
            return false;
        // What a press outside the grid wrote has no cell here to move from: ADR-0058 gives the
        // arrows a meaning inside the grid that press landed on, which this grid does not know. They
        // are claimed, and move and write nothing; the text stands.
        if (PointedFromOutside)
            return true;
        var extent = Extent;
        if (extent.RowCount <= 0 || extent.ColumnCount <= 0)
            return false;
        if (!PointingContinues)
        {
            EndPointing();
            // Home and End move an outline that stands; they start none. Caret's arrows are
            // the editor's.
            if (move.Kind == 2 || _editMode == EditMode.Caret || _editCaret < 0 || !pointAt(_editText, _editCaret))
                return false;
            _pointer = GridSelection.Empty.Click(_editingCell, extent);
            _pointStart = _editCaret;
            _pointLength = 0;
        }
        _pointer = move.Kind switch
        {
            0 => _pointer!.Move(move.Direction, extent),
            1 => _pointer!.Extend(move.Direction, extent),
            _ => _pointer!.MoveToEdge(move.Direction, extent),
        };
        WritePointedReference();
        StateHasChanged();
        return true;
    }

    /// <summary>
    /// A press on a cell while editing (ADR-0051): it points — at that cell, or with Shift
    /// from the outline's Focus (from the edited cell when none stands) — where an outline
    /// stands over unchanged text or the Consumer says a Reference can go at the caret. The
    /// press does not commit, and the drag it starts extends the outline.
    /// </summary>
    /// <returns>Whether the press pointed.</returns>
    private bool OnPointPress(MouseEventArgs e)
    {
        if (_editMode == EditMode.None || PointAt is not { } pointAt || ReferenceText is null || e.Button != 0)
            return false;
        if (ShowsRowHeadings && _columnStyles.Geometry.IsInLead(e.OffsetX, _scrollLeftPx))
            return false;
        if (CellUnder(e) is not { } cell)
            return false;
        var extent = Extent;
        if (PointingContinues && _pointer is { } pointer)
        {
            _pointer = e.ShiftKey ? pointer.ExtendTo(cell, extent) : pointer.Click(cell, extent);
        }
        else if (PointingContinues)
        {
            // What a press outside the grid wrote is replaced by this cell's Reference, in the same
            // place (ADR-0058): there is no outline here to move, so Shift reaches from the edited
            // cell, as it does before anything is pointed.
            _pointer = e.ShiftKey
                ? GridSelection.Empty.Click(_editingCell, extent).ExtendTo(cell, extent)
                : GridSelection.Empty.Click(cell, extent);
        }
        else
        {
            EndPointing();
            // A caret not known is not guessed at: the Reference would land somewhere else.
            if (_editCaret < 0 || !pointAt(_editText, _editCaret))
                return false;
            _pointStart = _editCaret;
            _pointLength = 0;
            _pointer = e.ShiftKey
                ? GridSelection.Empty.Click(_editingCell, extent).ExtendTo(cell, extent)
                : GridSelection.Empty.Click(cell, extent);
        }
        WritePointedReference(reveal: false);
        _pointDragging = true;
        SetDragging(true);
        _suppressRender = false;
        return true;
    }

    /// <summary>The pointing drag moved onto <paramref name="cell"/>: the outline extends to
    /// it.</summary>
    private void OnPointDrag(CellPosition cell)
    {
        if (!PointingContinues || _pointer is null || _pointer.Extent == cell)
        {
            _suppressRender = true;
            return;
        }
        _pointer = _pointer.ExtendTo(cell, Extent);
        WritePointedReference(reveal: false);
    }

    /// <summary>
    /// F4 while an edit is open (ADR-0051, 2026-09-29): the Consumer's rewrite of the text the
    /// key carried, at the selection it carried, written back and its selection placed. In two
    /// cases the selection is not the key's to give. While pointing, it is the caret at the end
    /// of the Reference the outline wrote: that Reference is the one F4 cycles, and pointing
    /// goes on over what it became. And while a placement the core asked for is still in flight
    /// in that very text, the browser's selection there is where setting the value left it —
    /// the end — and not the user's: the placement's is used, as a caret report ahead of it is
    /// not taken (ADR-0051's second round). The user's own move in that text is newer, and
    /// stands.
    /// </summary>
    /// <param name="start">The selection start the key carried; -1 when it carried none.</param>
    /// <param name="end">The selection end it carried.</param>
    /// <param name="moved">Whether the user moved the caret in that very text.</param>
    /// <exception cref="ArgumentOutOfRangeException">The answer's selection does not lie inside
    /// its text.</exception>
    private void OnCycleReferenceKey(int start, int end, bool moved)
    {
        // After a press outside the grid, F4 changes nothing (ADR-0058): what that press wrote is
        // not a Reference of this grid's cells, and the Consumer's rewrite is not asked.
        if (PointedFromOutside)
            return;
        var text = _editText;
        var pointing = PointingContinues;
        if (pointing)
        {
            start = end = _pointStart + _pointLength;
        }
        else if (_caretPlacing is { } placing && string.Equals(placing.Text, text, StringComparison.Ordinal))
        {
            if (moved)
            {
                _caretPlacing = null;
                if (_caretToPlace is { } unsent && string.Equals(unsent.Text, text, StringComparison.Ordinal))
                    _caretToPlace = null;
            }
            else
            {
                (start, end) = (placing.Caret, placing.End);
                _editCaret = start;
            }
        }
        // A selection not known is not guessed at: the wrong Reference would change.
        if (start < 0 || start > text.Length)
            return;
        if (end < start || end > text.Length)
            end = start;
        if (CycleReference!(text, start, end) is not { } answer)
            return;
        var rewritten = answer.Text ?? throw new ArgumentException("A rewrite answers the whole new text (ADR-0051).", nameof(CycleReference));
        if (answer.SelectionStart < 0 || answer.SelectionEnd < answer.SelectionStart || answer.SelectionEnd > rewritten.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(CycleReference),
                $"The rewrite's selection {answer.SelectionStart}..{answer.SelectionEnd} does not lie inside its text of {rewritten.Length} characters (ADR-0051).");
        }
        // The outline's Reference is what lies between the text before it and the text after
        // it, both unchanged; a rewrite that touched either has rewritten more than the
        // outline's Reference, and pointing ends as typing ends it.
        var before = _pointStart;
        var after = text.Length - _pointStart - _pointLength;
        pointing = pointing
            && rewritten.Length >= before + after
            && string.CompareOrdinal(rewritten, 0, text, 0, before) == 0
            && string.CompareOrdinal(rewritten, rewritten.Length - after, text, text.Length - after, after) == 0;
        _editText = rewritten;
        _editCaret = answer.SelectionStart;
        PlaceCaret(answer.SelectionEnd);
        if (pointing)
        {
            _pointLength = rewritten.Length - before - after;
            _pointWritten = rewritten;
            CloseCompletion();
        }
        else
        {
            if (_pointer is not null || _editMode == EditMode.Point)
            {
                EndPointing();
                if (_editMode == EditMode.Point)
                    _editMode = EditMode.Overwrite;
            }
            RequestCompletion();
        }
        MarkGateIfMoved();
        TellConsumerPointState();
        _suppressRender = false;
        StateHasChanged();
    }

    /// <summary>
    /// Writes the Consumer's Reference text for the outline in place of the one written for
    /// it before — or, the first time, at the caret — and leaves the caret after it
    /// (ADR-0051). The edit is now in Point: the list the old text had goes, and the gate keeps
    /// the arrows.
    /// </summary>
    private void WritePointedReference(bool reveal = true)
    {
        var range = _pointer!.Ranges[0];
        var reference = ReferenceText!(range);
        var (text, caret) = EditorTextRules.Replace(_editText, _pointStart, _pointLength, reference);
        _pointLength = reference.Length;
        _editText = text;
        _editCaret = caret;
        _pointWritten = text;
        _editMode = EditMode.Point;
        // This grid's own cell replaces whatever a press outside it wrote.
        _pointedFromOutside = false;
        _pointBefore = null;
        PlaceCaret();
        CloseCompletion();
        MarkGateIfMoved();
        TellConsumerPointState();
        if (reveal)
        {
            // The outline's moving end is kept on screen as the Extent is (ADR-0052): the
            // pointed cell when the outline moves, the far end when Shift extends it. The Focus
            // itself does not move.
            _revealTarget = new ExtentReveal(_pointer.Extent, _pointer.FocusRange);
            _revealFocus = true;
        }
    }

    /// <summary>
    /// Writes <paramref name="text"/> into the open edit as Point writes a Reference, for a press a
    /// Consumer took outside the grid (ADR-0058) — a Pointing Scope's <c>Positions[PV]</c>, or
    /// <c>XLOOKUP("R-4471", Positions[Id], Positions[PV])</c>. It goes where Point writes: in place of
    /// what this Point wrote, from this grid's cells or from outside, or else at the caret where
    /// <see cref="PointAt"/> says a Reference can go; and the caret stands after it. The edit is then
    /// in Point, written from outside (<see cref="PointState.WrittenFromOutside"/>): no outline stands
    /// over this grid's cells, the Name Box is empty, F4 changes nothing, and the arrows move nothing.
    /// A further press, on this grid or outside it, replaces the text, and anything typed or a caret
    /// moved ends Point over it, as it ends Point over a Reference. The text wears the pointed look
    /// where one of the Consumer's References stands exactly over it (ADR-0057). The grid reads
    /// nothing of the text: what it means is the Consumer's.
    /// </summary>
    /// <param name="text">What to write; never empty.</param>
    /// <returns>Whether it was written: false, and nothing changed, when no edit is open, when
    /// pointing is not declared, or when the edit is not in Point (<see cref="PointState.None"/>),
    /// as when something was typed after the press was made.</returns>
    /// <exception cref="ArgumentException">The text is null or empty.</exception>
    public async Task<bool> WritePointedTextAsync(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        // On the renderer's own context: a press on another instance hands the text in, and it must
        // not race this grid's render.
        var written = false;
        await InvokeAsync(() => written = WritePointedText(text));
        return written;
    }

    /// <summary>
    /// Takes back the text <see cref="WritePointedTextAsync"/> wrote last, while it still stands: the
    /// edit returns to what it was before that write — its text, its caret, its editing state, and
    /// what Point had written before it, which stands again. ADR-0058 asks it of a press that a drag
    /// then carries onto another cell: a Formula that read one cell of the range the user meant would
    /// be a plausible wrong answer.
    /// </summary>
    /// <returns>Whether anything was taken back: false, and nothing changed, when no edit is open or
    /// the text written from outside no longer stands — typed on, moved away from, replaced by a
    /// press, or already taken back.</returns>
    public async Task<bool> TakeBackPointedTextAsync()
    {
        var taken = false;
        await InvokeAsync(() => taken = TakeBackPointedText());
        return taken;
    }

    private bool WritePointedText(string text)
    {
        if (_disposed || CurrentPointState() == PointState.None)
            return false;
        if (!PointingContinues)
        {
            // Nothing this Point wrote stands: the text goes in at the caret.
            EndPointing();
            _pointStart = _editCaret;
            _pointLength = 0;
        }
        var before = new PointBefore(_editText, _editCaret, _editMode, _pointer, _pointStart, _pointLength, _pointWritten, _pointedFromOutside);
        var (written, caret) = EditorTextRules.Replace(_editText, _pointStart, _pointLength, text);
        _pointer = null;
        _pointDragging = false;
        _pointLength = text.Length;
        _editText = written;
        _editCaret = caret;
        _pointWritten = written;
        _pointedFromOutside = true;
        _pointBefore = before;
        _editMode = EditMode.Point;
        PlaceCaret();
        CloseCompletion();
        MarkGateIfMoved();
        TellConsumerPointState();
        // Not a UI event of this grid's: an armed suppression would swallow this render.
        _suppressRender = false;
        StateHasChanged();
        return true;
    }

    private bool TakeBackPointedText()
    {
        if (_disposed || !PointedFromOutside || _pointBefore is not { } before)
            return false;
        _editText = before.Text;
        _editCaret = before.Caret;
        _editMode = before.Mode;
        _pointer = before.Pointer;
        _pointStart = before.Start;
        _pointLength = before.Length;
        _pointWritten = before.Written;
        _pointedFromOutside = before.FromOutside;
        _pointBefore = null;
        _pointDragging = false;
        PlaceCaret();
        // What Point wrote before stands again, and takes no list; text that was only waiting for a
        // Reference is asked about again, as it was before the press.
        if (PointingContinues)
            CloseCompletion();
        else
            RequestCompletion();
        MarkGateIfMoved();
        TellConsumerPointState();
        _suppressRender = false;
        StateHasChanged();
        return true;
    }
}
