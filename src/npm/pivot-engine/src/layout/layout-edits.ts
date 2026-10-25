// What each Field List gesture means (ADR-0061), as pure functions from a layout to the next. The
// component, a substituted Chrome and a server apply the same rules: a field stands at most once across
// Filters, Rows and Columns and its settings travel with it; it may stand in Values any number of times;
// Σ Values moves between Rows and Columns only and always stands last; and an index is where the entry is
// inserted in the Area as it stands, the Area's count being its end. The C# engine's PivotLayoutEdits.

import { fieldMetas, numberFormatProblem, type PivotField } from '../fields/pivot-field';
import { itemKeysEqual, publicKeyId, type PivotItemKey } from '../items/item-key';
import { elementAt } from '../must';
import { resolveOptions, type PivotOptions } from '../report/labels';
import { valueCaptions } from '../report/report';
import { compareOrdinalIgnoringCase } from '../text/casing';
import type { PivotAggregation, PivotArea, PivotAxis, PivotReportForm } from './pivot-enums';
import {
    createPivotFieldPlacement, createPivotValueField, isItemCollapsed, placementOf, placementsIn, pivotSort, sortsEqual, withLayout,
    withPlacement, type PivotFieldPlacement, type PivotLayout, type PivotSort, type PivotValueField, type PivotValueFieldInput,
} from './pivot-layout';

/** A placed entry of an Area, as the Field List shows it (ADR-0061): a field in Filters, Rows or Columns,
 * a Value Field, or Σ Values, whose index is −1 and which stands last in its Area. */
export interface PivotEntry {
    /** The Area it stands in. */
    readonly area: PivotArea;
    /** Its index in that Area's list; −1 for Σ Values. */
    readonly index: number;
}

/** Σ Values, standing in the Area its axis names. */
export function valuesPseudoField(axis: PivotAxis): PivotEntry {
    return Object.freeze({ area: axis === 'rows' ? 'rows' : 'columns', index: -1 });
}

/** Whether an entry is Σ Values. */
export function isValuesPseudoField(entry: PivotEntry): boolean {
    return entry.index === -1;
}

/** What the Field List reads of a field: its name, caption and declared type. */
export type PivotFieldInfo = Pick<PivotField, 'name' | 'caption' | 'type'>;

/** Why the Field List refused an edit (ADR-0060/0061). */
export type PivotRefusal = 'hidesEveryItem' | 'captionTaken' | 'captionEmpty' | 'numberFormatInvalid';

/** An edit that may be refused: the layout it produced, or the unchanged layout and why not. */
export interface PivotEditResult {
    /** The new layout, or the one given when refused. */
    readonly layout: PivotLayout;
    /** Why the edit was refused. */
    readonly refusal?: PivotRefusal;
}

const refusalWords: Readonly<Record<PivotRefusal, string>> = Object.freeze({
    hidesEveryItem: 'refused-hides-every-item',
    captionTaken: 'refused-caption-taken',
    captionEmpty: 'refused-caption-empty',
    numberFormatInvalid: 'refused-number-format',
});

/** The id of a refusal's sentence among the report's words. */
export function refusalWord(refusal: PivotRefusal): string {
    return refusalWords[refusal];
}

function made(layout: PivotLayout): PivotEditResult {
    return Object.freeze({ layout });
}

function refused(layout: PivotLayout, refusal: PivotRefusal): PivotEditResult {
    return Object.freeze({ layout, refusal });
}

/**
 * One choice of the Layout menu on the Pivot Toolbar (ADR-0061): Excel's Design tab settings, under
 * Excel's names, in Excel's order — Subtotals, Grand Totals, then Report Layout. Excel's Blank Rows is left
 * out: the engine has no blank row.
 */
export type PivotLayoutChoice =
    | 'doNotShowSubtotals' | 'showSubtotalsAtBottom' | 'showSubtotalsAtTop'
    | 'grandTotalsOff' | 'grandTotalsOn' | 'grandTotalsOnRowsOnly' | 'grandTotalsOnColumnsOnly'
    | 'compactForm' | 'outlineForm' | 'tabularForm' | 'repeatItemLabels' | 'doNotRepeatItemLabels';

/** The Layout menu's choices, in Excel's order. */
export const pivotLayoutChoices: readonly PivotLayoutChoice[] = Object.freeze([
    'doNotShowSubtotals', 'showSubtotalsAtBottom', 'showSubtotalsAtTop',
    'grandTotalsOff', 'grandTotalsOn', 'grandTotalsOnRowsOnly', 'grandTotalsOnColumnsOnly',
    'compactForm', 'outlineForm', 'tabularForm', 'repeatItemLabels', 'doNotRepeatItemLabels',
]);

/** The Aggregation a new Value Field of a field takes: Sum for a field declared Number, Count for any
 * other (ADR-0060). */
export function defaultAggregation(field: PivotFieldInfo): PivotAggregation {
    return field.type === 'number' ? 'sum' : 'count';
}

function newValue(field: string, aggregation: PivotAggregation): PivotValueField {
    return createPivotValueField({ field, aggregation });
}

/** Ticking a field (ADR-0061): one standing nowhere goes to the end of Values when it is declared Number,
 * and to the end of Rows otherwise. A field already standing somewhere is left where it is. */
export function tickField(layout: PivotLayout, field: PivotFieldInfo): PivotLayout {
    if (placementOf(layout, field.name) !== undefined || layout.values.some(v => v.field === field.name)) return layout;
    return field.type === 'number'
        ? withLayout(layout, { values: Object.freeze([...layout.values, newValue(field.name, defaultAggregation(field))]) })
        : withLayout(layout, { rows: Object.freeze([...layout.rows, createPivotFieldPlacement(field.name)]) });
}

/** Unticking a field: it leaves every Area it stands in, each of its Value Fields included (ADR-0061). */
export function untickField(layout: PivotLayout, field: string): PivotLayout {
    let result = withLayout(layout, {
        filters: Object.freeze(layout.filters.filter(p => p.field !== field)),
        rows: Object.freeze(layout.rows.filter(p => p.field !== field)),
        columns: Object.freeze(layout.columns.filter(p => p.field !== field)),
    });
    for (let i = result.values.length - 1; i >= 0; i--) {
        if (elementAt(result.values, i).field === field) result = removeValue(result, i);
    }
    return same(layout, result) ? layout : result;
}

/** A field dropped from the list of fields on an Area at `index` (ADR-0061): in Values it is a new Value
 * Field, whatever else it stands in; in Filters, Rows or Columns it is placed there, moving out of
 * whichever of them it stood in, with its settings. */
export function placeField(layout: PivotLayout, field: PivotFieldInfo, area: PivotArea, index: number): PivotLayout {
    if (area === 'values') return insertValue(layout, newValue(field.name, defaultAggregation(field)), index);
    checkArea(area);
    const at = placementOf(layout, field.name);
    if (at !== undefined) return moveEntry(layout, at, area, index, field);
    return insert(layout, area, createPivotFieldPlacement(field.name), index);
}

/**
 * A placed entry dropped on an Area at `index` (ADR-0061). Within its own Area it is a reorder. A field
 * moving between Filters, Rows and Columns keeps its settings; one moving to Values leaves its Area and
 * becomes a Value Field; a Value Field moving to another Area leaves Values and its field is placed there.
 * Σ Values moves between Rows and Columns only — anywhere else it stays — and always stands last. `field`
 * is the dragged entry's field, whose declared type decides a new Value Field's Aggregation; it is not read
 * for Σ Values.
 */
export function moveEntry(layout: PivotLayout, entry: PivotEntry, area: PivotArea, index: number, field: PivotFieldInfo | undefined): PivotLayout {
    if (isValuesPseudoField(entry)) {
        if (area === 'rows') return layout.valuesAxis === 'rows' ? layout : withLayout(layout, { valuesAxis: 'rows' });
        if (area === 'columns') return layout.valuesAxis === 'columns' ? layout : withLayout(layout, { valuesAxis: 'columns' });
        return layout;
    }
    if (field === undefined) throw new TypeError('A dragged field entry names its field.');

    if (entry.area === 'values') {
        checkIndex(layout.values.length, entry.index);
        const value = elementAt(layout.values, entry.index);
        if (field.name !== value.field) throw new TypeError(`The dragged Value Field is '${value.field}', not '${field.name}'.`);
        if (area === 'values') return moveValue(layout, entry.index, index);
        const withoutValue = removeValue(layout, entry.index);
        const placed = placementOf(withoutValue, value.field);
        if (placed !== undefined) return moveEntry(withoutValue, placed, area, index, field);
        return insert(withoutValue, area, createPivotFieldPlacement(value.field), index);
    }

    checkArea(entry.area);
    const from = placementsIn(layout, entry.area);
    checkIndex(from.length, entry.index);
    const placement = elementAt(from, entry.index);
    if (field.name !== placement.field) throw new TypeError(`The dragged field is '${placement.field}', not '${field.name}'.`);
    if (area === 'values') {
        const without = replace(layout, entry.area, from.filter((_, i) => i !== entry.index));
        return insertValue(without, newValue(placement.field, defaultAggregation(field)), index);
    }
    checkArea(area);
    if (area === entry.area) {
        const target = clamp(index, from.length);
        if (target === entry.index || target === entry.index + 1) return layout;
        const reordered = from.filter((_, i) => i !== entry.index);
        reordered.splice(target > entry.index ? target - 1 : target, 0, placement);
        return replace(layout, area, reordered);
    }
    const removed = replace(layout, entry.area, from.filter((_, i) => i !== entry.index));
    return insert(removed, area, placement, index);
}

/** Removing an entry — Remove Field, or dragging it back to the list of fields (ADR-0061). Σ Values cannot
 * be removed: it leaves when fewer than two Value Fields remain. */
export function removeEntry(layout: PivotLayout, entry: PivotEntry): PivotLayout {
    if (isValuesPseudoField(entry)) return layout;
    if (entry.area === 'values') {
        checkIndex(layout.values.length, entry.index);
        return removeValue(layout, entry.index);
    }
    checkArea(entry.area);
    const from = placementsIn(layout, entry.area);
    checkIndex(from.length, entry.index);
    return replace(layout, entry.area, from.filter((_, i) => i !== entry.index));
}

/**
 * A field's Hidden Items, as Filter… applies them (ADR-0060/0061): refused when they would hide every one
 * of `items` — the field's Items in the data — because a report of nothing reads as "no data". A key hidden
 * that no Item carries is kept: it hides the Item if later data brings it.
 */
export function setHiddenItems(layout: PivotLayout, field: string, hidden: Iterable<PivotItemKey>, items: readonly PivotItemKey[]): PivotEditResult {
    const set: PivotItemKey[] = [];
    const ids = new Set<string>();
    for (const key of hidden) {
        const id = publicKeyId(key);
        if (ids.has(id)) continue;
        ids.add(id);
        set.push(key);
    }
    if (items.length > 0 && items.every(item => ids.has(publicKeyId(item)))) return refused(layout, 'hidesEveryItem');
    return made(update(layout, field, p => createPivotFieldPlacement({ ...p, hiddenItems: set })));
}

/** A field's Item order — Sort A to Z, Z to A, or by a Value Field. */
export function setSort(layout: PivotLayout, field: string, sort: PivotSort): PivotLayout {
    const byValue = sort.byValue;
    if (byValue !== undefined && (byValue < 0 || byValue >= layout.values.length)) {
        throw new RangeError(`The layout has ${layout.values.length} Value Fields, and the sort names ${byValue}.`);
    }
    const checked = pivotSort(sort.direction, byValue);
    return update(layout, field, p => (sortsEqual(p.sort, checked) ? p : withPlacement(p, { sort: checked })));
}

/** A field's subtotals: Automatic (true) or None. */
export function setSubtotals(layout: PivotLayout, field: string, subtotals: boolean): PivotLayout {
    return update(layout, field, p => (p.subtotals === subtotals ? p : withPlacement(p, { subtotals })));
}

/** One Item collapsed or expanded, wherever it appears (ADR-0060). */
export function setCollapsed(layout: PivotLayout, field: string, item: PivotItemKey, collapsed: boolean): PivotLayout {
    return update(layout, field, p => {
        if (isItemCollapsed(p, item) === collapsed) return p;
        const toggled = p.toggledItems.some(key => itemKeysEqual(key, item))
            ? p.toggledItems.filter(key => !itemKeysEqual(key, item))
            : [...p.toggledItems, item];
        return createPivotFieldPlacement({ ...p, toggledItems: toggled });
    });
}

/** Expand or Collapse Entire Field: every Item of the field, with no exception. */
export function setFieldCollapsed(layout: PivotLayout, field: string, collapsed: boolean): PivotLayout {
    return update(layout, field, p => (p.collapsed === collapsed && p.toggledItems.length === 0
        ? p
        : withPlacement(p, { collapsed, toggledItems: Object.freeze([]) })));
}

/**
 * A Value Field's settings, as Value Field Settings… applies them (ADR-0060/0061): refused when its own
 * caption is empty, or is another Value Field's caption or a Pivot Field's, or when its number format
 * cannot be used. A caption is compared with the captions the report shows, in its words.
 */
export function setValueField(layout: PivotLayout, index: number, value: PivotValueFieldInput, fields: readonly PivotField[],
    options?: PivotOptions): PivotEditResult {
    checkIndex(layout.values.length, index);
    const caption = value.caption;
    if (caption !== undefined) {
        if (caption.trim().length === 0) return refused(layout, 'captionEmpty');
        const others = layout.values.filter((_, i) => i !== index);
        const otherCaptions = valueCaptions(others, fieldMetas(fields), resolveOptions(options));
        if (otherCaptions.some(c => compareOrdinalIgnoringCase(c, caption) === 0)
            || fields.some(f => compareOrdinalIgnoringCase(f.caption, caption) === 0)) {
            return refused(layout, 'captionTaken');
        }
    }
    if (value.numberFormat !== undefined && numberFormatProblem(value.numberFormat) !== undefined) return refused(layout, 'numberFormatInvalid');
    const values = [...layout.values];
    values[index] = createPivotValueField(value);
    return made(withLayout(layout, { values: Object.freeze(values) }));
}

// ---- The Layout menu (ADR-0061) -----------------------------------------------------------------

/**
 * A Layout menu choice applied (ADR-0061), as Excel's Design tab applies it. The Subtotals choices set
 * every field in Rows and Columns — Automatic, or None — and, for the two that show them, where they stand;
 * the Grand Totals choices set the two switches; the Report Layout choices set the form, or whether the
 * Outline and Tabular forms repeat their outer Items' labels. Everything else in the layout is kept, and a
 * choice that changes nothing returns the layout it was given.
 */
export function chooseLayout(layout: PivotLayout, choice: PivotLayoutChoice): PivotLayout {
    switch (choice) {
        case 'doNotShowSubtotals': return withSubtotals(layout, false);
        case 'showSubtotalsAtBottom': return withSubtotalsAt(withSubtotals(layout, true), false);
        case 'showSubtotalsAtTop': return withSubtotalsAt(withSubtotals(layout, true), true);
        case 'grandTotalsOff': return withGrandTotals(layout, false, false);
        case 'grandTotalsOn': return withGrandTotals(layout, true, true);
        case 'grandTotalsOnRowsOnly': return withGrandTotals(layout, false, true);
        case 'grandTotalsOnColumnsOnly': return withGrandTotals(layout, true, false);
        case 'compactForm': return withForm(layout, 'compact');
        case 'outlineForm': return withForm(layout, 'outline');
        case 'tabularForm': return withForm(layout, 'tabular');
        case 'repeatItemLabels': return layout.repeatItemLabels ? layout : withLayout(layout, { repeatItemLabels: true });
        case 'doNotRepeatItemLabels': return layout.repeatItemLabels ? withLayout(layout, { repeatItemLabels: false }) : layout;
        default: throw new RangeError(`Unknown PivotLayoutChoice (${String(choice satisfies never)}).`);
    }
}

/**
 * Whether a choice is what the report shows now — the Layout menu marks it (ADR-0061). Read from what is
 * painted: in the Tabular form, which puts every subtotal at the bottom, Show at Bottom is the current
 * Subtotals choice whatever the layout's setting; in the Compact form, which has no outer label columns,
 * neither label choice is. With no field in Rows or Columns no Subtotals choice is current, and with fields
 * that disagree, none is.
 */
export function isLayoutChosen(layout: PivotLayout, choice: PivotLayoutChoice): boolean {
    const fields = axisFields(layout);
    const atTop = subtotalsShownAtTop(layout);
    switch (choice) {
        case 'doNotShowSubtotals': return fields.length > 0 && fields.every(p => !p.subtotals);
        case 'showSubtotalsAtBottom': return fields.length > 0 && fields.every(p => p.subtotals) && !atTop;
        case 'showSubtotalsAtTop': return fields.length > 0 && fields.every(p => p.subtotals) && atTop;
        case 'grandTotalsOff': return !layout.grandTotalRow && !layout.grandTotalColumn;
        case 'grandTotalsOn': return layout.grandTotalRow && layout.grandTotalColumn;
        case 'grandTotalsOnRowsOnly': return !layout.grandTotalRow && layout.grandTotalColumn;
        case 'grandTotalsOnColumnsOnly': return layout.grandTotalRow && !layout.grandTotalColumn;
        case 'compactForm': return layout.form === 'compact';
        case 'outlineForm': return layout.form === 'outline';
        case 'tabularForm': return layout.form === 'tabular';
        case 'repeatItemLabels': return layout.form !== 'compact' && layout.repeatItemLabels;
        case 'doNotRepeatItemLabels': return layout.form !== 'compact' && !layout.repeatItemLabels;
        default: throw new RangeError(`Unknown PivotLayoutChoice (${String(choice satisfies never)}).`);
    }
}

/**
 * Whether a choice would change what the report shows (ADR-0061): the Layout menu disables one that would
 * not. The current choice never would. Nor would a Subtotals choice with no field in Rows or Columns, Show
 * at Top in the Tabular form, nor a label choice in the Compact form.
 */
export function layoutChoiceChanges(layout: PivotLayout, choice: PivotLayoutChoice): boolean {
    const fields = axisFields(layout);
    const someOff = fields.some(p => !p.subtotals);
    const atTop = subtotalsShownAtTop(layout);
    switch (choice) {
        case 'doNotShowSubtotals': return fields.some(p => p.subtotals);
        case 'showSubtotalsAtBottom': return fields.length > 0 && (someOff || atTop);
        case 'showSubtotalsAtTop': return fields.length > 0 && layout.form !== 'tabular' && (someOff || !atTop);
        case 'repeatItemLabels': return layout.form !== 'compact' && !layout.repeatItemLabels;
        case 'doNotRepeatItemLabels': return layout.form !== 'compact' && layout.repeatItemLabels;
        case 'grandTotalsOff':
        case 'grandTotalsOn':
        case 'grandTotalsOnRowsOnly':
        case 'grandTotalsOnColumnsOnly':
        case 'compactForm':
        case 'outlineForm':
        case 'tabularForm': return !isLayoutChosen(layout, choice);
        default: throw new RangeError(`Unknown PivotLayoutChoice (${String(choice satisfies never)}).`);
    }
}

function axisFields(layout: PivotLayout): readonly PivotFieldPlacement[] {
    return [...layout.rows, ...layout.columns];
}

// Where subtotals are painted: at the top only outside the Tabular form (ADR-0060).
function subtotalsShownAtTop(layout: PivotLayout): boolean {
    return layout.subtotalsAtTop && layout.form !== 'tabular';
}

function withSubtotals(layout: PivotLayout, subtotals: boolean): PivotLayout {
    if (axisFields(layout).every(p => p.subtotals === subtotals)) return layout;
    const set = (placements: readonly PivotFieldPlacement[]): readonly PivotFieldPlacement[] =>
        Object.freeze(placements.map(p => (p.subtotals === subtotals ? p : withPlacement(p, { subtotals }))));
    return withLayout(layout, { rows: set(layout.rows), columns: set(layout.columns) });
}

function withSubtotalsAt(layout: PivotLayout, atTop: boolean): PivotLayout {
    return layout.subtotalsAtTop === atTop ? layout : withLayout(layout, { subtotalsAtTop: atTop });
}

function withGrandTotals(layout: PivotLayout, row: boolean, column: boolean): PivotLayout {
    return layout.grandTotalRow === row && layout.grandTotalColumn === column
        ? layout
        : withLayout(layout, { grandTotalRow: row, grandTotalColumn: column });
}

function withForm(layout: PivotLayout, form: PivotReportForm): PivotLayout {
    return layout.form === form ? layout : withLayout(layout, { form });
}

// ---- The parts ----------------------------------------------------------------------------------

function clamp(index: number, count: number): number {
    return Math.min(Math.max(index, 0), count);
}

function insert(layout: PivotLayout, area: PivotArea, placement: PivotFieldPlacement, index: number): PivotLayout {
    const list = [...placementsIn(layout, area)];
    list.splice(clamp(index, list.length), 0, placement);
    return replace(layout, area, list);
}

function insertValue(layout: PivotLayout, value: PivotValueField, index: number): PivotLayout {
    const at = clamp(index, layout.values.length);
    const values = [...layout.values];
    values.splice(at, 0, value);
    // Value Fields at and after the insertion moved one along; an order by one follows it.
    return remap(withLayout(layout, { values: Object.freeze(values) }), old => (old >= at ? old + 1 : old));
}

function moveValue(layout: PivotLayout, from: number, index: number): PivotLayout {
    const target = clamp(index, layout.values.length);
    if (target === from || target === from + 1) return layout;
    const values = [...layout.values];
    const [moving] = values.splice(from, 1);
    if (moving === undefined) throw new Error('Internal: the moved Value Field was checked to stand in Values.');
    const to = target > from ? target - 1 : target;
    values.splice(to, 0, moving);
    return remap(withLayout(layout, { values: Object.freeze(values) }), old => {
        if (old === from) return to;
        if (from < to && old > from && old <= to) return old - 1;
        if (to < from && old >= to && old < from) return old + 1;
        return old;
    });
}

function removeValue(layout: PivotLayout, index: number): PivotLayout {
    const values = Object.freeze(layout.values.filter((_, i) => i !== index));
    // An order by the removed Value Field falls back to its label, in the same direction.
    return remap(withLayout(layout, { values }), old => (old === index ? undefined : old > index ? old - 1 : old));
}

function remap(layout: PivotLayout, map: (old: number) => number | undefined): PivotLayout {
    const mapped = (placements: readonly PivotFieldPlacement[]): readonly PivotFieldPlacement[] => Object.freeze(placements.map(p => {
        const old = p.sort.byValue;
        return old === undefined ? p : withPlacement(p, { sort: pivotSort(p.sort.direction, map(old)) });
    }));
    return withLayout(layout, { filters: mapped(layout.filters), rows: mapped(layout.rows), columns: mapped(layout.columns) });
}

function update(layout: PivotLayout, field: string, change: (placement: PivotFieldPlacement) => PivotFieldPlacement): PivotLayout {
    const at = placementOf(layout, field);
    if (at === undefined) throw new TypeError(`'${field}' stands in none of Filters, Rows and Columns.`);
    const placements = [...placementsIn(layout, at.area)];
    const before = elementAt(placements, at.index);
    const changed = change(before);
    if (changed === before) return layout;
    placements[at.index] = changed;
    return replace(layout, at.area, placements);
}

function replace(layout: PivotLayout, area: PivotArea, placements: readonly PivotFieldPlacement[]): PivotLayout {
    const frozen = Object.freeze([...placements]);
    switch (area) {
        case 'filters': return withLayout(layout, { filters: frozen });
        case 'rows': return withLayout(layout, { rows: frozen });
        case 'columns': return withLayout(layout, { columns: frozen });
        case 'values': throw new RangeError('Filters, Rows or Columns.');
        default: throw new RangeError(`Unknown PivotArea (${String(area satisfies never)}).`);
    }
}

function sameList<T>(one: readonly T[], other: readonly T[]): boolean {
    return one.length === other.length && one.every((item, i) => item === other[i]);
}

function same(one: PivotLayout, other: PivotLayout): boolean {
    return sameList(one.filters, other.filters) && sameList(one.rows, other.rows)
        && sameList(one.columns, other.columns) && sameList(one.values, other.values);
}

function checkArea(area: PivotArea): void {
    if (area !== 'filters' && area !== 'rows' && area !== 'columns') throw new RangeError(`Filters, Rows or Columns, not ${area}.`);
}

function checkIndex(count: number, index: number): void {
    if (!Number.isInteger(index) || index < 0 || index >= count) throw new RangeError(`The Area holds ${count} entries, and ${index} is none of them.`);
}
