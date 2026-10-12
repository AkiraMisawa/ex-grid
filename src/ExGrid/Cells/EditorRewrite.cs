namespace ExGrid.Cells;

/// <summary>
/// The Consumer's rewrite of the editor's text (ADR-0051, F4 cycling the Reference at the
/// caret): the whole text the editor is to hold, and the selection in it — collapsed to a
/// caret where <paramref name="SelectionStart"/> equals <paramref name="SelectionEnd"/>. The
/// grid writes the text to both editor surfaces and has the listener place the selection while
/// the edit is in Caret, Excel's Edit mode, and a caret at its end in Overwrite and while
/// pointing, as Excel places them after F4; it does not know what was rewritten. A selection that does not lie inside the text is refused
/// by name, never clamped into a plausible caret.
/// </summary>
/// <param name="Text">The text the editor holds after the rewrite.</param>
/// <param name="SelectionStart">Where the selection starts in <paramref name="Text"/>, or the caret.</param>
/// <param name="SelectionEnd">Where it ends; <paramref name="SelectionStart"/> for a caret.</param>
public sealed record EditorRewrite(string Text, int SelectionStart, int SelectionEnd);
