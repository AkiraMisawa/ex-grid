using ExGrid.Selection;

namespace ExGrid.Clipboard;

/// <summary>
/// The Clear Intent Delete raises (ADR-0054): these positions should hold <b>no value</b>.
/// It carries no value at all, which is what separates it from a paste of empty text — on
/// an amount column "" is a parse failure or a zero, and a cleared cell is neither. The
/// Consumer maps it onto its own notion of Blank, per column.
///
/// <para>Positional, like a paste: the Consumer resolves "rows N–M of the current order,
/// this column" itself, rows off screen or not yet fetched included (ADR-0014). A version
/// that no longer matches the Consumer's own means the order moved under the intent, and
/// it must be discarded rather than applied to different rows (ADR-0011). One intent for
/// the whole selection, so one Ctrl+Z (ADR-0007).</para>
/// </summary>
public sealed class GridClearIntent
{
    internal GridClearIntent(IReadOnlyList<SelectionRange> targets, int rowSequenceVersion)
    {
        Targets = targets;
        RowSequenceVersion = rowSequenceVersion;
    }

    /// <summary>The ranges to clear — the selection as it stood, in creation order. Ranges
    /// may overlap; a cell covered twice is cleared once.</summary>
    public IReadOnlyList<SelectionRange> Targets { get; }

    /// <summary>The order these positions are written in (ADR-0011).</summary>
    public int RowSequenceVersion { get; }

    /// <summary>How many cells the intent covers — the sum of the target areas, the figure
    /// the status display shows (ADR-0014).</summary>
    public long CellCount
    {
        get
        {
            long sum = 0;
            foreach (var target in Targets)
                sum += target.CellCount;
            return sum;
        }
    }
}
