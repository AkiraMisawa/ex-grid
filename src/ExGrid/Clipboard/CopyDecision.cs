using ExGrid.Selection;

namespace ExGrid.Clipboard;

/// <summary>
/// Why a copy is refused — say which one when refusing (ADR-0005 / 0011 / 0014).
/// <see cref="MisalignedShape"/>: the selected ranges do not line up.
/// <see cref="TooLarge"/>: past the cap — point at export instead.
/// <see cref="EmptySelection"/>: nothing to copy; Chrome typically renders it as
/// nothing happening, the safe side to fall on.
/// <see cref="RowsUnavailable"/>: the selection runs beyond the Window and nobody can
/// be asked for the rows — a push Consumer that passed no provider, or one whose
/// answer did not cover the selection. Component-level, never produced by the pure
/// rules: refusing by name beats copying the fraction in hand (ADR-0005).
/// </summary>
public enum CopyRefusalReason
{
    /// <summary>Nothing is selected, so there is nothing to copy.</summary>
    EmptySelection,

    /// <summary>The ranges do not line up into one block: they neither share a column
    /// span with disjoint rows nor share a row span with disjoint columns (ADR-0011).</summary>
    MisalignedShape,

    /// <summary>The block, with its header row when one is asked for, is past the cell
    /// cap; point at export instead (ADR-0005).</summary>
    TooLarge,

    /// <summary>The selection runs beyond the Window and the rows could not be had.
    /// Raised by the component, never by <see cref="ClipboardRules"/>.</summary>
    RowsUnavailable,
}

/// <summary>
/// How the aligned segments combine into one block (ADR-0011): ranges sharing a column
/// span stack vertically; ranges sharing a row span concatenate horizontally. A single
/// range is canonically <see cref="Vertical"/> — the choice is unobservable there.
/// </summary>
public enum CopyOrientation
{
    /// <summary>The segments share a column span and stack top to bottom.</summary>
    Vertical,

    /// <summary>The segments share a row span and sit side by side, left to right.</summary>
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

    /// <summary>The rectangles to emit, in position order: by top row when
    /// <see cref="CopyOrientation.Vertical"/>, by left column when
    /// <see cref="CopyOrientation.Horizontal"/>.</summary>
    public IReadOnlyList<SelectionRange> Segments { get; }

    /// <summary>How the segments combine into one block.</summary>
    public CopyOrientation Orientation { get; }

    /// <summary>The block's height in rows, not counting a header row.</summary>
    public int TotalRows { get; }

    /// <summary>The block's width in columns.</summary>
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

    /// <summary>Whether the copy is refused: read <see cref="Reason"/> if so, and
    /// <see cref="Plan"/> if not.</summary>
    public bool IsRefused => _plan is null;

    /// <summary>Why the copy is refused. Throws when it was approved.</summary>
    public CopyRefusalReason Reason => _plan is null
        ? _reason
        : throw new InvalidOperationException("The copy was approved; there is no refusal reason.");

    /// <summary>The approved copy. Throws when it was refused.</summary>
    public CopyPlan Plan => _plan
        ?? throw new InvalidOperationException(
            "The copy was refused; read Reason instead (ADR-0005: say which one when refusing).");
}
