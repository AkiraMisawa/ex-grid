import { Temporal } from 'temporal-polyfill';
import type { SnapshotDecimal } from '@ex-grid/data';
import { pivotRecordField, type PivotRecordField } from '@ex-grid/pivot-engine';

/** One invented sale: a line of an order, as the ExPivot Examples pivot it. */
export interface Sale {
    readonly id: number;
    readonly region: string;
    readonly country: string;
    readonly category: string;
    readonly product: string;
    readonly channel: string;
    readonly date: Temporal.PlainDate;
    readonly units: number;
    /** Money is an exact decimal, never a double: a total of thousands of them is summed exactly. */
    readonly revenue: SnapshotDecimal;
    readonly cost: SnapshotDecimal;
}

/** An amount of whole cents, as the exact decimal the pivot sums. */
const cents = (amount: number): SnapshotDecimal => ({ mantissa: BigInt(amount), scale: 2 });

/** What a sale earned over its cost, exactly. */
export function profitOf(sale: Sale): SnapshotDecimal {
    return { mantissa: sale.revenue.mantissa - sale.cost.mantissa, scale: 2 };
}

const channels = ['Online', 'Retail', 'Partner', 'Direct'] as const;

// Each field once, with its accessor. Year, Quarter and Month are parts of the date: the source reads the date, and
// the pivot groups it.
export const salesFields: readonly PivotRecordField<Sale>[] = [
    pivotRecordField<Sale>({ name: 'Region', type: 'text', value: s => s.region }),
    pivotRecordField<Sale>({ name: 'Country', type: 'text', value: s => s.country }),
    pivotRecordField<Sale>({ name: 'Category', type: 'text', value: s => s.category }),
    pivotRecordField<Sale>({ name: 'Product', type: 'text', value: s => s.product }),
    pivotRecordField<Sale>({ name: 'Channel', type: 'text', value: s => s.channel, itemOrder: channels }),
    pivotRecordField<Sale>({ name: 'Date', type: 'date', format: 'yyyy-MM-dd', value: s => s.date }),
    pivotRecordField<Sale>({ name: 'Year', type: 'date', datePart: 'year', value: s => s.date }),
    pivotRecordField<Sale>({ name: 'Quarter', type: 'date', datePart: 'quarter', value: s => s.date }),
    pivotRecordField<Sale>({ name: 'Month', type: 'date', datePart: 'month', value: s => s.date }),
    pivotRecordField<Sale>({ name: 'Units', type: 'number', value: s => s.units }),
    pivotRecordField<Sale>({ name: 'Revenue', type: 'number', format: '#,##0.00', value: s => s.revenue }),
    pivotRecordField<Sale>({ name: 'Cost', type: 'number', format: '#,##0.00', value: s => s.cost }),
    pivotRecordField<Sale>({ name: 'Profit', type: 'number', format: '#,##0.00', value: profitOf }),
];

const regions: readonly (readonly [string, readonly string[]])[] = [
    ['North America', ['United States', 'Canada', 'Mexico']],
    ['Europe', ['Germany', 'United Kingdom', 'France', 'Italy']],
    ['Asia Pacific', ['Japan', 'Australia', 'South Korea', 'Singapore']],
    ['Latin America', ['Brazil', 'Chile', 'Colombia']],
];
const catalog: readonly (readonly [string, readonly (readonly [string, number])[]])[] = [
    ['Computers', [['Laptop Pro 14', 1899], ['Laptop Air 13', 1199], ['Desktop Tower', 1499]]],
    ['Displays', [['27" 4K Monitor', 429], ['34" Ultrawide', 749]]],
    ['Accessories', [['Wireless Mouse', 49], ['Mechanical Keyboard', 129], ['USB-C Dock', 199]]],
    ['Audio', [['Noise-Cancelling Headphones', 299], ['Wireless Earbuds', 179]]],
];
const first = Temporal.PlainDate.from('2025-01-01');

/** The entry at `index` of a list known to hold it. */
function at<T>(list: readonly T[], index: number): T {
    const found = list[index];
    if (found === undefined) throw new RangeError(`No entry ${index} in a list of ${list.length}.`);
    return found;
}

/** `count` invented sales from January 2025 to September 2026, the same on every call: they come from a fixed seed. */
export function sampleSales(count = 5000): readonly Sale[] {
    let seed = 20261003;
    // Mulberry32, a small seeded generator: the same draws in every browser.
    const next = (below: number): number => {
        seed = (seed + 0x6D2B79F5) | 0;
        let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
        t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
        return Math.floor((((t ^ (t >>> 14)) >>> 0) / 4294967296) * below);
    };
    return Array.from({ length: count }, (_, i): Sale => {
        const [region, countries] = at(regions, next(regions.length));
        const [category, products] = at(catalog, next(catalog.length));
        const [product, price] = at(products, next(products.length));
        const units = 1 + next(12);
        return {
            id: 100_001 + i,
            region,
            country: at(countries, next(countries.length)),
            category,
            product,
            channel: at(channels, next(channels.length)),
            date: first.add({ days: next(638) }),
            units,
            // In whole cents: a discount of up to a tenth on the revenue, and a cost of 52% to 67% of the list price.
            revenue: cents(units * price * (90 + next(11))),
            cost: cents(units * price * (52 + next(16))),
        };
    });
}
