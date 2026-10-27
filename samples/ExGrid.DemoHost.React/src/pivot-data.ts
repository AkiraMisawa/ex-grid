// DemoPivotData.cs under the Script Core: the same 2,000 invented trades from the same seed, the same fields
// and the same first layout, so the /pivot page here pivots the numbers the Blazor hosts pivot. The amounts
// are exact decimals made as C#'s are: a double turned into a decimal of fifteen significant digits, and
// Math.Round's banker's rounding to two places (ADR-0060).
import type { SnapshotDecimal } from '@ex-grid/data';
import { createPivotLayout, pivotRecordField, type PivotLayout, type PivotRecordField } from '@ex-grid/pivot-engine';
import { Temporal } from 'temporal-polyfill';
import { pick } from './demo-data';
import { DotNetRandom } from './dotnet-random';

/** A booked trade, as DemoPivotTrade. */
export interface DemoPivotTrade {
    readonly id: string;
    readonly region: string;
    readonly desk: string;
    readonly book: string;
    readonly product: string;
    readonly currency: string;
    readonly tradeDate: Temporal.PlainDate;
    readonly notional: SnapshotDecimal;
    readonly pnl: SnapshotDecimal;
    readonly quantity: number;
    readonly confirmed: boolean;
}

const regions: readonly (readonly [string, readonly string[]])[] = [
    ['Americas', ['NY-RATES-01', 'NY-CREDIT-02', 'TOR-FX-01']],
    ['EMEA', ['LDN-RATES-01', 'LDN-FX-02', 'FRA-CREDIT-01', 'ZRH-EQ-01']],
    ['APAC', ['TKY-RATES-01', 'HKG-EQ-01', 'SGP-FX-01']],
];
const products = ['Swap', 'Bond', 'Option', 'Future', 'Spot'] as const;
const currencies = ['USD', 'EUR', 'JPY', 'GBP', 'CHF'] as const;
const start = Temporal.PlainDate.from('2026-01-02');

/** The trades the page opens with. */
export const demoPivotTradeCount = 2000;

/** C#'s `(decimal)double`: the double's fifteen significant digits, as an exact decimal. */
export function decimalOfDouble(value: number): SnapshotDecimal {
    const [digits, exponentText] = value.toExponential(14).split('e');
    if (digits === undefined || exponentText === undefined) throw new Error(`Internal: ${value} has no exponential text.`);
    const negative = digits.startsWith('-');
    const mantissa = BigInt(digits.replace('-', '').replace('.', ''));
    const scale = 14 - Number(exponentText);
    const signed = negative ? -mantissa : mantissa;
    return scale >= 0 ? { mantissa: signed, scale } : { mantissa: signed * 10n ** BigInt(-scale), scale: 0 };
}

/** C#'s `Math.Round(decimal, 2)`: to two places, a half to the even digit. */
export function roundToCents(value: SnapshotDecimal): SnapshotDecimal {
    if (value.scale <= 2) return value;
    const divisor = 10n ** BigInt(value.scale - 2);
    const magnitude = value.mantissa < 0n ? -value.mantissa : value.mantissa;
    let whole = magnitude / divisor;
    const rest = magnitude % divisor;
    if (rest * 2n > divisor || (rest * 2n === divisor && whole % 2n === 1n)) whole += 1n;
    return { mantissa: value.mantissa < 0n ? -whole : whole, scale: 2 };
}

/** `count` trades from DemoPivotData's seed: the first 2,000 are the same whatever the count. */
export function demoPivotTrades(count = demoPivotTradeCount): readonly DemoPivotTrade[] {
    const random = new DotNetRandom(20260930);
    return Array.from({ length: count }, (_, i): DemoPivotTrade => {
        const [region, books] = pick(regions, random.next(regions.length));
        const book = pick(books, random.next(books.length));
        const desk = book.includes('RATES') ? 'Rates' : book.includes('CREDIT') ? 'Credit' : book.includes('FX') ? 'FX' : 'Equities';
        const notional = BigInt(random.next(1, 500) * 10_000);
        const fraction = decimalOfDouble(random.nextDouble() - 0.45);
        // × notional / 100m: the digits times the whole notional, two places further right.
        const pnl = roundToCents({ mantissa: fraction.mantissa * notional, scale: fraction.scale + 2 });
        return Object.freeze({
            id: `T${100000 + i}`,
            region,
            desk,
            book,
            product: pick(products, random.next(products.length)),
            currency: pick(currencies, random.next(currencies.length)),
            tradeDate: start.add({ days: random.next(0, 270) }),
            notional: { mantissa: notional, scale: 0 },
            pnl,
            quantity: random.next(1, 50),
            confirmed: random.next(10) > 0,
        });
    });
}

/** DemoPivotData.Fields: each field once, Month as the month of the trade date. */
export const demoPivotFields: readonly PivotRecordField<DemoPivotTrade>[] = Object.freeze([
    pivotRecordField<DemoPivotTrade>({ name: 'Region', type: 'text', value: t => t.region }),
    pivotRecordField<DemoPivotTrade>({ name: 'Desk', type: 'text', value: t => t.desk }),
    pivotRecordField<DemoPivotTrade>({ name: 'Book', type: 'text', value: t => t.book }),
    pivotRecordField<DemoPivotTrade>({ name: 'Product', type: 'text', value: t => t.product }),
    pivotRecordField<DemoPivotTrade>({ name: 'Currency', type: 'text', value: t => t.currency }),
    pivotRecordField<DemoPivotTrade>({ name: 'TradeDate', type: 'date', caption: 'Trade date', format: 'yyyy-MM-dd', value: t => t.tradeDate }),
    pivotRecordField<DemoPivotTrade>({ name: 'Month', type: 'date', datePart: 'month', value: t => t.tradeDate }),
    pivotRecordField<DemoPivotTrade>({ name: 'Notional', type: 'number', format: '#,##0.00', value: t => t.notional }),
    pivotRecordField<DemoPivotTrade>({ name: 'Pnl', type: 'number', caption: 'P&L', format: '#,##0.00', value: t => t.pnl }),
    pivotRecordField<DemoPivotTrade>({ name: 'Quantity', type: 'number', value: t => t.quantity }),
    pivotRecordField<DemoPivotTrade>({ name: 'Confirmed', type: 'boolean', value: t => t.confirmed }),
]);

/** DemoPivotData.FirstLayout: P&L by region and desk, across products, filtered by currency. */
export function demoPivotFirstLayout(): PivotLayout {
    return createPivotLayout({
        filters: ['Currency'],
        rows: ['Region', 'Desk'],
        columns: ['Product'],
        values: [{ field: 'Pnl', aggregation: 'sum', numberFormat: '#,##0' }],
    });
}
