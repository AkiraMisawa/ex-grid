// The question ExPivot asks a Pivot Source for a report (ADR-0066): the row and column fields, the Filters
// that hide something, each field in Values with the parts its Aggregations read, and the cap on leaves.
// A question is a serialisable value; a source answers it with the Leaf Aggregates, or refuses it. The C#
// engine's PivotQuery, PivotQueryField and PivotQueryValue, with their checks and messages.

import { pivotItemKey, publicKeyId, type PivotItemKey } from '../items/item-key';
import type { PivotLayout, PivotFieldPlacement } from '../layout/pivot-layout';
import { pivotAggregations, type PivotAggregation } from '../layout/pivot-enums';
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

/**
 * One field's rule of a question's levels (ADR-0370 section 3): whether its Items are collapsed, and the
 * Items whose state is the other. Item `x` is expanded when `collapsed` is not `x ∈ toggledItems`; the Items
 * of the innermost field have nothing below them, and their rule changes nothing.
 */
export interface PivotLevelRule {
    /** Whether its Items are collapsed, but for the toggled ones. */
    readonly collapsed: boolean;
    /** The Items whose state is the other of `collapsed`. */
    readonly toggledItems: readonly PivotItemKey[];
    /** How a row field's Items are ordered by a value, for a question with a window; none for a field
     * ordered by label, and for a column field. */
    readonly order?: PivotLevelOrder;
}

/**
 * How a row field's Items are ordered among their siblings (ADR-0370 section 3): by a field in Values under an
 * Aggregation, read at the grand total of the columns, as the report sorts them. A sibling whose value is not
 * a number follows every number, in either direction. What a window of a sibling set is cut from.
 */
export interface PivotLevelOrder {
    /** The field in Values, by its place in the question's `values`. */
    readonly value: number;
    /** The Aggregation read of it. */
    readonly aggregation: PivotAggregation;
    /** Whether the largest comes first. */
    readonly descending: boolean;
}

/** Where one parent's window starts: the parent row group, by its Items, and the sibling it starts at. */
export interface PivotWindowStart {
    /** The parent's Items, outermost first: none for the grand total. */
    readonly rows: readonly PivotItemKey[];
    /** The place among the siblings, from 0, of the first one asked for. */
    readonly start: number;
}

/**
 * The window of a question's levels (ADR-0370 section 3): a sibling set ordered by a value with more than
 * `limit` siblings is answered as a window of them, starting where `starts` says for its parent, or at the
 * first, and no further than `limit` from the end; with what lies on either side merged.
 */
export interface PivotLevelWindow {
    /** The most siblings a set ordered by a value is answered with whole. */
    readonly limit: number;
    /** Where the windows start, one a parent at most. */
    readonly starts: readonly PivotWindowStart[];
}

/**
 * Which groups a report shows, as a question for Level Aggregates carries them (ADR-0370 section 3): one
 * rule a row field and one a column field. A group is shown when every Item above it is expanded, and the
 * grand total always is; a cell is a shown row group crossed with a shown column group. A server can turn
 * them into `GROUPING SETS` without knowing the report.
 */
export interface PivotLevels {
    /** The row fields' rules, outermost first. */
    readonly rows: readonly PivotLevelRule[];
    /** The column fields' rules, outermost first. */
    readonly columns: readonly PivotLevelRule[];
    /** The window of the sets ordered by a value; none, and every set is answered whole. */
    readonly window?: PivotLevelWindow;
}

/** A rule's settings, each left out taking its default: every Item expanded, ordered by label. */
export interface PivotLevelRuleInput {
    /** Whether the Items are collapsed, but for the toggled ones. */
    readonly collapsed?: boolean;
    /** The Items whose state is the other. */
    readonly toggledItems?: readonly PivotItemKey[];
    /** How the Items are ordered by a value. */
    readonly order?: PivotLevelOrder;
}

/** A rule, checked and frozen. */
export function pivotLevelRule(input: PivotLevelRuleInput = {}): PivotLevelRule {
    const collapsed = input.collapsed ?? false; // oxlint-disable-line ex-grid/no-stand-in-default -- a rule left unsaid is every Item expanded
    if (typeof collapsed !== 'boolean') throw new TypeError("A rule's collapse is true or false.");
    const toggled = (input.toggledItems ?? []).map(key => pivotItemKey(key.kind, key.value)); // oxlint-disable-line ex-grid/no-stand-in-default -- a rule that toggles nothing toggles no Item
    if (input.order === undefined) return Object.freeze({ collapsed, toggledItems: Object.freeze(toggled) });
    return Object.freeze({ collapsed, toggledItems: Object.freeze(toggled), order: levelOrder(input.order) });
}

function levelOrder(input: PivotLevelOrder): PivotLevelOrder {
    if (!Number.isSafeInteger(input.value) || input.value < 0) throw new RangeError(`An order names a field in Values by its place, not ${input.value}.`);
    if (!pivotAggregations.includes(input.aggregation)) throw new RangeError(`'${input.aggregation}' is not an Aggregation.`);
    if (typeof input.descending !== 'boolean') throw new TypeError("An order's direction is true or false.");
    return Object.freeze({ value: input.value, aggregation: input.aggregation, descending: input.descending });
}

/** A window's settings: its limit, and where its windows start, none by default. */
export interface PivotLevelWindowInput {
    /** The most siblings a set ordered by a value is answered with whole. */
    readonly limit: number;
    /** Where the windows start. */
    readonly starts?: readonly PivotWindowStart[];
}

function levelWindow(input: PivotLevelWindowInput): PivotLevelWindow {
    if (!Number.isSafeInteger(input.limit) || input.limit < 1) throw new RangeError(`A window holds at least one sibling, not ${input.limit}.`);
    const seen = new Set<string>();
    const starts = (input.starts ?? []).map(given => { // oxlint-disable-line ex-grid/no-stand-in-default -- a window named nowhere starts every set at its first sibling
        if (!Number.isSafeInteger(given.start) || given.start < 0) throw new RangeError(`A window starts at a sibling's place from 0, not ${given.start}.`);
        const rows = given.rows.map(key => pivotItemKey(key.kind, key.value));
        const id = rows.map(publicKeyId).join('\u0000');
        if (seen.has(id)) throw new TypeError('The window starts twice under one parent (ADR-0370 section 3).');
        seen.add(id);
        return Object.freeze({ rows: Object.freeze(rows), start: given.start });
    });
    return Object.freeze({ limit: input.limit, starts: Object.freeze(starts) });
}

/** Whether the groups below `item` are shown under `rule`. */
export function isLevelExpanded(rule: PivotLevelRule, item: PivotItemKey): boolean {
    const id = publicKeyId(item);
    return rule.collapsed === rule.toggledItems.some(toggled => publicKeyId(toggled) === id);
}

function ruleOfPlacement(placement: PivotFieldPlacement): PivotLevelRule {
    return pivotLevelRule({ collapsed: placement.collapsed, toggledItems: placement.toggledItems });
}

/** The groups a layout shows: each row and column field's collapse state. */
export function levelsOfLayout(layout: PivotLayout): PivotLevels {
    return Object.freeze({ rows: Object.freeze(layout.rows.map(ruleOfPlacement)), columns: Object.freeze(layout.columns.map(ruleOfPlacement)) });
}

function sameOrder(a: PivotLevelOrder | undefined, b: PivotLevelOrder | undefined): boolean {
    if (a === undefined || b === undefined) return a === b;
    return a.value === b.value && a.aggregation === b.aggregation && a.descending === b.descending;
}

function sameRules(one: readonly PivotLevelRule[], other: readonly PivotLevelRule[]): boolean {
    return one.length === other.length
        && one.every((rule, i) => {
            const theirs = other[i];
            return theirs !== undefined && rule.collapsed === theirs.collapsed && sameKeys(rule.toggledItems, theirs.toggledItems)
                && sameOrder(rule.order, theirs.order);
        });
}

// Two windows' starts are one when each parent starts at the same place, in any order.
function sameWindow(a: PivotLevelWindow | undefined, b: PivotLevelWindow | undefined): boolean {
    if (a === undefined || b === undefined) return a === b;
    const startOf = (window: PivotLevelWindow): Map<string, number> => new Map(window.starts.map(start => [start.rows.map(publicKeyId).join('\u0000'), start.start]));
    const mine = startOf(a);
    const theirs = startOf(b);
    return a.limit === b.limit && mine.size === theirs.size && [...mine].every(([parent, start]) => theirs.get(parent) === start);
}

/** Whether two questions' levels show the same groups: both none, or the same rules and the same window. */
export function sameLevels(a: PivotLevels | undefined, b: PivotLevels | undefined): boolean {
    if (a === undefined || b === undefined) return a === b;
    return sameRules(a.rows, b.rows) && sameRules(a.columns, b.columns) && sameWindow(a.window, b.window);
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
    /**
     * Which groups the report shows, for a source that answers Level Aggregates (ADR-0370 section 3): it may
     * then answer, when the leaves would pass the cap, with the cells of the groups shown instead
     * (an answer's `isLevels`). Left out, the question asks for the leaves alone.
     */
    readonly levels?: PivotLevels;
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
    /** Which groups the report shows, one rule a row and a column field, and the window of the sets ordered by a
     * value; none when left out. */
    readonly levels?: {
        readonly rows?: readonly PivotLevelRuleInput[];
        readonly columns?: readonly PivotLevelRuleInput[];
        readonly window?: PivotLevelWindowInput;
    };
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
    if (input.levels === undefined) return Object.freeze({ rows, columns, filters, values, maxLeaves });
    const levelRows = Object.freeze((input.levels.rows ?? []).map(pivotLevelRule)); // oxlint-disable-line ex-grid/no-stand-in-default -- no row field, no rule
    const levelColumns = Object.freeze((input.levels.columns ?? []).map(pivotLevelRule)); // oxlint-disable-line ex-grid/no-stand-in-default -- no column field, no rule
    const levels: PivotLevels = Object.freeze(input.levels.window === undefined
        ? { rows: levelRows, columns: levelColumns }
        : { rows: levelRows, columns: levelColumns, window: levelWindow(input.levels.window) });
    if (levels.rows.length !== rows.length || levels.columns.length !== columns.length) {
        throw new TypeError(`The question's levels give ${levels.rows.length} row and ${levels.columns.length} column rules, and it places ${rows.length} row and ${columns.length} column fields; a field has one rule (ADR-0370 section 3).`);
    }
    checkOrders(levels, values);
    return Object.freeze({ rows, columns, filters, values, maxLeaves, levels });
}

// A row field's order names a field in Values whose parts its Aggregation reads; a column field's names none, since
// only row sets are windowed; a window's parent names an Item of each row field above the set.
function checkOrders(levels: PivotLevels, values: readonly PivotQueryValue[]): void {
    if (levels.columns.some(rule => rule.order !== undefined)) {
        throw new TypeError("A column field's rule says no order; only row sets are answered as windows (ADR-0370 section 3).");
    }
    for (const rule of levels.rows) {
        const order = rule.order;
        if (order === undefined) continue;
        const value = values[order.value];
        if (value === undefined) throw new RangeError(`An order reads value ${order.value}, and the question asks for ${values.length} values.`);
        const needed = partsOf(order.aggregation);
        if ((partBits(value.parts) & needed) !== needed) {
            throw new TypeError(`An order reads the ${order.aggregation} of '${value.field}', and the question asks for none of its parts (ADR-0370 section 3).`);
        }
    }
    for (const start of levels.window?.starts ?? []) { // oxlint-disable-line ex-grid/no-stand-in-default -- no window starts nowhere
        if (start.rows.length >= levels.rows.length) {
            throw new RangeError(`A window's parent names ${start.rows.length} Items, and the question places ${levels.rows.length} row fields; its children are a row field's Items (ADR-0370 section 3).`);
        }
    }
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

/** The question {@link queryForLayout} asks, with the groups the layout shows (ADR-0370 section 3): what
 * ExPivot asks a source that answers Level Aggregates. */
export function levelQueryForLayout(layout: PivotLayout, maxLeaves: number = defaultMaxLeaves): PivotQuery {
    return Object.freeze({ ...queryForLayout(layout, maxLeaves), levels: levelsOfLayout(layout) });
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
            && sameLevels(a.levels, b.levels)
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

/** The most records a question for them asks for: every one, as the C# engine's `int.MaxValue`, the most a
 * document carries (PivotJson). */
export const everyRecord = 2_147_483_647;

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
    /** The most records the page holds; every one, 2^31 − 1 as the C# engine's `int.MaxValue`, when the
     * question was made without one, so that it crosses to a C# server as it is. */
    readonly count: number;
}

/** A question for the records behind a cell, checked: a path naming a field twice, or Hidden Items given
 * twice for one field, are refused by name. */
export function pivotDetailsQuery({ sourceVersion, rowItems = [], columnItems = [], hiddenItems = [], start = 0, count = everyRecord }: {
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
