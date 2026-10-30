// A value cell as the report shows it (ADR-0060): an error value's text, General as Excel's PivotTable shows
// it — at most eleven characters — or the Value Field's number format, a Show Values As percentage in
// `0.00%` when it has none. An exact value is written from its own digits, never from a double. The C#
// engine's PivotValue.

import type { SnapshotDecimal } from '@ex-grid/data';
import type { AggregateValue } from '../aggregates/parts';
import type { Culture } from '../culture/culture-data';
import { generalOfDecimal, generalOfDouble } from '../text/general';
import { formatDecimal, formatDouble } from '../text/number-text';

/** A value cell of a report: its text, its number, and its exact decimal where it was computed in one. */
export interface PivotValue {
    /** The text the cell shows. */
    readonly text: string;
    /** The number, as a double: the exact one converted where there is one; 0 for an error. */
    readonly number: number;
    /** The exact decimal, where the Aggregation was computed in one. */
    readonly exact?: SnapshotDecimal;
    /** The error value's text, `#DIV/0!` or `#NUM!`, when the cell is one. */
    readonly error?: string;
}

/** The shown value of an Aggregation's answer, which is not empty. */
export function pivotValueOf(value: Exclude<AggregateValue, { readonly kind: 'empty' }>, numberFormat: string | undefined,
    percent: boolean, culture: Culture): PivotValue {
    if (value.kind === 'error') return Object.freeze({ text: value.error, number: 0, error: value.error });
    const numbers = culture.data.number;
    let text: string;
    if (numberFormat === undefined && !percent) {
        text = value.exact === undefined ? generalOfDouble(value.number, numbers) : generalOfDecimal(value.exact, numbers);
    } else {
        const format = numberFormat ?? '0.00%'; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0060: a percentage with no format of its own is shown in 0.00%
        text = value.exact === undefined ? formatDouble(value.number, format, numbers) : formatDecimal(value.exact, format, numbers);
    }
    return Object.freeze(value.exact === undefined ? { text, number: value.number } : { text, number: value.number, exact: value.exact });
}
