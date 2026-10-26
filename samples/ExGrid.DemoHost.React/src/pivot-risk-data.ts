// DemoRiskData.cs under the Script Core: the same rate-delta positions from the same seed, the same fields and
// the same layout, so /pivot-risk here pivots the numbers the Blazor hosts pivot (ADR-0060).
import { createPivotLayout, pivotRecordField, type PivotLayout, type PivotRecordField } from '@ex-grid/pivot-engine';
import { DotNetRandom } from './dotnet-random';

/** One position, as DemoRiskPosition: a book's PV01 on one curve at one tenor, in whole US dollars. */
export interface DemoRiskPosition {
    readonly id: string;
    readonly desk: string;
    readonly curve: string;
    readonly book: string;
    readonly tenor: string;
    readonly pv01: number;
}

/** Every tenor the positions are bucketed at, in maturity order. The options desk writes 18M as 1Y6M. */
const tenorBuckets = ['ON', 'TN', '1W', '1M', '3M', '6M', '9M', '1Y', '18M', '2Y', '3Y', '5Y', '7Y', '10Y', '15Y', '20Y', '30Y'] as const;

const desks: readonly (readonly [string, readonly string[], readonly string[]])[] = [
    ['Rates Flow', ['USD SOFR', 'EUR ESTR', 'GBP SONIA'], ['RFL-NY-01', 'RFL-LDN-02']],
    ['Rates Options', ['USD SOFR', 'EUR EURIBOR 6M'], ['ROP-NY-01', 'ROP-LDN-01']],
    ['Treasury', ['USD SOFR', 'EUR ESTR', 'JPY TONA'], ['TSY-LDN-01', 'TSY-TKY-01']],
];

function itemOf<T>(list: readonly T[], index: number): T {
    const found = list[index];
    if (found === undefined) throw new Error(`Internal: index ${index} is outside a list of ${list.length}.`);
    return found;
}

/** The positions, from DemoRiskData's seed, drawn in the order C# draws them. */
function makePositions(): readonly DemoRiskPosition[] {
    const random = new DotNetRandom(20261001);
    const positions: DemoRiskPosition[] = [];
    for (const [desk, curves, books] of desks) {
        for (const curve of curves) {
            for (let t = 0; t < tenorBuckets.length; t++) {
                let tenor: string = itemOf(tenorBuckets, t);
                // Only the treasury funds overnight. Any bucket but the overnight ones and eighteen months is left
                // empty now and then, and so is a book.
                if (t < 2 && desk !== 'Treasury') continue;
                if (tenor !== 'ON' && tenor !== 'TN' && tenor !== '18M' && random.next(6) === 0) continue;
                if (tenor === '18M' && desk === 'Rates Options') tenor = '1Y6M';
                const booked = books.filter(() => random.next(3) !== 0);
                if (booked.length === 0) booked.push(itemOf(books, random.next(books.length)));
                for (const book of booked) {
                    // Longer tenors carry more PV01 per notional: a rough scale, whole dollars.
                    const scale = 200 + (t * t * 60);
                    const pv01 = random.next(-scale, scale + 1) * 10;
                    positions.push(Object.freeze({ id: `P${1001 + positions.length}`, desk, curve, book, tenor, pv01 }));
                }
            }
        }
    }
    return Object.freeze(positions);
}

/** The page's positions. */
export const demoRiskPositions: readonly DemoRiskPosition[] = makePositions();

/** Months to maturity: "1Y6M" is 18, as "18M" is, and the two stand side by side. Not a tenor: no key, so after
 * the tenors. */
export function tenorMonths(tenor: string | number | bigint | boolean): number | undefined {
    if (typeof tenor !== 'string') return undefined;
    if (tenor === 'ON') return -2;
    if (tenor === 'TN') return -1;
    let months = 0;
    let number = 0;
    for (const c of tenor) {
        if (c >= '0' && c <= '9') {
            number = (number * 10) + Number(c);
            continue;
        }
        const unit = c === 'W' ? 0.25 : c === 'M' ? 1 : c === 'Y' ? 12 : 0;
        if (unit === 0 || number === 0) return undefined;
        months += number * unit;
        number = 0;
    }
    return number === 0 && months > 0 ? months : undefined;
}

function fieldsOf(orderKey: boolean): readonly PivotRecordField<DemoRiskPosition>[] {
    return Object.freeze([
        pivotRecordField<DemoRiskPosition>({ name: 'Desk', type: 'text', value: p => p.desk }),
        pivotRecordField<DemoRiskPosition>({ name: 'Curve', type: 'text', value: p => p.curve }),
        pivotRecordField<DemoRiskPosition>({ name: 'Book', type: 'text', value: p => p.book }),
        pivotRecordField<DemoRiskPosition>({ name: 'Tenor', type: 'text', value: p => p.tenor, ...(orderKey ? { orderKey: tenorMonths } : {}) }),
        pivotRecordField<DemoRiskPosition>({ name: 'Pv01', type: 'number', caption: 'PV01 (USD)', format: '#,##0', value: p => p.pv01 }),
    ]);
}

/** The fields, the tenor's carrying its Order Key: Items ordered by `tenorMonths`, called once per Item. */
export const demoRiskFields = fieldsOf(true);

/** The same fields with no Order Key on the tenor, whose Items then fall back to the order of their labels. */
export const demoRiskFieldsWithoutKey = fieldsOf(false);

/** Desks and curves in Rows, tenors in Columns. */
export function demoRiskLayout(): PivotLayout {
    return createPivotLayout({
        rows: ['Desk', 'Curve'],
        columns: ['Tenor'],
        values: [{ field: 'Pv01', aggregation: 'sum', numberFormat: '#,##0' }],
    });
}
