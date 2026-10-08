using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// A Cell Editor commit lands as the user typed it, on the row the editor was opened on (ADR-0142,
// rewritten 2026-10-07; LV-11, LV-17, LV-19, LV-20). The editor outlives an order move: with a Row Key
// it and the Focus follow their row, and without one it stays where it is (ADR-0011's note of
// 2026-10-07). It keeps a baseline — what its cell paints when it opens — and a commit over a cell that
// paints otherwise lands with an Overwrite Notice. The user's own writes count as seen (D1, kept for the
// notice alone): a write of theirs to the cell that had not settled when the editor opened moves the
// baseline to what the cell paints when it settles, and it settles at a point in the grid's order of
// events, never at a time and never by comparing text (ExGrid.Commit.cs, "D1").
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Cell Editor commit refused because the grid cannot find, in the rows it holds now, the row
    /// the editor was opened on (ADR-0142, LV-20): with a Row Key, no row in the Window answers its key
    /// (<see cref="CommitRefusalReason.RowLeftTheWindow"/>); without one, the Row Sequence Version moved
    /// since the editor opened (<see cref="CommitRefusalReason.OrderMoved"/>). Without a Row Key, a
    /// row that left the Window under the same order takes the typing with it, raised through
    /// <see cref="OnEditDiscarded"/> as <see cref="EditDiscardReason.RowLeftTheWindow"/> (ADR-0011,
    /// ED-21). The bound source is asked for what it has gathered first (D5, LV-16). No Edit Intent
    /// is raised, and the editor stays open with what was typed; Escape leaves without writing, and a
    /// later commit is asked again — with a Row Key, it lands once the key is back in the Window.
    /// Every commit gesture is refused alike, and, as after a Reject, the gesture keeps no meaning
    /// of its own. A commit is never refused because the cell's value changed under the editor: it
    /// lands, and <see cref="OnOverwriteNotice"/> tells it. The grid holds no string for it; Chrome
    /// words it into its refusal live region (A11Y-16).
    /// </summary>
    [Parameter] public EventCallback<GridCommitRefusal> OnCommitRefused { get; set; }

    /// <summary>
    /// The Overwrite Notice (ADR-0142, LV-11, LV-21): a Cell Editor commit — Enter, Tab, an arrow in
    /// Overwrite, a press elsewhere, Ctrl+Enter — landed over a cell whose painted text changed while
    /// the editor covered it. Raised once the commit's intent was accepted, with the cell, the text
    /// the user saw and the text the commit replaced — the Edit Intent's
    /// <see cref="GridEditIntent{TRow}.SeenText"/> and <see cref="GridEditIntent{TRow}.ReplacedText"/>,
    /// and exactly when the two differ. What the user saw is what the cell painted when the editor
    /// opened; where one of the user's own earlier writes to the cell had not settled then, it is what
    /// the cell paints once that write settles, so the user's own value is never reported as a change
    /// (D1, LV-17), and the same keys give the same outcome on both hosts. A write settles when the
    /// first new Window is taken in after its intent's handler has completed — for a bound
    /// <see cref="Source"/>, the first it publishes, which the grid asks for as the handler completes
    /// (D5) — never at a time and never by comparing text. The grid holds no string for it; Chrome
    /// words it into the root's live region, as it words a refusal (A11Y-16), and nothing is added to
    /// the row (ADR-0013).
    /// </summary>
    [Parameter] public EventCallback<GridOverwriteNotice> OnOverwriteNotice { get; set; }

    // ---- The Cell Editor: a commit lands, and a change under the editor is told ----

    // The editor's baseline (ADR-0142, LV-11, D1): what the edited cell painted when the editor opened,
    // or, once a write of the user's own to the cell that had not settled then has settled, what the
    // cell painted at that moment. A commit's Edit Intent carries it as SeenText, and the Overwrite
    // Notice is raised when the cell paints otherwise. Null while no edit is open, or over a cell that
    // paints no text.
    private string? _editSeenText;

    // The Row Sequence Version the editor was opened under: without a Row Key, the commit lands by
    // position while it holds, and is refused as OrderMoved once it has moved (ADR-0142).
    private int _editSequence;

    // The Row Key of the row the editor was opened on, while the editor is open: the third holding
    // ADR-0160 names, so that the editor and the Focus follow their row through an order move and a
    // commit can tell that the row is gone (ADR-0011's note of 2026-10-07). Null without a Row Key.
    private object? _editKey;

    /// <summary>Notes what the edit opening over <paramref name="cell"/> starts from (ADR-0142,
    /// LV-11): the baseline — what the cell paints now — the order in force, the row's Row Key, and
    /// the user's own writes to the cell that have not settled (D1), whose settling moves the
    /// baseline. A change in the round trip between the gesture that opens the editor and the open is
    /// not seen under the editor, and is the user's to accept.</summary>
    private void NoteEditOpened(CellPosition cell)
    {
        _editSeenText = PaintedTextNow(cell.Row, cell.Column);
        _editSequence = _sequenceVersion;
        _editKey = _rowKey is { } rowKey && RowInHand(cell.Row) is { } row ? rowKey(row) : null;
        _editAwaits.Clear();
        _editRebaseOwed = false;
        foreach (var write in _ownWrites)
        {
            if (write.Binding == _binding && write.SequenceVersion == _sequenceVersion && write.Cells.Contains(cell))
                _editAwaits.Add(write);
        }
    }

    /// <summary>Forgets what the edit that is ending started from: its baseline, the writes it awaited,
    /// and its row's Row Key, which ADR-0160 lets the grid hold only while the editor is open.</summary>
    private void ForgetEditBaseline()
    {
        _editSeenText = null;
        _editKey = null;
        _editAwaits.Clear();
        _editRebaseOwed = false;
    }

    /// <summary>The baseline moves to what the edited cell paints now: a write of the user's own that
    /// it awaited has settled, or was dropped by the Window that brought it (D1). Called once the
    /// editor has followed its row, so a keyed row that moved is read where it stands now. A row the
    /// Window does not hold at that moment paints nothing: the baseline is then the text the commit
    /// finds, and no change is told for what the grid could not see.</summary>
    private void RebaseEdit()
    {
        _editRebaseOwed = false;
        if (_editMode != EditMode.None)
            _editSeenText = PaintedTextNow(_editingCell.Row, _editingCell.Column);
    }

    /// <summary>
    /// With a Row Key, the editor and the Focus follow the row they were opened on (ADR-0011's note
    /// of 2026-10-07): after a new Window is taken in, the editor stands where the Window holds its
    /// row now, and the Focus with it, while the Selection the order move dropped stays dropped. The
    /// grid does not scroll to follow it. A row the Window no longer holds leaves the editor where it
    /// is; its commit is refused as <c>RowLeftTheWindow</c>. Without a Row Key the editor stays where it is,
    /// and its commit is refused as <c>OrderMoved</c> once the order has moved.
    /// </summary>
    private void FollowEditedRow()
    {
        if (_editMode == EditMode.None || _rowKey is not { } rowKey || _editKey is not { } key)
            return;
        if (PositionOfKey(rowKey, key, _editingCell.Row) is not { } row)
            return;
        _editingCell = new CellPosition(row, _editingCell.Column);
        if (_selection.Selection.IsEmpty || _selection.Selection.Focus != _editingCell)
        {
            _selection = _selection.With(GridSelection.Empty.Click(_editingCell, Extent));
            _selectionChanged = true;
        }
    }

    /// <summary>Whether the row the editor stands at is out of the painted rows, as a row it followed
    /// can be: the grid does not scroll to follow it (ADR-0011's note of 2026-10-07).</summary>
    private bool EditorAway(RowRange visible)
        => _editingCell.Row < visible.Start - 1 || _editingCell.Row > visible.Start + visible.Count;

    /// <summary>The cell whose box the editor takes: its own, or, while it is away, its column's on
    /// the first painted row, where it stays open and holds the keyboard — so the keys typed still
    /// reach it, and Enter still commits it — but is neither seen nor pressed (<c>ex-editor-away</c>).
    /// Placed among the painted rows, it adds nothing to what the scroller can scroll.</summary>
    private CellPosition EditorCellIn(RowRange visible, bool away)
        => away ? new CellPosition(visible.Start, _editingCell.Column) : _editingCell;

    /// <summary>
    /// Where a commit lands (ADR-0142, LV-20): on the row the editor was opened on — by its Row Key
    /// wherever the Window holds it now, or, without one, at the editor's position while the order it
    /// was opened under holds. Otherwise the commit is refused, <c>RowLeftTheWindow</c> or <c>OrderMoved</c>,
    /// the editor stays, and null is answered. Without a Row Key, the caller has already taken a row
    /// that left the Window under the same order (ED-21).
    /// </summary>
    private async Task<CellPosition?> CommitLandingAsync()
    {
        var cell = _editingCell;
        CommitRefusalReason reason;
        if (_rowKey is { } rowKey)
        {
            if (_editKey is { } key && PositionOfKey(rowKey, key, cell.Row) is { } at)
                return new CellPosition(at, cell.Column);
            reason = CommitRefusalReason.RowLeftTheWindow;
        }
        else if (_sequenceVersion != _editSequence)
        {
            reason = CommitRefusalReason.OrderMoved;
        }
        else
        {
            return cell;
        }
        if (OnCommitRefused.HasDelegate)
            await OnCommitRefused.InvokeAsync(new GridCommitRefusal(cell, Columns[cell.Column].Name, reason));
        return null;
    }

    /// <summary>What a commit landing on <paramref name="cell"/> replaces: the text the cell paints
    /// now, or empty where it paints none.</summary>
    private string ReplacedTextAt(CellPosition cell) => PaintedTextNow(cell.Row, cell.Column) ?? "";

    /// <summary>The text the user saw of the edited cell, for a commit landing over
    /// <paramref name="replaced"/> (ADR-0142, D1): the editor's baseline, or the replaced text itself
    /// where the cell painted none when the editor opened. The Edit Intent's SeenText.</summary>
    private string SeenTextFor(string replaced) => _editSeenText ?? replaced;

    /// <summary>The Overwrite Notice a commit landing on <paramref name="cell"/> owes (ADR-0142,
    /// LV-11): the text the user saw (<see cref="SeenTextFor"/>) and <paramref name="replaced"/>, the
    /// text the cell paints now — the Edit Intent's two texts — exactly when they differ. Null when
    /// none is owed.</summary>
    private GridOverwriteNotice? OverwriteNoticeFor(CellPosition cell, string replaced)
        => SeenTextFor(replaced) is var seen && !string.Equals(seen, replaced, StringComparison.Ordinal)
            ? new GridOverwriteNotice(cell, Columns[cell.Column].Name, seen, replaced)
            : null;

    private async Task RaiseOverwriteNoticeAsync(GridOverwriteNotice? notice)
    {
        if (notice is { } told && OnOverwriteNotice.HasDelegate && !_disposed)
            await OnOverwriteNotice.InvokeAsync(told);
    }

    // ---- D1: the user's own writes count as seen, for the Overwrite Notice ----
    //
    // A write the grid raises for the user's own gesture — an Edit Intent, a paste or fill intent, a
    // Clear Intent, a Fill Intent — is followed from the moment it is raised until it settles, by the
    // positions it covers. An editor opened over one of its cells while it has not settled awaits it,
    // and when it settles the editor's baseline becomes what the cell paints at that moment: the change
    // it brings is the user's own. After that, any change under the editor is told.
    //
    // A write settles at a point in the grid's order of events, never at a time and never by comparing
    // painted text (a write that leaves the text as it was — the same value retyped, F2 and Enter, a
    // value the format paints alike, a write the Consumer let pass without writing — settles all the
    // same):
    //   - its intent's handler has completed, unrefused, and a new Window — another list or another
    //     start — has been taken in since the write was raised: during the handler (a Consumer that
    //     applies it there, a source that publishes the Consumer's write at once), or after;
    //   - for a bound source, the grid asks it to put out what it gathered as the handler completes
    //     (D5, OwnWriteAnsweredAsync), so a source that gathers the Consumer's write and publishes it
    //     synchronously settles it at once, and one whose answer is a question to a server settles it
    //     with the first Window it publishes after;
    //   - for a pushed Window, the first new Window taken in after the handler completed.
    // An order move or a replaced Source drops it (its positions name other rows), and so does a new
    // Window that no longer holds any of its rows; an editor that awaited it takes the cell's text then,
    // as at a settle. A write the Consumer refused is taken back: it wrote nothing, and moves nothing.

    /// <summary>
    /// A write the grid raised for one of the user's own gestures (ADR-0142, D1), until it settles:
    /// the binding and the order its positions are written in, and the cells of it the Window held
    /// when it was raised. Positions only, never a row or a string (ADR-0160). A class, so the one
    /// noted is the one taken back.
    /// </summary>
    private sealed class OwnWrite(int binding, int sequenceVersion, HashSet<CellPosition> cells)
    {
        public int Binding { get; } = binding;

        public int SequenceVersion { get; } = sequenceVersion;

        public HashSet<CellPosition> Cells { get; } = cells;

        /// <summary>Whether its intent's handler has completed, and did not refuse it.</summary>
        public bool Answered { get; set; }

        /// <summary>Whether a new Window has been taken in since it was raised.</summary>
        public bool WindowTakenIn { get; set; }
    }

    // The user's own writes that have not settled, oldest first. What each holds is positions only,
    // never a row, a Row Key or a string (ADR-0160), and both are bounded, for a Consumer that never
    // hands over a new Window: at most OwnWriteCellsFollowed cells per write, the painted rows first,
    // and the newest OwnWritesKept writes. Beyond the bound a cell is not followed, and an editor opened
    // on it tells a change as over any other cell: its baseline is what it painted at the open, so the
    // user's own write arriving under it there is told (ADR-0142, as before D1).
    private const int OwnWriteCellsFollowed = 1024;
    private const int OwnWritesKept = 64;
    private readonly List<OwnWrite> _ownWrites = [];

    // The user's own writes that covered the edited cell, unsettled, when the editor opened (D1).
    private readonly List<OwnWrite> _editAwaits = [];

    // An awaited write settled, or was dropped, while a new Window was taken in: the baseline is read
    // again once the editor has followed its row (ApplyState, RebaseEdit).
    private bool _editRebaseOwed;

    /// <summary>
    /// Notes a write the grid raises for the user's own gesture (ADR-0142, D1): an Edit Intent, a
    /// paste or fill intent, a Clear Intent, a Fill Intent. Noted as it is raised, before the
    /// Consumer hears it, so an edit the grid opens while the Consumer's handler awaits is opened
    /// after it, as it was typed. An Action is not one: the grid cannot tell what it writes. Its
    /// handler's completion is told with <see cref="OwnWriteAnsweredAsync"/>.
    /// </summary>
    private OwnWrite NoteOwnWrite(IReadOnlyList<SelectionRange> cells)
    {
        var followed = new HashSet<CellPosition>();
        // The painted rows first, then the rest of the Window, as far as the bound.
        if (_visible is { } visible)
            FollowOwnWriteCells(cells, visible.Start, visible.Start + visible.Count - 1, followed);
        FollowOwnWriteCells(cells, _windowStart, _windowStart + _window.Count - 1, followed);
        var write = new OwnWrite(_binding, _sequenceVersion, followed);
        if (followed.Count > 0)
        {
            _ownWrites.Add(write);
            if (_ownWrites.Count > OwnWritesKept)
            {
                // Past the bound: no longer followed, and an editor that awaited it keeps its baseline.
                _editAwaits.Remove(_ownWrites[0]);
                _ownWrites.RemoveAt(0);
            }
        }
        return write;
    }

    private void FollowOwnWriteCells(IReadOnlyList<SelectionRange> cells, int fromRow, int toRow,
        HashSet<CellPosition> followed)
    {
        foreach (var range in cells)
        {
            for (var row = Math.Max(range.TopRow, fromRow); row <= Math.Min(range.BottomRow, toRow); row++)
            {
                for (var column = Math.Max(range.LeftColumn, 0); column <= Math.Min(range.RightColumn, Columns.Count - 1); column++)
                {
                    if (followed.Count >= OwnWriteCellsFollowed)
                        return;
                    // Only a cell an editor can open over: one that paints a value, of a row in hand.
                    if (Columns[column].PaintsValue && RowInHand(row) is not null)
                        followed.Add(new CellPosition(row, column));
                }
            }
        }
    }

    /// <summary>
    /// The handler of a write intent the grid raised has completed (ADR-0142, D1, D5). The bound
    /// source is asked to put out what it has gathered, so a gathering source's own write is in the
    /// Window before the next gesture is handled — whether the Consumer refused or not. A refused
    /// write is taken back (ADR-0050, items 3 and 5): it wrote nothing. An accepted one settles now if
    /// a new Window has been taken in since it was raised, during the handler or by that ask, and
    /// otherwise at the first one taken in after this (<see cref="NoteOwnWritesTakenIn"/>).
    /// </summary>
    /// <param name="write">The write noted as the intent was raised.</param>
    /// <param name="refused">Whether the Consumer refused the intent.</param>
    private async Task OwnWriteAnsweredAsync(OwnWrite write, bool refused)
    {
        if (refused)
            ForgetOwnWrite(write);
        else
            write.Answered = true;
        await TakeInGatheredAsync();
        if (!refused && write.WindowTakenIn && _ownWrites.Remove(write) && _editAwaits.Remove(write))
            RebaseEdit();
    }

    /// <summary>Takes back a write the Consumer refused (ADR-0050, items 3 and 5; ADR-0142): it
    /// wrote nothing, so an editor that awaited it keeps its baseline.</summary>
    private void ForgetOwnWrite(OwnWrite write)
    {
        _ownWrites.Remove(write);
        _editAwaits.Remove(write);
    }

    /// <summary>
    /// The user's own writes against the Window just taken in (D1). Under another binding or another
    /// order, a write's positions name other rows: it goes. A new Window — another list or another
    /// start — lets go of the cells whose rows it no longer holds, and is the one a write waits for:
    /// one whose handler has completed settles; one still being handled settles as its handler
    /// completes (<see cref="OwnWriteAnsweredAsync"/>). A render of the same Window, a scroll over it
    /// or a change of the Selection settles nothing. An editor that awaited a write that went has its
    /// baseline read again once it has followed its row.
    /// </summary>
    private void NoteOwnWritesTakenIn(IReadOnlyList<TRow> previous, int previousStart)
    {
        if (_ownWrites.Count == 0)
            return;
        var newWindow = !ReferenceEquals(previous, _window) || previousStart != _windowStart;
        for (var i = _ownWrites.Count - 1; i >= 0; i--)
        {
            var write = _ownWrites[i];
            bool goes;
            if (write.Binding != _binding || write.SequenceVersion != _sequenceVersion)
            {
                goes = true;
            }
            else if (!newWindow)
            {
                goes = false;
            }
            else
            {
                write.Cells.RemoveWhere(cell => RowInHand(cell.Row) is null);
                write.WindowTakenIn = true;
                goes = write.Answered || write.Cells.Count == 0;
            }
            if (!goes)
                continue;
            _ownWrites.RemoveAt(i);
            if (_editAwaits.Remove(write))
                _editRebaseOwed = true;
        }
    }
}
