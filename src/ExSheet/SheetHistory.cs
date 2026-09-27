using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// ExSheet's one undo stack (ADR-0048): every operation, the user's and the Consumer's alike, is
/// one step on it, in the order it was done. Undoing walks back through that order and redoing
/// walks forward again; doing anything new forgets what was undone, as every undo stack does. One
/// per ExSheet instance (ADR-0018).
/// </summary>
/// <remarks>
/// An operation is usually one engine step. One the engine does as several — a copied block
/// pasted over a Selection of several ranges — is still one operation, and is undone and redone
/// whole: its engine steps undone in the reverse of their order, redone in their order.
/// </remarks>
internal sealed class SheetHistory
{
    private readonly List<SheetStep[]> _done = [];
    private readonly List<SheetStep[]> _undone = [];

    internal bool CanUndo => _done.Count > 0;

    internal bool CanRedo => _undone.Count > 0;

    /// <summary>A step just done: it goes on top, and nothing undone can be redone past it.</summary>
    internal void Record(SheetStep step) => Record([step]);

    /// <summary>One operation done as several engine steps, in the order they were done.</summary>
    internal void Record(IReadOnlyList<SheetStep> steps)
    {
        if (steps.Count == 0) throw new ArgumentException("An operation is at least one step.", nameof(steps));
        _done.Add([.. steps]);
        _undone.Clear();
    }

    /// <summary>Undoes the latest operation and says what that changed, or answers null when there is none.</summary>
    internal IReadOnlyList<SheetChange>? Undo()
    {
        if (_done.Count == 0) return null;
        var steps = _done[^1];
        var changes = new List<SheetChange>(steps.Length);
        for (var i = steps.Length - 1; i >= 0; i--) changes.Add(steps[i].Undo());
        _done.RemoveAt(_done.Count - 1);
        _undone.Add(steps);
        return changes;
    }

    /// <summary>Redoes the operation undone last and says what that changed, or answers null when there is none.</summary>
    internal IReadOnlyList<SheetChange>? Redo()
    {
        if (_undone.Count == 0) return null;
        var steps = _undone[^1];
        var changes = new List<SheetChange>(steps.Length);
        foreach (var step in steps) changes.Add(step.Redo());
        _undone.RemoveAt(_undone.Count - 1);
        _done.Add(steps);
        return changes;
    }

    /// <summary>Forgets every step: the document they belonged to has been replaced (ADR-0048).</summary>
    internal void Clear()
    {
        _done.Clear();
        _undone.Clear();
    }
}
