// The contract every Wrapper keeps with ExChart's Chrome slots (ADR-0390 section 7): run at layer 2 against each one
// alike, the test Chrome of plain controls here and each design system's Wrapper. The toolbar and the menu draw the
// chart's commands in the chart's order, each enabled as the chart says, with the chart's words; a command runs as
// the chart runs it; Chart Filters hands the typed range to the chart, which reads it and says what it could not
// read; and the Wrapper decides nothing: a disabled command runs nothing, and the menu's frame is the chart's.
import { createElement, type ComponentType, type ReactNode } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { ChartDefinition, type AxisScale, type ChartRange } from '@ex-grid/chart';
import { ExChart, type ExChartChrome } from '../src/ex-chart';

/** What a Wrapper's contract run needs around the chart: a provider its components read, as a Consumer's page has. */
export interface ChartContractHost {
    /** Wraps the rendered chart, as `@testing-library/react`'s `wrapper` does. */
    readonly wrapper?: ComponentType<{ readonly children: ReactNode }>;
}

/** Lets the chart's paint, and the render it asks of React, run. */
async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 4; turn++) await Promise.resolve();
    });
}

const numbers = new ChartDefinition({
    axis: { values: 'numbers', title: 'Years' },
    series: [{ id: 'epe', name: 'EPE', kind: 'level', points: [1, 5, 9, 4, 2].map((y, x) => ({ x, y })) }],
});

const categories = new ChartDefinition({
    axis: { values: 'labels', labels: ['1M', '3M', '6M', '1Y'] },
    series: [{ id: 'vol', name: 'Vol', kind: 'level', points: [10, 11, 12, 13].map((y, x) => ({ x, y })) }],
});

interface Mounted {
    readonly root: HTMLElement;
    readonly ranges: (ChartRange | null)[];
    readonly scales: AxisScale[];
}

function mounted(chrome: ExChartChrome, host: ChartContractHost, definition = numbers): Mounted {
    const ranges: (ChartRange | null)[] = [];
    const scales: AxisScale[] = [];
    const { container } = render(createElement(ExChart, {
        definition, title: 'Exposure', chrome, width: 600, height: 300,
        onRangeChanged: range => { ranges.push(range); }, onScaleChanged: scale => { scales.push(scale); },
    }), host);
    const root = container.querySelector('.ex-chart');
    if (!(root instanceof HTMLElement)) throw new Error('The chart drew no root.');
    return { root, ranges, scales };
}

function toolbarOf(root: HTMLElement): HTMLElement {
    const toolbar = root.querySelector<HTMLElement>('[role=toolbar]');
    if (toolbar === null) throw new Error('The Chrome drew no toolbar.');
    return toolbar;
}

/** The control the Wrapper drew for a word: the button whose text is the word. */
function button(within: Element, label: string): HTMLButtonElement {
    const found = [...within.querySelectorAll('button')].find(b => b.textContent.trim() === label);
    if (found === undefined) throw new Error(`No button reads ${label}.`);
    return found;
}

const enabled = (control: HTMLElement): boolean => !control.matches(':disabled') && control.getAttribute('aria-disabled') !== 'true';

/** The field a label names, by its `<label>` or its `aria-label`. */
function field(within: HTMLElement, label: string): HTMLInputElement | HTMLSelectElement {
    const byAria = within.querySelector(`[aria-label="${label}"]`);
    const labelled = [...within.querySelectorAll('label')].find(l => ownText(l) === label);
    const byFor = labelled === undefined ? null : labelled.control ?? (labelled.htmlFor === '' ? null : within.ownerDocument.getElementById(labelled.htmlFor));
    const control = byAria ?? byFor;
    if (!(control instanceof HTMLInputElement) && !(control instanceof HTMLSelectElement)) throw new Error(`No field is labelled ${label}.`);
    return control;
}

/** A label's own words: its text less what a select inside it lists. */
function ownText(label: HTMLLabelElement): string {
    return [...label.childNodes].filter(node => !(node instanceof HTMLSelectElement)).map(node => node.textContent).join('').trim();
}

/** Types `text` into a text field, or picks the option that reads `text` in a select, as React hears either. */
async function enter(control: HTMLInputElement | HTMLSelectElement, text: string): Promise<void> {
    await act(async () => {
        if (control instanceof HTMLSelectElement) {
            const option = [...control.options].find(o => o.textContent.trim() === text);
            if (option === undefined) throw new Error(`No option reads ${text}.`);
            Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value')?.set?.call(control, option.value);
            control.dispatchEvent(new Event('change', { bubbles: true }));
        } else {
            Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set?.call(control, text);
            control.dispatchEvent(new Event('input', { bubbles: true }));
        }
        await Promise.resolve();
    });
}

async function press(control: HTMLElement): Promise<void> {
    await act(async () => {
        control.click();
        await Promise.resolve();
    });
    await settled();
}

async function openMenu(root: HTMLElement): Promise<HTMLElement> {
    const layer = root.querySelector('.ex-chart-hit');
    if (layer === null) throw new Error('The chart drew no plot to ask a menu of.');
    await act(async () => {
        layer.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 40, clientY: 40 }));
        await Promise.resolve();
    });
    await settled();
    const menu = root.querySelector<HTMLElement>('.ex-chart-menu');
    if (menu === null) throw new Error('No menu opened.');
    return menu;
}

/** Runs the chart slots' contract against `chrome`, named `name` in the report. */
export function chartSlotContract(name: string, chrome: ExChartChrome, host: ChartContractHost = {}): void {
    describe(`${name} keeps the chart slots' contract (ADR-0390 section 7)`, () => {
        afterEach(() => { cleanup(); });

        it('ADR-0281: the toolbar draws the chart\'s commands in its order, each enabled as the chart says, and runs them through the chart', async () => {
            const { root, ranges } = mounted(chrome, host);
            await settled();
            expect(root.querySelector('.ex-chart-toolbar')).toBeNull();
            const toolbar = toolbarOf(root);
            expect(toolbar.getAttribute('aria-label')).toBe('Exposure');
            const labels = ['Chart Filters', 'Reset range', 'View as table', 'CSV', 'SVG', 'PNG'];
            const drawn = labels.map(label => button(toolbar, label));
            const all = [...toolbar.querySelectorAll('button')];
            const places = drawn.map(control => all.indexOf(control));
            expect(places).toEqual(places.toSorted((a, b) => a - b));
            expect(drawn.map(enabled)).toEqual([true, false, true, true, true, true]);
            // A disabled command runs nothing, whatever the Wrapper's control does with a click.
            await press(button(toolbarOf(root), 'Reset range'));
            expect(ranges).toEqual([]);
            await press(button(toolbarOf(root), 'View as table'));
            expect(root.querySelector('.ex-chart-table')).not.toBeNull();
            expect(button(toolbarOf(root), 'View as table').getAttribute('aria-expanded')).toBe('true');
            await press(button(toolbarOf(root), 'View as table'));
            expect(root.querySelector('.ex-chart-table')).toBeNull();
        });

        it('ADR-0273: Chart Filters hands the typed range to the chart, which says what it could not read, and keeps the typing across a new context', async () => {
            const { root, ranges, scales } = mounted(chrome, host);
            await settled();
            await press(button(toolbarOf(root), 'Chart Filters'));
            expect(button(toolbarOf(root), 'Chart Filters').getAttribute('aria-expanded')).toBe('true');
            await enter(field(root, 'From'), 'one');
            await press(button(root, 'Apply'));
            expect(root.textContent).toContain('“one” is not a number on this axis.');
            expect(ranges).toEqual([]);
            await enter(field(root, 'From'), '1');
            await enter(field(root, 'To'), '3');
            // What is typed is the Chrome's until Apply: a new context, the table opened, keeps it.
            await press(button(toolbarOf(root), 'View as table'));
            expect(field(root, 'To').value).toBe('3');
            await press(button(root, 'Apply'));
            expect(ranges).toEqual([{ from: 1, to: 3 }]);
            expect(root.textContent).not.toContain('is not a number');
            expect(enabled(button(toolbarOf(root), 'Reset range'))).toBe(true);
            await press(button(toolbarOf(root), 'Reset range'));
            expect(ranges).toEqual([{ from: 1, to: 3 }, null]);
            // The range as the chart now holds it replaces what was typed.
            expect(field(root, 'From').value).toBe('');
            await enter(field(root, 'Axis'), 'Categories');
            expect(scales).toEqual(['categories']);
        });

        it('ADR-0273: on a category axis the range is one of the axis\' categories, and no axis switch is offered', async () => {
            const { root, ranges } = mounted(chrome, host, categories);
            await settled();
            await press(button(toolbarOf(root), 'Chart Filters'));
            await enter(field(root, 'From'), '3M');
            await enter(field(root, 'To'), '1Y');
            await press(button(root, 'Apply'));
            expect(ranges).toEqual([{ from: 1, to: 3 }]);
            expect(() => field(root, 'Axis')).toThrow(/No field/);
        });

        it('ADR-0281: the menu draws the chart\'s commands inside the chart\'s frame, and a command invoked closes it', async () => {
            const { root } = mounted(chrome, host);
            await settled();
            const menu = await openMenu(root);
            const items = [...menu.querySelectorAll<HTMLElement>('[role=menuitem]')];
            expect(items.map(item => [item.textContent.trim(), enabled(item)])).toEqual([
                ['Reset range', false], ['View as table', true], ['Download CSV', true], ['Download SVG', true], ['Download PNG', true],
            ]);
            const table = items[1];
            if (table === undefined) throw new Error('No second item.');
            await press(table);
            expect(root.querySelector('.ex-chart-menu')).toBeNull();
            expect(root.querySelector('.ex-chart-table')).not.toBeNull();
            const again = await openMenu(root);
            expect([...again.querySelectorAll('[role=menuitem]')][1]?.textContent.trim()).toBe('Hide table');
            await act(async () => {
                again.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
                await Promise.resolve();
            });
            await settled();
            expect(root.querySelector('.ex-chart-menu')).toBeNull();
        });
    });
}
