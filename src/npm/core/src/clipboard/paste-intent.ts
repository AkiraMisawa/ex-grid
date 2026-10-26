// The one Edit Intent a paste raises (ADR-0007, ADR-0014): the approved plan, the block's values, and the
// Row Sequence Version the target was made under. One notification carrying every cell, never one per cell,
// so a bulk paste is one intent, and one undo.

import { itemAt } from '../must';
import { rangeCellCount } from '../selection/range';
import type { CellPosition } from '../selection/cell';
import type { SelectionRange } from '../selection/range';
import type { PasteFieldOrigin } from './clipboard-parse';
import type { PastePlan } from './clipboard-rules';

/**
 * A paste the grid approved, for the Consumer to write or refuse (ADR-0007, ADR-0014).
 *
 * Positional, like the Selection: the Consumer resolves "rows N to M of the current order, this column"
 * itself, and rows off screen or not yet fetched are included. A version that no longer matches the
 * Consumer's own means the order moved under the intent, which is then discarded rather than applied to
 * other rows (ADR-0011).
 *
 * The grid writes nothing: the screen changes when the source hands over new row instances. A Consumer
 * that will not write it calls {@link GridPasteIntent.refuse} before its `onPaste` returns, and the
 * Selection stays where it was (ADR-0050, item 3).
 */
export class GridPasteIntent {
    /** The approved plan: the target ranges, and how the block tiles onto them (ADR-0014). */
    readonly plan: PastePlan;
    /** The block, `plan.source.rows` × `plan.source.columns`: raw strings, which the Consumer parses by
     * its own column types. Excel's HTML flavour supplies full precision where it was on the clipboard
     * (ADR-0005). */
    readonly values: readonly (readonly string[])[];
    /** Where each field of `values` came from, at the same position (ADR-0050, item 10): `invariant` for
     * Excel's `x:num` and the family's own unformatted HTML, read under the invariant culture; `shownText`
     * for everything else, read as typed. */
    readonly origins: readonly (readonly PasteFieldOrigin[])[];
    /** The order these positions are written in (ADR-0011): the source's own Row Sequence Version, the one its
     * `snapshot()` says and `placeSelection` takes. */
    readonly rowSequenceVersion: number;
    /** The cell the text was typed in, for a Ctrl+Enter fill from the Cell Editor (ADR-0007, ADR-0050 item 5);
     * `undefined` for a paste from the clipboard, whose text was typed nowhere in this grid. */
    readonly enteredAt: CellPosition | undefined;
    /** The range a fill key read `values` from (ADR-0035): Ctrl+D's top row, Ctrl+R's left column, or the row
     * above (column to the left) of a range one cell deep, in positions of the same order as `plan`.
     * `undefined` for a paste from the clipboard and for Ctrl+Enter's typed text. A Consumer that holds more
     * than values, such as a Sheet's formulas, copies from this range rather than parse `values`, as Excel's
     * fill keys do (ADR-0050, item 5). */
    readonly fillSource: SelectionRange | undefined;
    readonly #marks: readonly (readonly (string | undefined)[])[] | undefined;
    #refused = false;
    #sealed = false;

    /** @internal */
    constructor(
        plan: PastePlan, values: readonly (readonly string[])[], origins: readonly (readonly PasteFieldOrigin[])[],
        rowSequenceVersion: number, marks: readonly (readonly (string | undefined)[])[] | undefined, enteredAt?: CellPosition,
        fillSource?: SelectionRange,
    ) {
        this.plan = plan;
        this.values = values;
        this.origins = origins;
        this.rowSequenceVersion = rowSequenceVersion;
        this.#marks = marks;
        this.enteredAt = enteredAt;
        this.fillSource = fillSource;
    }

    /** Whether the Consumer refused the paste. */
    get refused(): boolean {
        return this.#refused;
    }

    /** The Consumer's answer that it does not write this paste (ADR-0050, item 3). Called before `onPaste`
     * returns, it leaves the Selection where it was; telling the user why is the Consumer's, since the grid
     * knows only that it was refused. Called after, it throws: the grid has already taken the paste as
     * written, and a refusal it never heard would leave the Selection saying otherwise. */
    refuse(): void {
        if (this.#sealed) {
            throw new Error('This paste was answered when onPaste returned: refuse it before then (ADR-0050, item 3).');
        }
        this.#refused = true;
    }

    /** @internal Ends the time the Consumer may refuse it in. */
    seal(): void {
        this.#sealed = true;
    }

    /** How many cells the intent covers: the sum of the target areas (ADR-0014). */
    get cellCount(): number {
        return this.plan.targets.reduce((sum, target) => sum + rangeCellCount(target), 0);
    }

    /** The value that lands on one target cell, through the plan's tiling. */
    valueFor(target: CellPosition): string {
        const { row, column } = this.plan.sourceCellFor(target);
        return itemAt(itemAt(this.values, row), column);
    }

    /** Where the value that lands on one target cell came from (ADR-0050, item 10). */
    originFor(target: CellPosition): PasteFieldOrigin {
        const { row, column } = this.plan.sourceCellFor(target);
        return itemAt(itemAt(this.origins, row), column);
    }

    /** The mark the value that lands on one target cell carried in a table of the family's own HTML, as
     * written; `undefined` for a field that carried none, and for every field of another program's
     * clipboard. The grid gives it no meaning; the Consumer that wrote it reads it. */
    markFor(target: CellPosition): string | undefined {
        if (this.#marks === undefined) return undefined;
        const { row, column } = this.plan.sourceCellFor(target);
        return itemAt(this.#marks, row)[column];
    }
}
