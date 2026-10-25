// A Pivot Source's answer to a question (ADR-0066): the Leaf Aggregates — one leaf per combination of the
// row and column fields' Items that has an included record, each with its Items, its record count and the
// parts asked for of each field in Values — under the Source Version they were computed at; or a refusal,
// which is an answer and not a failure. The C# engine's PivotAnswer, PivotSourceRefusal, PivotNumber and
// PivotAnswerBuilder.

import type { SnapshotDecimal } from '@ex-grid/data';
import { decimalCanonical, decimalEquals, decimalToNumber } from '../aggregates/exact-decimal';
import { PartColumns, partsText } from '../aggregates/parts';
import { itemKeyText, pivotItemKey, publicKeyId, type PivotItemKey } from '../items/item-key';
import { aggregationTitle, type PivotAggregation } from '../layout/pivot-enums';
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

    /** The index among {@link PivotAnswerAxis.items} of a leaf's Item. */
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

let makeAnswer: (sourceVersion: string, rows: readonly PivotAnswerAxis[], columns: readonly PivotAnswerAxis[], leafCount: number,
    records: readonly number[], values: readonly PivotAnswerValues[]) => PivotAnswer;

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

    private constructor(
        sourceVersion: string | undefined, refusal: PivotSourceRefusal | undefined, rows: readonly PivotAnswerAxis[],
        columns: readonly PivotAnswerAxis[], leafCount: number, records: readonly number[], values: readonly PivotAnswerValues[],
    ) {
        this.#sourceVersion = sourceVersion;
        this.refusal = refusal;
        this.#rows = Object.freeze([...rows]);
        this.#columns = Object.freeze([...columns]);
        this.leafCount = leafCount;
        this.#records = records;
        this.#values = Object.freeze([...values]);
    }

    static {
        makeAnswer = (sourceVersion, rows, columns, leafCount, records, values) =>
            new PivotAnswer(sourceVersion, undefined, rows, columns, leafCount, records, values);
    }

    /** A refusal, as an answer. */
    static refused(refusal: PivotSourceRefusal): PivotAnswer {
        return new PivotAnswer(undefined, refusal, [], [], 0, [], []);
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

    /** What does not answer the question, in words, or `undefined` when the answer answers it. */
    mismatch(query: PivotQuery): string | undefined {
        if (this.refusal !== undefined) return `the source refused: ${this.refusal.message}`;
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
        if (this.leafCount > query.maxLeaves) return `it holds ${this.leafCount} leaves, and the question allows ${query.maxLeaves}`;
        return undefined;
    }

    #refusedError(): Error {
        return new Error(`The source refused to answer: ${this.refusal?.message ?? 'without a reason'}`); // oxlint-disable-line ex-grid/no-stand-in-default -- only a refusal reaches here, and a refusal says why
    }
}

/**
 * Builds an answer a leaf at a time, as a server's rows arrive (ADR-0066): each leaf named by its Items, one
 * per row and column field, with its record count, and the parts asked for of each field in Values set on
 * it. Two leaves for one combination of Items are refused, and so is a part the question did not ask for.
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
        const indexes = items.map((item, level) => {
            const key = pivotItemKey(item.kind, item.value);
            const id = publicKeyId(key);
            const index = elementAt(this.#itemIndex, level);
            const found = index.get(id);
            if (found !== undefined) return found;
            const made = elementAt(this.#items, level).length;
            elementAt(this.#items, level).push(key);
            index.set(id, made);
            return made;
        });
        const leafKey = indexes.join(',');
        if (this.#leaves.has(leafKey)) throw new TypeError(`The answer already has a leaf for [${items.map(itemKeyText).join(', ')}].`);
        const leaf = this.#records.length;
        this.#leaves.set(leafKey, leaf);
        indexes.forEach((index, level) => elementAt(this.#itemOfLeaf, level).push(index));
        this.#records.push(records);
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
        return makeAnswer(this.#sourceVersion, axes.slice(0, rowCount), axes.slice(rowCount), leafCount, [...this.#records], values);
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

/** An answer with its leaves, as a pass or a builder makes it. */
export function madeAnswer(sourceVersion: string, rows: readonly PivotAnswerAxis[], columns: readonly PivotAnswerAxis[], leafCount: number,
    records: readonly number[], values: readonly PivotAnswerValues[]): PivotAnswer {
    return makeAnswer(sourceVersion, rows, columns, leafCount, records, values);
}

/** One field in Values of an answer, from its parts column by column. */
export function answerValues(field: string, columns: PartColumns, leafCount: number): PivotAnswerValues {
    return makeValues(field, columns, leafCount);
}

/** The parts of a field in Values, column by column. */
export function answerColumns(values: PivotAnswerValues): PartColumns {
    return columnsOfValues(values);
}
