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
//
// Settled while building it (ADR-0142, D1 to D5 of 2026-10-06):
// - The user's own writes count as seen (D1). A cell one of the user's own earlier gestures wrote,
//   after the paint a later gesture was taken against, is not compared for that gesture. "After"
//   is the order the grid handled them in, never a time: a write notes the newest paint named when
//   it is raised, and a later gesture taken against that paint or an older one lets its cells off.
//   `5` Enter ↑ Ctrl+V typed at once therefore pastes on both hosts.
// - The Cell Editor keeps the painted text of the paint the gesture that opened it was taken
//   against (D2), with D1's rule; a fill judges its source as well as its target (D4).
// - Before a write is judged, the bound source puts out what it has gathered, and the Window is
//   taken in again (D5), so the write is judged against — and carries — the newest version.
// - With a Row Key, an Action press is paired with its row by key.
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Cell Editor commit refused because the edited cell paints other text than it did in the
    /// render the gesture that opened the editor was taken against (ADR-0142, LV-11, D2): the
    /// value changed upstream after the user last saw it — before the editor opened, or while the
    /// user typed under an editor that covers it. A cell the user's own earlier write changed in
    /// between is judged from what it paints at the open (D1). The bound source is asked for what
    /// it has gathered first, so a change it was holding refuses the commit too (D5, LV-16). No
    /// Edit Intent is raised, and the editor stays open with what was
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
    /// (<see cref="ActionRefusalReason.RowChanged"/>), or the grid can no longer tell
    /// (<see cref="ActionRefusalReason.RenderNoLongerKept"/>). With a Row Key in force the press
    /// is paired with its row by key, so a row that only moved under a new order is judged on its
    /// cells; without one, such a press cannot be paired and is refused as before. Cells the
    /// user's own earlier write changed are not compared (D1), and the bound source is asked for
    /// what it has gathered first (D5). Nothing is raised through
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
    /// against what it showed when the editor opened instead (LV-11). A cell the user's own
    /// earlier gesture wrote after that paint is left out too (D1, LV-17). A fill passes its
    /// source among the ranges, since it writes the source's values (D4).
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
                    // The user wrote it after this paint: they know what it holds (D1).
                    if (WrittenByUserSince(paint.Id, row, column))
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
    /// found as the user pressed it. With a Row Key in force it is paired by key: the row under
    /// the pressed row's key in that paint, and the row under it now, wherever either stands, so
    /// a press whose row moved under a new order is judged on that row (ADR-0140). Without one, it
    /// is the instance the pressed button was painted with in that paint, or — for one whose
    /// button already shows a newer instance — the row at its position, which names the same row
    /// only under the order the paint was painted in; a press that cannot be paired so is refused
    /// as taken against a render no longer kept. <paramref name="atRow"/> is the press's position
    /// where the gesture names it (Space on the Focus). A cell the user's own earlier gesture
    /// wrote after that paint is not compared (D1, LV-17).
    /// </summary>
    private Seen JudgeActionRow(int told, TRow pressed, int? atRow, out int? judgedAt)
    {
        judgedAt = null;
        if (told == PaintNotTold && _paints.Count == 0)
        {
            judgedAt = atRow ?? (_rowKey is { } key0 ? PositionInHandByKey(key0, key0(pressed)) : PositionInHand(pressed));
            return Seen.Unchanged;
        }
        if (PaintNamed(told) is not { } paint)
            return Seen.Unknown;
        int thenRow;
        int nowRow;
        if (_rowKey is { } rowKey)
        {
            // Paired by key (ADR-0142, settled 2026-10-06): the key names the row across versions
            // and orders, so neither the instance nor the position has to have stayed.
            var key = rowKey(pressed);
            // A row the paint did not paint was not seen, and nothing of it is compared, as below.
            if (PaintedPositionOf(paint, rowKey, key) is not { } then)
            {
                judgedAt = PositionInHandByKey(rowKey, key);
                return Seen.Unchanged;
            }
            thenRow = then;
            var now = atRow is { } at && RowInHand(at) is { } there && Equals(rowKey(there), key)
                ? at
                : PositionInHandByKey(rowKey, key);
            // The row is no longer in the Window: what it shows now cannot be told.
            if (now is null)
                return Seen.Unknown;
            nowRow = now.Value;
        }
        else
        {
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
            thenRow = then!.Value;
            nowRow = now!.Value;
            // A row the paint did not paint was not seen, and nothing of it is compared: Space on
            // an action whose row the view has scrolled away from fires as before (ADR-0142).
            if (thenRow < paint.FirstRow || thenRow >= paint.FirstRow + paint.Rows.Length)
            {
                judgedAt = PositionInHand(pressed) ?? nowRow;
                return Seen.Unchanged;
            }
            if (RowInHand(nowRow) is null)
                return Seen.Unknown;
        }
        for (var column = 0; column < paint.Columns.Count; column++)
        {
            if (paint.TextAt(thenRow, column) is not { } seen)
                continue;
            if (WrittenByUserSince(paint.Id, nowRow, column))
                continue;
            if (!string.Equals(seen, PaintedTextNow(nowRow, column), StringComparison.Ordinal))
                return Seen.Changed;
        }
        judgedAt = nowRow;
        return Seen.Unchanged;
    }

    /// <summary>Where <paramref name="paint"/> painted the row under <paramref name="key"/>, or
    /// null for a row it did not paint. A pass over the painted rows only.</summary>
    private static int? PaintedPositionOf(Paint paint, Func<TRow, object> rowKey, object key)
    {
        for (var i = 0; i < paint.Rows.Length; i++)
        {
            if (paint.Rows[i] is { } row && Equals(rowKey(row), key))
                return paint.FirstRow + i;
        }
        return null;
    }

    /// <summary>Where the Window holds the row under <paramref name="key"/>: among the rows painted
    /// now first, where a pressed row almost always is, and across the whole Window otherwise —
    /// a row the order moved out of view is still the row the user pressed. Null when the Window
    /// no longer holds it. A key repeats in no Window (ADR-0140, LV-2), so the answer is
    /// unambiguous.</summary>
    private int? PositionInHandByKey(Func<TRow, object> rowKey, object key)
    {
        if (_visible is { } visible)
        {
            for (var position = visible.Start; position < visible.Start + visible.Count; position++)
            {
                if (RowInHand(position) is { } row && Equals(rowKey(row), key))
                    return position;
            }
        }
        for (var i = 0; i < _window.Count; i++)
        {
            if (Equals(rowKey(_window[i]), key))
                return _windowStart + i;
        }
        return null;
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

    // What the edited cell painted in the paint the gesture that opened the editor was taken
    // against (ADR-0142, LV-11, D2), and after a refused commit, what the refusal showed: the text a
    // commit is judged against. Null while no edit is open, or over a cell that paints no text.
    private string? _editSeenText;

    // Whether what the opening gesture's paint showed of the edited cell can no longer be told: the
    // paint is no longer kept, or showed the rows in another order. The first commit is then
    // refused with the text the cell paints, as a change is, and the next is judged against it.
    private bool _editSeenUnknown;

    /// <summary>
    /// Keeps what the user saw of the edited cell as the editor opens over it (ADR-0142, LV-11,
    /// D2): its painted text in the paint <paramref name="told"/> that the gesture opening the
    /// editor was taken against — a typed key, F2, a double click, a press into the Formula Bar.
    /// Keeping the text at the open instead left the round trip between the key and the open, in
    /// which a change upstream went unseen under an editor that then covered the cell.
    ///
    /// <para>With D1's rule: a cell the user's own earlier gesture wrote after that paint is kept
    /// as it paints at the open — the user knows what they wrote, and the grid has raised that
    /// write before this gesture, so <c>1</c> Enter ↑ <c>2</c> Enter typed at once commits both
    /// on both hosts. A cell that paint did not paint — off screen, or no row in hand — was not
    /// seen there, and is kept as it paints at the open, which is what the editor shows the user
    /// from then on. A paint no longer kept, or painted under another order, cannot say what was
    /// seen (<see cref="_editSeenUnknown"/>): refused rather than written over something the user
    /// may not have seen (principle 1), as <see cref="CommitRefusalReason.RenderNoLongerKept"/>,
    /// never as a change; it needs more paints than are kept within one round trip.</para>
    /// </summary>
    private void KeepSeenText(CellPosition cell, int told)
    {
        _editSeenUnknown = false;
        var now = PaintedTextNow(cell.Row, cell.Column);
        if (told == PaintNotTold && _paints.Count == 0)
        {
            _editSeenText = now;
            return;
        }
        if (PaintNamed(told) is not { } paint || paint.SequenceVersion != _sequenceVersion)
        {
            _editSeenText = now;
            _editSeenUnknown = true;
            return;
        }
        _editSeenText = WrittenByUserSince(paint.Id, cell.Row, cell.Column)
            ? now
            : paint.TextAt(cell.Row, cell.Column) ?? now;
    }

    /// <summary>
    /// Refuses a commit whose cell paints other text than the opening gesture's paint showed, or
    /// than the last refusal showed (ADR-0142, LV-11), raising the reason with the text it paints
    /// now. The text the refusal shows is what the next commit is judged against. Answers whether
    /// it refused; the editor, its typing and the Focus are left as they are.
    /// </summary>
    private async Task<bool> RefuseCommitOverChangedCellAsync(CellPosition cell)
    {
        // A row out of the Window has nothing to compare: a commit has discarded the edit before
        // asking (RowLeftTheWindow), and a Ctrl+Enter fill goes on without it, as its verdict does.
        if (RowInHand(cell.Row) is null || (_editSeenText is null && !_editSeenUnknown))
            return false;
        var now = PaintedTextNow(cell.Row, cell.Column) ?? "";
        var unknown = _editSeenUnknown;
        if (!unknown && string.Equals(_editSeenText, now, StringComparison.Ordinal))
            return false;
        _editSeenText = now;
        _editSeenUnknown = false;
        var reason = unknown ? CommitRefusalReason.RenderNoLongerKept : CommitRefusalReason.CellChanged;
        if (OnCommitRefused.HasDelegate)
            await OnCommitRefused.InvokeAsync(new GridCommitRefusal(cell, Columns[cell.Column].Name, now, reason));
        return true;
    }

    // The paint the next Action press by pointer was taken against, told by the grid's listener
    // just before Blazor dispatches its click (ADR-0142, LV-12), until that press is heard.
    private int? _actionPressTold;

    /// <summary>
    /// What the next press on an action of this grid's own rows was taken against (ADR-0142,
    /// LV-12): the paint the Viewport named at its mousedown, and the row, column and action the
    /// button stood for in it. Told by the grid's listener at the release on the same button, just
    /// before Blazor dispatches the click, so the click the core hears next is the one it
    /// describes. Reading what the render wrote is not a measurement, and nothing per cell crosses
    /// (ADR-0021, notes of 2026-10-05 and 2026-10-06).
    ///
    /// <para>Blazor does not deliver an event whose attribute a component since disposed had
    /// rendered. A row whose instance a render replaced while the press was on its way has its
    /// component disposed when there is no Row Key (ADR-0140), and the click on its button would
    /// then be lost without a word. So a press whose row component is no longer rendered is
    /// answered here — refused, or fired, by ADR-0142's rule — and one whose component a later
    /// render disposes before its click arrives is answered after that render.</para>
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="paint">The paint the Viewport named at the press (<c>data-ex-paint</c>).</param>
    /// <param name="row">The pressed cell's row, as its id names it, or −1.</param>
    /// <param name="column">The pressed cell's column, as its id names it, or −1.</param>
    /// <param name="action">Which of the cell's actions was pressed, or −1.</param>
    [JSInvokable]
    public async Task ActionPressTakenAt(int paint, int row = -1, int column = -1, int action = -1)
    {
        if (_disposed)
            return;
        _actionPressTold = paint;
        _actionPressPending = null;
        if (row < 0 || column < 0 || action < 0 || PaintNamed(paint) is not { } painted
            || row < painted.FirstRow || row >= painted.FirstRow + painted.Rows.Length
            || painted.Rows[row - painted.FirstRow] is not { } pressed
            || column >= painted.Columns.Count || action >= painted.Columns[column].Actions.Count)
        {
            return;
        }
        _actionPressPending = new ActionPressPending(paint, pressed, painted.Columns[column].Name,
            painted.Columns[column].Actions[action].Name);
        await AnswerActionPressWithNoClickAsync();
    }

    // A pointer press on an action told with what it pressed, until its click is heard, or until
    // the core answers it because no click can come (ActionPressTakenAt).
    private ActionPressPending? _actionPressPending;

    private sealed record ActionPressPending(int Paint, TRow Pressed, string Column, string Action);

    /// <summary>
    /// Answers a told press whose click Blazor will not deliver, because the row component that
    /// rendered its button is no longer rendered: by the Row Key where there is one, by the
    /// instance where there is not (ADR-0140). Raised as the click would have been — judged against
    /// the paint it was taken on, and refused or fired (ADR-0142). A press whose component is still
    /// rendered waits for its click. Decided by the order the core hears things in, never by time.
    /// </summary>
    private async Task AnswerActionPressWithNoClickAsync()
    {
        if (_actionPressPending is not { } press || _disposed || RendersRowOf(press.Pressed))
            return;
        _actionPressPending = null;
        _actionPressTold = null;
        await RaiseActionAsync(new GridActionEventArgs<TRow>(press.Pressed, press.Column, press.Action), press.Paint, atRow: null);
    }

    /// <summary>Whether the newest paint rendered a row component for <paramref name="row"/>: one
    /// under its Row Key, or, with none, for the instance itself.</summary>
    private bool RendersRowOf(TRow row)
    {
        if (_paints.Count == 0)
            return true;
        var rows = _paints[^1].Rows;
        if (_rowKey is { } rowKey)
        {
            var key = rowKey(row);
            foreach (var painted in rows)
            {
                if (painted is not null && Equals(rowKey(painted), key))
                    return true;
            }
            return false;
        }
        // By reference, never by value: a record row equal to the pressed one is another row's
        // component (ADR-0003).
        foreach (var painted in rows)
        {
            if (ReferenceEquals(painted, row))
                return true;
        }
        return false;
    }

    /// <summary>The paint the press being heard was told, once: the next press is its own. Its
    /// click has come, so the core has nothing left to answer for it.</summary>
    private int TakeActionPressTold()
    {
        var told = _actionPressTold ?? PaintNotTold;
        _actionPressTold = null;
        _actionPressPending = null;
        return told;
    }

    /// <summary>
    /// Raises an Action press, or its refusal (ADR-0020, ADR-0142): judged on the painted text of
    /// its row's painted cells in the paint it was taken against, against the row's now.
    /// </summary>
    private async Task FireOrRefuseActionAsync(GridActionEventArgs<TRow> args, int told, int? atRow)
    {
        var seen = JudgeActionRow(told, args.Row, atRow, out var judgedAt);
        if (seen != Seen.Unchanged)
        {
            if (OnActionRefused.HasDelegate)
            {
                await OnActionRefused.InvokeAsync(new GridActionRefusal<TRow>(args,
                    seen == Seen.Changed ? ActionRefusalReason.RowChanged : ActionRefusalReason.RenderNoLongerKept));
            }
            return;
        }
        // Raised with the version it was judged against (ADR-0142 D5, LV-16), as an Edit Intent
        // carries the newest row: the button held the row it was painted with, and a gathered change
        // to cells the user did not see may have replaced it since. A handler that writes the row back
        // through GridSource.From's ReplaceRow would otherwise be refused for a stale version — the
        // exception D5 exists to prevent. The pressed instance stays where it is still the one in hand.
        if (judgedAt is { } at && RowInHand(at) is { } newest && !ReferenceEquals(newest, args.Row)
            && (_rowKey is { } rowKey ? Equals(rowKey(newest), rowKey(args.Row)) : PositionInHand(args.Row) is null))
        {
            args = args with { Row = newest };
        }
        if (OnAction.HasDelegate)
            await OnAction.InvokeAsync(args);
    }

    // ---- D1: the user's own writes count as seen ----

    /// <summary>A write the grid raised for one of the user's own gestures (ADR-0142, D1): the
    /// cells it named, the newest paint named when it was raised, and the order its positions are
    /// written in. A class, so the one noted is the one taken back.</summary>
    private sealed class OwnWrite(int afterPaint, int sequenceVersion, IReadOnlyList<SelectionRange> cells)
    {
        public int AfterPaint { get; } = afterPaint;

        public int SequenceVersion { get; } = sequenceVersion;

        public IReadOnlyList<SelectionRange> Cells { get; } = cells;
    }

    // The user's own writes a gesture taken against a kept paint may still be told of, oldest
    // first. A write raised before the oldest kept paint can let off no gesture's cells — a gesture
    // told an older paint is refused anyway — and one under another order names other rows; both
    // go when the next is noted. A bound beside that, for a Consumer whose writes never repaint.
    private const int OwnWritesKept = 256;
    private readonly List<OwnWrite> _ownWrites = [];

    /// <summary>
    /// Notes a write the grid raises for the user's own gesture (ADR-0142, D1): an Edit Intent, a
    /// paste or fill intent, a Clear Intent, a Fill Intent — anything it raises as a write. Noted
    /// as it is raised, before the Consumer hears it, so a gesture the grid handles while the
    /// Consumer's handler awaits is judged after it, as it was typed. The newest paint named then
    /// is what orders it: a later gesture taken against that paint, or an older one, was taken
    /// before the write could be painted. An Action is not one: the grid cannot tell what it writes.
    /// </summary>
    private OwnWrite NoteOwnWrite(IReadOnlyList<SelectionRange> cells)
    {
        var oldestKept = _paints.Count > 0 ? _paints[0].Id : 0;
        _ownWrites.RemoveAll(w => w.AfterPaint < oldestKept || w.SequenceVersion != _sequenceVersion);
        var write = new OwnWrite(_paintId, _sequenceVersion, cells);
        _ownWrites.Add(write);
        if (_ownWrites.Count > OwnWritesKept)
            _ownWrites.RemoveAt(0);
        return write;
    }

    /// <summary>Takes back a write the Consumer refused (ADR-0050, items 3 and 5): it wrote
    /// nothing, so the cells it named are compared as any others.</summary>
    private void ForgetOwnWrite(OwnWrite write) => _ownWrites.Remove(write);

    /// <summary>Whether one of the user's own writes, raised after the paint <paramref name="paint"/>
    /// was named, wrote the cell at (<paramref name="row"/>, <paramref name="column"/>) under the
    /// order in force (D1).</summary>
    private bool WrittenByUserSince(int paint, int row, int column)
    {
        foreach (var write in _ownWrites)
        {
            if (write.AfterPaint < paint || write.SequenceVersion != _sequenceVersion)
                continue;
            foreach (var range in write.Cells)
            {
                if (range.Contains(new CellPosition(row, column)))
                    return true;
            }
        }
        return false;
    }

    // ---- D5: the source puts out what it has gathered before a write is judged ----

    // Set while the grid asks its bound source to put out what it has gathered (ADR-0141/0142,
    // D5). The source raises StateChanged inside the call, on this thread, when it published
    // something; that event is noted instead of answered, and the Window is taken in once, after
    // the call, by the write that asked — never twice, and never half-way through the write.
    private bool _askingGathered;
    private bool _gatheredHeard;

    /// <summary>
    /// Asks the bound source to put out what it has gathered, on the grid's own synchronization
    /// context, and takes its Window in again through the state application, as a change it
    /// announced is taken in (ADR-0141/0142, D5; LV-16). Called just before a write is judged — a
    /// commit, an Action, a paste, a fill, a clear — so the write is judged against, and carries,
    /// the newest version. A change gathered while the user typed is brought forward by the
    /// gesture, as in ExPivot (ADR-0067), never waited for. Without a source, or with nothing
    /// gathered, nothing happens. A gathered change that moved the order drops the Selection and
    /// discards an open edit, as any such change does (ADR-0011): the caller reads the state
    /// again before it goes on. Answers false when the source or the new state was refused; the
    /// failure is reported as a source's is, and the write is not made.
    /// </summary>
    private async Task<bool> TakeInGatheredAsync()
    {
        if (_disposed)
            return false;
        if (Source is not { } source)
            return true;
        _gatheredHeard = false;
        try
        {
            _askingGathered = true;
            try
            {
                source.PublishGathered();
            }
            finally
            {
                _askingGathered = false;
            }
            var heard = _gatheredHeard;
            _gatheredHeard = false;
            // A source that moved without saying so inside the call is taken in all the same.
            if (!heard && !SourceMovedOn(source))
                return true;
            ApplyState();
        }
        catch (Exception ex)
        {
            await DispatchExceptionAsync(ex);
            return false;
        }
        // The newest version is painted as well as judged: a press heard through a row's own
        // handler renders that row, not this root.
        _suppressRender = false;
        StateHasChanged();
        return !_disposed;
    }

    /// <summary>Whether the source's state is not what the grid last took in.</summary>
    private bool SourceMovedOn(IGridSource<TRow> source)
        => !ReferenceEquals(source.Window, _window) || source.WindowStart != _windowStart
           || source.TotalCount != _total || source.IsLoading != _loading
           || source.RowSequenceVersion != _sequenceVersion;

    // ---- D2: a press into the Formula Bar carries its paint ----

    // The paint the next press into the Formula Bar was taken against, told by the grid's listener
    // at the press, before the focus it gives the bar is dispatched (ADR-0142, D2), until that focus
    // is heard; and the paint of the last such press, for its answer in its turn among held keys.
    private int? _barPressTold;
    private int _barPressPaint = PaintNotTold;

    /// <summary>
    /// What the next press into this grid's Formula Bar was taken against (ADR-0142, LV-11, D2):
    /// the paint the Viewport named at its mousedown. Told by the grid's listener at the press,
    /// before the focus it gives the bar is dispatched, so the focus the core hears next — which
    /// opens the editor — is the one it describes, and the editor keeps what that paint showed of
    /// the cell. Reading what the render wrote is not a measurement, and nothing per cell crosses
    /// (ADR-0021, note of 2026-10-05).
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="paint">The paint the Viewport named at the press (<c>data-ex-paint</c>).</param>
    [JSInvokable]
    public void BarPressTakenAt(int paint)
    {
        if (!_disposed)
            _barPressTold = paint;
    }

    /// <summary>The paint the focus being heard was told, once; kept as the last press's for its
    /// answer (<see cref="BarPressAnsweredAsync"/>). A focus nobody told of — Tab, a Chrome's own
    /// field — is judged against the newest paint.</summary>
    private int TakeBarPressTold()
    {
        var told = _barPressTold ?? PaintNotTold;
        _barPressTold = null;
        _barPressPaint = told;
        return told;
    }
}
