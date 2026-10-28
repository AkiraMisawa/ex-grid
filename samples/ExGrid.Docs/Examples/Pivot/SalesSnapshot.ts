import { createSnapshot, type Snapshot } from '@ex-grid/data';
import { datePartField, pivotField, type PivotField } from '@ex-grid/pivot-engine';
import type { Sale } from './SalesData';

/** The sales as a Snapshot's columns, one a field of the sale; Id is the Record Key a Change Batch finds a sale by. */
export function salesSnapshot(sales: readonly Sale[]): Snapshot {
    return createSnapshot({
        recordKey: 'Id',
        columns: [
            { name: 'Id', kind: 'integer', values: sales.map(s => s.id) },
            { name: 'Region', kind: 'text', values: sales.map(s => s.region) },
            { name: 'Country', kind: 'text', values: sales.map(s => s.country) },
            { name: 'Category', kind: 'text', values: sales.map(s => s.category) },
            { name: 'Product', kind: 'text', values: sales.map(s => s.product) },
            { name: 'Channel', kind: 'text', values: sales.map(s => s.channel) },
            { name: 'Date', kind: 'date', values: sales.map(s => s.date) },
            { name: 'Units', kind: 'integer', values: sales.map(s => s.units) },
            { name: 'Revenue', kind: 'decimal', values: sales.map(s => s.revenue) },
            { name: 'Cost', kind: 'decimal', values: sales.map(s => s.cost) },
        ],
    });
}

// The fields over those columns: each column once, and Year and Quarter as parts of the Date column.
export const salesSnapshotFields: readonly PivotField[] = [
    pivotField({ name: 'Region', type: 'text' }),
    pivotField({ name: 'Country', type: 'text' }),
    pivotField({ name: 'Category', type: 'text' }),
    pivotField({ name: 'Product', type: 'text' }),
    pivotField({ name: 'Channel', type: 'text', itemOrder: ['Online', 'Retail', 'Partner', 'Direct'] }),
    pivotField({ name: 'Date', type: 'date', format: 'yyyy-MM-dd' }),
    datePartField('Year', 'Date', 'year'),
    datePartField('Quarter', 'Date', 'quarter'),
    pivotField({ name: 'Units', type: 'number' }),
    pivotField({ name: 'Revenue', type: 'number', format: '#,##0.00' }),
    pivotField({ name: 'Cost', type: 'number', format: '#,##0.00' }),
];

/** `amount` scaled by `to / from` and rounded to the cent, half away from zero, exactly. */
function scaled(amount: bigint, to: number, from: number): bigint {
    const twice = (amount * BigInt(to) * 2n) / BigInt(from);
    return (twice + (twice < 0n ? -1n : 1n)) / 2n;
}

/** Amends `count` of `sales` in place, as an order system would (units changed, revenue and cost with them), and
 * returns the amended ones. */
export function amendSales(sales: Sale[], random: () => number, count: number): readonly Sale[] {
    const amended = new Map<number, Sale>();
    while (amended.size < count) {
        const at = Math.floor(random() * sales.length);
        const sale = sales[at];
        if (sale === undefined) throw new RangeError(`No sale ${at} in a list of ${sales.length}.`);
        const units = Math.max(1, sale.units + Math.floor(random() * 6) - 2);
        const next: Sale = {
            ...sale,
            units,
            revenue: { mantissa: scaled(sale.revenue.mantissa, units, sale.units), scale: sale.revenue.scale },
            cost: { mantissa: scaled(sale.cost.mantissa, units, sale.units), scale: sale.cost.scale },
        };
        sales[at] = next;
        amended.set(at, next);
    }
    return [...amended.values()];
}
