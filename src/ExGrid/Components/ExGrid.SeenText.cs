using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// A write lands as the user entered it, on the row the user aimed it at (ADR-0142, rewritten
// 2026-10-07; LV-12 to LV-14, LV-20). No write compares painted text. What stops one is the
// operation's own rules (ADR-0014/0035), the Consumer, its row being gone, or the order it was aimed
// under having moved (ADR-0011).
//
// Each render that can change what its painted cells show is a new paint, named on the Viewport
// (data-ex-paint, beside data-ex-sequence and data-ex-layout). The browser reads that name with each
// gesture and tells it with the gesture (ADR-0021's notes of 2026-10-05 to 2026-10-07). The grid uses
// it for where a gesture lands and which order it was aimed under, never for what: a positional
// write is checked against the order its gesture was aimed under, and an Action press finds the row
// component that painted its button. Of its last paints the grid keeps numbers only — the order each
// was painted under, its first row, and a serial for each painted row's component — and never a row
// or a Row Key: it holds no Consumer row beyond the Window it was given (ADR-0160).
//
// Before a write is handled, the bound source puts out what it has gathered, so the write is made on
// the newest version (ADR-0142, D5 of 2026-10-06). The Cell Editor keeps what its cell paints when it
// opens, and a commit over a cell that changed under it lands with an Overwrite Notice; the user's
// own writes not yet painted when it opened count as seen (D1, kept for the notice alone).
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Cell Editor commit refused because the grid can no longer find the row the editor was
    /// opened on (ADR-0142, LV-20): with a Row Key, no row in the Window answers its key
    /// (<see cref="CommitRefusalReason.RowGone"/>); without one, the Row Sequence Version moved
    /// since the editor opened (<see cref="CommitRefusalReason.OrderMoved"/>). Without a Row Key, a
    /// row that left the Window under the same order takes the typing with it, raised through
    /// <see cref="OnEditDiscarded"/> as <see cref="EditDiscardReason.RowLeftTheWindow"/> (ADR-0011,
    /// ED-21). The bound source is
    /// asked for what it has gathered first (D5, LV-16), and a move it brings in is answered here,
    /// not by a discard. No Edit Intent is raised, and the editor stays open with what was typed,
    /// held from then on; Escape leaves without writing. Every commit gesture is refused alike, and,
    /// as after a Reject, the gesture keeps no meaning of its own. A commit is never refused because
    /// the cell's value changed under the editor: it lands, and <see cref="OnOverwriteNotice"/> tells
    /// it. The grid holds no string for it; Chrome words it into its refusal live region (A11Y-16).
    /// </summary>
    [Parameter] public EventCallback<GridCommitRefusal> OnCommitRefused { get; set; }

    /// <summary>
    /// The Overwrite Notice (ADR-0142, LV-11, LV-21): a Cell Editor commit — Enter, Tab, an arrow in
    /// Overwrite, a press elsewhere, Ctrl+Enter — landed over a cell whose painted text changed while
    /// the editor covered it. Raised once the commit's intent was accepted, with the cell, the text
    /// the editor opened over and the text the commit replaced; the Edit Intent carries the same two
    /// texts. A commit over an unchanged cell raises none. A cell one of the user's own earlier
    /// writes covered, not yet painted when the editor opened, is not reported (D1, LV-17), so the
    /// same keys give the same outcome on both hosts. The grid holds no string for it; Chrome words
    /// it into the root's live region, as it words a refusal (A11Y-16), and nothing is added to the
    /// row (ADR-0013).
    /// </summary>
    [Parameter] public EventCallback<GridOverwriteNotice> OnOverwriteNotice { get; set; }

    /// <summary>
    /// An Action press refused (ADR-0142, LV-12, LV-20): its row is gone
    /// (<see cref="ActionRefusalReason.RowGone"/>), or it named its row by a position under an order
    /// that has moved since (<see cref="ActionRefusalReason.OrderMoved"/>). A press never is refused
    /// because its row's values changed: it acts on the row it was pressed on, as that row is now.
    /// With a Row Key, that is the row under the pressed row's key, wherever it stands. Without one,
    /// a press whose button a render has since disposed acts on the row at the position it was told,
    /// while the order it was taken under holds. Nothing is raised through <see cref="OnAction"/>.
    /// The refusal carries the press as it would have been raised; its row is the pressed row where
    /// the grid still has it, and <see langword="default"/> where the grid never resolved it,
    /// because the grid holds no row beyond its Window (ADR-0160). A Template cell's own controls
    /// are the Consumer's (ADR-0037). The grid holds no string for it; Chrome words it into its
    /// refusal live region (A11Y-16).
    /// </summary>
    [Parameter] public EventCallback<GridActionRefusal<TRow>> OnActionRefused { get; set; }

    // What a gesture says when the browser told no paint: a press made by script with no mousedown
    // before it, or a component test driving the core directly. It is taken as aimed at the newest
    // paint — what the core last painted.
    private const int PaintNotTold = -1;

    // The newest paint's name, and what it was painted from. A render painted from the same is the
    // same paint, and keeps the name.
    private int _paintId;
    private PaintBasis? _painted;

    // The first paint painted under the order the newest paint was painted under: a gesture told an
    // earlier one was aimed under another order (ADR-0011).
    private int _orderPaintedFrom;

    // Moved whenever a new Window replaces a row instance at a position the newest paint painted. It
    // stands in for the rows of that paint, which the grid does not keep (ADR-0160): compared while
    // the old Window and the new one are both in hand, never later.
    private int _paintedRowsVersion;

    /// <summary>What a paint was painted from, as far as what its cells can show: the order, the
    /// painted rows, the columns, their geometry, which slice of them, the metrics, and the
    /// Consumer's lookups the text depends on. References and numbers only, never a row: the
    /// painted rows are named by <see cref="RowsVersion"/>.</summary>
    private sealed record PaintBasis(
        int Sequence, int FirstRow, int Count, int RowsVersion, IReadOnlyList<GridColumn<TRow>> Columns,
        ColumnGeometry Geometry, ColumnRange? Scrollable, CellTextMetrics Metrics, PaintedTextOf<TRow>? PaintedText,
        CellAppearanceOf<TRow>? Appearance, Func<TRow, object>? RowKey)
    {
        /// <summary>Whether a render painted from what the parameters name paints what this one
        /// painted. The columns, geometry and lookups compare by reference, as the rows render.</summary>
        public bool Paints(int sequence, int firstRow, int count, int rowsVersion, IReadOnlyList<GridColumn<TRow>> columns,
            ColumnGeometry geometry, ColumnRange? scrollable, CellTextMetrics metrics, PaintedTextOf<TRow>? paintedText,
            CellAppearanceOf<TRow>? appearance, Func<TRow, object>? rowKey)
            => Sequence == sequence && FirstRow == firstRow && Count == count && RowsVersion == rowsVersion
               && ReferenceEquals(Columns, columns) && ReferenceEquals(Geometry, geometry) && Scrollable == scrollable
               && Metrics == metrics && ReferenceEquals(PaintedText, paintedText) && ReferenceEquals(Appearance, appearance)
               && Equals(RowKey, rowKey);
    }

    /// <summary>
    /// Names the paint this render paints, starting a new one if what its cells can show changed
    /// (ADR-0142). Called from the Viewport's own attribute, so whatever path led to the render, the
    /// name is computed from the very state the rows below it are painted from. A render that changes
    /// no painted cell's text — a Selection moved, a popover opened, a row off screen replaced — keeps
    /// the name.
    /// </summary>
    private int NotePaint()
    {
        EndRowComponents();
        var first = _visible?.Start ?? 0;
        var count = _visible?.Count ?? 0;
        // A Placeholder paints its Pinned Columns only (ADR-0004).
        var scrollable = _placeholderMode ? null : _scrollable;
        var geometry = _columnStyles.Geometry;
        var metrics = _metrics.CellMetrics;
        if (_painted is { } last && last.Paints(_sequenceVersion, first, count, _paintedRowsVersion, Columns, geometry,
                scrollable, metrics, PaintedText, CellAppearance, _rowKey))
        {
            return _paintId;
        }
        var id = ++_paintId;
        if (_painted is null || _painted.Sequence != _sequenceVersion)
            _orderPaintedFrom = id;
        _painted = new PaintBasis(_sequenceVersion, first, count, _paintedRowsVersion, Columns, geometry, scrollable,
            metrics, PaintedText, CellAppearance, _rowKey);
        StartRowComponents(id, first, count);
        return id;
    }

    /// <summary>
    /// Notes whether the Window just taken in replaced a row instance at a position the newest paint
    /// painted, comparing it with <paramref name="previous"/>, the Window it replaced, while both are
    /// in hand (ADR-0160). A pass over the painted positions only.
    /// </summary>
    private void NotePaintedRowsReplaced(IReadOnlyList<TRow> previous, int previousStart)
    {
        if (_painted is not { } painted || (ReferenceEquals(previous, _window) && previousStart == _windowStart))
            return;
        for (var row = painted.FirstRow; row < painted.FirstRow + painted.Count; row++)
        {
            var before = row - previousStart;
            var then = before >= 0 && before < previous.Count ? previous[before] : null;
            if (!ReferenceEquals(then, RowInHand(row)))
            {
                _paintedRowsVersion++;
                return;
            }
        }
    }

    /// <summary>Whether a gesture told the paint <paramref name="told"/> was aimed under an order
    /// that has moved since (ADR-0011): its positions name other rows now. One nobody told of is
    /// taken as aimed at the newest paint, under the order in force.</summary>
    private bool AimedUnderAnotherOrder(int told)
        => told != PaintNotTold
           && (told < _orderPaintedFrom || _painted is not { } newest || newest.Sequence != _sequenceVersion);

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
    /// <c>####</c> is a change (principle 1). Null for an Action, Template or Mark cell, which
    /// paints no text.
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

    /// <summary>
    /// Refuses a positional write whose gesture was aimed under an order that has moved since
    /// (ADR-0142, LV-13; ADR-0011), through <see cref="OnPasteRefused"/> — the one gate paste, fill
    /// and Delete go through (ADR-0035). The Selection the gesture was aimed with went with that
    /// order, so the write is refused as aimed at no Selection. Answers whether it refused.
    /// </summary>
    private async Task<bool> RefuseWriteAimedUnderAnotherOrderAsync(int told)
    {
        if (!AimedUnderAnotherOrder(told))
            return false;
        if (OnPasteRefused.HasDelegate)
            await OnPasteRefused.InvokeAsync(PasteRefusalReason.EmptySelection);
        return true;
    }

    // ---- The Cell Editor: a commit lands, and a change under the editor is told ----

    // What the edited cell painted when the editor opened (ADR-0142, LV-11): the text a commit's
    // Overwrite Notice compares with. Null while no edit is open, or over a cell that paints no text.
    private string? _editSeenText;

    // Whether one of the user's own writes, not yet painted when the editor opened, covered the
    // edited cell (D1, LV-17): a change the commit finds there is the user's own, and is not told.
    private bool _editOpenedBeforeOwnWrite;

    // The Row Sequence Version the editor was opened under: without a Row Key, the commit lands by
    // position under it (ADR-0142).
    private int _editSequence;

    // Whether a commit of this edit has been refused (LV-20). The editor is held from then on: an
    // order move no longer discards it (ADR-0011), since the typing is never thrown away for what the
    // user did not do (ADR-0142). With a Row Key, the row's key went with the refusal, so every later
    // commit is refused too; Escape leaves.
    private bool _editHeld;
    private bool _editRowLost;

    // Set while a commit takes in what the bound source gathered, and while the Consumer hears its
    // intent: an order move that brings in is the commit's to answer — refused, or followed by key —
    // never ADR-0011's discard.
    private bool _committingEdit;

    /// <summary>Notes what the edit opening over <paramref name="cell"/> starts from (ADR-0142,
    /// LV-11): what the cell paints now, the order in force, and whether the user's own write
    /// covers the cell unpainted (D1). A change in the round trip between the gesture that opens the
    /// editor and the open is not seen under the editor, and is the user's to accept.</summary>
    private void NoteEditOpened(CellPosition cell)
    {
        _editSeenText = PaintedTextNow(cell.Row, cell.Column);
        _editOpenedBeforeOwnWrite = WrittenSinceNewestPaint(cell);
        _editSequence = _sequenceVersion;
        _editHeld = false;
        _editRowLost = false;
    }

    /// <summary>Takes in what the bound source has gathered for a commit (D5), answering an order
    /// move it brings in with the commit rather than with ADR-0011's discard.</summary>
    private async Task<bool> TakeInGatheredForCommitAsync()
    {
        _committingEdit = true;
        try
        {
            return await TakeInGatheredAsync();
        }
        finally
        {
            _committingEdit = false;
        }
    }

    /// <summary>Whether an order move just taken in leaves the open edit standing, instead of
    /// discarding it (ADR-0011): one a commit is answering, or one that came after a commit of this
    /// edit was refused.</summary>
    private bool EditOutlivesOrderMove => _committingEdit || _editHeld;

    /// <summary>
    /// Where a commit lands (ADR-0142, LV-20): on the row the editor was opened on — by its Row Key
    /// wherever the row is now, or, without one, at the editor's position while the order it was
    /// opened under holds. <paramref name="openedKey"/> is that row's key, read before the bound
    /// source put out what it gathered. Otherwise the commit is refused, <c>RowGone</c> or
    /// <c>OrderMoved</c>, the editor is held, and null is answered. Without a Row Key, the caller
    /// has already taken a row that left the Window under the same order (ED-21).
    /// </summary>
    private async Task<CellPosition?> CommitLandingAsync(object? openedKey)
    {
        var cell = _editingCell;
        CommitRefusalReason reason;
        if (_rowKey is { } rowKey)
        {
            if (!_editRowLost && openedKey is not null && PositionInHandByKey(rowKey, openedKey) is { } at)
                return new CellPosition(at, cell.Column);
            _editRowLost = true;
            reason = CommitRefusalReason.RowGone;
        }
        else if (_sequenceVersion != _editSequence)
        {
            reason = CommitRefusalReason.OrderMoved;
        }
        else
        {
            return cell;
        }
        _editHeld = true;
        if (OnCommitRefused.HasDelegate)
        {
            await OnCommitRefused.InvokeAsync(new GridCommitRefusal(cell, Columns[cell.Column].Name,
                PaintedTextNow(cell.Row, cell.Column) ?? "", reason));
        }
        return null;
    }

    /// <summary>The Overwrite Notice a commit landing on <paramref name="cell"/> owes (ADR-0142,
    /// LV-11): the text the editor opened over and the text the cell paints now, when they differ and
    /// the change is not the user's own unpainted write (D1). Null when none is owed.</summary>
    private GridOverwriteNotice? OverwriteNoticeFor(CellPosition cell, string replaced)
        => _editSeenText is { } seen && !string.Equals(seen, replaced, StringComparison.Ordinal) && !_editOpenedBeforeOwnWrite
            ? new GridOverwriteNotice(cell, Columns[cell.Column].Name, seen, replaced)
            : null;

    private async Task RaiseOverwriteNoticeAsync(GridOverwriteNotice? notice)
    {
        if (notice is { } told && OnOverwriteNotice.HasDelegate && !_disposed)
            await OnOverwriteNotice.InvokeAsync(told);
    }

    // ---- D1: the user's own writes count as seen, for the Overwrite Notice ----

    /// <summary>A write the grid raised for one of the user's own gestures (ADR-0142, D1): the cells
    /// it named, as positions, the newest paint when it was raised, and the order its positions are
    /// written in. Positions and numbers only, never a row (ADR-0160). A class, so the one noted is
    /// the one taken back.</summary>
    private sealed class OwnWrite(int afterPaint, int sequenceVersion, IReadOnlyList<SelectionRange> cells)
    {
        public int AfterPaint { get; } = afterPaint;

        public int SequenceVersion { get; } = sequenceVersion;

        public IReadOnlyList<SelectionRange> Cells { get; } = cells;
    }

    // The user's own writes raised since the newest paint, under the order in force: only these can
    // be unpainted when an editor opens. One raised before a newer paint, or under another order,
    // goes when the next is noted.
    private readonly List<OwnWrite> _ownWrites = [];

    /// <summary>
    /// Notes a write the grid raises for the user's own gesture (ADR-0142, D1): an Edit Intent, a
    /// paste or fill intent, a Clear Intent, a Fill Intent. Noted as it is raised, before the
    /// Consumer hears it, so an edit the grid opens while the Consumer's handler awaits is opened
    /// after it, as it was typed. An Action is not one: the grid cannot tell what it writes.
    /// </summary>
    private OwnWrite NoteOwnWrite(IReadOnlyList<SelectionRange> cells)
    {
        _ownWrites.RemoveAll(w => w.AfterPaint < _paintId || w.SequenceVersion != _sequenceVersion);
        var write = new OwnWrite(_paintId, _sequenceVersion, cells);
        _ownWrites.Add(write);
        return write;
    }

    /// <summary>Takes back a write the Consumer refused (ADR-0050, items 3 and 5; ADR-0142): it
    /// wrote nothing.</summary>
    private void ForgetOwnWrite(OwnWrite write) => _ownWrites.Remove(write);

    /// <summary>Whether one of the user's own writes raised since the newest paint was named — so
    /// not yet painted — covers <paramref name="cell"/> under the order in force (D1). "Since" is
    /// the order the grid handled them in, never a time.</summary>
    private bool WrittenSinceNewestPaint(CellPosition cell)
    {
        foreach (var write in _ownWrites)
        {
            if (write.AfterPaint < _paintId || write.SequenceVersion != _sequenceVersion)
                continue;
            foreach (var range in write.Cells)
            {
                if (range.Contains(cell))
                    return true;
            }
        }
        return false;
    }

    // ---- An Action press acts on the row it was pressed on ----

    // How many paints keep their row components' serials, for a press told one of them. A paint is a
    // render that changed what a painted cell can show — a scroll step, a new row instance on screen —
    // so a fling makes one a frame; a press is told a few round trips after it is made at most. A
    // press told a paint older than all of them waits for its click.
    private const int PaintRowsKept = 64;

    /// <summary>A paint's row components: the serial of the component each painted position
    /// rendered (0 for a Placeholder), and the columns it painted their actions from.</summary>
    private sealed record PaintRows(int Id, int FirstRow, int[] Serials, IReadOnlyList<GridColumn<TRow>> Columns);

    private readonly List<PaintRows> _paintRows = [];

    // Each painted row component's serial, by its component key — its Row Key, or the object that
    // stands for its instance (ADR-0140) — for the newest paint, and for the one before while the
    // newest is being painted. Rebuilt from the rows each new paint paints, so they hold the keys of
    // rows painted now and nothing older (ADR-0160). Kept only while a column has actions: a serial
    // serves an Action press alone.
    private Dictionary<object, int> _rowSerials = [];
    private Dictionary<object, int> _previousRowSerials = [];
    private int[]? _paintingSerials;
    private int _paintingFirstRow;
    private int _lastRowSerial;

    private void StartRowComponents(int id, int first, int count)
    {
        if (!Columns.Any(static c => c.Actions.Count > 0))
        {
            _paintRows.Clear();
            _rowSerials.Clear();
            return;
        }
        (_previousRowSerials, _rowSerials) = (_rowSerials, _previousRowSerials);
        _rowSerials.Clear();
        _paintingSerials = new int[count];
        _paintingFirstRow = first;
        _paintRows.Add(new PaintRows(id, first, _paintingSerials, Columns));
        if (_paintRows.Count > PaintRowsKept)
            _paintRows.RemoveAt(0);
    }

    /// <summary>A painted row's component key (<see cref="RowComponentKey"/>), noting the
    /// component's serial while a new paint is painted: the same key as in the paint before is the
    /// same component, which Blazor keeps (ADR-0140/0003).</summary>
    private object PaintedRowKey(int position, TRow row)
    {
        var key = RowComponentKey(row);
        if (_paintingSerials is { } serials && position - _paintingFirstRow is var at && at >= 0 && at < serials.Length)
        {
            if (!_previousRowSerials.TryGetValue(key, out var serial))
                serial = ++_lastRowSerial;
            serials[at] = serial;
            _rowSerials[key] = serial;
        }
        return key;
    }

    /// <summary>Ends a new paint's rows: the keys of the paint before go.</summary>
    private void EndRowComponents()
    {
        _paintingSerials = null;
        _previousRowSerials.Clear();
    }

    /// <summary>Whether the newest paint rendered a row component for <paramref name="row"/>: under
    /// its Row Key, or, with none, for the instance itself — by reference, never by value (ADR-0003).</summary>
    private bool RendersRowComponentOf(TRow row) => _rowSerials.ContainsKey(RowComponentKey(row));

    /// <summary>A press on an action of this grid's own rows, told by the grid's listener, until its
    /// click is heard or the core answers it: the paint it was taken against, and — where the
    /// listener read them — its row's position, the serial of the row component that painted its
    /// button, and the column and action it stood for. <see cref="Pressed"/> is the row at that
    /// position when the press was told under the order it was taken under, else null.</summary>
    private sealed record ActionPress(int Paint, int Row, int Serial, TRow? Pressed, string? Column, string? Action);

    // The press being told, until its click is heard or the core answers it. Its row is one of the two
    // holdings that outlive a Window (ADR-0160): bounded to one, and dropped when the press is heard
    // or answered. The other is the Window last measured for Auto widths (_measuredWindow).
    private ActionPress? _actionPress;

    // Whether the core answered a told press whose click had not come. A click such a press's
    // disposed component still delivers is that press's, and acts on nothing more: it fires once
    // (LV-12), whether or not the click comes.
    private bool _actionPressAnswered;

    /// <summary>
    /// What the next press on an action of this grid's own rows was taken against (ADR-0142,
    /// LV-12): the paint the Viewport named at its mousedown, and the row, column and action the
    /// button stood for in it. Told by the grid's listener at the release on the same button, just
    /// before Blazor dispatches the click, so the click the core hears next is the one it
    /// describes. Reading what the render wrote is not a measurement, and nothing per cell crosses
    /// (ADR-0021, notes of 2026-10-05 to 2026-10-07).
    ///
    /// <para>Blazor does not deliver an event whose attribute a component since disposed had
    /// rendered. Without a Row Key a row whose instance a render replaced while the press was on its
    /// way has its component disposed (ADR-0140), and the click on its button would then be lost
    /// without a word. So a press whose row component is no longer rendered is answered here, and
    /// one whose component a later render disposes before its click arrives is answered after that
    /// render: it acts on the row at the told position while the order it was taken under holds,
    /// and is refused otherwise (ADR-0142).</para>
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
        _actionPressAnswered = false;
        var painted = _paintRows.FindLast(p => p.Id == paint);
        var at = painted is null ? -1 : row - painted.FirstRow;
        if (painted is null || at < 0 || at >= painted.Serials.Length || painted.Serials[at] == 0
            || column < 0 || column >= painted.Columns.Count || action < 0 || action >= painted.Columns[column].Actions.Count)
        {
            _actionPress = new ActionPress(paint, -1, 0, null, null, null);
            return;
        }
        _actionPress = new ActionPress(paint, row, painted.Serials[at], AimedUnderAnotherOrder(paint) ? null : RowInHand(row),
            painted.Columns[column].Name, painted.Columns[column].Actions[action].Name);
        await AnswerActionPressWithNoClickAsync();
    }

    /// <summary>
    /// Answers a told press whose click Blazor will not deliver, because the row component that
    /// rendered its button is no longer rendered (ADR-0142, "No press is lost to Blazor"). Raised as
    /// the click would have been, on the row it was pressed on, or refused. A press whose component
    /// is still rendered waits for its click. Decided by the order the core hears things in, never
    /// by time.
    /// </summary>
    private async Task AnswerActionPressWithNoClickAsync()
    {
        if (_actionPress is not { Serial: > 0 } press || _disposed || _rowSerials.ContainsValue(press.Serial))
            return;
        _actionPress = null;
        _actionPressAnswered = true;
        await RaiseActionAsync(new GridActionEventArgs<TRow>(press.Pressed!, press.Column!, press.Action!), press.Paint,
            press.Row, byPosition: false);
    }

    /// <summary>An action pressed by pointer, through its row's button (ADR-0020): the click of the
    /// press the grid's listener told of, if it did (ADR-0142, LV-12).</summary>
    private Task RaiseActionAsync(GridActionEventArgs<TRow> args)
    {
        var press = _actionPress;
        var answered = _actionPressAnswered;
        _actionPress = null;
        _actionPressAnswered = false;
        // The click of a press the core has answered already, which its component, disposed since,
        // still delivered: the press was acted on once.
        if (press is null && answered && !RendersRowComponentOf(args.Row))
            return Task.CompletedTask;
        return RaiseActionAsync(args, press?.Paint ?? PaintNotTold, press is { Row: >= 0 } told ? told.Row : null, byPosition: false);
    }

    /// <summary>
    /// Raises an Action press, or its refusal (ADR-0020, ADR-0142): on the row it was pressed on, as
    /// that row is in the newest version the bound source holds (D5, LV-16) — the button held the row
    /// it was painted with, and a handler that writes the row back through <c>GridSource.From</c>'s
    /// <c>ReplaceRow</c> must not be refused for a stale version.
    /// </summary>
    /// <param name="args">The press: the row the pressed button held, or the one the core resolved
    /// for it, which is <see langword="default"/> when it resolved none.</param>
    /// <param name="told">The paint the press was taken against (ADR-0142).</param>
    /// <param name="at">The position the press named, under the order of <paramref name="told"/>;
    /// null where it named none.</param>
    /// <param name="byPosition">Whether the press names its row by the position alone — Space on the
    /// Focus — rather than by the row its button held.</param>
    private async Task RaiseActionAsync(GridActionEventArgs<TRow> args, int told, int? at, bool byPosition)
    {
        // Pointed at from outside, a press on the rows is handed over instead of acting (ADR-0058):
        // the stylesheet lets it through to the rows, and this is the guard behind it.
        if (_disposed || PointedAtNow)
            return;
        // A press on any action ends Interactive (ADR-0037), by pointer as by Space — and
        // the pointer's press arrives through the row's handler, which renders the row but
        // not this root.
        if (EndInteractive())
        {
            _suppressRender = false;
            StateHasChanged();
        }
        // A position names its row only under the order it was taken against (ADR-0011). Space names
        // its row by nothing else, so under another order it is refused; a press whose button held
        // its row still has the row to go by.
        var orderMoved = at is not null && AimedUnderAnotherOrder(told);
        if (orderMoved && byPosition)
        {
            await RefuseActionAsync(args, ActionRefusalReason.OrderMoved);
            return;
        }
        if (orderMoved)
            at = null;
        at ??= args.Row is { } held ? PositionInHand(held) : null;
        var version = _sequenceVersion;
        if (!await TakeInGatheredAsync())
            return;
        if (version != _sequenceVersion)
        {
            orderMoved = at is not null;
            at = null;
        }
        // And nothing else: no focus is moved (ADR-0037, amended). A press never focused the button,
        // so the keyboard is still where it was — or in whatever the handler opened, which taking it
        // back to the root would rob.
        if (PressedRowNow(args.Row, at, orderMoved, out var reason) is not { } row)
        {
            await RefuseActionAsync(args, reason);
            return;
        }
        if (OnAction.HasDelegate)
            await OnAction.InvokeAsync(args with { Row = row });
    }

    /// <summary>
    /// The row a press acts on now (ADR-0142, LV-12): with a Row Key, the row under the pressed row's
    /// key, wherever the Window holds it (ADR-0140); without one, the pressed instance while the rows
    /// painted now hold it, else the row at <paramref name="at"/>, the position the press named while
    /// it still names that row. Null when there is none, and <paramref name="reason"/> says why.
    /// </summary>
    private TRow? PressedRowNow(TRow? pressed, int? at, bool orderMoved, out ActionRefusalReason reason)
    {
        reason = ActionRefusalReason.RowGone;
        if (_rowKey is { } rowKey)
        {
            if (pressed is null)
                return at is { } told ? RowInHand(told) : null;
            var key = rowKey(pressed);
            if (at is { } p && RowInHand(p) is { } there && Equals(rowKey(there), key))
                return there;
            return PositionInHandByKey(rowKey, key) is { } found ? RowInHand(found) : null;
        }
        if (pressed is not null && PositionInHand(pressed) is not null)
            return pressed;
        if (at is { } position && RowInHand(position) is { } newest)
            return newest;
        if (orderMoved)
            reason = ActionRefusalReason.OrderMoved;
        return null;
    }

    private async Task RefuseActionAsync(GridActionEventArgs<TRow> args, ActionRefusalReason reason)
    {
        if (OnActionRefused.HasDelegate)
            await OnActionRefused.InvokeAsync(new GridActionRefusal<TRow>(args, reason));
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

    // ---- D5: the source puts out what it has gathered before a write is handled ----

    // Set while the grid asks its bound source to put out what it has gathered (ADR-0141/0142,
    // D5). The source raises StateChanged inside the call, on this thread, when it published
    // something; that event is noted instead of answered, and the Window is taken in once, after
    // the call, by the write that asked — never twice, and never half-way through the write.
    private bool _askingGathered;
    private bool _gatheredHeard;

    /// <summary>
    /// Asks the bound source to put out what it has gathered, on the grid's own synchronization
    /// context, and takes its Window in again through the state application, as a change it
    /// announced is taken in (ADR-0141/0142, D5; LV-16). Called just before a write is handled — a
    /// commit, an Action, a paste, a fill, a clear — so the write is made on, and carries, the
    /// newest version. A change gathered while the user typed is brought forward by the gesture, as
    /// in ExPivot (ADR-0067), never waited for. Without a source, or with nothing gathered, nothing
    /// happens. A gathered change that moved the order drops the Selection and discards an open
    /// edit, as any such change does (ADR-0011): the caller reads the state again before it goes
    /// on. Answers false when the source or the new state was refused; the failure is reported as a
    /// source's is, and the write is not made.
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
        // The newest version is painted as well as handled: a press heard through a row's own
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

    // ---- A press into the Formula Bar ----

    /// <summary>
    /// What the next press into this grid's Formula Bar was taken against: the paint the Viewport
    /// named at its mousedown (ADR-0021, note of 2026-10-05). The edit the press opens keeps what
    /// the cell paints when it opens (ADR-0142, rewritten 2026-10-07), so the core no longer reads
    /// it; the listener still tells it.
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="paint">The paint the Viewport named at the press (<c>data-ex-paint</c>).</param>
    [JSInvokable]
    public void BarPressTakenAt(int paint)
    {
    }
}
