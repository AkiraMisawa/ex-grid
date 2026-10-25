import { Value, type ErrorValue } from '../value';
import type { CellReader } from './cell-reader';
import { Area } from './reference';
import { ValueArray } from './value-array';

/**
 * What an operand is: a Value (or a blank read from one cell); a rectangle of cells not yet read; an argument
 * left empty; a Linked Table's column (ADR-0049); an array a Formula computed (ADR-0125); or a 3D Reference,
 * one rectangle on each Sheet of a span (ADR-0331 §7).
 */
export type OperandKind = 'scalar' | 'area' | 'missing' | 'column' | 'array' | 'threeD';

interface OperandParts {
    readonly kind: OperandKind;
    readonly scalar?: Value | undefined;
    readonly area?: Area;
    readonly column?: readonly (Value | undefined)[] | undefined;
    readonly array?: ValueArray | undefined;
    readonly isSpillRange?: boolean;
    readonly keptBlank?: boolean;
    readonly cells?: CellReader | undefined;
    readonly sheets?: readonly CellReader[] | undefined;
    readonly handedOn?: boolean;
}

const noArea = new Area(0, 0, 0, 0);

/**
 * What a node evaluates to before it is used: a Value (or blank), or a rectangle of cells that a function reads
 * as a range. Excel's functions tell the two apart — `SUM` ignores text in a range but not text typed as an
 * argument.
 */
export class Operand {
    readonly kind: OperandKind;
    /** For `scalar`: the Value, or `undefined` for a blank. */
    readonly scalar: Value | undefined;
    readonly area: Area;
    /** For `area`: the Sheet its cells are on, as the recalculation reads it (ADR-0331). */
    readonly cells: CellReader | undefined;
    /** For `threeD`: the Sheets of the span, in tab order (ADR-0331 §7). */
    readonly sheets: readonly CellReader[] | undefined;
    /**
     * For `threeD`: whether a function handed it on (`IF`, `CHOOSE`, a `LET` name), rather than its Reference
     * standing where it is read. Read as one Value, one written there is `#REF!` and one handed on `#VALUE!`.
     */
    readonly handedOn: boolean;
    /** For `column`: the column's Values top to bottom, blank as `undefined`. */
    readonly column: readonly (Value | undefined)[] | undefined;
    /** For `array`: the computed Values. */
    readonly array: ValueArray | undefined;
    /** For `array`: whether the Values are a Spill Range's, read through `A1#` (ADR-0125): a Reference to cells in Excel. */
    readonly isSpillRange: boolean;
    /**
     * Whether this holds a kept blank: a blank that `UNIQUE`, `SORT`, `SORTBY`, `FILTER` or `TRANSPOSE` kept
     * from its array (ADR-0125), as one of its Values or as the one Value `INDEX` picked from it. A function
     * reads such an operand only through `FunctionCall.keepingBlanks` or `FunctionCall.oneValue`; anywhere else
     * the Formula is refused.
     */
    readonly keptBlank: boolean;

    private constructor(parts: OperandParts) {
        this.kind = parts.kind;
        this.scalar = parts.scalar;
        this.area = parts.area ?? noArea;
        this.cells = parts.cells;
        this.sheets = parts.sheets;
        this.handedOn = parts.handedOn === true;
        this.column = parts.column;
        this.array = parts.array;
        this.isSpillRange = parts.isSpillRange === true;
        this.keptBlank = parts.keptBlank === true;
    }

    /** Whether this is a Reference of cells: a rectangle, or a Spill Range read through `A1#`. */
    get isReference(): boolean {
        return this.kind === 'area' || this.isSpillRange;
    }

    /** Whether this is a range a function reads cell by cell: a rectangle of cells, a Linked Table's column, an array or a 3D Reference. */
    get isRange(): boolean {
        return this.kind === 'area' || this.kind === 'column' || this.kind === 'array' || this.kind === 'threeD';
    }

    static readonly missing = new Operand({ kind: 'missing' });

    static readonly blank = new Operand({ kind: 'scalar' });

    /** One Value. */
    static of(value: Value): Operand {
        return new Operand({ kind: 'scalar', scalar: value });
    }

    /** One Error Value. */
    static ofError(error: ErrorValue): Operand {
        return new Operand({ kind: 'scalar', scalar: Value.fromError(error) });
    }

    /** One Value, or a blank. */
    static ofScalar(value: Value | undefined): Operand {
        return value === undefined ? Operand.blank : Operand.of(value);
    }

    /** A rectangle of the Sheet `cells` reads (ADR-0331). */
    static ofArea(area: Area, cells: CellReader): Operand {
        return new Operand({ kind: 'area', area, cells });
    }

    /** A 3D Reference's rectangle on each Sheet of its span, `sheets` in tab order (ADR-0331 §7). */
    static ofThreeD(area: Area, sheets: readonly CellReader[]): Operand {
        return new Operand({ kind: 'threeD', area, sheets });
    }

    /** This operand as a function hands it on (`handedOn`): a 3D Reference marked so, anything else as it is. */
    handOn(): Operand {
        return this.kind === 'threeD' ? new Operand({ kind: 'threeD', area: this.area, sheets: this.sheets, handedOn: true }) : this;
    }

    /** Another rectangle of the same Sheet as this one, which must be a rectangle. */
    sameSheet(area: Area): Operand {
        if (this.kind !== 'area' || this.cells === undefined) throw new Error('Not a rectangle of cells.');
        return Operand.ofArea(area, this.cells);
    }

    /** A Linked Table's column (ADR-0049). */
    static ofColumn(column: readonly (Value | undefined)[]): Operand {
        return new Operand({ kind: 'column', column });
    }

    /** A computed array; one of a single Value is that Value. */
    static ofArray(array: ValueArray): Operand {
        return array.isSingle ? new Operand({ kind: 'scalar', scalar: array.at(0, 0) }) : new Operand({ kind: 'array', array });
    }

    /** A Spill Range's Values, read through `A1#`: an array that is a Reference (`isSpillRange`). */
    static ofSpillRange(array: ValueArray): Operand {
        return new Operand({ kind: 'array', array, isSpillRange: true });
    }

    /** One Value as an array of one, for a function that takes an array and is given what a computation reduced to one Value (ADR-0125). */
    static asArray(value: Value | undefined): Operand {
        return new Operand({ kind: 'array', array: ValueArray.single(value) });
    }

    get isError(): boolean {
        return this.kind === 'scalar' && this.scalar !== undefined && this.scalar.isError;
    }

    /** Whether this is a blank, or an array that holds one. */
    get holdsBlank(): boolean {
        switch (this.kind) {
            case 'scalar': return this.scalar === undefined;
            case 'array': return this.arrayOf().all().includes(undefined);
            case 'area':
            case 'missing':
            case 'column':
            case 'threeD':
                return false;
            default: throw new Error(`Internal: ${String(this.kind)} is no kind of operand.`);
        }
    }

    /** This operand marked as holding a kept blank (`keptBlank`), when it holds a blank at all; as it is otherwise. */
    withKeptBlank(): Operand {
        return this.holdsBlank
            ? new Operand({
                kind: this.kind, scalar: this.scalar, area: this.area, column: this.column, array: this.array, isSpillRange: this.isSpillRange,
                keptBlank: true, cells: this.cells,
            })
            : this;
    }

    /** The array of an `array` operand. */
    arrayOf(): ValueArray {
        if (this.array === undefined) throw new Error(`Internal: a ${this.kind} operand holds no array.`);
        return this.array;
    }

    /** The column of a `column` operand. */
    columnOf(): readonly (Value | undefined)[] {
        if (this.column === undefined) throw new Error(`Internal: a ${this.kind} operand holds no column.`);
        return this.column;
    }

    /** The Sheet of an `area` operand. */
    cellsOf(): CellReader {
        if (this.cells === undefined) throw new Error(`Internal: a ${this.kind} operand is on no Sheet.`);
        return this.cells;
    }

    /** The Sheets of a `threeD` operand. */
    sheetsOf(): readonly CellReader[] {
        if (this.sheets === undefined) throw new Error(`Internal: a ${this.kind} operand spans no Sheets.`);
        return this.sheets;
    }
}
