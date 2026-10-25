// The Copied Range (ADR-0170): what the grid copied, outlined with dashes for as long as the clipboard still
// holds it. A copy is made ready to outline with its flavours and kept only once its write has landed; what
// is here is the copy as the clipboard holds it and the test that keeps it. The other ends — a write by
// anyone but this grid, Escape, another copy landing — are the grid's to hear.

import { textFor } from '../cells/display-text';
import type { CopyPlan } from '../clipboard/clipboard-rules';
import { itemAt } from '../must';
import { bottomRow, type SelectionRange } from '../selection/range';
import type { ResolvedColumn } from './grid-column';

/**
 * A copy as the clipboard holds it: its rectangles, the Row Sequence Version and the column names they were
 * written under, and a fingerprint per copied row and rectangle — the painted text of its copied cells as
 * one number, 0 for a row the grid did not have when it copied, which is never compared.
 */
export class CopiedRange<Row> {
    readonly rectangles: readonly SelectionRange[];
    readonly version: number;
    readonly columnNames: readonly string[];
    readonly #fingerprints: readonly Int32Array[];
    // The instance last read at each copied position, and the columns it was read in: a row that is the same
    // instance under the same columns is not read again. First the rows it was copied from, then the Window's.
    #checked: ReadonlyMap<number, Row>;
    #checkedColumns: readonly ResolvedColumn<Row>[];
    // The Window last checked, by identity: a pass that moved neither it nor the columns reads nothing.
    #checkedWindow: readonly Row[] | undefined;
    #checkedStart = -1;

    private constructor(
        rectangles: readonly SelectionRange[], version: number, columns: readonly ResolvedColumn<Row>[],
        fingerprints: readonly Int32Array[], copiedFrom: ReadonlyMap<number, Row>,
    ) {
        this.rectangles = rectangles;
        this.version = version;
        this.columnNames = Object.freeze(columns.map(({ column }) => column.name));
        this.#fingerprints = fingerprints;
        this.#checked = copiedFrom;
        this.#checkedColumns = columns;
    }

    /** A copy of `plan`, made as it is written: `rowAt` answers the rows the grid has, `undefined` for one it
     * does not. */
    static of<Row>(
        plan: CopyPlan, version: number, columns: readonly ResolvedColumn<Row>[], rowAt: (row: number) => Row | undefined,
    ): CopiedRange<Row> {
        const rectangles = Object.freeze([...plan.segments]);
        const copiedFrom = new Map<number, Row>();
        const fingerprints = rectangles.map(rectangle => {
            const prints = new Int32Array(rectangle.rowCount);
            for (let i = 0; i < prints.length; i++) {
                const row = rowAt(rectangle.topRow + i);
                if (row === undefined) continue;
                prints[i] = fingerprint(columns, row, rectangle);
                copiedFrom.set(rectangle.topRow + i, row);
            }
            return prints;
        });
        return new CopiedRange(rectangles, version, columns, fingerprints, copiedFrom);
    }

    /**
     * Whether it still marks what was copied, read against the Window: false when a copied row the Window
     * holds paints other text in its copied cells than it was copied with (the painted text, not the row
     * object). A row is read again only when it is another instance than the one last read, or the columns
     * are another list.
     */
    holds(columns: readonly ResolvedColumn<Row>[], window: readonly Row[], windowStart: number): boolean {
        const sameColumns = this.#checkedColumns === columns;
        if (sameColumns && this.#checkedWindow === window && this.#checkedStart === windowStart) return true;
        const checked = new Map<number, Row>();
        for (let i = 0; i < window.length; i++) {
            const position = windowStart + i;
            const row = itemAt(window, i);
            const reread = !sameColumns || this.#checked.get(position) !== row;
            let inside = false;
            for (let k = 0; k < this.rectangles.length; k++) {
                const rectangle = itemAt(this.rectangles, k);
                if (position < rectangle.topRow || position > bottomRow(rectangle)) continue;
                inside = true;
                const print = itemAt(this.#fingerprints, k)[position - rectangle.topRow];
                if (reread && print !== 0 && print !== fingerprint(columns, row, rectangle)) return false;
            }
            if (inside) checked.set(position, row);
        }
        this.#checked = checked;
        this.#checkedColumns = columns;
        this.#checkedWindow = window;
        this.#checkedStart = windowStart;
        return true;
    }
}

// The painted text of a row's cells across a rectangle's columns as one number (FNV-1a over each text and
// its length): never 0, which stands for a row the grid did not have.
function fingerprint<Row>(columns: readonly ResolvedColumn<Row>[], row: Row, rectangle: SelectionRange): number {
    let hash = 0x811c9dc5;
    const mix = (unit: number): void => {
        hash = Math.imul(hash ^ unit, 0x01000193);
    };
    for (let c = rectangle.leftColumn; c < rectangle.leftColumn + rectangle.columnCount; c++) {
        const { column } = itemAt(columns, c);
        const text = textFor(column, column.value(row));
        mix(text.length);
        for (let i = 0; i < text.length; i++) mix(text.charCodeAt(i));
    }
    return hash === 0 ? 1 : hash | 0;
}
