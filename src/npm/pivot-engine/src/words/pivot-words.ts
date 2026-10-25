// The words ExPivot paints, by id, with their English, which is Excel's English (United States)
// edition's (ADR-0060): "Max of {0}", "Summarize value field by". A Consumer replaces any of them through
// a function from id to text, answering undefined to keep the English. A word with {0} (and {1}) is a
// template, filled as .NET's string.Format fills one, so a word that reads in C# reads here. The English
// (United Kingdom) and Japanese editions' words are bundled, as the C# engine bundles them; they never
// follow the locale on their own (ADR-0060's note of 2026-10-14).

import { PivotFormatError } from '../errors';
import type { PivotAggregation, PivotDatePart, PivotShowValuesAs } from '../layout/pivot-enums';
import { unreachable } from '../must';
import { dateEnglishWords, englishWords, japaneseWords, unitedKingdomWords } from './word-tables';

/** The Consumer's word for an id, or `undefined` to keep the English. */
export type PivotLabel = (id: string) => string | undefined;

const english: ReadonlyMap<string, string> = new Map(Object.entries(englishWords));
const unitedKingdom: ReadonlyMap<string, string> = new Map(Object.entries(unitedKingdomWords));
const japanese: ReadonlyMap<string, string> = new Map(Object.entries(japaneseWords));
const dateEnglish: ReadonlyMap<string, string> = new Map(Object.entries(dateEnglishWords));

/** The ids of the words a report paints, by what they name. */
export const pivotWordIds = Object.freeze({
    /** The Compact form's label column header: `Row Labels`. */
    rowLabels: 'row-labels',
    /** The Σ Values pseudo-field's header, and a report's lone value column when the values stand in
     * rows: `Values`. */
    values: 'values',
    /** The grand total row and column: `Grand Total`. */
    grandTotal: 'grand-total',
    /** An outer Item's subtotal: `{0} Total`, `{0}` the Item's label. */
    itemTotal: 'item-total',
    /** A Value Field's grand total row, with the values in rows: `Total {0}`, `{0}` its caption. */
    totalOf: 'total-of',
    /** An outer Item's subtotal row for one Value Field: `{0} {1}`, the Item's label and the caption. */
    itemValue: 'item-value',
    /** The Blank Item: `(blank)`. */
    blank: 'blank',
    /** A report filter with nothing hidden: `(All)`. */
    all: 'all',
    /** A report filter showing several Items: `(Multiple Items)`. */
    multipleItems: 'multiple-items',
    /** The Σ Values entry in an Area: `Σ Values`. */
    valuesPseudoField: 'values-pseudo-field',
    /** A year's Item: `{0}`, the year. */
    dateYear: 'date-year',
    /** A quarter's Item: `Qtr{0}`, the quarter from 1 to 4. */
    dateQuarter: 'date-quarter',
});

function slug(aggregation: PivotAggregation): string {
    switch (aggregation) {
        case 'sum': return 'sum';
        case 'count': return 'count';
        case 'average': return 'average';
        case 'max': return 'max';
        case 'min': return 'min';
        case 'product': return 'product';
        case 'countNumbers': return 'count-numbers';
        case 'stdDev': return 'stddev';
        case 'stdDevp': return 'stddevp';
        case 'var': return 'var';
        case 'varp': return 'varp';
        default: return unreachable(aggregation);
    }
}

/** The id of an Aggregation's name: `aggregation-sum` is `Sum`. */
export function aggregationNameId(aggregation: PivotAggregation): string {
    return `aggregation-${slug(aggregation)}`;
}

/** The id of a Value Field's default caption: `caption-sum` is `Sum of {0}`, `{0}` the field's caption. */
export function captionId(aggregation: PivotAggregation): string {
    return `caption-${slug(aggregation)}`;
}

/** The id of a Show Values As choice's name: `show-percent-of-grand-total` is `% of Grand Total`. */
export function showValuesAsNameId(showValuesAs: PivotShowValuesAs): string {
    switch (showValuesAs) {
        case 'noCalculation': return 'show-no-calculation';
        case 'percentOfGrandTotal': return 'show-percent-of-grand-total';
        case 'percentOfColumnTotal': return 'show-percent-of-column-total';
        case 'percentOfRowTotal': return 'show-percent-of-row-total';
        default: return unreachable(showValuesAs);
    }
}

/** The id of a month's Item, from 1 to 12: `date-month-9` is `Sep`. */
export function monthId(month: number): string {
    if (!Number.isInteger(month) || month < 1 || month > 12) throw new RangeError(`A month is from 1 to 12, not ${month}.`);
    return `date-month-${month}`;
}

/** The English for an id, or `undefined` for an id the engine does not know. A date part's words are
 * here too. */
export function englishWordFor(id: string): string | undefined {
    return english.get(id) ?? dateEnglish.get(id);
}

/** Every id the engine has an English word for, a date part's included: what a Consumer replaces to give
 * ExPivot another language. */
export function pivotWordIdList(): readonly string[] {
    return Object.freeze([...english.keys(), ...dateEnglish.keys()]);
}

/** The words of Excel's Japanese edition: `行ラベル`, `総計`, `合計 / 金額`, `(空白)`, a date part's
 * `2026年`, `第3四半期`, `9月`. A {@link PivotLabel} to hand to a report's options in one line. */
export const japanesePivotWords: PivotLabel = id => japanese.get(id);

/** The words of Excel's English (United Kingdom) edition where they differ from the English (United
 * States) edition's: `Max.`, `Max. of {0}`, `Summarise value field by`. */
export const unitedKingdomPivotWords: PivotLabel = id => unitedKingdom.get(id);

/** The word for an id: the Consumer's, the English where it says nothing, and the id itself where neither
 * knows it — an id painted is a missing word seen, never a blank. */
export function resolveWord(id: string, label: PivotLabel | undefined): string {
    return label?.(id) ?? english.get(id) ?? id;
}

/** A date part's word, resolved as the C# engine's PivotDateWords resolves one: through the Consumer's,
 * then the date part's English, then the id. */
function resolveDateWord(id: string, label: PivotLabel | undefined): string {
    return label?.(id) ?? dateEnglish.get(id) ?? id;
}

/** A template word with its arguments put in, as .NET's `string.Format` puts them in under the invariant
 * culture: `{n}`, `{n,width}`, `{n:format}` (a text takes no format), and `{{` and `}}` for a brace. A
 * template .NET refuses throws a PivotFormatError. */
export function fillWord(template: string, ...args: readonly string[]): string {
    const refused = (): PivotFormatError => new PivotFormatError(
        `The word '${template}' is not a template .NET fills: a brace stands where it cannot, or names an argument it is not given.`);
    let result = '';
    let i = 0;
    while (i < template.length) {
        const ch = template.charAt(i);
        if (ch === '}') {
            if (template.charAt(i + 1) !== '}') throw refused();
            result += '}';
            i += 2;
            continue;
        }
        if (ch !== '{') {
            result += ch;
            i++;
            continue;
        }
        if (template.charAt(i + 1) === '{') {
            result += '{';
            i += 2;
            continue;
        }
        const match = /^\{(\d+) *(?:, *(-?\d+) *)?(?::([^{}]*))?\}/.exec(template.slice(i));
        if (match === null) throw refused();
        const [whole, index = '', width] = match;
        const arg = args[Number(index)];
        if (arg === undefined) throw refused();
        const pad = width === undefined ? 0 : Number(width);
        result += pad < 0 ? arg.padEnd(-pad) : arg.padStart(pad);
        i += whole.length;
    }
    return result;
}

/** The part of a date: its year, its quarter from 1 to 4, or its month from 1 to 12. */
export function datePartOf(part: PivotDatePart, year: number, month: number): number {
    switch (part) {
        case 'year': return year;
        case 'quarter': return Math.floor((month - 1) / 3) + 1;
        case 'month': return month;
        default: return unreachable(part);
    }
}

/** Whether a number is one a date part takes: a year from 1 to 9999, a quarter from 1 to 4, a month from
 * 1 to 12, each whole. */
export function datePartTakes(part: PivotDatePart, value: number): boolean {
    if (value !== Math.floor(value)) return false;
    switch (part) {
        case 'year': return value >= 1 && value <= 9999;
        case 'quarter': return value >= 1 && value <= 4;
        case 'month': return value >= 1 && value <= 12;
        default: return unreachable(part);
    }
}

/** The label of a date part's Item in the report's words: `2026`, `Qtr3`, `Sep`. */
export function datePartLabel(part: PivotDatePart, value: number, label: PivotLabel | undefined): string {
    switch (part) {
        case 'year': return fillWord(resolveDateWord(pivotWordIds.dateYear, label), String(value));
        case 'quarter': return fillWord(resolveDateWord(pivotWordIds.dateQuarter, label), String(value));
        case 'month': return resolveDateWord(monthId(value), label);
        default: return unreachable(part);
    }
}
