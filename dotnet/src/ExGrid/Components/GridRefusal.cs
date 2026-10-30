using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Finding;
using ExGrid.Rows;
using ExGrid.Selection;

namespace ExGrid.Components;

/// <summary>
/// A gesture the grid refused (ADR-0005), as <see cref="ExGrid{TRow}.OnRefused"/> hears every one: one event whose
/// kind says what was refused and carries that kind's own facts (ADR-0350 section 16). Read it with a pattern:
/// <c>refusal is GridRefusal&lt;Trade&gt;.Copy { Reason: CopyRefusalReason.TooLarge }</c>.
/// </summary>
/// <typeparam name="TRow">The grid's row type.</typeparam>
public abstract record GridRefusal<TRow>
    where TRow : class
{
    private GridRefusal()
    {
    }

    /// <summary>A copy that wrote nothing: the reason names the rule it hit (ADR-0005, ADR-0011).</summary>
    /// <param name="Reason">The rule the copy hit.</param>
    public sealed record Copy(CopyRefusalReason Reason) : GridRefusal<TRow>;

    /// <summary>A paste that raised no intent, and so wrote nothing (ADR-0014, ADR-0035).</summary>
    /// <param name="Reason">The rule the paste hit.</param>
    public sealed record Paste(PasteRefusalReason Reason) : GridRefusal<TRow>;

    /// <summary>A Cell Editor commit that raised no intent, because the row the editor was opened on is not
    /// where the grid can find it (ADR-0142): the editor stands with the typing.</summary>
    /// <param name="Reason">Why the commit found no row.</param>
    /// <param name="Cell">Where the editor stands.</param>
    /// <param name="Column">The column it edits.</param>
    public sealed record Commit(CommitRefusalReason Reason, CellPosition Cell, string Column) : GridRefusal<TRow>;

    /// <summary>A Find step or a Replace that moved or wrote nothing, or a Ctrl+F or Ctrl+H that opened nothing
    /// (ADR-0055, ADR-0332).</summary>
    /// <param name="Reason">Why nothing was found or written.</param>
    public sealed record Find(FindRefusalReason Reason) : GridRefusal<TRow>;

    /// <summary>A press on a mark that marked nothing (ADR-0043).</summary>
    /// <param name="Reason">Why the press marked nothing.</param>
    public sealed record Mark(MarkRefusalReason Reason) : GridRefusal<TRow>;

    /// <summary>A Selection placed by the Consumer that placed nothing, because its positions no longer name the
    /// rows they were written for (ADR-0050 item 4, ADR-0011).</summary>
    /// <param name="Reason">What moved since the positions were written.</param>
    public sealed record Placement(PlacementRefusalReason Reason) : GridRefusal<TRow>;

    /// <summary>An action press that raised nothing (ADR-0020, ADR-0142).</summary>
    /// <param name="Reason">Why the press raised nothing.</param>
    /// <param name="Row">The row it was pressed on while the Window still holds it, else null.</param>
    /// <param name="Column">The Action Column's name.</param>
    /// <param name="ActionName">The action's name.</param>
    public sealed record Action(ActionRefusalReason Reason, TRow? Row, string Column, string ActionName) : GridRefusal<TRow>;
}

/// <summary>Why a Selection the Consumer placed was not placed (ADR-0050 item 4).</summary>
public enum PlacementRefusalReason
{
    /// <summary>The source's Row Sequence Version moved since the positions were written (ADR-0011).</summary>
    OrderMoved,

    /// <summary>An edit's commit, made first, brought in another source (ADR-0142).</summary>
    SourceChanged,

    /// <summary>An edit's commit, made first, moved the columns, so the positions name other columns (ADR-0011).</summary>
    ColumnsMoved,
}
