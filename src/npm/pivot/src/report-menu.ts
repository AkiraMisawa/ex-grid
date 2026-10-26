// The report's own commands in the grid's Context Menu (ADR-0036, ADR-0059): the ones the Razor component's
// ContextCommandsFor builds, in its order and under its ids, after the grid's clipboard commands. Each is enabled from
// the report the menu was opened over, and runs against the pane's layout when it is chosen: where the pane no longer
// holds what it was for, it changes nothing (spine rule 6).

import type { GridCommand } from '@ex-grid/core';
import {
    isItemCollapsed, placementOf, removeEntry, setCollapsed, setFieldCollapsed, setSort, sortsEqual, valueFieldsEqual,
    type PivotDisplayRow, type PivotFieldItem, type PivotFieldPlacement, type PivotLayout, type PivotReportMetadata, type PivotSort,
} from '@ex-grid/pivot-engine';
import { defined, elementAt } from './must';

/** What the menu was opened over. */
export interface ReportMenuModel {
    readonly metadata: PivotReportMetadata;
    /** The row it was opened on, which the report on screen holds. */
    readonly row: PivotDisplayRow;
    /** The name of the column it was opened on. */
    readonly column: string;
    /** The pane's layout now: Value Field Settings… opens in the pane, under the entry it shows. */
    readonly pane: PivotLayout;
    readonly fieldListShown: boolean;
    /** A field's caption. */
    readonly captionOf: (field: string) => string;
}

/** What the commands do. */
export interface ReportMenuActions {
    /** A change of the pane's layout, made from the pane's layout when chosen; `undefined` changes nothing. An Item
     * expanded or collapsed is named, and keeps the Focus in the report the change brings. */
    readonly edit: (edit: (pane: PivotLayout) => PivotLayout | undefined, toggled?: PivotFieldItem) => void;
    /** Show Details on the row's value cell in `valueColumn`. */
    readonly showDetails: (valueColumn: number) => void;
    /** Value Field Settings… for the Value Field at `valueField`, in the pane, while `holds` says the pane holds the
     * one the cell showed there. */
    readonly valueFieldSettings: (valueField: number, holds: (pane: PivotLayout) => boolean) => void;
    readonly setFieldListShown: (shown: boolean) => void;
}

/** The id of "Remove <field>", which carries the field's caption after it. */
export const removeNamedFieldId = 'remove-named-field';

/** The caption "Remove <field>" names, from its command's id; `undefined` for any other id. */
export function captionOfRemove(id: string): string | undefined {
    const prefix = `${removeNamedFieldId}:`;
    return id.startsWith(prefix) ? id.slice(prefix.length) : undefined;
}

// The outer Item a row's Expand and Collapse act on: the innermost of its row Items that has Items under it.
function collapsibleItemOf(metadata: PivotReportMetadata, row: PivotDisplayRow): PivotFieldItem | undefined {
    const path = row.rowPath;
    for (let level = path.length - 1; level >= 0; level--) {
        if (level < metadata.layout.rows.length - 1) return elementAt(path, level);
    }
    return undefined;
}

function innermostFieldOf(row: PivotDisplayRow): string | undefined {
    return row.rowPath.at(-1)?.field;
}

// The Value Field a value cell shows: its row's, its column's, or the one Value Field.
export function valueFieldAt(metadata: PivotReportMetadata, row: PivotDisplayRow, valueColumn: number): number {
    if (row.valueField >= 0) return row.valueField;
    const column = elementAt(metadata.valueColumns, valueColumn);
    if (column.valueField >= 0) return column.valueField;
    return metadata.layout.values.length === 1 ? 0 : -1;
}

function inRows(layout: PivotLayout, field: string): boolean {
    return placementOf(layout, field)?.area === 'rows';
}

/** The report's commands for the cell the menu was opened on. Which Items and fields they stand for is read from the
 * report on screen; whether each would change something, from the pane's layout, which it is run against. */
export function reportMenuCommands(model: ReportMenuModel, actions: ReportMenuActions): readonly GridCommand[] {
    const { metadata, row, column, pane, captionOf } = model;
    const labelColumn = metadata.labelColumns.findIndex(label => label.name === column);
    const valueColumn = labelColumn >= 0 ? -1 : metadata.valueColumns.findIndex(value => value.name === column);
    const commands: GridCommand[] = [];
    const add = (id: string, enabled: boolean, invoke: () => void): void => { commands.push(Object.freeze({ id, enabled, invoke })); };
    // A change of a field in Rows, made while the pane still holds it there.
    const editRows = (field: string, edit: (pane: PivotLayout) => PivotLayout | undefined, toggled?: PivotFieldItem): () => void =>
        () => { actions.edit(now => (inRows(now, field) ? edit(now) : undefined), toggled); };
    const inPane = (field: string): PivotFieldPlacement | undefined => pane.rows.find(placement => placement.field === field);

    const item = collapsibleItemOf(metadata, row);
    if (item !== undefined) {
        const placement = inPane(item.field);
        const collapsed = placement !== undefined && isItemCollapsed(placement, item.item);
        const toggled = placement !== undefined && placement.toggledItems.length > 0;
        add('expand', collapsed, editRows(item.field, now => setCollapsed(now, item.field, item.item, false), item));
        add('collapse', placement !== undefined && !collapsed, editRows(item.field, now => setCollapsed(now, item.field, item.item, true), item));
        add('expand-field', placement !== undefined && (placement.collapsed || toggled), editRows(item.field, now => setFieldCollapsed(now, item.field, false)));
        add('collapse-field', placement !== undefined && (!placement.collapsed || toggled), editRows(item.field, now => setFieldCollapsed(now, item.field, true)));
    }

    const field = labelColumn >= 0
        ? elementAt(metadata.labelColumns, labelColumn).field ?? (row.valueField < 0 ? innermostFieldOf(row) : undefined)
        : innermostFieldOf(row);
    // A sort by a Value Field is by the one the cell shows, while the pane holds it at the same place.
    const sortBy = (sortField: string, sort: PivotSort, holds: (now: PivotLayout) => boolean): void => {
        const placement = inPane(sortField);
        add(sortCommandId(sort), placement !== undefined && holds(pane) && !sortsEqual(placement.sort, sort),
            editRows(sortField, now => (holds(now) ? setSort(now, sortField, sort) : undefined)));
    };
    if (labelColumn >= 0 && field !== undefined) {
        sortBy(field, { direction: 'ascending' }, () => true);
        sortBy(field, { direction: 'descending' }, () => true);
    }
    if (valueColumn >= 0) {
        const valueField = valueFieldAt(metadata, row, valueColumn);
        const shown = metadata.layout.values[valueField];
        const holds = (now: PivotLayout): boolean => {
            const at = now.values[valueField];
            return shown !== undefined && at !== undefined && valueFieldsEqual(at, shown);
        };
        if (field !== undefined && shown !== undefined) {
            sortBy(field, { direction: 'ascending', byValue: valueField }, holds);
            sortBy(field, { direction: 'descending', byValue: valueField }, holds);
        }
        // Show Details is always offered: the tab, the dialog or the Consumer takes the records (ADR-0059). An empty
        // cell has none to show.
        add('show-details', row.values[valueColumn] !== undefined, () => { actions.showDetails(valueColumn); });
        // The panel opens in the pane, under the Value Field's entry: offered while the pane shows that Value Field
        // where the report does.
        if (shown !== undefined) add('value-field-settings', holds(pane), () => { actions.valueFieldSettings(valueField, holds); });
    }
    if (field !== undefined) {
        add(`${removeNamedFieldId}:${captionOf(field)}`, inPane(field) !== undefined, editRows(field, now => removeEntry(now, defined(placementOf(now, field), 'the field in Rows'))));
    }
    if (model.fieldListShown) add('hide-field-list', true, () => { actions.setFieldListShown(false); });
    else add('show-field-list', true, () => { actions.setFieldListShown(true); });
    return Object.freeze(commands);
}

function sortCommandId(sort: PivotSort): string {
    if (sort.byValue === undefined) return sort.direction === 'ascending' ? 'sort-ascending' : 'sort-descending';
    return sort.direction === 'ascending' ? 'sort-smallest-to-largest' : 'sort-largest-to-smallest';
}

