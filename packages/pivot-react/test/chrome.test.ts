// @vitest-environment happy-dom
// ExPivot's slot contract run against a Chrome of plain controls (ADR-0390 section 7): what a Consumer's own design
// system writes, with nothing of a Wrapper's. Each Wrapper runs the same contract against its own components.
import { createElement, type ChangeEvent, type DragEvent, type ReactElement, type ReactNode } from 'react';
import { act, cleanup, render, within } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { RecordPivotSource, createPivotLayout, pivotRecordField } from '@ex-grid/pivot-engine';
import type {
    PivotDetailsDialogSlotContext, PivotDetailsTabsSlotContext, PivotFieldListSlotContext, PivotFieldSettingsSlotContext, PivotItemFilterSlotContext, PivotMenuSlotContext, PivotReportFiltersSlotContext, PivotStaleReportSlotContext,
    PivotToolbarSlotContext, PivotValueFieldSettingsSlotContext,
} from '@ex-grid/pivot';
import type { GridMenuSlotContext } from '@ex-grid/core';
import { useMenuItems } from '@ex-grid/react';
import { usePivotMenu, usePivotPanel, usePivotPart, type ExPivotChrome } from '../src/chrome';
import { ExPivot } from '../src/ex-pivot';
import { dragEvent, pivotSlotContract } from './pivot-slot-contract';

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

// The toolbar's popups open in the anchor around the button that opens them, which the pivot appends their frame to.
function PlainToolbar({ context }: { readonly context: PivotToolbarSlotContext }): ReactElement {
    const view = usePivotPart(context);
    return createElement('div', { role: 'toolbar' },
        view.reportFilters ? createElement('div', { ref: context.anchor('report-filters') }) : null,
        createElement('span', { ref: context.anchor('layout') }, createElement('button', {
            ref: context.control('layout'), type: 'button', 'aria-haspopup': 'menu', 'aria-expanded': view.layoutMenu.open, onClick: context.toggleLayoutMenu,
        }, view.layoutMenu.label)),
        view.refresh === undefined ? null : createElement('button', { ref: context.control('refresh'), type: 'button', onClick: context.refresh }, view.refresh.label),
        createElement('button', {
            ref: context.control('field-list'), type: 'button', 'aria-pressed': view.fieldList.shown, onClick: context.toggleFieldList,
        }, view.fieldList.label),
        view.refusal === undefined ? null : createElement('div', { role: 'alert' }, view.refusal));
}

function PlainReportFilters({ context }: { readonly context: PivotReportFiltersSlotContext }): ReactElement {
    const view = usePivotPart(context);
    return createElement('div', null, ...view.filters.map(filter => createElement('span', { key: filter.field },
        filter.caption,
        createElement('span', { ref: context.anchor(filter.field) }, createElement('button', {
            ref: context.control(filter.field), type: 'button', 'aria-label': filter.buttonLabel, 'aria-haspopup': 'dialog', 'aria-expanded': filter.open,
            onClick: () => { context.toggle(filter.field); },
        }, filter.summary)))));
}

function PlainStaleReport({ context }: { readonly context: PivotStaleReportSlotContext }): ReactElement {
    const view = usePivotPart(context);
    return createElement('div', null, view.message,
        createElement('button', { ref: context.control('retry'), type: 'button', disabled: !view.retry.enabled, onClick: context.retry }, view.retry.label));
}

// The pane of plain controls: each drop target hears the drag's events, each draggable element starts and ends it.
function PlainFieldList({ context }: { readonly context: PivotFieldListSlotContext }): ReactElement {
    const view = usePivotPart(context);
    const target = (area: Parameters<PivotFieldListSlotContext['drop']>[1], index: number): object => ({
        onDragOver: (event: DragEvent<HTMLElement>) => { context.dragOver(event.nativeEvent); },
        onDragEnter: (event: DragEvent<HTMLElement>) => { event.stopPropagation(); context.dragEnter(area, index); },
        onDrop: (event: DragEvent<HTMLElement>) => { event.stopPropagation(); context.drop(event.nativeEvent, area, index); },
    });
    return createElement('div', null,
        createElement('div', null, view.title,
            createElement('button', { type: 'button', 'aria-label': view.hideLabel, onClick: context.close }, '×')),
        createElement('input', {
            ref: context.control('search'), type: 'search', 'aria-label': view.searchLabel, value: view.search,
            onChange: (event: ChangeEvent<HTMLInputElement>) => { context.setSearch(event.currentTarget.value); },
        }),
        createElement('ul', {
            'aria-label': view.title,
            onDragOver: (event: DragEvent<HTMLElement>) => { context.dragOver(event.nativeEvent); },
            onDrop: (event: DragEvent<HTMLElement>) => { context.dropOnList(event.nativeEvent); },
        }, ...view.fields.map(field => createElement('li', {
            key: field.name, draggable: true,
            onDragStart: (event: DragEvent<HTMLElement>) => { context.startDrag(event.nativeEvent, { field: field.name }); },
            onDragEnd: context.endDrag,
        }, labelled(field.caption, createElement('input', {
            ref: context.control(field.key), type: 'checkbox', checked: field.placed, onChange: () => { context.toggle(field.name); },
        }))))),
        ...view.areas.map(area => createElement('div', { key: area.area, ref: context.area(area.area), ...target(area.area, area.end) },
            area.title,
            area.overCap === undefined ? null : createElement('div', { role: 'status' }, area.overCap),
            createElement('div', { role: 'list', 'aria-label': area.title }, ...area.entries.map(entry => createElement('div', {
                key: entry.key, role: 'listitem', ref: context.anchor(entry.key), draggable: !entry.menuOpen, ...target(area.area, entry.dropIndex),
                onDragStart: (event: DragEvent<HTMLElement>) => { context.startDrag(event.nativeEvent, { entry: entry.entry }); },
                onDragEnd: context.endDrag,
            }, createElement('button', {
                ref: context.control(entry.key), type: 'button', 'aria-label': entry.menuLabel, 'aria-expanded': entry.menuOpen,
                onClick: () => { context.openMenu(entry.entry); },
            }, entry.caption)))))),
        labelled(view.defer.label, createElement('input', {
            type: 'checkbox', checked: view.defer.ticked, onChange: (event: ChangeEvent<HTMLInputElement>) => { context.setDeferred(event.currentTarget.checked); },
        })),
        createElement('button', { type: 'button', disabled: !view.update.enabled, onClick: context.update }, view.update.label));
}

function PlainDetailsTabs({ context }: { readonly context: PivotDetailsTabsSlotContext }): ReactElement {
    const view = usePivotPart(context);
    return createElement('div', { role: 'tablist', 'aria-label': view.name }, ...view.tabs.map(tab => createElement('span', { key: tab.key },
        createElement('button', {
            ref: context.control(tab.key), type: 'button', role: 'tab', 'aria-selected': tab.selected, 'aria-controls': tab.panelId,
            onClick: () => { context.select(tab.key); },
        }, tab.title),
        tab.closeLabel === undefined ? null : createElement('button', { type: 'button', 'aria-label': tab.closeLabel, onClick: () => { context.close(tab.key); } }, '×'))));
}

function PlainDetailsDialog({ context }: { readonly context: PivotDetailsDialogSlotContext }): ReactElement {
    const view = usePivotPart(context);
    return createElement('div', null, createElement('h2', null, view.title), createElement('div', { ref: context.anchor('records') }),
        createElement('button', { ref: context.control('close'), type: 'button', onClick: context.close }, view.closeLabel));
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
    toolbar: (context: PivotToolbarSlotContext) => createElement(PlainToolbar, { context }),
    reportFilters: (context: PivotReportFiltersSlotContext) => createElement(PlainReportFilters, { context }),
    staleReport: (context: PivotStaleReportSlotContext) => createElement(PlainStaleReport, { context }),
    fieldList: (context: PivotFieldListSlotContext) => createElement(PlainFieldList, { context }),
    detailsTabs: (context: PivotDetailsTabsSlotContext) => createElement(PlainDetailsTabs, { context }),
    detailsDialog: (context: PivotDetailsDialogSlotContext) => createElement(PlainDetailsDialog, { context }),
};

pivotSlotContract('a Chrome of plain controls', plainChrome);

const sales = [{ region: 'EU', amount: 10 }, { region: 'US', amount: 5 }];
const salesFields = [
    pivotRecordField<(typeof sales)[number]>({ name: 'Region', type: 'text', value: sale => sale.region }),
    pivotRecordField<(typeof sales)[number]>({ name: 'Amount', type: 'number', value: sale => sale.amount }),
];
const salesSource = new RecordPivotSource(sales, salesFields);

async function drawn(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 3; turn++) await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

// A pivot of the sales, drawn with `chrome`.
function salesElement(chrome: ExPivotChrome): ReactElement {
    return createElement(ExPivot, {
        source: salesSource, layout: createPivotLayout({ rows: ['Region'], values: ['Amount'] }), slicing: { yield: () => Promise.resolve() }, locale: 'en-US',
        chrome, viewportWidth: 600, viewportHeight: 320,
    });
}

// The sales pivot rendered with `chrome`; rendered again with the next by the answer's `again`.
async function salesPivot(chrome: ExPivotChrome): Promise<{ readonly root: HTMLElement; readonly again: (next: ExPivotChrome) => Promise<void> }> {
    const { container, rerender } = render(salesElement(chrome));
    await drawn();
    const root = container.querySelector('.ex-pivot');
    if (!(root instanceof HTMLElement)) throw new Error('The pivot drew no root.');
    return { root, again: async next => { rerender(salesElement(next)); await drawn(); } };
}

describe('ExPivot for React\'s standing parts (ADR-0390)', () => {
    afterEach(cleanup);

    it('ADR-0390: contents a render function made anew at each render draw are still told each change', async () => {
        const { root, again } = await salesPivot({ toolbar: context => createElement(PlainToolbar, { context }) });
        await again({ toolbar: context => createElement(PlainToolbar, { context }) });
        const toggle = within(root).getByRole('button', { name: 'Field List' });
        await act(async () => { toggle.click(); await Promise.resolve(); });
        await drawn();
        expect(toggle.getAttribute('aria-pressed')).toBe('false');
    });

    it('ADR-0390, ADR-0061: a drag the Field List\'s contents started ends with them, and the report no longer offers to remove its field', async () => {
        const { root, again } = await salesPivot({ fieldList: context => createElement(PlainFieldList, { context }) });
        const region = within(root).getByRole('button', { name: 'Options for Region' });
        await act(async () => {
            region.parentElement?.dispatchEvent(dragEvent('dragstart'));
            await Promise.resolve();
        });
        await drawn();
        expect(root.querySelector('.ex-pivot-report-drop-remove')).not.toBeNull();
        await again({});
        expect([root.querySelector('.ex-pivot-report-drop-remove'), root.querySelector('[data-ex-pivot-slot=fieldList]')]).toEqual([null, null]);
    });
});
