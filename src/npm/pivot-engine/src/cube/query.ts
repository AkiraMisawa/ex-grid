// The question ExPivot asks a Pivot Source for a report (ADR-0066): the row and column fields, the Filters
// that hide something, each field in Values with the parts its Aggregations read, and the cap on leaves.
// A question is a serialisable value; a source answers it with the Leaf Aggregates, or refuses it. The C#
// engine's PivotQuery, PivotQueryField and PivotQueryValue, with their checks and messages.

import { pivotItemKey, publicKeyId, type PivotItemKey } from '../items/item-key';
import type { PivotLayout, PivotFieldPlacement } from '../layout/pivot-layout';
import { partExtremes, partProduct, partSum, partVariance, partsOf } from '../aggregates/parts';

/** A part of a Leaf Aggregate beyond the counts, which always travel (ADR-0066). */
export type PivotPart = 'sum' | 'extremes' | 'product' | 'variance';

/** The parts in their order. */
export const pivotParts: readonly PivotPart[] = Object.freeze(['sum', 'extremes', 'product', 'variance']);

const bitOf: Readonly<Record<PivotPart, number>> = Object.freeze({ sum: partSum, extremes: partExtremes, product: partProduct, variance: partVariance });

/** The parts as bits. */
export function partBits(parts: readonly PivotPart[]): number {
    let bits = 0;
    for (const part of parts) {
        if (!pivotParts.includes(part)) throw new RangeError(`'${part}' is not a part of a Leaf Aggregate.`);
        bits |= bitOf[part];
    }
    return bits;
}

/** The parts the bits name, in their order. */
export function partList(bits: number): readonly PivotPart[] {
    return Object.freeze(pivotParts.filter(part => (bits & bitOf[part]) !== 0));
}

/** The default cap on leaves: "this layout needs more than 200,000 cells". */
export const defaultMaxLeaves = 200_000;

/** A field placed in Rows, Columns or Filters, with the Items it hides. */
export interface PivotQueryField {
    /** The field's name. */
    readonly field: string;
    /** The Items whose records are left out. */
    readonly hiddenItems: readonly PivotItemKey[];
}

/** A field in Values, and the parts its Value Fields' Aggregations read. */
export interface PivotQueryValue {
    /** The field's name. */
    readonly field: string;
    /** The parts asked for beyond the counts. */
    readonly parts: readonly PivotPart[];
}

/** The question for a report's Leaf Aggregates. */
export interface PivotQuery {
    /** The row fields, outermost first. */
    readonly rows: readonly PivotQueryField[];
    /** The column fields, outermost first. */
    readonly columns: readonly PivotQueryField[];
    /** The Filters that hide something; one that hides nothing leaves every record in. */
    readonly filters: readonly PivotQueryField[];
    /** Each field in Values, once, with the parts its Value Fields read. */
    readonly values: readonly PivotQueryValue[];
    /** The most leaves the answer may have; one more refuses the question. */
    readonly maxLeaves: number;
}

/** A question's settings, each left out taking its default: no field anywhere and the default cap. */
export interface PivotQueryInput {
    /** The row fields, by name or with their Hidden Items. */
    readonly rows?: readonly (string | { readonly field: string; readonly hiddenItems?: readonly PivotItemKey[] })[];
    /** The column fields. */
    readonly columns?: readonly (string | { readonly field: string; readonly hiddenItems?: readonly PivotItemKey[] })[];
    /** The Filters that hide something. */
    readonly filters?: readonly (string | { readonly field: string; readonly hiddenItems?: readonly PivotItemKey[] })[];
    /** The fields in Values, by name or with their parts. */
    readonly values?: readonly (string | { readonly field: string; readonly parts?: readonly PivotPart[] })[];
    /** The cap on leaves; {@link defaultMaxLeaves} when left out. */
    readonly maxLeaves?: number;
}

function queryField(input: string | { readonly field: string; readonly hiddenItems?: readonly PivotItemKey[] }): PivotQueryField {
    const given = typeof input === 'string' ? { field: input } : input;
    if (typeof given.field !== 'string' || given.field.length === 0) throw new TypeError('A field of a question is named.');
    const hidden = (given.hiddenItems ?? []).map(key => pivotItemKey(key.kind, key.value)); // oxlint-disable-line ex-grid/no-stand-in-default -- a field that hides nothing hides no Item
    return Object.freeze({ field: given.field, hiddenItems: Object.freeze(hidden) });
}

function queryValue(input: string | { readonly field: string; readonly parts?: readonly PivotPart[] }): PivotQueryValue {
    const given = typeof input === 'string' ? { field: input } : input;
    if (typeof given.field !== 'string' || given.field.length === 0) throw new TypeError('A field in Values is named.');
    return Object.freeze({ field: given.field, parts: partList(partBits(given.parts ?? [])) }); // oxlint-disable-line ex-grid/no-stand-in-default -- the counts alone are no part
}

/**
 * A question, checked and frozen: a field placed twice among Filters, Rows and Columns, or asked for twice in
 * Values, is refused with a TypeError naming it; a cap below 1 with a RangeError.
 */
export function pivotQuery(input: PivotQueryInput = {}): PivotQuery {
    const rows = Object.freeze((input.rows ?? []).map(queryField)); // oxlint-disable-line ex-grid/no-stand-in-default -- no row field
    const columns = Object.freeze((input.columns ?? []).map(queryField)); // oxlint-disable-line ex-grid/no-stand-in-default -- no column field
    const filters = Object.freeze((input.filters ?? []).map(queryField)); // oxlint-disable-line ex-grid/no-stand-in-default -- no filter
    const values = Object.freeze((input.values ?? []).map(queryValue)); // oxlint-disable-line ex-grid/no-stand-in-default -- nothing in Values
    const maxLeaves = input.maxLeaves ?? defaultMaxLeaves;
    if (!Number.isSafeInteger(maxLeaves) || maxLeaves < 1) throw new RangeError(`A question allows at least one leaf, not ${maxLeaves}.`);
    const placed = new Set<string>();
    for (const field of [...rows, ...columns, ...filters]) {
        if (placed.has(field.field)) {
            throw new TypeError(`The question places '${field.field}' twice among Filters, Rows and Columns; a field stands in one of them at most (ADR-0061).`);
        }
        placed.add(field.field);
    }
    const asked = new Set<string>();
    for (const value of values) {
        if (asked.has(value.field)) {
            throw new TypeError(`The question asks for '${value.field}' twice in Values; ask once, with the parts of every Value Field of it (ADR-0066).`);
        }
        asked.add(value.field);
    }
    return Object.freeze({ rows, columns, filters, values, maxLeaves });
}

function askedOf(p: PivotFieldPlacement): PivotQueryField {
    return Object.freeze({ field: p.field, hiddenItems: p.hiddenItems });
}

/** The question a layout asks (ADR-0066): its row and column fields, the Filters that hide something, and
 * each field in Values once, with the parts of every Value Field of it. */
export function queryForLayout(layout: PivotLayout, maxLeaves: number = defaultMaxLeaves): PivotQuery {
    const values: { field: string; parts: number }[] = [];
    for (const value of layout.values) {
        const parts = partsOf(value.aggregation);
        const held = values.find(asked => asked.field === value.field);
        if (held === undefined) values.push({ field: value.field, parts });
        else held.parts |= parts;
    }
    return pivotQuery({
        rows: layout.rows.map(askedOf),
        columns: layout.columns.map(askedOf),
        filters: hidingFilters(layout.filters).map(askedOf),
        values: values.map(value => ({ field: value.field, parts: partList(value.parts) })),
        maxLeaves,
    });
}

/** The Filters that hide something. */
export function hidingFilters(filters: readonly PivotFieldPlacement[]): readonly PivotFieldPlacement[] {
    return filters.filter(placement => placement.hiddenItems.length > 0);
}

/** Every field the question places, Rows, Columns and Filters in that order. */
export function placedFields(query: PivotQuery): readonly PivotQueryField[] {
    return [...query.rows, ...query.columns, ...query.filters];
}

/** Whether two lists of Items hide the same Items, in any order. */
export function sameKeys(one: readonly PivotItemKey[], other: readonly PivotItemKey[]): boolean {
    if (one === other || (one.length === 0 && other.length === 0)) return true;
    const mine = new Set(one.map(publicKeyId));
    const theirs = new Set(other.map(publicKeyId));
    return mine.size === theirs.size && [...mine].every(id => theirs.has(id));
}

function sameFields(one: readonly PivotQueryField[], other: readonly PivotQueryField[]): boolean {
    return one.length === other.length
        && one.every((field, i) => {
            const theirs = other[i];
            return theirs !== undefined && field.field === theirs.field && sameKeys(field.hiddenItems, theirs.hiddenItems);
        });
}

/** Whether two questions are one. */
export function queriesEqual(a: PivotQuery, b: PivotQuery): boolean {
    return a === b
        || (a.maxLeaves === b.maxLeaves
            && sameFields(a.rows, b.rows) && sameFields(a.columns, b.columns) && sameFields(a.filters, b.filters)
            && a.values.length === b.values.length
            && a.values.every((value, i) => {
                const theirs = b.values[i];
                return theirs !== undefined && value.field === theirs.field && partBits(value.parts) === partBits(theirs.parts);
            }));
}

// ---- The other questions ------------------------------------------------------------------------

/** An Item of a field, as a cell's path names it. */
export interface PivotFieldItem {
    /** The field's name. */
    readonly field: string;
    /** The Item. */
    readonly item: PivotItemKey;
}

/** A field's Items over all the data, under a Source Version (ADR-0066). */
export interface PivotItemsQuery {
    /** The field whose Items are asked for. */
    readonly field: string;
    /** The Source Version they are asked for under. */
    readonly sourceVersion: string;
    /** Only the Items whose invariant text holds this, ignoring case. */
    readonly search?: string;
    /** The most Items the page lists. */
    readonly max: number;
}

/** The default most Items a page lists. */
export const defaultMaxItems = 10_000;

/** A question for a field's Items, checked: an empty search is no search. */
export function pivotItemsQuery(input: { readonly field: string; readonly sourceVersion: string; readonly search?: string; readonly max?: number }): PivotItemsQuery {
    if (typeof input.field !== 'string' || input.field.length === 0) throw new TypeError('The field whose Items are asked for is named.');
    if (typeof input.sourceVersion !== 'string') throw new TypeError('Items are asked for under a Source Version.');
    const max = input.max ?? defaultMaxItems;
    if (!Number.isSafeInteger(max) || max < 0) throw new RangeError(`A page lists at least no Item, not ${max}.`);
    const query: { -readonly [K in keyof PivotItemsQuery]: PivotItemsQuery[K] } = { field: input.field, sourceVersion: input.sourceVersion, max };
    if (input.search !== undefined && input.search.length > 0) query.search = input.search;
    return Object.freeze(query);
}

/** One page of the records behind a cell (ADR-0063/0066): those carrying every Item of its paths and no
 * Hidden Item of any placed field, under a Source Version. */
export interface PivotDetailsQuery {
    /** The Source Version the records are asked for under. */
    readonly sourceVersion: string;
    /** The row's Items, outermost first. */
    readonly rowItems: readonly PivotFieldItem[];
    /** The column's Items, outermost first. */
    readonly columnItems: readonly PivotFieldItem[];
    /** Every placed field's Hidden Items. */
    readonly hiddenItems: readonly PivotQueryField[];
    /** The place of the page's first record. */
    readonly start: number;
    /** The most records the page holds. */
    readonly count: number;
}

/** A question for the records behind a cell, checked: a path naming a field twice, or Hidden Items given
 * twice for one field, are refused by name. */
export function pivotDetailsQuery({ sourceVersion, rowItems = [], columnItems = [], hiddenItems = [], start = 0, count = Number.MAX_SAFE_INTEGER }: {
    readonly sourceVersion: string;
    readonly rowItems?: readonly PivotFieldItem[];
    readonly columnItems?: readonly PivotFieldItem[];
    readonly hiddenItems?: readonly PivotQueryField[];
    readonly start?: number;
    readonly count?: number;
}): PivotDetailsQuery {
    if (typeof sourceVersion !== 'string') throw new TypeError('The records behind a cell are asked for under a Source Version.');
    if (!Number.isSafeInteger(start) || start < 0) throw new RangeError(`A page starts at a record from 0, not ${start}.`);
    if (!Number.isSafeInteger(count) || count < 0) throw new RangeError(`A page holds at least no record, not ${count}.`);
    const step = (item: PivotFieldItem): PivotFieldItem => {
        if (typeof item.field !== 'string' || item.field.length === 0) throw new TypeError('A field is named.');
        return Object.freeze({ field: item.field, item: pivotItemKey(item.item.kind, item.item.value) });
    };
    const rows = Object.freeze(rowItems.map(step));
    const columns = Object.freeze(columnItems.map(step));
    const hidden = Object.freeze(hiddenItems.map(queryField));
    const named = new Set<string>();
    for (const item of [...rows, ...columns]) {
        if (named.has(item.field)) throw new TypeError(`The cell's path names '${item.field}' twice.`);
        named.add(item.field);
    }
    const hiding = new Set<string>();
    for (const field of hidden) {
        if (hiding.has(field.field)) throw new TypeError(`The question gives the Hidden Items of '${field.field}' twice.`);
        hiding.add(field.field);
    }
    return Object.freeze({ sourceVersion, rowItems: rows, columnItems: columns, hiddenItems: hidden, start, count });
}
