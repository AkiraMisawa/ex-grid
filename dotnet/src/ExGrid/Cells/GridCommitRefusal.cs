using ExGrid.Selection;

namespace ExGrid.Cells;

/// <summary>Why a Cell Editor commit was refused (ADR-0142, LV-20).</summary>
public enum CommitRefusalReason
{
    /// <summary>A Row Key is in force, and no row in the Window the grid holds now answers the key of
    /// the row the editor was opened on. The row has left the Window: it may have left the data, or
    /// only the rows a pushed or fetched Window holds — scrolled far away — and the grid cannot tell
    /// which, since it holds no row beyond its Window (ADR-0160). The editor holds the key while it is
    /// open, so a commit made once the row is back in the Window lands; Escape leaves without
    /// writing.</summary>
    RowLeftTheWindow,

    /// <summary>No Row Key is in force, and the Row Sequence Version moved while the editor was
    /// open: the position the editor stands at names another row now (ADR-0011's note of
    /// 2026-10-07). Escape is the way out.</summary>
    OrderMoved,

    /// <summary>Escape was typed while the commit was pending, its verdict or its edit's answer not yet given
    /// (ADR-0380 section 3): what was asked is called off, nothing is written, and the editor stays open with the
    /// text. Raised by the grid over the Script Core.</summary>
    Cancelled,
}
