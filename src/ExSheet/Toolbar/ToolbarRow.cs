namespace ExSheet;

/// <summary>
/// A Toolbar Row (ADR-0100): one band of a Sheet Toolbar, declared in an ExSheet's
/// <c>ToolbarContent</c>, holding Toolbar Items in markup order. Every row of a Sheet is the same
/// height, so the toolbar's height is a count of rows; what a row holds is fitted to it and
/// clipped, never allowed to grow it. Its <see cref="KeyTip"/> is what a ribbon tab's is.
/// </summary>
public partial class ToolbarRow
{
}
