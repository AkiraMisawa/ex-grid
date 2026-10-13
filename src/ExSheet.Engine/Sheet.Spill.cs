using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>
/// Spilled arrays (ADR-0125): after each pass of a recalculation the layout of every spill is
/// decided again, Anchors in address order, so that which of two Anchors spills never depends on
/// which was computed first; what the layout changed is recalculated in a further pass.
/// </summary>
public sealed partial class Sheet
{
    /// <summary>The most passes one recalculation makes before it stops, for spills that keep moving each other.</summary>
    private const int MostPasses = 64;

    /// <summary>
    /// How many passes the last recalculation made. Every volatile Formula is computed once in each:
    /// once, and once more after each layout that changed something, never more than
    /// <see cref="MostPasses"/>. Layer 1 reads it (ADR-0124).
    /// </summary>
    internal int RecalculationPasses { get; private set; }

    /// <summary>
    /// One recalculation: what <paramref name="changed"/> reaches and every volatile Formula, the moment
    /// read once (ADR-0124); then the spills laid out, and what their layout changed recalculated with
    /// every volatile Formula, until a layout changes nothing. It always ends on a layout, so an
    /// Anchor shows what the last layout decided: <c>#SPILL!</c> where its Spill Range is in the way.
    /// </summary>
    /// <remarks>
    /// A volatile Formula is computed again after every layout that changed something, because a
    /// Reference <c>INDIRECT</c> or <c>OFFSET</c> computes may read what the layout changed, and no
    /// Dependent says so. Each draws and reads the clock as in the first pass (ADR-0180), and an
    /// Anchor computed again with an array of the shape the layout blocked stays <c>#SPILL!</c>
    /// (<c>RecalculatePass</c>), so computing it again changes nothing and the next layout settles.
    /// A spill blocked or freed by a change is therefore two passes: every volatile Formula is
    /// computed twice. Spills that move each other take a pass for each layout that moves one.
    /// </remarks>
    private SheetChange Recalculate(HashSet<CellAddress> changed, SortedSet<int> rows)
    {
        _moment = NowSource?.Invoke();
        var changes = new List<SheetChange> { RecalculatePass(changed, rows, withVolatile: true) };
        var passes = 1;
        while (true)
        {
            var laidOut = LayOutSpills();
            if (laidOut.Count == 0) break;
            foreach (var address in laidOut) rows.Add(address.Row);
            changes.Add(new SheetChange(laidOut, [], [.. laidOut.Select(a => a.Row).Distinct().Order()]));
            var readers = new HashSet<CellAddress>(laidOut.SelectMany(Dependents));
            if ((readers.Count == 0 && _volatileFormulas.Count == 0) || passes == MostPasses) break;
            changes.Add(RecalculatePass(readers, rows, withVolatile: true));
            passes++;
        }
        RecalculationPasses = passes;
        return SheetChange.Merge(changes);
    }

    /// <summary>
    /// Decides where every array spills, Anchors in address order: an Anchor whose Spill Range passes
    /// the Sheet's edge, or covers a cell holding an Entry or another Anchor's spill, shows
    /// <c>#SPILL!</c> and spills nothing. Cells no Anchor spills into any more are released.
    /// </summary>
    /// <returns>The cells whose Value the layout changed, Anchors included.</returns>
    private List<CellAddress> LayOutSpills()
    {
        var changed = new List<CellAddress>();
        var claimed = new Dictionary<CellAddress, (CellAddress Anchor, Value Value)>();
        foreach (var anchor in _cells.Values.Where(c => c.Array is not null).OrderBy(c => c.Address).ToList())
        {
            if (anchor.Entry?.Parsed is null)
            {
                anchor.Array = null;
                anchor.SpillBlocked = false;
                continue;
            }
            var array = anchor.Array!;
            var origin = anchor.Address;
            var blocked = origin.Row + array.Rows > RowCount || origin.Column + array.Columns > ColumnCount;
            for (var r = 0; r < array.Rows && !blocked; r++)
            {
                for (var c = 0; c < array.Columns && !blocked; c++)
                {
                    if (r == 0 && c == 0) continue;
                    var address = new CellAddress(origin.Row + r, origin.Column + c);
                    blocked = claimed.ContainsKey(address) || _cells.GetValueOrDefault(address)?.Entry is not null;
                }
            }
            // Whether it spills is what A1# reads: a change of it is a change, whatever the Value shows.
            if (anchor.SpillBlocked != blocked) changed.Add(anchor.Address);
            anchor.SpillBlocked = blocked;
            Set(anchor, blocked ? Value.FromError(ErrorValue.Spill) : array[0, 0] ?? Value.FromNumber(0));
            if (blocked) continue;
            for (var r = 0; r < array.Rows; r++)
            {
                for (var c = 0; c < array.Columns; c++)
                {
                    if (r == 0 && c == 0) continue;
                    claimed[new CellAddress(origin.Row + r, origin.Column + c)] = (origin, array[r, c] ?? Value.FromNumber(0));
                }
            }
        }

        foreach (var cell in _cells.Values.Where(c => c.SpilledFrom is not null && !claimed.ContainsKey(c.Address)).ToList())
        {
            cell.SpilledFrom = null;
            if (cell.Entry is null) Set(cell, null);
            if (cell.IsEmpty) _cells.Remove(cell.Address);
        }
        foreach (var (address, (anchorAddress, value)) in claimed)
        {
            if (!_cells.TryGetValue(address, out var cell)) _cells[address] = cell = new Cell(address);
            cell.SpilledFrom = anchorAddress;
            Set(cell, value);
        }
        changed.Sort();
        return changed;

        void Set(Cell cell, Value? value)
        {
            if (Value.Unchanged(cell.Value, value)) return;
            cell.Value = value;
            changed.Add(cell.Address);
        }
    }

    /// <summary>The Anchor whose array gives a cell its Value, or <see langword="null"/> for a cell no array spills into (ADR-0125).</summary>
    public CellAddress? SpilledFrom(CellAddress address) => _cells.GetValueOrDefault(address)?.SpilledFrom;
}
