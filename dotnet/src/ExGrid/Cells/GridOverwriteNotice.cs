using ExGrid.Selection;

namespace ExGrid.Cells;

/// <summary>
/// The Overwrite Notice (ADR-0142, <c>CONTEXT.md</c>): a Cell Editor commit landed over text that
/// changed while the editor was open — the one change the user cannot see, because the editor
/// covers the cell. Nothing was refused: the commit landed with what the user typed, and the
/// Consumer accepted its intent. The notice names the cell, the text the user saw, and the text the
/// commit replaced — the Edit Intent's two texts, which differ exactly when a notice is raised; a
/// Consumer may offer to put the replaced value back.
///
/// <para>The grid holds no string for it. Chrome words it into the root's live region, as it words
/// a refusal (A11Y-16, LV-21); whatever else is shown, such as a toast, is Chrome's and the
/// Consumer's, and nothing is added to the row (ADR-0013).</para>
/// </summary>
/// <param name="Cell">The edited cell's position, in the order in force when the commit landed.</param>
/// <param name="Column">The edited column's name.</param>
/// <param name="SeenText">The text the user saw: what the cell painted when the editor opened, or,
/// where one of the user's own earlier writes to it had not settled then, what it painted once that
/// write settled (ADR-0142, D1). The Edit Intent's <c>SeenText</c>.</param>
/// <param name="ReplacedText">The text the cell painted as the commit landed: what it replaced. The
/// Edit Intent's <c>ReplacedText</c>.</param>
public readonly record struct GridOverwriteNotice(CellPosition Cell, string Column, string SeenText, string ReplacedText);
