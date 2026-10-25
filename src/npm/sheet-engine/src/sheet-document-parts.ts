// The parts a Sheet Document records (C#'s SheetDocumentCell, SheetDocumentAxisFormat, SheetDocumentColumnWidth and
// SheetDocumentTable in SheetDocument.cs; ADR-0046, ADR-0047, ADR-0048, ADR-0049, ADR-0071).
import type { CellAddress } from './cell-address';
import type { CellBorders, CellFill, CellFont, HorizontalAlignment } from './cell-format';
import { AxisFormat } from './cell-format-change';
import type { Entry } from './entry';
import type { NumberFormat } from './number-format';
import type { SheetColumnWidthKind } from './sheet-change';

/** One cell of a Sheet Document: where it is, its Entry and its own Cell Format. Never its Value (ADR-0048). */
export class SheetDocumentCell {
    readonly address: CellAddress;

    /** What the user put into it; `undefined` for a cell that holds only a Cell Format. */
    readonly entry: Entry | undefined;

    /** The Number Format the cell records itself; `undefined` when it takes its row's or column's (ADR-0047). */
    readonly numberFormat: NumberFormat | undefined;

    readonly alignment: HorizontalAlignment | undefined;

    readonly font: CellFont | undefined;

    readonly fill: CellFill | undefined;

    readonly borders: CellBorders | undefined;

    constructor(
        address: CellAddress,
        entry: Entry | undefined,
        numberFormat?: NumberFormat,
        alignment?: HorizontalAlignment,
        font?: CellFont,
        fill?: CellFill,
        borders?: CellBorders,
    ) {
        this.address = address;
        this.entry = entry;
        this.numberFormat = numberFormat;
        this.alignment = alignment;
        this.font = font;
        this.fill = fill;
        this.borders = borders;
    }
}

/**
 * A Cell Format recorded on whole columns or whole rows, as a Sheet Document records it: one entry for a run of
 * adjacent columns (rows) recorded alike, never one per cell (ADR-0047, ADR-0071).
 */
export class SheetDocumentAxisFormat {
    /** The first column (row) of the run, from 0. */
    readonly first: number;

    /** The last column (row) of the run. */
    readonly last: number;

    readonly numberFormat: NumberFormat | undefined;

    readonly alignment: HorizontalAlignment | undefined;

    readonly font: CellFont | undefined;

    readonly fill: CellFill | undefined;

    readonly borders: CellBorders | undefined;

    constructor(
        first: number,
        last: number,
        numberFormat: NumberFormat | undefined,
        alignment: HorizontalAlignment | undefined,
        font: CellFont | undefined,
        fill: CellFill | undefined,
        borders: CellBorders | undefined,
    ) {
        this.first = first;
        this.last = last;
        this.numberFormat = numberFormat;
        this.alignment = alignment;
        this.font = font;
        this.fill = fill;
        this.borders = borders;
    }

    /** The level each row (column) of the run records. @internal */
    get level(): AxisFormat {
        return new AxisFormat(this.numberFormat, this.alignment, this.font, this.fill, this.borders);
    }
}

/** A width recorded on columns, as a Sheet Document records it: one entry for a run of adjacent columns of one width and one kind (ADR-0046). */
export class SheetDocumentColumnWidth {
    readonly first: number;

    readonly last: number;

    /** The width, in characters of the default font (Excel's unit, ADR-0047). */
    readonly width: number;

    readonly kind: SheetColumnWidthKind;

    constructor(
        first: number,
        last: number,
        width: number,
        kind: SheetColumnWidthKind = 'setByUser',
    ) {
        this.first = first;
        this.last = last;
        this.width = width;
        this.kind = kind;
    }

    /** Whether Excel's file marks the width custom: every recorded width is. */
    get isCustom(): boolean {
        return true;
    }
}

/** A Linked Table's declaration as a Sheet Document records it: its name, its column names and its key, never its rows (ADR-0049). */
export class SheetDocumentTable {
    /** The key's columns: one, several for a key of several columns (version 9; ADR-0058), or none. */
    readonly keyColumns: readonly string[];

    readonly name: string;

    readonly columns: readonly string[];

    constructor(name: string, columns: readonly string[], keyColumns: readonly string[] = []) {
        this.name = name;
        this.columns = columns;
        this.keyColumns = keyColumns;
    }

    /** The key column when the key is one column; `undefined` for none, and for a key of several columns. */
    get key(): string | undefined {
        return this.keyColumns.length === 1 ? this.keyColumns[0] : undefined;
    }
}

/** A Sheet Document that cannot be read: an unknown version, or anything the version does not define (ADR-0048). */
export class SheetDocumentError extends Error {
    /** The version the document declared, when that is why it was refused. */
    readonly documentVersion: number | undefined;

    constructor(message: string, options?: { cause?: unknown; documentVersion?: number }) {
        super(message, options?.cause === undefined ? undefined : { cause: options.cause });
        this.name = 'SheetDocumentError';
        this.documentVersion = options?.documentVersion;
    }
}
