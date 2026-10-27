// A Pointing Scope (ADR-0058), the counterpart of C#'s PointingScope.cs: the Sheets and the grids a Consumer
// groups so that a Formula edited in one of the Sheets can point at the grids' cells. What a press or an arrow
// writes, and which it refuses, is decided here; the grid only names the shape a press landed on and answers a
// step, and the Sheet only writes the text into its edit.
//
// C#'s grid side of this — GridPointedAt and the records it hands over — belongs to ExGrid's Cells, which
// @ex-grid/core does not have yet. It is declared below, in the same shape, so the Scope's decisions run
// without it; when core gains it, these move there and the Scope imports them.

import type { GridDirection, ReferenceColour } from '@ex-grid/core';
import { lookupFinds, lookupText, structuredReferenceText } from '@ex-grid/sheet-engine';
import type { LinkedTable, LinkedTableColumn, Value } from '@ex-grid/sheet-engine';
import { defined, elementAt } from './must';
import type { PointingRefusal, PointingRefusalReason } from './pointing-refusal';
import { sheetWords, type SheetWords } from './sheet-words';

export type { ReferenceColour };

/**
 * What a press handed over by a grid pointed at from outside landed on (ADR-0058). The grid names the shape and
 * decides nothing about it:
 *
 * - `cell`: one cell, pressed without Shift; the press names its row and column.
 * - `columnHeader`: a column's own header, pressed without Shift; the press names the column.
 * - `severalCells`: a Shift+press on a cell, a press dragged onto another cell, or a press on a Row Heading or
 *   on the corner.
 * - `severalColumns`: a Shift+press on a column's header, or one dragged onto another column.
 * - `headerGroup`: a Header Group's rectangle (ADR-0032).
 */
export type GridPointedPressKind = 'cell' | 'columnHeader' | 'severalCells' | 'severalColumns' | 'headerGroup';

/**
 * What a step from a cell or a column of a grid pointed at from outside reached (ADR-0058; DC-55):
 *
 * - `cell`: a cell, named by the step's row and column; down from a column, the column's first row.
 * - `edge`: nothing that way; nothing moves.
 * - `rowNotArrived`: the row one step up or down, or a column's first row, has not arrived.
 * - `notHeld`: the grid does not hold the cell or the column to step from, is not pointed at, or takes no
 *   declaration.
 * - `column`: a whole column, as a step left or right from a column reaches.
 */
export type GridPointedStepKind = 'cell' | 'edge' | 'rowNotArrived' | 'notHeld' | 'column';

/**
 * Where an open edit stands with respect to Point (ADR-0051, ADR-0058): `none`, no Reference can go at the
 * caret; `inPoint`, one can; `writtenFromOutside`, what stands at the caret was written by a press outside the
 * grid.
 */
export type PointState = 'none' | 'inPoint' | 'writtenFromOutside';

/** An arrow key in a Sheet while what a press on a registered grid wrote stands in its edit (ADR-0058, "The
 * keyboard"): its direction, and whether Shift (`extends`) or the Primary Modifier (`toEdge`) was held. */
export interface GridPointArrow {
    /** Which way the arrow points. */
    readonly direction: GridDirection;
    /** Shift was held: refused as a range is. */
    readonly extends?: boolean;
    /** The Primary Modifier was held: refused, since where the data ends is not known to the grid. */
    readonly toEdge?: boolean;
}

/** A press a grid pointed at from outside hands over (ADR-0058). */
export interface GridPointedPress<TRow> {
    /** What the press landed on. */
    readonly kind: GridPointedPressKind;
    /** The pressed row, for a cell; `undefined` for a row that has not arrived, and for other shapes. */
    readonly row?: TRow;
    /** The pressed grid column's name, for a cell or a column header. */
    readonly column?: string;
    /** Whether this hands over a drag that began with a press already handed over. */
    readonly dragged?: boolean;
}

/** Where a step from a cell or a column of a grid pointed at from outside landed (ADR-0058; DC-55). */
export interface GridPointedStep<TRow> {
    /** What the step reached. */
    readonly kind: GridPointedStepKind;
    /** The row reached, for `cell`. */
    readonly row?: TRow;
    /** The grid column reached, for `cell`, `column` and `rowNotArrived`. */
    readonly column?: string;
}

/** The dashes over the cell or the column a press wrote for (ADR-0058, "What is drawn"): over `column`'s cell in
 * the row `row` answers true for, or down the column's body when `row` is `undefined`. */
export interface GridPointDashes<TRow> {
    /** Which painted row is the dashed one; `undefined` for dashes down the column. */
    readonly row: ((row: TRow) => boolean) | undefined;
    /** The grid column's name. */
    readonly column: string;
}

/** A grid column outlined in a Reference colour while a Formula reads it (ADR-0057). */
export interface OutlinedColumn {
    /** The grid column's name. */
    readonly column: string;
    /** Its outline's colour. */
    readonly colour: ReferenceColour;
}

/** A Linked Table column a Formula edited in a Sheet reads, and the colour its References wear (ADR-0057). */
export interface LinkedColumnColour {
    /** The table and column. */
    readonly column: LinkedTableColumn;
    /** The colour. */
    readonly colour: ReferenceColour;
}

/** What a grid that takes a {@link GridPointedAt} answers for it: a step from a cell or a column, and scrolling
 * a cell or a column into view (DC-55). */
export interface PointedAtGrid<TRow> {
    /** The cell one step from the cell in the row `isRow` finds, in `column`, in the grid's current order;
     * left and right pass over the grid columns `isColumn` refuses. */
    step(isRow: (row: TRow) => boolean, column: string, direction: GridDirection, isColumn: (column: string) => boolean): Promise<GridPointedStep<TRow>>;
    /** What one step from `column`, as a column, reaches: its first row down, or another column left or right. */
    stepFromColumn(column: string, direction: GridDirection, isColumn: (column: string) => boolean): Promise<GridPointedStep<TRow>>;
    /** Scrolls the cell in the row `isRow` finds, in `column`, into view; false when the grid holds no such row. */
    reveal(isRow: (row: TRow) => boolean, column: string): Promise<boolean>;
    /** Scrolls `column` into view across only; false when the grid shows no such column. */
    revealColumn(column: string): Promise<boolean>;
}

/**
 * A grid's declaration that it is pointed at from outside (ADR-0058), the counterpart of C#'s
 * `GridPointedAt<TRow>`: what the Scope tells the grid to paint, and how the grid hands over a press. The grid
 * that takes it attaches itself with {@link GridPointedAt.attach} to answer steps, and hears each change through
 * {@link GridPointedAt.onChanged}.
 */
export class GridPointedAt<TRow> {
    readonly #listeners = new Set<() => void>();
    #isPointedAt = false;
    #pointingRootId: string | undefined;
    #dashes: GridPointDashes<TRow> | undefined;
    #outlinedColumns: readonly OutlinedColumn[] | undefined;
    #grid: PointedAtGrid<TRow> | undefined;

    /** Where the grid hands each press while it is pointed at. */
    readonly onPress: (press: GridPointedPress<TRow>) => Promise<void>;

    /** A declaration whose presses go to `onPress`. */
    constructor(onPress: (press: GridPointedPress<TRow>) => Promise<void>) {
        this.onPress = onPress;
    }

    /** Whether the grid is pointed at now: a press on it moves neither focus nor its Selection, and is handed over. */
    get isPointedAt(): boolean {
        return this.#isPointedAt;
    }

    set isPointedAt(value: boolean) {
        if (this.#isPointedAt === value) return;
        this.#isPointedAt = value;
        this.#changed();
    }

    /** The id of the root of the Sheet that points, which the grid names while it is pointed at (ADR-0058, "On a
     * circuit"); `undefined` before one has pointed. */
    get pointingRootId(): string | undefined {
        return this.#pointingRootId;
    }

    set pointingRootId(value: string | undefined) {
        if (this.#pointingRootId === value) return;
        this.#pointingRootId = value;
        this.#changed();
    }

    /** The dashes over the cell or the column a press wrote for, or `undefined`. */
    get dashes(): GridPointDashes<TRow> | undefined {
        return this.#dashes;
    }

    set dashes(value: GridPointDashes<TRow> | undefined) {
        if (this.#dashes === value) return;
        this.#dashes = value;
        this.#changed();
    }

    /** The grid columns outlined in Reference colours while a Formula reads them, or `undefined`. A new list is
     * the change signal: the same list set again changes nothing. */
    get outlinedColumns(): readonly OutlinedColumn[] | undefined {
        return this.#outlinedColumns;
    }

    set outlinedColumns(value: readonly OutlinedColumn[] | undefined) {
        if (this.#outlinedColumns === value) return;
        this.#outlinedColumns = value;
        this.#changed();
    }

    /** Calls `listener` after each change above; the function returned stops it. */
    onChanged(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => {
            this.#listeners.delete(listener);
        };
    }

    /** The grid that takes this declaration attaches itself to answer steps; the function returned detaches it. */
    attach(grid: PointedAtGrid<TRow>): () => void {
        this.#grid = grid;
        return () => {
            if (this.#grid === grid) this.#grid = undefined;
        };
    }

    /** Asks the grid for the cell one step from the one in the row `isRow` finds (DC-55); `notHeld` with no grid. */
    async step(isRow: (row: TRow) => boolean, column: string, direction: GridDirection, isColumn: (column: string) => boolean): Promise<GridPointedStep<TRow>> {
        named(column);
        return this.#grid === undefined ? { kind: 'notHeld' } : this.#grid.step(isRow, column, direction, isColumn);
    }

    /** Asks the grid what one step from `column`, as a column, reaches (DC-55); `notHeld` with no grid. */
    async stepFromColumn(column: string, direction: GridDirection, isColumn: (column: string) => boolean): Promise<GridPointedStep<TRow>> {
        named(column);
        return this.#grid === undefined ? { kind: 'notHeld' } : this.#grid.stepFromColumn(column, direction, isColumn);
    }

    /** Asks the grid to scroll the cell into view; false with no grid. */
    async reveal(isRow: (row: TRow) => boolean, column: string): Promise<boolean> {
        named(column);
        return this.#grid === undefined ? false : this.#grid.reveal(isRow, column);
    }

    /** Asks the grid to scroll the column into view across only; false with no grid. */
    async revealColumn(column: string): Promise<boolean> {
        named(column);
        return this.#grid === undefined ? false : this.#grid.revealColumn(column);
    }

    #changed(): void {
        for (const listener of this.#listeners) listener();
    }
}

/** What a Pointing Scope asks of a Sheet in it (ADR-0058): its own declarations, to read a table's columns and
 * key from, and its edit, to write into. */
export interface PointingSheet {
    /** The Linked Tables the Sheet declares, keys included. */
    readonly linkedTables: readonly LinkedTable[];
    /** The id of the root of the Sheet's grid; `undefined` before it is rendered. */
    readonly rootId: string | undefined;
    /** Writes text into the Sheet's open edit where Point writes; false when it is no longer in Point. */
    writePointedText(text: string): Promise<boolean>;
    /** Takes back what the last write put there, while it stands. */
    takeBackPointedText(): Promise<boolean>;
    /** Tells the Sheet's Consumer why a press wrote nothing. */
    tellPointingRefused(refusal: PointingRefusal): Promise<void>;
}

/** A row of a registered grid read as the table's row: one Value per declared column, `undefined` for a blank. */
export type TableRow = readonly (Value | undefined)[];

/** Where a press's dashes stand in its grid, read as the table's rows. */
interface PointDashes {
    readonly column: string;
    readonly isRow: ((row: TableRow) => boolean) | undefined;
}

interface Dashed {
    readonly grid: GridMember;
    readonly sheet: PointingSheet;
    readonly where: PointDashes;
}

interface Step {
    readonly kind: GridPointedStepKind;
    readonly row: (() => TableRow) | undefined;
    readonly column: string | undefined;
}

/** A grid registered in the Scope, whatever its row type. */
interface GridMember {
    readonly table: string;
    tableColumnOf(gridColumn: string): string;
    outline(told: readonly (readonly [PointingSheet, readonly LinkedColumnColour[]])[]): void;
    declarePointedAt(pointing: PointingSheet | undefined): void;
    showDashes(dashes: PointDashes | undefined): void;
    step(from: PointDashes, direction: GridDirection, isColumn: (column: string) => boolean): Promise<Step>;
    stepFromColumn(column: string, direction: GridDirection, isColumn: (column: string) => boolean): Promise<Step>;
    reveal(dashes: PointDashes): Promise<void>;
}

/**
 * A Pointing Scope (ADR-0058): the Sheets and the grids a Consumer groups so that a Formula edited in one of the
 * Sheets can point at the grids' cells. Pointing at a grid of the Scope writes what reads the pressed cell by its
 * row's key, `XLOOKUP("R-4471", Positions[Id], Positions[PV])`, or the pressed column's structured reference,
 * `Positions[PV]` — never a position, so the Formula reads the same row after the grid is sorted (ADR-0049,
 * rule 2).
 *
 * The Consumer makes one per group and registers each grid that shows a Linked Table with
 * {@link PointingScope.registerGrid}. A Sheet of the Scope joins it, says whether it points, tells the columns
 * its Formula reads and where its edit stands with respect to Point, and hands over its arrow keys; the Scope
 * writes into the Sheet that points, or tells it why a press wrote nothing. Only the Sheet that holds the
 * keyboard points, and a Sheet is never pointed at.
 */
export class PointingScope {
    readonly #words: SheetWords;
    readonly #sheets: PointingSheet[] = [];
    readonly #grids: GridMember[] = [];
    #pointing: PointingSheet | undefined;
    #pointedLast: PointingSheet | undefined;
    // The grid whose press wrote what the pointing Sheet's Point holds: a drag from that press takes it back.
    #wrote: GridMember | undefined;
    // What each Sheet last told of the Linked Table columns its Formula reads, the latest last.
    readonly #linkedColumns: (readonly [PointingSheet, readonly LinkedColumnColour[]])[] = [];
    #dashed: Dashed | undefined;
    #dashedBeforeWrite: Dashed | undefined;

    /** A Scope whose refusals are worded by `words`: ExSheet's English unless a Consumer's words are given. */
    constructor(words: SheetWords = sheetWords()) {
        this.#words = words;
    }

    /**
     * Registers a grid that shows a Linked Table, and gives back the declaration to pass it (ADR-0058).
     *
     * @param table - The Linked Table's name, as the Sheets declare it; matched without regard to case.
     * @param tableRow - A grid row as the table's row: one Value per declared column, in the declared order, a
     *   blank as `undefined`. Asked for a pressed cell's row and, while that cell is dashed, for the rows the grid
     *   paints: it must be cheap and must not throw.
     * @param tableColumns - Which table column each grid column is, by the grid column's name, for the grid
     *   columns named otherwise than their table column. A grid column with no entry is the table column of its
     *   own name, if the table has one.
     * @throws RangeError when the table's name is empty, or an entry of `tableColumns` names no column.
     */
    registerGrid<TRow>(table: string, tableRow: (row: TRow) => TableRow, tableColumns?: Readonly<Record<string, string>>): GridPointedAt<TRow> {
        if (table.length === 0) throw new RangeError('A registered grid names the Linked Table it shows (ADR-0058).');
        const columns = new Map<string, string>();
        for (const [gridColumn, tableColumn] of Object.entries(tableColumns === undefined ? {} : tableColumns)) {
            if (gridColumn.length === 0 || tableColumn.length === 0)
                throw new RangeError('A grid column and the table column it is are both named (ADR-0058).');
            columns.set(gridColumn, tableColumn);
        }
        const [grid, declaration] = gridMember(table, tableRow, columns, async (...press) => this.#press(...press));
        this.#grids.push(grid);
        grid.declarePointedAt(this.#pointing);
        grid.outline(this.#linkedColumns);
        return declaration;
    }

    /** A Sheet given this Scope joins it. */
    join(sheet: PointingSheet): void {
        if (!this.#sheets.includes(sheet)) this.#sheets.push(sheet);
    }

    /** A Sheet no longer given this Scope, or disposed, leaves it; if it pointed, no grid is pointed at. */
    leave(sheet: PointingSheet): void {
        this.points(sheet, false);
        const at = this.#sheets.indexOf(sheet);
        if (at >= 0) this.#sheets.splice(at, 1);
        if (this.#pointedLast === sheet) this.#pointedLast = undefined;
        this.pointStateChanged(sheet, 'none');
        this.outlineLinkedColumns(sheet, []);
    }

    /** A Sheet of the Scope says whether it points: it holds the keyboard, has an edit open, and is in Point. A
     * Sheet that stops pointing after another started changes nothing. */
    points(sheet: PointingSheet, points: boolean): void {
        if (points) {
            if (!this.#sheets.includes(sheet)) return;
            if (this.#pointing !== sheet) this.#wrote = undefined;
            this.#pointing = sheet;
            this.#pointedLast = sheet;
            this.#declarePointedAt();
        } else if (this.#pointing === sheet) {
            this.#pointing = undefined;
            this.#wrote = undefined;
            this.#declarePointedAt();
        }
    }

    /** A Sheet of the Scope tells the Linked Table columns the Formula it edits reads, and their colours, or none
     * once the edit ends (ADR-0057); each registered grid outlines those of its table, whether or not the Sheet
     * points. */
    outlineLinkedColumns(sheet: PointingSheet, columns: readonly LinkedColumnColour[]): void {
        const told = this.#linkedColumns.findIndex(([teller]) => teller === sheet);
        if (told >= 0) this.#linkedColumns.splice(told, 1);
        if (columns.length > 0 && this.#sheets.includes(sheet)) this.#linkedColumns.push([sheet, columns]);
        else if (told < 0) return;
        for (const grid of this.#grids) grid.outline(this.#linkedColumns);
    }

    /** A Sheet of the Scope tells where its edit stands with respect to Point. Anything but `writtenFromOutside`
     * means Point over what a press wrote has ended, and its dashes go (ADR-0058, "What is drawn"). */
    pointStateChanged(sheet: PointingSheet, state: PointState): void {
        if (state === 'writtenFromOutside') return;
        if (this.#dashedBeforeWrite?.sheet === sheet) this.#dashedBeforeWrite = undefined;
        if (this.#dashed?.sheet === sheet) this.#dash(undefined);
    }

    /**
     * An arrow key in a Sheet of the Scope while what a press on a registered grid wrote stands in its edit
     * (ADR-0058, "The keyboard"): the cell or the column that press wrote for moves one step inside that grid,
     * and the text is rewritten for what it reached. At an edge nothing moves; a row that has not arrived, Shift
     * and an arrow, and the Primary Modifier and an arrow write nothing, and the reason is told.
     */
    async pointArrow(sheet: PointingSheet, arrow: GridPointArrow): Promise<void> {
        const dashed = this.#dashed;
        if (this.#pointing !== sheet || dashed === undefined || dashed.sheet !== sheet) return;
        const grid = dashed.grid;
        if (arrow.toEdge === true) {
            await sheet.tellPointingRefused(this.#refusal('dataEdge', grid.table, undefined, false));
            return;
        }
        if (arrow.extends === true) {
            await sheet.tellPointingRefused({ reason: 'severalCells', message: this.#words.pointingExtendedByKey(grid.table) });
            return;
        }
        const table = findTable(sheet, grid.table);
        if (table === undefined) {
            await this.#refuse(sheet, 'tableNotDeclared', grid.table, undefined, false);
            return;
        }
        const isTableColumn = (gridColumn: string): boolean => findColumn(table, grid.tableColumnOf(gridColumn)) !== undefined;
        const fromColumn = dashed.where.isRow === undefined;
        const step = fromColumn
            ? await grid.stepFromColumn(dashed.where.column, arrow.direction, isTableColumn)
            : await grid.step(dashed.where, arrow.direction, isTableColumn);
        switch (step.kind) {
            case 'edge':
                return;
            case 'rowNotArrived':
                await this.#refuse(sheet, 'rowNotArrived', table.name, undefined, false);
                return;
            case 'notHeld': {
                // From a column, the column is named: it is what the grid no longer shows.
                const notShown = fromColumn ? orOwn(findColumn(table, grid.tableColumnOf(dashed.where.column)), dashed.where.column) : undefined;
                await this.#refuse(sheet, 'cellNotHeld', table.name, notShown, false);
                return;
            }
            case 'cell':
            case 'column':
                break;
        }
        const column = defined(step.column, 'the grid column a step reached');
        const tableColumn = findColumn(table, grid.tableColumnOf(column));
        if (tableColumn === undefined) {
            await this.#refuse(sheet, 'columnNotInTable', table.name, column, false);
            return;
        }
        const written = step.kind === 'column' ? columnReference(table, tableColumn, column) : cellLookup(table, tableColumn, column, step.row);
        if (written.kind === 'refused') {
            await this.#refuse(sheet, written.reason, table.name, tableColumn, false);
            return;
        }
        if (!(await sheet.writePointedText(written.text))) {
            await this.#refuse(sheet, 'notPointing', table.name, tableColumn, false);
            return;
        }
        // A gesture of its own: a drag from the press that began this no longer takes anything back.
        this.#wrote = undefined;
        this.#dashedBeforeWrite = undefined;
        this.#dash({ grid, sheet, where: written.dashes });
        await grid.reveal(written.dashes);
    }

    /** A press a registered grid handed over: written into the pointing Sheet's edit, or refused with the
     * reason; a drag takes back what its press wrote. */
    async #press(grid: GridMember, kind: GridPointedPressKind, row: (() => TableRow) | undefined, column: string | undefined, dragged: boolean): Promise<void> {
        // The drag shows the user meant a range, so the text goes back to what it was before the press. Any other
        // press is a gesture of its own, and forgets which press wrote last.
        const writer = this.#pointing;
        const tookBack = dragged && this.#wrote === grid && writer !== undefined && await writer.takeBackPointedText();
        this.#wrote = undefined;
        if (tookBack) this.#dash(this.#dashedBeforeWrite);
        this.#dashedBeforeWrite = undefined;
        const sheet = this.#pointing;
        if (sheet === undefined) {
            // Handed over by a grid still painted pointed at, after the Sheet stopped pointing.
            const last = this.#pointedLast;
            if (last !== undefined) await last.tellPointingRefused(this.#refusal('notPointing', grid.table, undefined, false));
            return;
        }
        switch (kind) {
            case 'severalCells':
                await this.#refuse(sheet, 'severalCells', grid.table, undefined, tookBack);
                return;
            case 'severalColumns':
                await this.#refuse(sheet, 'severalColumns', grid.table, undefined, tookBack);
                return;
            case 'headerGroup':
                await this.#refuse(sheet, 'headerGroup', grid.table, undefined, false);
                return;
            case 'cell':
            case 'columnHeader':
                break;
        }
        const table = findTable(sheet, grid.table);
        if (table === undefined) {
            await this.#refuse(sheet, 'tableNotDeclared', grid.table, undefined, false);
            return;
        }
        const pressed = defined(column, 'the grid column a press on a cell or a column header names');
        const tableColumn = findColumn(table, grid.tableColumnOf(pressed));
        if (tableColumn === undefined) {
            await this.#refuse(sheet, 'columnNotInTable', table.name, pressed, false);
            return;
        }
        const written = kind === 'columnHeader' ? columnReference(table, tableColumn, pressed) : cellLookup(table, tableColumn, pressed, row);
        if (written.kind === 'refused') {
            await this.#refuse(sheet, written.reason, table.name, tableColumn, false);
            return;
        }
        const dashedBefore = this.#dashed;
        if (!(await sheet.writePointedText(written.text))) {
            // The Sheet's edit left Point while the press was on its way.
            await this.#refuse(sheet, 'notPointing', table.name, tableColumn, false);
            return;
        }
        this.#wrote = grid;
        this.#dashedBeforeWrite = dashedBefore?.sheet === sheet ? dashedBefore : undefined;
        this.#dash({ grid, sheet, where: written.dashes });
    }

    #declarePointedAt(): void {
        for (const grid of this.#grids) grid.declarePointedAt(this.#pointing);
    }

    #dash(dashed: Dashed | undefined): void {
        const shown = this.#dashed;
        if (shown !== undefined && shown.grid !== dashed?.grid) shown.grid.showDashes(undefined);
        this.#dashed = dashed;
        if (dashed !== undefined) dashed.grid.showDashes(dashed.where);
    }

    async #refuse(sheet: PointingSheet, reason: PointingRefusalReason, table: string, column: string | undefined, tookBack: boolean): Promise<void> {
        await sheet.tellPointingRefused(this.#refusal(reason, table, column, tookBack));
    }

    #refusal(reason: PointingRefusalReason, table: string, column: string | undefined, tookBack: boolean): PointingRefusal {
        return { reason, message: this.#words.pointingRefused(reason, table, column, tookBack) };
    }
}

type Written =
    | { readonly kind: 'written'; readonly text: string; readonly dashes: PointDashes }
    | { readonly kind: 'refused'; readonly reason: PointingRefusalReason };

/** What a column writes, `T[<column>]`, and the dashes down its body. */
function columnReference(table: LinkedTable, tableColumn: string, gridColumn: string): Written {
    return { kind: 'written', text: structuredReferenceText(table.name, tableColumn), dashes: { column: gridColumn, isRow: undefined } };
}

/**
 * What a cell writes, `XLOOKUP(<key>, T[<key column>], T[<column>])`, or for a key of several columns
 * `XLOOKUP(1, (T[K1]=v1)*(T[K2]=v2), T[<column>])` (ADR-0058, amended 2026-10-03), and the dashes that find it by
 * its row's key; or the reason nothing is written.
 */
function cellLookup(table: LinkedTable, tableColumn: string, gridColumn: string, row: (() => TableRow) | undefined): Written {
    if (row === undefined) return { kind: 'refused', reason: 'rowNotArrived' };
    const keyColumns = table.keyColumns;
    if (keyColumns.length === 0) return { kind: 'refused', reason: 'noKey' };
    const values = row();
    if (values.length !== table.columns.length) {
        throw new RangeError(
            `The row a grid registered for '${table.name}' gave has ${values.length} Values, and the table declares ${table.columns.length} columns: `
            + "registerGrid's tableRow gives one Value per declared column, in the declared order, as a push does (ADR-0058).");
    }
    const keyIndexes = keyColumns.map(key => indexOf(table, key));
    const keys: Value[] = [];
    for (const index of keyIndexes) {
        const key = values[index];
        if (key === undefined) return { kind: 'refused', reason: 'blankKey' };
        if (key.isError) return { kind: 'refused', reason: 'keyIsAnError' };
        keys.push(key);
    }
    const width = table.columns.length;
    return {
        kind: 'written',
        text: lookupText(table.name, keyColumns, keys, tableColumn),
        dashes: {
            column: gridColumn,
            isRow: candidate => candidate.length === width && lookupFinds(keys, keyIndexes.map(index => candidate[index])),
        },
    };
}

/** The pointing Sheet's declaration of the table, found as a Formula finds a table: without regard to case. */
function findTable(sheet: PointingSheet, name: string): LinkedTable | undefined {
    return sheet.linkedTables.find(table => sameName(table.name, name));
}

/** The table's column of that name, as declared, found without regard to case. */
function findColumn(table: LinkedTable, name: string): string | undefined {
    return table.columns.find(column => sameName(column, name));
}

function indexOf(table: LinkedTable, column: string): number {
    const at = table.columns.findIndex(declared => sameName(declared, column));
    if (at < 0) throw new RangeError(`The key '${column}' is not one of the columns of '${table.name}'.`);
    return at;
}

function sameName(a: string, b: string): boolean {
    return a.toUpperCase() === b.toUpperCase();
}

function orOwn(found: string | undefined, own: string): string {
    return found === undefined ? own : found;
}

function named(column: string): void {
    if (column.length === 0) throw new RangeError('A grid column is named (ADR-0058).');
}

/** Hands a press over to the Scope. */
type HandOver = (grid: GridMember, kind: GridPointedPressKind, row: (() => TableRow) | undefined, column: string | undefined, dragged: boolean) => Promise<void>;

/** A registered grid of rows of `TRow`, and the declaration that hands each press to the Scope. */
function gridMember<TRow>(
    table: string, tableRow: (row: TRow) => TableRow, tableColumns: ReadonlyMap<string, string>, handOver: HandOver,
): readonly [GridMember, GridPointedAt<TRow>] {
    const tableColumnOf = (gridColumn: string): string => orOwn(tableColumns.get(gridColumn), gridColumn);
    // The grid columns that are a table column: each given it as its entry, and the one of its own name unless
    // that one is given another.
    const gridColumnsOf = (tableColumn: string): string[] => {
        const found = [...tableColumns].filter(([, entry]) => sameName(entry, tableColumn)).map(([gridColumn]) => gridColumn);
        if (!tableColumns.has(tableColumn)) found.push(tableColumn);
        return found;
    };
    const asTableRow = (isRow: (row: TableRow) => boolean) => (row: TRow): boolean => isRow(tableRow(row));
    const read = (step: GridPointedStep<TRow>): Step => {
        const reached = step.row;
        return { kind: step.kind, row: reached === undefined ? undefined : () => tableRow(reached), column: step.column };
    };
    const declaration = new GridPointedAt<TRow>(async press => {
        const pressed = press.row;
        await handOver(member, press.kind, pressed === undefined ? undefined : () => tableRow(pressed), press.column, press.dragged === true);
    });
    const member: GridMember = {
        table,
        tableColumnOf,
        outline(told) {
            const outlined: OutlinedColumn[] = [];
            for (const [, columns] of told) {
                for (const { column, colour } of columns) {
                    if (!sameName(column.table, table)) continue;
                    for (const gridColumn of gridColumnsOf(column.column)) outlined.push({ column: gridColumn, colour });
                }
            }
            const current = declaration.outlinedColumns;
            const same = outlined.length === 0
                ? current === undefined
                : current !== undefined && current.length === outlined.length
                    && current.every((one, i) => one.column === elementAt(outlined, i).column && one.colour === elementAt(outlined, i).colour);
            if (!same) declaration.outlinedColumns = outlined.length === 0 ? undefined : outlined;
        },
        declarePointedAt(pointing) {
            // The Sheet's root is named first, so the render that paints the grid pointed at names it too.
            if (pointing !== undefined) declaration.pointingRootId = pointing.rootId;
            declaration.isPointedAt = pointing !== undefined;
        },
        showDashes(dashes) {
            const isRow = dashes?.isRow;
            declaration.dashes = dashes === undefined
                ? undefined
                : { row: isRow === undefined ? undefined : asTableRow(isRow), column: dashes.column };
        },
        async step(from, direction, isColumn) {
            const isRow = defined(from.isRow, 'the row of the dashed cell a step starts from');
            return read(await declaration.step(asTableRow(isRow), from.column, direction, isColumn));
        },
        async stepFromColumn(column, direction, isColumn) {
            return read(await declaration.stepFromColumn(column, direction, isColumn));
        },
        async reveal(dashes) {
            const isRow = dashes.isRow;
            await (isRow === undefined ? declaration.revealColumn(dashes.column) : declaration.reveal(asTableRow(isRow), dashes.column));
        },
    };
    return [member, declaration];
}
