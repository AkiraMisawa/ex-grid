using ExGrid.Selection;

namespace ExGrid.Clipboard;

/// <summary>
/// Why a copy is refused — say which one when refusing (ADR-0005 / 0011 / 0014).
/// <see cref="MisalignedShape"/>: the selected ranges do not line up.
/// <see cref="TooLarge"/>: past the cap — point at export instead.
/// <see cref="EmptySelection"/>: nothing to copy; Chrome typically renders it as
/// nothing happening, the safe side to fall on.
/// </summary>
public enum CopyRefusalReason
{
    EmptySelection,
    MisalignedShape,
    TooLarge,
}

/// <summary>
/// How the aligned segments combine into one block (ADR-0011): ranges sharing a column
/// span stack vertically; ranges sharing a row span concatenate horizontally. A single
/// range is canonically <see cref="Vertical"/> — the choice is unobservable there.
/// </summary>
public enum CopyOrientation
{
    Vertical,
    Horizontal,
}

/// <summary>
/// The approved copy, ready for the assembler: which rectangles to emit, in which order,
/// forming a block of <see cref="TotalRows"/> × <see cref="TotalColumns"/>. Segments
/// come in position order — by top row when vertical, by left column when horizontal —
/// never creation order, so the pasted block reads as the screen does; gaps between
/// segments close up, as in Excel (ADR-0011). Alignment admits no overlap, so
/// <c>TotalRows * TotalColumns</c> always equals the selection's cell count.
///
/// Positional, like the selection it came from: meaningful only under the Row Sequence
/// Version the selection was made under (ADR-0011).
/// </summary>
public sealed class CopyPlan
{
    internal CopyPlan(
        IReadOnlyList<SelectionRange> segments,
        CopyOrientation orientation,
        int totalRows,
        int totalColumns)
    {
        // Read-only wrapped so no caller can cast back to the array (the
        // FilterOperators pattern).
        Segments = segments is SelectionRange[] array ? Array.AsReadOnly(array) : segments;
        Orientation = orientation;
        TotalRows = totalRows;
        TotalColumns = totalColumns;
    }

    public IReadOnlyList<SelectionRange> Segments { get; }

    public CopyOrientation Orientation { get; }

    public int TotalRows { get; }

    public int TotalColumns { get; }
}

/// <summary>
/// Approved with a plan, or refused with a reason — never both, never neither
/// (ADR-0005). Reading the absent half throws rather than answering something
/// plausible.
/// </summary>
public sealed class CopyDecision
{
    private readonly CopyPlan? _plan;
    private readonly CopyRefusalReason _reason;

    private CopyDecision(CopyPlan? plan, CopyRefusalReason reason)
    {
        _plan = plan;
        _reason = reason;
    }

    internal static CopyDecision Approve(CopyPlan plan) => new(plan, default);

    internal static CopyDecision Refuse(CopyRefusalReason reason) => new(null, reason);

    public bool IsRefused => _plan is null;

    public CopyRefusalReason Reason => _plan is null
        ? _reason
        : throw new InvalidOperationException("The copy was approved; there is no refusal reason.");

    public CopyPlan Plan => _plan
        ?? throw new InvalidOperationException(
            "The copy was refused; read Reason instead (ADR-0005: say which one when refusing).");
}
