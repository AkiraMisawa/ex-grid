// An Order Key a source answers for the Items it names (ADR-0412): the value each Item hands an Order Key, which a
// source that answers keys files them under, and a key that refuses in its own words, naming its field, refused in
// them. Behaviour of the Script Core alone (ADR-0350 section 5's note of 2026-10-27): the C# engine calls no Order
// Key but a .NET function's.
import { describe, expect, it } from 'vitest';
import {
    PivotOrderKeyError, blankItemKey, booleanItemKey, computePivot, createPivotLayout, dateItemKey, errorItemKey, numberItemKey, pivotItemValue,
    pivotRecordField, textItemKey, type PivotOrderKey,
} from '../src/index';

interface Trade {
    readonly region: string;
    readonly amount: number;
}

const trades: readonly Trade[] = [{ region: 'East', amount: 3 }, { region: 'West', amount: 4 }];

function byRegion(orderKey: PivotOrderKey): void {
    const fields = [
        pivotRecordField<Trade>({ name: 'Region', type: 'text', caption: 'Desk region', value: t => t.region, orderKey }),
        pivotRecordField<Trade>({ name: 'Amount', type: 'number', value: t => t.amount }),
    ];
    computePivot(trades, fields, createPivotLayout({ rows: ['Region'], values: [{ field: 'Amount', aggregation: 'sum' }] }));
}

describe('an Order Key a source answers (ADR-0412)', () => {
    it('ADR-0412: each Item hands an Order Key the value the engine hands it', () => {
        expect(pivotItemValue(textItemKey('East'))).toBe('East');
        expect(pivotItemValue(numberItemKey(1.5))).toBe(1.5);
        expect(pivotItemValue(dateItemKey(638_000_000_000_000_000n))).toBe(638_000_000_000_000_000n);
        expect(pivotItemValue(booleanItemKey(true))).toBe(true);
        expect(pivotItemValue(blankItemKey)).toBeUndefined();
        expect(pivotItemValue(errorItemKey)).toBeUndefined();
    });

    it('ADR-0412: a key refused in its own words, naming its field, is refused in them', () => {
        const refusal = new PivotOrderKeyError('Region', 'The Order Key of Desk region gave \'East\' a key of type Version.');
        expect(() => { byRegion(() => { throw refusal; }); }).toThrow(refusal);
    });

    it('ADR-0060: a key that fails otherwise is refused naming its field and its Item, its failure the cause', () => {
        const failure = new Error('no rank for East');
        let thrown: unknown;
        try {
            byRegion(value => { if (value === 'East') throw failure; return 1; });
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal is what the test reads
            thrown = error;
        }
        expect(thrown).toBeInstanceOf(PivotOrderKeyError);
        expect(thrown).toMatchObject({ field: 'Region', message: 'The Order Key of Desk region failed on \'East\'.', cause: failure });
    });
});
