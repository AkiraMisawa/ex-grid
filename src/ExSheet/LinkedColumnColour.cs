using ExGrid.Cells;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// A Linked Table column the Formula being edited reads, and the colour its References wear
/// (ADR-0057, ADR-0049): what ExSheet tells its Consumer, so that the grid showing the table can
/// outline the column in that colour. The colour is the core's, never the Consumer's.
/// </summary>
/// <param name="Column">The table and the column, named as the Consumer declared them, however
/// the Formula cased them.</param>
/// <param name="Colour">The colour the column's References wear in the editor.</param>
public sealed record LinkedColumnColour(LinkedTableColumn Column, ReferenceColour Colour);
