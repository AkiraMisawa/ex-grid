// @vitest-environment happy-dom
// ExPivot's slot contract run against a Chrome of plain controls (ADR-0390 section 7): what a Consumer's own design
// system writes, with nothing of a Wrapper's. Each Wrapper runs the same contract against its own components.
import { createElement, type ChangeEvent, type ReactElement, type ReactNode } from 'react';
import type {
    PivotFieldSettingsSlotContext, PivotItemFilterSlotContext, PivotMenuSlotContext, PivotValueFieldSettingsSlotContext,
} from '@ex-grid/pivot';
import type { GridMenuSlotContext } from '@ex-grid/core';
import { useMenuItems } from '@ex-grid/react';
import { usePivotMenu, usePivotPanel, type ExPivotChrome } from '../src/chrome';
import { pivotSlotContract } from './pivot-slot-contract';

function PlainMenu({ context }: { readonly context: PivotMenuSlotContext }): ReactElement {
    const { view, item } = usePivotMenu(context);
    return createElement('div', null, ...view.commands.flatMap((command, index) => [
        command.heading === undefined ? null : createElement('div', { key: `${command.id}-heading`, 'aria-hidden': true }, command.heading),
        createElement('button', { key: command.id, type: 'button', ...item(index) }, command.label),
    ]));
}

// A field under its label, which names it.
const labelled = (label: string, control: ReactNode): ReactElement => createElement('label', null, label, control);

const actions = (context: { readonly label: (id: string) => string; readonly apply: () => void; readonly cancel: () => void }, canApply = true): ReactElement =>
    createElement('div', null,
        createElement('button', { type: 'button', disabled: !canApply, onClick: context.apply }, context.label('ok')),
        createElement('button', { type: 'button', onClick: context.cancel }, context.label('cancel')));

function PlainFieldSettings({ context }: { readonly context: PivotFieldSettingsSlotContext }): ReactElement {
    const { view, first } = usePivotPanel(context);
    return createElement('div', null, createElement('div', null, view.caption),
        labelled(context.label('subtotals-automatic'), createElement('input', {
            ref: first, type: 'radio', checked: view.subtotals, onChange: () => { context.setSubtotals(true); },
        })),
        labelled(context.label('subtotals-none'), createElement('input', {
            type: 'radio', checked: !view.subtotals, onChange: () => { context.setSubtotals(false); },
        })),
        labelled(context.label('sort-order'), createElement('select', {
            value: String(view.sort), onChange: (event: ChangeEvent<HTMLSelectElement>) => { context.setSort(Number(event.currentTarget.value)); },
        }, ...view.sorts.map((sort, index) => createElement('option', { key: index, value: String(index) }, sort)))),
        actions(context));
}

function PlainValueFieldSettings({ context }: { readonly context: PivotValueFieldSettingsSlotContext }): ReactElement {
    const { view, first } = usePivotPanel(context);
    return createElement('div', null, createElement('div', null, view.sourceCaption),
        labelled(context.label('custom-name'), createElement('input', {
            ref: first, type: 'text', value: view.caption, onChange: (event: ChangeEvent<HTMLSelectElement>) => { context.setCaption(event.currentTarget.value); },
        })),
        labelled(context.label('summarize-by'), createElement('select', {
            value: view.aggregation,
            onChange: (event: ChangeEvent<HTMLSelectElement>) => {
                const picked = view.aggregations.find(choice => choice.value === event.currentTarget.value);
                if (picked === undefined) throw new RangeError('The select offered an Aggregation the pivot did not.');
                context.setAggregation(picked.value);
            },
        }, ...view.aggregations.map(choice => createElement('option', { key: choice.value, value: choice.value, disabled: !choice.enabled }, choice.label)))),
        labelled(context.label('show-values-as'), createElement('select', {
            value: view.showValuesAs,
            onChange: (event: ChangeEvent<HTMLSelectElement>) => {
                const picked = view.showValuesAsChoices.find(choice => choice.value === event.currentTarget.value);
                if (picked === undefined) throw new RangeError('The select offered a Show Values As the pivot did not.');
                context.setShowValuesAs(picked.value);
            },
        }, ...view.showValuesAsChoices.map(choice => createElement('option', { key: choice.value, value: choice.value }, choice.label)))),
        labelled(context.label('number-format'), createElement('input', {
            type: 'text', value: view.numberFormat, placeholder: view.numberFormatPlaceholder, onChange: event => { context.setNumberFormat(event.currentTarget.value); },
        })),
        createElement('div', null, view.sample),
        view.refusal === undefined ? null : createElement('div', { role: 'alert' }, view.refusal),
        actions(context));
}

function PlainItemFilter({ context }: { readonly context: PivotItemFilterSlotContext }): ReactElement {
    const { view, first } = usePivotPanel(context);
    return createElement('div', null,
        createElement('input', {
            ref: first, type: 'search', 'aria-label': context.label('search-items'), value: view.search,
            onChange: event => { context.setSearch(event.currentTarget.value); },
        }),
        view.unavailable !== undefined ? createElement('div', { role: 'alert' }, view.unavailable)
            : view.loading ? createElement('div', null, context.label('loading'))
            : createElement('div', { role: 'group', 'aria-label': view.caption, 'aria-busy': view.updating },
                view.items.length === 0 ? null : labelled(context.label('select-all'), createElement('input', {
                    type: 'checkbox', checked: view.allTicked === true, onChange: event => { context.setAll(event.currentTarget.checked); },
                })),
                ...view.items.map((item, index) => createElement('label', { key: index }, item.label, createElement('input', {
                    type: 'checkbox', checked: item.ticked, onChange: event => { context.setTicked(item.key, event.currentTarget.checked); },
                }))),
                view.note === undefined ? null : createElement('div', null, view.note)),
        view.refusal === undefined ? null : createElement('div', { role: 'alert' }, view.refusal),
        actions(context, view.canApply));
}

function PlainGridMenu({ context }: { readonly context: GridMenuSlotContext }): ReactElement {
    const item = useMenuItems(context);
    return createElement('div', { role: 'none' }, ...context.commands.map((command, index) => createElement('button', {
        ...item(index), key: command.id, type: 'button',
    }, context.label(command.id))));
}

const plainChrome: ExPivotChrome = {
    grid: { contextMenu: (context: GridMenuSlotContext) => createElement(PlainGridMenu, { context }) },
    menu: (context: PivotMenuSlotContext) => createElement(PlainMenu, { context }),
    fieldSettings: (context: PivotFieldSettingsSlotContext) => createElement(PlainFieldSettings, { context }),
    valueFieldSettings: (context: PivotValueFieldSettingsSlotContext) => createElement(PlainValueFieldSettings, { context }),
    itemFilter: (context: PivotItemFilterSlotContext) => createElement(PlainItemFilter, { context }),
};

pivotSlotContract('a Chrome of plain controls', plainChrome);
