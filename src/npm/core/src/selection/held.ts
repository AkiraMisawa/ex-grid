import { Selection } from './selection';

/**
 * A selection paired with the Row Sequence Version it was made under. Its positions are positions in
 * the current order, so when the version changes the selection is dropped, never remapped (ADR-0011),
 * and this pairing is where that rule lives, rather than as a convention every holder repeats.
 */
export class HeldSelection {
    /** The Row Sequence Version the selection was made under. */
    readonly rowSequenceVersion: number;
    /** The selection, in positions of that version's order. */
    readonly selection: Selection;

    private constructor(rowSequenceVersion: number, selection: Selection) {
        this.rowSequenceVersion = rowSequenceVersion;
        this.selection = selection;
    }

    /** Nothing selected, held under the version: where a holder starts. */
    static empty(rowSequenceVersion: number): HeldSelection {
        return new HeldSelection(rowSequenceVersion, Selection.empty);
    }

    /**
     * Reconciles with the version now current: the same version keeps the selection, any other drops it.
     * There is no "newer": versions only name orders, so any difference means the positions no longer
     * name the rows the user selected (ADR-0011).
     */
    under(currentRowSequenceVersion: number): HeldSelection {
        return currentRowSequenceVersion === this.rowSequenceVersion ? this : HeldSelection.empty(currentRowSequenceVersion);
    }

    /** `selection`, held under `rowSequenceVersion`: a view handed back, taken with the version it is shown under. */
    static of(rowSequenceVersion: number, selection: Selection): HeldSelection {
        return new HeldSelection(rowSequenceVersion, selection);
    }

    /** Replaces the selection under the same version: the ordinary result of a gesture. */
    with(selection: Selection): HeldSelection {
        return new HeldSelection(this.rowSequenceVersion, selection);
    }
}
