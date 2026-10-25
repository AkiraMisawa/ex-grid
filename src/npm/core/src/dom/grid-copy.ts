// The grid's half of a copy (ADR-0005): which rows an approved plan reads, and the values the two flavours
// are assembled from. The rules that approve or refuse a copy are clipboard-rules.ts's, and the assembly
// clipboard-data.ts's; what is here is where the rows come from. Rows the source's Window holds are read
// from it, in the copy event itself; the rest are asked of the source, and a run that comes back short
// refuses the whole copy, because a fraction of the selection on the clipboard is the quietly smaller total
// ADR-0005 exists to prevent.

import type { CopyPlan } from '../clipboard/clipboard-rules';
import type { CopyValues } from '../clipboard/clipboard-data';
import { rawText, textFor } from '../cells/display-text';
import { itemAt } from '../must';
import { bottomRow } from '../selection/range';
import type { GridSource, GridSourceState } from '../source/grid-source';
import type { ResolvedColumn } from './grid-column';

/** Whether every row the plan reads is in the Window: the test that chooses the copy event's own route
 * over the asynchronous one (ADR-0005). */
export function windowCovers(plan: CopyPlan, state: GridSourceState<unknown>): boolean {
    const end = state.windowStart + state.window.length;
    return plan.segments.every(segment => segment.topRow >= state.windowStart && bottomRow(segment) < end);
}

/** The values of a copy, read from the rows by their position in the result: the text each cell displays
 * before the overflow decision, so `####` never reaches the clipboard (ADR-0016), and the raw value's
 * locale-free text (ADR-0005). */
export function copyValues<Row>(columns: readonly ResolvedColumn<Row>[], rowAt: (row: number) => Row): CopyValues {
    return {
        displayAt: (row, column) => {
            const { column: declared } = itemAt(columns, column);
            return textFor(declared, declared.value(rowAt(row)));
        },
        rawAt: (row, column) => rawText(itemAt(columns, column).column.value(rowAt(row))),
    };
}

/** The Window's row at a position the plan reads, which {@link windowCovers} has answered for. */
export function windowRowAt<Row>(state: GridSourceState<Row>): (row: number) => Row {
    return row => itemAt(state.window, row - state.windowStart);
}

/**
 * The rows the plan reads, by their position in the result: from the Window where it holds a run whole,
 * otherwise asked of the source, every run at once. Side-by-side segments share their rows, which are asked
 * for once. `undefined` when a run came back short, or `moved` says the order or the columns changed while
 * the rows were asked for: either refuses the copy whole.
 */
export async function gatherRows<Row>(
    plan: CopyPlan, state: GridSourceState<Row>, source: GridSource<Row>, signal: AbortSignal, moved: () => boolean,
): Promise<ReadonlyMap<number, Row> | undefined> {
    const runs = plan.orientation === 'horizontal' ? plan.segments.slice(0, 1) : plan.segments;
    const answers = await Promise.all(runs.map(async ({ topRow: start, rowCount: count }) => {
        const held = start >= state.windowStart && start + count <= state.windowStart + state.window.length;
        const rows = held
            ? state.window.slice(start - state.windowStart, start - state.windowStart + count)
            : await source.rows({ start, count }, signal);
        return { start, count, rows };
    }));
    if (moved()) return undefined;
    const rows = new Map<number, Row>();
    for (const { start, count, rows: answered } of answers) {
        if (answered.length < count) return undefined;
        for (let i = 0; i < count; i++) rows.set(start + i, itemAt(answered, i));
    }
    return rows;
}
