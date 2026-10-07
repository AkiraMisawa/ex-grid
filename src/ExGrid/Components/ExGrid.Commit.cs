using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// A Cell Editor commit lands as the user typed it, on the row the editor was opened on (ADR-0142,
// rewritten 2026-10-07; LV-11, LV-17, LV-19, LV-20). The editor keeps what its cell paints when it
// opens, and a commit over a cell that changed under it lands with an Overwrite Notice; the user's own
// writes not yet painted when it opened count as seen (D1, kept for the notice alone).
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Cell Editor commit refused because the grid can no longer find the row the editor was
    /// opened on (ADR-0142, LV-20): with a Row Key, no row in the Window answers its key
    /// (<see cref="CommitRefusalReason.RowGone"/>); without one, the Row Sequence Version moved
    /// since the editor opened (<see cref="CommitRefusalReason.OrderMoved"/>). Without a Row Key, a
    /// row that left the Window under the same order takes the typing with it, raised through
    /// <see cref="OnEditDiscarded"/> as <see cref="EditDiscardReason.RowLeftTheWindow"/> (ADR-0011,
    /// ED-21). The bound source is asked for what it has gathered first (D5, LV-16), and a move it
    /// brings in is answered here, not by a discard. No Edit Intent is raised, and the editor stays
    /// open with what was typed, held from then on; Escape leaves without writing. Every commit
    /// gesture is refused alike, and, as after a Reject, the gesture keeps no meaning of its own. A
    /// commit is never refused because the cell's value changed under the editor: it lands, and
    /// <see cref="OnOverwriteNotice"/> tells it. The grid holds no string for it; Chrome words it
    /// into its refusal live region (A11Y-16).
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

    // Whether one of the user's own writes, not yet painted when the editor opened, covered the
    // edited cell (D1, LV-17): a change the commit finds there is the user's own, and is not told.
    private bool _editOpenedBeforeOwnWrite;

    // The Row Sequence Version the editor was opened under: without a Row Key, the commit lands by
    // position under it (ADR-0142).
    private int _editSequence;

    // Whether a commit gesture of this edit has been refused — a commit as RowGone or OrderMoved
    // (LV-20), or a Ctrl+Enter fill by the paste gate (ADR-0035). The editor is held from then on: an
    // order move does not discard it (ADR-0011), since the typing is never thrown away for what the
    // user did not do (ADR-0142). With a Row Key, a commit refused as RowGone lost the row's key, so
    // every later commit is refused too (_editRowLost); Escape leaves.
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
        var taken = false;
        await WhileCommittingAsync(async () => taken = await TakeInGatheredAsync());
        return taken;
    }

    /// <summary>Runs <paramref name="step"/> of a commit — taking in what the source gathered, or
    /// the Consumer hearing the intent — with an order move it brings in left to the commit.</summary>
    private async Task WhileCommittingAsync(Func<Task> step)
    {
        _committingEdit = true;
        try
        {
            await step();
        }
        finally
        {
            _committingEdit = false;
        }
    }

    /// <summary>Whether an order move just taken in leaves the open edit standing, instead of
    /// discarding it (ADR-0011): one a commit is answering, or one that came after a commit gesture
    /// of this edit was refused.</summary>
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

    /// <summary>What a commit landing on <paramref name="cell"/> replaces: the text the cell paints
    /// now, or empty where it paints none.</summary>
    private string ReplacedTextAt(CellPosition cell) => PaintedTextNow(cell.Row, cell.Column) ?? "";

    /// <summary>The Overwrite Notice a commit landing on <paramref name="cell"/> owes (ADR-0142,
    /// LV-11): the text the editor opened over and <paramref name="replaced"/>, the text the cell
    /// paints now, when they differ and the change is not the user's own unpainted write (D1). Null
    /// when none is owed.</summary>
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
}
