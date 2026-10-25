// The incremental requery of a live in-memory source (ADR-0141), ported from C#'s LiveRequery: ag-grid's
// deltaSort on this engine's semantics.
//
// Each row carries an ordinal that grows with its place in the source's order: a changed row keeps its own,
// an added row takes the next. The result is ordered by (sort keys, ordinal), a strict total order, and that
// is exactly applyQuery's order: its sort is stable, so ties fall in the source's order (ADR-0023). So the
// rows that left (removed rows, and the old versions of changed ones) are found in the previous result by
// binary search on their own keys, which they still have, being immutable (ADR-0003); the rows that entered
// are filtered and sorted by the engine itself, handed over in ordinal order so its stable sort breaks
// their ties as the whole requery would; and each is placed among the previous result by binary search,
// and the two are merged block by block.
//
// Whether the sequence moved is answered from the changes alone: it moved when a row left or entered the
// result, or a changed row stands at another position than its old version stood at. With every change at
// its old position and nothing in or out, every other row keeps its position too.
//
// A whole new list may put the rows themselves in another order (D7). They then take new ordinals, which the
// previous result's cannot be compared with, and a row that did not change can move: the result is
// requeried whole, and the two results are compared position by position.
//
// Its result equals applyQuery over the rows held now exactly, the stable tie order included; a property
// test holds it to that (LV-5).

import { elementAt, itemAt } from '../must';
import type { QueryColumn } from '../query/columns';
import type { GridFilter } from '../query/filter';
import { filterPredicate, queryPositions, rowOrder, type RowOrder, type SortKeys } from '../query/query-engine';
import type { SortSpec } from '../query/sort';
import type { OrderedRows } from './keyed-rows';
import type { RowKey } from './row-key';

/** One row's change since the result was last computed, net of everything gathered meanwhile: the version
 * the result was computed from and the version held now, each with its ordinal. A row added has no old
 * version, a row removed no new one, and a key removed and added again has both, at different ordinals. */
export interface RowChange<Row> {
    readonly key: RowKey;
    readonly old: { readonly row: Row; readonly ordinal: number } | undefined;
    readonly new: { readonly row: Row; readonly ordinal: number } | undefined;
}

/** What the requery made: the new result and each row's ordinal beside it (the same arrays as before when no
 * change reached the result), where each change's old version stood in the previous result and its new
 * version stands in this one (-1 for none), and whether the result's sequence moved anywhere. */
export interface LiveRequeryOutcome<Row> {
    readonly result: readonly Row[];
    readonly ordinals: readonly number[];
    readonly oldIndex: readonly number[];
    readonly newIndex: readonly number[];
    readonly moved: boolean;
}

/** What the requery reads besides the changes. */
export interface LiveQuery<Row> {
    readonly columns: readonly QueryColumn<Row>[];
    readonly filter: GridFilter | undefined;
    readonly sorts: readonly SortSpec[];
}

/** Past this share of the rows held, a batch is requeried whole: C#'s measured crossing (M4), where the two
 * cost about the same; the result is the same either way. */
const wholeRequeryShare = 6;

/**
 * The result after `changes`, from `previous` (the result before them, under the same Query) and its
 * ordinals. `count` is how many rows are held now, which decides a whole requery; `whole` reads them, only
 * when one is made. `reordered` says the rows were put in another order since `previous` was computed.
 */
export function liveRequery<Row>(
    previous: readonly Row[],
    previousOrdinals: readonly number[],
    changes: readonly RowChange<Row>[],
    query: LiveQuery<Row>,
    count: number,
    whole: () => OrderedRows<Row>,
    reordered: boolean,
): LiveRequeryOutcome<Row> {
    if (reordered) return requeriedAfterReorder(previous, changes, query, whole);
    const { columns, filter, sorts } = query;
    const order = rowOrder(columns, sorts);
    const passes = filter === undefined ? undefined : filterPredicate(columns, filter);

    // 1. Where each old version stood in the previous result, if it stood there at all.
    const oldIndex = changes.map(() => -1);
    let touchesResult = false;
    let requeryWhole = changes.length * wholeRequeryShare > count;
    changes.forEach((change, j) => {
        if (change.old === undefined) return;
        const at = lowerBound(previous, previousOrdinals, 0, order, order.keysOf(change.old.row), change.old.ordinal);
        if (at < previous.length && previous[at] === change.old.row) {
            oldIndex[j] = at;
            touchesResult = true;
        } else if (passes === undefined || passes(change.old.row)) {
            // An old version that passes the Filter was in the previous result, so a search that does not
            // find it means its sort keys are no longer what they were: the row was changed in place
            // (ADR-0003). The search over everything else could be off as well, so the rows are requeried
            // whole, which reads them as they are.
            requeryWhole = true;
            touchesResult = true;
        }
    });

    // 2. The new versions, filtered and sorted by the engine itself, handed over in ordinal order so its
    // stable sort breaks their ties as the whole requery would.
    const entering = changes.flatMap((change, j) => change.new === undefined ? [] : [{ j, ordinal: change.new.ordinal, row: change.new.row }])
        .toSorted((a, b) => a.ordinal - b.ordinal);
    const passed = queryPositions(entering.map(each => each.row), columns, filter, sorts);
    touchesResult ||= passed.length > 0;

    const newIndex = changes.map(() => -1);
    if (!touchesResult) {
        // No change reached the result: it is the previous one, the same arrays, and the grid has nothing
        // new to paint (ADR-0023).
        return { result: previous, ordinals: previousOrdinals, oldIndex, newIndex, moved: false };
    }

    let result: readonly Row[];
    let ordinals: readonly number[];
    if (requeryWhole) {
        ({ result, ordinals } = requeried(whole(), query));
        for (const at of passed) {
            const { j, row, ordinal } = elementAt(entering, at);
            const found = lowerBound(result, ordinals, 0, order, order.keysOf(row), ordinal);
            if (found < result.length && result[found] === row) newIndex[j] = found;
        }
    } else {
        ({ result, ordinals } = merged(previous, previousOrdinals, oldIndex, entering, passed, order, newIndex));
    }

    // 3. Whether the sequence moved, from the changes alone.
    const moved = oldIndex.some((at, j) => at !== newIndex[j]);
    return { result, ordinals, oldIndex, newIndex, moved };
}

function requeried<Row>(rows: OrderedRows<Row>, { columns, filter, sorts }: LiveQuery<Row>): { result: Row[]; ordinals: number[] } {
    const positions = queryPositions(rows.rows, columns, filter, sorts);
    return {
        result: positions.map(at => itemAt(rows.rows, at)),
        ordinals: positions.map(at => elementAt(rows.ordinals, at)),
    };
}

/**
 * The result after the rows were put in another order (D7), requeried whole by the engine itself. The
 * ordinals of `previous` and of the old versions are of the order before, so nothing is placed by them:
 * each old version is found in the previous result, and each new one in this, by identity. Whether the
 * sequence moved is answered by comparing the two results position by position: rows that kept their keys
 * and their places did not move it, and a row and its new version under one key are the same row. When the
 * result is the previous one row for row, it is kept, with the new ordinals beside it.
 */
function requeriedAfterReorder<Row>(
    previous: readonly Row[], changes: readonly RowChange<Row>[], query: LiveQuery<Row>, whole: () => OrderedRows<Row>,
): LiveRequeryOutcome<Row> {
    const { result, ordinals } = requeried(whole(), query);
    const previousAt = indexOf(previous);
    const resultAt = indexOf(result);
    // A changed row's old version, and the new one under its key.
    const newVersionOf = new Map<Row, Row>();
    const oldIndex = changes.map(change => change.old === undefined ? -1 : positionIn(previousAt, change.old.row));
    const newIndex = changes.map(change => change.new === undefined ? -1 : positionIn(resultAt, change.new.row));
    for (const change of changes) {
        if (change.old !== undefined && change.new !== undefined && !newVersionOf.has(change.old.row)) {
            newVersionOf.set(change.old.row, change.new.row);
        }
    }
    let moved = previous.length !== result.length;
    let same = !moved;
    for (let i = 0; i < result.length && !moved; i++) {
        const was = itemAt(previous, i);
        const now = itemAt(result, i);
        if (was === now) continue;
        same = false;
        moved = newVersionOf.get(was) !== now;
    }
    return { result: same ? previous : result, ordinals, oldIndex, newIndex, moved };
}

function indexOf<Row>(rows: readonly Row[]): Map<Row, number> {
    const at = new Map<Row, number>();
    rows.forEach((row, index) => { if (!at.has(row)) at.set(row, index); });
    return at;
}

/** Where a row stands, or -1 where it does not: "none" is part of what an index answers here. */
function positionIn<Row>(at: ReadonlyMap<Row, number>, row: Row): number {
    const position = at.get(row);
    return position === undefined ? -1 : position;
}

function merged<Row>(
    previous: readonly Row[],
    previousOrdinals: readonly number[],
    oldIndex: readonly number[],
    entering: readonly { readonly j: number; readonly ordinal: number; readonly row: Row }[],
    passed: readonly number[],
    order: RowOrder<Row>,
    newIndex: number[],
): { result: Row[]; ordinals: number[] } {
    const left = new Set(oldIndex.filter(at => at >= 0));
    // Where each entering row goes among the previous result: monotone, because they are sorted.
    let lower = 0;
    const insertAt = passed.map(at => {
        const { row, ordinal } = elementAt(entering, at);
        lower = lowerBound(previous, previousOrdinals, lower, order, order.keysOf(row), ordinal);
        return lower;
    });

    const result: Row[] = [];
    const ordinals: number[] = [];
    let source = 0;
    for (let i = 0; i <= passed.length; i++) {
        const until = i < passed.length ? elementAt(insertAt, i) : previous.length;
        // previous[source, until), without the rows that left.
        for (; source < until; source++) {
            if (left.has(source)) continue;
            result.push(itemAt(previous, source));
            ordinals.push(elementAt(previousOrdinals, source));
        }
        if (i < passed.length) {
            const { j, row, ordinal } = elementAt(entering, elementAt(passed, i));
            newIndex[j] = result.length;
            result.push(row);
            ordinals.push(ordinal);
        }
    }
    const expected = previous.length - left.size + passed.length;
    if (result.length !== expected) {
        throw new Error(`Internal: the incremental requery wrote ${result.length.toString()} of ${expected.toString()} rows (ADR-0141).`);
    }
    return { result, ordinals };
}

/** The first position in `rows` from `from` whose (keys, ordinal) is not before the probe's. */
function lowerBound<Row>(
    rows: readonly Row[], ordinals: readonly number[], from: number, order: RowOrder<Row>, keys: SortKeys, ordinal: number,
): number {
    let low = from;
    let high = rows.length;
    while (low < high) {
        const middle = (low + high) >>> 1;
        let compared = order.compare(itemAt(rows, middle), keys);
        if (compared === 0) compared = elementAt(ordinals, middle) - ordinal;
        if (compared < 0) low = middle + 1;
        else high = middle;
    }
    return low;
}
