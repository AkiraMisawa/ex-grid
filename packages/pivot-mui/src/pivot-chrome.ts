// ExPivot's Chrome under MUI (ADR-0061, ADR-0390): MUI's components in each of ExPivot for React's slots — the Field
// List, the Pivot Toolbar and its report filters, the menus and panels that open from them, the Stale Report's notice
// and Show Details' tabs and dialog — at the size the pivot's density maps onto. What a part shows, what a command or
// a drop does, what OK applies and why it is refused, where a frame stands and what closes it are the pivot's; these
// components draw what it hands them and call back. The frames are the pivot's, so MUI's own Menu, MenuList, Popover,
// Dialog and Select, which position, dismiss or answer keys themselves, are not used; a select is MUI's native one,
// whose list is the browser's and opens no popup of the page's own.

import { createElement, useContext, useId, type DragEvent, type KeyboardEvent, type ReactElement, type ReactNode } from 'react';
import {
    Alert, Button, Checkbox, CircularProgress, FormControlLabel, IconButton, ListItemButton, Paper, Radio, RadioGroup, Tab, Tabs, TextField, Typography,
} from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { MuiSizeContext, refuseThemeWithoutVariables, type MuiExGridSize } from '@ex-grid/mui';
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

/** The size the pivot's density maps onto, read where the theme has been found readable: every slot's contents call it,
 * so a Consumer that frames the pivot itself under a theme without CSS variables is refused by name whichever slots it
 * keeps (ADR-0390 section 5). */
function useMuiSize(): MuiExGridSize {
    refuseThemeWithoutVariables(useTheme(), 'muiPivotChrome');
    return useContext(MuiSizeContext);
}

/** The body text MUI sets beside a control of `size`. */
const textVariant = (size: MuiExGridSize['size']): 'body2' | 'body1' => (size === 'small' ? 'body2' : 'body1');

/** A ▾ after a button's words, read by no assistive technology: the button's `aria-haspopup` says it. */
const opens = createElement('span', { 'aria-hidden': true, className: 'mui-ex-pivot-opens' }, '▾');

/** The close mark's cross, drawn rather than written, so a control's text is its name alone. */
const cross = createElement('svg', { viewBox: '0 0 16 16', width: 12, height: 12, 'aria-hidden': true, focusable: false },
    createElement('path', { d: 'M4 4l8 8M12 4l-8 8', stroke: 'currentColor', strokeWidth: 1.75, strokeLinecap: 'round' }));

/** A control with its label, in the body text of the size, its press or its keys claimed as `claims` says. */
function labelled(label: string, control: ReactElement, size: MuiExGridSize['size'], claims: typeof claimsClick | typeof claimsNothing, key?: string | number): ReactElement {
    return createElement(FormControlLabel, {
        ...(key === undefined ? {} : { key }), label, control, className: 'mui-ex-pivot-label', ...claims,
        slotProps: { typography: { variant: textVariant(size) } },
    });
}

/** A menu, a placed field's or the Layout menu: one of MUI's list item buttons per command, as its Menu draws an item,
 * a choice marked while it is the current one, and a group's heading where one starts. MUI's MenuItem is not used: it
 * stands only inside MUI's MenuList, which answers the arrows itself. */
function MuiPivotMenu({ context }: { readonly context: PivotMenuSlotContext }): ReactElement {
    const { view, item } = usePivotMenu(context);
    const { size, dense } = useMuiSize();
    return createElement('div', { role: 'none', className: 'mui-ex-pivot-menu' }, ...view.commands.flatMap((command, index) => [
        command.heading === undefined ? null : createElement(Typography<'div'>, {
            key: `${command.id}-heading`, component: 'div', variant: textVariant(size), color: 'textSecondary', className: 'mui-ex-pivot-menu-heading', 'aria-hidden': true,
        }, command.heading),
        createElement(ListItemButton<'button'>, {
            ...item(index), key: command.id, component: 'button', type: 'button', dense, className: 'mui-ex-pivot-menu-item',
        },
        // The mark is the stylesheet's, read off aria-checked, so the command's words stay the pivot's.
        command.checked === undefined ? null : createElement('span', { className: 'mui-ex-pivot-menu-mark', 'aria-hidden': true }),
        command.label),
    ]));
}

/** OK before Cancel, as Excel orders them; Cancel is never disabled. */
function panelActions(context: PivotPanelSlotContext, size: MuiExGridSize['size'], canApply = true): ReactElement {
    return createElement('div', { className: 'mui-ex-pivot-actions' },
        createElement(Button, { type: 'button', size, variant: 'contained', disabled: !canApply, onClick: context.apply, ...claimsClick }, context.label('ok')),
        createElement(Button, { type: 'button', size, variant: 'outlined', onClick: context.cancel, ...claimsClick }, context.label('cancel')));
}

/** The choice an option names: one the pivot offered, never one a changed option claims. */
function offered<T extends string>(value: string, choices: readonly { readonly value: T }[], what: string): T {
    const found = choices.find(choice => choice.value === value);
    if (found === undefined) throw new RangeError(`'${value}' is not ${what} the panel offers.`);
    return found.value;
}

/** A native select in MUI's field, its label above it: its list is the browser's, a popup of no page's. */
function nativeSelect(label: string, value: string, options: readonly { readonly value: string; readonly label: string; readonly disabled?: boolean }[],
    change: (value: string) => void, size: MuiExGridSize['size']): ReactElement {
    return createElement(TextField, {
        size, select: true, label, value, fullWidth: true, ...claimsNothing,
        slotProps: { select: { native: true }, inputLabel: { shrink: true } },
        onChange: event => { change(event.target.value); },
    }, ...options.map(option => createElement('option', { key: option.value, value: option.value, disabled: option.disabled === true }, option.label)));
}

/** Field Settings…: the subtotals' choice, which takes the keyboard, and the field's order. */
function MuiFieldSettings({ context }: { readonly context: PivotFieldSettingsSlotContext }): ReactElement {
    const { view, first } = usePivotPanel(context);
    const { size } = useMuiSize();
    // One group, so the arrows move between the two choices, named apart from every other panel's.
    const subtotals = useId();
    return createElement('div', { className: 'mui-ex-pivot-panel' },
        createElement(Typography<'div'>, { component: 'div', variant: textVariant(size), className: 'mui-ex-pivot-strong' }, view.caption),
        createElement(RadioGroup, {
            name: subtotals, value: view.subtotals ? 'automatic' : 'none',
            onChange: (_event: unknown, value: string) => { context.setSubtotals(value === 'automatic'); },
        },
        labelled(context.label('subtotals-automatic'), createElement(Radio, { size, value: 'automatic', slotProps: { input: { ref: first } } }), size, claimsNothing),
        labelled(context.label('subtotals-none'), createElement(Radio, { size, value: 'none' }), size, claimsNothing)),
        nativeSelect(context.label('sort-order'), String(view.sort), view.sorts.map((sort, index) => ({ value: String(index), label: sort })),
            value => { context.setSort(Number(value)); }, size),
        panelActions(context, size));
}

/** Value Field Settings…: the caption, which takes the keyboard, the Aggregation, Show Values As and the number
 * format, its sample, and the pivot's refusal. */
function MuiValueFieldSettings({ context }: { readonly context: PivotValueFieldSettingsSlotContext }): ReactElement {
    const { view, first } = usePivotPanel(context);
    const { size } = useMuiSize();
    return createElement('div', { className: 'mui-ex-pivot-panel' },
        createElement(Typography<'div'>, { component: 'div', variant: textVariant(size), color: 'textSecondary' }, view.sourceCaption),
        createElement(TextField, {
            size, inputRef: first, label: context.label('custom-name'), value: view.caption, fullWidth: true, ...claimsNothing,
            slotProps: { inputLabel: { shrink: true } },
            onChange: event => { context.setCaption(event.target.value); },
        }),
        nativeSelect(context.label('summarize-by'), view.aggregation,
            view.aggregations.map(choice => ({ value: choice.value, label: choice.label, disabled: !choice.enabled })),
            value => { context.setAggregation(offered(value, view.aggregations, 'an Aggregation')); }, size),
        nativeSelect(context.label('show-values-as'), view.showValuesAs, view.showValuesAsChoices,
            value => { context.setShowValuesAs(offered(value, view.showValuesAsChoices, 'a Show Values As')); }, size),
        createElement(TextField, {
            size, label: context.label('number-format'), value: view.numberFormat, placeholder: view.numberFormatPlaceholder, fullWidth: true, ...claimsNothing,
            slotProps: { inputLabel: { shrink: true } },
            onChange: event => { context.setNumberFormat(event.target.value); },
        }),
        createElement(Typography<'div'>, { component: 'div', variant: textVariant(size), color: 'textSecondary', className: 'mui-ex-pivot-sample' }, view.sample),
        view.refusal === undefined ? null : createElement(Alert, { severity: 'error' }, view.refusal),
        panelActions(context, size));
}

/** Filter…: the search, which takes the keyboard, (Select All) and each Item ticked while shown, why they cannot be
 * listed or that they are on their way, the note under them, and the pivot's refusal. */
function MuiItemFilter({ context }: { readonly context: PivotItemFilterSlotContext }): ReactElement {
    const { view, first } = usePivotPanel(context);
    const { size } = useMuiSize();
    const list = view.unavailable !== undefined
        ? createElement(Alert, { severity: 'warning' }, view.unavailable)
        : view.loading
            ? createElement('div', { className: 'mui-ex-pivot-loading' },
                createElement(CircularProgress, { size: size === 'small' ? 16 : 20 }),
                createElement(Typography<'span'>, { component: 'span', variant: textVariant(size) }, context.label('loading')))
            : createElement('div', { role: 'group', 'aria-label': view.caption, 'aria-busy': view.updating, className: 'mui-ex-pivot-items' },
                view.items.length === 0 ? null : labelled(context.label('select-all'), createElement(Checkbox, {
                    size, checked: view.allTicked === true, indeterminate: view.allTicked === undefined,
                    onChange: event => { context.setAll(event.target.checked); },
                }), size, claimsNothing),
                // Keyed by position: the pivot's list is the order the Items stand in.
                ...view.items.map((item, at) => labelled(item.label, createElement(Checkbox, {
                    size, checked: item.ticked, onChange: event => { context.setTicked(item.key, event.target.checked); },
                }), size, claimsNothing, at)),
                view.note === undefined ? null : createElement(Typography<'div'>, { component: 'div', variant: textVariant(size), color: 'textSecondary' }, view.note));
    return createElement('div', { className: 'mui-ex-pivot-panel' },
        createElement(TextField, {
            size, inputRef: first, type: 'search', placeholder: context.label('search-items'), value: view.search, fullWidth: true, ...claimsNothing,
            slotProps: { htmlInput: { 'aria-label': context.label('search-items') } },
            onChange: event => { context.setSearch(event.target.value); },
        }),
        list,
        view.refusal === undefined ? null : createElement(Alert, { severity: 'error' }, view.refusal),
        panelActions(context, size, view.canApply));
}

/** The Pivot Toolbar: the report filter band's anchor on its left; Layout ▾ in the anchor its menu opens in, Refresh
 * where the source can be refreshed, and the Field List's toggle on its right; and the refusal of the user's last
 * change beneath. */
function MuiPivotToolbar({ context }: { readonly context: PivotToolbarSlotContext }): ReactElement {
    const view = usePivotPart(context);
    const { size } = useMuiSize();
    return createElement('div', { className: 'mui-ex-pivot-toolbar-frame' },
        createElement('div', { role: 'toolbar', className: 'mui-ex-pivot-toolbar' },
            createElement('div', { ref: context.anchor('report-filters'), className: 'mui-ex-pivot-toolbar-start' }),
            createElement('div', { className: 'mui-ex-pivot-toolbar-end' },
                createElement('span', { ref: context.anchor('layout'), className: 'mui-ex-pivot-anchor' }, createElement(Button, {
                    ref: context.control('layout'), type: 'button', size, variant: view.layoutMenu.open ? 'outlined' : 'text', className: 'mui-ex-pivot-button',
                    endIcon: opens, 'aria-haspopup': 'menu', 'aria-expanded': view.layoutMenu.open, onClick: context.toggleLayoutMenu, ...claimsClick,
                }, view.layoutMenu.label)),
                view.refresh === undefined ? null : createElement(Button, {
                    ref: context.control('refresh'), type: 'button', size, variant: 'text', className: 'mui-ex-pivot-button', onClick: context.refresh, ...claimsClick,
                }, view.refresh.label),
                createElement(Button, {
                    ref: context.control('field-list'), type: 'button', size, variant: view.fieldList.shown ? 'outlined' : 'text', className: 'mui-ex-pivot-button',
                    'aria-pressed': view.fieldList.shown, onClick: context.toggleFieldList, ...claimsClick,
                }, view.fieldList.label))),
        view.refusal === undefined ? null : createElement(Alert, { severity: 'error', className: 'mui-ex-pivot-refusal' }, view.refusal));
}

/** The report filter band: each field in Filters, its caption, and the button that says what it shows and opens
 * Filter… in the anchor around it. */
function MuiReportFilters({ context }: { readonly context: PivotReportFiltersSlotContext }): ReactElement {
    const view = usePivotPart(context);
    const { size } = useMuiSize();
    return createElement('div', { className: 'mui-ex-pivot-filters' }, ...view.filters.map(filter => createElement('div', {
        key: filter.field, className: 'mui-ex-pivot-filter',
    },
    createElement(Typography<'span'>, { component: 'span', variant: textVariant(size), className: 'mui-ex-pivot-strong mui-ex-pivot-filter-caption' }, filter.caption),
    createElement('span', { ref: context.anchor(filter.field), className: 'mui-ex-pivot-anchor mui-ex-pivot-filter-anchor' }, createElement(Button, {
        ref: context.control(filter.field), type: 'button', size, variant: 'outlined', color: filter.filtered ? 'primary' : 'inherit', fullWidth: true,
        className: 'mui-ex-pivot-button mui-ex-pivot-caption-button', endIcon: opens,
        'aria-label': filter.buttonLabel, 'aria-haspopup': 'dialog', 'aria-expanded': filter.open, onClick: () => { context.toggle(filter.field); }, ...claimsClick,
    }, createElement('span', { className: 'mui-ex-pivot-caption' }, filter.summary))))));
}

/** The Stale Report's notice, in MUI's alert, inside the pivot's live region: the sentence, and Retry as the alert's
 * action. */
function MuiStaleReport({ context }: { readonly context: PivotStaleReportSlotContext }): ReactElement {
    const view = usePivotPart(context);
    const { size } = useMuiSize();
    return createElement(Alert, {
        severity: 'warning', className: 'mui-ex-pivot-stale',
        action: createElement(Button, {
            ref: context.control('retry'), type: 'button', size, color: 'inherit', disabled: !view.retry.enabled, onClick: context.retry, ...claimsClick,
        }, view.retry.label),
    }, view.message);
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

/** The Field List, Excel's PivotTable Fields pane, in MUI's controls: the search and the fields with their tick boxes,
 * scrolling on their own; the four Areas, two by two, always in view at the foot; then Defer Layout Update and Update.
 * Drag and drop is the browser's own, its every event handed to the pivot. */
function MuiFieldList({ context }: { readonly context: PivotFieldListSlotContext }): ReactElement {
    const view = usePivotPart(context);
    const { size } = useMuiSize();
    const variant = textVariant(size);
    const fields = view.fields.map(field => createElement('div', {
        key: field.name, role: 'listitem', draggable: true,
        className: field.dragging ? 'mui-ex-pivot-field mui-ex-pivot-dragging' : 'mui-ex-pivot-field',
        onDragStart: (event: DragEvent<HTMLElement>) => { context.startDrag(event.nativeEvent, { field: field.name }); },
        onDragEnd: context.endDrag,
    },
    labelled(field.caption, createElement(Checkbox, {
        size, slotProps: { input: { ref: context.control(field.key) } }, checked: field.placed, onChange: () => { context.toggle(field.name); },
    }), size, claimsClick),
    field.filtered ? createElement(Typography<'span'>, { component: 'span', variant, color: 'textSecondary', 'aria-hidden': true }, '⏷') : null,
    field.count === undefined ? null : createElement(Typography<'span'>, {
        component: 'span', variant, color: 'textSecondary', title: field.countLabel, 'aria-label': field.countLabel,
    }, field.count)));
    const areas = view.areas.map(area => createElement(Paper, {
        key: area.area, ref: context.area(area.area), variant: 'outlined', ...dropTarget(context, area.area, area.end),
        className: area.dropTarget ? 'mui-ex-pivot-area mui-ex-pivot-drop-target' : 'mui-ex-pivot-area',
    },
    createElement(Typography<'div'>, { component: 'div', variant, className: 'mui-ex-pivot-strong mui-ex-pivot-area-title' }, area.title),
    area.overCap === undefined ? null : createElement(Typography<'div'>, { component: 'div', variant, color: 'error', role: 'status' }, area.overCap),
    createElement('div', { role: 'list', 'aria-label': area.title, className: 'mui-ex-pivot-entries' }, ...area.entries.map(entry => {
        const classes = ['mui-ex-pivot-entry'];
        if (entry.dragging) classes.push('mui-ex-pivot-dragging');
        if (entry.dropBefore) classes.push('mui-ex-pivot-drop-before');
        // The anchor the entry's menu and panels open in: no position of its own, so their frame takes the pane's width.
        return createElement('div', {
            key: entry.key, role: 'listitem', ref: context.anchor(entry.key), draggable: !entry.menuOpen, className: classes.join(' '),
            ...dropTarget(context, area.area, entry.dropIndex),
            onDragStart: (event: DragEvent<HTMLElement>) => { context.startDrag(event.nativeEvent, { entry: entry.entry }); },
            onDragEnd: context.endDrag,
        }, createElement(Button, {
            ref: context.control(entry.key), type: 'button', size, variant: 'outlined', color: entry.filtered ? 'primary' : 'inherit', fullWidth: true,
            className: 'mui-ex-pivot-button mui-ex-pivot-caption-button', endIcon: opens,
            'aria-label': entry.menuLabel, 'aria-haspopup': 'menu', 'aria-expanded': entry.menuOpen, onClick: () => { context.openMenu(entry.entry); }, ...claimsClick,
        }, createElement('span', { className: 'mui-ex-pivot-caption' }, entry.caption)));
    }))));
    return createElement('div', { className: 'mui-ex-pivot-pane' },
        createElement('div', { className: 'mui-ex-pivot-pane-heading' },
            createElement(Typography<'span'>, { component: 'span', variant, className: 'mui-ex-pivot-strong mui-ex-pivot-pane-title' }, view.title),
            createElement(IconButton, { size: 'small', 'aria-label': view.hideLabel, title: view.hideLabel, onClick: context.close, ...claimsClick }, cross)),
        createElement('div', { className: 'mui-ex-pivot-pane-body' },
            createElement(Typography<'div'>, { component: 'div', variant, color: 'textSecondary' }, view.chooseCaption),
            createElement(TextField, {
                inputRef: context.control('search'), size, type: 'search', placeholder: view.searchLabel, value: view.search, fullWidth: true, ...claimsNothing,
                slotProps: { htmlInput: { 'aria-label': view.searchLabel } },
                onChange: event => { context.setSearch(event.target.value); },
            }),
            createElement('div', {
                role: 'list', 'aria-label': view.title,
                className: view.dropRemoves ? 'mui-ex-pivot-fields mui-ex-pivot-drop-remove' : 'mui-ex-pivot-fields',
                onDragOver: (event: DragEvent<HTMLElement>) => { context.dragOver(event.nativeEvent); },
                onDrop: (event: DragEvent<HTMLElement>) => { context.dropOnList(event.nativeEvent); },
            }, ...fields),
            createElement(Typography<'div'>, { component: 'div', variant, color: 'textSecondary' }, view.dragCaption),
            createElement('div', { className: 'mui-ex-pivot-areas' }, ...areas)),
        createElement('div', { className: 'mui-ex-pivot-defer' },
            labelled(view.defer.label, createElement(Checkbox, {
                size, checked: view.defer.ticked, onChange: event => { context.setDeferred(event.target.checked); },
            }), size, claimsClick),
            createElement(Button, {
                type: 'button', size, variant: 'outlined', disabled: !view.update.enabled, onClick: context.update, ...claimsClick,
            }, view.update.label)));
}

/** Show Details' sheet tabs at the report's foot, as MUI's tabs: the report's own first, then each sheet's with its
 * close mark. Which sheet is selected, and what closing one selects, are the pivot's. */
function MuiDetailsTabs({ context }: { readonly context: PivotDetailsTabsSlotContext }): ReactElement {
    const view = usePivotPart(context);
    const { dense } = useMuiSize();
    const selected = view.tabs.find(tab => tab.selected);
    return createElement(Tabs, {
        // oxlint-disable-next-line ex-grid/no-stand-in-default -- MUI's Tabs say "no tab" as false; the pivot always selects one
        value: selected?.key ?? false, variant: 'scrollable', scrollButtons: 'auto', 'aria-label': view.name,
        // MUI's tabs take no size: a compact pivot's are drawn as low as its other parts by the stylesheet.
        className: dense ? 'mui-ex-pivot-tabs mui-ex-pivot-tabs-dense' : 'mui-ex-pivot-tabs',
        onChange: (_: unknown, key: unknown) => {
            if (typeof key !== 'string') throw new TypeError('A sheet\'s tab is named by its key.');
            context.select(key);
        },
    }, ...view.tabs.map(tab => createElement(Tab, {
        key: tab.key, value: tab.key, ref: context.control(tab.key), className: 'mui-ex-pivot-tab', ...claimsClick,
        ...(tab.panelId === undefined ? {} : { 'aria-controls': tab.panelId }),
        // Delete closes the sheet the keyboard is on, as the close mark does under the pointer.
        ...(tab.closeLabel === undefined ? {} : {
            'aria-keyshortcuts': 'Delete',
            onKeyDown: (event: KeyboardEvent<HTMLDivElement>) => { if (event.key === 'Delete') { event.preventDefault(); context.close(tab.key); } },
        }),
        // The title in a box of its own, which ends in an ellipsis when it is too long, so the close mark after it stays whole.
        label: createElement('span', { className: 'mui-ex-pivot-tab-label' }, createElement('span', { className: 'mui-ex-pivot-tab-title' }, tab.title),
            tab.closeLabel === undefined ? null :
            // A span, not a button: a button inside the tab's button is not one the browser lets stand. Off the Tab
            // order: Delete closes from the keyboard, and a stop inside each tab's button would be one the tabs' own
            // roving order does not know.
            createElement(IconButton<'span'>, {
                component: 'span', role: 'button', size: 'small', tabIndex: -1, 'aria-label': tab.closeLabel, title: tab.closeLabel,
                className: 'mui-ex-pivot-tab-close', ...claimsClick,
                onClick: event => { event.stopPropagation(); context.close(tab.key); },
            }, cross)),
    })));
}

/** What the Show Details dialog draws inside the pivot's frame: its title, the records where they stand, and Close. */
function MuiDetailsDialog({ context }: { readonly context: PivotDetailsDialogSlotContext }): ReactElement {
    const view = usePivotPart(context);
    const { size } = useMuiSize();
    return createElement('div', { className: 'mui-ex-pivot-dialog' },
        createElement(Typography<'div'>, { component: 'div', variant: 'h6', className: 'mui-ex-pivot-dialog-title' }, view.title),
        createElement('div', { ref: context.anchor('records'), className: 'mui-ex-pivot-dialog-records' }),
        createElement('div', { className: 'mui-ex-pivot-actions' },
            createElement(Button, { ref: context.control('close'), type: 'button', size, variant: 'contained', onClick: context.close, ...claimsClick }, view.closeLabel)));
}

const menu = (context: PivotMenuSlotContext): ReactNode => createElement(MuiPivotMenu, { context });

/** ExPivot's Chrome drawn in MUI's components, but for the report's grid: what {@link MuiPivot} hands its pivot, with
 * `@ex-grid/mui`'s `muiChrome` as `grid`. A Consumer that frames an ExPivot itself spreads both into its `chrome`,
 * inside an element of the classes `mui-ex-grid mui-ex-pivot` and a MuiSizeContext, under a ThemeProvider whose theme
 * was made with `cssVariables: true`, as MuiPivot draws them. */
export const muiPivotChrome: Omit<ExPivotChrome, 'grid'> = Object.freeze({
    menu,
    itemFilter: (context: PivotItemFilterSlotContext) => createElement(MuiItemFilter, { context }),
    fieldSettings: (context: PivotFieldSettingsSlotContext) => createElement(MuiFieldSettings, { context }),
    valueFieldSettings: (context: PivotValueFieldSettingsSlotContext) => createElement(MuiValueFieldSettings, { context }),
    toolbar: (context: PivotToolbarSlotContext) => createElement(MuiPivotToolbar, { context }),
    reportFilters: (context: PivotReportFiltersSlotContext) => createElement(MuiReportFilters, { context }),
    staleReport: (context: PivotStaleReportSlotContext) => createElement(MuiStaleReport, { context }),
    fieldList: (context: PivotFieldListSlotContext) => createElement(MuiFieldList, { context }),
    detailsTabs: (context: PivotDetailsTabsSlotContext) => createElement(MuiDetailsTabs, { context }),
    detailsDialog: (context: PivotDetailsDialogSlotContext) => createElement(MuiDetailsDialog, { context }),
});
