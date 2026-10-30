// Every group a level answer shows, checked against its parent (ADR-0370 section 3), as its cube is made: each
// sibling set's records and parts composed and compared with the parent's, by the bounds level-check.ts holds.
// The children are all of them, since every question names the whole screen: the groups below a shown group
// that is expanded, crossed with the same group of the other axis. A set that does not add up refuses the
// answer, naming its parent, and no figure from it is shown (spine rule 1). A Sibling Window's set is its siblings
// answered and its Rest Cells, and its margins are checked to sort strictly beyond its edges.

import type { PartColumns } from '../aggregates/parts';
import type { AxisNode } from '../cube/cube';
import type { PivotRestSide } from '../cube/answer';
import type { PivotLevels } from '../cube/query';
import type { PivotItemKey } from '../items/item-key';
import { defined, elementAt } from '../must';
import { pieceUnits } from '../source/slicing';
import { levelMismatch, levelPartsAt, type LevelParts } from './level-check';
import { levelExpander } from './level-shown';

/** What the check reads of a cube being made from a level answer. */
export interface LevelCells {
    readonly levels: PivotLevels;
    /** How many row and column fields the question places. */
    readonly rowFields: number;
    readonly columnFields: number;
    /** Each cell's row node and column node. */
    readonly rowOf: readonly AxisNode[];
    readonly columnOf: readonly AxisNode[];
    /** The cell at a row node and a column node. */
    readonly cellAt: (row: AxisNode, column: AxisNode) => number | undefined;
    /** Each cell's records. */
    readonly records: (cell: number) => number;
    /** Each field in Values: its name and its parts at every cell. */
    readonly values: readonly { readonly field: string; readonly columns: PartColumns }[];
    /** A cell's groups, as a refusal names them. */
    readonly describe: (cell: number) => string;
    /** The column tree's root: the grand total of the columns. */
    readonly columnRoot: AxisNode;
    /** The answer's Sibling Windows. */
    readonly windows: readonly LevelWindow[];
    /** Whether a cell is a Rest Cell, which stands beside a group's children and is checked as a row of its own. */
    readonly isRest: (cell: number) => boolean;
}

/** A Sibling Window as the check reads it: its parent's row node, the siblings before and after the ones shown,
 * and its Rest Cells by side and column node. */
export interface LevelWindow {
    readonly parent: AxisNode;
    readonly before: number;
    readonly after: number;
    readonly rests: ReadonlyMap<PivotRestSide, ReadonlyMap<AxisNode, number>>;
}

/** Whether a node's children are shown under an axis's rules: the grand total's always are, where there is a
 * field below it. */
export function expandsBelow(node: AxisNode, fields: number, expanded: (level: number, item: PivotItemKey) => boolean): boolean {
    if (node.level + 1 >= fields) return false;
    return node.item === undefined || expanded(node.level, node.item.publicKey);
}

/** Checks every sibling set of the cells against its parent, yielding about every {@link pieceUnits} children
 * read; throws, naming the parent, at the first set that does not add up. */
export function* levelSumsSteps(cells: LevelCells): Generator<void, void> {
    const rowExpanded = levelExpander(cells.levels.rows);
    const columnExpanded = levelExpander(cells.levels.columns);
    // Each cell's parts, read once: a cell is a parent once and a child in a set of each axis.
    const parts = cells.values.map(() => new Map<number, LevelParts>());
    const partsAt = (v: number, cell: number): LevelParts => {
        const read = elementAt(parts, v);
        let held = read.get(cell);
        if (held === undefined) {
            held = levelPartsAt(elementAt(cells.values, v).columns, cell);
            read.set(cell, held);
        }
        return held;
    };
    const windowOf = new Map(cells.windows.map(window => [window.parent, window]));
    const check = (cell: number, axis: string, children: readonly number[]): void => {
        const refuse = (what: string): never => {
            throw new Error(`The server's answer for ${cells.describe(cell)} does not add up: ${what} is not what its ${axis} groups below `
                + 'compose to (ADR-0370 section 3).');
        };
        const records = children.reduce((total, child) => total + cells.records(child), 0);
        if (records !== cells.records(cell)) refuse(`its records (${cells.records(cell)})`);
        cells.values.forEach((value, v) => {
            const part = levelMismatch(partsAt(v, cell), children.map(child => partsAt(v, child)));
            if (part !== undefined) refuse(`its ${part} of '${value.field}'`);
        });
    };
    let units = 0;
    for (let cell = 0; cell < cells.rowOf.length; cell++) {
        if (cells.isRest(cell)) continue;
        const row = elementAt(cells.rowOf, cell);
        const column = elementAt(cells.columnOf, cell);
        const sets: [string, number[]][] = [];
        if (expandsBelow(row, cells.rowFields, rowExpanded)) {
            sets.push(['row', [...row.children.flatMap(child => {
                const at = cells.cellAt(child, column);
                return at === undefined ? [] : [at];
            }), ...restsAt(windowOf.get(row), column)]]);
        }
        if (expandsBelow(column, cells.columnFields, columnExpanded)) {
            sets.push(['column', column.children.flatMap(child => {
                const at = cells.cellAt(row, child);
                return at === undefined ? [] : [at];
            })]);
        }
        for (const [axis, children] of sets) {
            check(cell, axis, children);
            units += children.length * (1 + cells.values.length);
        }
        if (++units >= pieceUnits) {
            units = 0;
            yield;
        }
    }
    // A Rest Cell is a row of its own across the columns: its column groups below compose to it as any row's do.
    for (const window of cells.windows) {
        for (const byColumn of window.rests.values()) {
            for (const [column, cell] of byColumn) {
                if (!expandsBelow(column, cells.columnFields, columnExpanded)) continue;
                check(cell, 'column', column.children.flatMap(child => {
                    const at = byColumn.get(child);
                    return at === undefined ? [] : [at];
                }));
                units += column.children.length * (1 + cells.values.length);
                if (units >= pieceUnits) {
                    units = 0;
                    yield;
                }
            }
        }
        marginsCheck(cells, window);
    }
}

// A window's Rest Cells at a column node, before and after.
function restsAt(window: LevelWindow | undefined, column: AxisNode): number[] {
    if (window === undefined) return [];
    return (['before', 'after'] as const).flatMap(side => {
        const rest = window.rests.get(side)?.get(column);
        return rest === undefined ? [] : [rest];
    });
}

// Each margin of a Sibling Window sorts strictly beyond its edge, by the value its set is ordered by at the grand
// total of the columns: a margin tied with the sibling next to it would leave a tie straddling the edge, and the
// siblings answered as the window would not be those the order puts there (ADR-0370 section 3).
function marginsCheck(cells: LevelCells, window: LevelWindow): void {
    const depth = window.parent.level + 1;
    const order = elementAt(cells.levels.rows, depth).order;
    if (order === undefined) throw new Error('Internal: a window is of a set ordered by a value, as the answer was checked.');
    const columns = elementAt(cells.values, order.value).columns;
    const keyOf = (cell: number): number | undefined => {
        const value = columns.read(cell, order.aggregation);
        return value.kind === 'number' ? value.number : undefined;
    };
    // Numbers in the order's direction, and every value that is not one after them, tied with one another.
    const compare = (x: number | undefined, y: number | undefined): number => {
        if (x === undefined || y === undefined) return x === undefined ? (y === undefined ? 0 : 1) : -1;
        const compared = x < y ? -1 : x > y ? 1 : 0;
        return order.descending ? -compared : compared;
    };
    const siblings = window.parent.children.flatMap(child => {
        const at = cells.cellAt(child, cells.columnRoot);
        return at === undefined ? [] : [{ cell: at, key: keyOf(at) }];
    }).toSorted((a, b) => compare(a.key, b.key));
    const parent = defined(cells.cellAt(window.parent, cells.columnRoot), "a window's parent's cell, as the answer was checked");
    const refuse = (side: string, margin: number, edge: number): never => {
        throw new Error(`The server's window under ${cells.describe(parent)} is not where its order puts it: `
            + `the margin ${side} it, ${cells.describe(margin)}, does not sort strictly ${side} ${cells.describe(edge)} (ADR-0370 section 3).`);
    };
    const first = elementAt(siblings, 0);
    const last = elementAt(siblings, siblings.length - 1);
    if (window.before > 0) {
        const edge = elementAt(siblings, 1);
        if (compare(first.key, edge.key) >= 0) refuse('before', first.cell, edge.cell);
    }
    if (window.after > 0) {
        const edge = elementAt(siblings, siblings.length - 2);
        if (compare(last.key, edge.key) <= 0) refuse('after', last.cell, edge.cell);
    }
}
