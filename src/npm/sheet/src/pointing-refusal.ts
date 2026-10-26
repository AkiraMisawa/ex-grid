// Why a press on a grid of a Pointing Scope, or an arrow key after one, wrote nothing (ADR-0058), the
// counterpart of C#'s PointingRefusal.cs. A Formula reads a Linked Table's cell by its row's key, and its column
// by name; a press or a key that stands for anything else is refused, rather than written as something close
// to it.

/**
 * Why a press on a grid of a Pointing Scope, or an arrow key after one, wrote nothing into the Formula being
 * edited (ADR-0058):
 *
 * - `severalCells`: more than one cell — a Shift+press, a press on a Row Heading or on the corner, a drag that
 *   reached another cell, or Shift and an arrow key after a press. A drag takes back what its press wrote.
 * - `severalColumns`: more than one column — a Shift+press on a header, or a drag across headers. A drag takes
 *   back what its press wrote.
 * - `headerGroup`: a Header Group's rectangle, which stands over several columns (ADR-0032).
 * - `columnNotInTable`: a column the Linked Table does not have.
 * - `noKey`: a cell of a table declared without a key, whose row no Formula can name.
 * - `blankKey`: a cell whose row has a blank key.
 * - `keyIsAnError`: a cell whose row holds an Error Value as its key, which no lookup finds.
 * - `rowNotArrived`: a cell whose row has not arrived yet; an arrow key that reaches one moves nothing.
 * - `tableNotDeclared`: the Sheet that points declares no Linked Table by the grid's table's name.
 * - `notPointing`: the press arrived after the Sheet stopped pointing.
 * - `dataEdge`: Ctrl+arrow after a press; where the data ends is not known to a grid that holds only the rows
 *   near those it shows (ADR-0058, "The keyboard").
 * - `cellNotHeld`: an arrow key after a press, when the grid no longer holds the cell, or shows the column,
 *   pointed at.
 */
export type PointingRefusalReason =
    | 'severalCells' | 'severalColumns' | 'headerGroup' | 'columnNotInTable' | 'noKey' | 'blankKey' | 'keyIsAnError'
    | 'rowNotArrived' | 'tableNotDeclared' | 'notPointing' | 'dataEdge' | 'cellNotHeld';

/** A press on a grid of a Pointing Scope that wrote nothing, and why (ADR-0058). The text of the Formula being
 * edited is as it was before the press. */
export interface PointingRefusal {
    /** Which refusal. */
    readonly reason: PointingRefusalReason;
    /** The refusal in words, for the user, naming the table and the column where they are known. */
    readonly message: string;
}
