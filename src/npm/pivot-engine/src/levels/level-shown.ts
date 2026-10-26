// Which groups a question's levels show (ADR-0370 section 3), and the check of a level answer against them.
// The C# engine's PivotLevelAnswers.Mismatch and the helpers it shares with the bundled sources' level answer
// (level-answers.ts).
//
// A group named by Items x₀ … x₍d−1₎ is shown when x₀ … x₍d−2₎ are all expanded: the outermost field's groups
// and the grand total always are. Along a leaf the shown groups are those down to a depth, the first Item that
// is collapsed being the last one named, so a cell is a shown row group crossed with a shown column group.

import type { PivotAnswer, PivotAnswerAxis, PivotAnswerWindowing } from '../cube/answer';
import type { PivotLevelRule, PivotLevels } from '../cube/query';
import { itemKeyText, publicKeyId, type PivotItemKey } from '../items/item-key';
import { elementAt } from '../must';

/** Whether the groups below an Item of a level are shown, by each level's rule: an Item is expanded when its
 * field's Items are not collapsed, or it is toggled from that. */
export function levelExpander(rules: readonly PivotLevelRule[]): (level: number, item: PivotItemKey) => boolean {
    const toggled = rules.map(rule => new Set(rule.toggledItems.map(publicKeyId)));
    return (level, item) => elementAt(rules, level).collapsed === elementAt(toggled, level).has(publicKeyId(item));
}

/** Each level's Items: whether the groups below each are shown. */
export function expandedItems(rules: readonly PivotLevelRule[], axes: readonly PivotAnswerAxis[]): readonly (readonly boolean[])[] {
    const expanded = levelExpander(rules);
    return axes.map((axis, level) => axis.items.map(item => expanded(level, item)));
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

/** [Region=Text:East] × [] — a cell's Items, for a refusal. */
export function describe(answer: PivotAnswer, cell: number): string {
    const path = (axes: readonly PivotAnswerAxis[]): string => {
        const named = axes.slice(0, depthOf(axes, cell));
        return `[${named.map(axis => `${axis.field}=${itemKeyText(elementAt(axis.items, axis.itemAt(cell)))}`).join(', ')}]`;
    };
    return `${path(answer.rows)} × ${path(answer.columns)}`;
}

/**
 * Why a level answer does not answer the levels asked, or `undefined`: a cell the rules do not show, two cells
 * for one group, or a cell whose parent on either axis is not in the answer; a window the question does not
 * ask for, or one that is not where it was asked; a rest cell without its window's parent, or a side it says
 * has no siblings beyond its margin. Its shape — each cell named down to its depth — the answer checked as it
 * was made.
 */
export function levelAnswerMismatch(levels: PivotLevels, answer: PivotAnswer, windowing: PivotAnswerWindowing): string | undefined {
    const rows = answer.rows;
    const columns = answer.columns;
    const rowExpanded = expandedItems(levels.rows, rows);
    const columnExpanded = expandedItems(levels.columns, columns);
    const isRest = (cell: number): boolean => windowing.windows.length > 0 && elementAt(windowing.rests, cell) >= 0;
    const cells = new Set<string>();
    const rests = new Set<string>();
    const paths: number[][] = [];
    for (let cell = 0; cell < answer.leafCount; cell++) {
        const rowDepth = depthOf(rows, cell);
        const columnDepth = depthOf(columns, cell);
        if (!isShown(rows, rowExpanded, cell, rowDepth) || !isShown(columns, columnExpanded, cell, columnDepth)) {
            return `its cell ${describe(answer, cell)} is a group the question's levels do not show`;
        }
        const path = cellPath(rows, columns, cell, rowDepth, columnDepth);
        paths.push(path);
        if (isRest(cell)) {
            const restKey = `${pathKey(path)}|${elementAt(windowing.rests, cell)}`;
            if (rests.has(restKey)) return `it has two rest cells for one side of a window at ${describe(answer, cell)}`;
            rests.add(restKey);
            continue;
        }
        const key = pathKey(path);
        if (cells.has(key)) return `it has two cells for ${describe(answer, cell)}`;
        cells.add(key);
    }
    for (let cell = 0; cell < answer.leafCount; cell++) {
        const path = elementAt(paths, cell);
        const rowDepth = depthOf(rows, cell);
        const columnDepth = depthOf(columns, cell);
        if (isRest(cell)) {
            if (!cells.has(pathKey(path))) return `its rest cell ${describe(answer, cell)} is answered without its parent's cell`;
            continue;
        }
        if (rowDepth > 0 && !cells.has(aboveKey(path, rowDepth - 1))) {
            return `its cell ${describe(answer, cell)} is shown without its parent row group`;
        }
        if (columnDepth > 0 && !cells.has(aboveKey(path, rows.length + columnDepth - 1))) {
            return `its cell ${describe(answer, cell)} is shown without its parent column group`;
        }
    }
    return windowMismatch(levels, answer, windowing, rowExpanded, paths);
}

// Each set ordered by a value under a question's window: answered whole up to the limit, and past it as a window
// where it was asked, its rest cells on a side only where that side has siblings beyond its margin.
function windowMismatch(levels: PivotLevels, answer: PivotAnswer, windowing: PivotAnswerWindowing, rowExpanded: readonly (readonly boolean[])[],
    paths: readonly (readonly number[])[]): string | undefined {
    const rows = answer.rows;
    const asked = levels.window;
    if (asked === undefined) return windowing.windows.length === 0 ? undefined : 'it answers a window, and the question asks for none';
    const rowCount = rows.length;
    // The children each parent row group has at the grand total of the columns, by the parent's path.
    const children = new Map<string, number>();
    const parents = new Set<string>();
    paths.forEach((path, cell) => {
        if (windowing.windows.length > 0 && elementAt(windowing.rests, cell) >= 0) return;
        if (path.slice(rowCount).some(item => item >= 0)) return;
        const depth = depthOf(rows, cell);
        parents.add(path.slice(0, depth).join(','));
        if (depth === 0) return;
        const parent = path.slice(0, depth - 1).join(',');
        children.set(parent, (children.get(parent) ?? 0) + 1); // oxlint-disable-line ex-grid/no-stand-in-default -- a parent's first child counted
    });
    const describeParent = (parent: readonly number[]): string =>
        `[${parent.map((item, level) => `${elementAt(rows, level).field}=${itemKeyText(elementAt(elementAt(rows, level).items, item))}`).join(', ')}]`;
    const startOf = new Map(asked.starts.map(start => [start.rows.map(publicKeyId).join('\u0000'), start.start]));
    const windowed = new Set<string>();
    for (const [w, window] of windowing.windows.entries()) {
        const key = window.parent.join(',');
        const depth = window.parent.length;
        if (windowed.has(key)) return `it has two windows under ${describeParent(window.parent)}`;
        windowed.add(key);
        if (!parents.has(key)) return `its window ${w} is under ${describeParent(window.parent)}, a group it has no cell for`;
        if (depth > 0 && !elementAt(elementAt(rowExpanded, depth - 1), elementAt(window.parent, depth - 1))) {
            return `its window ${w} is under ${describeParent(window.parent)}, whose children the question's levels do not show`;
        }
        if (elementAt(levels.rows, depth).order === undefined) {
            return `its window ${w} is of the Items of '${elementAt(rows, depth).field}', which the question orders by label`;
        }
        const answered = children.get(key) ?? 0; // oxlint-disable-line ex-grid/no-stand-in-default -- a window with no sibling answered is refused below
        const margins = (window.before > 0 ? 1 : 0) + (window.after > 0 ? 1 : 0);
        const shown = answered - margins;
        if (shown < 1) return `its window ${w} under ${describeParent(window.parent)} shows no sibling`;
        const total = window.before + shown + window.after;
        if (total <= asked.limit) return `its window ${w} under ${describeParent(window.parent)} is of ${total} siblings, which the limit of ${asked.limit} answers whole`;
        const parentId = window.parent.map((item, level) => publicKeyId(elementAt(elementAt(rows, level).items, item))).join('\u0000');
        const start = Math.min(startOf.get(parentId) ?? 0, total - asked.limit); // oxlint-disable-line ex-grid/no-stand-in-default -- a window the question names no start for starts at the first sibling
        if (window.before > start || window.before + shown < start + asked.limit) {
            return `its window ${w} under ${describeParent(window.parent)} shows siblings ${window.before} to ${window.before + shown - 1}, and the question asks for ${start} to ${start + asked.limit - 1}`;
        }
    }
    const restSides = new Set<number>();
    paths.forEach((path, cell) => {
        const code = windowing.windows.length === 0 ? -1 : elementAt(windowing.rests, cell);
        if (code >= 0 && path.slice(rowCount).every(item => item < 0)) restSides.add(code);
    });
    for (const [w, window] of windowing.windows.entries()) {
        for (const [side, count, code] of [['before', window.before, 2 * w], ['after', window.after, 2 * w + 1]] as const) {
            if ((count >= 2) !== restSides.has(code)) {
                return count >= 2
                    ? `its window ${w} has ${count} siblings ${side} it and no rest cell for those beyond its margin`
                    : `its window ${w} has ${count} siblings ${side} it and a rest cell beyond its margin`;
            }
        }
    }
    // A set ordered by a value that came whole holds no more siblings than the limit.
    for (const [parent, count] of children) {
        if (windowed.has(parent) || count <= asked.limit) continue;
        const depth = parent === '' ? 0 : parent.split(',').length;
        if (elementAt(levels.rows, depth).order === undefined) continue;
        const named = parent === '' ? [] : parent.split(',').map(Number);
        return `it answers the ${count} siblings under ${describeParent(named)} whole, past the window's limit of ${asked.limit}`;
    }
    return undefined;
}
