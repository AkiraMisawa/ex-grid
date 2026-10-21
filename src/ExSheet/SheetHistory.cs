using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// ExSheet's one undo stack (ADR-0048): every operation, the user's and the Consumer's alike, is
/// one step on it, in the order it was done, whichever Sheet of the Workbook it was done on
/// (ADR-0331). Undoing walks back through that order and redoing walks forward again; doing
/// anything new forgets what was undone, as every undo stack does. One per ExSheet instance
/// (ADR-0018). Each operation keeps the Sheet it was done on and the ranges it changed there, which
/// its undo and its redo show and select (ADR-0331 §3).
/// </summary>
/// <remarks>
/// An operation is usually one engine step. One done as several — an edit with the widths it
/// widens its columns to, a resize of several whole columns the grid reports one column at a
/// time — is still one operation, and is undone and redone whole: its engine steps undone in the
/// reverse of their order, redone in their order. Each step's undo and redo is a change of its own
/// when it changes an Entry (ADR-0180), so at most one step of an operation may: an operation that
/// writes Entries in several parts, such as a paste over several ranges, is one engine edit
/// (<see cref="SheetEdit.Paste(SheetBlock, IEnumerable{CellRange})"/>), and draws once.
/// </remarks>
internal sealed class SheetHistory
{
    private readonly List<SheetOperation> _done = [];
    private readonly List<SheetOperation> _undone = [];

    internal bool CanUndo => _done.Count > 0;

    internal bool CanRedo => _undone.Count > 0;

    /// <summary>A step just done on <paramref name="from"/>: it goes on top, and nothing undone can be redone past it.</summary>
    internal void Record(WorkbookStep step, Sheet from) => Record([step], from);

    /// <summary>
    /// One operation done on <paramref name="from"/> as several engine steps, in the order they were done.
    /// The first step is the operation's own; the ones after it, such as the widths an entry widens its
    /// columns to, are part of it, and add no range its undo selects. <paramref name="ranges"/>, when given,
    /// are the ranges it acted on in place of those its first step names: Delete's Selection, of which it
    /// names only the cells that held an Entry.
    /// </summary>
    internal void Record(IReadOnlyList<WorkbookStep> steps, Sheet from, IReadOnlyList<CellRange>? ranges = null)
    {
        if (steps.Count == 0) throw new ArgumentException("An operation is at least one step.", nameof(steps));
        _done.Add(new SheetOperation([.. steps], from, ranges ?? steps[0].Ranges));
        _undone.Clear();
    }

    /// <summary>
    /// Adds <paramref name="step"/> to the latest operation when that operation began with
    /// <paramref name="first"/> and nothing was done or undone since — one operation the grid
    /// reports in parts, such as a resize of several whole columns, one column at a time. Its ranges
    /// join the operation's. Answers false, recording nothing, otherwise.
    /// </summary>
    internal bool TryExtend(WorkbookStep first, WorkbookStep step)
    {
        if (_done.Count == 0 || _undone.Count > 0 || !ReferenceEquals(_done[^1].Steps[0], first)) return false;
        var latest = _done[^1];
        _done[^1] = latest with { Steps = [.. latest.Steps, step], Ranges = [.. latest.Ranges, .. step.Ranges] };
        return true;
    }

    /// <summary>Undoes the latest operation and says what that changed, or answers null when there is none.</summary>
    internal SheetOperationStepped? Undo()
    {
        if (_done.Count == 0) return null;
        var operation = _done[^1];
        var steps = operation.Steps;
        var changes = new List<WorkbookChange>(steps.Length);
        for (var i = steps.Length - 1; i >= 0; i--) changes.Add(steps[i].Undo());
        _done.RemoveAt(_done.Count - 1);
        _undone.Add(operation);
        return new SheetOperationStepped(operation, changes);
    }

    /// <summary>Redoes the operation undone last and says what that changed, or answers null when there is none.</summary>
    internal SheetOperationStepped? Redo()
    {
        if (_undone.Count == 0) return null;
        var operation = _undone[^1];
        var changes = new List<WorkbookChange>(operation.Steps.Length);
        foreach (var step in operation.Steps) changes.Add(step.Redo());
        _undone.RemoveAt(_undone.Count - 1);
        _done.Add(operation);
        return new SheetOperationStepped(operation, changes);
    }

    /// <summary>Forgets every step: the document they belonged to has been replaced (ADR-0048).</summary>
    internal void Clear()
    {
        _done.Clear();
        _undone.Clear();
    }
}

/// <summary>
/// One operation on the undo stack: its engine steps, the Sheet it was done on, and the ranges it
/// changed, which its undo and its redo select (ADR-0331 §3).
/// </summary>
/// <param name="Steps">The engine steps, in the order they were done.</param>
/// <param name="From">The Sheet it was done on: for a Sheet added or copied, the one it was added beside or copied from.</param>
/// <param name="Ranges">The ranges it changed on its first step's Sheet; none for an operation on the Sheets themselves.</param>
internal sealed record SheetOperation(WorkbookStep[] Steps, Sheet From, IReadOnlyList<CellRange> Ranges)
{
    /// <summary>
    /// The Sheet its undo or redo shows (ADR-0331 §3): the Sheet its step was taken on while the
    /// Workbook holds it — a cell's edit, a rename, a move, the Sheet a deletion's undo brings back, the
    /// Sheet an insertion's or a copy's redo brings back — else the Sheet it was done from, which an
    /// insertion's or a copy's undo leaves, else the Sheet the Workbook shows in the place of the one a
    /// deletion's redo takes away.
    /// </summary>
    internal Sheet ShownAfter(Engine.Workbook workbook)
    {
        var sheet = Steps[0].Sheet;
        if (workbook.Sheets.Contains(sheet)) return sheet;
        return workbook.Sheets.Contains(From) ? From : workbook.Shown;
    }

    /// <summary>
    /// The one range its undo or redo selects, since the grid places one (ADR-0050, item 4): its range,
    /// or the range its ranges make together when they tile one, as the whole columns of one resize do;
    /// otherwise the first, the one it acted from (for Ctrl+Enter, the range it was typed in). Null for
    /// an operation that changed no range.
    /// </summary>
    internal CellRange? Selects()
    {
        if (Ranges.Count == 0) return null;
        var distinct = Ranges.Distinct().ToList();
        if (distinct.Count == 1) return distinct[0];
        var bounding = new CellRange(
            new CellAddress(distinct.Min(r => r.First.Row), distinct.Min(r => r.First.Column)),
            new CellAddress(distinct.Max(r => r.Last.Row), distinct.Max(r => r.Last.Column)));
        for (var i = 0; i < distinct.Count; i++)
        {
            for (var j = i + 1; j < distinct.Count; j++)
            {
                if (Overlap(distinct[i], distinct[j])) return Ranges[0];
            }
        }
        return distinct.Sum(r => r.CellCount) == bounding.CellCount ? bounding : Ranges[0];
    }

    private static bool Overlap(CellRange a, CellRange b) =>
        a.First.Row <= b.Last.Row && b.First.Row <= a.Last.Row && a.First.Column <= b.Last.Column && b.First.Column <= a.Last.Column;
}

/// <summary>An operation undone or redone, and what that changed, in the order its steps changed it.</summary>
internal sealed record SheetOperationStepped(SheetOperation Operation, IReadOnlyList<WorkbookChange> Changes);
