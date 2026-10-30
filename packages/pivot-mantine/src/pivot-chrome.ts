// ExPivot's Chrome under Mantine (ADR-0061, ADR-0390): Mantine's components in each of ExPivot for React's slots —
// the Field List, the Pivot Toolbar and its report filters, the menus and panels that open from them, the Stale
// Report's notice and Show Details' tabs and dialog — at the size the pivot's density maps onto. What a part shows,
// what a command or a drop does, what OK applies and why it is refused, where a frame stands and what closes it are
// the pivot's; these components draw what it hands them and call back. The frames are the pivot's, so Mantine's own
// Menu, Popover, Modal and Combobox, which position and dismiss themselves, are not used; a select is Mantine's
// NativeSelect, whose list is the browser's and opens no popup of the page's own.

import { createElement, useContext, useId, type DragEvent, type KeyboardEvent, type ReactElement, type ReactNode } from 'react';
import { Alert, Button, Checkbox, CloseButton, Loader, NativeSelect, Paper, Radio, Tabs, Text, TextInput, type MantineSize } from '@mantine/core';
import { MantineSizeContext } from '@ex-grid/mantine';
import type {
    PivotDetailsDialogSlotContext, PivotDetailsTabsSlotContext, PivotFieldListSlotContext, PivotFieldSettingsSlotContext, PivotItemFilterSlotContext,
    PivotMenuSlotContext, PivotPanelSlotContext, PivotReportFiltersSlotContext, PivotStaleReportSlotContext, PivotToolbarSlotContext,
    PivotValueFieldSettingsSlotContext,
} from '@ex-grid/pivot';
import { usePivotMenu, usePivotPanel, usePivotPart, type ExPivotChrome } from '@ex-grid/pivot-react';

/** A press the pivot takes in its Input Order, as its own buttons claim theirs (ADR-0240). */
const claimsClick = { 'data-ex-claims': 'click' } as const;
/** A field typed in claims nothing: its keys are the field's (ADR-0240). */
const claimsNothing = { 'data-ex-claims': '' } as const;

/** One of the Field List's four areas, as the pivot names it. */
type PivotArea = Parameters<PivotFieldListSlotContext['area']>[0];

/** A menu, a placed field's or the Layout menu: one of Mantine's subtle buttons per command, as its Menu draws an
 * item, a choice marked while it is the current one, and a group's heading where one starts. */
function MantinePivotMenu({ context }: { readonly context: PivotMenuSlotContext }): ReactElement {
    const { view, item } = usePivotMenu(context);
    const size = useContext(MantineSizeContext);
    return createElement('div', { role: 'none', className: 'mantine-ex-pivot-menu' }, ...view.commands.flatMap((command, index) => [
        command.heading === undefined ? null : createElement(Text<'div'>, {
            key: `${command.id}-heading`, size, c: 'dimmed', fw: 600, className: 'mantine-ex-pivot-menu-heading', 'aria-hidden': true,
        }, command.heading),
        createElement(Button<'button'>, {
            ...item(index), key: command.id, type: 'button', variant: 'subtle', color: 'gray', size, fullWidth: true, justify: 'flex-start',
            className: 'mantine-ex-pivot-menu-item',
            // The mark is the stylesheet's, read off aria-checked, so the command's words stay the pivot's.
            leftSection: command.checked === undefined ? undefined : createElement('span', { className: 'mantine-ex-pivot-menu-mark', 'aria-hidden': true }),
        }, command.label),
    ]));
}

/** OK before Cancel, as Excel orders them; Cancel is never disabled. */
function panelActions(context: PivotPanelSlotContext, size: MantineSize, canApply = true): ReactElement {
    return createElement('div', { className: 'mantine-ex-pivot-actions' },
        createElement(Button<'button'>, { type: 'button', size, disabled: !canApply, onClick: context.apply, ...claimsClick }, context.label('ok')),
        createElement(Button<'button'>, { type: 'button', size, variant: 'default', onClick: context.cancel, ...claimsClick }, context.label('cancel')));
}

/** The choice an option names: one the pivot offered, never one a changed option claims. */
function offered<T extends string>(value: string, choices: readonly { readonly value: T }[], what: string): T {
    const found = choices.find(choice => choice.value === value);
    if (found === undefined) throw new RangeError(`'${value}' is not ${what} the panel offers.`);
    return found.value;
}

/** Field Settings…: the subtotals' choice, which takes the keyboard, and the field's order. */
function MantineFieldSettings({ context }: { readonly context: PivotFieldSettingsSlotContext }): ReactElement {
    const { view, first } = usePivotPanel(context);
    const size = useContext(MantineSizeContext);
    // One group, so the arrows move between the two choices, named apart from every other panel's.
    const subtotals = useId();
    return createElement('div', { className: 'mantine-ex-pivot-panel' },
        createElement(Text<'div'>, { size, fw: 600 }, view.caption),
        createElement(Radio, {
            size, ref: first, name: subtotals, label: context.label('subtotals-automatic'), checked: view.subtotals, onChange: () => { context.setSubtotals(true); }, ...claimsNothing,
        }),
        createElement(Radio, {
            size, name: subtotals, label: context.label('subtotals-none'), checked: !view.subtotals, onChange: () => { context.setSubtotals(false); }, ...claimsNothing,
        }),
        createElement(NativeSelect, {
            size, label: context.label('sort-order'), value: String(view.sort), ...claimsNothing,
            data: view.sorts.map((sort, index) => ({ value: String(index), label: sort })),
            onChange: event => { context.setSort(Number(event.currentTarget.value)); },
        }),
        panelActions(context, size));
}

/** Value Field Settings…: the caption, which takes the keyboard, the Aggregation, Show Values As and the number
 * format, its sample, and the pivot's refusal. */
function MantineValueFieldSettings({ context }: { readonly context: PivotValueFieldSettingsSlotContext }): ReactElement {
    const { view, first } = usePivotPanel(context);
    const size = useContext(MantineSizeContext);
    return createElement('div', { className: 'mantine-ex-pivot-panel' },
        createElement(Text<'div'>, { size, c: 'dimmed' }, view.sourceCaption),
        createElement(TextInput, {
            size, ref: first, label: context.label('custom-name'), value: view.caption, ...claimsNothing,
            onChange: event => { context.setCaption(event.currentTarget.value); },
        }),
        createElement(NativeSelect, {
            size, label: context.label('summarize-by'), value: view.aggregation, ...claimsNothing,
            data: view.aggregations.map(choice => ({ value: choice.value, label: choice.label, disabled: !choice.enabled })),
            onChange: event => { context.setAggregation(offered(event.currentTarget.value, view.aggregations, 'an Aggregation')); },
        }),
        createElement(NativeSelect, {
            size, label: context.label('show-values-as'), value: view.showValuesAs, ...claimsNothing,
            data: view.showValuesAsChoices.map(choice => ({ value: choice.value, label: choice.label })),
            onChange: event => { context.setShowValuesAs(offered(event.currentTarget.value, view.showValuesAsChoices, 'a Show Values As')); },
        }),
        createElement(TextInput, {
            size, label: context.label('number-format'), value: view.numberFormat, placeholder: view.numberFormatPlaceholder, ...claimsNothing,
            onChange: event => { context.setNumberFormat(event.currentTarget.value); },
        }),
        createElement(Text<'div'>, { size, className: 'mantine-ex-pivot-sample' }, view.sample),
        view.refusal === undefined ? null : createElement(Alert, { color: 'red', variant: 'light', p: 'xs', role: 'alert' }, view.refusal),
        panelActions(context, size));
}

/** Filter…: the search, which takes the keyboard, (Select All) and each Item ticked while shown, why they cannot be
 * listed or that they are on their way, the note under them, and the pivot's refusal. */
function MantineItemFilter({ context }: { readonly context: PivotItemFilterSlotContext }): ReactElement {
    const { view, first } = usePivotPanel(context);
    const size = useContext(MantineSizeContext);
    const list = view.unavailable !== undefined
        ? createElement(Alert, { color: 'yellow', variant: 'light', p: 'xs', role: 'alert' }, view.unavailable)
        : view.loading
            ? createElement('div', { className: 'mantine-ex-pivot-loading' }, createElement(Loader, { size, type: 'dots' }), createElement(Text<'span'>, { size }, context.label('loading')))
            : createElement('div', { role: 'group', 'aria-label': view.caption, 'aria-busy': view.updating, className: 'mantine-ex-pivot-items' },
                view.items.length === 0 ? null : createElement(Checkbox, {
                    size, label: context.label('select-all'), checked: view.allTicked === true, indeterminate: view.allTicked === undefined, ...claimsNothing,
                    onChange: event => { context.setAll(event.currentTarget.checked); },
                }),
                // Keyed by position: the pivot's list is the order the Items stand in.
                ...view.items.map((item, at) => createElement(Checkbox, {
                    key: at, size, label: item.label, checked: item.ticked, ...claimsNothing,
                    onChange: event => { context.setTicked(item.key, event.currentTarget.checked); },
                })),
                view.note === undefined ? null : createElement(Text<'div'>, { size, c: 'dimmed' }, view.note));
    return createElement('div', { className: 'mantine-ex-pivot-panel' },
        createElement(TextInput, {
            size, ref: first, type: 'search', 'aria-label': context.label('search-items'), placeholder: context.label('search-items'), value: view.search,
            ...claimsNothing, onChange: event => { context.setSearch(event.currentTarget.value); },
        }),
        list,
        view.refusal === undefined ? null : createElement(Alert, { color: 'red', variant: 'light', p: 'xs', role: 'alert' }, view.refusal),
        panelActions(context, size, view.canApply));
}

/** The Pivot Toolbar: the report filter band's anchor on its left; Layout ▾ in the anchor its menu opens in, Refresh
 * where the source can be refreshed, and the Field List's toggle on its right; and the refusal of the user's last
 * change beneath. */
function MantinePivotToolbar({ context }: { readonly context: PivotToolbarSlotContext }): ReactElement {
    const view = usePivotPart(context);
    const size = useContext(MantineSizeContext);
    return createElement('div', { className: 'mantine-ex-pivot-toolbar-frame' },
        createElement('div', { role: 'toolbar', className: 'mantine-ex-pivot-toolbar' },
            createElement('div', { ref: context.anchor('report-filters'), className: 'mantine-ex-pivot-toolbar-start' }),
            createElement('div', { className: 'mantine-ex-pivot-toolbar-end' },
                createElement('span', { ref: context.anchor('layout'), className: 'mantine-ex-pivot-anchor' }, createElement(Button<'button'>, {
                    ref: context.control('layout'), type: 'button', size, variant: view.layoutMenu.open ? 'light' : 'subtle',
                    'aria-haspopup': 'menu', 'aria-expanded': view.layoutMenu.open, onClick: context.toggleLayoutMenu, ...claimsClick,
                }, `${view.layoutMenu.label} ▾`)),
                view.refresh === undefined ? null : createElement(Button<'button'>, {
                    ref: context.control('refresh'), type: 'button', size, variant: 'subtle', onClick: context.refresh, ...claimsClick,
                }, view.refresh.label),
                createElement(Button<'button'>, {
                    ref: context.control('field-list'), type: 'button', size, variant: view.fieldList.shown ? 'light' : 'subtle',
                    'aria-pressed': view.fieldList.shown, onClick: context.toggleFieldList, ...claimsClick,
                }, view.fieldList.label))),
        view.refusal === undefined ? null : createElement(Alert, {
            color: 'red', variant: 'light', p: 'xs', role: 'alert', className: 'mantine-ex-pivot-refusal',
        }, view.refusal));
}

/** The report filter band: each field in Filters, its caption, and the button that says what it shows and opens
 * Filter… in the anchor around it. */
function MantineReportFilters({ context }: { readonly context: PivotReportFiltersSlotContext }): ReactElement {
    const view = usePivotPart(context);
    const size = useContext(MantineSizeContext);
    return createElement('div', { className: 'mantine-ex-pivot-filters' }, ...view.filters.map(filter => createElement('div', {
        key: filter.field, className: 'mantine-ex-pivot-filter',
    },
    createElement(Text<'span'>, { size, fw: 600, className: 'mantine-ex-pivot-filter-caption' }, filter.caption),
    createElement('span', { ref: context.anchor(filter.field), className: 'mantine-ex-pivot-anchor mantine-ex-pivot-filter-anchor' }, createElement(Button<'button'>, {
        ref: context.control(filter.field), type: 'button', size, variant: filter.filtered ? 'light' : 'default', fullWidth: true, justify: 'space-between',
        rightSection: createElement('span', { 'aria-hidden': true }, '▾'),
        'aria-label': filter.buttonLabel, 'aria-haspopup': 'dialog', 'aria-expanded': filter.open, onClick: () => { context.toggle(filter.field); }, ...claimsClick,
    }, filter.summary)))));
}

/** The Stale Report's notice, in Mantine's alert, inside the pivot's live region: the sentence, and Retry. */
function MantineStaleReport({ context }: { readonly context: PivotStaleReportSlotContext }): ReactElement {
    const view = usePivotPart(context);
    const size = useContext(MantineSizeContext);
    return createElement(Alert, { color: 'yellow', variant: 'light', p: 'xs', className: 'mantine-ex-pivot-stale' },
        createElement('div', { className: 'mantine-ex-pivot-stale-body' },
            createElement(Text<'span'>, { size }, view.message),
            createElement(Button<'button'>, {
                ref: context.control('retry'), type: 'button', size, variant: 'default', disabled: !view.retry.enabled, onClick: context.retry, ...claimsClick,
            }, view.retry.label)));
}

/** Where a drag may land in the Field List: an Area's end, or before an entry. Each hears the drag's events, the
 * innermost first, so an entry's place outranks its Area's end. */
function dropTarget(context: PivotFieldListSlotContext, area: PivotArea, index: number): Record<string, (event: DragEvent<HTMLElement>) => void> {
    return {
        onDragOver: event => { context.dragOver(event.nativeEvent); },
        onDragEnter: event => { event.stopPropagation(); context.dragEnter(area, index); },
        onDrop: event => { event.stopPropagation(); context.drop(event.nativeEvent, area, index); },
    };
}

/** The Field List, Excel's PivotTable Fields pane, in Mantine's controls: the search and the fields with their tick
 * boxes, scrolling on their own; the four Areas, two by two, always in view at the foot; then Defer Layout Update and
 * Update. Drag and drop is the browser's own, its every event handed to the pivot. */
function MantineFieldList({ context }: { readonly context: PivotFieldListSlotContext }): ReactElement {
    const view = usePivotPart(context);
    const size = useContext(MantineSizeContext);
    const fields = view.fields.map(field => createElement('div', {
        key: field.name, role: 'listitem', draggable: true,
        className: field.dragging ? 'mantine-ex-pivot-field mantine-ex-pivot-dragging' : 'mantine-ex-pivot-field',
        onDragStart: (event: DragEvent<HTMLElement>) => { context.startDrag(event.nativeEvent, { field: field.name }); },
        onDragEnd: context.endDrag,
    },
    createElement(Checkbox, {
        ref: context.control(field.key), size, label: field.caption, checked: field.placed, className: 'mantine-ex-pivot-field-check', ...claimsClick,
        onChange: () => { context.toggle(field.name); },
    }),
    field.filtered ? createElement(Text<'span'>, { size, c: 'dimmed', 'aria-hidden': true }, '⏷') : null,
    field.count === undefined ? null : createElement(Text<'span'>, { size, c: 'dimmed', title: field.countLabel, 'aria-label': field.countLabel }, field.count)));
    const areas = view.areas.map(area => createElement(Paper<'div'>, {
        key: area.area, ref: context.area(area.area), withBorder: true, ...dropTarget(context, area.area, area.end),
        className: area.dropTarget ? 'mantine-ex-pivot-area mantine-ex-pivot-drop-target' : 'mantine-ex-pivot-area',
    },
    createElement(Text<'div'>, { size, fw: 600, className: 'mantine-ex-pivot-area-title' }, area.title),
    area.overCap === undefined ? null : createElement(Text<'div'>, { size, c: 'red', role: 'status' }, area.overCap),
    createElement('div', { role: 'list', 'aria-label': area.title, className: 'mantine-ex-pivot-entries' }, ...area.entries.map(entry => {
        const classes = ['mantine-ex-pivot-entry'];
        if (entry.dragging) classes.push('mantine-ex-pivot-dragging');
        if (entry.dropBefore) classes.push('mantine-ex-pivot-drop-before');
        // The anchor the entry's menu and panels open in: no position of its own, so their frame takes the pane's width.
        return createElement('div', {
            key: entry.key, role: 'listitem', ref: context.anchor(entry.key), draggable: !entry.menuOpen, className: classes.join(' '),
            ...dropTarget(context, area.area, entry.dropIndex),
            onDragStart: (event: DragEvent<HTMLElement>) => { context.startDrag(event.nativeEvent, { entry: entry.entry }); },
            onDragEnd: context.endDrag,
        }, createElement(Button<'button'>, {
            ref: context.control(entry.key), type: 'button', size, variant: entry.filtered ? 'light' : 'default', fullWidth: true, justify: 'space-between',
            rightSection: createElement('span', { 'aria-hidden': true }, '▾'),
            'aria-label': entry.menuLabel, 'aria-haspopup': 'menu', 'aria-expanded': entry.menuOpen, onClick: () => { context.openMenu(entry.entry); }, ...claimsClick,
        }, entry.caption));
    }))));
    return createElement('div', { className: 'mantine-ex-pivot-pane' },
        createElement('div', { className: 'mantine-ex-pivot-pane-heading' },
            createElement(Text<'span'>, { size, fw: 600, className: 'mantine-ex-pivot-pane-title' }, view.title),
            createElement(CloseButton<'button'>, { size, 'aria-label': view.hideLabel, title: view.hideLabel, onClick: context.close, ...claimsClick })),
        createElement('div', { className: 'mantine-ex-pivot-pane-body' },
            createElement(Text<'div'>, { size, c: 'dimmed' }, view.chooseCaption),
            createElement(TextInput, {
                ref: context.control('search'), size, type: 'search', 'aria-label': view.searchLabel, placeholder: view.searchLabel, value: view.search,
                ...claimsNothing, onChange: event => { context.setSearch(event.currentTarget.value); },
            }),
            createElement('div', {
                role: 'list', 'aria-label': view.title,
                className: view.dropRemoves ? 'mantine-ex-pivot-fields mantine-ex-pivot-drop-remove' : 'mantine-ex-pivot-fields',
                onDragOver: (event: DragEvent<HTMLElement>) => { context.dragOver(event.nativeEvent); },
                onDrop: (event: DragEvent<HTMLElement>) => { context.dropOnList(event.nativeEvent); },
            }, ...fields),
            createElement(Text<'div'>, { size, c: 'dimmed' }, view.dragCaption),
            createElement('div', { className: 'mantine-ex-pivot-areas' }, ...areas)),
        createElement('div', { className: 'mantine-ex-pivot-defer' },
            createElement(Checkbox, {
                size, label: view.defer.label, checked: view.defer.ticked, ...claimsClick,
                onChange: event => { context.setDeferred(event.currentTarget.checked); },
            }),
            createElement(Button<'button'>, { type: 'button', size, variant: 'default', disabled: !view.update.enabled, onClick: context.update, ...claimsClick }, view.update.label)));
}

/** Show Details' sheet tabs at the report's foot, as Mantine's tabs: the report's own first, then each sheet's with
 * its close button. Which sheet is selected, and what closing one selects, are the pivot's. */
function MantineDetailsTabs({ context }: { readonly context: PivotDetailsTabsSlotContext }): ReactElement {
    const view = usePivotPart(context);
    const size = useContext(MantineSizeContext);
    const selected = view.tabs.find(tab => tab.selected);
    return createElement(Tabs, {
        // oxlint-disable-next-line ex-grid/no-stand-in-default -- Mantine's Tabs say "no tab" as null; the pivot always selects one
        value: selected?.key ?? null, className: 'mantine-ex-pivot-tabs', inverted: true,
        onChange: (key: string | null) => { if (key !== null) context.select(key); },
    }, createElement(Tabs.List, { 'aria-label': view.name, children: view.tabs.map(tab => createElement(Tabs.Tab, {
        key: tab.key, value: tab.key, ref: context.control(tab.key), size, ...claimsClick,
        ...(tab.panelId === undefined ? {} : { 'aria-controls': tab.panelId }),
        // Delete closes the sheet the keyboard is on, as the close mark does under the pointer: the mark is a span
        // inside the tab's button, which takes no keyboard of its own.
        ...(tab.closeLabel === undefined ? {} : {
            'aria-keyshortcuts': 'Delete',
            onKeyDown: (event: KeyboardEvent<HTMLButtonElement>) => { if (event.key === 'Delete') { event.preventDefault(); context.close(tab.key); } },
        }),
        // A span, not a button: a button inside the tab's button is not one the browser lets stand.
        rightSection: tab.closeLabel === undefined ? undefined : createElement(CloseButton<'span'>, {
            component: 'span', size: 'xs', role: 'button', 'aria-label': tab.closeLabel, title: tab.closeLabel, ...claimsClick,
            onClick: event => { event.stopPropagation(); context.close(tab.key); },
        }),
    }, tab.title)) }));
}

/** What the Show Details dialog draws inside the pivot's frame: its title, the records where they stand, and Close. */
function MantineDetailsDialog({ context }: { readonly context: PivotDetailsDialogSlotContext }): ReactElement {
    const view = usePivotPart(context);
    const size = useContext(MantineSizeContext);
    return createElement('div', { className: 'mantine-ex-pivot-dialog' },
        createElement(Text<'div'>, { size: 'md', fw: 600 }, view.title),
        createElement('div', { ref: context.anchor('records'), className: 'mantine-ex-pivot-dialog-records' }),
        createElement('div', { className: 'mantine-ex-pivot-actions' },
            createElement(Button<'button'>, { ref: context.control('close'), type: 'button', size, onClick: context.close, ...claimsClick }, view.closeLabel)));
}

const menu = (context: PivotMenuSlotContext): ReactNode => createElement(MantinePivotMenu, { context });

/** ExPivot's Chrome drawn in Mantine's components, but for the report's grid: what {@link MantinePivot} hands its
 * pivot, with `@ex-grid/mantine`'s `mantineChrome` as `grid`. A Consumer that frames an ExPivot itself spreads both into
 * its `chrome`, inside an element of the classes `mantine-ex-grid mantine-ex-pivot` and a MantineSizeContext, as
 * MantinePivot draws them. */
export const mantinePivotChrome: Omit<ExPivotChrome, 'grid'> = Object.freeze({
    menu,
    itemFilter: (context: PivotItemFilterSlotContext) => createElement(MantineItemFilter, { context }),
    fieldSettings: (context: PivotFieldSettingsSlotContext) => createElement(MantineFieldSettings, { context }),
    valueFieldSettings: (context: PivotValueFieldSettingsSlotContext) => createElement(MantineValueFieldSettings, { context }),
    toolbar: (context: PivotToolbarSlotContext) => createElement(MantinePivotToolbar, { context }),
    reportFilters: (context: PivotReportFiltersSlotContext) => createElement(MantineReportFilters, { context }),
    staleReport: (context: PivotStaleReportSlotContext) => createElement(MantineStaleReport, { context }),
    fieldList: (context: PivotFieldListSlotContext) => createElement(MantineFieldList, { context }),
    detailsTabs: (context: PivotDetailsTabsSlotContext) => createElement(MantineDetailsTabs, { context }),
    detailsDialog: (context: PivotDetailsDialogSlotContext) => createElement(MantineDetailsDialog, { context }),
});
