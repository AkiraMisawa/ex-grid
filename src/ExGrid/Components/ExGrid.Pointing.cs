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

    // The pointing outline as a Focus and an Extent of its own — a one-range selection, so the
    // arrows, Shift and a click move it by the same transitions the Selection's own use — the
    // span of the text its Reference occupies, and the text as the core last wrote it. Pointing
    // stands only while the editor's text is still that text: anything typed ends it.
    private GridSelection? _pointer;
    private int _pointStart;
    private int _pointLength;
    private string? _pointWritten;
    private bool _pointDragging;

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

    /// <summary>Whether a pointing outline stands, and the editor's text is still the text its
    /// Reference was written into.</summary>
    private bool PointingContinues
        => _pointer is not null && string.Equals(_editText, _pointWritten, StringComparison.Ordinal);

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
        if (PointingContinues)
        {
            _pointer = e.ShiftKey ? _pointer!.ExtendTo(cell, extent) : _pointer!.Click(cell, extent);
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
        if (!PointingContinues || _pointer!.Extent == cell)
        {
            _suppressRender = true;
            return;
        }
        _pointer = _pointer.ExtendTo(cell, Extent);
        WritePointedReference(reveal: false);
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
        PlaceCaret();
        CloseCompletion();
        MarkGateIfMoved();
        if (reveal)
        {
            // The outline's moving end is kept on screen as the Extent is (ADR-0052): the
            // pointed cell when the outline moves, the far end when Shift extends it. The Focus
            // itself does not move.
            _revealTarget = new ExtentReveal(_pointer.Extent, _pointer.FocusRange);
            _revealFocus = true;
        }
    }
}
