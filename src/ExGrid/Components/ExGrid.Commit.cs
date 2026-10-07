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
// 2026-10-07). It keeps what its cell paints when it opens, and a commit over a cell that changed under
// it lands with an Overwrite Notice; the user's own writes not yet painted when it opened count as
// seen (D1, kept for the notice alone).
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Cell Editor commit refused because the grid can no longer find the row the editor was
    /// opened on (ADR-0142, LV-20): with a Row Key, no row in the Window answers its key
    /// (<see cref="CommitRefusalReason.RowGone"/>); without one, the Row Sequence Version moved
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
    /// the editor opened over and the text the commit replaced; the Edit Intent carries the same two
    /// texts. A commit over an unchanged cell raises none. A cell one of the user's own earlier
    /// writes covered, not yet painted when the editor opened, is not reported (D1, LV-17), so the
    /// same keys give the same outcome on both hosts. The grid holds no string for it; Chrome words
    /// it into the root's live region, as it words a refusal (A11Y-16), and nothing is added to the
    /// row (ADR-0013).
    /// </summary>
    [Parameter] public EventCallback<GridOverwriteNotice> OnOverwriteNotice { get; set; }

    // ---- The Cell Editor: a commit lands, and a change under the editor is told ----

    // What the edited cell painted when the editor opened (ADR-0142, LV-11): the text a commit's
    // Overwrite Notice compares with. Null while no edit is open, or over a cell that paints no text.
    private string? _editSeenText;

    // Whether one of the user's own writes covered the edited cell, not yet painted, when the editor
    // opened (D1, LV-17): a change the commit finds there is the user's own, and is not told.
    private bool _editCellOwnWriteUnpainted;

    // The Row Sequence Version the editor was opened under: without a Row Key, the commit lands by
    // position while it holds, and is refused as OrderMoved once it has moved (ADR-0142).
    private int _editSequence;

    // The Row Key of the row the editor was opened on, while the editor is open: the third holding
    // ADR-0160 names, so that the editor and the Focus follow their row through an order move and a
    // commit can tell that the row is gone (ADR-0011's note of 2026-10-07). Null without a Row Key.
    private object? _editKey;

    /// <summary>Notes what the edit opening over <paramref name="cell"/> starts from (ADR-0142,
    /// LV-11): what the cell paints now, the order in force, the row's Row Key, and whether the
    /// user's own write covers the cell unpainted (D1). A change in the round trip between the
    /// gesture that opens the editor and the open is not seen under the editor, and is the user's to
    /// accept.</summary>
    private void NoteEditOpened(CellPosition cell)
    {
        _editSeenText = PaintedTextNow(cell.Row, cell.Column);
        _editCellOwnWriteUnpainted = OwnWriteUnpaintedAt(cell);
        _editSequence = _sequenceVersion;
        _editKey = _rowKey is { } rowKey && RowInHand(cell.Row) is { } row ? rowKey(row) : null;
    }

    /// <summary>
    /// With a Row Key, the editor and the Focus follow the row they were opened on (ADR-0011's note
    /// of 2026-10-07): after a new Window is taken in, the editor stands where the Window holds its
    /// row now, and the Focus with it, while the Selection the order move dropped stays dropped. The
    /// grid does not scroll to follow it. A row the Window no longer holds leaves the editor where it
    /// is; its commit is refused as <c>RowGone</c>. Without a Row Key the editor stays where it is,
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
    /// was opened under holds. Otherwise the commit is refused, <c>RowGone</c> or <c>OrderMoved</c>,
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
        if (OnCommitRefused.HasDelegate)
            await OnCommitRefused.InvokeAsync(new GridCommitRefusal(cell, Columns[cell.Column].Name, reason));
        return null;
    }

    /// <summary>What a commit landing on <paramref name="cell"/> replaces: the text the cell paints
    /// now, or empty where it paints none.</summary>
    private string ReplacedTextAt(CellPosition cell) => PaintedTextNow(cell.Row, cell.Column) ?? "";

    /// <summary>The Overwrite Notice a commit landing on <paramref name="cell"/> owes (ADR-0142,
    /// LV-11): the text the editor opened over and <paramref name="replaced"/>, the text the cell
    /// paints now, when they differ and the change is not the user's own unpainted write (D1). Null
    /// when none is owed.</summary>
    private GridOverwriteNotice? OverwriteNoticeFor(CellPosition cell, string replaced)
        => _editSeenText is { } seen && !string.Equals(seen, replaced, StringComparison.Ordinal) && !_editCellOwnWriteUnpainted
            ? new GridOverwriteNotice(cell, Columns[cell.Column].Name, seen, replaced)
            : null;

    private async Task RaiseOverwriteNoticeAsync(GridOverwriteNotice? notice)
    {
        if (notice is { } told && OnOverwriteNotice.HasDelegate && !_disposed)
            await OnOverwriteNotice.InvokeAsync(told);
    }

    // ---- D1: the user's own writes count as seen, for the Overwrite Notice ----

    /// <summary>
    /// A write the grid raised for one of the user's own gestures (ADR-0142, D1): the order its
    /// positions are written in, and the cells of it the Window held when it was raised, each with
    /// the text it painted then — until a paint shows the cell otherwise. Positions and strings
    /// only, never a row (ADR-0160). A class, so the one noted is the one taken back.
    /// </summary>
    private sealed class OwnWrite(int sequenceVersion, Dictionary<CellPosition, string> unpainted)
    {
        public int SequenceVersion { get; } = sequenceVersion;

        public Dictionary<CellPosition, string> Unpainted { get; } = unpainted;
    }

    // The user's own writes some cell of which no paint has shown yet, oldest first. What each holds
    // is positions and strings only, never a row or a Row Key (ADR-0160), and both are bounded, for a
    // Consumer that never repaints what it is told to write: at most OwnWriteCellsFollowed cells per
    // write, the painted rows first, and the newest OwnWritesKept writes. A cell beyond the bound is
    // not let off, so an editor opened on it tells a change as any other (ADR-0142).
    private const int OwnWriteCellsFollowed = 1024;
    private const int OwnWritesKept = 64;
    private readonly List<OwnWrite> _ownWrites = [];

    /// <summary>
    /// Notes a write the grid raises for the user's own gesture (ADR-0142, D1): an Edit Intent, a
    /// paste or fill intent, a Clear Intent, a Fill Intent. Noted as it is raised, before the
    /// Consumer hears it, so an edit the grid opens while the Consumer's handler awaits is opened
    /// after it, as it was typed. An Action is not one: the grid cannot tell what it writes.
    /// </summary>
    private OwnWrite NoteOwnWrite(IReadOnlyList<SelectionRange> cells)
    {
        var unpainted = new Dictionary<CellPosition, string>();
        // The painted rows first, then the rest of the Window, as far as the bound.
        if (_visible is { } visible)
            FollowOwnWriteCells(cells, visible.Start, visible.Start + visible.Count - 1, unpainted);
        FollowOwnWriteCells(cells, _windowStart, _windowStart + _window.Count - 1, unpainted);
        var write = new OwnWrite(_sequenceVersion, unpainted);
        if (unpainted.Count > 0)
        {
            _ownWrites.Add(write);
            if (_ownWrites.Count > OwnWritesKept)
                _ownWrites.RemoveAt(0);
        }
        return write;
    }

    private void FollowOwnWriteCells(IReadOnlyList<SelectionRange> cells, int fromRow, int toRow,
        Dictionary<CellPosition, string> unpainted)
    {
        foreach (var range in cells)
        {
            for (var row = Math.Max(range.TopRow, fromRow); row <= Math.Min(range.BottomRow, toRow); row++)
            {
                for (var column = Math.Max(range.LeftColumn, 0); column <= Math.Min(range.RightColumn, Columns.Count - 1); column++)
                {
                    if (unpainted.Count >= OwnWriteCellsFollowed)
                        return;
                    var cell = new CellPosition(row, column);
                    if (!unpainted.ContainsKey(cell) && PaintedTextNow(row, column) is { } text)
                        unpainted[cell] = text;
                }
            }
        }
    }

    /// <summary>Takes back a write the Consumer refused (ADR-0050, items 3 and 5; ADR-0142): it
    /// wrote nothing.</summary>
    private void ForgetOwnWrite(OwnWrite write) => _ownWrites.Remove(write);

    /// <summary>
    /// Notes which cells of the user's own writes the Window just taken in shows (D1): a cell that
    /// paints other text than when the write was raised has been painted with it, and a cell whose
    /// row the Window no longer holds is let go too; a write whose order has moved goes whole. A
    /// scroll, or a change to another cell, even of the same row, shows nothing, and lets nothing go.
    /// </summary>
    private void NoteOwnWritesShown(IReadOnlyList<TRow> previous, int previousStart)
    {
        if (_ownWrites.Count == 0)
            return;
        var replaced = !ReferenceEquals(previous, _window) || previousStart != _windowStart;
        _ownWrites.RemoveAll(write =>
        {
            if (write.SequenceVersion != _sequenceVersion)
                return true;
            if (replaced)
            {
                List<CellPosition>? shown = null;
                foreach (var (cell, text) in write.Unpainted)
                {
                    if (!string.Equals(PaintedTextNow(cell.Row, cell.Column), text, StringComparison.Ordinal))
                        (shown ??= []).Add(cell);
                }
                if (shown is not null)
                {
                    foreach (var cell in shown)
                        write.Unpainted.Remove(cell);
                }
            }
            return write.Unpainted.Count == 0;
        });
    }

    /// <summary>Whether one of the user's own writes covers <paramref name="cell"/> under the order
    /// in force, and no paint has shown it there yet (D1). "Yet" is the order the grid handled them
    /// in, never a time.</summary>
    private bool OwnWriteUnpaintedAt(CellPosition cell)
    {
        foreach (var write in _ownWrites)
        {
            if (write.SequenceVersion == _sequenceVersion && write.Unpainted.ContainsKey(cell))
                return true;
        }
        return false;
    }
}
