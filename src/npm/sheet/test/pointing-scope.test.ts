// The decisions of PointingScopeTests and PointingScopeKeyboardTests (ADR-0058), against a Sheet and a grid that
// only record: what a press or an arrow writes, what it refuses and why, and what the Scope declares to the grid.
// Pointing as the components drive it — the keyboard crossing a Sheet's edge, the Input Order — is the
// components' to test.

import { describe, expect, it } from 'vitest';
import type { GridDirection } from '@ex-grid/core';
import { LinkedTable, LinkedTableColumn, Value } from '@ex-grid/sheet-engine';
import {
    PointingScope, sheetWords, type GridPointedAt, type GridPointedStep, type PointedAtGrid, type PointingRefusal,
    type PointingSheet, type TableRow,
} from '../src';

interface Position {
    readonly id: string | undefined;
    /** The key is an Error Value, whatever `id` says. */
    readonly keyIsAnError?: true;
    readonly book: string;
    readonly pv: number;
    readonly note: string;
}

const positions: readonly Position[] = [
    { id: 'R-1', book: 'Rates', pv: 110, note: 'a' },
    { id: 'R-2', book: 'FX', pv: 250, note: 'b' },
    { id: 'R-"5"', book: 'Credit', pv: 0, note: 'c' },
];

const lookupR1 = 'XLOOKUP("R-1", Positions[Id], Positions[PV])';
const lookupR2 = 'XLOOKUP("R-2", Positions[Id], Positions[PV])';

function tableRow(row: Position): TableRow {
    const id = row.keyIsAnError === true ? Value.fromError('#N/A') : row.id === undefined ? undefined : Value.fromText(row.id);
    return [id, Value.fromText(row.book), Value.fromNumber(row.pv)];
}

function positionsTable(keyColumns: readonly string[] = ['Id']): LinkedTable {
    return new LinkedTable('Positions', ['Id', 'Book', 'PV'], false, positions.length, keyColumns);
}

/** A Sheet whose edit is `start` followed by what Point wrote last, and which records what it is told. */
class FakeSheet implements PointingSheet {
    readonly refusals: PointingRefusal[] = [];
    readonly rootId = 'sheet-root';
    inPoint = true;
    readonly #written: string[] = [];

    readonly start: string;
    linkedTables: readonly LinkedTable[];

    constructor(start: string, linkedTables: readonly LinkedTable[] = [positionsTable()]) {
        this.start = start;
        this.linkedTables = linkedTables;
    }

    get text(): string {
        const last = this.#written.at(-1);
        return last === undefined ? this.start : this.start + last;
    }

    async writePointedText(text: string): Promise<boolean> {
        if (!this.inPoint) return false;
        this.#written.push(text);
        return true;
    }

    async takeBackPointedText(): Promise<boolean> {
        if (this.#written.length === 0) return false;
        this.#written.pop();
        return true;
    }

    async tellPointingRefused(refusal: PointingRefusal): Promise<void> {
        this.refusals.push(refusal);
    }

    get reasons(): string[] {
        return this.refusals.map(refusal => refusal.reason);
    }
}

/** A grid of `rows` in its current order, an `undefined` row being one that has not arrived, showing `columns`. */
class FakeGrid implements PointedAtGrid<Position> {
    readonly revealed: string[] = [];

    rows: readonly (Position | undefined)[];
    columns: readonly string[];

    constructor(rows: readonly (Position | undefined)[], columns: readonly string[] = ['Id', 'Book', 'Value', 'Note']) {
        this.rows = rows;
        this.columns = columns;
    }

    async step(isRow: (row: Position) => boolean, column: string, direction: GridDirection, isColumn: (column: string) => boolean): Promise<GridPointedStep<Position>> {
        const at = this.rows.findIndex(row => row !== undefined && isRow(row));
        const from = this.rows[at];
        if (from === undefined || !this.columns.includes(column)) return { kind: 'notHeld' };
        if (direction === 'up' || direction === 'down') {
            const next = direction === 'up' ? at - 1 : at + 1;
            if (next < 0 || next >= this.rows.length) return { kind: 'edge' };
            const row = this.rows[next];
            return row === undefined ? { kind: 'rowNotArrived', column } : { kind: 'cell', row, column };
        }
        const next = this.#nextColumn(column, direction, isColumn);
        return next === undefined ? { kind: 'edge' } : { kind: 'cell', row: from, column: next };
    }

    async stepFromColumn(column: string, direction: GridDirection, isColumn: (column: string) => boolean): Promise<GridPointedStep<Position>> {
        if (!this.columns.includes(column)) return { kind: 'notHeld' };
        if (direction === 'up') return { kind: 'edge' };
        if (direction === 'down') {
            if (this.rows.length === 0) return { kind: 'edge' };
            const row = this.rows[0];
            return row === undefined ? { kind: 'rowNotArrived', column } : { kind: 'cell', row, column };
        }
        const next = this.#nextColumn(column, direction, isColumn);
        return next === undefined ? { kind: 'edge' } : { kind: 'column', column: next };
    }

    async reveal(isRow: (row: Position) => boolean, column: string): Promise<boolean> {
        const row = this.rows.find(candidate => candidate !== undefined && isRow(candidate));
        this.revealed.push(`${row === undefined ? '?' : String(row.id)}/${column}`);
        return row !== undefined;
    }

    async revealColumn(column: string): Promise<boolean> {
        this.revealed.push(column);
        return this.columns.includes(column);
    }

    /** The row and column the declaration's dashes stand over, as `id/column`, or `column` down its body. */
    static dashedIn(grid: FakeGrid, declaration: GridPointedAt<Position>): string | undefined {
        const dashes = declaration.dashes;
        if (dashes === undefined) return undefined;
        const isRow = dashes.row;
        if (isRow === undefined) return dashes.column;
        const row = grid.rows.find(candidate => candidate !== undefined && isRow(candidate));
        return `${row === undefined ? '?' : String(row.id)}/${dashes.column}`;
    }

    #nextColumn(column: string, direction: 'left' | 'right', isColumn: (column: string) => boolean): string | undefined {
        const at = this.columns.indexOf(column);
        const order = direction === 'left' ? this.columns.slice(0, at).toReversed() : this.columns.slice(at + 1);
        return order.find(candidate => isColumn(candidate));
    }
}

interface Page {
    readonly scope: PointingScope;
    readonly sheet: FakeSheet;
    readonly grid: FakeGrid;
    readonly declaration: GridPointedAt<Position>;
}

/** A Scope with one Sheet, pointing from `start`, and one grid of the positions whose Value column is the table's PV. */
function page(start = '=', sheet = new FakeSheet(start), rows: readonly (Position | undefined)[] = positions): Page {
    const scope = new PointingScope();
    const grid = new FakeGrid(rows);
    const declaration = scope.registerGrid('Positions', tableRow, { Value: 'PV' });
    declaration.attach(grid);
    scope.join(sheet);
    scope.points(sheet, true);
    return { scope, sheet, grid, declaration };
}

function rowOf(id: string): Position {
    const row = positions.find(position => position.id === id);
    if (row === undefined) throw new RangeError(id);
    return row;
}

async function pressCell(declaration: GridPointedAt<Position>, id: string, column: string, dragged = false): Promise<void> {
    await declaration.onPress({ kind: 'cell', row: rowOf(id), column, dragged });
}

describe('PointingScope: a press', () => {
    it('ADR-0058 / SH-32: a press on a cell writes XLOOKUP by the row\'s key, in the table\'s column names', async () => {
        expect.hasAssertions();
        const { sheet, declaration, grid } = page();
        expect(declaration.isPointedAt).toBe(true);
        await pressCell(declaration, 'R-2', 'Value');
        expect(sheet.text).toBe(`=${lookupR2}`);
        expect(sheet.refusals).toEqual([]);
        expect(FakeGrid.dashedIn(grid, declaration)).toBe('R-2/Value');
    });

    it('ADR-0058 (amended 2026-10-03): a press on a cell of a table keyed by several columns writes the lookup by every part', async () => {
        expect.hasAssertions();
        const { sheet, declaration } = page('=', new FakeSheet('=', [positionsTable(['Book', 'Id'])]));
        await pressCell(declaration, 'R-2', 'Value');
        expect(sheet.text).toBe('=XLOOKUP(1, (Positions[Book]="FX")*(Positions[Id]="R-2"), Positions[PV])');
    });

    it('ADR-0058 / SH-32: a press on a column header writes the table\'s column, and dashes down the column', async () => {
        expect.hasAssertions();
        const { sheet, declaration, grid } = page('=SUM(');
        await declaration.onPress({ kind: 'columnHeader', column: 'Value' });
        expect(sheet.text).toBe('=SUM(Positions[PV]');
        expect(FakeGrid.dashedIn(grid, declaration)).toBe('Value');
    });

    it('ADR-0058 / SH-32: the key is written as Excel writes a constant of its kind, a quote inside doubled', async () => {
        expect.hasAssertions();
        const { sheet, declaration } = page();
        await pressCell(declaration, 'R-"5"', 'Book');
        expect(sheet.text).toBe('=XLOOKUP("R-""5""", Positions[Id], Positions[Book])');
    });

    it('ADR-0058 / SH-32: a further press replaces what this Point wrote', async () => {
        expect.hasAssertions();
        const { sheet, declaration } = page('=1+');
        await pressCell(declaration, 'R-2', 'Value');
        await declaration.onPress({ kind: 'columnHeader', column: 'Id' });
        expect(sheet.text).toBe('=1+Positions[Id]');
        await pressCell(declaration, 'R-1', 'Value');
        expect(sheet.text).toBe(`=1+${lookupR1}`);
    });

    it('ADR-0058 / SH-32: more than one cell by Shift writes nothing, and the reason is told', async () => {
        expect.hasAssertions();
        const { sheet, declaration } = page();
        await declaration.onPress({ kind: 'severalCells' });
        expect(sheet.text).toBe('=');
        expect(sheet.refusals).toEqual([{ reason: 'severalCells', message: sheetWords().pointingRefused('severalCells', 'Positions', undefined, false) }]);
    });

    it('ADR-0058 / SH-32 (2026-09-30): a drag that reaches another cell takes back what its press wrote, and tells why', async () => {
        expect.hasAssertions();
        const { sheet, declaration, grid } = page('=1+');
        await pressCell(declaration, 'R-1', 'Value');
        await pressCell(declaration, 'R-2', 'Value');
        await declaration.onPress({ kind: 'severalCells', dragged: true });
        expect(sheet.text).toBe(`=1+${lookupR1}`);
        expect(sheet.refusals.map(refusal => refusal.message)).toEqual([sheetWords().pointingRefused('severalCells', 'Positions', undefined, true)]);
        // The dashes go back with the text.
        expect(FakeGrid.dashedIn(grid, declaration)).toBe('R-1/Value');
    });

    it('ADR-0058 / SH-32: a drag from a press that wrote nothing takes back nothing an earlier press wrote', async () => {
        expect.hasAssertions();
        const { sheet, declaration } = page();
        await pressCell(declaration, 'R-2', 'Value');
        await pressCell(declaration, 'R-1', 'Note');
        await declaration.onPress({ kind: 'severalCells', dragged: true });
        expect(sheet.text).toBe(`=${lookupR2}`);
        expect(sheet.reasons).toEqual(['columnNotInTable', 'severalCells']);
        expect(sheet.refusals.at(-1)?.message).toBe(sheetWords().pointingRefused('severalCells', 'Positions', undefined, false));
    });

    it('ADR-0058 / SH-32: several columns write nothing; a drag across headers takes back its press', async () => {
        expect.hasAssertions();
        const { sheet, declaration } = page('=SUM(');
        await declaration.onPress({ kind: 'severalColumns' });
        expect(sheet.text).toBe('=SUM(');
        await declaration.onPress({ kind: 'columnHeader', column: 'Id' });
        await declaration.onPress({ kind: 'severalColumns', dragged: true });
        expect(sheet.text).toBe('=SUM(');
        expect(sheet.refusals.map(refusal => refusal.message)).toEqual([
            sheetWords().pointingRefused('severalColumns', 'Positions', undefined, false),
            sheetWords().pointingRefused('severalColumns', 'Positions', undefined, true),
        ]);
    });

    it.each([
        ['ADR-0058 / SH-32 / ADR-0032: a Header Group\'s rectangle', { kind: 'headerGroup' }, 'headerGroup'],
        ['ADR-0058 / SH-32: a column the table does not have, from a header', { kind: 'columnHeader', column: 'Note' }, 'columnNotInTable'],
        ['ADR-0058 / SH-32: a column the table does not have, from a cell', { kind: 'cell', row: rowOf('R-1'), column: 'Note' }, 'columnNotInTable'],
        ['ADR-0058 / SH-32: a cell whose row has not arrived', { kind: 'cell', column: 'Value' }, 'rowNotArrived'],
    ] as const)('%s writes nothing', async (_, press, reason) => {
        expect.hasAssertions();
        const { sheet, declaration } = page();
        await declaration.onPress(press);
        expect(sheet.text).toBe('=');
        expect(sheet.reasons).toEqual([reason]);
    });

    it('ADR-0058 / SH-32: a cell of a table declared without a key writes nothing; its column header still writes the column', async () => {
        expect.hasAssertions();
        const { sheet, declaration } = page('=', new FakeSheet('=', [positionsTable([])]));
        await pressCell(declaration, 'R-2', 'Value');
        expect(sheet.text).toBe('=');
        expect(sheet.reasons).toEqual(['noKey']);
        await declaration.onPress({ kind: 'columnHeader', column: 'Value' });
        expect(sheet.text).toBe('=Positions[PV]');
    });

    it.each([
        ['blank', { id: undefined, book: 'FX', pv: 1, note: '' }, 'blankKey'],
        ['an Error Value: no lookup finds it', { id: 'R-9', keyIsAnError: true, book: 'FX', pv: 1, note: '' }, 'keyIsAnError'],
    ] as const)('ADR-0058 / SH-32: a cell whose key is %s writes nothing', async (_, row, reason) => {
        expect.hasAssertions();
        const { sheet, declaration } = page();
        await declaration.onPress({ kind: 'cell', row, column: 'Value' });
        expect(sheet.text).toBe('=');
        expect(sheet.reasons).toEqual([reason]);
    });

    it('ADR-0058 / SH-32: a table the pointing Sheet does not declare writes nothing', async () => {
        expect.hasAssertions();
        const { sheet, declaration } = page('=', new FakeSheet('=', []));
        await pressCell(declaration, 'R-2', 'Value');
        expect(sheet.reasons).toEqual(['tableNotDeclared']);
        expect(sheet.refusals[0]?.message).toBe("Nothing was written: this Sheet has no Linked Table 'Positions' declared.");
    });

    it('ADR-0058: the table and its columns are found without regard to case, and the declared names are written', async () => {
        expect.hasAssertions();
        const scope = new PointingScope();
        const sheet = new FakeSheet('=');
        const declaration = scope.registerGrid('POSITIONS', tableRow, { Value: 'pv' });
        scope.join(sheet);
        scope.points(sheet, true);
        await pressCell(declaration, 'R-2', 'Value');
        expect(sheet.text).toBe(`=${lookupR2}`);
    });

    it('ADR-0058 / SH-32: a press handed over after the Sheet stopped pointing writes nothing, and the Sheet that pointed last tells why', async () => {
        expect.hasAssertions();
        const { scope, sheet, declaration } = page();
        scope.points(sheet, false);
        expect(declaration.isPointedAt).toBe(false);
        await pressCell(declaration, 'R-2', 'Value');
        expect(sheet.text).toBe('=');
        expect(sheet.reasons).toEqual(['notPointing']);
    });

    it('ADR-0058 / SH-32: a press whose Sheet left Point on its way writes nothing, and tells why', async () => {
        expect.hasAssertions();
        const { sheet, declaration } = page();
        sheet.inPoint = false;
        await pressCell(declaration, 'R-2', 'Value');
        expect(sheet.reasons).toEqual(['notPointing']);
    });

    it('ADR-0058 / SH-32: a Sheet that leaves the Scope stops pointing', async () => {
        expect.hasAssertions();
        const { scope, sheet, declaration } = page();
        scope.leave(sheet);
        expect(declaration.isPointedAt).toBe(false);
        await pressCell(declaration, 'R-2', 'Value');
        expect(sheet.reasons).toEqual([]);
    });

    it('ADR-0058 / SH-32: registering refuses a correspondence that names no column, and a table with no name', () => {
        expect.hasAssertions();
        const scope = new PointingScope();
        expect(() => scope.registerGrid('Positions', tableRow, { Value: '' })).toThrow(RangeError);
        expect(() => scope.registerGrid('', tableRow)).toThrow(RangeError);
    });

    it('ADR-0058: a row read as the table\'s row with a Value per column other than declared is the Consumer\'s defect, and throws', async () => {
        expect.hasAssertions();
        const scope = new PointingScope();
        const sheet = new FakeSheet('=');
        const declaration = scope.registerGrid('Positions', (row: Position) => tableRow(row).slice(0, 2), { Value: 'PV' });
        scope.join(sheet);
        scope.points(sheet, true);
        await expect(pressCell(declaration, 'R-2', 'Value')).rejects.toThrow(RangeError);
    });
});

describe('PointingScope: which Sheet points', () => {
    it('ADR-0058 / SH-35: only the Sheet that holds the keyboard points; another Sheet pointing in its turn writes alone', async () => {
        expect.hasAssertions();
        const { scope, sheet: left, declaration } = page();
        const right = new FakeSheet('=2*');
        scope.join(right);
        scope.points(right, true);
        // The keyboard went from the left Sheet to the right: the left one stopping changes nothing.
        scope.points(left, false);
        expect(declaration.isPointedAt).toBe(true);
        await pressCell(declaration, 'R-2', 'Value');
        expect(right.text).toBe(`=2*${lookupR2}`);
        expect(left.text).toBe('=');
    });

    it('ADR-0058 / SH-35: a Sheet that has not joined the Scope never points', () => {
        expect.hasAssertions();
        const scope = new PointingScope();
        const declaration = scope.registerGrid('Positions', tableRow);
        scope.points(new FakeSheet('='), true);
        expect(declaration.isPointedAt).toBe(false);
    });

    it('ADR-0058 ("On a circuit") / DC-54: a registered grid names the root of the Sheet that points, and only once one points', () => {
        expect.hasAssertions();
        const scope = new PointingScope();
        const declaration = scope.registerGrid('Positions', tableRow);
        const changes: boolean[] = [];
        declaration.onChanged(() => changes.push(declaration.isPointedAt));
        expect(declaration.pointingRootId).toBeUndefined();
        const sheet = new FakeSheet('=');
        scope.join(sheet);
        scope.points(sheet, true);
        expect(declaration.pointingRootId).toBe('sheet-root');
        // The root is named before the grid is declared pointed at.
        expect(changes).toEqual([false, true]);
    });

    it('ADR-0058 ("What is drawn"): the dashes go once Point over what the press wrote ends, and stay while it stands', async () => {
        expect.hasAssertions();
        const { scope, sheet, declaration } = page();
        await pressCell(declaration, 'R-2', 'Value');
        scope.pointStateChanged(sheet, 'writtenFromOutside');
        expect(declaration.dashes).toBeDefined();
        scope.pointStateChanged(sheet, 'inPoint');
        expect(declaration.dashes).toBeUndefined();
    });
});

describe('PointingScope: outlines (ADR-0057)', () => {
    it('ADR-0057 / ADR-0058: the Linked Table columns a Formula reads are outlined over the grid columns that are them, until the edit ends', () => {
        expect.hasAssertions();
        const { scope, sheet, declaration } = page();
        scope.outlineLinkedColumns(sheet, [
            { column: new LinkedTableColumn('positions', 'pv'), colour: 2 },
            { column: new LinkedTableColumn('Positions', 'Id'), colour: 3 },
            { column: new LinkedTableColumn('Other', 'Id'), colour: 4 },
        ]);
        // As C#'s does, a table column is outlined over the grid columns given it and over the one of its own
        // name, which this grid does not show.
        expect(declaration.outlinedColumns).toEqual([{ column: 'Value', colour: 2 }, { column: 'pv', colour: 2 }, { column: 'Id', colour: 3 }]);
        const outlined = declaration.outlinedColumns;
        scope.outlineLinkedColumns(sheet, [
            { column: new LinkedTableColumn('positions', 'pv'), colour: 2 },
            { column: new LinkedTableColumn('Positions', 'Id'), colour: 3 },
        ]);
        // The same outlines keep the same list, which is the declaration's change signal.
        expect(declaration.outlinedColumns).toBe(outlined);
        scope.outlineLinkedColumns(sheet, []);
        expect(declaration.outlinedColumns).toBeUndefined();
    });

    it('ADR-0057: a grid registered while a Formula is edited is outlined at once', () => {
        expect.hasAssertions();
        const scope = new PointingScope();
        const sheet = new FakeSheet('=');
        scope.join(sheet);
        scope.outlineLinkedColumns(sheet, [{ column: new LinkedTableColumn('Positions', 'Book'), colour: 1 }]);
        expect(scope.registerGrid('Positions', tableRow).outlinedColumns).toEqual([{ column: 'Book', colour: 1 }]);
    });
});

describe('PointingScope: the keyboard (ADR-0058, "The keyboard")', () => {
    it('SH-35: ↓ gives the next row\'s lookup and moves the dashes, scrolling the cell into view; ↑ goes back', async () => {
        expect.hasAssertions();
        const { scope, sheet, declaration, grid } = page();
        await pressCell(declaration, 'R-1', 'Value');
        await scope.pointArrow(sheet, { direction: 'down' });
        expect(sheet.text).toBe(`=${lookupR2}`);
        expect(FakeGrid.dashedIn(grid, declaration)).toBe('R-2/Value');
        expect(grid.revealed).toEqual(['R-2/Value']);
        await scope.pointArrow(sheet, { direction: 'up' });
        expect(sheet.text).toBe(`=${lookupR1}`);
    });

    it('SH-35: ← and → move to the next column the table has, passing over the grid\'s columns the table does not have', async () => {
        expect.hasAssertions();
        const scope = new PointingScope();
        const sheet = new FakeSheet('=');
        const grid = new FakeGrid(positions, ['Id', 'Note', 'Value']);
        const declaration = scope.registerGrid('Positions', tableRow, { Value: 'PV' });
        declaration.attach(grid);
        scope.join(sheet);
        scope.points(sheet, true);
        await pressCell(declaration, 'R-2', 'Id');
        await scope.pointArrow(sheet, { direction: 'right' });
        expect(sheet.text).toBe(`=${lookupR2}`);
        await scope.pointArrow(sheet, { direction: 'left' });
        expect(sheet.text).toBe('=XLOOKUP("R-2", Positions[Id], Positions[Id])');
    });

    it('SH-35: at an edge nothing moves, and nothing is told', async () => {
        expect.hasAssertions();
        const { scope, sheet, declaration } = page();
        await pressCell(declaration, 'R-1', 'Value');
        await scope.pointArrow(sheet, { direction: 'up' });
        await scope.pointArrow(sheet, { direction: 'right' });
        expect(sheet.text).toBe(`=${lookupR1}`);
        expect(sheet.refusals).toEqual([]);
    });

    it('SH-35: ↑ and ↓ follow the grid\'s current order, whatever row the press was on', async () => {
        expect.hasAssertions();
        const { scope, sheet, declaration, grid } = page();
        await pressCell(declaration, 'R-1', 'Value');
        grid.rows = positions.toReversed();
        await scope.pointArrow(sheet, { direction: 'up' });
        expect(sheet.text).toBe(`=${lookupR2}`);
    });

    it('SH-35: a row that has not arrived writes nothing, leaves the text as it was, and tells why', async () => {
        expect.hasAssertions();
        const { scope, sheet, declaration } = page('=', undefined, [rowOf('R-1'), undefined]);
        await pressCell(declaration, 'R-1', 'Value');
        await scope.pointArrow(sheet, { direction: 'down' });
        expect(sheet.text).toBe(`=${lookupR1}`);
        expect(sheet.reasons).toEqual(['rowNotArrived']);
    });

    it('SH-35 / SH-32: an arrow onto a row whose key is blank writes nothing, as a press there does, and tells why', async () => {
        expect.hasAssertions();
        const { scope, sheet, declaration } = page('=', undefined, [rowOf('R-1'), { id: undefined, book: 'FX', pv: 1, note: '' }]);
        await pressCell(declaration, 'R-1', 'Value');
        await scope.pointArrow(sheet, { direction: 'down' });
        expect(sheet.text).toBe(`=${lookupR1}`);
        expect(sheet.reasons).toEqual(['blankKey']);
    });

    it('SH-35: Shift and an arrow is refused as a range is, and Ctrl and an arrow is not built: each writes nothing, and tells why', async () => {
        expect.hasAssertions();
        const { scope, sheet, declaration } = page();
        await pressCell(declaration, 'R-1', 'Value');
        await scope.pointArrow(sheet, { direction: 'down', extends: true });
        await scope.pointArrow(sheet, { direction: 'down', toEdge: true });
        expect(sheet.text).toBe(`=${lookupR1}`);
        expect(sheet.refusals).toEqual([
            { reason: 'severalCells', message: sheetWords().pointingExtendedByKey('Positions') },
            { reason: 'dataEdge', message: sheetWords().pointingRefused('dataEdge', 'Positions', undefined, false) },
        ]);
    });

    it('SH-35: an arrow with nothing a press wrote standing does nothing', async () => {
        expect.hasAssertions();
        const { scope, sheet } = page();
        await scope.pointArrow(sheet, { direction: 'down' });
        expect(sheet.text).toBe('=');
        expect(sheet.refusals).toEqual([]);
    });

    it('Part B of the ninth run, Q52 / SH-35: after a header press, ↓ points at the first row of the current order, and the arrows go on from that cell', async () => {
        expect.hasAssertions();
        const { scope, sheet, declaration, grid } = page();
        grid.rows = positions.toReversed();
        await declaration.onPress({ kind: 'columnHeader', column: 'Value' });
        await scope.pointArrow(sheet, { direction: 'down' });
        expect(sheet.text).toBe('=XLOOKUP("R-""5""", Positions[Id], Positions[PV])');
        expect(grid.revealed).toEqual(['R-"5"/Value']);
        await scope.pointArrow(sheet, { direction: 'down' });
        expect(sheet.text).toBe(`=${lookupR2}`);
    });

    it('Part B of the ninth run, Q52 / SH-35: after a header press, ← and → point at the next column the table has, as a column, scrolled into view across', async () => {
        expect.hasAssertions();
        const { scope, sheet, declaration, grid } = page();
        await declaration.onPress({ kind: 'columnHeader', column: 'Book' });
        await scope.pointArrow(sheet, { direction: 'right' });
        expect(sheet.text).toBe('=Positions[PV]');
        expect(FakeGrid.dashedIn(grid, declaration)).toBe('Value');
        expect(grid.revealed).toEqual(['Value']);
        // Note is the grid's own: nothing that way.
        await scope.pointArrow(sheet, { direction: 'right' });
        expect(sheet.text).toBe('=Positions[PV]');
        expect(sheet.refusals).toEqual([]);
    });

    it('Part B of the ninth run, Q52 / SH-35: after a header press ↑ is an edge, and Shift and Ctrl with an arrow are refused as from a cell', async () => {
        expect.hasAssertions();
        const { scope, sheet, declaration } = page();
        await declaration.onPress({ kind: 'columnHeader', column: 'Value' });
        await scope.pointArrow(sheet, { direction: 'up' });
        await scope.pointArrow(sheet, { direction: 'left', extends: true });
        await scope.pointArrow(sheet, { direction: 'left', toEdge: true });
        expect(sheet.text).toBe('=Positions[PV]');
        expect(sheet.reasons).toEqual(['severalCells', 'dataEdge']);
    });

    it('Part B of the ninth run, Q52 / SH-35: a first row whose key is blank, or that has not arrived, is refused: the column stays written and dashed', async () => {
        expect.hasAssertions();
        for (const [first, reason] of [[{ id: undefined, book: 'FX', pv: 1, note: '' }, 'blankKey'], [undefined, 'rowNotArrived']] as const) {
            const { scope, sheet, declaration, grid } = page('=', undefined, [first, ...positions]);
            await declaration.onPress({ kind: 'columnHeader', column: 'Value' });
            await scope.pointArrow(sheet, { direction: 'down' });
            expect(sheet.text).toBe('=Positions[PV]');
            expect(FakeGrid.dashedIn(grid, declaration)).toBe('Value');
            expect(sheet.reasons).toEqual([reason]);
        }
    });

    it('Settled while building ticket 41 / SH-35: after a header press, a column the grid no longer shows writes nothing, and the column is named', async () => {
        expect.hasAssertions();
        const { scope, sheet, declaration, grid } = page();
        await declaration.onPress({ kind: 'columnHeader', column: 'Value' });
        grid.columns = ['Id', 'Book'];
        await scope.pointArrow(sheet, { direction: 'down' });
        expect(sheet.text).toBe('=Positions[PV]');
        expect(sheet.refusals).toEqual([{ reason: 'cellNotHeld', message: sheetWords().pointingRefused('cellNotHeld', 'Positions', 'PV', false) }]);
    });

    it('DC-55: a declaration taken by no grid answers every step as not held', async () => {
        expect.hasAssertions();
        const scope = new PointingScope();
        const sheet = new FakeSheet('=');
        const declaration = scope.registerGrid('Positions', tableRow, { Value: 'PV' });
        scope.join(sheet);
        scope.points(sheet, true);
        await pressCell(declaration, 'R-1', 'Value');
        await scope.pointArrow(sheet, { direction: 'down' });
        expect(sheet.reasons).toEqual(['cellNotHeld']);
    });
});
