using ExGrid.Selection;

namespace ExGrid.Components;

/// <summary>
/// What a reader of the grid looks at: the Selection, with the Row Sequence Version its positions are
/// written in, and where the rows and columns are scrolled to. A Consumer that shows several sets of
/// rows in one grid, one at a time — ExSheet's Sheets behind their tabs (ADR-0331) — keeps each set's
/// view with <see cref="ExGrid{TRow}.ReadView"/> and hands it back with
/// <see cref="ExGrid{TRow}.ShowViewWith(GridView)"/> when that set is shown again.
/// </summary>
/// <param name="RowSequenceVersion">The Row Sequence Version the Selection's positions are written in.</param>
/// <param name="Selection">The Selection.</param>
/// <param name="ScrollTopPx">The scroller's vertical offset, in pixels.</param>
/// <param name="ScrollLeftPx">The scroller's horizontal offset, in pixels.</param>
public readonly record struct GridView(int RowSequenceVersion, GridSelection Selection, double ScrollTopPx, double ScrollLeftPx)
{
    /// <summary>The grid and the set of rows the view was read of (<see cref="ExGrid{TRow}.ReadView"/>),
    /// carried through <c>with</c>, so a view handed back to that grid brings the same set again; none for a
    /// view that grid did not read, which brings a set of its own. Not part of the view's equality: two
    /// views of the same Selection and offsets are the same view, whoever read them.</summary>
    internal RowSetStamp? RowSet { get; init; }

    /// <summary>Whether <paramref name="other"/> has the same Row Sequence Version, Selection and offsets.</summary>
    /// <param name="other">The view to compare with.</param>
    /// <returns>Whether the two are the same view.</returns>
    public bool Equals(GridView other)
        => RowSequenceVersion == other.RowSequenceVersion && Equals(Selection, other.Selection)
           && ScrollTopPx.Equals(other.ScrollTopPx) && ScrollLeftPx.Equals(other.ScrollLeftPx);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(RowSequenceVersion, Selection, ScrollTopPx, ScrollLeftPx);
}

/// <summary>Which grid read a view, and of which of its sets of rows (<see cref="GridView.RowSet"/>).</summary>
internal sealed record RowSetStamp(object Grid, int Set);

// A view handed ahead of the Row Sequence Version it is written in (ADR-0331): the grid takes it in the
// render that takes that version, in place of the empty Selection a new version leaves (ADR-0011). So the
// Selection and the version arrive together, and an input the grid takes after the Consumer's switch meets
// the Selection shown, never an empty one a round trip before it: nothing depends on when it is taken.
//
// A view handed from the handling of an input — a declared key such as ExSheet's Ctrl+PageDown — moves the
// order as that input's answer. The inputs after it in the Input Order (ADR-0240) were typed after it, and
// are taken as a user typing slowly would have them taken: under the new version, at the view's Selection,
// whichever paint the browser told them with. On a circuit those inputs leave the browser before the render
// that shows the new rows reaches it, and told the paint before it; judged by that paint alone, they were
// refused as aimed under another order, and the outcome hung on the round trip (ADR-0240 section 1). An
// order move that came from no input, or from another move since, is judged by the paint as before
// (ADR-0011, ADR-0142).
public partial class ExGrid<TRow>
{
    private GridView? _viewAhead;
    private bool _viewScrollOwed;

    // The set of rows shown, by a number the grid gives each set a view brings in and stamps on the view it
    // reads of it (ReadView), and the numbers given so far. Rows the Consumer hands under a new version with
    // no view are the same set, in another order.
    private int _rowSet = 1;
    private int _rowSetsGiven = 1;

    // The set the open edit was opened among. While another is shown the edit is away (EditAway): open, its
    // editor holding the keyboard but neither seen nor pressed, its text in the Formula Bar, pointing at the
    // rows shown, and its commit landing on _editRow.
    private int _editSet;

    // The row the open edit was opened on, held only while the edit is away, so its commit has a row to land
    // on: read from the Window it was in as that Window was replaced (ApplyState), the one row the grid holds
    // beyond its Window (ADR-0160).
    private TRow? _editRow;
    private TRow? _editRowLeaving;

    // The placement a view handed ahead carries in place of its own Selection, with its Focus revealed
    // (ShowViewWith(GridView, SelectionRange, CellPosition)); null when the view's Selection stands.
    private (SelectionRange Range, CellPosition Focus)? _viewAheadPlace;

    // Whether the place handed ahead is a match a find step found (ShowViewWithMatch), which becomes the current
    // match as it is placed.
    private bool _viewAheadIsMatch;

    // The input whose handling handed the view ahead; 0 when none did.
    private int _viewAheadInput;

    // The order move a view taken made as an input's answer: the input, the version it moved to, and the
    // first paint of the order it moved from. Inputs numbered after it that told a paint of that order are
    // aimed under the new one (AimedAwayFrom).
    private (int Input, int Version, int FromPaint)? _orderMovedByInput;

    /// <summary>
    /// The view as the grid holds it now: the Selection with the Row Sequence Version it is written in
    /// (as <see cref="ReadSelection"/> reads it), and the scroll offsets the grid last knew. Read on the
    /// renderer's context, where the Consumer's handlers run.
    /// </summary>
    public GridView ReadView() => new(_selection.RowSequenceVersion, _selection.Selection, _scrollTopPx, _scrollLeftPx) { RowSet = new RowSetStamp(this, _rowSet) };

    /// <summary>
    /// The open edit's text as the grid holds it — what the keys and the typing taken so far in the
    /// Input Order left in it, the key being answered included — or null while no edit is open. Read on
    /// the renderer's context, where the Consumer's handlers run: a Consumer deciding what a declared key
    /// pressed with an edit open means reads the text the key was pressed over (ExSheet shows another
    /// Sheet on Ctrl+PageDown while a Formula is typed, and not while a constant is, ADR-0331 §5).
    /// </summary>
    public string? ReadEditText() => _editMode == EditMode.None ? null : _editText;

    /// <summary>
    /// Shows <paramref name="view"/> when the grid next takes the Row Sequence Version it is written in
    /// (ADR-0331): its Selection in place of the empty one a new version leaves (ADR-0011), and its scroll
    /// offsets, written to the browser once that render has landed. A Consumer that swaps the rows the
    /// grid shows hands the view of the rows it brings, with the new version, before the render that
    /// hands the grid the version, from the same handler: the two then arrive together. A view handed
    /// later replaces one not yet taken; one whose version the grid never takes is never shown.
    ///
    /// <para>An edit open as the view is taken stays open with the rows it was opened among (ADR-0331
    /// §5): while other rows are shown it is away — its editor holds the keyboard but is neither seen nor
    /// pressed, the Formula Bar shows its text, and Point points at the rows shown — and its commit raises
    /// the Edit Intent with the row it was opened on, as the grid last held it. A view this grid read of
    /// those rows (<see cref="ReadView"/>), handed back, brings them back with the edit standing over its
    /// cell again: the Consumer vouches that a view handed back is of the same rows in the same order.</para>
    /// </summary>
    /// <param name="view">The view, written in the version the Consumer is about to hand the grid.</param>
    /// <exception cref="ArgumentException">The view holds no Selection (<c>default(GridView)</c>).</exception>
    public void ShowViewWith(GridView view)
    {
        if (view.Selection is null)
            throw new ArgumentException("A view holds a Selection; start from GridSelection.Empty.", nameof(view));
        _viewAhead = view;
        _viewAheadPlace = null;
        _viewAheadIsMatch = false;
        _viewAheadInput = Inputs.RunningInput;
    }

    /// <summary>
    /// Shows <paramref name="view"/> as <see cref="ShowViewWith(GridView)"/> does, with
    /// <paramref name="range"/> placed as its Selection and the Focus on <paramref name="focus"/>,
    /// scrolled into view from the view's offsets, as <see cref="PlaceSelectionAsync"/> places them
    /// (ADR-0050, item 4). A Consumer that shows other rows to go to a place among them — ExSheet's Name
    /// Box taking <c>Sheet2!B5</c> (ADR-0331 §5) — hands the place with the rows, so it is placed under
    /// the version that brings them: placed after it, it would hang on when the grid took that version.
    /// A range reaching outside the grid's rows and columns once the version is taken is not placed,
    /// and the grid shows what reconciling left, as for a view whose Selection lies outside them.
    /// </summary>
    /// <param name="view">The view, written in the version the Consumer is about to hand the grid; its Selection is replaced.</param>
    /// <param name="range">The one range to place.</param>
    /// <param name="focus">The Focus, inside <paramref name="range"/>.</param>
    /// <exception cref="ArgumentException">The view holds no Selection (<c>default(GridView)</c>).</exception>
    /// <exception cref="ArgumentOutOfRangeException">The Focus lies outside the range, or the range starts before the first row or column.</exception>
    public void ShowViewWith(GridView view, SelectionRange range, CellPosition focus)
    {
        if (range.TopRow < 0 || range.LeftColumn < 0)
            throw new ArgumentOutOfRangeException(nameof(range), range, "A range starts at row and column 0 or after (ADR-0050).");
        if (!range.Contains(focus))
            throw new ArgumentOutOfRangeException(nameof(focus), focus, "The Focus is placed inside the range it belongs to (ADR-0050/0052).");
        ShowViewWith(view);
        _viewAheadPlace = (range, focus);
    }

    /// <summary>
    /// Shows <paramref name="view"/> with <paramref name="match"/> placed as the one cell selected and the
    /// Focus, as <see cref="ShowViewWith(GridView, SelectionRange, CellPosition)"/> places a range, and takes it
    /// as the find panel's current match (ADR-0332 §4): marked apart from the other matches, and the cell
    /// Replace writes, for as long as the Focus stays there. For a Consumer whose <see cref="OnFind"/> finds the
    /// match among other rows than those shown — ExSheet's Find within the Workbook, finding it on another
    /// Sheet (ADR-0332 §5) — and hands those rows with it, under a new Row Sequence Version, from inside its
    /// answer to the step. Its answer cannot name a position in rows the step was not asked about, so it then
    /// gives the step up, by throwing <see cref="OperationCanceledException"/>, and the step moves nothing
    /// itself. A match that lies outside the rows and columns the version brings is not placed, and nothing
    /// is the current match. While the find panel is closed it is placed and is no match.
    /// </summary>
    /// <param name="view">The view, written in the version the Consumer is about to hand the grid; its Selection is replaced.</param>
    /// <param name="match">The cell the step found, among the rows the version brings.</param>
    /// <exception cref="ArgumentException">The view holds no Selection (<c>default(GridView)</c>).</exception>
    /// <exception cref="ArgumentOutOfRangeException">The match lies before the first row or column.</exception>
    public void ShowViewWithMatch(GridView view, CellPosition match)
    {
        ShowViewWith(view, SelectionRange.FromCorners(match, match), match);
        _viewAheadIsMatch = true;
    }

    /// <summary>The view handed ahead of the version just taken, in place of what reconciling left
    /// (ReconcileSelection); the Selection as reconciled otherwise, or when its positions lie outside
    /// the grid's rows and columns.</summary>
    private HeldSelection TakeViewAhead(HeldSelection reconciled)
    {
        if (_viewAhead is not { } ahead || ahead.RowSequenceVersion != _sequenceVersion)
            return reconciled;
        _viewAhead = null;
        TakeRowSet(ahead.RowSet is { } stamp && ReferenceEquals(stamp.Grid, this) ? stamp.Set : ++_rowSetsGiven);
        var place = _viewAheadPlace;
        _viewAheadPlace = null;
        var isMatch = _viewAheadIsMatch;
        _viewAheadIsMatch = false;
        _orderMovedByInput = _viewAheadInput > 0 ? (_viewAheadInput, _sequenceVersion, _orderPaintedFrom) : null;
        var top = Math.Max(0, ahead.ScrollTopPx);
        var left = Math.Max(0, ahead.ScrollLeftPx);
        _scrollTopPx = _expectedScrollTopPx = top;
        _scrollLeftPx = _expectedScrollLeftPx = left;
        _anchorScrollTopPx = null;
        // Written from inside the render that paints the view's rows, numbered as a reveal's write is, so
        // the script holds it until the frame that carries them: the offset and the slice are painted
        // together, and a scroll read already on the wire is set aside (StageScroll). Before the scroller
        // is attached, once it is (PushViewScrollAsync).
        if (_scrollHandle is not null)
            _ = PushScrollOffsetAsync(top, left, ++_revealToken);
        else
            _viewScrollOwed = true;
        var extent = Extent;
        if (place is { } placed)
        {
            if (placed.Range.BottomRow >= extent.RowCount || placed.Range.RightColumn >= extent.ColumnCount)
                return reconciled;
            // Revealed from the view's offsets, in the render that takes them: the reveal's write, when the
            // Focus lies outside them, follows the view's and supersedes it (StageReveal).
            RequestFocusReveal();
            // A match a find step found among these rows is the current match, under this version, once the
            // Selection placed here stands (ADR-0332 §4).
            if (isMatch && _findOpen)
                TakeCurrentMatch(placed.Focus);
            return new HeldSelection(_sequenceVersion, GridSelection.Empty.Place(placed.Range, placed.Focus, extent));
        }
        foreach (var range in ahead.Selection.Ranges)
        {
            if (range.BottomRow >= extent.RowCount || range.RightColumn >= extent.ColumnCount)
                return reconciled;
        }
        return new HeldSelection(_sequenceVersion, ahead.Selection);
    }

    /// <summary>Whether the open edit is away: opened among rows another set has replaced (ShowViewWith).</summary>
    private bool EditAway => _editMode != EditMode.None && _editSet != _rowSet;

    /// <summary>
    /// The rows of <paramref name="set"/> are shown now. An open edit leaving its own set is set aside
    /// with them: its row as last held, and Point's outline, which stood over the rows left, goes while
    /// what it wrote stays, for the next point to replace (_pointAside). Back among its own set, the edit
    /// stands over its cell under the version they are shown under now, as when it was opened.
    /// </summary>
    private void TakeRowSet(int set)
    {
        if (set == _rowSet)
            return;
        _rowSet = set;
        if (_editMode == EditMode.None)
            return;
        if (set == _editSet)
        {
            _editSequence = _sequenceVersion;
            _editRow = null;
        }
        else if (_editRowLeaving is { } leaving)
        {
            _editRow = leaving;
        }
        if (PointingContinues && _pointer is not null)
        {
            _pointer = null;
            _pointAside = true;
        }
    }

    /// <summary>Notes the set an edit opens among.</summary>
    private void NoteEditSet()
    {
        _editSet = _rowSet;
        _editRow = null;
    }

    /// <summary>Notes the row the open edit stands on in the Window about to be replaced, while it is still in
    /// hand (ADR-0160), for a set the replacing rows may take the edit away from (TakeRowSet).</summary>
    private void NoteEditRowLeaving(IReadOnlyList<TRow> window, int start)
    {
        if (_editMode == EditMode.None)
            _editRow = null;
        _editRowLeaving = _editMode != EditMode.None && !EditAway && _viewAhead is not null && _sequenceVersion == _editSequence
            ? RowOf(window, start, _editingCell.Row)
            : null;
    }

    /// <summary>Whether a gesture told the paint <paramref name="told"/>, of an order since moved, comes after
    /// the input whose answer moved it, in the Input Order: it was then taken after the move, and is aimed
    /// under the order in force (ShowViewWith).</summary>
    private bool TakenAfterTheMoveItTold(int told)
        => _orderMovedByInput is { } moved && moved.Version == _sequenceVersion && told >= moved.FromPaint
           && Inputs.RunningInput > moved.Input;

    /// <summary>Writes the scroll offsets of a view taken to the browser, once the render that shows it has landed.</summary>
    private async Task PushViewScrollAsync()
    {
        if (!_viewScrollOwed || _scrollHandle is null)
            return;
        _viewScrollOwed = false;
        await PushScrollOffsetAsync(_scrollTopPx, _scrollLeftPx);
    }
}
