// The parts a Leaf Aggregate is made of, and the arithmetic over them (ADR-0060/0066/0130): how one number
// is folded into each part, how two parts of one field merge, how a part is finished, and how an
// Aggregation is read from the parts. Every part combines exactly — counts and exact sums add, extremes
// compare, products multiply and running variances merge — so a total merged from its pieces is the total
// of its values, never a total of totals. Money stays exact until it cannot: then double, Excel's own
// arithmetic. ExGrid.Data's AggregateArithmetic and ExPivot's PartColumns, transcribed: the order of every
// double operation is the C# engine's, so a sum agrees with it to the last bit.

import type { SnapshotDecimal } from '@ex-grid/data';
import { pivotErrorText } from '../items/item-key';
import type { PivotAggregation } from '../layout/pivot-enums';
import { elementAt, unreachable } from '../must';
import { decimalAdd, decimalCanonical, decimalCompare, decimalDivide, decimalOfWhole, decimalToNumber, decimalZero } from './exact-decimal';

/** The text of a division by zero. */
export const divideByZeroText = '#DIV/0!';

/** The parts a question asks of one field in Values, as bits: the counts always travel, and are 0. */
export const partSum = 1;
/** Max and Min read the extremes. */
export const partExtremes = 2;
/** Product reads the product. */
export const partProduct = 4;
/** StdDev, StdDevp, Var and Varp read the running variance. */
export const partVariance = 8;
/** Every part. */
export const allParts = partSum | partExtremes | partProduct | partVariance;

/** The parts an Aggregation reads. */
export function partsOf(aggregation: PivotAggregation): number {
    switch (aggregation) {
        case 'sum':
        case 'average': return partSum;
        case 'max':
        case 'min': return partExtremes;
        case 'product': return partProduct;
        case 'stdDev':
        case 'stdDevp':
        case 'var':
        case 'varp': return partVariance;
        case 'count':
        case 'countNumbers': return 0;
        default: throw new RangeError(`Unknown PivotAggregation '${String(aggregation satisfies never)}'.`);
    }
}

/** The parts' names as C#'s PivotParts writes them: `Sum, Extremes`, or `Counts` for none. */
export function partsText(parts: number): string {
    const names: string[] = [];
    if ((parts & partSum) !== 0) names.push('Sum');
    if ((parts & partExtremes) !== 0) names.push('Extremes');
    if ((parts & partProduct) !== 0) names.push('Product');
    if ((parts & partVariance) !== 0) names.push('Variance');
    return names.length === 0 ? 'Counts' : names.join(', ');
}

/** The counts every summarised set of values carries. */
export interface CountsPart {
    /** Values that are not Blank — Excel's COUNTA. */
    values: number;
    /** Values that are numbers, non-finite ones included. */
    numbers: number;
    /** A non-finite number was among the values: every numeric Aggregation is #NUM!. */
    nonFinite: boolean;
}

/** The sum part: exact while every number was, double once one was not or the exact sum overflowed. */
export interface SumPart {
    exact: SnapshotDecimal;
    double: number;
    /** Neumaier's running error of `double`, folded in once the part is finished. */
    compensation: number;
    inexact: boolean;
}

/** The extremes part: exact while every number was, double once one was not. */
export interface ExtremesPart {
    exactMin: SnapshotDecimal;
    exactMax: SnapshotDecimal;
    min: number;
    max: number;
    inexact: boolean;
}

/** The running variance: Welford's mean and sum of squared deviations, merged by Chan's rule. */
export interface VariancePart {
    mean: number;
    m2: number;
}

function noCounts(): CountsPart {
    return { values: 0, numbers: 0, nonFinite: false };
}

/** A sum that holds no number. */
export function noSum(): SumPart {
    return { exact: decimalZero, double: 0, compensation: 0, inexact: false };
}

/** Extremes that hold no number. */
export function noExtremes(): ExtremesPart {
    return { exactMin: decimalZero, exactMax: decimalZero, min: 0, max: 0, inexact: false };
}

function noVariance(): VariancePart {
    return { mean: 0, m2: 0 };
}

// ---- One number ---------------------------------------------------------------------------------

/** Folds an exact number into a sum; `first` when the sum held no number before. */
export function addExactToSum(sum: SumPart, value: SnapshotDecimal, first: boolean): void {
    if (sum.inexact) {
        neumaier(sum, decimalToNumber(value));
        return;
    }
    if (first) {
        sum.exact = value;
        return;
    }
    const added = decimalAdd(sum.exact, value);
    if (added !== undefined) {
        sum.exact = added;
        return;
    }
    sumToInexact(sum);
    neumaier(sum, decimalToNumber(value));
}

/** Folds a finite double into a sum; `first` when the sum held no number before. */
export function addDoubleToSum(sum: SumPart, value: number, first: boolean): void {
    if (first) {
        sum.inexact = true;
        sum.double = value;
        return;
    }
    if (!sum.inexact) sumToInexact(sum);
    neumaier(sum, value);
}

/** A sum leaves exactness for double, starting from the exact sum it had, converted. */
export function sumToInexact(sum: SumPart): void {
    sum.inexact = true;
    sum.double = decimalToNumber(sum.exact);
    sum.compensation = 0;
    sum.exact = decimalZero;
}

/** Adds to a double sum with Neumaier's compensation. */
export function neumaier(sum: SumPart, value: number): void {
    const total = sum.double + value;
    sum.compensation += Math.abs(sum.double) >= Math.abs(value)
        ? (sum.double - total) + value
        : (value - total) + sum.double;
    sum.double = total;
}

/** Folds an exact number into extremes; `first` when they held no number before. */
export function addExactToExtremes(extremes: ExtremesPart, value: SnapshotDecimal, first: boolean): void {
    if (extremes.inexact) {
        addInexact(extremes, decimalToNumber(value));
        return;
    }
    if (first) {
        extremes.exactMin = value;
        extremes.exactMax = value;
        return;
    }
    if (decimalCompare(value, extremes.exactMin) < 0) extremes.exactMin = value;
    if (decimalCompare(value, extremes.exactMax) > 0) extremes.exactMax = value;
}

/** Folds a finite double into extremes; `first` when they held no number before. */
export function addDoubleToExtremes(extremes: ExtremesPart, value: number, first: boolean): void {
    if (first) {
        extremes.inexact = true;
        extremes.min = value;
        extremes.max = value;
        return;
    }
    if (!extremes.inexact) extremesToInexact(extremes);
    addInexact(extremes, value);
}

// The conversion is monotonic, so the double extremes of exact numbers are their exact extremes converted.
function extremesToInexact(extremes: ExtremesPart): void {
    extremes.inexact = true;
    extremes.min = decimalToNumber(extremes.exactMin);
    extremes.max = decimalToNumber(extremes.exactMax);
    extremes.exactMin = decimalZero;
    extremes.exactMax = decimalZero;
}

function addInexact(extremes: ExtremesPart, value: number): void {
    if (value < extremes.min) extremes.min = value;
    if (value > extremes.max) extremes.max = value;
}

/** Folds a number into a running variance by Welford's rule; `count` is the numbers with this one. */
export function addToVariance(variance: VariancePart, value: number, count: number): void {
    const delta = value - variance.mean;
    variance.mean += delta / count;
    variance.m2 += delta * (value - variance.mean);
}

// ---- Merging ------------------------------------------------------------------------------------

function mergeSum(sum: SumPart, other: SumPart, first: boolean): void {
    if (first) {
        Object.assign(sum, other);
        return;
    }
    if (!sum.inexact && !other.inexact) {
        const added = decimalAdd(sum.exact, other.exact);
        if (added !== undefined) {
            sum.exact = added;
            return;
        }
        sumToInexact(sum);
        neumaier(sum, decimalToNumber(other.exact));
        return;
    }
    if (!sum.inexact) sumToInexact(sum);
    if (other.inexact) {
        neumaier(sum, other.double);
        sum.compensation += other.compensation;
    } else {
        neumaier(sum, decimalToNumber(other.exact));
    }
}

function mergeExtremes(extremes: ExtremesPart, other: ExtremesPart, first: boolean): void {
    if (first) {
        Object.assign(extremes, other);
        return;
    }
    if (!extremes.inexact && !other.inexact) {
        if (decimalCompare(other.exactMin, extremes.exactMin) < 0) extremes.exactMin = other.exactMin;
        if (decimalCompare(other.exactMax, extremes.exactMax) > 0) extremes.exactMax = other.exactMax;
        return;
    }
    if (!extremes.inexact) extremesToInexact(extremes);
    addInexact(extremes, other.inexact ? other.min : decimalToNumber(other.exactMin));
    addInexact(extremes, other.inexact ? other.max : decimalToNumber(other.exactMax));
}

function mergeVariance(variance: VariancePart, other: VariancePart, count: number, otherCount: number): void {
    if (count === 0) {
        Object.assign(variance, other);
        return;
    }
    const n = count + otherCount;
    const delta = other.mean - variance.mean;
    variance.mean += delta * otherCount / n;
    variance.m2 += other.m2 + (delta * delta * count * otherCount / n);
}

// ---- Finishing ----------------------------------------------------------------------------------

/** Ends a sum: a double takes its compensation in, and the figures it does not use are zeroed. */
export function finishSum(sum: SumPart): void {
    if (sum.inexact) {
        sum.double = sum.double + sum.compensation;
        sum.compensation = 0;
        sum.exact = decimalZero;
    } else {
        sum.double = 0;
        sum.compensation = 0;
    }
}

/** Ends extremes, as {@link finishSum} ends a sum. */
export function finishExtremes(extremes: ExtremesPart): void {
    if (extremes.inexact) {
        extremes.exactMin = decimalZero;
        extremes.exactMax = decimalZero;
    } else {
        extremes.min = 0;
        extremes.max = 0;
    }
}

// ---- Reading an Aggregation ---------------------------------------------------------------------

/** An Aggregation's answer before it is shown: nothing, a number (exact where it could be) or an error
 * value's text. */
export type AggregateValue =
    | { readonly kind: 'empty' }
    | { readonly kind: 'error'; readonly error: string }
    | { readonly kind: 'number'; readonly number: number; readonly exact: SnapshotDecimal | undefined };

/** No value at all: an empty cell. */
export const emptyValue: AggregateValue = Object.freeze({ kind: 'empty' });

/** #DIV/0!. */
export const divideByZeroValue: AggregateValue = Object.freeze({ kind: 'error', error: divideByZeroText });

/** #NUM!. */
export const numberErrorValue: AggregateValue = Object.freeze({ kind: 'error', error: pivotErrorText });

/** An exact number. */
export function exactValue(value: SnapshotDecimal): AggregateValue {
    return Object.freeze({ kind: 'number', number: decimalToNumber(value), exact: value });
}

/** A double. */
export function doubleValue(value: number): AggregateValue {
    return Object.freeze({ kind: 'number', number: value, exact: undefined });
}

// A double that is not finite is #NUM!.
function checked(value: number): AggregateValue {
    return Number.isFinite(value) ? doubleValue(value) : numberErrorValue;
}

function need<T>(part: T | undefined, aggregation: PivotAggregation): T {
    if (part === undefined) throw new Error(`${aggregation} reads a part that was not accumulated (ADR-0066).`);
    return part;
}

/** An Aggregation's answer from its parts, by the table in ADR-0060. */
export function readAggregate(
    aggregation: PivotAggregation,
    counts: CountsPart,
    sum: SumPart | undefined,
    extremes: ExtremesPart | undefined,
    product: number | undefined,
    variance: VariancePart | undefined,
): AggregateValue {
    if (counts.values === 0) return emptyValue;
    if (aggregation === 'count') return exactValue(decimalOfWhole(BigInt(counts.values)));
    if (aggregation === 'countNumbers') return exactValue(decimalOfWhole(BigInt(counts.numbers)));
    const noNumber = counts.numbers === 0;
    switch (aggregation) {
        case 'sum':
        case 'max':
        case 'min':
        case 'product':
            if (noNumber) return exactValue(decimalZero);
            break;
        case 'average':
        case 'stdDev':
        case 'stdDevp':
        case 'var':
        case 'varp':
            if (noNumber) return divideByZeroValue;
            break;
        default:
            return unreachable(aggregation);
    }
    if (counts.nonFinite) return numberErrorValue;
    const n = counts.numbers;
    switch (aggregation) {
        case 'sum': {
            const s = need(sum, aggregation);
            return s.inexact ? checked(s.double + s.compensation) : exactValue(s.exact);
        }
        case 'average': {
            const s = need(sum, aggregation);
            return s.inexact ? checked((s.double + s.compensation) / n) : exactValue(decimalDivide(s.exact, n));
        }
        case 'max': {
            const e = need(extremes, aggregation);
            return e.inexact ? checked(e.max) : exactValue(e.exactMax);
        }
        case 'min': {
            const e = need(extremes, aggregation);
            return e.inexact ? checked(e.min) : exactValue(e.exactMin);
        }
        case 'product':
            return checked(need(product, aggregation));
        case 'stdDev':
            return n < 2 ? divideByZeroValue : checked(Math.sqrt(Math.max(0, need(variance, aggregation).m2) / (n - 1)));
        case 'stdDevp':
            return checked(Math.sqrt(Math.max(0, need(variance, aggregation).m2) / n));
        case 'var':
            return n < 2 ? divideByZeroValue : checked(Math.max(0, need(variance, aggregation).m2) / (n - 1));
        case 'varp':
            return checked(Math.max(0, need(variance, aggregation).m2) / n);
        default:
            return unreachable(aggregation);
    }
}

// ---- Column by column ---------------------------------------------------------------------------

/**
 * One field in Values, held column by column over a set of cells — a question's leaves, or every cell of a
 * cube (ADR-0066). Only the parts asked for have columns, and only they are accumulated.
 */
export class PartColumns {
    readonly parts: number;
    readonly counts: CountsPart[] = [];
    readonly sums: SumPart[] | undefined;
    readonly extremes: ExtremesPart[] | undefined;
    readonly products: number[] | undefined;
    readonly variances: VariancePart[] | undefined;
    /** The sum of the magnitudes of each cell's numbers, as doubles added in the records' order: what bounds
     * the rounding of a sum that is a double, which a level answer carries with it (ADR-0370 section 3).
     * Kept only where {@link trackAbsolutes} asked for it, beside a sum. */
    absolutes: number[] | undefined;

    constructor(parts: number) {
        this.parts = parts;
        this.sums = (parts & partSum) !== 0 ? [] : undefined;
        this.extremes = (parts & partExtremes) !== 0 ? [] : undefined;
        this.products = (parts & partProduct) !== 0 ? [] : undefined;
        this.variances = (parts & partVariance) !== 0 ? [] : undefined;
    }

    /** How many cells there are. */
    get count(): number {
        return this.counts.length;
    }

    /** Keeps {@link absolutes} beside the sum from now on; the cells hold none yet. */
    trackAbsolutes(): void {
        if (this.sums === undefined) throw new Error('Internal: magnitudes are kept beside a sum.');
        this.absolutes ??= this.counts.map(() => 0);
    }

    /** Adds a number's magnitude to a cell's, where they are kept. */
    addAbsolute(cell: number, value: number): void {
        if (this.absolutes !== undefined) this.absolutes[cell] = elementAt(this.absolutes, cell) + Math.abs(value);
    }

    /** A new cell, holding no value; its index. */
    addCell(): number {
        const cell = this.counts.length;
        this.counts.push(noCounts());
        this.sums?.push(noSum());
        this.extremes?.push(noExtremes());
        this.products?.push(0);
        this.variances?.push(noVariance());
        this.absolutes?.push(0);
        return cell;
    }

    /** Empties a cell, as {@link addCell} makes it. */
    resetCell(cell: number): void {
        if (cell < 0 || cell >= this.counts.length) throw new RangeError(`There is no cell ${cell} of ${this.counts.length}.`);
        this.counts[cell] = noCounts();
        if (this.sums !== undefined) this.sums[cell] = noSum();
        if (this.extremes !== undefined) this.extremes[cell] = noExtremes();
        if (this.products !== undefined) this.products[cell] = 0;
        if (this.variances !== undefined) this.variances[cell] = noVariance();
        if (this.absolutes !== undefined) this.absolutes[cell] = 0;
    }

    /** Cells until there are `count`. */
    ensureCells(count: number): void {
        while (this.counts.length < count) this.addCell();
    }

    /** Cell `from` of `other`, which holds at least these parts, as a new cell of this one. */
    copyCell(other: PartColumns, from: number): number {
        const cell = this.counts.length;
        this.counts.push({ ...elementAt(other.counts, from) });
        if (this.sums !== undefined) this.sums.push({ ...elementAt(must(other.sums), from) });
        if (this.extremes !== undefined) this.extremes.push({ ...elementAt(must(other.extremes), from) });
        if (this.products !== undefined) this.products.push(elementAt(must(other.products), from));
        if (this.variances !== undefined) this.variances.push({ ...elementAt(must(other.variances), from) });
        if (this.absolutes !== undefined) this.absolutes.push(elementAt(must(other.absolutes), from));
        return cell;
    }

    /** Writes over cell `cell` a copy of `other`'s cell `from`, which holds at least these parts. */
    setCell(cell: number, other: PartColumns, from: number): void {
        if (cell < 0 || cell >= this.counts.length) throw new RangeError(`There is no cell ${cell} of ${this.counts.length}.`);
        this.counts[cell] = { ...elementAt(other.counts, from) };
        if (this.sums !== undefined) this.sums[cell] = { ...elementAt(must(other.sums), from) };
        if (this.extremes !== undefined) this.extremes[cell] = { ...elementAt(must(other.extremes), from) };
        if (this.products !== undefined) this.products[cell] = elementAt(must(other.products), from);
        if (this.variances !== undefined) this.variances[cell] = { ...elementAt(must(other.variances), from) };
        if (this.absolutes !== undefined) this.absolutes[cell] = elementAt(must(other.absolutes), from);
    }

    /** Folds in a value that is neither Blank nor a number: counted, never a number. */
    addOther(cell: number): void {
        elementAt(this.counts, cell).values++;
    }

    /** Folds in an exact number. */
    addExact(cell: number, value: SnapshotDecimal): void {
        const counts = elementAt(this.counts, cell);
        counts.values++;
        // Once a non-finite number is in, every numeric Aggregation is #NUM! and no part is read.
        if (!counts.nonFinite) {
            const first = counts.numbers === 0;
            if (this.sums !== undefined) addExactToSum(elementAt(this.sums, cell), value, first);
            this.addAbsolute(cell, decimalToNumber(value));
            if (this.extremes !== undefined) addExactToExtremes(elementAt(this.extremes, cell), value, first);
            if (this.products !== undefined || this.variances !== undefined) {
                this.addInexactParts(cell, decimalToNumber(value), counts.numbers + 1);
            }
        }
        counts.numbers++;
    }

    /** Folds in a double: not exact, and #NUM! when it is not finite. */
    addDouble(cell: number, value: number): void {
        const counts = elementAt(this.counts, cell);
        counts.values++;
        if (!Number.isFinite(value)) {
            counts.nonFinite = true;
            counts.numbers++;
            return;
        }
        if (!counts.nonFinite) {
            const first = counts.numbers === 0;
            if (this.sums !== undefined) addDoubleToSum(elementAt(this.sums, cell), value, first);
            this.addAbsolute(cell, value);
            if (this.extremes !== undefined) addDoubleToExtremes(elementAt(this.extremes, cell), value, first);
            this.addInexactParts(cell, value, counts.numbers + 1);
        }
        counts.numbers++;
    }

    /** Folds one number into the parts kept in double — the product and the running variance; `count` is
     * the cell's numbers with this one. */
    addInexactParts(cell: number, value: number, count: number): void {
        if (this.products !== undefined) this.products[cell] = count === 1 ? value : elementAt(this.products, cell) * value;
        if (this.variances !== undefined) addToVariance(elementAt(this.variances, cell), value, count);
    }

    /** Ends accumulation: a double sum takes its compensation in, and the figures a part does not use are
     * zeroed, so a part is one value whatever path it took. */
    finish(): void {
        this.sums?.forEach(finishSum);
        this.extremes?.forEach(finishExtremes);
    }

    /** Writes each exact sum and exact extreme of cells [from, to) without trailing zeros (ADR-0064); every
     * cell when no range is given. */
    canonicalize(from = 0, to = this.counts.length): void {
        for (let cell = from; cell < to; cell++) {
            const sum = this.sums?.[cell];
            if (sum !== undefined && !sum.inexact) sum.exact = decimalCanonical(sum.exact);
            const extremes = this.extremes?.[cell];
            if (extremes !== undefined && !extremes.inexact) {
                extremes.exactMin = decimalCanonical(extremes.exactMin);
                extremes.exactMax = decimalCanonical(extremes.exactMax);
            }
        }
    }

    /** Folds `other`'s cell `from`, of the same field, into cell `cell`. */
    merge(cell: number, other: PartColumns, from: number): void {
        const counts = elementAt(this.counts, cell);
        const theirs = elementAt(other.counts, from);
        counts.values += theirs.values;
        counts.nonFinite ||= theirs.nonFinite;
        if (theirs.numbers === 0) return;
        const first = counts.numbers === 0;
        if (this.sums !== undefined) mergeSum(elementAt(this.sums, cell), elementAt(must(other.sums), from), first);
        if (this.absolutes !== undefined) this.absolutes[cell] = elementAt(this.absolutes, cell) + elementAt(must(other.absolutes), from);
        if (this.extremes !== undefined) mergeExtremes(elementAt(this.extremes, cell), elementAt(must(other.extremes), from), first);
        if (this.products !== undefined) {
            const product = elementAt(must(other.products), from);
            this.products[cell] = first ? product : elementAt(this.products, cell) * product;
        }
        if (this.variances !== undefined) {
            mergeVariance(elementAt(this.variances, cell), elementAt(must(other.variances), from), counts.numbers, theirs.numbers);
        }
        counts.numbers += theirs.numbers;
    }

    /** Whether this holds every part `aggregation` reads. */
    answers(aggregation: PivotAggregation): boolean {
        const needed = partsOf(aggregation);
        return (this.parts & needed) === needed;
    }

    /** An Aggregation's answer at a cell, by the table in ADR-0060. */
    read(cell: number, aggregation: PivotAggregation): AggregateValue {
        const counts = elementAt(this.counts, cell);
        const reads = aggregation !== 'count' && aggregation !== 'countNumbers';
        if (counts.values !== 0 && reads && !this.answers(aggregation)) {
            throw new Error(`${aggregation} reads a part this field was not aggregated with (${partsText(this.parts)}); ask again with its parts (ADR-0066).`);
        }
        return readAggregate(aggregation, counts,
            this.sums === undefined ? undefined : elementAt(this.sums, cell),
            this.extremes === undefined ? undefined : elementAt(this.extremes, cell),
            this.products === undefined ? undefined : elementAt(this.products, cell),
            this.variances === undefined ? undefined : elementAt(this.variances, cell));
    }
}

function must<T>(part: T | undefined): T {
    if (part === undefined) throw new Error('Internal: a part the cells merge from is not held.');
    return part;
}
