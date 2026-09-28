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
///
/// <para>After a spilled paste the Consumer accepts, the block is the Selection; a Consumer
/// that will not write it calls <see cref="Refuse"/> before its handler completes, and the
/// Selection stays where it was, as in Excel (ADR-0050, item 3).</para>
/// </summary>
public sealed class GridPasteIntent
{
    internal GridPasteIntent(
        PastePlan plan, IReadOnlyList<IReadOnlyList<string>> values, int rowSequenceVersion,
        IReadOnlyList<IReadOnlyList<PasteFieldOrigin>>? origins = null)
    {
        Plan = plan;
        Values = values;
        RowSequenceVersion = rowSequenceVersion;
        Origins = origins ?? AllShown(values);
    }

    private static IReadOnlyList<IReadOnlyList<PasteFieldOrigin>> AllShown(IReadOnlyList<IReadOnlyList<string>> values)
    {
        var origins = new IReadOnlyList<PasteFieldOrigin>[values.Count];
        for (var r = 0; r < values.Count; r++)
            origins[r] = new PasteFieldOrigin[values[r].Count];
        return origins;
    }

    /// <summary>The approved plan: the target ranges, and how the source block tiles
    /// onto them (ADR-0014).</summary>
    public PastePlan Plan { get; }

    /// <summary>The source block, <c>Plan.Source.Rows</c> × <c>Plan.Source.Columns</c>.
    /// Raw strings — Excel's HTML flavour supplies full precision where it was on the
    /// clipboard (ADR-0005); the Consumer parses per its own column types.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Values { get; }

    /// <summary>Where each field of <see cref="Values"/> came from, at the same position
    /// (ADR-0050, item 10): <see cref="PasteFieldOrigin.Invariant"/> for Excel's <c>x:num</c>
    /// and ExGrid's own unformatted HTML, read under the invariant culture;
    /// <see cref="PasteFieldOrigin.ShownText"/> for everything else, read as typed. A block
    /// typed in the grid (Ctrl+Enter) is shown text throughout.</summary>
    public IReadOnlyList<IReadOnlyList<PasteFieldOrigin>> Origins { get; }

    /// <summary>The order these positions are written in (ADR-0011).</summary>
    public int RowSequenceVersion { get; }

    /// <summary>Whether the Consumer refused the paste through <see cref="Refuse"/>.</summary>
    public bool IsRefused { get; private set; }

    /// <summary>
    /// The Consumer's answer that it does not write this paste (ADR-0050, item 3), the paste
    /// counterpart of <see cref="ExGrid.Selection.GridFillIntent.Refuse"/>. Called before the
    /// <c>OnPaste</c> handler completes, it leaves the Selection and the Focus where they were;
    /// a handler that completes without calling it has accepted, and a spilled block becomes
    /// the Selection. Telling the user why is the Consumer's: the grid knows only that it was
    /// refused, not the reason.
    /// </summary>
    public void Refuse() => IsRefused = true;

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

    /// <summary>Where the value that lands on one target cell came from, through the plan's
    /// tiling (ADR-0050, item 10).</summary>
    public PasteFieldOrigin OriginFor(CellPosition target)
    {
        var source = Plan.SourceCellFor(target);
        return Origins[source.Row][source.Column];
    }
}
