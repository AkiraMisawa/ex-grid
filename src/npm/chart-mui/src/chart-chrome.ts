// ExChart's Chrome under MUI (ADR-0281, ADR-0390): the toolbar, with Chart Filters' range in MUI's fields and the
// axis switch, and the context menu's items, in MUI's components at the size the dock or the grid's density maps
// onto. Which commands there are, what a typed range means, what is refused and where the menu opens are the chart's;
// these components draw what the chart hands them and call back. The legend, the tooltip and the plot stay the
// chart's own (ADR-0255). MUI's Menu and MenuList are not used: the menu's frame is the chart's, which takes the keys.

import { createElement, useContext, type ReactElement } from 'react';
import { Alert, Button, ButtonGroup, ListItemButton, TextField } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { chartWordIds, type AxisScale, type ChartCommand, type ChartFiltersSlotContext, type ChartMenuSlotContext, type ChartToolbarSlotContext } from '@ex-grid/chart';
import { useChartFilterText, type ExChartChrome } from '@ex-grid/chart-react';
import { MuiSizeContext } from '@ex-grid/mui';

const downloads: ReadonlySet<string> = new Set([chartWordIds.downloadCsv, chartWordIds.downloadSvg, chartWordIds.downloadPng]);

/** One of the toolbar's commands: MUI's text button, outlined while what it opens is open. */
function commandButton(command: ChartCommand, size: 'small' | 'medium'): ReactElement {
    return createElement(Button, {
        key: command.id, type: 'button', size, variant: command.expanded === true ? 'outlined' : 'text',
        disabled: !command.enabled, onClick: command.invoke, className: 'mui-ex-chart-command',
        ...(command.expanded === undefined ? {} : { 'aria-expanded': command.expanded }),
        ...(command.title === undefined ? {} : { title: command.title }),
    }, command.label);
}

/** Chart Filters: the range's ends in MUI's fields, a native select of the categories on a category axis, Apply, and
 * the axis switch, which takes effect at once. MUI has no date picker of its own (ADR-0390 section 10), so a date is
 * typed as the chart's hint shows. */
function MuiChartFilters({ context, filters }: { readonly context: ChartToolbarSlotContext; readonly filters: ChartFiltersSlotContext }): ReactElement {
    const { size } = useContext(MuiSizeContext);
    const typed = useChartFilterText(filters);
    const end = (id: string, value: string, set: (text: string) => void): ReactElement => (filters.values === 'labels'
        ? createElement(TextField, {
            key: id, size, select: true, label: context.label(id), value, className: 'mui-ex-chart-end',
            slotProps: { select: { native: true }, inputLabel: { shrink: true } },
            onChange: event => { set(event.target.value); },
        }, createElement('option', { key: '', value: '' }, ''), ...filters.labels.map(label => createElement('option', { key: label, value: label }, label)))
        : createElement(TextField, {
            key: id, size, label: context.label(id), value, placeholder: filters.hint, className: 'mui-ex-chart-end',
            slotProps: { inputLabel: { shrink: true } },
            onChange: event => { set(event.target.value); },
        }));
    return createElement('div', { className: 'mui-ex-chart-filters', role: 'group', 'aria-label': context.label(chartWordIds.filters) },
        end(chartWordIds.from, typed.from, typed.setFrom),
        end(chartWordIds.to, typed.to, typed.setTo),
        createElement(Button, {
            type: 'button', size, variant: 'outlined', className: 'mui-ex-chart-apply', onClick: typed.apply,
        }, context.label(chartWordIds.apply)),
        filters.scale === undefined ? null : createElement(TextField, {
            size, select: true, label: context.label(chartWordIds.axis), value: filters.scale, className: 'mui-ex-chart-scale',
            slotProps: { select: { native: true } },
            onChange: event => { filters.setScale(scaleOf(event.target.value)); },
        },
        createElement('option', { value: 'proportional' }, context.label(chartWordIds.proportional)),
        createElement('option', { value: 'categories' }, context.label(chartWordIds.categories))),
        filters.error === null ? null : createElement(Alert, { severity: 'error', className: 'mui-ex-chart-filter-error' }, filters.error));
}

/** Refuses a theme made without CSS variables (ADR-0390 section 5): the stylesheet reads the theme's colours from the
 * variables MUI writes on the page, and without them a chart would be drawn in ExChart's own colours, not the theme's. */
export function refuseThemeWithoutVariables(theme: object, who: string): void {
    const vars: unknown = Reflect.get(theme, 'vars');
    if (vars === undefined || vars === null) {
        throw new Error(`${who} needs a MUI theme with CSS variables: make it with createTheme({ cssVariables: true }) and hand it to the `
            + 'ThemeProvider above it. Without them the theme\'s colours exist only in script, and the chart would be drawn in colours that '
            + 'are not the theme\'s (ADR-0390 section 5).');
    }
    if (Reflect.get(theme, 'cssVarPrefix') !== 'mui') {
        throw new Error(`${who} reads the theme's variables as --mui-*: make the theme with the default cssVarPrefix (ADR-0390 section 5).`);
    }
}

/** The scale an option names: one the chart offered, never one a changed option claims. */
function scaleOf(value: string): AxisScale {
    if (value === 'proportional' || value === 'categories') return value;
    throw new RangeError(`'${value}' is not a scale the axis switch offers (ADR-0251).`);
}

/** The toolbar: the chart's commands in its order, the downloads as one button group, then Chart Filters while it is
 * open. */
function MuiChartToolbar({ context }: { readonly context: ChartToolbarSlotContext }): ReactElement {
    refuseThemeWithoutVariables(useTheme(), 'muiChartChrome');
    const { size } = useContext(MuiSizeContext);
    const { filters } = context;
    return createElement('div', { className: 'mui-ex-chart-chrome' },
        createElement('div', { className: 'mui-ex-chart-toolbar', role: 'toolbar', 'aria-label': context.name },
            ...context.commands.filter(command => !downloads.has(command.id)).map(command => commandButton(command, size)),
            // MUI's ButtonGroup is a group already, named here as the downloads.
            createElement(ButtonGroup, { size, variant: 'outlined', 'aria-label': context.label(chartWordIds.downloads), className: 'mui-ex-chart-downloads' },
                ...context.commands.filter(command => downloads.has(command.id)).map(command => createElement(Button, {
                    key: command.id, type: 'button', disabled: !command.enabled, onClick: command.invoke,
                    ...(command.title === undefined ? {} : { title: command.title }),
                }, command.label)))),
        filters === undefined ? null : createElement(MuiChartFilters, { context, filters }));
}

/** The context menu's items: MUI's list item buttons, as its Menu draws an item, inside the chart's frame. MUI's
 * MenuItem is not used: it stands only inside MUI's MenuList, which answers the arrows itself. */
function MuiChartMenu({ context }: { readonly context: ChartMenuSlotContext }): ReactElement {
    const { dense } = useContext(MuiSizeContext);
    return createElement('div', { role: 'none', className: 'mui-ex-chart-menu' },
        ...context.commands.map(command => createElement(ListItemButton<'button'>, {
            key: command.id, component: 'button', type: 'button', role: 'menuitem', dense,
            disabled: !command.enabled, onClick: command.invoke, className: 'mui-ex-chart-menu-item',
        }, command.label)));
}

/** ExChart's Chrome drawn in MUI's components: hand it as an ExChart's `chrome`, inside a ThemeProvider whose theme
 * was made with `cssVariables: true` and with `@ex-grid/chart-mui/styles.css` loaded. {@link MuiChartDock} hands it
 * to every chart it holds. */
export const muiChartChrome: ExChartChrome = Object.freeze({
    toolbar: (context: ChartToolbarSlotContext) => createElement(MuiChartToolbar, { context }),
    menu: (context: ChartMenuSlotContext) => createElement(MuiChartMenu, { context }),
});
