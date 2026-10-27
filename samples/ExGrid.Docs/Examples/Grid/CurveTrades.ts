import type { Breach } from '@ex-grid/core';

/** A trade discounted on its CSA's curve, for the rule over the whole collection (ADR-0381). */
export interface CurveTrade {
    readonly id: string;
    readonly csa: string;
    readonly counterparty: string;
    readonly curve: string;
}

const csas = ['ABC', 'DEF', 'GHI', 'JKL', 'MNO'];
const counterparties = ['Northwind Bank', 'Contoso Capital', 'Fabrikam Securities', 'Tailspin Partners', 'Adatum Trust'];
const curves = ['USD-SOFR', 'EUR-ESTR', 'GBP-SONIA', 'JPY-TONA', 'USD-SOFR'];

/** Sixty trades over five CSAs. CSA ABC's trades stand far apart, so a breach reaches rows outside the Window. */
export function sampleCurveTrades(): CurveTrade[] {
    return Array.from({ length: 60 }, (_, i) => ({
        id: `T-${String(i + 1).padStart(4, '0')}`, csa: at(csas, i), counterparty: at(counterparties, i), curve: at(curves, i),
    }));
}

function at(values: readonly string[], i: number): string {
    const value = values[i % values.length];
    if (value === undefined) throw new RangeError('No values to cycle through.');
    return value;
}

// The items that share each key, in the order each key first appears.
function groupBy<T>(items: readonly T[], key: (item: T) => string): [string, T[]][] {
    const groups = new Map<string, T[]>();
    for (const item of items) {
        const group = groups.get(key(item));
        if (group === undefined) groups.set(key(item), [item]);
        else group.push(item);
    }
    return [...groups];
}

/** One discount curve per CSA: every trade of a CSA with two or more curves breaches, and the message names the
 * other side by trade id, never by row position, which a sort changes. */
export function* oneCurvePerCsa(trades: readonly CurveTrade[]): Iterable<Breach> {
    for (const [csa, held] of groupBy(trades, t => t.csa)) {
        const byCurve = groupBy(held, t => t.curve).toSorted(([, a], [, b]) => b.length - a.length);
        if (byCurve.length < 2) continue;
        const told = byCurve.map(([curve, on]) => `${curve} on ${on.length > 3 ? `${String(on.length)} trades` : on.map(t => t.id).join(' ')}`);
        yield {
            rule: 'one-curve-per-csa',
            message: `CSA ${csa} has ${String(byCurve.length)} discount curves: ${told.join(', ')}.`,
            cells: held.map(t => ({ rowKey: t.id, column: 'Curve' })),
        };
    }
}
