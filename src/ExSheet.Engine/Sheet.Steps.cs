namespace ExSheet.Engine;

public sealed partial class Sheet
{
    /// <summary>
    /// Does <paramref name="edit"/> as one operation and returns the step that undoes it
    /// (ADR-0048). A refused or unreadable operation changes nothing and returns no step.
    /// </summary>
    /// <exception cref="SheetRefusedException">The Sheet refuses the operation (<see cref="Check"/> says why).</exception>
    /// <exception cref="FormulaSyntaxException">A typed Formula cannot be read.</exception>
    public SheetStep Do(SheetEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (edit.Check(this) is { } refusal) throw new SheetRefusedException(refusal);
        return edit.Apply(this);
    }

    /// <summary>Whether <see cref="Do"/> would refuse <paramref name="edit"/> as the Sheet stands, and why; <see langword="null"/> when it would not.</summary>
    public SheetRefusal? Check(SheetEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        return edit.Check(this);
    }

    /// <summary>What each of <paramref name="addresses"/> records: Entry and formatting, never a Value.</summary>
    internal List<(CellAddress Address, CellState State)> Record(IEnumerable<CellAddress> addresses) =>
        [.. addresses.Select(a => (a, StateOf(a).Recorded))];

    /// <summary>
    /// Puts cells back to what they recorded — Entry, number format and alignment — as one change
    /// with one recalculation.
    /// </summary>
    internal SheetChange Restore(IEnumerable<(CellAddress Address, CellState State)> states)
    {
        var list = states.ToList();
        var rows = new SortedSet<int>();
        foreach (var (address, state) in list)
        {
            var existing = _cells.TryGetValue(address, out var cell);
            cell ??= new Cell(address);
            if (Equals(cell.Format, state.Format) && cell.Alignment == state.Alignment) continue;
            cell.Format = state.Format;
            cell.Alignment = state.Alignment;
            rows.Add(address.Row);
            if (!existing) _cells[address] = cell;
            else if (cell.IsEmpty && cell.Value is null) _cells.Remove(address);
        }
        var change = SetEntries(list.Select(p => new KeyValuePair<CellAddress, Entry?>(p.Address, p.State.Entry)));
        // A cell given formatting only for the moment of the swap, and left with nothing, goes.
        foreach (var (address, _) in list)
        {
            if (_cells.TryGetValue(address, out var cell) && cell.IsEmpty && cell.Value is null) _cells.Remove(address);
        }
        if (rows.Count == 0) return change;
        rows.UnionWith(change.Rows);
        return new SheetChange(change.ValueChanges, change.Recalculated, [.. rows]);
    }

    /// <summary>Undoes a structural edit: the inverse edit, then the rewritten Formulas and the dropped cells put back.</summary>
    internal SheetChange Unrestructure(Formulas.StructuralEdit edit, StructuralOutcome outcome)
    {
        var before = Snapshot();
        var shownBefore = ShownSnapshot();
        var recalculated = new List<CellAddress>(Restructure(edit.Inverse, formatInserted: false).Change.Recalculated);
        _rowStyles = new Dictionary<int, AxisStyle>(outcome.RowsBefore);
        _columnStyles = new Dictionary<int, AxisStyle>(outcome.ColumnsBefore);
        var states = new List<(CellAddress, CellState)>();
        foreach (var (address, entry) in outcome.Rewritten) states.Add((address, StateOf(address).Recorded with { Entry = entry }));
        states.AddRange(outcome.Dropped.Select(d => (d.Address, d.State.Recorded)));
        recalculated.AddRange(Restore(states).Recalculated);
        return Diff(before, recalculated, shownBefore);
    }
}
