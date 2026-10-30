// ExChart's Chrome under Mantine (ADR-0281, ADR-0390): the toolbar, with Chart Filters' range in Mantine's inputs and
// the axis switch, and the context menu's items, in Mantine's components at the size the dock or the grid's density
// maps onto. Which commands there are, what a typed range means, what is refused and where the menu opens are the
// chart's; these components draw what the chart hands them and call back. The legend, the tooltip and the plot stay
// the chart's own (ADR-0255).

import { createElement, useContext, type ReactElement } from 'react';
import { Alert, Button, NativeSelect, TextInput } from '@mantine/core';
import { chartWordIds, type AxisScale, type ChartCommand, type ChartFiltersSlotContext, type ChartMenuSlotContext, type ChartToolbarSlotContext } from '@ex-grid/chart';
import { useChartFilterText, type ExChartChrome } from '@ex-grid/chart-react';
import { MantineSizeContext } from '@ex-grid/mantine';

const downloads: ReadonlySet<string> = new Set([chartWordIds.downloadCsv, chartWordIds.downloadSvg, chartWordIds.downloadPng]);

/** One of the toolbar's commands: Mantine's subtle button, lit while what it opens is open. */
function commandButton(command: ChartCommand, variant: 'subtle' | 'default', size: string): ReactElement {
    return createElement(Button<'button'>, {
        key: command.id, type: 'button', size, variant: command.expanded === true ? 'light' : variant,
        disabled: !command.enabled, onClick: command.invoke,
        ...(command.expanded === undefined ? {} : { 'aria-expanded': command.expanded }),
        ...(command.title === undefined ? {} : { title: command.title }),
    }, command.label);
}

/** Chart Filters: the range's ends in Mantine's inputs, a select of the categories on a category axis, Apply, and the
 * axis switch, which takes effect at once. */
function MantineChartFilters({ context, filters }: { readonly context: ChartToolbarSlotContext; readonly filters: ChartFiltersSlotContext }): ReactElement {
    const size = useContext(MantineSizeContext);
    const typed = useChartFilterText(filters);
    const end = (id: string, value: string, set: (text: string) => void): ReactElement => (filters.values === 'labels'
        ? createElement(NativeSelect, {
            key: id, size, label: context.label(id), value, className: 'mantine-ex-chart-end',
            data: [{ value: '', label: '' }, ...filters.labels.map(label => ({ value: label, label }))],
            onChange: event => { set(event.currentTarget.value); },
        })
        : createElement(TextInput, {
            key: id, size, label: context.label(id), value, placeholder: filters.hint, className: 'mantine-ex-chart-end',
            onChange: event => { set(event.currentTarget.value); },
        }));
    return createElement('div', { className: 'mantine-ex-chart-filters', role: 'group', 'aria-label': context.label(chartWordIds.filters) },
        end(chartWordIds.from, typed.from, typed.setFrom),
        end(chartWordIds.to, typed.to, typed.setTo),
        createElement(Button<'button'>, {
            type: 'button', size, className: 'mantine-ex-chart-apply', onClick: typed.apply,
        }, context.label(chartWordIds.apply)),
        filters.scale === undefined ? null : createElement(NativeSelect, {
            size, label: context.label(chartWordIds.axis), value: filters.scale, className: 'mantine-ex-chart-scale',
            data: [
                { value: 'proportional', label: context.label(chartWordIds.proportional) },
                { value: 'categories', label: context.label(chartWordIds.categories) },
            ],
            onChange: event => { filters.setScale(scaleOf(event.currentTarget.value)); },
        }),
        filters.error === null ? null : createElement(Alert, {
            color: 'red', variant: 'light', className: 'mantine-ex-chart-filter-error', p: 'xs',
        }, filters.error));
}

/** The scale an option names: one the chart offered, never one a changed option claims. */
function scaleOf(value: string): AxisScale {
    if (value === 'proportional' || value === 'categories') return value;
    throw new RangeError(`'${value}' is not a scale the axis switch offers (ADR-0251).`);
}

/** The toolbar: the chart's commands in its order, the downloads as one group, then Chart Filters while it is open. */
function MantineChartToolbar({ context }: { readonly context: ChartToolbarSlotContext }): ReactElement {
    const size = useContext(MantineSizeContext);
    const { filters } = context;
    return createElement('div', { className: 'mantine-ex-chart-chrome' },
        createElement('div', { className: 'mantine-ex-chart-toolbar', role: 'toolbar', 'aria-label': context.name },
            ...context.commands.filter(command => !downloads.has(command.id)).map(command => commandButton(command, 'subtle', size)),
            createElement('div', { role: 'group', 'aria-label': context.label(chartWordIds.downloads), className: 'mantine-ex-chart-downloads' },
                createElement(Button.Group, null,
                    ...context.commands.filter(command => downloads.has(command.id)).map(command => commandButton(command, 'default', size))))),
        filters === undefined ? null : createElement(MantineChartFilters, { context, filters }));
}

/** The context menu's items: Mantine's subtle buttons, as its Menu draws an item, inside the chart's frame. */
function MantineChartMenu({ context }: { readonly context: ChartMenuSlotContext }): ReactElement {
    const size = useContext(MantineSizeContext);
    return createElement('div', { role: 'none', className: 'mantine-ex-chart-menu' },
        ...context.commands.map(command => createElement(Button<'button'>, {
            key: command.id, type: 'button', role: 'menuitem', variant: 'subtle', color: 'gray', size, fullWidth: true, justify: 'flex-start',
            disabled: !command.enabled, onClick: command.invoke, className: 'mantine-ex-chart-menu-item',
        }, command.label)));
}

/** ExChart's Chrome drawn in Mantine's components: hand it as an ExChart's `chrome`, inside a MantineProvider and
 * with `@ex-grid/chart-mantine/styles.css` loaded. {@link MantineChartDock} hands it to every chart it holds. */
export const mantineChartChrome: ExChartChrome = Object.freeze({
    toolbar: (context: ChartToolbarSlotContext) => createElement(MantineChartToolbar, { context }),
    menu: (context: ChartMenuSlotContext) => createElement(MantineChartMenu, { context }),
});
