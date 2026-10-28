// A Pivot Source's answer to a question (ADR-0066): the Leaf Aggregates — one leaf per combination of the
// row and column fields' Items that has an included record, each with its Items, its record count and the
// parts asked for of each field in Values — under the Source Version they were computed at; or a refusal,
// which is an answer and not a failure. The C# engine's PivotAnswer, PivotSourceRefusal, PivotNumber and
// PivotAnswerBuilder.
//
// To a question carrying levels, a source that answers Level Aggregates may answer, when the leaves would pass
// the cap, with the cells of the groups shown instead (ADR-0370 section 3): each a shown row group crossed
// with a shown column group that some record reaches, named by its Items down to its depth — an axis's
// `itemAt` is −1 below it — with its records and its parts, and, beside a sum that is a double, the sum of its
// numbers' magnitudes. What is said here of a leaf is then said of a cell. A sibling set ordered by a value and
// passing the question's window limit is answered as a window: its siblings from where the window starts, a
// margin on either side, and rest cells merging the siblings beyond each margin, one a shown column group.

import type { SnapshotDecimal } from '@ex-grid/data';
import { decimalCanonical, decimalDivide, decimalEquals, decimalToNumber } from '../aggregates/exact-decimal';
import { PartColumns, partsText } from '../aggregates/parts';
import { itemKeyText, pivotItemKey, publicKeyId, type PivotItemKey } from '../items/item-key';
import { aggregationTitle, type PivotAggregation } from '../layout/pivot-enums';
import { PivotFormatError } from '../errors';
import { levelAnswerMismatch } from '../levels/level-shown';
import { elementAt } from '../must';
import { partBits, partList, placedFields, type PivotPart, type PivotQuery } from './query';

/** Why a Pivot Source refused a question (ADR-0066). */
export type PivotSourceRefusalKind = 'tooManyLeaves' | 'unknownField' | 'aggregationNotOffered' | 'sourceVersionNotHeld';

/** A Pivot Source's refusal to answer: its kind, a sentence a person can read, and the field or the cap it
 * concerns. The report stays on the layout it had and says why. */
export interface PivotSourceRefusal {
    /** Why the source refused. */
    readonly kind: PivotSourceRefusalKind;
    /** A sentence a person can read. */
    readonly message: string;
    /** The field the refusal concerns. */
    readonly field?: string;
    /** The cap a `tooManyLeaves` refusal passed. */
    readonly limit?: number;
}

/** A whole number with its thousands grouped by commas, as .NET's invariant `N0` writes it. */
function grouped(value: number): string {
    return Math.trunc(value).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}

/** The refusals, worded as the C# engine words them. */
export const PivotRefusals = Object.freeze({
    /** "This layout needs more than 200,000 cells." */
    tooManyLeaves: (maxLeaves: number): PivotSourceRefusal => Object.freeze({
        kind: 'tooManyLeaves', message: `This layout needs more than ${grouped(maxLeaves)} cells.`, limit: maxLeaves,
    }),
    /** A field the source does not have. */
    unknownField: (field: string): PivotSourceRefusal => Object.freeze({
        kind: 'unknownField', message: `The source has no field named '${field}'.`, field,
    }),
    /** A part that only Aggregations the source does not offer read. */
    aggregationNotOffered: (field: string, aggregations: readonly PivotAggregation[]): PivotSourceRefusal => Object.freeze({
        kind: 'aggregationNotOffered',
        message: `The source does not offer ${aggregations.map(aggregationTitle).join(' or ')}, which the question asks of '${field}'.`,
        field,
    }),
    /** A question asked under a Source Version the source no longer holds. */
    sourceVersionNotHeld: (sourceVersion: string): PivotSourceRefusal => Object.freeze({
        kind: 'sourceVersionNotHeld', message: `The data has changed since version '${sourceVersion}'; refresh the report.`,
    }),
});

/** A number a part carries: exact while every number it was made of was, a double otherwise. */
export type PivotNumber =
    | { readonly kind: 'exact'; readonly value: SnapshotDecimal }
    | { readonly kind: 'double'; readonly value: number };

/** The number as a double: the exact one converted as C# converts a decimal. */
export function pivotNumberValue(number: PivotNumber): number {
    return number.kind === 'exact' ? decimalToNumber(number.value) : number.value;
}

/** The number without trailing zeros where it is exact, as C#'s accumulator finishes a figure (`AggregateArithmetic.Canonical`). */
export function pivotNumberCanonical(number: PivotNumber): PivotNumber {
    if (number.kind !== 'exact') return number;
    const value = decimalCanonical(number.value);
    return value === number.value ? number : Object.freeze({ kind: 'exact', value });
}

/** The Average of `count` numbers whose sum is `sum`: exact where the sum is, divided as C#'s decimal divides by a
 * whole count, and a double otherwise, as a Selection Summary's Average over the report reads it (ADR-0130). */
export function pivotNumberAverage(sum: PivotNumber, count: number): PivotNumber {
    return sum.kind === 'exact'
        ? Object.freeze({ kind: 'exact', value: decimalDivide(sum.value, count) })
        : Object.freeze({ kind: 'double', value: sum.value / count });
}

/** Whether two numbers are one: both exact and equal, or both doubles with the same bits. */
export function pivotNumbersEqual(a: PivotNumber, b: PivotNumber): boolean {
    if (a.kind === 'exact') return b.kind === 'exact' && decimalEquals(a.value, b.value);
    return b.kind === 'double' && Object.is(a.value, b.value);
}

function exact(value: SnapshotDecimal): PivotNumber {
    return Object.freeze({ kind: 'exact', value });
}

function double(value: number): PivotNumber {
    return Object.freeze({ kind: 'double', value });
}

/** One row or column field of an answer: its Items, and each leaf's Item among them. */
export class PivotAnswerAxis {
    /** The field. */
    readonly field: string;
    /** Its Items that some leaf carries. */
    readonly items: readonly PivotItemKey[];
    readonly #itemOfLeaf: readonly number[];

    /** @internal */
    constructor(field: string, items: readonly PivotItemKey[], itemOfLeaf: readonly number[]) {
        this.field = field;
        this.items = Object.freeze([...items]);
        this.#itemOfLeaf = itemOfLeaf;
    }

    /** The index among {@link PivotAnswerAxis.items} of a leaf's Item; in a level answer, −1 for a cell above
     * this field, which stands for every Item of it. */
    itemAt(leaf: number): number {
        return elementAt(this.#itemOfLeaf, checkLeaf(leaf, this.#itemOfLeaf.length));
    }
}

function checkLeaf(leaf: number, leafCount: number): number {
    if (!Number.isInteger(leaf) || leaf < 0 || leaf >= leafCount) throw new RangeError(`The answer has ${leafCount} leaves.`);
    return leaf;
}

let makeValues: (field: string, columns: PartColumns, leafCount: number) => PivotAnswerValues;
let columnsOfValues: (values: PivotAnswerValues) => PartColumns;

/** One field in Values of an answer: the parts at each leaf. */
export class PivotAnswerValues {
    /** The field. */
    readonly field: string;
    readonly #columns: PartColumns;
    readonly #leafCount: number;

    private constructor(field: string, columns: PartColumns, leafCount: number) {
        this.field = field;
        this.#columns = columns;
        this.#leafCount = leafCount;
    }

    static {
        makeValues = (field, columns, leafCount) => new PivotAnswerValues(field, columns, leafCount);
        columnsOfValues = values => values.#columns;
    }

    /** The parts carried beyond the counts. */
    get parts(): readonly PivotPart[] {
        return partList(this.#columns.parts);
    }

    /** The values at a leaf that are not Blank. */
    valuesAt(leaf: number): number {
        return elementAt(this.#columns.counts, this.#at(leaf)).values;
    }

    /** The numbers at a leaf, non-finite ones included. */
    numbersAt(leaf: number): number {
        return elementAt(this.#columns.counts, this.#at(leaf)).numbers;
    }

    /** Whether a non-finite number is among a leaf's values. */
    nonFiniteAt(leaf: number): boolean {
        return elementAt(this.#columns.counts, this.#at(leaf)).nonFinite;
    }

    /** The sum of a leaf's numbers. */
    sumAt(leaf: number): PivotNumber {
        const sum = elementAt(this.#carried(this.#columns.sums, 'Sum'), this.#at(leaf));
        return sum.inexact ? double(sum.double + sum.compensation) : exact(sum.exact);
    }

    /** The smallest of a leaf's numbers. */
    minAt(leaf: number): PivotNumber {
        const extremes = elementAt(this.#carried(this.#columns.extremes, 'Extremes'), this.#at(leaf));
        return extremes.inexact ? double(extremes.min) : exact(extremes.exactMin);
    }

    /** The largest of a leaf's numbers. */
    maxAt(leaf: number): PivotNumber {
        const extremes = elementAt(this.#carried(this.#columns.extremes, 'Extremes'), this.#at(leaf));
        return extremes.inexact ? double(extremes.max) : exact(extremes.exactMax);
    }

    /** The sum of the magnitudes of a cell's numbers, which a level answer carries beside a sum that is a
     * double: what bounds its rounding when it is checked against its parent (ADR-0370 section 3).
     * `undefined` for an exact sum, and in an answer of leaves. */
    absoluteSumAt(leaf: number): number | undefined {
        const at = this.#at(leaf);
        const sum = elementAt(this.#carried(this.#columns.sums, 'Sum'), at);
        const absolutes = this.#columns.absolutes;
        return sum.inexact && absolutes !== undefined ? elementAt(absolutes, at) : undefined;
    }

    /** The product of a leaf's numbers. */
    productAt(leaf: number): number {
        return elementAt(this.#carried(this.#columns.products, 'Product'), this.#at(leaf));
    }

    /** The running mean of a leaf's numbers. */
    meanAt(leaf: number): number {
        return elementAt(this.#carried(this.#columns.variances, 'Variance'), this.#at(leaf)).mean;
    }

    /** The running sum of squared deviations of a leaf's numbers. */
    m2At(leaf: number): number {
        return elementAt(this.#carried(this.#columns.variances, 'Variance'), this.#at(leaf)).m2;
    }

    #at(leaf: number): number {
        return checkLeaf(leaf, this.#leafCount);
    }

    #carried<T>(part: T | undefined, name: string): T {
        if (part === undefined) throw new Error(`The answer carries no ${name} part for '${this.field}'; the question did not ask for it (ADR-0066).`);
        return part;
    }
}

function names(fields: readonly { readonly field: string }[]): string {
    return `[${fields.map(f => f.field).join(', ')}]`;
}

function sameNames(a: readonly { readonly field: string }[], b: readonly { readonly field: string }[]): boolean {
    return a.length === b.length && a.every((f, i) => f.field === b[i]?.field);
}

/**
 * A sibling set answered as a window (ADR-0370 section 3): its parent row group, and how many of its siblings
 * come before the first one shown and after the last. Each count, when above 0, takes in a margin — the
 * nearest sibling on that side, answered as any shown group is and not shown — and the siblings beyond it,
 * merged into the window's rest cells on that side.
 */
export interface PivotAnswerWindow {
    /** The parent's Item at each row field above the set, as an index among that axis's Items: none for the
     * grand total. */
    readonly parent: readonly number[];
    /** The siblings before the first one shown, its margin among them. */
    readonly before: number;
    /** The siblings after the last one shown, its margin among them. */
    readonly after: number;
}

/** Which side of a window a rest cell merges. */
export type PivotRestSide = 'before' | 'after';

const restSides: ReadonlySet<string> = new Set(['before', 'after']);

/** A rest cell's window, by its place among the answer's windows, and its side. */
export interface PivotAnswerRest {
    /** The window, by its place among {@link PivotAnswer.windows}. */
    readonly window: number;
    /** The side whose siblings beyond the margin it merges. */
    readonly side: PivotRestSide;
}

/** An answer's windows, and each cell's rest code: −1 for a cell, 2w for the before side of window w, 2w + 1 for
 * its after side. */
export interface PivotAnswerWindowing {
    readonly windows: readonly PivotAnswerWindow[];
    readonly rests: readonly number[];
}

/** No window. */
export const noWindowing: PivotAnswerWindowing = Object.freeze({ windows: Object.freeze([]), rests: Object.freeze([]) });

let makeAnswer: (sourceVersion: string, rows: readonly PivotAnswerAxis[], columns: readonly PivotAnswerAxis[], leafCount: number,
    records: readonly number[], values: readonly PivotAnswerValues[], levels: boolean, windowing: PivotAnswerWindowing) => PivotAnswer;

// A level answer's shape (ADR-0370 section 3): each cell named down to its depth on each axis and by nothing
// below, a double sum carried with its magnitudes, and each rest cell named by its window's parent.
function checkLevels(rows: readonly PivotAnswerAxis[], columns: readonly PivotAnswerAxis[], cells: number, values: readonly PivotAnswerValues[],
    windowing: PivotAnswerWindowing): void {
    for (let cell = 0; cell < cells; cell++) {
        checkDepth(rows, cell);
        checkDepth(columns, cell);
    }
    checkWindowing(rows, cells, windowing);
    for (const value of values) {
        const parts = columnsOfValues(value);
        const sums = parts.sums;
        if (sums === undefined) continue;
        const absolutes = parts.absolutes;
        for (let cell = 0; cell < cells; cell++) {
            if (elementAt(sums, cell).inexact && !elementAt(parts.counts, cell).nonFinite && (absolutes === undefined || Number.isNaN(elementAt(absolutes, cell)))) {
                throw new PivotFormatError(`Cell ${cell}'s sum of '${value.field}' is a double, and carries no sum of its magnitudes (ADR-0370 section 3).`);
            }
        }
    }
}

function checkWindowing(rows: readonly PivotAnswerAxis[], cells: number, windowing: PivotAnswerWindowing): void {
    const { windows, rests } = windowing;
    if (windows.length === 0) {
        if (rests.length !== 0) throw new PivotFormatError('The answer has rest cells and no window (ADR-0370 section 3).');
        return;
    }
    if (rests.length !== cells) throw new PivotFormatError(`The answer says whether ${rests.length} cells are rests, and has ${cells}.`);
    windows.forEach((window, w) => {
        if (window.parent.length >= rows.length) {
            throw new PivotFormatError(`Window ${w}'s parent names ${window.parent.length} Items, and the answer has ${rows.length} row fields; its siblings are a row field's Items (ADR-0370 section 3).`);
        }
        window.parent.forEach((item, level) => {
            if (!Number.isSafeInteger(item) || item < 0 || item >= elementAt(rows, level).items.length) {
                throw new PivotFormatError(`Window ${w}'s parent names Item ${item} of '${elementAt(rows, level).field}', which has ${elementAt(rows, level).items.length}.`);
            }
        });
        for (const [side, count] of [['before', window.before], ['after', window.after]] as const) {
            if (!Number.isSafeInteger(count) || count < 0) throw new PivotFormatError(`Window ${w} has ${count} siblings ${side} it; a count is a whole number, not below 0.`);
        }
    });
    rests.forEach((code, cell) => {
        if (code === -1) return;
        if (!Number.isSafeInteger(code) || code < 0 || code >= 2 * windows.length) {
            throw new PivotFormatError(`Cell ${cell} is a rest of window ${Math.floor(code / 2)}, and the answer has ${windows.length}.`);
        }
        const window = elementAt(windows, Math.floor(code / 2));
        const named = rows.every((axis, level) => axis.itemAt(cell) === (level < window.parent.length ? elementAt(window.parent, level) : -1));
        if (!named) throw new PivotFormatError(`Cell ${cell} is a rest of window ${Math.floor(code / 2)}, and is not named by its parent's Items alone (ADR-0370 section 3).`);
    });
}

function checkDepth(axes: readonly PivotAnswerAxis[], cell: number): void {
    let below = false;
    for (const axis of axes) {
        if (axis.itemAt(cell) < 0) below = true;
        else if (below) throw new PivotFormatError(`Cell ${cell} names an Item of '${axis.field}' below a field it names none of; a cell is named down to its depth (ADR-0370 section 3).`);
    }
}

/** A Pivot Source's answer: the Leaf Aggregates under a Source Version, or a refusal. */
export class PivotAnswer {
    /** The refusal, when the source refused. */
    readonly refusal: PivotSourceRefusal | undefined;
    /** How many leaves the answer has. */
    readonly leafCount: number;
    readonly #sourceVersion: string | undefined;
    readonly #rows: readonly PivotAnswerAxis[];
    readonly #columns: readonly PivotAnswerAxis[];
    readonly #values: readonly PivotAnswerValues[];
    readonly #records: readonly number[];
    /** Whether the answer holds the cells of the groups shown — Level Aggregates — rather than the leaves
     * (ADR-0370 section 3). */
    readonly isLevels: boolean;
    readonly #windowing: PivotAnswerWindowing;

    private constructor(
        sourceVersion: string | undefined, refusal: PivotSourceRefusal | undefined, rows: readonly PivotAnswerAxis[],
        columns: readonly PivotAnswerAxis[], leafCount: number, records: readonly number[], values: readonly PivotAnswerValues[],
        levels: boolean, windowing: PivotAnswerWindowing,
    ) {
        if (levels) checkLevels(rows, columns, leafCount, values, windowing);
        else if (windowing.windows.length > 0) throw new PivotFormatError('An answer of leaves has no window (ADR-0370 section 3).');
        this.isLevels = levels;
        this.#windowing = windowing;
        this.#sourceVersion = sourceVersion;
        this.refusal = refusal;
        this.#rows = Object.freeze([...rows]);
        this.#columns = Object.freeze([...columns]);
        this.leafCount = leafCount;
        this.#records = records;
        this.#values = Object.freeze([...values]);
    }

    static {
        makeAnswer = (sourceVersion, rows, columns, leafCount, records, values, levels, windowing) =>
            new PivotAnswer(sourceVersion, undefined, rows, columns, leafCount, records, values, levels, windowing);
    }

    /** A refusal, as an answer. */
    static refused(refusal: PivotSourceRefusal): PivotAnswer {
        return new PivotAnswer(undefined, refusal, [], [], 0, [], [], false, noWindowing);
    }

    /** Whether the source refused. */
    get isRefused(): boolean {
        return this.refusal !== undefined;
    }

    /** The Source Version the answer was computed at. A refusal has none, and throws. */
    get sourceVersion(): string {
        if (this.#sourceVersion === undefined) throw this.#refusedError();
        return this.#sourceVersion;
    }

    /** The row fields, in the question's order. */
    get rows(): readonly PivotAnswerAxis[] {
        if (this.refusal !== undefined) throw this.#refusedError();
        return this.#rows;
    }

    /** The column fields, in the question's order. */
    get columns(): readonly PivotAnswerAxis[] {
        if (this.refusal !== undefined) throw this.#refusedError();
        return this.#columns;
    }

    /** Each field in Values, in the question's order. */
    get values(): readonly PivotAnswerValues[] {
        if (this.refusal !== undefined) throw this.#refusedError();
        return this.#values;
    }

    /** The included records at a leaf. */
    recordsAt(leaf: number): number {
        return elementAt(this.#records, checkLeaf(leaf, this.leafCount));
    }

    /** The sibling sets answered as windows (ADR-0370 section 3); none in an answer of leaves. */
    get windows(): readonly PivotAnswerWindow[] {
        if (this.refusal !== undefined) throw this.#refusedError();
        return this.#windowing.windows;
    }

    /** The window and side a rest cell merges, or `undefined` for a cell of a group. */
    restAt(cell: number): PivotAnswerRest | undefined {
        checkLeaf(cell, this.leafCount);
        if (this.#windowing.windows.length === 0) return undefined;
        const code = elementAt(this.#windowing.rests, cell);
        return code < 0 ? undefined : { window: Math.floor(code / 2), side: code % 2 === 0 ? 'before' : 'after' };
    }

    /** What does not answer the question, in words, or `undefined` when the answer answers it. */
    mismatch(query: PivotQuery): string | undefined {
        if (this.refusal !== undefined) return `the source refused: ${this.refusal.message}`;
        if (this.isLevels && query.levels === undefined) return 'it holds Level Aggregates, and the question asks for the leaves';
        if (!sameNames(this.#rows, query.rows)) return `its row fields are ${names(this.#rows)}, and the question's ${names(query.rows)}`;
        if (!sameNames(this.#columns, query.columns)) return `its column fields are ${names(this.#columns)}, and the question's ${names(query.columns)}`;
        if (!sameNames(this.#values, query.values)) return `its fields in Values are ${names(this.#values)}, and the question's ${names(query.values)}`;
        for (let v = 0; v < this.#values.length; v++) {
            const held = elementAt(this.#values, v);
            const asked = partBits(elementAt(query.values, v).parts);
            if ((columnsOfValues(held).parts & asked) !== asked) {
                return `it carries the parts ${partsText(columnsOfValues(held).parts)} of '${held.field}', and the question asks for ${partsText(asked)}`;
            }
        }
        if (this.leafCount > query.maxLeaves) {
            return `it holds ${this.leafCount} ${this.isLevels ? 'cells' : 'leaves'}, and the question allows ${query.maxLeaves}`;
        }
        return query.levels !== undefined && this.isLevels ? levelAnswerMismatch(query.levels, this, this.#windowing) : undefined;
    }

    #refusedError(): Error {
        return new Error(`The source refused to answer: ${this.refusal?.message ?? 'without a reason'}`); // oxlint-disable-line ex-grid/no-stand-in-default -- only a refusal reaches here, and a refusal says why
    }
}

/**
 * Builds an answer a leaf at a time, as a server's rows arrive (ADR-0066): each leaf named by its Items, one
 * per row and column field, with its record count, and the parts asked for of each field in Values set on
 * it. Two leaves for one combination of Items are refused, and so is a part the question did not ask for.
 *
 * A server that answers Level Aggregates (ADR-0370 section 3) adds the cells of the groups shown with
 * {@link PivotAnswerBuilder.addCell} instead, from `GROUPING SETS` for instance, and gives a sum that is a
 * double with the sum of its magnitudes; an answer holds leaves or cells, never both.
 */
export class PivotAnswerBuilder {
    readonly #query: PivotQuery;
    readonly #sourceVersion: string;
    readonly #levels: number;
    readonly #items: PivotItemKey[][];
    readonly #itemIndex: Map<string, number>[];
    readonly #itemOfLeaf: number[][];
    readonly #records: number[] = [];
    readonly #values: PartColumns[];
    readonly #leaves = new Map<string, number>();
    readonly #windows: PivotAnswerWindow[] = [];
    readonly #windowOf = new Map<string, number>();
    readonly #rests: number[] = [];
    #cells: boolean | undefined;
    #built = false;

    /** A builder for the answer to `query`, under `sourceVersion`. */
    constructor(query: PivotQuery, sourceVersion: string) {
        if (typeof sourceVersion !== 'string') throw new TypeError('An answer carries its Source Version.');
        this.#query = query;
        this.#sourceVersion = sourceVersion;
        this.#levels = query.rows.length + query.columns.length;
        this.#items = Array.from({ length: this.#levels }, () => []);
        this.#itemIndex = Array.from({ length: this.#levels }, () => new Map<string, number>());
        this.#itemOfLeaf = Array.from({ length: this.#levels }, () => []);
        this.#values = query.values.map(value => new PartColumns(partBits(value.parts)));
    }

    /** How many leaves have been added. */
    get leafCount(): number {
        return this.#records.length;
    }

    /** Adds a leaf of `records` records (at least 1) at one Item per row and column field, outermost row
     * field first; its index. */
    addLeaf(items: readonly PivotItemKey[], records: number): number {
        if (!Number.isSafeInteger(records) || records < 1) throw new RangeError(`A leaf holds at least one record, not ${records}.`);
        if (items.length !== this.#levels) {
            throw new TypeError(`A leaf names one Item per row and column field: ${this.#levels}, not ${items.length}.`);
        }
        if (this.#cells === true) throw new Error('This answer holds the cells of the groups shown; it is given them with addCell (ADR-0370 section 3).');
        this.#cells = false;
        const indexes = items.map((item, level) => this.#indexOf(level, item));
        const leafKey = indexes.join(',');
        if (this.#leaves.has(leafKey)) throw new TypeError(`The answer already has a leaf for [${items.map(itemKeyText).join(', ')}].`);
        return this.#add(leafKey, indexes, records);
    }

    /**
     * Adds the cell of a shown row group crossed with a shown column group, and returns its index (ADR-0370
     * section 3): an answer of Level Aggregates, to a question carrying levels. Each group is named by its
     * Items, outermost first, down to its depth — none for a grand total. Refuses a cell of no records, more
     * Items than an axis has fields, and a second cell for the same Items. Which cells are shown, and that each
     * has its parent, is checked when the answer is read against the question.
     */
    addCell(rowItems: readonly PivotItemKey[], columnItems: readonly PivotItemKey[], records: number): number {
        if (!Number.isSafeInteger(records) || records < 1) throw new RangeError(`A leaf holds at least one record, not ${records}.`);
        const rowCount = this.#query.rows.length;
        const columnCount = this.#query.columns.length;
        if (rowItems.length > rowCount) throw new TypeError(`A row group names at most one Item per row field: ${rowCount}, not ${rowItems.length}.`);
        if (columnItems.length > columnCount) throw new TypeError(`A column group names at most one Item per column field: ${columnCount}, not ${columnItems.length}.`);
        this.#startCells();
        const indexes = Array.from({ length: this.#levels }, () => -1);
        rowItems.forEach((item, level) => {
            indexes[level] = this.#indexOf(level, item);
        });
        columnItems.forEach((item, level) => {
            indexes[rowCount + level] = this.#indexOf(rowCount + level, item);
        });
        const cellKey = indexes.join(',');
        if (this.#leaves.has(cellKey)) {
            throw new TypeError(`The answer already has a cell for [${rowItems.map(itemKeyText).join(', ')}] × [${columnItems.map(itemKeyText).join(', ')}].`);
        }
        const cell = this.#add(cellKey, indexes, records);
        for (const parts of this.#values) if (parts.absolutes !== undefined) parts.absolutes[cell] = Number.NaN;
        return cell;
    }

    /**
     * Adds a sibling set answered as a window, and returns its index (ADR-0370 section 3): the children of the
     * row group `parentRows` names, of which `before` come before the first one shown and `after` after the
     * last, each count taking in its margin. The margins and the siblings shown are added as cells; the siblings
     * beyond each margin as rest cells with {@link PivotAnswerBuilder.addRest}. Refuses a second window for one
     * parent; whether the window answers the question is checked when the answer is read against it.
     */
    addWindow(parentRows: readonly PivotItemKey[], before: number, after: number): number {
        this.#startCells();
        if (parentRows.length >= this.#query.rows.length) {
            throw new TypeError(`A window's parent names fewer Items than there are row fields: ${this.#query.rows.length}, not ${parentRows.length}.`);
        }
        for (const [side, count] of [['before', before], ['after', after]] as const) {
            if (!Number.isSafeInteger(count) || count < 0) throw new RangeError(`A window has a whole number of siblings ${side} it, not ${count}.`);
        }
        const parent = parentRows.map((item, level) => this.#indexOf(level, item));
        const key = parent.join(',');
        if (this.#windowOf.has(key)) throw new TypeError(`The answer already has a window under [${parentRows.map(itemKeyText).join(', ')}].`);
        const window = this.#windows.length;
        this.#windowOf.set(key, window);
        this.#windows.push(Object.freeze({ parent: Object.freeze(parent), before, after }));
        return window;
    }

    /**
     * Adds a rest cell of a window, and returns its index (ADR-0370 section 3): the siblings on one side beyond
     * its margin, merged, crossed with the shown column group `columnItems` names. Its parts are set as a cell's.
     * Refuses one of no records and a second one for the same side and column group.
     */
    addRest(window: number, side: PivotRestSide, columnItems: readonly PivotItemKey[], records: number): number {
        if (!Number.isSafeInteger(records) || records < 1) throw new RangeError(`A leaf holds at least one record, not ${records}.`);
        const held = this.#windows[window];
        if (held === undefined) throw new RangeError(`The answer has ${this.#windows.length} windows.`);
        if (!restSides.has(side)) throw new TypeError(`A rest is before or after its window, not '${side}'.`);
        const rowCount = this.#query.rows.length;
        const columnCount = this.#query.columns.length;
        if (columnItems.length > columnCount) throw new TypeError(`A column group names at most one Item per column field: ${columnCount}, not ${columnItems.length}.`);
        const indexes = Array.from({ length: this.#levels }, () => -1);
        held.parent.forEach((item, level) => {
            indexes[level] = item;
        });
        columnItems.forEach((item, level) => {
            indexes[rowCount + level] = this.#indexOf(rowCount + level, item);
        });
        const code = 2 * window + (side === 'after' ? 1 : 0);
        const restKey = `${indexes.join(',')}|${code}`;
        if (this.#leaves.has(restKey)) throw new TypeError(`Window ${window} already has a rest ${side} it at [${columnItems.map(itemKeyText).join(', ')}].`);
        const cell = this.#add(restKey, indexes, records, code);
        for (const parts of this.#values) if (parts.absolutes !== undefined) parts.absolutes[cell] = Number.NaN;
        return cell;
    }

    #startCells(): void {
        if (this.#query.levels === undefined) throw new Error('The question asks for the leaves, and carries no levels to answer with cells (ADR-0370 section 3).');
        if (this.#cells === false) throw new Error('This answer holds leaves; it is given them with addLeaf (ADR-0370 section 3).');
        if (this.#cells === undefined) {
            this.#cells = true;
            for (const parts of this.#values) if (parts.sums !== undefined) parts.trackAbsolutes();
        }
    }

    #indexOf(level: number, item: PivotItemKey): number {
        const key = pivotItemKey(item.kind, item.value);
        const id = publicKeyId(key);
        const index = elementAt(this.#itemIndex, level);
        const found = index.get(id);
        if (found !== undefined) return found;
        const made = elementAt(this.#items, level).length;
        elementAt(this.#items, level).push(key);
        index.set(id, made);
        return made;
    }

    #add(leafKey: string, indexes: readonly number[], records: number, rest = -1): number {
        const leaf = this.#records.length;
        this.#leaves.set(leafKey, leaf);
        indexes.forEach((index, level) => elementAt(this.#itemOfLeaf, level).push(index));
        this.#records.push(records);
        this.#rests.push(rest);
        for (const values of this.#values) values.addCell();
        return leaf;
    }

    /** Sets a leaf's counts for a field in Values. */
    setCounts(leaf: number, value: number, values: number, numbers: number, nonFinite = false): void {
        const columns = this.#columnsOf(leaf, value);
        const records = elementAt(this.#records, leaf);
        if (values < 0 || numbers < 0 || numbers > values || values > records) {
            throw new RangeError(`A leaf of ${records} records cannot hold ${values} values of which ${numbers} are numbers.`);
        }
        columns.counts[leaf] = { values, numbers, nonFinite };
    }

    /** Sets a leaf's sum for a field in Values. */
    setSum(leaf: number, value: number, sum: PivotNumber): void {
        const sums = this.#asked(this.#columnsOf(leaf, value).sums, value, 'Sum');
        sums[leaf] = sum.kind === 'exact'
            ? { exact: decimalCanonical(sum.value), double: 0, compensation: 0, inexact: false }
            : { exact: { mantissa: 0n, scale: 0 }, double: sum.value, compensation: 0, inexact: true };
    }

    /** Sets a cell's sum of Level Aggregates with the sum of its numbers' magnitudes, which a sum that is a
     * double carries (ADR-0370 section 3): what bounds its rounding when the cell is checked against its
     * parent. */
    setLevelSum(leaf: number, value: number, sum: PivotNumber, absoluteSum: number): void {
        if (this.#cells !== true) throw new Error('A sum of magnitudes is carried by a cell of Level Aggregates (ADR-0370 section 3).');
        if (Number.isNaN(absoluteSum) || absoluteSum < 0) throw new RangeError(`A sum of magnitudes is a number, not below 0, not ${absoluteSum} (ADR-0370 section 3).`);
        this.setSum(leaf, value, sum);
        const absolutes = elementAt(this.#values, value).absolutes;
        if (absolutes === undefined) throw new Error('Internal: a cell of Level Aggregates keeps the magnitudes beside its sum.');
        absolutes[leaf] = absoluteSum;
    }

    /** Sets a leaf's extremes for a field in Values: both exact or both doubles. */
    setExtremes(leaf: number, value: number, min: PivotNumber, max: PivotNumber): void {
        const extremes = this.#asked(this.#columnsOf(leaf, value).extremes, value, 'Extremes');
        if (min.kind === 'exact' && max.kind === 'exact') {
            extremes[leaf] = { exactMin: min.value, exactMax: max.value, min: 0, max: 0, inexact: false };
        } else if (min.kind === 'double' && max.kind === 'double') {
            extremes[leaf] = { exactMin: { mantissa: 0n, scale: 0 }, exactMax: { mantissa: 0n, scale: 0 }, min: min.value, max: max.value, inexact: true };
        } else {
            throw new TypeError("A leaf's extremes are both exact or both double.");
        }
    }

    /** Sets a leaf's product for a field in Values. */
    setProduct(leaf: number, value: number, product: number): void {
        this.#asked(this.#columnsOf(leaf, value).products, value, 'Product')[leaf] = product;
    }

    /** Sets a leaf's running variance for a field in Values. */
    setVariance(leaf: number, value: number, mean: number, m2: number): void {
        this.#asked(this.#columnsOf(leaf, value).variances, value, 'Variance')[leaf] = { mean, m2 };
    }

    /** The answer, once: one with more leaves than the question allows is refused. */
    build(): PivotAnswer {
        if (this.#built) throw new Error('This answer has been built already.');
        this.#built = true;
        const leafCount = this.#records.length;
        if (leafCount > this.#query.maxLeaves) return PivotAnswer.refused(PivotRefusals.tooManyLeaves(this.#query.maxLeaves));
        for (const parts of this.#values) parts.finish();
        const rowCount = this.#query.rows.length;
        const axes = placedFields(this.#query).slice(0, this.#levels).map((field, level) =>
            new PivotAnswerAxis(field.field, elementAt(this.#items, level), elementAt(this.#itemOfLeaf, level)));
        const values = this.#values.map((parts, v) => makeValues(elementAt(this.#query.values, v).field, parts, leafCount));
        try {
            const windowing = this.#windows.length === 0 ? noWindowing : Object.freeze({ windows: Object.freeze([...this.#windows]), rests: Object.freeze([...this.#rests]) });
            return makeAnswer(this.#sourceVersion, axes.slice(0, rowCount), axes.slice(rowCount), leafCount, [...this.#records], values, this.#cells === true, windowing);
        } catch (error) {
            if (error instanceof PivotFormatError) throw new Error(error.message, { cause: error });
            throw error;
        }
    }

    #columnsOf(leaf: number, value: number): PartColumns {
        if (this.#built) throw new Error('This answer has been built already.');
        checkLeaf(leaf, this.#records.length);
        if (!Number.isInteger(value) || value < 0 || value >= this.#values.length) {
            throw new RangeError(`The question has ${this.#values.length} fields in Values.`);
        }
        return elementAt(this.#values, value);
    }

    #asked<T>(part: T | undefined, value: number, name: string): T {
        if (part === undefined) {
            throw new Error(`The question did not ask for the ${name} part of '${elementAt(this.#query.values, value).field}' (ADR-0066).`);
        }
        return part;
    }
}

/** An answer with its leaves, or with its cells of Level Aggregates (`levels`), as a pass, a builder or a
 * reader makes it; a level answer's shape is checked, and refused with a PivotFormatError. */
export function madeAnswer(sourceVersion: string, rows: readonly PivotAnswerAxis[], columns: readonly PivotAnswerAxis[], leafCount: number,
    records: readonly number[], values: readonly PivotAnswerValues[], levels = false, windowing: PivotAnswerWindowing = noWindowing): PivotAnswer {
    return makeAnswer(sourceVersion, rows, columns, leafCount, records, values, levels, windowing);
}

/** One field in Values of an answer, from its parts column by column. */
export function answerValues(field: string, columns: PartColumns, leafCount: number): PivotAnswerValues {
    return makeValues(field, columns, leafCount);
}

/** The parts of a field in Values, column by column. */
export function answerColumns(values: PivotAnswerValues): PartColumns {
    return columnsOfValues(values);
}
