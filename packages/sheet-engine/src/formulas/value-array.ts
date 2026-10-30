import { Value } from '../value';

/**
 * A rectangle of Values a Formula computes (ADR-0125): an operator applied to a range, a function built for
 * arrays, or a range read whole. Row by row; a blank is `undefined`.
 */
export class ValueArray {
    /**
     * The most Values one array holds: 2^24, sixteen whole columns. A larger array is not computed, as Excel
     * runs out of resources before it (ADR-0125: a cap belongs on what cannot be executed); whatever would
     * build one is `#NUM!`.
     */
    static readonly mostCells = 2 ** 24;

    readonly rows: number;
    readonly columns: number;
    readonly #values: (Value | undefined)[];

    constructor(rows: number, columns: number) {
        if (rows < 1 || columns < 1) throw new RangeError('An array has at least one row and one column.');
        if (rows * columns > ValueArray.mostCells) throw new RangeError('The array is larger than one array holds.');
        this.rows = rows;
        this.columns = columns;
        this.#values = Array.from<Value | undefined>({ length: rows * columns });
    }

    /** Whether the array is one Value, which is no array at all to whoever reads it. */
    get isSingle(): boolean {
        return this.rows === 1 && this.columns === 1;
    }

    at(row: number, column: number): Value | undefined {
        return this.#values[row * this.columns + column];
    }

    set(row: number, column: number, value: Value | undefined): void {
        this.#values[row * this.columns + column] = value;
    }

    /** Every Value row by row, blanks included as `undefined`. */
    all(): readonly (Value | undefined)[] {
        return this.#values;
    }

    /** The Values row by row, blanks left out, as a range's are read. */
    *nonBlank(): Generator<Value> {
        for (const v of this.#values) {
            if (v !== undefined) yield v;
        }
    }

    /**
     * The element at `row`, `column` as Excel's broadcasting reads it: a single row or column is repeated along
     * the other array, and past its size an array of more than one row or column is `#N/A`.
     */
    broadcast(row: number, column: number): Value | undefined {
        const r = this.rows === 1 ? 0 : row;
        const c = this.columns === 1 ? 0 : column;
        return r < this.rows && c < this.columns ? this.at(r, c) : Value.fromError('#N/A');
    }

    static single(value: Value | undefined): ValueArray {
        const array = new ValueArray(1, 1);
        array.set(0, 0, value);
        return array;
    }
}
