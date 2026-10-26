// The Sheet's columns A … XFD as ExGrid takes them (ADR-0046): Text columns whose cells declare their own kind,
// alignment, painted text and appearance (ADR-0050), never queryable, since a Sheet's rows are places and sorting
// them would move what the References name. Their widths are the Sheet's, in Excel's characters, converted to
// the grid's pixels as the painted text converts them (ADR-0047, third round).

import type { CellAlign, CellAppearance, CellTextMeasure, ColumnType, GridColumn, GridDensity } from '@ex-grid/core';
import { cellTextMeasure } from '@ex-grid/core';
import { CellAddress, Sheet, type CellRange } from '@ex-grid/sheet-engine';
import { charactersIn, type FormulaUnreadableWords, type SheetCellText, type SheetRow } from './sheet-row';

/** The narrowest a drag makes a column, as the grid's own default is, unless the Sheet's is narrower. */
const defaultMinWidthPx = 40;

/** A column's width in pixels from its width in characters (ADR-0047, third round). */
export function pxOf(characters: number, metrics: CellTextMeasure): number {
    return Math.round((characters * metrics.digitWidthPx + 2 * metrics.cellHorizontalPaddingPx) * 1e6) / 1e6;
}

/** A column's width in characters from its width in pixels, at most the Sheet's widest; `undefined` for a
 * width that holds no character, which the Sheet does not record (a hidden column is not supported). */
export function charactersOfColumn(columnWidthPx: number, metrics: CellTextMeasure): number | undefined {
    const characters = charactersIn(metrics.contentWidthPx(columnWidthPx), metrics);
    return characters > 0 ? Math.min(characters, Sheet.maxColumnWidth) : undefined;
}

// Each column's index, recorded as the column is made, so a cell's declarations never parse its name again.
const indices = new WeakMap<GridColumn<SheetRow>, number>();

/** The column's index on the Sheet from its name. */
export function indexOf(column: GridColumn<SheetRow>): number {
    const recorded = indices.get(column);
    if (recorded !== undefined) return recorded;
    const index = CellAddress.tryParseColumn(column.name);
    if (index === undefined) throw new RangeError(`'${column.name}' is not one of the Sheet's columns.`);
    return index;
}

const formatText = (value: unknown): string => {
    // The grid formats only what a column's `value` answered, which is a cell's text or a blank.
    if (typeof value !== 'object' || value === null || !('text' in value) || typeof value.text !== 'string') {
        throw new TypeError('A Sheet column formats only the text of one of its cells.');
    }
    return value.text;
};

/** The per-cell declarations the grid asks of a Sheet's rows, one instance each, so they are the same functions
 * on every update (ADR-0003). */
export const sheetCellPaint = Object.freeze({
    cellType: (row: SheetRow, column: GridColumn<SheetRow>): ColumnType => (row.at(indexOf(column))?.isNumber === true ? 'number' : 'text'),
    cellAlign: (row: SheetRow, column: GridColumn<SheetRow>): CellAlign => {
        const shown = row.at(indexOf(column));
        return shown === undefined ? 'auto' : shown.align;
    },
    paintedText: (row: SheetRow, column: GridColumn<SheetRow>, contentWidthPx: number, metrics: CellTextMeasure): string | undefined =>
        row.paintedAt(indexOf(column), contentWidthPx, metrics),
    editorText: (row: SheetRow, column: GridColumn<SheetRow>): string | undefined => row.entryTextAt(indexOf(column)),
    editorAppearance: (row: SheetRow, column: GridColumn<SheetRow>): CellAppearance => row.editorAppearanceAt(indexOf(column)),
    // A cell an array spills into holds no Entry: the bar shows its Anchor's Formula, dimmed (ADR-0125).
    formulaBarBorrowedText: (row: SheetRow, column: GridColumn<SheetRow>): string | undefined => row.spilledFormulaAt(indexOf(column)),
});

/** The appearance lookup for one reading of the Sheet's formatting: a new function, so every row is asked again,
 * when a change reaches cells the engine names no row of (ADR-0071). */
export function appearanceLookup(reading: number): (row: SheetRow, column: GridColumn<SheetRow>) => CellAppearance {
    return (row, column) => row.appearanceAt(indexOf(column), reading);
}

/**
 * The Sheet's columns, each as wide as the Sheet records it. A column object is kept while its width is the
 * same, so an update after an edit hands the grid the same columns.
 */
export class SheetColumns {
    readonly #metrics: CellTextMeasure;
    readonly #unreadable: FormulaUnreadableWords;
    readonly #defaultWidthPx: number;
    // Each column's object at its width: kept while the width holds, replaced when it moves.
    readonly #byIndex = new Map<number, GridColumn<SheetRow>>();
    #columns: readonly GridColumn<SheetRow>[] = [];
    #sheet: Sheet | undefined;

    constructor(density: GridDensity, unreadable: FormulaUnreadableWords) {
        this.#metrics = cellTextMeasure(density);
        this.#unreadable = unreadable;
        // Excel's default column: 8.43 of the grid's digits and the cell's padding on both sides, in whole pixels.
        this.#defaultWidthPx = Math.ceil(pxOf(Sheet.defaultColumnWidth, this.#metrics));
    }

    /** The metrics the columns are sized by. */
    get metrics(): CellTextMeasure { return this.#metrics; }

    /** The columns as the Sheet now records their widths. */
    get all(): readonly GridColumn<SheetRow>[] { return this.#columns; }

    /** Reads every column's width from `sheet`, or only `changed` where it is the Sheet read last; answers
     * whether any column changed. */
    read(sheet: Sheet, changed?: readonly number[]): boolean {
        if (sheet !== this.#sheet || changed === undefined || this.#columns.length === 0) {
            this.#sheet = sheet;
            this.#columns = Array.from({ length: Sheet.columnCount }, (_, index) => this.#column(index, this.#widthPx(sheet, index)));
            return true;
        }
        const columns = [...this.#columns];
        let moved = false;
        for (const index of changed) {
            const column = this.#column(index, this.#widthPx(sheet, index));
            if (column !== columns[index]) {
                columns[index] = column;
                moved = true;
            }
        }
        if (moved) this.#columns = columns;
        return moved;
    }

    /** The column's width in pixels as last read. */
    widthPxOf(index: number): number {
        const column = this.#columns[index];
        if (column === undefined) throw new RangeError(`Internal: no column ${String(index)} has been read.`);
        const { width } = column;
        if (typeof width !== 'object' || !('px' in width)) throw new Error(`Internal: column ${column.name} is not Fixed.`);
        return width.px;
    }

    /**
     * The columns a number widens, as Excel's do (ADR-0047, second round; SH-20): for each cell of `cells` that
     * holds an Entry, in a column the user has not sized, the width its text takes at the width the engine fits it
     * to on entry, where that is wider than the column. Answered in characters, widest per column, by column.
     */
    widenedBy(sheet: Sheet, cells: readonly CellRange[]): readonly (readonly [number, number])[] {
        const needed = new Map<number, number>();
        for (const range of cells) {
            for (const address of sheet.entryAddressesIn(range)) {
                const { column } = address;
                if (sheet.getColumnWidth(column)?.isSetByUser === true) continue;
                const characters = sheet.getWidthOnEntry(address);
                if (characters === undefined) continue;
                const shown = sheet.getDisplay(address, characters);
                const charged = sheet.getFont(address).bold ? this.#metrics.bold : this.#metrics;
                // The column the text takes: its width and the cell's padding on both sides.
                const px = Math.ceil(charged.textWidthPx(shown.text) + 2 * charged.cellHorizontalPaddingPx);
                const widest = needed.get(column);
                if (px > this.widthPxOf(column) && (widest === undefined || px > widest)) needed.set(column, px);
            }
        }
        const widths: (readonly [number, number])[] = [];
        for (const [column, px] of [...needed].toSorted(([a], [b]) => a - b)) {
            const width = charactersOfColumn(px, this.#metrics);
            if (width !== undefined) widths.push([column, width]);
        }
        return widths;
    }

    #widthPx(sheet: Sheet, index: number): number {
        const recorded = sheet.getColumnWidth(index);
        return recorded === undefined ? this.#defaultWidthPx : pxOf(recorded.width, this.#metrics);
    }

    #column(index: number, widthPx: number): GridColumn<SheetRow> {
        const kept = this.#byIndex.get(index);
        if (kept?.width !== undefined && typeof kept.width === 'object' && 'px' in kept.width && kept.width.px === widthPx) return kept;
        const name = CellAddress.columnName(index);
        const unreadable = this.#unreadable;
        const column: GridColumn<SheetRow> = Object.freeze({
            name,
            header: name,
            type: 'text',
            queryable: false,
            editable: true,
            headerAlign: 'center',
            width: Object.freeze({ px: widthPx, minPx: Math.min(defaultMinWidthPx, widthPx) }),
            value: (row: SheetRow): SheetCellText | undefined => row.at(index),
            format: formatText,
            validate: (row: SheetRow, typed: string) => row.judge(typed, unreadable),
        });
        this.#byIndex.set(index, column);
        indices.set(column, index);
        return column;
    }
}
