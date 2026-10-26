// The commands a menu offers, and what the Context Menu hands a Consumer's (ADR-0010, ADR-0036).

import type { ColumnType } from '../query/columns';
import type { SelectionRange } from '../selection/range';

/**
 * A command a menu offers (ADR-0010, ADR-0036): an id, whether it may run, and what running it does. What a
 * menu calls it is the Chrome's, resolved from the id (`commandLabel`), so a menu can be shown in any
 * language without the core holding a sentence.
 */
export interface GridCommand {
    /** The command's id: one of the core's `gridCommandIds`, or the Consumer's own. */
    readonly id: string;
    /** Whether it may run now. A disabled command is shown, and is never moved to or run. */
    readonly enabled: boolean;
    /** Runs it. The menu has closed and the keyboard is back on the grid by then, so a command may move the
     * keyboard on itself. */
    readonly invoke: () => void;
}

/**
 * What `contextCommands` is handed when the Context Menu opens (ADR-0036): the cell it was opened on, and
 * the Selection its commands act on. The Selection is rectangles in position space and the Row Sequence
 * Version they are written in (ADR-0011), never rows: it may cover rows the grid has never fetched, and
 * resolving them is the Consumer's question, asked of the data it owns.
 */
export interface GridContextMenuContext<Row> {
    /** The row the menu was opened on: a press or the Focus is inside the Window, so the grid holds it. */
    readonly row: Row;
    /** The `name` of the column the menu was opened on. */
    readonly column: string;
    /** That column's type. */
    readonly columnType: ColumnType;
    /** The Selection, in the order its ranges were made. */
    readonly selection: readonly SelectionRange[];
    /** The Row Sequence Version the Selection's positions are written in: the source's own, which its `snapshot()`
     * says and `placeSelection` takes (ADR-0011, ADR-0050). */
    readonly rowSequenceVersion: number;
    /** The core's own commands, which the menu shows first: the clipboard's, since only the grid can build
     * that payload under the copy's rules (ADR-0005). */
    readonly commands: readonly GridCommand[];
}
