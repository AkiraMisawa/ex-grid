using ExGrid.Clipboard;

namespace ExGrid.Selection;

/// <summary>
/// Where a fill-handle drag reaches, and whether it may be raised (ADR-0050, item 5): the
/// gesture is the core's, the meaning is the Consumer's. Pure and stateless — the component
/// holds the drag, and this answers what it has reached.
///
/// <para>The only shape is one rectangle extended along one axis. A disjoint Selection has
/// no handle, as in Excel. The axis is the one the pointer has travelled further along past
/// the source, counted in cells; a tie goes down or up, Excel's usual fill. A pointer back
/// inside the source reaches nothing, and a release there raises nothing.</para>
/// </summary>
public static class FillRules
{
    /// <summary>
    /// The range the handle stands on, or null when there is none: the Selection's last
    /// range, only while it is the one range — a disjoint Selection shows no handle
    /// (ADR-0050, item 5).
    /// </summary>
    public static SelectionRange? HandleRange(GridSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return selection.Ranges.Count == 1 ? selection.Ranges[^1] : null;
    }

    /// <summary>
    /// What a drag from <paramref name="source"/>'s handle has reached with the pointer over
    /// <paramref name="pointer"/>: the cells a release would fill — never the source itself
    /// — and the direction. Null while the pointer is inside the source.
    /// </summary>
    public static FillExtension? ExtensionFor(SelectionRange source, CellPosition pointer)
    {
        var rows = Beyond(pointer.Row, source.TopRow, source.BottomRow);
        var columns = Beyond(pointer.Column, source.LeftColumn, source.RightColumn);
        if (rows == 0 && columns == 0)
            return null;

        // One axis only, the one the pointer went further along (ADR-0050); a tie is the
        // vertical fill, the one Excel users make most.
        if (Math.Abs(rows) >= Math.Abs(columns))
        {
            return rows > 0
                ? new FillExtension(
                    new SelectionRange(source.BottomRow + 1, source.LeftColumn, rows, source.ColumnCount),
                    GridDirection.Down)
                : new FillExtension(
                    new SelectionRange(pointer.Row, source.LeftColumn, -rows, source.ColumnCount),
                    GridDirection.Up);
        }
        return columns > 0
            ? new FillExtension(
                new SelectionRange(source.TopRow, source.RightColumn + 1, source.RowCount, columns),
                GridDirection.Right)
            : new FillExtension(
                new SelectionRange(source.TopRow, pointer.Column, source.RowCount, -columns),
                GridDirection.Left);
    }

    /// <summary>
    /// The release (ADR-0050, item 5): null when the pointer is inside the source and
    /// nothing is filled; otherwise approved with the extension, or refused whole as
    /// <see cref="PasteRefusalReason.TargetNotEditable"/> when the target covers a column
    /// that is not Editable. Paste and fill go through one gate, and it is judged before
    /// anything is raised (ADR-0035). <paramref name="columnIsEditable"/> is required for
    /// the reason <see cref="ClipboardRules.PlanPaste"/> requires it.
    /// </summary>
    public static FillDecision? PlanFill(
        SelectionRange source, CellPosition pointer, Func<int, bool> columnIsEditable)
    {
        ArgumentNullException.ThrowIfNull(columnIsEditable);
        if (ExtensionFor(source, pointer) is not { } extension)
            return null;
        for (var column = extension.Target.LeftColumn; column <= extension.Target.RightColumn; column++)
        {
            if (!columnIsEditable(column))
                return FillDecision.Refuse(PasteRefusalReason.TargetNotEditable);
        }
        return FillDecision.Approve(extension);
    }

    /// <summary>How far past <paramref name="first"/>..<paramref name="last"/> a position
    /// lies: positive after, negative before, zero inside.</summary>
    private static int Beyond(int position, int first, int last) =>
        position > last ? position - last
        : position < first ? position - first
        : 0;
}

/// <summary>
/// What a fill-handle drag has reached (ADR-0050, item 5): the cells a release would fill,
/// which never include the source, and the direction they lie in from it.
/// </summary>
/// <param name="Target">The cells to fill, adjacent to the source along one axis and as
/// wide (or as tall) as it.</param>
/// <param name="Direction">Which way from the source the target lies.</param>
public readonly record struct FillExtension(SelectionRange Target, GridDirection Direction);

/// <summary>
/// A fill-handle release approved with its extension, or refused with a reason — never
/// both, never neither. Reading the absent half throws rather than answering something
/// plausible (ADR-0014's shape, for a fill).
/// </summary>
public sealed class FillDecision
{
    private readonly FillExtension? _extension;
    private readonly PasteRefusalReason _reason;

    private FillDecision(FillExtension? extension, PasteRefusalReason reason)
    {
        _extension = extension;
        _reason = reason;
    }

    internal static FillDecision Approve(FillExtension extension) => new(extension, default);

    internal static FillDecision Refuse(PasteRefusalReason reason) => new(null, reason);

    /// <summary>Whether the fill is refused: read <see cref="Reason"/> if so, and
    /// <see cref="Extension"/> if not.</summary>
    public bool IsRefused => _extension is null;

    /// <summary>Why the fill is refused. Throws when it was approved.</summary>
    public PasteRefusalReason Reason => _extension is null
        ? _reason
        : throw new InvalidOperationException("The fill was approved; there is no refusal reason.");

    /// <summary>The approved extension. Throws when the fill was refused.</summary>
    public FillExtension Extension => _extension
        ?? throw new InvalidOperationException("The fill was refused; read Reason instead (ADR-0035).");
}

/// <summary>
/// The Fill Intent (ADR-0050, item 5): a fill-handle drag, released. The grid writes
/// nothing — what a fill means (copies, a series, a refusal) is the Consumer's, which
/// resolves the intent into its own data and pushes new rows back, as with every edit
/// (ADR-0007). One drag is one intent.
///
/// <para>Positional, like a paste: the Consumer resolves the ranges under the order they
/// were written in, and a <see cref="RowSequenceVersion"/> that no longer matches its own
/// means the order moved under the intent, which must then be discarded (ADR-0011).</para>
/// </summary>
public sealed class GridFillIntent
{
    internal GridFillIntent(SelectionRange source, FillExtension extension, int rowSequenceVersion)
    {
        Source = source;
        Target = extension.Target;
        Direction = extension.Direction;
        RowSequenceVersion = rowSequenceVersion;
    }

    /// <summary>The range the handle was dragged from: the Selection when the drag began.</summary>
    public SelectionRange Source { get; }

    /// <summary>The cells to fill, adjacent to the source along one axis; never the source
    /// itself. Every column it covers was Editable when the intent was raised (ADR-0035).</summary>
    public SelectionRange Target { get; }

    /// <summary>Which way from the source the target lies.</summary>
    public GridDirection Direction { get; }

    /// <summary>The order these positions are written in (ADR-0011).</summary>
    public int RowSequenceVersion { get; }
}
