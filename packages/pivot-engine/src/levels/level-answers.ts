// The bundled sources' level answer (ADR-0370 section 3): for a question with levels whose leaves pass its cap,
// a pass over every leaf there is, each sum with its magnitudes beside it, merged into the cells shown. The C#
// engine's PivotLevelAnswers.AnswerAsync and FromLeavesAsync.
//
// Each leaf is merged, in the leaves' order, into every shown cell along it, as the cube merges a leaf into its
// totals; the cells are numbered as the leaves first reach them, row groups deepest first and, within one,
// column groups deepest first. The same arithmetic in the same order is what makes this answer the C#
// engine's, bit for bit.
//
// Under a question's window, the cells are merged once to order each sibling set past the limit by its value
// — among ties, by the order the leaves first reached them — and merged again with the siblings beyond each
// margin merged into rest cells instead, a leaf at a time in the leaves' order, so a rest's parts are the
// merge of its leaves as a cell's are.

import type { Snapshot } from '@ex-grid/data';
import { PartColumns } from '../aggregates/parts';
import { PivotAnswer, PivotAnswerAxis, PivotRefusals, answerColumns, answerValues, madeAnswer, type PivotAnswerWindow } from '../cube/answer';
import type { PivotLevelWindow, PivotLevels, PivotQuery } from '../cube/query';
import { publicKeyId, type PivotItemKey } from '../items/item-key';
import { defined, elementAt } from '../must';
import { AggregationPass, chunkRows } from '../source/aggregation-pass';
import type { FieldBinding } from '../source/binding';
import { inPieces, pieceUnits } from '../source/slicing';
import { cellPath, deepestShown, expandedItems, pathKey } from './level-shown';

/** The level answer to `query` over `snapshot`, a step at a time (PV-40): a pass that is never kept — a Change
 * Batch is not folded into it — and the cells merged from its leaves. */
export function* levelAnswerSteps(snapshot: Snapshot, bindings: ReadonlyMap<string, FieldBinding>, query: PivotQuery, sourceVersion: string): Generator<void, PivotAnswer> {
    const pass = new AggregationPass(snapshot, bindings, query, false, true);
    for (let at = 0; at < pass.rowCount;) {
        const to = Math.min(pass.rowCount, at + chunkRows);
        if (!pass.step(to)) throw new Error('Internal: the pass a level answer is merged from has no cap to pass.');
        at = to;
        yield;
    }
    const leaves = yield* pass.answerSteps(sourceVersion);
    return yield* levelsFromLeavesSteps(query, leaves);
}

// Where a dropped sibling's leaves go: its window, by its provisional place, and the side.
interface Dropped {
    readonly window: number;
    readonly after: boolean;
}

// A window as the first merge plans it: its parent's path in the leaves' Items, its counts, and its dropped
// siblings by their Item.
interface PlannedWindow {
    readonly parent: readonly number[];
    readonly before: number;
    readonly after: number;
    readonly dropped: ReadonlyMap<number, Dropped>;
}

// The cells merged from the leaves: each one's path in the leaves' Items, its rest code, its records and parts.
interface Merged {
    readonly paths: readonly (readonly number[])[];
    readonly rests: readonly number[];
    readonly records: readonly number[];
    readonly values: readonly PartColumns[];
}

/** The level answer to `query` merged from `leaves`, every leaf there is, each sum with its magnitudes beside
 * it; or, past the question's cap on cells, the refusal for it. A step at a time, as the cube is made. */
export function* levelsFromLeavesSteps(query: PivotQuery, leaves: PivotAnswer): Generator<void, PivotAnswer> {
    const levels = defined(query.levels, 'a level answer answers a question with levels');
    const window = levels.window;
    if (window === undefined || !levels.rows.some(rule => rule.order !== undefined)) {
        const whole = yield* mergeSteps(query, levels, leaves, undefined, false);
        return whole === undefined ? PivotAnswer.refused(PivotRefusals.tooManyLeaves(query.maxLeaves)) : answerOf(leaves, whole, []);
    }
    // The row groups at the grand total of the columns, every one, are what orders each set.
    const rowGroups = defined(yield* mergeSteps(query, levels, leaves, undefined, true), 'an uncapped merge is never refused');
    const planned = planWindows(levels, window, leaves, rowGroups);
    const cut = yield* mergeSteps(query, levels, leaves, planned.length === 0 ? undefined : windowTree(planned), false);
    if (cut === undefined) return PivotAnswer.refused(PivotRefusals.tooManyLeaves(query.maxLeaves));
    return answerOf(leaves, cut, planned);
}

// The planned windows by their parents' Items, level by level, so a leaf finds the window over each of its row
// groups by its Items alone.
interface WindowTree {
    window: PlannedWindow | undefined;
    readonly children: Map<number, WindowTree>;
}

function windowTree(planned: readonly PlannedWindow[]): WindowTree {
    const root: WindowTree = { window: undefined, children: new Map() };
    for (const window of planned) {
        let node = root;
        for (const item of window.parent) {
            let child = node.children.get(item);
            if (child === undefined) {
                child = { window: undefined, children: new Map() };
                node.children.set(item, child);
            }
            node = child;
        }
        node.window = window;
    }
    return root;
}

// Merges every leaf into the cells shown along it; a leaf under a dropped sibling of a window is merged into that
// window's rest cell instead, once a column group. Undefined past the cap; with `columnRootOnly`, only the cells at
// the grand total of the columns, and never capped, for ordering each set before its window is cut.
function* mergeSteps(query: PivotQuery, levels: PivotLevels, leaves: PivotAnswer, windows: WindowTree | undefined, columnRootOnly: boolean): Generator<void, Merged | undefined> {
    const capped = !columnRootOnly;
    const rows = leaves.rows;
    const columns = leaves.columns;
    const rowExpanded = expandedItems(levels.rows, rows);
    const columnExpanded = expandedItems(levels.columns, columns);
    const answered = leaves.values.map(answerColumns);
    const values = answered.map(other => {
        const parts = new PartColumns(other.parts);
        if (other.absolutes !== undefined) parts.trackAbsolutes();
        return parts;
    });
    const index = new Map<string, number>();
    const paths: number[][] = [];
    const rests: number[] = [];
    const records: number[] = [];
    // A leaf merges into as many cells as it has shown groups, each value's parts in each: a step falls about
    // every pieceUnits of them.
    const piece = Math.max(1, Math.floor(pieceUnits / ((rows.length + 1) * (columns.length + 1) * (1 + values.length))));
    for (let leaf = 0; leaf < leaves.leafCount; leaf++) {
        if (leaf > 0 && leaf % piece === 0) yield;
        const rowDeepest = deepestShown(rows, rowExpanded, leaf);
        const columnDeepest = columnRootOnly ? 0 : deepestShown(columns, columnExpanded, leaf);
        // The first level whose sibling along the leaf a window drops, and where its leaves go.
        let droppedAt = rowDeepest;
        let dropped: Dropped | undefined;
        let node = windows;
        for (let level = 0; level < rowDeepest && node !== undefined; level++) {
            const item = elementAt(rows, level).itemAt(leaf);
            const held = node.window?.dropped.get(item);
            if (held !== undefined) {
                droppedAt = level;
                dropped = held;
                break;
            }
            node = node.children.get(item);
        }
        for (let rowDepth = rowDeepest; rowDepth >= 0; rowDepth--) {
            if (dropped !== undefined && rowDepth > droppedAt + 1) continue;
            const rest = dropped !== undefined && rowDepth === droppedAt + 1 ? 2 * dropped.window + (dropped.after ? 1 : 0) : -1;
            for (let columnDepth = columnDeepest; columnDepth >= 0; columnDepth--) {
                const path = cellPath(rows, columns, leaf, rest < 0 ? rowDepth : droppedAt, columnDepth);
                const key = rest < 0 ? pathKey(path) : `${pathKey(path)}|${rest}`;
                let cell = index.get(key);
                if (cell === undefined) {
                    cell = paths.length;
                    if (capped && cell === query.maxLeaves) return undefined;
                    index.set(key, cell);
                    paths.push(path);
                    rests.push(rest);
                    records.push(0);
                    for (const parts of values) parts.addCell();
                }
                records[cell] = elementAt(records, cell) + leaves.recordsAt(leaf);
                for (let v = 0; v < values.length; v++) elementAt(values, v).merge(cell, elementAt(answered, v), leaf);
            }
        }
    }
    for (const parts of values) {
        parts.finish();
        yield* inPieces(paths.length, 1, (from, to) => {
            parts.canonicalize(from, to);
        });
    }
    return { paths, rests, records, values };
}

// The windows of the sibling sets past the limit, outermost level first: a set under a sibling an outer window
// drops is not answered, and has none.
function planWindows(levels: PivotLevels, window: PivotLevelWindow, leaves: PivotAnswer, merged: Merged): PlannedWindow[] {
    const rows = leaves.rows;
    const rowCount = rows.length;
    // Each parent's children at the grand total of the columns, in the order the leaves first reached them.
    const children = new Map<string, number[]>();
    merged.paths.forEach((path, cell) => {
        if (path.slice(rowCount).some(item => item >= 0)) return;
        const depth = rowDepthOf(path, rowCount);
        if (depth === 0) return;
        const parent = path.slice(0, depth - 1).join(',');
        const siblings = children.get(parent);
        if (siblings === undefined) children.set(parent, [cell]);
        else siblings.push(cell);
    });
    const startOf = new Map(window.starts.map(start => [start.rows.map(publicKeyId).join('\u0000'), start.start]));
    const planned: PlannedWindow[] = [];
    const droppedUnder = new Map<string, PlannedWindow>();
    const parents = [...children.keys()].map(key => (key === '' ? [] : key.split(',').map(Number)));
    parents.sort((a, b) => a.length - b.length);
    for (const parent of parents) {
        const depth = parent.length;
        const order = elementAt(levels.rows, depth).order;
        if (order === undefined) continue;
        // A set under a dropped sibling is merged into a rest: it has no window.
        let under = false;
        for (let level = 0; level < depth && !under; level++) {
            under = droppedUnder.get(parent.slice(0, level).join(','))?.dropped.has(elementAt(parent, level)) === true;
        }
        if (under) continue;
        const siblings = defined(children.get(parent.join(',')), 'a parent is one with children');
        if (siblings.length <= window.limit) continue;
        const values = elementAt(merged.values, order.value);
        const keys = new Map(siblings.map(cell => {
            const value = values.read(cell, order.aggregation);
            return [cell, value.kind === 'number' ? value.number : undefined];
        }));
        const keyOf = (cell: number): number | undefined => keys.get(cell);
        const ordered = siblings.toSorted((a, b) => {
            const x = keyOf(a);
            const y = keyOf(b);
            if (x === undefined || y === undefined) return x === undefined ? (y === undefined ? 0 : 1) : -1;
            const compared = x < y ? -1 : x > y ? 1 : 0;
            return order.descending ? -compared : compared;
        });
        const tied = (i: number, j: number): boolean => keyOf(elementAt(ordered, i)) === keyOf(elementAt(ordered, j));
        const count = ordered.length;
        const id = parent.map((item, level) => publicKeyId(elementAt(elementAt(rows, level).items, item))).join('\u0000');
        const start = Math.min(startOf.get(id) ?? 0, count - window.limit); // oxlint-disable-line ex-grid/no-stand-in-default -- a window the question names no start for starts at the first sibling
        let first = start;
        while (first > 0 && tied(first - 1, start)) first--;
        let end = start + window.limit;
        while (end < count && tied(end, start + window.limit - 1)) end++;
        const dropped = new Map<number, Dropped>();
        const provisional = planned.length;
        const itemOf = (cell: number): number => elementAt(elementAt(merged.paths, cell), depth);
        for (let i = 0; i < first - 1; i++) dropped.set(itemOf(elementAt(ordered, i)), { window: provisional, after: false });
        for (let i = end + 1; i < count; i++) dropped.set(itemOf(elementAt(ordered, i)), { window: provisional, after: true });
        const made: PlannedWindow = { parent, before: first, after: count - end, dropped };
        planned.push(made);
        droppedUnder.set(parent.join(','), made);
    }
    return planned;
}

// The answer of merged cells, its Items renumbered to those some cell names, in the leaves' answer's order; its
// windows in the order of their parents' cells at the grand total of the columns.
function answerOf(leaves: PivotAnswer, merged: Merged, planned: readonly PlannedWindow[]): PivotAnswer {
    const rows = leaves.rows;
    const columns = leaves.columns;
    const rowCount = rows.length;
    const count = merged.paths.length;
    const renumberings = [...rows, ...columns].map((axis, level) => {
        const renumbered = Array.from({ length: axis.items.length }, () => -1);
        for (const path of merged.paths) {
            const item = elementAt(path, level);
            if (item >= 0) renumbered[item] = 0;
        }
        const items: PivotItemKey[] = [];
        renumbered.forEach((mark, item) => {
            if (mark < 0) return;
            renumbered[item] = items.length;
            items.push(elementAt(axis.items, item));
        });
        const itemOfCell = merged.paths.map(path => {
            const item = elementAt(path, level);
            return item < 0 ? -1 : elementAt(renumbered, item);
        });
        return { axis: new PivotAnswerAxis(axis.field, items, Object.freeze(itemOfCell)), renumbered };
    });
    const axes = renumberings.map(r => r.axis);
    const answerValuesOf = leaves.values.map((value, v) => answerValues(value.field, elementAt(merged.values, v), count));
    if (planned.length === 0) {
        return madeAnswer(leaves.sourceVersion, axes.slice(0, rowCount), axes.slice(rowCount), count, merged.records, answerValuesOf, true);
    }
    // Each window's parent's cell at the grand total, which places it.
    const cellOf = new Map<string, number>();
    merged.paths.forEach((path, cell) => {
        if (elementAt(merged.rests, cell) < 0 && path.slice(rowCount).every(item => item < 0)) {
            cellOf.set(path.slice(0, rowDepthOf(path, rowCount)).join(','), cell);
        }
    });
    const placeOf = planned.map(w => defined(cellOf.get(w.parent.join(',')), "a window's parent's cell"));
    const order = planned.map((_, w) => w).toSorted((a, b) => elementAt(placeOf, a) - elementAt(placeOf, b));
    const finalOf = Array.from({ length: planned.length }, () => 0);
    order.forEach((w, place) => {
        finalOf[w] = place;
    });
    const windows: PivotAnswerWindow[] = order.map(w => {
        const made = elementAt(planned, w);
        const parent = made.parent.map((item, level) => elementAt(elementAt(renumberings, level).renumbered, item));
        return Object.freeze({ parent: Object.freeze(parent), before: made.before, after: made.after });
    });
    const rests = merged.rests.map(code => (code < 0 ? -1 : 2 * elementAt(finalOf, Math.floor(code / 2)) + (code % 2)));
    return madeAnswer(leaves.sourceVersion, axes.slice(0, rowCount), axes.slice(rowCount), count, merged.records, answerValuesOf, true,
        Object.freeze({ windows: Object.freeze(windows), rests: Object.freeze(rests) }));
}

// How deep a cell's path is named along the rows.
function rowDepthOf(path: readonly number[], rowCount: number): number {
    let depth = 0;
    while (depth < rowCount && elementAt(path, depth) >= 0) depth++;
    return depth;
}
