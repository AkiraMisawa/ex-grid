// How a pasted field is written into a Sheet (ADR-0048, ADR-0050 item 10): an invariant number exactly, a run of
// `#` in shown text refused by name, and anything else typed under the Sheet's culture, or refused where an
// invariant date would read back as another value.

import type { PasteFieldOrigin } from '@ex-grid/core';
import { CellAddress, Entry, type Culture } from '@ex-grid/sheet-engine';

const isoDate = /^[0-9]{4}-[0-9]{2}-[0-9]{2}(T[0-9]{2}:[0-9]{2}(:[0-9]{2})?)?$/u;

/**
 * The number an invariant pasted field is: Excel's `x:num`, or ExGrid's and ExSheet's own unformatted HTML,
 * holding a finite number. It is pasted as that exact double, every digit it holds, not typed, which would cut
 * it to the fifteen significant digits a typed number keeps, and so reads the same under every culture.
 * Undefined for any other field, which {@link typedFor} types.
 */
export function exactNumberFor(field: string, origin: PasteFieldOrigin): number | undefined {
    if (origin !== 'invariant' || field.trim() === '') return undefined;
    const number = Number(field);
    return Number.isFinite(number) && /^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$/u.test(field.trim()) ? number : undefined;
}

/**
 * Whether a pasted field is a run of `#` in shown text: Excel showing a value too wide for its column, with the
 * value itself missing from the clipboard. Such a paste is refused by name, not taken as the text `########`
 * (ADR-0048). An invariant field holds the Value itself, so a run of `#` there is text.
 */
export function isTooNarrowToShow(field: string, origin: PasteFieldOrigin): boolean {
    return origin === 'shownText' && field.length > 0 && /^#+$/u.test(field);
}

/**
 * What a pasted field that is not an exact number is typed as under `culture`. Shown text is typed as it is. An
 * invariant field is its value as it is: an ISO date as the date; `TRUE` and `FALSE` as booleans; anything else
 * as text or an Error Value. Undefined when an invariant date would not read back under the culture as a date:
 * the paste is refused rather than write a different value.
 */
export function typedFor(field: string, origin: PasteFieldOrigin, culture: Culture): string | undefined {
    if (origin !== 'invariant') return field;
    const upper = field.toUpperCase();
    if (upper === 'TRUE' || upper === 'FALSE') return field;
    if (isoDate.test(field)) {
        // The engine reads a year-first date, and a time after a space, in every culture.
        const typed = field.replace('T', ' ');
        return Entry.parse(typed, culture)?.constant?.kind === 'number' ? typed : undefined;
    }
    return field;
}

const refusedMarkPrefix = 'exsheet-refused:';

/** ExSheet's mark on a refused cell of its clipboard table: the cell it was copied from, so that a paste's refusal
 * can name it (ADR-0047, 2026-10-12). The C# Sheet writes the same mark. */
export function refusedMarkFor(copiedFrom: CellAddress): string {
    return refusedMarkPrefix + copiedFrom.toString();
}

/** The cell a pasted field was copied from when ExSheet marked it refused, or `undefined` for any other field. */
export function refusedIn(mark: string | undefined): CellAddress | undefined {
    return mark?.startsWith(refusedMarkPrefix) === true ? CellAddress.tryParse(mark.slice(refusedMarkPrefix.length)) : undefined;
}
