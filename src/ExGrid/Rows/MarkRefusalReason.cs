namespace ExGrid.Rows;

/// <summary>
/// Why a press on a mark marked nothing (ADR-0043's note of 2026-10-08, decided with the user on
/// 2026-10-09; MK-9). A press on a row's checkbox, the header's or "Mark all N rows" is judged against
/// the paint it was made on, and what it can still name exactly is honoured: a row's checkbox names its
/// row by identity, which an order move leaves it, and the header's checkbox under a pager names the
/// page it was pressed on, as positions under the order it was pressed in, whichever page is shown now.
/// The rest marks nothing, raises no Row Mark intent, and is refused through <c>OnMarkRefused</c>, once
/// per press. A Refusal like any other: the grid holds no sentence for it, so a Consumer words it, into
/// its refusal live region (A11Y-16).
/// </summary>
public enum MarkRefusalReason
{
    /// <summary>
    /// The press named rows by position, and the order it named them in has moved since — a sort, a
    /// filter, a live reorder (ADR-0011). The header's checkbox named its page or the whole result as it
    /// stood, and "Mark all N rows" the whole result: lining up the rows that stand there now would mark
    /// rows the user never saw there. A row's checkbox names its row by identity and is not refused for
    /// this, save where its click never came — a render disposed the checkbox's row before the click was
    /// heard, so the grid answered the press itself — and the position it was pressed at is all that
    /// names its row: the grid keeps no row and no Row Key of an earlier render (ADR-0160). Nothing was
    /// marked.
    /// </summary>
    OrderMoved,

    /// <summary>
    /// The press was made on what the grid painted from a Grid Source it has since been unbound from:
    /// the <c>Source</c> parameter was replaced by another instance before the press was heard. The rows
    /// it was aimed at belong to data that is no longer shown, so nothing is marked in either source's
    /// marks — whatever the new source holds at that position or under that Row Key, and whatever the two
    /// sources' Row Sequence Versions are (ADR-0142). A row's checkbox is refused for this too.
    /// </summary>
    SourceChanged,
}
