import type { SnapshotDecimal } from '@ex-grid/data';
import { pivotRecordField, type PivotRecordField } from '@ex-grid/pivot-engine';
import { mulberry32 } from '../Grid/Seeded';

/** One invented holding at a custodian: a market value in the tens of billions, to the cent. */
export interface Holding {
    readonly id: number;
    readonly custodian: string;
    readonly assetClass: string;
    /** In whole cents, the exact decimal the pivot sums. */
    readonly marketValue: SnapshotDecimal;
}

// The same market value declared twice: exactly, and as a double, which is binary floating point.
export const holdingFields: readonly PivotRecordField<Holding>[] = [
    pivotRecordField<Holding>({ name: 'Custodian', type: 'text', value: h => h.custodian }),
    pivotRecordField<Holding>({ name: 'AssetClass', type: 'text', caption: 'Asset class', value: h => h.assetClass }),
    pivotRecordField<Holding>({ name: 'MarketValue', type: 'number', caption: 'Market value', value: h => h.marketValue }),
    pivotRecordField<Holding>({
        name: 'MarketValueDouble', type: 'number', caption: 'Market value (double)', value: h => Number(h.marketValue.mantissa) / 100,
    }),
];

const custodians = ['Harbor Trust', 'Northgate Custody', 'Pacific Clearing'] as const;
const assetClasses = ['Government bonds', 'Corporate bonds', 'Equities', 'Cash'] as const;

/** The entry at `index` of a list known to hold it. */
function at<T>(list: readonly T[], index: number): T {
    const found = list[index];
    if (found === undefined) throw new RangeError(`No entry ${index} in a list of ${list.length}.`);
    return found;
}

/** 5,000 holdings, the same on every call: they come from a fixed seed. */
export function sampleHoldings(): readonly Holding[] {
    const random = mulberry32(20261003);
    const below = (bound: number): number => Math.floor(random() * bound);
    return Array.from({ length: 5_000 }, (_, i): Holding => ({
        id: i + 1,
        custodian: at(custodians, below(custodians.length)),
        assetClass: at(assetClasses, below(assetClasses.length)),
        // Ten to ninety billion, to the cent.
        marketValue: { mantissa: BigInt(10_000 + below(80_000)) * 100_000_000n + BigInt(below(100_000_000)), scale: 2 },
    }));
}
