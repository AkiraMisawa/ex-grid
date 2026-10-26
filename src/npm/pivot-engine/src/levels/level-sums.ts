// Every group a level answer shows, checked against its parent (ADR-0370 section 3), as its cube is made: each
// sibling set's records and parts composed and compared with the parent's, by the bounds level-check.ts holds.
// The children are all of them, since every question names the whole screen: the groups below a shown group
// that is expanded, crossed with the same group of the other axis. A set that does not add up refuses the
// answer, naming its parent, and no figure from it is shown (spine rule 1).

import type { PartColumns } from '../aggregates/parts';
import type { AxisNode } from '../cube/cube';
import type { PivotLevels } from '../cube/query';
import type { PivotItemKey } from '../items/item-key';
import { elementAt } from '../must';
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
    let units = 0;
    for (let cell = 0; cell < cells.rowOf.length; cell++) {
        const row = elementAt(cells.rowOf, cell);
        const column = elementAt(cells.columnOf, cell);
        const sets: [string, number[]][] = [];
        if (expandsBelow(row, cells.rowFields, rowExpanded)) {
            sets.push(['row', row.children.flatMap(child => {
                const at = cells.cellAt(child, column);
                return at === undefined ? [] : [at];
            })]);
        }
        if (expandsBelow(column, cells.columnFields, columnExpanded)) {
            sets.push(['column', column.children.flatMap(child => {
                const at = cells.cellAt(row, child);
                return at === undefined ? [] : [at];
            })]);
        }
        for (const [axis, children] of sets) {
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
            units += children.length * (1 + cells.values.length);
        }
        if (++units >= pieceUnits) {
            units = 0;
            yield;
        }
    }
}
