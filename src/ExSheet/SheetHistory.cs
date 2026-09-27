using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// ExSheet's one undo stack (ADR-0048): every operation, the user's and the Consumer's alike, is
/// one engine step on it, in the order it was done. Undoing walks back through that order and
/// redoing walks forward again; doing anything new forgets what was undone, as every undo stack
/// does. One per ExSheet instance (ADR-0018).
/// </summary>
internal sealed class SheetHistory
{
    private readonly List<SheetStep> _done = [];
    private readonly List<SheetStep> _undone = [];

    internal bool CanUndo => _done.Count > 0;

    internal bool CanRedo => _undone.Count > 0;

    /// <summary>A step just done: it goes on top, and nothing undone can be redone past it.</summary>
    internal void Record(SheetStep step)
    {
        _done.Add(step);
        _undone.Clear();
    }

    /// <summary>Undoes the latest step, or answers null when there is none.</summary>
    internal SheetChange? Undo()
    {
        if (_done.Count == 0) return null;
        var step = _done[^1];
        var change = step.Undo();
        _done.RemoveAt(_done.Count - 1);
        _undone.Add(step);
        return change;
    }

    /// <summary>Redoes the step undone last, or answers null when there is none.</summary>
    internal SheetChange? Redo()
    {
        if (_undone.Count == 0) return null;
        var step = _undone[^1];
        var change = step.Redo();
        _undone.RemoveAt(_undone.Count - 1);
        _done.Add(step);
        return change;
    }

    /// <summary>Forgets every step: the document they belonged to has been replaced (ADR-0048).</summary>
    internal void Clear()
    {
        _done.Clear();
        _undone.Clear();
    }
}
