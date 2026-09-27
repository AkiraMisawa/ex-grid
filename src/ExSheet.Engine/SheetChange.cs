namespace ExSheet.Engine;

/// <summary>
/// What one change to a Sheet did, reported after its recalculation has completed. Every Value it
/// names is already published, and all of them come from that one recalculation (ADR-0047): a
/// component hands back a new row instance for exactly the rows in <see cref="Rows"/>.
/// </summary>
public sealed class SheetChange
{
    internal SheetChange(IReadOnlyList<CellAddress> valueChanges, IReadOnlyList<CellAddress> recalculated, IReadOnlyList<int> rows)
    {
        ValueChanges = valueChanges;
        Recalculated = recalculated;
        Rows = rows;
    }

    /// <summary>A change that changed nothing.</summary>
    internal static SheetChange None { get; } = new([], [], []);

    /// <summary>The cells whose Value changed (a cell becoming blank included), in row-major order.</summary>
    public IReadOnlyList<CellAddress> ValueChanges { get; }

    /// <summary>
    /// The Formula cells this change recomputed, in row-major order: the changed cells and the
    /// cells that depend on them, and no other (ADR-0047).
    /// </summary>
    public IReadOnlyList<CellAddress> Recalculated { get; }

    /// <summary>
    /// The rows whose painted content may differ — a Value, an Entry or a cell's formatting changed —
    /// in ascending order, each once.
    /// </summary>
    public IReadOnlyList<int> Rows { get; }
}
