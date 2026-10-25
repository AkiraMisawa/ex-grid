// The Clear Intent Delete raises (ADR-0054): the selected positions should hold no value. It carries no value
// at all, which is what separates it from a paste of empty text: on an amount column "" is a parse failure or
// a zero, and a cleared cell is neither. The Consumer maps it onto its own notion of Blank, per column.

import { rangeCellCount, type SelectionRange } from '../selection/range';

/**
 * Delete's one notification for the whole Selection (ADR-0054), judged by the paste's gate first: a Selection
 * covering a column that is not Editable is refused whole, and raises nothing.
 *
 * Positional, like a paste: the Consumer resolves "rows N to M of the current order, this column" itself,
 * rows off screen or not yet fetched included (ADR-0014). A version that no longer matches the Consumer's own
 * means the order moved under the intent, which is then discarded rather than applied to other rows
 * (ADR-0011). One intent for the whole Selection, so one undo (ADR-0007).
 */
export class GridClearIntent {
    /** The ranges to clear: the Selection as it stood, in creation order. Ranges may overlap; a cell covered
     * twice is cleared once. */
    readonly targets: readonly SelectionRange[];
    /** The order these positions are written in (ADR-0011). */
    readonly rowSequenceVersion: number;

    /** @internal */
    constructor(targets: readonly SelectionRange[], rowSequenceVersion: number) {
        this.targets = targets;
        this.rowSequenceVersion = rowSequenceVersion;
    }

    /** How many cells the intent covers: the sum of the target areas, the figure a status display shows
     * (ADR-0014). */
    get cellCount(): number {
        return this.targets.reduce((sum, target) => sum + rangeCellCount(target), 0);
    }
}
