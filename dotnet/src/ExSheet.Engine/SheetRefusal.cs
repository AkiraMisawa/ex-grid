namespace ExSheet.Engine;

/// <summary>
/// Why a Sheet refused an operation. A refusal changes nothing: the engine says an operation
/// cannot be done rather than do something close to it (ADR-0047, ADR-0050).
/// </summary>
public enum SheetRefusalReason
{
    /// <summary>An insertion would push a cell holding an Entry off the Sheet's edge. Excel refuses this too.</summary>
    EntriesWouldLeaveSheet,

    /// <summary>A block written at a place would run past the Sheet's edge (ADR-0050).</summary>
    BlockWouldLeaveSheet,

    /// <summary>
    /// A copy reaches a cell whose Value is <c>#GETTING_DATA</c>: it would carry a wait that means
    /// nothing where it lands (ADR-0049).
    /// </summary>
    WaitingForData,

    /// <summary>
    /// A fill whose source is a pattern ExSheet does not continue. Excel would continue a series
    /// there, and a column of copies would be a plausible, wrong series (ADR-0050).
    /// </summary>
    FillPatternNotSupported,

    /// <summary>A fill whose target does not extend its source along one axis (ADR-0050).</summary>
    FillShapeNotSupported,

    /// <summary>
    /// A command that changes the Sheet, given while an edit is open. The Cell Editor stands over
    /// a place, and a change under it — a row inserted above it — would carry the typing to
    /// another cell when it is committed. It is refused, as Excel greys out its ribbon while a
    /// cell is being edited. A Linked Table's data is not a command, and is never refused this
    /// way (ADR-0048, ADR-0049).
    /// </summary>
    EditIsOpen,

    /// <summary>A Sheet would be given a name another Sheet of the Workbook has, without regard to case, as Excel refuses (ADR-0331).</summary>
    SheetNameTaken,

    /// <summary>The Workbook's last Sheet would be deleted. A Workbook holds at least one Sheet, as Excel's does (ADR-0331).</summary>
    LastSheet,

    /// <summary>
    /// A command or an address that names no Sheet, given while the Workbook holds several (ADR-0331
    /// §6). Taken against the Sheet shown, its outcome would depend on whether a press on a tab reached
    /// the instance before it; it names its Sheet instead.
    /// </summary>
    SheetNotNamed,

    /// <summary>
    /// A change made from the Focus cell's Cell Format away from the Sheet, set once the Selection, or the Sheet shown,
    /// is no longer the one it was read from. Set where the Selection now stands, it would be a change made for other
    /// cells. Only the Script Core's ExSheet, which reads the Focus cell and sets the change over two round trips,
    /// meets it (ADR-0100, decided 2026-10-28).
    /// </summary>
    SelectionMoved,

    /// <summary>
    /// A change made from the Focus cell's Cell Format away from the Sheet, set once that format is no longer the one it
    /// was made from: a toggle made from it would go the other way. Met as <see cref="SelectionMoved"/> is (ADR-0100).
    /// </summary>
    FocusFormatChanged,
}

/// <summary>An operation a Sheet refused, and why. Nothing was changed.</summary>
/// <param name="Reason">Which refusal.</param>
/// <param name="Message">The refusal in words, naming what caused it.</param>
public sealed record SheetRefusal(SheetRefusalReason Reason, string Message);

/// <summary>Thrown when an operation the caller asked for is refused (<see cref="SheetRefusal"/>). Nothing was changed.</summary>
public sealed class SheetRefusedException : InvalidOperationException
{
    /// <summary>Creates the exception for <paramref name="refusal"/>.</summary>
    public SheetRefusedException(SheetRefusal refusal)
        : base((refusal ?? throw new ArgumentNullException(nameof(refusal))).Message)
    {
        Refusal = refusal;
    }

    /// <summary>The refusal.</summary>
    public SheetRefusal Refusal { get; }
}
