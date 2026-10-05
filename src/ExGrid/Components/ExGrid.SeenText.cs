using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// A write is refused when what the user saw of its target changed before it lands (ADR-0142,
// LV-11 to LV-14). What the user saw is the painted text of the target's painted cells — the
// comparison the Change Highlight makes (ADR-0067), made here so that a write does not land on
// something the user did not see; ADR-0068's rule that the grid never compares values to MARK a
// cell still holds. A cell that was not on screen was not seen, and is not compared.
//
// Each render that can change what its painted cells show is a new paint, named on the Viewport
// (data-ex-paint, beside data-ex-sequence and data-ex-layout). The browser reads that name with
// each gesture — a press on the rows or on an action, a key, a paste — and tells it with the
// gesture (ADR-0021's notes of 2026-10-02 and 2026-10-05), and the gesture is judged against the
// paint it names. The grid keeps, for its last few paints, what it needs to recompute the painted
// text of the cells it painted: the row instances, the columns, their widths and the Consumer's
// painted-text and appearance lookups. Nothing per cell is kept, and nothing per cell reaches
// JavaScript. A gesture taken against a paint no longer kept is refused: the grid can no longer
// tell what the user saw.
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Cell Editor commit refused because the edited cell paints other text than it did when
    /// the editor opened (ADR-0142, LV-11): the value changed upstream while the user typed, under
    /// an editor that covers it. No Edit Intent is raised, and the editor stays open with what was
    /// typed. The refusal carries the cell's new painted text, which Chrome words into its refusal
    /// live region (A11Y-16) — the editor covers the cell, so the notice is where the user sees
    /// it. A second commit is judged against that text, and lands unless the cell has changed
    /// again; Escape leaves without writing. A change to another cell of the row refuses nothing,
    /// so a row whose P&amp;L moves every second can still have its notional edited; a write that
    /// depends on another cell is the Edit Verdict's to judge (ADR-0034), which sees the row as
    /// it is at the commit. Every commit gesture is refused alike — Enter, Tab, an arrow in
    /// Overwrite, a press elsewhere, Ctrl+Enter — and, as after a Reject, the gesture keeps no
    /// meaning of its own: the Focus does not move away from the editor that still stands. The
    /// grid holds no string for it.
    /// </summary>
    [Parameter] public EventCallback<GridCommitRefusal> OnCommitRefused { get; set; }

    /// <summary>
    /// An Action press refused (ADR-0142, LV-12): the painted text of the row's painted cells in
    /// the render the press was taken against differs from the row's when the press is handled
    /// (<see cref="ActionRefusalReason.RowChanged"/>), or that render is no longer kept
    /// (<see cref="ActionRefusalReason.RenderNoLongerKept"/>). Nothing is raised through
    /// <see cref="OnAction"/>. Every action is judged, by pointer as by Space, with no
    /// declaration: on a row that did not change it fires once, as before, and one refused in a
    /// race costs a second press. On the Server host, a press on a row that changes faster than a
    /// round trip may be refused more than once, and each refusal says why. A Template cell's own
    /// controls are the Consumer's (ADR-0037): their handlers receive the row the template was
    /// painted with, and checking that row is the Consumer's job. The grid holds no string for
    /// it; Chrome words it into its refusal live region (A11Y-16).
    /// </summary>
    [Parameter] public EventCallback<GridActionRefusal<TRow>> OnActionRefused { get; set; }

    // What a gesture says when the browser told no paint: a press made by script with no
    // mousedown before it, or a component test driving the core directly. It is judged against
    // the newest paint — what the core last painted.
    private const int PaintNotTold = -1;

    // How many paints are kept. A paint is a render that changed what a painted cell can show —
    // a scroll step, a new row instance on screen — so a fling makes one a frame. A gesture is
    // answered a few round trips after it is made at most, and the paints of a few round trips of
    // scrolling and live updates fit well inside this; one older than all of them is refused.
    private const int PaintsKept = 64;

    private readonly List<Paint> _paints = [];
    private int _paintId;

    /// <summary>What one paint painted, as much as recomputing its cells' painted text needs: the
    /// row instances of the painted rows (null where a position had no row in hand), the columns
    /// and their widths, which of them were painted, and the Consumer's lookups the text depends
    /// on. The row order is kept, because a position names a row only under one.</summary>
    private sealed class Paint
    {
        public required int Id { get; init; }
        public required int SequenceVersion { get; init; }
        public required int FirstRow { get; init; }
        public required TRow?[] Rows { get; init; }
        public required IReadOnlyList<GridColumn<TRow>> Columns { get; init; }
        public required ColumnGeometry Geometry { get; init; }
        public required ColumnRange? Scrollable { get; init; }
        public required CellTextMetrics Metrics { get; init; }
        public required PaintedTextOf<TRow>? PaintedText { get; init; }
        public required CellAppearanceOf<TRow>? Appearance { get; init; }

        /// <summary>Whether column <paramref name="column"/> was painted: a Pinned Column always,
        /// a scrollable one only inside the slice on screen, and none of those in a fling, whose
        /// Placeholders paint their Pinned Columns alone (ADR-0004).</summary>
        public bool Painted(int column)
            => column >= 0 && column < Columns.Count
               && (column < Geometry.PinnedCount
                   || (Scrollable is { } scrollable && column >= scrollable.Start && column < scrollable.Start + scrollable.Count));

        /// <summary>The painted text of a cell this paint painted, or null for one it did not
        /// paint as text: off screen, no row in hand, or an Action, Template or Mark cell.</summary>
        public string? TextAt(int row, int column)
        {
            var at = row - FirstRow;
            if (at < 0 || at >= Rows.Length || Rows[at] is not { } data || !Painted(column))
                return null;
            return PaintedTextFor(data, Columns[column], column, Geometry, Metrics, PaintedText, Appearance);
        }
    }

    /// <summary>
    /// Names the paint this render paints, keeping it if it is a new one (ADR-0142). Called from
    /// the Viewport's own attribute, so whatever path led to the render, the name is computed from
    /// the very state the rows below it are painted from. A render that changes no painted cell's
    /// text — a Selection moved, a popover opened, a row off screen replaced — keeps the name, so a
    /// gesture taken on it is judged as painted; one that can change a painted cell's text gets a
    /// new one.
    /// </summary>
    private int NotePaint()
    {
        var first = _visible?.Start ?? 0;
        var count = _visible?.Count ?? 0;
        // A Placeholder paints its Pinned Columns only (ADR-0004).
        var scrollable = _placeholderMode ? null : _scrollable;
        var geometry = _columnStyles.Geometry;
        var metrics = _metrics.CellMetrics;
        var last = _paints.Count > 0 ? _paints[^1] : null;
        if (last is not null && last.SequenceVersion == _sequenceVersion && last.FirstRow == first
            && last.Rows.Length == count && ReferenceEquals(last.Columns, Columns)
            && ReferenceEquals(last.Geometry, geometry) && last.Scrollable == scrollable && last.Metrics == metrics
            && ReferenceEquals(last.PaintedText, PaintedText) && ReferenceEquals(last.Appearance, CellAppearance)
            && PaintsTheSameRows(last))
        {
            return last.Id;
        }
        var rows = new TRow?[count];
        for (var i = 0; i < count; i++)
            rows[i] = RowInHand(first + i);
        _paints.Add(new Paint
        {
            Id = ++_paintId, SequenceVersion = _sequenceVersion, FirstRow = first, Rows = rows,
            Columns = Columns, Geometry = geometry, Scrollable = scrollable, Metrics = metrics,
            PaintedText = PaintedText, Appearance = CellAppearance,
        });
        if (_paints.Count > PaintsKept)
            _paints.RemoveAt(0);
        return _paintId;
    }

    /// <summary>Whether the rows now in hand at <paramref name="paint"/>'s positions are the
    /// instances it painted: a row's identity is its change signal (ADR-0003).</summary>
    private bool PaintsTheSameRows(Paint paint)
    {
        for (var i = 0; i < paint.Rows.Length; i++)
        {
            if (!ReferenceEquals(paint.Rows[i], RowInHand(paint.FirstRow + i)))
                return false;
        }
        return true;
    }

    /// <summary>The row the Window holds at absolute position <paramref name="row"/>, or null.</summary>
    private TRow? RowInHand(int row)
    {
        var slice = row - _windowStart;
        return slice >= 0 && slice < _window.Count ? _window[slice] : null;
    }

    /// <summary>
    /// The painted text of one value cell: the Consumer's painted text where it supplies one
    /// (ADR-0050, item 11), fitted to the column's width with the bold widths for a bold cell, and
    /// the value's own text otherwise — what <c>ExGridRow</c> paints, before the <c>####</c>
    /// decision. A Number or Date the column is too narrow for paints <c>####</c>, but its value's
    /// text is still its accessible name and the Formula Bar's, so a change hidden behind
    /// <c>####</c> is a change (principle 1: refuse rather than write over it unseen). Null for an
    /// Action, Template or Mark cell, which paints no text.
    /// </summary>
    private static string? PaintedTextFor(TRow row, GridColumn<TRow> column, int index, ColumnGeometry geometry,
        CellTextMetrics metrics, PaintedTextOf<TRow>? paintedText, CellAppearanceOf<TRow>? appearance)
    {
        if (!column.PaintsValue)
            return null;
        var text = column.Info.TextFor(column.Value(row));
        if (paintedText is null)
            return text;
        var measured = appearance?.Invoke(row, column).Bold == true ? metrics.Bold : metrics;
        return paintedText(row, column, measured.ContentWidthPx(geometry.DeclaredWidthPxOf(index)), measured) ?? text;
    }

    /// <summary>The painted text the cell at (<paramref name="row"/>, <paramref name="column"/>)
    /// has now, from the row the Window holds there; null with no row in hand, or for a cell that
    /// paints no text.</summary>
    private string? PaintedTextNow(int row, int column)
        => column >= 0 && column < Columns.Count && RowInHand(row) is { } data
            ? PaintedTextFor(data, Columns[column], column, _columnStyles.Geometry, _metrics.CellMetrics, PaintedText, CellAppearance)
            : null;

    /// <summary>What a judgement found (ADR-0142).</summary>
    private enum Seen
    {
        /// <summary>Every painted cell judged shows what it showed: the write may land.</summary>
        Unchanged,

        /// <summary>A painted cell shows other text now.</summary>
        Changed,

        /// <summary>The paint is no longer kept, or was painted under another row order, or a
        /// painted row is no longer in hand: what the user saw can no longer be told.</summary>
        Unknown,
    }

    /// <summary>The paint a gesture names: the newest for one nobody told of, null for one no longer
    /// kept.</summary>
    private Paint? PaintNamed(int told)
        => told == PaintNotTold
            ? (_paints.Count > 0 ? _paints[^1] : null)
            : _paints.FindLast(p => p.Id == told);

    /// <summary>
    /// Judges a write's target (ADR-0142, LV-13): the painted text of every cell of
    /// <paramref name="targets"/> that the paint <paramref name="told"/> painted, against the text
    /// the same cell has now. Cells the paint did not paint were not seen, and are not compared,
    /// so a target of a million rows costs what the painted rows cost. A cell under the Cell
    /// Editor, <paramref name="covered"/>, is left out: the editor covers it, and it is judged
    /// against what it showed when the editor opened instead (LV-11).
    /// </summary>
    private Seen JudgeTargets(int told, IReadOnlyList<SelectionRange> targets, CellPosition? covered = null)
    {
        if (told == PaintNotTold && _paints.Count == 0)
            return Seen.Unchanged;
        // Positions name rows only under the order they were painted in (ADR-0011): under another,
        // the target's cells showed other rows.
        if (PaintNamed(told) is not { } paint || paint.SequenceVersion != _sequenceVersion)
            return Seen.Unknown;
        var last = paint.FirstRow + paint.Rows.Length - 1;
        foreach (var range in targets)
        {
            var right = Math.Min(range.RightColumn, paint.Columns.Count - 1);
            for (var row = Math.Max(range.TopRow, paint.FirstRow); row <= Math.Min(range.BottomRow, last); row++)
            {
                for (var column = Math.Max(range.LeftColumn, 0); column <= right; column++)
                {
                    if (covered is { } editing && editing.Row == row && editing.Column == column)
                        continue;
                    if (paint.TextAt(row, column) is not { } seen)
                        continue;
                    // A painted row the Window no longer holds: what it shows now cannot be told.
                    if (RowInHand(row) is null)
                        return Seen.Unknown;
                    if (!string.Equals(seen, PaintedTextNow(row, column), StringComparison.Ordinal))
                        return Seen.Changed;
                }
            }
        }
        return Seen.Unchanged;
    }

    /// <summary>
    /// Judges an Action press (ADR-0142, LV-12): the painted text of every cell of the pressed
    /// row that the paint <paramref name="told"/> painted, against the row's text now. The row is
    /// found as the user pressed it: the instance the pressed button was painted with in that
    /// paint, or — for one handed over by key, or whose button already shows a newer instance —
    /// the row at its position, which names the same row only under the order the paint was
    /// painted in. <paramref name="atRow"/> is that position where the gesture names it (Space on
    /// the Focus).
    /// </summary>
    private Seen JudgeActionRow(int told, TRow pressed, int? atRow)
    {
        if (told == PaintNotTold && _paints.Count == 0)
            return Seen.Unchanged;
        if (PaintNamed(told) is not { } paint)
            return Seen.Unknown;
        var sameOrder = paint.SequenceVersion == _sequenceVersion;
        var painted = Array.FindIndex(paint.Rows, r => ReferenceEquals(r, pressed));
        int? then = painted >= 0 ? paint.FirstRow + painted : null;
        var now = atRow ?? PositionInHand(pressed);
        if (then is null && now is null)
            return Seen.Unknown;
        if (then is null || now is null)
        {
            if (!sameOrder)
                return Seen.Unknown;
            then ??= now;
            now ??= then;
        }
        var thenRow = then!.Value;
        var nowRow = now!.Value;
        // A row the paint did not paint was not seen, and nothing of it is compared: Space on an
        // action whose row the view has scrolled away from fires as before (ADR-0142).
        if (thenRow < paint.FirstRow || thenRow >= paint.FirstRow + paint.Rows.Length)
            return Seen.Unchanged;
        if (RowInHand(nowRow) is null)
            return Seen.Unknown;
        for (var column = 0; column < paint.Columns.Count; column++)
        {
            if (paint.TextAt(thenRow, column) is not { } seen)
                continue;
            if (!string.Equals(seen, PaintedTextNow(nowRow, column), StringComparison.Ordinal))
                return Seen.Changed;
        }
        return Seen.Unchanged;
    }

    /// <summary>Where the Window holds <paramref name="row"/> among the rows painted now, or null.
    /// The Window never holds one instance twice (ADR-0003), so the answer is unambiguous.</summary>
    private int? PositionInHand(TRow row)
    {
        if (_visible is not { } visible)
            return null;
        for (var position = visible.Start; position < visible.Start + visible.Count; position++)
        {
            if (ReferenceEquals(RowInHand(position), row))
                return position;
        }
        return null;
    }

    /// <summary>
    /// Refuses a write whose painted target changed (ADR-0142, LV-13), through
    /// <see cref="OnPasteRefused"/> — the one gate paste, fill and Delete go through (ADR-0035).
    /// Judged after the operation's other rules, so an existing refusal still says its own
    /// reason. Answers whether it refused.
    /// </summary>
    private async Task<bool> RefuseWriteOverChangedTargetAsync(int told, IReadOnlyList<SelectionRange> targets,
        CellPosition? covered = null)
    {
        var seen = JudgeTargets(told, targets, covered);
        if (seen == Seen.Unchanged)
            return false;
        if (OnPasteRefused.HasDelegate)
        {
            await OnPasteRefused.InvokeAsync(seen == Seen.Changed
                ? PasteRefusalReason.TargetChanged
                : PasteRefusalReason.RenderNoLongerKept);
        }
        return true;
    }

    // What the edited cell painted when the editor opened (ADR-0142, LV-11), and after a refused
    // commit, what the refusal showed: the text a commit is judged against. Null while no edit is
    // open, or over a cell that paints no text.
    private string? _editSeenText;

    /// <summary>
    /// Keeps the edited cell's painted text as the editor opens over it (ADR-0142, LV-11). The
    /// text the core paints now, not the text in the render the opening key or press was taken
    /// against: the ADR keeps it "when the editor opens", and the user's own write just before —
    /// <c>1</c> Enter ↑ <c>2</c> Enter typed at machine speed on a circuit — has changed that
    /// render's text with nobody else's change in it. From here on the editor covers the cell.
    /// </summary>
    private void KeepSeenText(CellPosition cell) => _editSeenText = PaintedTextNow(cell.Row, cell.Column);

    /// <summary>
    /// Refuses a commit whose cell paints other text than the editor was opened over, or than the
    /// last refusal showed (ADR-0142, LV-11), raising the reason with the text it paints now. The
    /// text the refusal shows is what the next commit is judged against. Answers whether it
    /// refused; the editor, its typing and the Focus are left as they are.
    /// </summary>
    private async Task<bool> RefuseCommitOverChangedCellAsync(CellPosition cell)
    {
        // A row out of the Window has nothing to compare: a commit has discarded the edit before
        // asking (RowLeftTheWindow), and a Ctrl+Enter fill goes on without it, as its verdict does.
        if (_editSeenText is not { } seen || RowInHand(cell.Row) is null)
            return false;
        var now = PaintedTextNow(cell.Row, cell.Column) ?? "";
        if (string.Equals(seen, now, StringComparison.Ordinal))
            return false;
        _editSeenText = now;
        if (OnCommitRefused.HasDelegate)
            await OnCommitRefused.InvokeAsync(new GridCommitRefusal(cell, Columns[cell.Column].Name, now));
        return true;
    }

    // The paint the next Action press by pointer was taken against, told by the grid's listener
    // just before Blazor dispatches its click (ADR-0142, LV-12), until that press is heard.
    private int? _actionPressTold;

    /// <summary>
    /// What the next press on an action of this grid's own rows was taken against (ADR-0142,
    /// LV-12): the paint the Viewport named at its mousedown. Told by the grid's listener at the
    /// release on the same button, just before Blazor dispatches the click, so the click the core
    /// hears next is the one it describes. Reading what the render wrote is not a measurement, and
    /// nothing per cell crosses (ADR-0021, note of 2026-10-05).
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="paint">The paint the Viewport named at the press (<c>data-ex-paint</c>).</param>
    [JSInvokable]
    public void ActionPressTakenAt(int paint)
    {
        if (!_disposed)
            _actionPressTold = paint;
    }

    /// <summary>The paint the press being heard was told, once: the next press is its own.</summary>
    private int TakeActionPressTold()
    {
        var told = _actionPressTold ?? PaintNotTold;
        _actionPressTold = null;
        return told;
    }

    /// <summary>
    /// Raises an Action press, or its refusal (ADR-0020, ADR-0142): judged on the painted text of
    /// its row's painted cells in the paint it was taken against, against the row's now.
    /// </summary>
    private async Task FireOrRefuseActionAsync(GridActionEventArgs<TRow> args, int told, int? atRow)
    {
        var seen = JudgeActionRow(told, args.Row, atRow);
        if (seen != Seen.Unchanged)
        {
            if (OnActionRefused.HasDelegate)
            {
                await OnActionRefused.InvokeAsync(new GridActionRefusal<TRow>(args,
                    seen == Seen.Changed ? ActionRefusalReason.RowChanged : ActionRefusalReason.RenderNoLongerKept));
            }
            return;
        }
        if (OnAction.HasDelegate)
            await OnAction.InvokeAsync(args);
    }
}
