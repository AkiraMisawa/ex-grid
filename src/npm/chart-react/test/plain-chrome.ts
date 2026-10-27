// A Chrome of plain controls (ADR-0390 section 7): what a Consumer's own design system writes, with nothing of a
// Wrapper's. The contracts run against it here, and each Wrapper runs the same contracts against its own components.
import { createElement, type ReactElement } from 'react';
import type { ChartFiltersSlotContext, ChartMenuSlotContext, ChartToolbarSlotContext } from '@ex-grid/chart';
import { useChartFilterText } from '../src/chart-filter-text';
import type { ExChartChrome } from '../src/ex-chart';

// One end of the range, a text field under its label.
const end = (label: string, value: string, set: (text: string) => void): ReactElement => createElement('label', null, label,
    createElement('input', { value, onChange: event => { set(event.currentTarget.value); } }));

function PlainFilters({ context, filters }: { readonly context: ChartToolbarSlotContext; readonly filters: ChartFiltersSlotContext }): ReactElement {
    const typed = useChartFilterText(filters);
    return createElement('div', null,
        end(context.label('exchart.filters.from'), typed.from, typed.setFrom),
        end(context.label('exchart.filters.to'), typed.to, typed.setTo),
        createElement('button', { type: 'button', onClick: typed.apply }, context.label('exchart.filters.apply')),
        filters.scale === undefined ? null : createElement('label', null, context.label('exchart.filters.axis'), createElement('select', {
            value: filters.scale,
            onChange: event => {
                const { currentTarget } = event;
                if (!(currentTarget instanceof HTMLSelectElement)) throw new TypeError('The axis switch is a select.');
                filters.setScale(currentTarget.value === 'categories' ? 'categories' : 'proportional');
            },
        }, createElement('option', { value: 'proportional' }, context.label('exchart.filters.proportional')),
        createElement('option', { value: 'categories' }, context.label('exchart.filters.categories')))),
        filters.error === null ? null : createElement('span', { role: 'alert' }, filters.error));
}

function PlainToolbar({ context }: { readonly context: ChartToolbarSlotContext }): ReactElement {
    const { filters } = context;
    return createElement('div', null,
        createElement('div', { role: 'toolbar', 'aria-label': context.name }, ...context.commands.map(command => createElement('button', {
            key: command.id, type: 'button', disabled: !command.enabled, 'aria-expanded': command.expanded, onClick: command.invoke,
        }, command.label))),
        filters === undefined ? null : createElement(PlainFilters, { context, filters }));
}

/** The chart's toolbar and menu in plain elements. */
export const plainChrome: ExChartChrome = {
    toolbar: (context: ChartToolbarSlotContext) => createElement(PlainToolbar, { context }),
    menu: (context: ChartMenuSlotContext) => createElement('div', null, ...context.commands.map(command => createElement('button', {
        key: command.id, type: 'button', role: 'menuitem', disabled: !command.enabled, onClick: command.invoke,
    }, command.label))),
};
