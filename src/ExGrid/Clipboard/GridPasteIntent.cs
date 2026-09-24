using ExGrid.Selection;

namespace ExGrid.Clipboard;

/// <summary>
/// The one Edit Intent a paste raises (ADR-0007/0014): the approved plan, the source
/// block's values, and the Row Sequence Version the target selection was made under.
/// One notification carrying all cells, never one per cell — a bulk paste is one
/// intent, so one Ctrl+Z (ADR-0007).
///
/// <para>Positional, like the selection: the Consumer resolves "rows N–M of the
/// current order, this column" itself, and rows that are off screen or not yet fetched
/// are included — that is intended (ADR-0014). A version that no longer matches the
/// Consumer's own means the order moved under the intent, and it must be discarded
/// rather than applied to different rows (ADR-0011).</para>
/// </summary>
public sealed class GridPasteIntent
{
    internal GridPasteIntent(
        PastePlan plan, IReadOnlyList<IReadOnlyList<string>> values, int rowSequenceVersion)
    {
        Plan = plan;
        Values = values;
        RowSequenceVersion = rowSequenceVersion;
    }

    /// <summary>The approved plan: the target ranges, and how the source block tiles
    /// onto them (ADR-0014).</summary>
    public PastePlan Plan { get; }

    /// <summary>The source block, <c>Plan.Source.Rows</c> × <c>Plan.Source.Columns</c>.
    /// Raw strings — Excel's HTML flavour supplies full precision where it was on the
    /// clipboard (ADR-0005); the Consumer parses per its own column types.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Values { get; }

    /// <summary>The order these positions are written in (ADR-0011).</summary>
    public int RowSequenceVersion { get; }

    /// <summary>How many cells the intent covers — the sum of the target areas, the
    /// same figure the status display shows (ADR-0014).</summary>
    public long CellCount
    {
        get
        {
            long sum = 0;
            foreach (var target in Plan.Targets)
                sum += target.CellCount;
            return sum;
        }
    }

    /// <summary>The value that lands on one target cell, through the plan's tiling.</summary>
    public string ValueFor(CellPosition target)
    {
        var source = Plan.SourceCellFor(target);
        return Values[source.Row][source.Column];
    }
}
