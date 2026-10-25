// The bundled sources' level answer (ADR-0370 section 3): for a question with levels whose leaves pass its cap,
// a pass over every leaf there is, each sum with its magnitudes beside it, merged into the cells shown. The C#
// engine's PivotLevelAnswers.AnswerAsync and FromLeavesAsync.
//
// Each leaf is merged, in the leaves' order, into every shown cell along it, as the cube merges a leaf into its
// totals; the cells are numbered as the leaves first reach them, row groups deepest first and, within one,
// column groups deepest first. The same arithmetic in the same order is what makes this answer the C#
// engine's, bit for bit.

import type { Snapshot } from '@ex-grid/data';
import { PartColumns } from '../aggregates/parts';
import { PivotAnswer, PivotAnswerAxis, PivotRefusals, answerColumns, answerValues, madeAnswer } from '../cube/answer';
import type { PivotQuery } from '../cube/query';
import type { PivotItemKey } from '../items/item-key';
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

/** The level answer to `query` merged from `leaves`, every leaf there is, each sum with its magnitudes beside
 * it; or, past the question's cap on cells, the refusal for it. A step at a time, as the cube is made. */
export function* levelsFromLeavesSteps(query: PivotQuery, leaves: PivotAnswer): Generator<void, PivotAnswer> {
    const levels = defined(query.levels, 'a level answer answers a question with levels');
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
    const records: number[] = [];
    // A leaf merges into as many cells as it has shown groups, each value's parts in each: a step falls about
    // every pieceUnits of them.
    const piece = Math.max(1, Math.floor(pieceUnits / ((rows.length + 1) * (columns.length + 1) * (1 + values.length))));
    for (let leaf = 0; leaf < leaves.leafCount; leaf++) {
        if (leaf > 0 && leaf % piece === 0) yield;
        const rowDeepest = deepestShown(rows, rowExpanded, leaf);
        const columnDeepest = deepestShown(columns, columnExpanded, leaf);
        for (let rowDepth = rowDeepest; rowDepth >= 0; rowDepth--) {
            for (let columnDepth = columnDeepest; columnDepth >= 0; columnDepth--) {
                const path = cellPath(rows, columns, leaf, rowDepth, columnDepth);
                const key = pathKey(path);
                let cell = index.get(key);
                if (cell === undefined) {
                    cell = paths.length;
                    if (cell === query.maxLeaves) return PivotAnswer.refused(PivotRefusals.tooManyLeaves(query.maxLeaves));
                    index.set(key, cell);
                    paths.push(path);
                    records.push(0);
                    for (const parts of values) parts.addCell();
                }
                records[cell] = elementAt(records, cell) + leaves.recordsAt(leaf);
                for (let v = 0; v < values.length; v++) elementAt(values, v).merge(cell, elementAt(answered, v), leaf);
            }
        }
    }

    const count = paths.length;
    for (const parts of values) {
        parts.finish();
        yield* inPieces(count, 1, (from, to) => {
            parts.canonicalize(from, to);
        });
    }
    const axes = [...rows, ...columns].map((axis, level) => {
        // The Items some cell names, in the leaves' answer's order.
        const renumbered = Array.from({ length: axis.items.length }, () => -1);
        for (const path of paths) {
            const item = elementAt(path, level);
            if (item >= 0) renumbered[item] = 0;
        }
        const items: PivotItemKey[] = [];
        renumbered.forEach((mark, item) => {
            if (mark < 0) return;
            renumbered[item] = items.length;
            items.push(elementAt(axis.items, item));
        });
        const itemOfCell = paths.map(path => {
            const item = elementAt(path, level);
            return item < 0 ? -1 : elementAt(renumbered, item);
        });
        return new PivotAnswerAxis(axis.field, items, Object.freeze(itemOfCell));
    });
    const answerValuesOf = leaves.values.map((value, v) => answerValues(value.field, elementAt(values, v), count));
    return madeAnswer(leaves.sourceVersion, axes.slice(0, rows.length), axes.slice(rows.length), count, records, answerValuesOf, true);
}
