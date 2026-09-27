namespace ExSheet.Engine;

/// <summary>
/// One operation a Sheet has done, as one step of ExSheet's one undo stack (ADR-0048): undoing it
/// restores every Entry, format and Reference it changed exactly, with one recalculation; redoing
/// it does it again. The stack itself is the component's. Steps are undone in the reverse of the
/// order they were done, and redone in the order they were undone, as a stack does: a step
/// undone out of that order would put back cells that no longer mean what they did.
/// </summary>
public sealed class SheetStep
{
    private readonly Func<Sheet, SheetChange> _undo;
    private readonly Func<Sheet, SheetChange> _redo;

    internal SheetStep(Sheet sheet, SheetChange change, Func<Sheet, SheetChange> undo, Func<Sheet, SheetChange> redo)
    {
        Sheet = sheet;
        Change = change;
        _undo = undo;
        _redo = redo;
    }

    /// <summary>The Sheet the step belongs to.</summary>
    public Sheet Sheet { get; }

    /// <summary>What doing the operation changed.</summary>
    public SheetChange Change { get; }

    /// <summary>Whether the step is currently undone.</summary>
    public bool IsUndone { get; private set; }

    /// <summary>Undoes the operation, and says what that changed.</summary>
    /// <exception cref="InvalidOperationException">The step is already undone.</exception>
    public SheetChange Undo()
    {
        if (IsUndone) throw new InvalidOperationException("The step is already undone.");
        var change = _undo(Sheet);
        IsUndone = true;
        return change;
    }

    /// <summary>Does the operation again after <see cref="Undo"/>, and says what that changed.</summary>
    /// <exception cref="InvalidOperationException">The step is not undone.</exception>
    public SheetChange Redo()
    {
        if (!IsUndone) throw new InvalidOperationException("Only an undone step can be redone.");
        var change = _redo(Sheet);
        IsUndone = false;
        return change;
    }
}
