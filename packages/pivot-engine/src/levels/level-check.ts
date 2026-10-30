// Whether a server's Level Aggregates add up (ADR-0370 section 3): the parts of every sibling set shown,
// composed, against their parent's. Counts, extremes and exact sums are compared exactly; a double part (a
// sum, a product, a running variance) within the rounding its composition can make on either side, so an
// honest server is never refused for the order it summed in, and a figure the rounding cannot explain is.
//
// The bounds, fixed by test/level-check.test.ts before Level Aggregates are built: with u = 2^-53 and
// γ = k·u / (1 − k·u) for k the parent's numbers plus the children composed, any order of summing N numbers
// is within γ·Σ|x| of the true sum, and any order of multiplying them within γ·|Π x|. Each side may be that
// far off, so a sum agrees within 2γ·Σ|x| (Σ|x| the parent's absolute sum, itself within γ), and a product
// within 2γ of the larger. A running variance's mean and sum of squared deviations are bounded through S =
// m2 + n·mean², which is Σx² for the parent: the naive m2 = Σx² − (Σx)²/n a SQL server computes is within
// 3γ·S, and Welford's within the same order, so the mean agrees within 4γ·√S and m2 within 8γ·S. A part
// that is not finite is #NUM! in the parent and its children alike, so only the counts are compared; a bound
// that is not finite cannot be checked, and refuses.

import type { SnapshotDecimal } from '@ex-grid/data';
import { decimalCompare, decimalToNumber, doubleOfWide, scaledTo } from '../aggregates/exact-decimal';
import type { PartColumns } from '../aggregates/parts';
import type { PivotNumber } from '../cube/answer';
import { elementAt } from '../must';

/** One group's parts of one Value Field, as a server answers them under Level Aggregates. */
export interface LevelParts {
    /** Values that are not Blank. */
    readonly values: number;
    /** Values that are numbers, non-finite ones included. */
    readonly numbers: number;
    /** Whether a non-finite number is among them: every numeric part is then #NUM!. */
    readonly nonFinite: boolean;
    /** The sum, exact while every number was. */
    readonly sum?: PivotNumber;
    /** The sum of the numbers' magnitudes, carried with a sum that is a double: what bounds its rounding. */
    readonly absoluteSum?: number;
    /** The smallest number. */
    readonly min?: PivotNumber;
    /** The largest number. */
    readonly max?: PivotNumber;
    /** The product. */
    readonly product?: number;
    /** The running mean. */
    readonly mean?: number;
    /** The running sum of squared deviations from the mean. */
    readonly m2?: number;
}

/** One cell's parts of one Value Field as {@link levelMismatch} reads them: every part its columns carry, but
 * for a cell of no numbers or of #NUM!, whose parts are not read. */
export function levelPartsAt(columns: PartColumns, cell: number): LevelParts {
    const counts = elementAt(columns.counts, cell);
    const held = { values: counts.values, numbers: counts.numbers, nonFinite: counts.nonFinite };
    if (counts.numbers === 0 || counts.nonFinite) return held;
    const sum = columns.sums?.[cell];
    const extremes = columns.extremes?.[cell];
    const product = columns.products?.[cell];
    const variance = columns.variances?.[cell];
    const absoluteSum = columns.absolutes?.[cell];
    return {
        ...held,
        ...(sum === undefined ? {} : { sum: sum.inexact ? { kind: 'double', value: sum.double } : { kind: 'exact', value: sum.exact } }),
        ...(sum !== undefined && sum.inexact && absoluteSum !== undefined ? { absoluteSum } : {}),
        ...(extremes === undefined ? {} : extremes.inexact
            ? { min: { kind: 'double', value: extremes.min }, max: { kind: 'double', value: extremes.max } }
            : { min: { kind: 'exact', value: extremes.exactMin }, max: { kind: 'exact', value: extremes.exactMax } }),
        ...(product === undefined ? {} : { product }),
        ...(variance === undefined ? {} : { mean: variance.mean, m2: variance.m2 }),
    };
}

/** The part of a group that does not add up from its children, named as a refusal says it. */
export type LevelMismatch =
    | 'values' | 'numbers' | 'nonFinite' | 'sum' | 'absoluteSum' | 'min' | 'max' | 'product' | 'mean' | 'm2';

const unit = 2 ** -53;
const smallestNormal = 2 ** -1022;

function gamma(k: number): number {
    return k * unit / (1 - k * unit);
}

/**
 * The first part of `parent` its `children` do not compose to, or `undefined` where every part agrees. A part
 * the parent carries and a child does not, or the reverse, does not add up. The children are the whole
 * sibling set: every child of the parent that some record carries, crossed with the same column group.
 */
export function levelMismatch(parent: LevelParts, children: readonly LevelParts[]): LevelMismatch | undefined {
    let values = 0;
    let numbers = 0;
    let nonFinite = false;
    for (const child of children) {
        values += child.values;
        numbers += child.numbers;
        nonFinite ||= child.nonFinite;
    }
    if (values !== parent.values) return 'values';
    if (numbers !== parent.numbers) return 'numbers';
    if (nonFinite !== parent.nonFinite) return 'nonFinite';
    if (nonFinite || numbers === 0) return undefined;
    const numbered = children.filter(child => child.numbers > 0);
    const g = gamma(numbers + numbered.length);
    return sumMismatch(parent, numbered, g)
        ?? extremeMismatch('min', parent.min, numbered.map(c => c.min), -1)
        ?? extremeMismatch('max', parent.max, numbered.map(c => c.max), 1)
        ?? productMismatch(parent.product, numbered.map(c => c.product), g)
        ?? varianceMismatch(parent, numbered, g);
}

function carried<T>(parent: T | undefined, children: readonly (T | undefined)[]): { parent: T; children: readonly T[] } | 'none' | 'mixed' {
    const held = children.filter((c): c is T => c !== undefined);
    if (parent === undefined) return held.length === 0 ? 'none' : 'mixed';
    return held.length === children.length ? { parent, children: held } : 'mixed';
}

function within(a: number, b: number, bound: number): boolean {
    return Number.isFinite(bound) && Math.abs(a - b) <= bound;
}

function sumMismatch(parent: LevelParts, children: readonly LevelParts[], g: number): LevelMismatch | undefined {
    const sums = carried(parent.sum, children.map(c => c.sum));
    if (sums === 'none') return undefined;
    if (sums === 'mixed') return 'sum';
    const exact = sums.children.every(s => s.kind === 'exact');
    if (exact && sums.parent.kind === 'exact') {
        return exactSumOf(sums.children.map(s => (s.kind === 'exact' ? s.value : unreachableKind())), sums.parent.value) ? undefined : 'sum';
    }
    // A double parent bounds both sides by its absolute sum; an exact parent over a double child cannot be.
    if (sums.parent.kind === 'exact') return 'sum';
    const absolute = parent.absoluteSum;
    if (absolute === undefined || !(absolute >= Math.abs(sums.parent.value) * (1 - g))) return 'absoluteSum';
    const composed = exact ? exactAsDouble(sums.children) : compensated(sums.children.map(numberOf));
    if (!within(composed, sums.parent.value, 2 * g * absolute / (1 - g))) return 'sum';
    const absolutes = children.map(c => c.absoluteSum);
    if (absolutes.every((a): a is number => a !== undefined)) {
        const total = compensated(absolutes);
        if (!within(total, absolute, 2 * g * Math.max(total, absolute))) return 'absoluteSum';
    }
    return undefined;
}

function unreachableKind(): never {
    throw new Error('Internal: an exact sum was read as a double.');
}

function numberOf(number: PivotNumber): number {
    return number.kind === 'exact' ? decimalToNumber(number.value) : number.value;
}

// The children's exact sums added without a limit, against the parent's.
function exactSumOf(children: readonly SnapshotDecimal[], parent: SnapshotDecimal): boolean {
    const scale = Math.max(parent.scale, ...children.map(c => c.scale));
    let total = 0n;
    for (const child of children) total += scaledTo(child, scale);
    return total === scaledTo(parent, scale);
}

function exactAsDouble(children: readonly PivotNumber[]): number {
    const decimals = children.map(c => (c.kind === 'exact' ? c.value : unreachableKind()));
    const scale = Math.max(...decimals.map(c => c.scale));
    let total = 0n;
    for (const child of decimals) total += scaledTo(child, scale);
    return doubleOfWide(total, scale);
}

// Neumaier's compensated sum, so the composition adds no rounding of its own worth the name.
function compensated(numbers: readonly number[]): number {
    let total = 0;
    let compensation = 0;
    for (const value of numbers) {
        const next = total + value;
        compensation += Math.abs(total) >= Math.abs(value) ? (total - next) + value : (value - next) + total;
        total = next;
    }
    return total + compensation;
}

function extremeMismatch(name: 'min' | 'max', parent: PivotNumber | undefined, children: readonly (PivotNumber | undefined)[], sign: number): LevelMismatch | undefined {
    const extremes = carried(parent, children);
    if (extremes === 'none') return undefined;
    if (extremes === 'mixed') return name;
    // The parent is a double exactly when a child is: one double record makes both so.
    const inexact = extremes.children.some(e => e.kind === 'double');
    if (inexact !== (extremes.parent.kind === 'double')) return name;
    let best = extremes.children[0];
    for (const child of extremes.children) {
        if (best === undefined || compareNumbers(child, best) * sign > 0) best = child;
    }
    return best !== undefined && compareNumbers(best, extremes.parent) === 0 ? undefined : name;
}

function compareNumbers(a: PivotNumber, b: PivotNumber): number {
    if (a.kind === 'exact' && b.kind === 'exact') return decimalCompare(a.value, b.value);
    const x = numberOf(a);
    const y = numberOf(b);
    return x < y ? -1 : x > y ? 1 : 0;
}

function productMismatch(parent: number | undefined, children: readonly (number | undefined)[], g: number): LevelMismatch | undefined {
    const products = carried(parent, children);
    if (products === 'none') return undefined;
    if (products === 'mixed') return 'product';
    let composed = 1;
    for (const child of products.children) composed *= child;
    const expected = products.parent;
    if (composed === expected) return undefined;
    // Below the normal range a product's relative rounding is unbounded, and what it shows is zero either way.
    if (Math.abs(composed) < smallestNormal && Math.abs(expected) < smallestNormal) return undefined;
    return within(composed, expected, 2 * g * Math.max(Math.abs(composed), Math.abs(expected))) ? undefined : 'product';
}

function varianceMismatch(parent: LevelParts, children: readonly LevelParts[], g: number): LevelMismatch | undefined {
    const means = carried(parent.mean, children.map(c => c.mean));
    const m2s = carried(parent.m2, children.map(c => c.m2));
    if (means === 'none' && m2s === 'none') return undefined;
    if (means === 'none' || means === 'mixed') return 'mean';
    if (m2s === 'none' || m2s === 'mixed') return 'm2';
    let count = 0;
    let mean = 0;
    let m2 = 0;
    children.forEach((child, i) => {
        const childMean = elementAt(means.children, i);
        const childM2 = elementAt(m2s.children, i);
        if (count === 0) {
            count = child.numbers;
            mean = childMean;
            m2 = childM2;
            return;
        }
        const n = count + child.numbers;
        const delta = childMean - mean;
        mean += delta * child.numbers / n;
        m2 += childM2 + delta * delta * count * child.numbers / n;
        count = n;
    });
    const squares = m2s.parent + parent.numbers * means.parent * means.parent;
    if (!within(mean, means.parent, 4 * g * Math.sqrt(squares))) return 'mean';
    return within(m2, m2s.parent, 8 * g * squares) ? undefined : 'm2';
}
