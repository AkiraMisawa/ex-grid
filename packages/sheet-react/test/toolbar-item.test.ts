// @vitest-environment happy-dom
// useToolbarItem (ADR-0100, ADR-0390): what the hook decides for every Chrome that draws with it, run against a Chrome
// of plain controls drawn with it alone. The slot contract's cases, which the Wrappers run, hold the toolbar's keys
// and a choice; these hold what only the hook does: a press outside the item closes its list, handing the keyboard
// back only when the list held it, a second press on the control it opens from closes it, a choice that cannot be made
// is not made, and the swatches stand ten to a row.
import { createElement, createRef, type ReactElement } from 'react';
import { act, cleanup, fireEvent, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { CellAddress, Culture, SheetEdit, Workbook } from '@ex-grid/sheet-engine';
import type { SheetToolbarItemSlotContext } from '@ex-grid/sheet';
import { ExSheet, type ExSheetApi, type ExSheetChrome } from '../src/ex-sheet';
import { useToolbarItem } from '../src/toolbar-item';
import { pressA1 } from './sheet-slot-contract';

afterEach(() => { cleanup(); });

async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 4; turn++) await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

/** A Toolbar Item of plain controls, drawn with the hook alone. */
function HookItem({ context }: { readonly context: SheetToolbarItemSlotContext }): ReactElement {
    const item = useToolbarItem(context);
    const { view, control } = item;
    const button = (name: string, press: () => void): ReactElement => createElement('button', {
        ...control, key: name, type: 'button', 'aria-label': name, onClick: press,
    }, name);
    const controls = context.kind === 'split'
        ? [button(view.name, () => { context.invoke(); }), button(view.moreName ?? view.name, item.toggleList)]
        : [button(view.name, item.hasList ? item.toggleList : () => { context.invoke(); })];
    const option = (index: number): ReactElement => createElement('button', { ...item.choice(index), key: index, type: 'button' }, view.choices[index]?.label);
    return createElement('span', { ref: item.item, className: 'hook-item' }, ...controls,
        item.open ? createElement('div', item.list, ...item.rows.map((row, at) => (typeof row === 'number'
            ? option(row)
            : createElement('div', { key: `s${String(at)}`, className: 'hook-swatches' }, ...row.map(option))))) : null);
}

const chrome: ExSheetChrome = { toolbarItem: context => createElement(HookItem, { context }) };

async function mounted(): Promise<{ readonly element: HTMLElement; readonly grid: HTMLElement; readonly sheet: ExSheetApi }> {
    const workbook = new Workbook(Culture.of('en-US'), ['Sheet1']);
    workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse('A1'), '0.5'));
    const ref = createRef<ExSheetApi>();
    const { container } = render(createElement(ExSheet, {
        locale: 'en-US', viewportWidth: 720, viewportHeight: 360, showToolbar: true, document: workbook.toDocument(), chrome, ref,
    }));
    await settled();
    const element = container.querySelector<HTMLElement>('.ex-sheet');
    const grid = element?.querySelector<HTMLElement>('[role=grid]');
    if (element === null || grid === null || grid === undefined || ref.current === null) throw new Error('No Sheet was drawn.');
    await pressA1(element);
    return { element, grid, sheet: ref.current };
}

function named(element: HTMLElement, name: string): HTMLElement {
    const found = element.querySelector<HTMLElement>(`.hook-item [aria-label="${name}"]`);
    if (found === null) throw new Error(`No control named ${name}.`);
    return found;
}

const listOf = (element: HTMLElement, name: string): HTMLElement | null => element.querySelector<HTMLElement>(`[role=menu][aria-label="${name}"]`);

function openList(element: HTMLElement, name: string): HTMLElement {
    const list = listOf(element, name);
    if (list === null) throw new Error(`The list of ${name} is not open.`);
    return list;
}

describe('useToolbarItem', () => {
    it('ADR-0100: a press outside the item closes the list it holds the keyboard in, choosing nothing, and the keyboard goes back to the Sheet', async () => {
        const { element, grid, sheet } = await mounted();
        fireEvent.click(named(element, 'Fill Colour, more'));
        await settled();
        expect(listOf(element, 'Fill Colour')?.contains(document.activeElement)).toBe(true);
        fireEvent.mouseDown(document.body);
        await settled();
        expect([listOf(element, 'Fill Colour'), sheet.cellFormatAt(CellAddress.parse('A1')).fill.colour, grid.contains(document.activeElement)])
            .toEqual([null, undefined, true]);
    });

    it('ADR-0100: a press inside the item keeps its list, and a second press on the control it opens from closes it', async () => {
        const { element } = await mounted();
        const arrow = named(element, 'Fill Colour, more');
        fireEvent.click(arrow);
        await settled();
        fireEvent.mouseDown(arrow);
        await settled();
        const kept = listOf(element, 'Fill Colour') !== null;
        fireEvent.click(arrow);
        await settled();
        expect([kept, listOf(element, 'Fill Colour')]).toEqual([true, null]);
    });

    it('ADR-0100: the swatches stand ten to a row, and a choice that cannot be made says why and is not made', async () => {
        const { element, sheet } = await mounted();
        fireEvent.click(named(element, 'Fill Colour, more'));
        await settled();
        const rows = [...openList(element, 'Fill Colour').querySelectorAll('.hook-swatches')].map(row => row.children.length);
        expect(rows.length > 0 && rows.every(count => count === 10)).toBe(true);
        fireEvent.mouseDown(document.body);
        fireEvent.click(named(element, 'Number Format'));
        await settled();
        const refused = openList(element, 'Number Format').querySelector<HTMLElement>('[aria-disabled=true]');
        if (refused === null) throw new Error('The Number Format list offers no choice that cannot be made.');
        fireEvent.click(refused);
        await settled();
        expect([refused.title.startsWith(`${refused.textContent}: `), listOf(element, 'Number Format') !== null, sheet.cellFormatAt(CellAddress.parse('A1')).numberFormat.code])
            .toEqual([true, true, 'General']);
    });
});
