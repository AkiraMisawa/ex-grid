// The Item a Snapshot's value belongs to (ADR-0060), for a Consumer that reads a Snapshot's rows beside the
// engine, such as ExBoard telling which rows stand under a pivot's selected cells (ADR-0340, Q5).

import type { SnapshotColumn } from '@ex-grid/data';
import { decimalToNumber } from '../aggregates/exact-decimal';
import { unreachable } from '../must';
import { ItemKeys, toPublicKey, type ItemKey, type PivotItemKey } from './item-key';

/** The Item the value of `column` at `row` belongs to, as the engine groups a field over that column: a number
 * by its double, a Decimal by the double C# converts it to, a date by its clock value, and a Blank as Blank. */
export function pivotItemKeyAt(column: SnapshotColumn, row: number): PivotItemKey {
    return toPublicKey(itemKeyOf(column, row));
}

function itemKeyOf(column: SnapshotColumn, row: number): ItemKey {
    switch (column.kind) {
        case 'text': { const value = column.valueAt(row); return value === null ? ItemKeys.blank : ItemKeys.text(value); }
        case 'decimal': { const value = column.valueAt(row); return value === null ? ItemKeys.blank : ItemKeys.number(decimalToNumber(value)); }
        case 'integer': { const value = column.valueAt(row); return value === null ? ItemKeys.blank : ItemKeys.number(Number(value)); }
        case 'double': { const value = column.valueAt(row); return value === null ? ItemKeys.blank : ItemKeys.number(value); }
        case 'date': { const value = column.valueAt(row); return value === null ? ItemKeys.blank : ItemKeys.date(value); }
        case 'boolean': { const value = column.valueAt(row); return value === null ? ItemKeys.blank : ItemKeys.boolean(value); }
        default: return unreachable(column);
    }
}
