// How a report labels and orders Items (ADR-0060): a number Item in its field's format or in General, a date
// in its format or the locale's short date, a date part in the report's words, `(blank)`, `#NUM!`, TRUE and
// FALSE; and the order by label — `(blank)` last always, a declared Item order first, keyed Items by their
// Order Key, then by kind, numbers and dates by value, text by the locale's collation ignoring case. The C#
// engine's ItemLabels and ItemOrder.

import type { Culture } from '../culture/culture-data';
import { cultureOf } from '../culture/culture-data';
import { PivotFormatError, PivotOrderKeyError } from '../errors';
import type { FieldMeta, PivotOrderKeyValue } from '../fields/pivot-field';
import { keyIdOf, pivotErrorText } from '../items/item-key';
import { itemKindOrder } from '../layout/pivot-enums';
import { compareOrdinal } from '../text/casing';
import { compareInCulture } from '../text/collation';
import { formatDate } from '../text/date-text';
import { generalOfDouble } from '../text/general';
import { formatDouble } from '../text/number-text';
import { datePartLabel, datePartTakes, pivotWordIds, resolveWord, type PivotLabel } from '../words/pivot-words';
import type { ItemRef } from '../cube/cube';
import { unreachable } from '../must';

/** What a report is laid out in: a locale — a BCP 47 tag or `invariant` — and the words it is written in. */
export interface PivotOptions {
    /** The locale numbers and dates are written in and text is ordered by (ADR-0370 §10); the browser's
     * language when left out. */
    readonly locale?: string;
    /** The report's words by id, for those the Consumer translates; English for the rest. */
    readonly label?: PivotLabel;
}

/** Options resolved: the culture, and the word of an id. */
export interface ResolvedOptions {
    readonly culture: Culture;
    readonly label: PivotLabel | undefined;
    word(id: string): string;
}

// The browser's language, or none where the runtime has no navigator.
function browserLanguage(): string | undefined {
    const navigator: unknown = Reflect.get(globalThis, 'navigator');
    if (typeof navigator !== 'object' || navigator === null) return undefined;
    const language: unknown = Reflect.get(navigator, 'language');
    return typeof language === 'string' && language.length > 0 ? language : undefined;
}

/** The options resolved, refusing a locale the engine does not format in. With no locale given, the
 * browser's language is taken; where there is none, the locale must be given. */
export function resolveOptions(options: PivotOptions = {}): ResolvedOptions {
    const locale = options.locale ?? browserLanguage();
    if (locale === undefined) throw new TypeError("A report's options name its locale where the runtime has no browser language.");
    if (typeof locale !== 'string') throw new TypeError("A report's locale is a BCP 47 tag or 'invariant'.");
    const culture = cultureOf(locale);
    const label = options.label;
    return Object.freeze({ culture, label, word: (id: string) => resolveWord(id, label) });
}

const ticksPerDay = 864_000_000_000n;

// A format .NET cannot apply to the value writes it in its own text, as C#'s PivotNumberFormat.Apply does;
// a field's format is checked when it is declared, so only an unusual value reaches here.
function applyDouble(value: number, format: string, culture: Culture): string {
    try {
        return formatDouble(value, format, culture.data.number);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- C#'s PivotNumberFormat.Apply writes the value in its own text when .NET refuses the format

        if (!(error instanceof PivotFormatError)) throw error;
    }
    return formatDouble(value, undefined, culture.data.number);
}

function applyDate(ticks: bigint, format: string, culture: Culture): string {
    try {
        return formatDate(ticks, format, culture.data.date);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- C#'s Apply writes the date in its own text when .NET refuses the format

        if (!(error instanceof PivotFormatError)) throw error;
    }
    return formatDate(ticks, undefined, culture.data.date);
}

/** The labels of a report's Items, each computed once. */
export class ItemLabels {
    readonly #options: ResolvedOptions;
    readonly #labels = new Map<ItemRef, string>();

    constructor(options: ResolvedOptions) {
        this.#options = options;
    }

    /** The label of an Item of a field. */
    of(item: ItemRef, meta: FieldMeta): string {
        let label = this.#labels.get(item);
        if (label === undefined) {
            label = this.#compute(item, meta);
            this.#labels.set(item, label);
        }
        return label;
    }

    /** The number a number Item's label shows, for its copy; `undefined` for any other, a date part's word
     * included. */
    static numberOf(item: ItemRef, meta: FieldMeta): number | undefined {
        if (item.key.kind !== 'number') return undefined;
        const part = meta.field.datePart;
        if (part !== undefined && datePartTakes(part, item.key.number)) return undefined;
        return item.key.number;
    }

    #compute(item: ItemRef, meta: FieldMeta): string {
        const culture = this.#options.culture;
        const key = item.key;
        const part = meta.field.datePart;
        // A date part is labelled as Excel labels it, in the report's words: 2026, Qtr3, Sep.
        if (part !== undefined && key.kind === 'number' && datePartTakes(part, key.number)) {
            return datePartLabel(part, key.number, this.#options.label);
        }
        switch (key.kind) {
            case 'blank': return this.#options.word(pivotWordIds.blank);
            case 'error': return pivotErrorText;
            case 'boolean': return key.ticks === 1n ? 'TRUE' : 'FALSE';
            case 'number': {
                const format = meta.field.format;
                return format === undefined ? generalOfDouble(key.number, culture.data.number) : applyDouble(key.number, format, culture);
            }
            case 'date': {
                const format = meta.field.format ?? (key.ticks % ticksPerDay === 0n ? 'd' : 'G');
                return applyDate(key.ticks, format, culture);
            }
            case 'text': return key.text;
            default: return unreachable(key.kind);
        }
    }
}

// C#'s IComparable.CompareTo for each kind an Order Key gives: NaN below every number, false below true;
// text compared ordinally, where C# compares it in the process's culture, which a browser has no share in.
function compareKeys(x: PivotOrderKeyValue, y: PivotOrderKeyValue): number {
    if (typeof x === 'number' && typeof y === 'number') {
        if (Number.isNaN(x)) return Number.isNaN(y) ? 0 : -1;
        if (Number.isNaN(y)) return 1;
        return x < y ? -1 : x > y ? 1 : 0;
    }
    if (typeof x === 'bigint' && typeof y === 'bigint') return x < y ? -1 : x > y ? 1 : 0;
    if (typeof x === 'boolean' && typeof y === 'boolean') return x === y ? 0 : x ? 1 : -1;
    if (typeof x === 'string' && typeof y === 'string') return compareOrdinal(x, y);
    throw new Error('Internal: two Order Keys of different types are compared.');
}

function keyTypeName(key: PivotOrderKeyValue): string {
    return typeof key;
}

/** The order of one field's Items by label (ADR-0060), ascending or descending; `(blank)` is last either
 * way. With `byKey` false, as a sort by value's ties are broken, the Order Key is not read. */
export class ItemOrder {
    readonly #meta: FieldMeta;
    readonly #labels: ItemLabels;
    readonly #culture: string;
    readonly #descending: boolean;
    readonly #byKey: boolean;
    #firstKeyed: ItemRef | undefined;

    constructor(meta: FieldMeta, labels: ItemLabels, culture: Culture, descending: boolean, byKey = true) {
        this.#meta = meta;
        this.#labels = labels;
        this.#culture = culture.name;
        this.#descending = descending;
        this.#byKey = byKey;
    }

    /** Asks the Order Key of each Item, once: one that throws is refused, naming the field and the Item,
     * and so are keys of two types. */
    prepare(item: ItemRef): void {
        const orderKey = this.#meta.field.orderKey;
        if (!this.#byKey || orderKey === undefined) return;
        if (!item.hasOrderKey) {
            let key: PivotOrderKeyValue | undefined;
            if (item.key.kind !== 'blank' && item.key.kind !== 'error' && item.firstValue !== undefined) {
                let given: unknown;
                try {
                    given = orderKey(item.firstValue);
                } catch (error) {
                    // An Order Key that refuses in its own words, naming this field, is refused in them.
                    if (error instanceof PivotOrderKeyError && error.field === this.#meta.field.name) throw error;
                    throw new PivotOrderKeyError(this.#meta.field.name,
                        `The Order Key of ${this.#meta.field.caption} failed on '${this.#labels.of(item, this.#meta)}'.`, { cause: error });
                }
                key = this.#keyOf(given, item);
            }
            item.setOrderKey(key);
        }
        const key = item.orderKey;
        if (key === undefined) return;
        const first = this.#firstKeyed;
        if (first === undefined) {
            this.#firstKeyed = item;
            return;
        }
        const firstKey = first.orderKey;
        if (firstKey !== undefined && typeof firstKey !== typeof key) {
            throw new PivotOrderKeyError(this.#meta.field.name,
                `The Order Key of ${this.#meta.field.caption} gave '${this.#labels.of(first, this.#meta)}' a key of type ${keyTypeName(firstKey)} `
                + `and '${this.#labels.of(item, this.#meta)}' one of type ${keyTypeName(key)}; a field's keys are of one type.`);
        }
    }

    #keyOf(given: unknown, item: ItemRef): PivotOrderKeyValue | undefined {
        if (given === null || given === undefined) return undefined;
        if (typeof given === 'string' || typeof given === 'number' || typeof given === 'bigint' || typeof given === 'boolean') return given;
        throw new PivotOrderKeyError(this.#meta.field.name,
            `The Order Key of ${this.#meta.field.caption} gave '${this.#labels.of(item, this.#meta)}' a key that is not a string, a number, a bigint or a Boolean.`);
    }

    /** -1, 0 or 1 as `x` comes before, with or after `y`. */
    compare(x: ItemRef, y: ItemRef): number {
        if (x === y) return 0;
        const xBlank = x.key.kind === 'blank';
        const yBlank = y.key.kind === 'blank';
        if (xBlank || yBlank) return xBlank === yBlank ? 0 : xBlank ? 1 : -1;
        const ascending = this.#ascending(x, y);
        return this.#descending ? -ascending : ascending;
    }

    #ascending(x: ItemRef, y: ItemRef): number {
        const xAt = this.#meta.declaredOrder.get(keyIdOf(x.key));
        const yAt = this.#meta.declaredOrder.get(keyIdOf(y.key));
        if (xAt !== undefined || yAt !== undefined) {
            if (xAt !== undefined && yAt !== undefined) return Math.sign(xAt - yAt);
            return xAt !== undefined ? -1 : 1;
        }
        if (this.#byKey && this.#meta.field.orderKey !== undefined) {
            // Keyed Items first, by key; the Items with no key after them; ties by label.
            const xKey = x.hasOrderKey ? x.orderKey : undefined;
            const yKey = y.hasOrderKey ? y.orderKey : undefined;
            if (xKey !== undefined && yKey !== undefined) {
                const byKeys = compareKeys(xKey, yKey);
                if (byKeys !== 0) return byKeys;
            } else if (xKey !== undefined || yKey !== undefined) {
                return xKey !== undefined ? -1 : 1;
            }
        }
        if (x.key.kind !== y.key.kind) return Math.sign(itemKindOrder(x.key.kind) - itemKindOrder(y.key.kind));
        switch (x.key.kind) {
            case 'number': return x.key.number < y.key.number ? -1 : x.key.number > y.key.number ? 1 : 0;
            case 'date':
            case 'boolean': return x.key.ticks < y.key.ticks ? -1 : x.key.ticks > y.key.ticks ? 1 : 0;
            case 'text': {
                const xText = this.#labels.of(x, this.#meta);
                const yText = this.#labels.of(y, this.#meta);
                const compared = compareInCulture(this.#culture, xText, yText);
                return compared !== 0 ? compared : compareOrdinal(xText, yText);
            }
            case 'error':
            case 'blank': return 0;
            default: return unreachable(x.key.kind);
        }
    }
}
