// @vitest-environment happy-dom
// The chart slots' contract run against a Chrome of plain controls (ADR-0390 section 7): what a Consumer's own design
// system writes, with nothing of a Wrapper's. Each Wrapper runs the same contract against its own components.
import { createElement, useState, type ReactElement } from 'react';
import type { ChartMenuSlotContext, ChartToolbarSlotContext } from '@ex-grid/chart';
import type { ExChartChrome } from '../src/ex-chart';
import { chartSlotContract } from './chart-slot-contract';

// One end of the range, a text field under its label.
const end = (label: string, value: string, set: (text: string) => void): ReactElement => createElement('label', null, label,
    createElement('input', { value, onChange: event => { set(event.currentTarget.value); } }));

function PlainToolbar({ context }: { readonly context: ChartToolbarSlotContext }): ReactElement {
    const { filters } = context;
    // What is typed, and the range it was taken from: replaced whenever the chart's range changes.
    const [typed, setTyped] = useState({ held: [filters?.from, filters?.to], from: filters?.from ?? '', to: filters?.to ?? '' }); // oxlint-disable-line ex-grid/no-stand-in-default -- an end the chart holds none of is an empty field
    if (filters !== undefined && (typed.held[0] !== filters.from || typed.held[1] !== filters.to)) {
        setTyped({ held: [filters.from, filters.to], from: filters.from ?? '', to: filters.to ?? '' }); // oxlint-disable-line ex-grid/no-stand-in-default -- as above
    }
    return createElement('div', null,
        createElement('div', { role: 'toolbar', 'aria-label': context.name }, ...context.commands.map(command => createElement('button', {
            key: command.id, type: 'button', disabled: !command.enabled, 'aria-expanded': command.expanded, onClick: command.invoke,
        }, command.label))),
        filters === undefined ? null : createElement('div', null,
            end(context.label('exchart.filters.from'), typed.from, from => { setTyped({ ...typed, from }); }),
            end(context.label('exchart.filters.to'), typed.to, to => { setTyped({ ...typed, to }); }),
            createElement('button', { type: 'button', onClick: () => { filters.apply(typed.from, typed.to); } }, context.label('exchart.filters.apply')),
            filters.scale === undefined ? null : createElement('label', null, context.label('exchart.filters.axis'), createElement('select', {
                value: filters.scale,
                onChange: event => {
                    const { currentTarget } = event;
                    if (!(currentTarget instanceof HTMLSelectElement)) throw new TypeError('The axis switch is a select.');
                    filters.setScale(currentTarget.value === 'categories' ? 'categories' : 'proportional');
                },
            }, createElement('option', { value: 'proportional' }, context.label('exchart.filters.proportional')),
            createElement('option', { value: 'categories' }, context.label('exchart.filters.categories')))),
            filters.error === null ? null : createElement('span', { role: 'alert' }, filters.error)));
}

const plainChrome: ExChartChrome = {
    toolbar: (context: ChartToolbarSlotContext) => createElement(PlainToolbar, { context }),
    menu: (context: ChartMenuSlotContext) => createElement('div', null, ...context.commands.map(command => createElement('button', {
        key: command.id, type: 'button', role: 'menuitem', disabled: !command.enabled, onClick: command.invoke,
    }, command.label))),
};

chartSlotContract('a Chrome of plain controls', plainChrome);
