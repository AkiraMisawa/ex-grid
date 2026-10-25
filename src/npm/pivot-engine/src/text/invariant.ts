// The invariant culture's conventions, and the raw, locale-free text of a value: what a copy carries and a
// grid's raw form reads back (ADR-0060), the exact decimal's own digits or the double's round trip.

import type { SnapshotDecimal } from '@ex-grid/data';
import type { DateCulture, NumberCulture } from '../culture/culture-data';
import { cultureTable } from '../culture/culture-table';
import { defined } from '../must';
import { formatDecimal, formatDouble } from './number-text';

const invariant = defined(cultureTable['invariant'], 'the invariant culture');

/** The invariant culture's number conventions. */
export const invariantNumbers: NumberCulture = invariant.number;

/** The invariant culture's date conventions. */
export const invariantDates: DateCulture = invariant.date;

/** A decimal's invariant text, as C#'s `ToString(null, CultureInfo.InvariantCulture)` writes it. */
export function decimalRaw(value: SnapshotDecimal): string {
    return formatDecimal(value, undefined, invariantNumbers);
}

/** A double's round-trip text, as C#'s `ToString("R", CultureInfo.InvariantCulture)` writes it. */
export function doubleRaw(value: number): string {
    return formatDouble(value, 'R', invariantNumbers);
}
