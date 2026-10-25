// Which groups a question's levels show (ADR-0370 section 3), and the check of a level answer against them.
// The C# engine's PivotLevelAnswers.Mismatch and the helpers it shares with the bundled sources' level answer
// (level-answers.ts).
//
// A group named by Items x₀ … x₍d−1₎ is shown when x₀ … x₍d−2₎ are all expanded: the outermost field's groups
// and the grand total always are. Along a leaf the shown groups are those down to a depth, the first Item that
// is collapsed being the last one named, so a cell is a shown row group crossed with a shown column group.

import type { PivotAnswer, PivotAnswerAxis } from '../cube/answer';
import type { PivotLevelRule, PivotLevels } from '../cube/query';
import { itemKeyText, publicKeyId } from '../items/item-key';
import { elementAt } from '../must';

/** Each level's Items: whether the groups below each are shown. */
export function expandedItems(rules: readonly PivotLevelRule[], axes: readonly PivotAnswerAxis[]): readonly (readonly boolean[])[] {
    return axes.map((axis, level) => {
        const rule = elementAt(rules, level);
        // Each Item is expanded when its field's Items are not collapsed, or it is toggled from that.
        const toggled = new Set(rule.toggledItems.map(publicKeyId));
        return axis.items.map(item => rule.collapsed === toggled.has(publicKeyId(item)));
    });
}

// How deep a cell is named along one axis.
function depthOf(axes: readonly PivotAnswerAxis[], cell: number): number {
    let depth = 0;
    while (depth < axes.length && elementAt(axes, depth).itemAt(cell) >= 0) depth++;
    return depth;
}

// Whether the group a cell names down to `depth` along one axis is shown: every Item above its own is expanded.
function isShown(axes: readonly PivotAnswerAxis[], expanded: readonly (readonly boolean[])[], cell: number, depth: number): boolean {
    for (let level = 0; level < depth - 1; level++) {
        if (!elementAt(elementAt(expanded, level), elementAt(axes, level).itemAt(cell))) return false;
    }
    return true;
}

/** The deepest group shown along a leaf on one axis: 0 where the axis has no field. */
export function deepestShown(axes: readonly PivotAnswerAxis[], expanded: readonly (readonly boolean[])[], leaf: number): number {
    if (axes.length === 0) return 0;
    let depth = 1;
    while (depth < axes.length && elementAt(elementAt(expanded, depth - 1), elementAt(axes, depth - 1).itemAt(leaf))) depth++;
    return depth;
}

/** A cell's Item index at every level, −1 below its depth on either axis: the leaf `cell` of `rows` and
 * `columns` named down to `rowDepth` and `columnDepth`. */
export function cellPath(rows: readonly PivotAnswerAxis[], columns: readonly PivotAnswerAxis[], cell: number, rowDepth: number, columnDepth: number): number[] {
    return [
        ...rows.map((axis, level) => (level < rowDepth ? axis.itemAt(cell) : -1)),
        ...columns.map((axis, level) => (level < columnDepth ? axis.itemAt(cell) : -1)),
    ];
}

/** A path as a Map key. */
export function pathKey(path: readonly number[]): string {
    return path.join(',');
}

// The path with the Item at one level taken away, as a key: the group above along that axis.
function aboveKey(path: readonly number[], level: number): string {
    const above = [...path];
    above[level] = -1;
    return pathKey(above);
}

// [Region=Text:East] × [] — a cell's Items, for a refusal.
function describe(answer: PivotAnswer, cell: number): string {
    const path = (axes: readonly PivotAnswerAxis[]): string => {
        const named = axes.slice(0, depthOf(axes, cell));
        return `[${named.map(axis => `${axis.field}=${itemKeyText(elementAt(axis.items, axis.itemAt(cell)))}`).join(', ')}]`;
    };
    return `${path(answer.rows)} × ${path(answer.columns)}`;
}

/**
 * Why a level answer does not answer the levels asked, or `undefined`: a cell the rules do not show, two cells
 * for one group, or a cell whose parent on either axis is not in the answer. Its shape — each cell named down
 * to its depth — the answer checked as it was made.
 */
export function levelAnswerMismatch(levels: PivotLevels, answer: PivotAnswer): string | undefined {
    const rows = answer.rows;
    const columns = answer.columns;
    const rowExpanded = expandedItems(levels.rows, rows);
    const columnExpanded = expandedItems(levels.columns, columns);
    const cells = new Set<string>();
    const paths: number[][] = [];
    for (let cell = 0; cell < answer.leafCount; cell++) {
        const rowDepth = depthOf(rows, cell);
        const columnDepth = depthOf(columns, cell);
        if (!isShown(rows, rowExpanded, cell, rowDepth) || !isShown(columns, columnExpanded, cell, columnDepth)) {
            return `its cell ${describe(answer, cell)} is a group the question's levels do not show`;
        }
        const path = cellPath(rows, columns, cell, rowDepth, columnDepth);
        const key = pathKey(path);
        if (cells.has(key)) return `it has two cells for ${describe(answer, cell)}`;
        cells.add(key);
        paths.push(path);
    }
    for (let cell = 0; cell < answer.leafCount; cell++) {
        const path = elementAt(paths, cell);
        const rowDepth = depthOf(rows, cell);
        const columnDepth = depthOf(columns, cell);
        if (rowDepth > 0 && !cells.has(aboveKey(path, rowDepth - 1))) {
            return `its cell ${describe(answer, cell)} is shown without its parent row group`;
        }
        if (columnDepth > 0 && !cells.has(aboveKey(path, rows.length + columnDepth - 1))) {
            return `its cell ${describe(answer, cell)} is shown without its parent column group`;
        }
    }
    return undefined;
}
