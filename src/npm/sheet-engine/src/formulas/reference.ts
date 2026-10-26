import { CellAddress, columnCount, rowCount } from '../cell-address';
import { equalsIgnoreCase, isAsciiDigit, isAsciiLetter, isAsciiLetterOrDigit } from '../dotnet';

/** What a Reference names: one cell, a rectangle, whole columns or whole rows. */
export type ReferenceShape = 'cell' | 'area' | 'columns' | 'rows';

/** An inclusive rectangle of cells, zero-based. */
export class Area {
    readonly row1: number;
    readonly column1: number;
    readonly row2: number;
    readonly column2: number;

    constructor(row1: number, column1: number, row2: number, column2: number) {
        this.row1 = row1;
        this.column1 = column1;
        this.row2 = row2;
        this.column2 = column2;
    }

    get isSingleCell(): boolean {
        return this.row1 === this.row2 && this.column1 === this.column2;
    }

    get cellCount(): number {
        return (this.row2 - this.row1 + 1) * (this.column2 - this.column1 + 1);
    }

    get rows(): number {
        return this.row2 - this.row1 + 1;
    }

    get columns(): number {
        return this.column2 - this.column1 + 1;
    }

    contains(address: CellAddress): boolean {
        return address.row >= this.row1 && address.row <= this.row2 && address.column >= this.column1 && address.column <= this.column2;
    }

    equals(other: Area): boolean {
        return this.row1 === other.row1 && this.column1 === other.column1 && this.row2 === other.row2 && this.column2 === other.column2;
    }
}

/** The parts of a Reference, for making one with some of them changed (a C# record's `with`). */
export interface ReferenceParts {
    readonly sheetName: string | undefined;
    readonly shape: ReferenceShape;
    readonly row1: number;
    readonly column1: number;
    readonly row2: number;
    readonly column2: number;
    readonly row1Absolute: boolean;
    readonly column1Absolute: boolean;
    readonly row2Absolute: boolean;
    readonly column2Absolute: boolean;
    /** `A1#`: the Spill Range of the Formula in this one cell (ADR-0125). */
    readonly spilled: boolean;
    /** For a 3D Reference (`Sheet1:Sheet3!B2`, ADR-0331 §7): the name of the Sheet that ends the span, as written. */
    readonly lastSheetName: string | undefined;
}

/**
 * A Reference as a Formula records it. The target positions are held absolutely, with a flag per axis saying
 * whether the text wrote `$`; a relative Reference shifts when its Formula is copied (ADR-0048), and every form
 * survives a structural edit by rewriting the positions. The Sheet qualifier is recorded as written.
 */
export class Reference implements ReferenceParts {
    readonly sheetName: string | undefined;
    readonly shape: ReferenceShape;
    readonly row1: number;
    readonly column1: number;
    readonly row2: number;
    readonly column2: number;
    readonly row1Absolute: boolean;
    readonly column1Absolute: boolean;
    readonly row2Absolute: boolean;
    readonly column2Absolute: boolean;
    readonly spilled: boolean;
    readonly lastSheetName: string | undefined;

    constructor(parts: ReferenceParts) {
        this.sheetName = parts.sheetName;
        this.shape = parts.shape;
        this.row1 = parts.row1;
        this.column1 = parts.column1;
        this.row2 = parts.row2;
        this.column2 = parts.column2;
        this.row1Absolute = parts.row1Absolute;
        this.column1Absolute = parts.column1Absolute;
        this.row2Absolute = parts.row2Absolute;
        this.column2Absolute = parts.column2Absolute;
        this.spilled = parts.spilled;
        this.lastSheetName = parts.lastSheetName;
    }

    /** One cell's Reference. */
    static cell(sheet: string | undefined, atRow: number, atColumn: number, rowAbsolute: boolean, columnAbsolute: boolean): Reference {
        return new Reference({
            sheetName: sheet, shape: 'cell', row1: atRow, column1: atColumn, row2: atRow, column2: atColumn,
            row1Absolute: rowAbsolute, column1Absolute: columnAbsolute, row2Absolute: rowAbsolute, column2Absolute: columnAbsolute,
            spilled: false, lastSheetName: undefined,
        });
    }

    /** This Reference with some of its parts changed. */
    with(changes: Partial<ReferenceParts>): Reference {
        return new Reference({ ...this.#parts(), ...changes });
    }

    #parts(): ReferenceParts {
        return {
            sheetName: this.sheetName, shape: this.shape, row1: this.row1, column1: this.column1, row2: this.row2, column2: this.column2,
            row1Absolute: this.row1Absolute, column1Absolute: this.column1Absolute, row2Absolute: this.row2Absolute, column2Absolute: this.column2Absolute,
            spilled: this.spilled, lastSheetName: this.lastSheetName,
        };
    }

    /** A C# record's equality: every part the same. */
    equals(other: Reference): boolean {
        return this.sheetName === other.sheetName && this.shape === other.shape && this.row1 === other.row1 && this.column1 === other.column1
            && this.row2 === other.row2 && this.column2 === other.column2 && this.row1Absolute === other.row1Absolute
            && this.column1Absolute === other.column1Absolute && this.row2Absolute === other.row2Absolute
            && this.column2Absolute === other.column2Absolute && this.spilled === other.spilled && this.lastSheetName === other.lastSheetName;
    }

    /** Whether this is a 3D Reference, reading one range on every Sheet of a span (ADR-0331 §7). */
    get isThreeD(): boolean {
        return this.lastSheetName !== undefined;
    }

    /** The rectangle the Reference covers. */
    get area(): Area {
        switch (this.shape) {
            case 'columns': return new Area(0, this.column1, rowCount - 1, this.column2);
            case 'rows': return new Area(this.row1, 0, this.row2, columnCount - 1);
            case 'cell':
            case 'area':
                return new Area(this.row1, this.column1, this.row2, this.column2);
            default: return unreachableShape(this.shape);
        }
    }

    /**
     * A Reference to a rectangle, in the shape Excel writes it: one spanning every column is whole rows
     * (`A1:XFD3` is `1:3`, `A:XFD` is `$1:$1048576`), one spanning every row is whole columns (`A1:A1048576`
     * is `A:A`), and any other is an area. The axis a whole form drops is held absolute, as Excel holds it.
     */
    static rectangle(sheet: string | undefined, row1: number, column1: number, row2: number, column2: number,
        row1Absolute: boolean, column1Absolute: boolean, row2Absolute: boolean, column2Absolute: boolean): Reference {
        if (column1 === 0 && column2 === columnCount - 1) {
            return new Reference({
                sheetName: sheet, shape: 'rows', row1, column1: 0, row2, column2, row1Absolute, column1Absolute: true, row2Absolute,
                column2Absolute: true, spilled: false, lastSheetName: undefined,
            });
        }
        if (row1 === 0 && row2 === rowCount - 1) {
            return new Reference({
                sheetName: sheet, shape: 'columns', row1: 0, column1, row2, column2, row1Absolute: true, column1Absolute, row2Absolute: true,
                column2Absolute, spilled: false, lastSheetName: undefined,
            });
        }
        return new Reference({
            sheetName: sheet, shape: 'area', row1, column1, row2, column2, row1Absolute, column1Absolute, row2Absolute, column2Absolute,
            spilled: false, lastSheetName: undefined,
        });
    }

    /** The Reference as Excel writes it: a rectangle in the shape `rectangle` gives it. */
    write(): string {
        if (this.shape !== 'cell') {
            const shaped = Reference.rectangle(this.sheetName, this.row1, this.column1, this.row2, this.column2, this.row1Absolute,
                this.column1Absolute, this.row2Absolute, this.column2Absolute).with({ lastSheetName: this.lastSheetName });
            if (shaped.shape !== this.shape) return shaped.write();
        }
        let text = '';
        if (this.lastSheetName !== undefined) {
            if (this.sheetName === undefined) throw new Error('Internal: a 3D Reference names the Sheet that starts its span.');
            text += `${quoteSheetSpan(this.sheetName, this.lastSheetName)}!`;
        } else if (this.sheetName !== undefined) {
            text += `${quoteSheetName(this.sheetName)}!`;
        }
        switch (this.shape) {
            case 'cell':
                text += column(this.column1, this.column1Absolute) + row(this.row1, this.row1Absolute);
                if (this.spilled) text += '#';
                break;
            case 'area':
                text += `${column(this.column1, this.column1Absolute)}${row(this.row1, this.row1Absolute)}:${column(this.column2, this.column2Absolute)}${row(this.row2, this.row2Absolute)}`;
                break;
            case 'columns':
                text += `${column(this.column1, this.column1Absolute)}:${column(this.column2, this.column2Absolute)}`;
                break;
            case 'rows':
                text += `${row(this.row1, this.row1Absolute)}:${row(this.row2, this.row2Absolute)}`;
                break;
            default:
                return unreachableShape(this.shape);
        }
        return text;
    }
}

function column(index: number, absolute: boolean): string {
    return (absolute ? '$' : '') + CellAddress.columnName(index);
}

function row(index: number, absolute: boolean): string {
    return (absolute ? '$' : '') + String(index + 1);
}

function unreachableShape(shape: never): never {
    throw new Error(`Internal: ${String(shape)} is no shape of Reference.`);
}

/**
 * A 3D Reference's span, `Sheet1:Sheet3`: bare when both names are written bare, and otherwise quoted whole,
 * `'Q1 Sales:Sheet3'`, as Excel writes one, doubling any `'`.
 */
export function quoteSheetSpan(first: string, last: string): string {
    const bare = quoteSheetName(first) === first && quoteSheetName(last) === last;
    const span = `${first}:${last}`;
    return bare ? span : `'${span.replaceAll("'", "''")}'`;
}

/** A Sheet name is written bare when it is a plain identifier, and quoted otherwise, doubling any `'`. */
export function quoteSheetName(name: string): string {
    let bare = name.length > 0 && (isAsciiLetter(name.charCodeAt(0)) || name[0] === '_');
    for (let i = 0; i < name.length; i++) {
        const c = name.charCodeAt(i);
        if (!(isAsciiLetterOrDigit(c) || c === 0x5f || c === 0x2e)) bare = false;
    }
    // A bare name that reads as a cell address (A1!B2), an R1C1 address or a boolean would be misread.
    if (bare && (CellAddress.tryParse(name) !== undefined || rowColumn(name) || equalsIgnoreCase(name, 'TRUE') || equalsIgnoreCase(name, 'FALSE'))) {
        bare = false;
    }
    return bare ? name : `'${name.replaceAll("'", "''")}'`;
}

/** R, C, R1, C1, R1C1, RC, ...: R1C1 notation. */
function rowColumn(name: string): boolean {
    let i = 0;
    if (i < name.length && (name[i] === 'R' || name[i] === 'r')) {
        i++;
        while (i < name.length && isAsciiDigit(name.charCodeAt(i))) i++;
    }
    if (i < name.length && (name[i] === 'C' || name[i] === 'c')) {
        i++;
        while (i < name.length && isAsciiDigit(name.charCodeAt(i))) i++;
    }
    return i > 0 && i === name.length;
}
