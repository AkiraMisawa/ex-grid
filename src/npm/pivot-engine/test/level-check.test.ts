// Layer 1: the check of a server's Level Aggregates against their parent (ADR-0370 section 3), and the bounds
// it allows a double part, fixed here before Level Aggregates are built. An honest answer is made two ways a
// server might make it — the engine's own passes (compensated sums, Welford's variance) and a SQL server's
// (a plain sum, m2 as Σx² − (Σx)²/n) — each in an order of its own, over data chosen to make rounding show:
// cancellation, magnitudes twenty orders apart, a large offset under a small spread. Every honest answer must
// add up; a parent moved by a figure the rounding cannot explain must not.
import { decimalFromText, type SnapshotDecimal } from '@ex-grid/data';
import { describe, expect, it } from 'vitest';
import { decimalOfSum, decimalToNumber, scaledTo } from '../src/aggregates/exact-decimal';
import { allParts, PartColumns } from '../src/aggregates/parts';
import type { PivotNumber } from '../src/cube/answer';
import { levelMismatch, type LevelParts } from '../src/levels/level-check';
import { defined, elementAt, unreachable } from '../src/must';

/** A reproducible stream of numbers in [0, 1). */
function random(seed: number): () => number {
    let state = seed >>> 0;
    return () => {
        state = (state + 0x6d2b79f5) >>> 0;
        let t = state;
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

// A record's value: a number in Values, exact or a double, or text, which is counted and never summed.
type Value = number | SnapshotDecimal | 'text';

function isDecimal(value: Value): value is SnapshotDecimal {
    return typeof value === 'object';
}

function asNumber(value: number | SnapshotDecimal): number {
    return isDecimal(value) ? decimalToNumber(value) : value;
}

function shuffled<T>(items: readonly T[], next: () => number): T[] {
    return items.map(item => ({ item, key: next() })).toSorted((a, b) => a.key - b.key).map(entry => entry.item);
}

// The engine's own parts of a group, in the order given.
function byEngine(values: readonly Value[]): LevelParts {
    const columns = new PartColumns(allParts);
    const cell = columns.addCell();
    let absolute = 0;
    for (const value of values) {
        if (value === 'text') columns.addOther(cell);
        else if (isDecimal(value)) columns.addExact(cell, value);
        else columns.addDouble(cell, value);
        if (value !== 'text') absolute += Math.abs(asNumber(value));
    }
    columns.finish();
    const counts = elementAt(columns.counts, cell);
    if (counts.numbers === 0 || counts.nonFinite) return { ...counts };
    const sum = elementAt(defined(columns.sums, 'the sums'), cell);
    const extremes = elementAt(defined(columns.extremes, 'the extremes'), cell);
    const variance = elementAt(defined(columns.variances, 'the variances'), cell);
    const sumNumber: PivotNumber = sum.inexact ? { kind: 'double', value: sum.double } : { kind: 'exact', value: sum.exact };
    return {
        ...counts,
        sum: sumNumber,
        ...(sum.inexact ? { absoluteSum: absolute } : {}),
        min: extremes.inexact ? { kind: 'double', value: extremes.min } : { kind: 'exact', value: extremes.exactMin },
        max: extremes.inexact ? { kind: 'double', value: extremes.max } : { kind: 'exact', value: extremes.exactMax },
        product: elementAt(defined(columns.products, 'the products'), cell),
        mean: variance.mean,
        m2: variance.m2,
    };
}

// A SQL server's parts of a group: SUM, MIN, MAX, a plain product, AVG and n·VAR_POP from Σx and Σx².
function bySql(values: readonly Value[]): LevelParts {
    const numbers = values.filter((v): v is number | SnapshotDecimal => v !== 'text');
    const counts = { values: values.length, numbers: numbers.length, nonFinite: false };
    if (numbers.length === 0) return counts;
    const exact = numbers.every(isDecimal);
    let sum = 0;
    let squares = 0;
    let absolute = 0;
    let product = 1;
    for (const value of numbers) {
        const x = asNumber(value);
        sum += x;
        squares += x * x;
        absolute += Math.abs(x);
        product *= x;
    }
    let exactSum: SnapshotDecimal | undefined;
    if (exact) {
        const scale = Math.max(...numbers.map(v => (isDecimal(v) ? v.scale : 0)));
        let total = 0n;
        for (const value of numbers) if (isDecimal(value)) total += scaledTo(value, scale);
        exactSum = decimalOfSum(total, scale);
    }
    const sorted = numbers.toSorted((a, b) => asNumber(a) - asNumber(b));
    const extreme = (value: number | SnapshotDecimal): PivotNumber => (exact && isDecimal(value) ? { kind: 'exact', value } : { kind: 'double', value: asNumber(value) });
    const n = numbers.length;
    return {
        ...counts,
        sum: exactSum === undefined ? { kind: 'double', value: sum } : { kind: 'exact', value: exactSum },
        ...(exactSum === undefined ? { absoluteSum: absolute } : {}),
        min: extreme(elementAt(sorted, 0)),
        max: extreme(elementAt(sorted, n - 1)),
        product,
        mean: sum / n,
        m2: squares - sum * sum / n,
    };
}

type Kind = 'exact' | 'uniform' | 'offset' | 'magnitudes' | 'mixed' | 'nearOne';

function valueOf(kind: Kind, next: () => number): Value {
    if (next() < 0.03) return 'text';
    switch (kind) {
        case 'exact': return decimalFromText(`${Math.floor(next() * 2e6) - 1e6}.${Math.floor(next() * 100)}`);
        case 'uniform': return (next() - 0.5) * 2e6;
        case 'offset': return 1e9 + (next() - 0.5);
        case 'magnitudes': return (next() < 0.5 ? -1 : 1) * 10 ** (next() * 18 - 6);
        case 'mixed': return next() < 0.5 ? decimalFromText(`${Math.floor(next() * 1e4)}.25`) : next() * 1e4;
        case 'nearOne': return 0.5 + next() * 1.5;
        default: return unreachable(kind);
    }
}

// A parent and its children, each made by the engine or by SQL in an order of its own.
function honest(kind: Kind, seed: number, records: number): { parent: LevelParts; children: LevelParts[] } {
    const next = random(seed);
    const values = Array.from({ length: records }, () => valueOf(kind, next));
    const groups = 1 + Math.floor(next() * 20);
    const children: Value[][] = Array.from({ length: groups }, () => []);
    for (const value of values) elementAt(children, Math.floor(next() * groups)).push(value);
    const make = (group: readonly Value[]): LevelParts => (next() < 0.5 ? byEngine(shuffled(group, next)) : bySql(shuffled(group, next)));
    return { parent: make(values), children: children.filter(c => c.length > 0).map(make) };
}

const kinds: readonly Kind[] = ['exact', 'uniform', 'offset', 'magnitudes', 'mixed', 'nearOne'];

describe('ADR-0370: Level Aggregates are checked against their parent', () => {
    // Nine hundred answers of up to three thousand numbers: about 2.5 s on a laptop and past Vitest's default 5 s on
    // a busy CI runner. Its time is not what it checks (performance never gates).
    it('ADR-0370: an honest answer adds up, whatever order and whichever way each group was summed', { timeout: 60_000 }, () => {
        for (const kind of kinds) {
            for (let seed = 1; seed <= 150; seed++) {
                const { parent, children } = honest(kind, seed * 7919, 1 + (seed * 37) % 3000);
                expect(levelMismatch(parent, children), `${kind}, seed ${seed}`).toBeUndefined();
            }
        }
    });

    it('ADR-0370: a hundred thousand numbers twenty orders apart still add up', () => {
        for (const kind of ['magnitudes', 'offset', 'uniform'] as const) {
            const { parent, children } = honest(kind, 42, 100_000);
            expect(levelMismatch(parent, children), kind).toBeUndefined();
        }
    });

    it('ADR-0370: a count, an exact sum or an extreme a record off does not add up', () => {
        const { parent, children } = honest('exact', 5, 500);
        expect(levelMismatch({ ...parent, values: parent.values + 1 }, children)).toBe('values');
        const sum = parent.sum?.kind === 'exact' ? parent.sum.value : undefined;
        expect(sum).toBeDefined();
        if (sum === undefined) return;
        const cent = decimalFromText('0.01');
        expect(levelMismatch({ ...parent, sum: { kind: 'exact', value: { mantissa: scaledTo(sum, 2) + cent.mantissa, scale: 2 } } }, children)).toBe('sum');
        expect(levelMismatch({ ...parent, min: { kind: 'exact', value: decimalFromText('-99999999') } }, children)).toBe('min');
        expect(levelMismatch({ ...parent, max: { kind: 'double', value: 0 } }, children)).toBe('max');
    });

    it('ADR-0370: a double part moved by more than its rounding can explain does not add up', () => {
        for (const kind of ['uniform', 'offset', 'magnitudes'] as const) {
            const { parent, children } = honest(kind, 11, 2000);
            const sum = parent.sum?.kind === 'double' ? parent.sum.value : undefined;
            const absolute = parent.absoluteSum;
            const mean = parent.mean;
            const m2 = parent.m2;
            const product = parent.product;
            if (sum === undefined || absolute === undefined || mean === undefined || m2 === undefined || product === undefined) {
                throw new Error(`The ${kind} parent carries every double part.`);
            }
            const squares = m2 + parent.numbers * mean * mean;
            expect(levelMismatch({ ...parent, sum: { kind: 'double', value: sum - absolute * 1e-9 } }, children), kind).toBe('sum');
            expect(levelMismatch({ ...parent, absoluteSum: absolute * 0.5 }, children), kind).toBe('absoluteSum');
            expect(levelMismatch({ ...parent, mean: mean + Math.sqrt(squares) * 1e-9 }, children), kind).toBe('mean');
            expect(levelMismatch({ ...parent, m2: m2 + squares * 1e-8 }, children), kind).toBe('m2');
            if (product !== 0 && Number.isFinite(product)) {
                expect(levelMismatch({ ...parent, product: product * (1 + 1e-9) }, children), kind).toBe('product');
            }
        }
    });

    it('ADR-0370: a part the parent carries and a child does not, or a sum exact above a double, does not add up', () => {
        const { parent, children } = honest('uniform', 3, 300);
        const [first, ...rest] = children;
        if (first === undefined) throw new Error('The answer has a child.');
        const { product: _dropped, ...withoutProduct } = first;
        expect(levelMismatch(parent, [withoutProduct, ...rest])).toBe('product');
        expect(levelMismatch({ ...parent, sum: { kind: 'exact', value: decimalFromText('1') } }, children)).toBe('sum');
        const { absoluteSum: _absent, ...withoutAbsolute } = parent;
        expect(levelMismatch(withoutAbsolute, children)).toBe('absoluteSum');
    });

    it('ADR-0370: a non-finite number is #NUM! in the parent and its children alike, and only the counts are compared', () => {
        const child: LevelParts = { values: 2, numbers: 2, nonFinite: true };
        const other: LevelParts = { values: 1, numbers: 1, nonFinite: false, sum: { kind: 'double', value: 3 }, absoluteSum: 3 };
        expect(levelMismatch({ values: 3, numbers: 3, nonFinite: true }, [child, other])).toBeUndefined();
        expect(levelMismatch({ values: 3, numbers: 3, nonFinite: false, sum: { kind: 'double', value: 3 }, absoluteSum: 3 }, [child, other])).toBe('nonFinite');
    });
});
