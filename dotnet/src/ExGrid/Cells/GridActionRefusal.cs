namespace ExGrid.Cells;

/// <summary>
/// Why an Action press was refused (ADR-0142, LV-12, LV-20). A press acts on the row it was
/// pressed on, as that row is now: its values changing since it was painted refuses nothing. It is
/// refused only when the grid can no longer find that row in the rows it holds now — out of the
/// Window, named by a position under an order that has moved, or shown by a Source since replaced —
/// because acting on whatever row stands there now would act on a stranger (ADR-0011).
/// </summary>
public enum ActionRefusalReason
{
    /// <summary>The row the press was made on is not in the Window the grid holds now: with a Row
    /// Key, no row there answers its key; without one, the position it was pressed on holds no row.
    /// It may have left the data, or only the Window — scrolled out of the rows a pushed or fetched
    /// Window holds — and the grid cannot tell which, since it holds no row beyond its Window
    /// (ADR-0160). Nothing was done; a press made on the row once it is back in view acts on it.</summary>
    RowLeftTheWindow,

    /// <summary>The press named its row by a position whose order has moved since, and nothing else
    /// pairs it with its row: without a Row Key, a press whose button a render disposed, or Space on
    /// the Focus; with one, a press whose order moved before the grid heard it and whose button a
    /// render has since disposed, since the grid keeps no key of an earlier render (ADR-0160). The
    /// position names another row now.</summary>
    OrderMoved,

    /// <summary>The press was made on what the grid painted from a Grid Source it has since been
    /// unbound from: the <c>Source</c> parameter was replaced by another instance before the press
    /// was answered. The row it was made on belongs to a source the grid no longer shows, so nothing
    /// is done — whatever the new source holds at that position or under that Row Key, and whatever
    /// the two sources' Row Sequence Versions are — and the refusal names no row (ADR-0142,
    /// ADR-0160).</summary>
    SourceChanged,
}