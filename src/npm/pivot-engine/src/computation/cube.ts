// A computation's cube (ADR-0153): the cells, trees and leaves of the cube a fresh pass made, kept up to date
// from the leaves each Change Batch touched — a total by subtraction where every part allows it, merged again
// from its leaves, in the order a fresh pass merges them, where one does not. Its working indexes are its
// own; every cube it publishes is immutable. The C# engine's ComputationCube.

import { decimalCompare } from '../aggregates/exact-decimal';
import { partSum, type PartColumns } from '../aggregates/parts';
import { cellKey, cubeWith, type AxisNode, type CubeBuild, type PivotCube } from '../cube/cube';
import { defined, elementAt } from '../must';
import { noFirstRow, type AggregationPass } from '../source/aggregation-pass';
import { pieceUnits } from '../source/slicing';
import { ComputationCells } from './cells';
import { WorkingAxis } from './axis';
import { Members } from './members';

// The largest exact total subtraction keeps, and the most decimal places: within them every partial sum of a
// total's leaves is exact in any order, at most 2^31 leaves each within 10^12 at six places needing fewer than
// 96 bits. A guard on the work only, never a cap on the data: past it, a total is merged again in order.
const additiveBound = { mantissa: 1_000_000_000_000n, scale: 0 };
const additiveScale = 6;

/** Counts units of work and says when a piece's worth is done, for the work to yield. */
export class Pace {
    #units = 0;

    /** Adds a step's units; true once a piece's worth is done. */
    spent(units: number): boolean {
        this.#units += units;
        if (this.#units < pieceUnits) return false;
        this.#units = 0;
        return true;
    }
}

/** The computation of one question's cube, updated in place from the pass it was made from. */
export class ComputationCube {
    // Each leaf of the pass, by its number: the nodes it stands at, its records, and its first record — where a
    // fresh pass meets it, which orders the merges into its totals. A leaf with no records has no nodes.
    readonly #leafRow: (AxisNode | undefined)[] = [];
    readonly #leafColumn: (AxisNode | undefined)[] = [];
    readonly #leafRecords: number[] = [];
    readonly #leafFirst: number[] = [];
    // Each total's leaves, by first record: what a total that subtraction cannot keep exact is merged again
    // from. Made from the leaves held the first time an update needs them, so a question never updated makes
    // none (PV-21).
    #members: Map<number, Members> | undefined;
    readonly #rows: WorkingAxis;
    readonly #columns: WorkingAxis;
    #cells: ComputationCells;
    #cube: PivotCube;
    readonly #rowFields: number;
    readonly #columnFields: number;
    // The leaves whose parts subtraction cannot keep exactly, and how many there are.
    readonly #nonAdditive = new Set<number>();
    /** The row nodes on the path of a leaf the last update changed: a value sort under them may move. */
    readonly valueRows = new Set<number>();
    /** The column nodes on the path of a leaf the last update changed. */
    readonly valueColumns = new Set<number>();

    private constructor(cube: PivotCube, cells: ComputationCells, rows: WorkingAxis, columns: WorkingAxis, rowFields: number, columnFields: number) {
        this.#cube = cube;
        this.#cells = cells;
        this.#rows = rows;
        this.#columns = columns;
        this.#rowFields = rowFields;
        this.#columnFields = columnFields;
    }

    /** The cube the computation last published. */
    get cube(): PivotCube {
        return this.#cube;
    }

    /** The row nodes the last update made, relabelled, gave other children or took a leaf back to. */
    get changedRowNodes(): ReadonlySet<number> {
        return this.#rows.changed;
    }

    /** The column nodes the last update made, relabelled, gave other children or took a leaf back to. */
    get changedColumnNodes(): ReadonlySet<number> {
        return this.#columns.changed;
    }

    /** The column nodes the last update took the last leaf from. */
    get removedColumnNodes(): ReadonlySet<number> {
        return this.#columns.removed;
    }

    /**
     * The computation of a cube the pass's answer made: its cells, trees and leaves taken as the cube holds them.
     * `build` is what the cube's build made: the nodes of each of the answer's leaves, which are the pass's
     * leaves that hold records, in their order.
     */
    static *create(cube: PivotCube, build: CubeBuild, pass: AggregationPass): Generator<void, ComputationCube> {
        const rowFields = pass.query.rows.length;
        const columnFields = pass.query.columns.length;
        const rows = new WorkingAxis(build.rowNodes, rowFields);
        const columns = new WorkingAxis(build.columnNodes, columnFields);
        const made = new ComputationCube(cube, ComputationCells.of(build.cells, build.values), rows, columns, rowFields, columnFields);
        const first = pass.firstRows();
        const pace = new Pace();
        let answered = 0;
        for (let leaf = 0; leaf < pass.leafCount; leaf++) {
            const records = pass.recordsOf(leaf);
            if (records === 0) continue;
            if (answered === build.rowLeaves.length) throw new Error('Internal: the cube holds fewer leaves than the pass it was made from (ADR-0153).');
            made.#hold(pass, leaf, elementAt(build.rowLeaves, answered), elementAt(build.columnLeaves, answered), records, elementAt(first, leaf));
            answered++;
            if (pace.spent(1 + rowFields + columnFields)) yield;
        }
        if (answered !== build.rowLeaves.length) throw new Error('Internal: the cube holds more leaves than the pass it was made from (ADR-0153).');
        return made;
    }

    // A leaf of the first cube, at the nodes the cube made for it.
    #hold(pass: AggregationPass, leaf: number, row: AxisNode, column: AxisNode, records: number, first: number): void {
        this.#rows.use(pass, leaf, 0, row);
        this.#columns.use(pass, leaf, this.#rowFields, column);
        this.#keep(leaf, row, column, records, first);
        const { columns, offset } = this.#cells.at(this.#cells.mustCellOf(cellKey(row.id, column.id)));
        if (!additive(columns, offset)) this.#nonAdditive.add(leaf);
    }

    #keep(leaf: number, row: AxisNode | undefined, column: AxisNode | undefined, records: number, first: number): void {
        this.#leafRow[leaf] = row;
        this.#leafColumn[leaf] = column;
        this.#leafRecords[leaf] = records;
        this.#leafFirst[leaf] = first;
    }

    // Each total's leaves, made from the leaves held, a piece at a time, the first time an update needs them.
    // The leaves are listed in their number's order and sorted once by their first record.
    *#membersSteps(): Generator<void, Map<number, Members>> {
        if (this.#members !== undefined) return this.#members;
        const made = new Map<number, Members>();
        const pace = new Pace();
        const weight = (1 + this.#rowFields) * (1 + this.#columnFields);
        for (let leaf = 0; leaf < this.#leafRow.length; leaf++) {
            const row = this.#leafRow[leaf];
            const column = this.#leafColumn[leaf];
            if (row === undefined || column === undefined) continue;
            const first = elementAt(this.#leafFirst, leaf);
            for (const total of totals(row, column)) membersIn(made, total).append(first, leaf);
            if (pace.spent(weight)) yield;
        }
        for (const members of made.values()) members.seal();
        this.#members = made;
        return made;
    }

    /**
     * Brings the cube up to the batches the pass folded in since it was last updated, and publishes it under
     * `version`: the leaves the pass names as changed are written, each total over them adjusted or merged again,
     * and the Items whose spelling changed relabelled. Run to its end once begun: a cancellation is not heard.
     */
    *update(pass: AggregationPass, version: string): Generator<void, PivotCube> {
        // Made before the batch changes any leaf held: they are the leaves before it.
        const allMembers = yield* this.#membersSteps();
        this.#rows.begin();
        this.#columns.begin();
        this.valueRows.clear();
        this.valueColumns.clear();
        const changed = [...pass.changedLeaves].toSorted((a, b) => a - b);
        const values = pass.partsOf(changed);
        const first = pass.firstRows();
        // Subtraction keeps a total only where every leaf it held, and every leaf it holds now, is kept by it.
        const additiveBefore = this.#nonAdditive.size === 0;
        for (let at = 0; at < changed.length; at++) {
            const leaf = elementAt(changed, at);
            if (pass.recordsOf(leaf) !== 0 && !additive(values, at)) this.#nonAdditive.add(leaf);
            else this.#nonAdditive.delete(leaf);
        }
        const additiveNow = this.#nonAdditive.size === 0 && additiveBefore;
        const write = this.#cells.writer();
        const touched = new Set<number>();
        let included = this.#cube.includedRecordCount;
        const pace = new Pace();
        for (let at = 0; at < changed.length; at++) {
            if (pace.spent(1 + this.#rowFields + this.#columnFields)) yield;
            const leaf = elementAt(changed, at);
            const records = pass.recordsOf(leaf);
            const heldRow = this.#leafRow[leaf];
            const heldColumn = this.#leafColumn[leaf];
            const held = heldRow !== undefined && heldColumn !== undefined;
            included += records - (held ? elementAt(this.#leafRecords, leaf) : 0);
            if (!held && records === 0) continue;
            let row: AxisNode;
            let column: AxisNode;
            let oldFirst: number;
            if (held) {
                row = heldRow;
                column = heldColumn;
                oldFirst = elementAt(this.#leafFirst, leaf);
            } else {
                row = this.#rows.addLeaf(pass, leaf, 0, this.#rowFields);
                column = this.#columns.addLeaf(pass, leaf, this.#rowFields, this.#columnFields);
                oldFirst = elementAt(first, leaf);
                for (const total of totals(row, column)) membersIn(allMembers, total).add(oldFirst, leaf);
            }
            for (let r: AxisNode | undefined = row; r !== undefined; r = r.parent) this.valueRows.add(r.id);
            for (let c: AxisNode | undefined = column; c !== undefined; c = c.parent) this.valueColumns.add(c.id);
            const key = cellKey(row.id, column.id);
            const affected = [...totals(row, column)];
            for (const total of affected) touched.add(total);
            if (additiveNow) {
                const before = held ? this.#cells.at(this.#cells.mustCellOf(key)) : undefined;
                const after = records > 0 ? { values, at } : undefined;
                for (const total of affected) write.adjust(write.cell(total), before, after);
            }
            if (records === 0) {
                write.remove(key);
                this.#rows.removeLeaf(row);
                this.#columns.removeLeaf(column);
                for (const total of affected) mustMembers(allMembers, total).remove(oldFirst, leaf);
                this.#keep(leaf, undefined, undefined, 0, noFirstRow);
            } else {
                const nextFirst = elementAt(first, leaf);
                if (oldFirst !== nextFirst) {
                    for (const total of affected) {
                        const members = mustMembers(allMembers, total);
                        members.remove(oldFirst, leaf);
                        members.add(nextFirst, leaf);
                    }
                }
                this.#keep(leaf, row, column, records, nextFirst);
                write.put(write.cell(key), values, at);
            }
        }
        for (const total of touched) {
            const members = mustMembers(allMembers, total);
            if (members.count === 0) {
                write.remove(total);
                continue;
            }
            const cell = write.cell(total);
            if (additiveNow) {
                write.finish(cell);
                continue;
            }
            write.reset(cell);
            for (const leaf of members.leaves()) {
                write.merge(cell, write.cell(cellKey(defined(this.#leafRow[leaf], 'the row of a leaf a total lists').id, defined(this.#leafColumn[leaf], 'the column of a leaf a total lists').id)));
                if (pace.spent(1)) yield;
            }
            write.finish(cell);
        }
        this.#rows.relabel(pass, 0, this.#rowFields);
        this.#columns.relabel(pass, this.#rowFields, this.#columnFields);
        this.#cells = write.freeze();
        this.#cube = cubeWith(this.#cube, version, included, this.#rows.freeze(), this.#columns.freeze(), this.#cells);
        return this.#cube;
    }
}

// Whether every part of a leaf's cell is kept exactly by subtraction: counts, and an exact sum within the bound,
// and no non-finite number.
function additive(parts: readonly PartColumns[], cell: number): boolean {
    for (const columns of parts) {
        if ((columns.parts & ~partSum) !== 0 || elementAt(columns.counts, cell).nonFinite) return false;
        const sums = columns.sums;
        if (sums === undefined) continue;
        const sum = elementAt(sums, cell);
        if (sum.inexact || sum.exact.scale > additiveScale) return false;
        const magnitude = sum.exact.mantissa < 0n ? { mantissa: -sum.exact.mantissa, scale: sum.exact.scale } : sum.exact;
        if (decimalCompare(magnitude, additiveBound) > 0) return false;
    }
    return true;
}

// Every total a leaf at `row` and `column` is merged into: each pair of their own nodes or ancestors but the leaf's.
function* totals(row: AxisNode, column: AxisNode): Generator<number, void> {
    for (let r: AxisNode | undefined = row; r !== undefined; r = r.parent) {
        for (let c: AxisNode | undefined = column; c !== undefined; c = c.parent) {
            if (r !== row || c !== column) yield cellKey(r.id, c.id);
        }
    }
}

function membersIn(all: Map<number, Members>, total: number): Members {
    let members = all.get(total);
    if (members === undefined) {
        members = new Members();
        all.set(total, members);
    }
    return members;
}

function mustMembers(all: ReadonlyMap<number, Members>, total: number): Members {
    const members = all.get(total);
    if (members === undefined) throw new Error('Internal: a total the update touches lists no leaves (ADR-0153).');
    return members;
}

