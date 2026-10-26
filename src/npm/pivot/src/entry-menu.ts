// A placed field's menu (ADR-0061): the commands the Razor component's ExPivot.MenuFor builds, in its order and
// under its ids, each enabled only where it would change something. Each says what it does as the next layout,
// taken against the layout the menu was drawn from, as a gesture in the Field List does (spine rule 6), and where
// the entry it was opened on stands in that layout, which the keyboard goes back to.
//
// Field Settings… and Value Field Settings… open a panel in the menu's place. Filter… is not offered yet.

import {
    isValuesPseudoField, moveEntry, placementOf, placementsIn, removeEntry, setFieldCollapsed, setSort, valuesPseudoField, type PivotArea,
    type PivotEntry, type PivotField, type PivotLayout,
} from '@ex-grid/pivot-engine';
import { fieldOf, type FieldListEdit } from './field-list';
import type { PivotMenuCommand } from './menu';
import { defined, elementAt } from './must';
import type { PivotScreenWords } from './words';

/** What a field's menu is drawn from. */
export interface EntryMenuModel {
    /** The pane's layout, which the menu's commands are taken against. */
    readonly layout: PivotLayout;
    /** The fields the source offers. */
    readonly fields: readonly PivotField[];
    /** The words. */
    readonly words: PivotScreenWords;
}

/** A command's change: the layout it makes, and where the entry its menu was opened on stands in that layout, or
 * `undefined` when its field left the Areas. */
export interface EntryMenuEdit extends FieldListEdit {
    readonly entry: PivotEntry | undefined;
}

/** A panel a field's menu opens. */
export type EntryPanel = 'field-settings' | 'value-field-settings';

/** One command of a field's menu: what it is, and the change it makes when pressed, or the panel it opens. */
export interface EntryMenuCommand extends Omit<PivotMenuCommand, 'invoke'> {
    readonly does: { readonly edit: () => EntryMenuEdit } | { readonly panel: EntryPanel };
}

/** Where `field` stands in `layout`, for the keyboard to go back to: at `near`, the entry a menu was opened on, if it
 * still stands for the field, else in Values beside it, else in the axis or Filters Area it is placed in, else in
 * Values; Σ Values (no field) on its axis while two Value Fields hold it there. `undefined` once it left the Areas. */
export function entryOfField(layout: PivotLayout, field: string | undefined, near: PivotEntry): PivotEntry | undefined {
    if (field === undefined) return layout.values.length >= 2 ? valuesPseudoField(layout.valuesAxis) : undefined;
    const inValues = layout.values.findIndex(value => value.field === field);
    if (near.area === 'values' && inValues >= 0) return layout.values[near.index]?.field === field ? near : { area: 'values', index: inValues };
    return placementOf(layout, field) ?? (inValues >= 0 ? { area: 'values', index: inValues } : undefined);
}

// Past an Area's last entry: a move there puts the entry last.
const end = Number.MAX_SAFE_INTEGER;

/** The commands of the menu of `entry`. */
export function entryMenuCommands(model: EntryMenuModel, entry: PivotEntry): readonly EntryMenuCommand[] {
    const { layout, fields, words } = model;
    const commands: EntryMenuCommand[] = [];
    const add = (id: string, enabled: boolean, edit: () => EntryMenuEdit): void => {
        commands.push(Object.freeze({ id, label: words.word(id), enabled, does: Object.freeze({ edit }) }));
    };
    const opens = (panel: EntryPanel): void => {
        commands.push(Object.freeze({ id: panel, label: words.word(panel), enabled: true, does: Object.freeze({ panel }) }));
    };
    const edit = (next: PivotLayout, at: PivotEntry | undefined, moved?: { readonly field: string; readonly area: PivotArea }): EntryMenuEdit =>
        ({ layout: next, against: layout, moved, entry: at });

    if (isValuesPseudoField(entry)) {
        // Σ Values moves between Rows and Columns only (ADR-0060).
        const onRows = layout.valuesAxis === 'rows';
        add('move-to-rows', !onRows, () => edit(moveEntry(layout, entry, 'rows', end, undefined), valuesPseudoField('rows')));
        add('move-to-columns', onRows, () => edit(moveEntry(layout, entry, 'columns', end, undefined), valuesPseudoField('columns')));
        return Object.freeze(commands);
    }

    const name = fieldOf(layout, entry);
    const field = defined(fields.find(f => f.name === name), `the field '${name}'`);
    const count = entry.area === 'values' ? layout.values.length : placementsIn(layout, entry.area).length;
    // Within its Area the entry lands where it was put; moved to another, it stands last there, where a field already
    // in that Area is moved to (moveEntry).
    const within = (index: number, at: number): EntryMenuEdit =>
        edit(moveEntry(layout, entry, entry.area, index, field), { area: entry.area, index: at }, { field: name, area: entry.area });
    const to = (area: PivotArea): EntryMenuEdit => {
        const next = moveEntry(layout, entry, area, end, field);
        const index = area === 'values' ? next.values.length - 1 : placementsIn(next, area).findIndex(placement => placement.field === name);
        return edit(next, { area, index }, { field: name, area });
    };
    add('move-up', entry.index > 0, () => within(entry.index - 1, entry.index - 1));
    add('move-down', entry.index < count - 1, () => within(entry.index + 2, entry.index + 1));
    add('move-to-beginning', entry.index > 0, () => within(0, 0));
    add('move-to-end', entry.index < count - 1, () => within(count, count - 1));
    add('move-to-filters', entry.area !== 'filters', () => to('filters'));
    add('move-to-rows', entry.area !== 'rows', () => to('rows'));
    add('move-to-columns', entry.area !== 'columns', () => to('columns'));
    add('move-to-values', entry.area !== 'values', () => to('values'));
    // Removed from one Area, a field may still stand in another: Values beside Rows.
    add('remove-field', true, () => {
        const next = removeEntry(layout, entry);
        return edit(next, entryOfField(next, name, entry));
    });
    if (entry.area === 'values') opens('value-field-settings');
    if (entry.area !== 'rows' && entry.area !== 'columns') return Object.freeze(commands);

    const placement = elementAt(placementsIn(layout, entry.area), entry.index);
    const bySort = (direction: 'ascending' | 'descending'): boolean => placement.sort.direction === direction && placement.sort.byValue === undefined;
    add('sort-ascending', !bySort('ascending'), () => edit(setSort(layout, name, { direction: 'ascending' }), entry));
    add('sort-descending', !bySort('descending'), () => edit(setSort(layout, name, { direction: 'descending' }), entry));
    // The innermost field has nothing under its Items to collapse (ADR-0060).
    const outer = entry.index < count - 1;
    const toggled = placement.toggledItems.length > 0;
    add('expand-field', outer && (placement.collapsed || toggled), () => edit(setFieldCollapsed(layout, name, false), entry));
    add('collapse-field', outer && (!placement.collapsed || toggled), () => edit(setFieldCollapsed(layout, name, true), entry));
    opens('field-settings');
    return Object.freeze(commands);
}
