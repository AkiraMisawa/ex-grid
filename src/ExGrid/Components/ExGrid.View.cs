using ExGrid.Selection;

namespace ExGrid.Components;

/// <summary>
/// What a reader of the grid looks at: the Selection, with the Row Sequence Version its positions are
/// written in, and where the rows and columns are scrolled to. A Consumer that shows several sets of
/// rows in one grid, one at a time — ExSheet's Sheets behind their tabs (ADR-0331) — keeps each set's
/// view with <see cref="ExGrid{TRow}.ReadView"/> and hands it back with
/// <see cref="ExGrid{TRow}.ShowViewWith"/> when that set is shown again.
/// </summary>
/// <param name="RowSequenceVersion">The Row Sequence Version the Selection's positions are written in.</param>
/// <param name="Selection">The Selection.</param>
/// <param name="ScrollTopPx">The scroller's vertical offset, in pixels.</param>
/// <param name="ScrollLeftPx">The scroller's horizontal offset, in pixels.</param>
public readonly record struct GridView(int RowSequenceVersion, GridSelection Selection, double ScrollTopPx, double ScrollLeftPx);

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
    public GridView ReadView() => new(_selection.RowSequenceVersion, _selection.Selection, _scrollTopPx, _scrollLeftPx);

    /// <summary>
    /// Shows <paramref name="view"/> when the grid next takes the Row Sequence Version it is written in
    /// (ADR-0331): its Selection in place of the empty one a new version leaves (ADR-0011), and its scroll
    /// offsets, written to the browser once that render has landed. A Consumer that swaps the rows the
    /// grid shows hands the view of the rows it brings, with the new version, before the render that
    /// hands the grid the version, from the same handler: the two then arrive together. A view handed
    /// later replaces one not yet taken; one whose version the grid never takes is never shown.
    /// </summary>
    /// <param name="view">The view, written in the version the Consumer is about to hand the grid.</param>
    /// <exception cref="ArgumentException">The view holds no Selection (<c>default(GridView)</c>).</exception>
    public void ShowViewWith(GridView view)
    {
        if (view.Selection is null)
            throw new ArgumentException("A view holds a Selection; start from GridSelection.Empty.", nameof(view));
        _viewAhead = view;
        _viewAheadInput = Inputs.RunningInput;
    }

    /// <summary>The view handed ahead of the version just taken, in place of what reconciling left
    /// (ReconcileSelection); the Selection as reconciled otherwise, or when its positions lie outside
    /// the grid's rows and columns.</summary>
    private HeldSelection TakeViewAhead(HeldSelection reconciled)
    {
        if (_viewAhead is not { } ahead || ahead.RowSequenceVersion != _sequenceVersion)
            return reconciled;
        _viewAhead = null;
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
        foreach (var range in ahead.Selection.Ranges)
        {
            if (range.BottomRow >= extent.RowCount || range.RightColumn >= extent.ColumnCount)
                return reconciled;
        }
        return new HeldSelection(_sequenceVersion, ahead.Selection);
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
