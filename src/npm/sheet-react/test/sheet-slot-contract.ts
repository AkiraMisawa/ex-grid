// The contract every Wrapper keeps with ExSheet's own slots (ADR-0390 section 7): run at layer 2 against each one
// alike, the plain test Chrome here and each design system's Wrapper. A Toolbar Item is named, pressed and enabled as
// the Sheet says and takes no keyboard on a press; its list is the Sheet's choices in the Sheet's order, opened on the
// toolbar's request with the keyboard in it, and a choice or Escape is handed back with the keyboard on the Sheet.
// Format Cells draws the offer's tabs in the Sheet's words, the tab shown taking the keyboard, sets every choice
// through the draft, and shows the refusal an OK meets. A Sheet Tab's face shows its Sheet's name. The Wrapper decides
// none of it.
import { createElement, createRef } from 'react';
import { act, cleanup, fireEvent, render, within } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { CellAddress, Culture, SheetEdit, Workbook } from '@ex-grid/sheet-engine';
import type { ContractHost } from '../../react/test/contract-host';
import { ExSheet, type ExSheetApi, type ExSheetChrome, type ExSheetProps } from '../src/ex-sheet';

/** Lets the Sheet's pass, the grid's and the render they ask of React run. */
async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 4; turn++) await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

function active(): HTMLElement {
    const holder = document.activeElement;
    if (!(holder instanceof HTMLElement)) throw new Error('Nothing holds the keyboard.');
    return holder;
}

function key(name: string, init: KeyboardEventInit = {}): void {
    fireEvent.keyDown(active(), { key: name, ...init });
}

/** A Workbook whose first Sheet holds `entries`, with a second Sheet where `second` is given. */
function documentOf(entries: Readonly<Record<string, string>>, second?: string): ReturnType<Workbook['toDocument']> {
    const workbook = new Workbook(Culture.of('en-US'), second === undefined ? ['Sheet1'] : ['Sheet1', second]);
    for (const [address, typed] of Object.entries(entries)) workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse(address), typed));
    return workbook.toDocument();
}

interface Mounted {
    readonly element: HTMLElement;
    readonly grid: HTMLElement;
    readonly sheet: ExSheetApi;
}

async function mounted(chrome: ExSheetChrome, host: ContractHost, props: Partial<ExSheetProps> = {}): Promise<Mounted> {
    const ref = createRef<ExSheetApi>();
    const { container } = render(createElement(ExSheet, {
        locale: 'en-US', viewportWidth: 720, viewportHeight: 360, document: documentOf({ A1: '0.5', A2: 'x' }), chrome, ref, ...props,
    }), host);
    await settled();
    const element = container.querySelector<HTMLElement>('.ex-sheet');
    const grid = element?.querySelector<HTMLElement>('[role=grid]');
    if (element === null || grid === null || grid === undefined || ref.current === null) throw new Error('No Sheet was drawn.');
    return { element, grid, sheet: ref.current };
}

/** A press on A1, where the keyboard then is: the Row Headings stand at the Viewport's left edge. */
async function pressA1(element: HTMLElement): Promise<void> {
    const viewport = element.querySelector<HTMLElement>('.ex-viewport');
    if (viewport === null) throw new Error('The grid has no viewport.');
    const heading = /width: ([\d.]+)px/u.exec(element.querySelector('.ex-row-heading')?.getAttribute('style') ?? ''); // oxlint-disable-line ex-grid/no-stand-in-default -- no heading band reads as width 0, which the press then starts from
    const headingPx = heading?.[1] === undefined ? 0 : Number(heading[1]);
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
        Object.defineProperty(event, 'offsetX', { value: headingPx + 20 });
        Object.defineProperty(event, 'offsetY', { value: 5 });
        viewport.dispatchEvent(event);
        if (type === 'mousedown' && !event.defaultPrevented) element.querySelector<HTMLElement>('.ex-scroller')?.focus();
    }
    await settled();
}

const a1 = CellAddress.parse('A1');

/** The control a Toolbar Item drew under its name. */
function itemNamed(element: HTMLElement, name: string): HTMLElement {
    const found = [...element.querySelectorAll<HTMLElement>('.ex-sheet-toolbar-slot [aria-label]')].find(each => each.getAttribute('aria-label') === name);
    if (found === undefined) throw new Error(`The Chrome drew no Toolbar Item named ${name}.`);
    return found;
}

/** The list a Toolbar Item drew under its name. */
function listNamed(element: HTMLElement, name: string): HTMLElement | null {
    return element.querySelector<HTMLElement>(`[role=menu][aria-label="${name}"]`);
}

/** Moves the band's keyboard onto the item named `name`, from the band's first. */
async function bandOn(element: HTMLElement, name: string): Promise<void> {
    const band = element.querySelector<HTMLElement>('.ex-sheet-toolbar');
    if (band === null) throw new Error('No Sheet Toolbar stands.');
    band.focus();
    const slot = itemNamed(element, name).closest('.ex-sheet-toolbar-slot');
    for (let step = 0; step < 30 && band.getAttribute('aria-activedescendant') !== slot?.id; step++) key('ArrowRight');
    if (band.getAttribute('aria-activedescendant') !== slot?.id) throw new Error(`The band's keys never reached ${name}.`);
    await settled();
}

/** Registers the contract's tests for the Sheet Toolbar's items against a Wrapper's Chrome, which fills `toolbarItem`. */
export function toolbarItemSlotContract(name: string, chrome: ExSheetChrome, host: ContractHost = {}): void {
    describe(`${name}: the Toolbar Item slot's contract (ADR-0390 section 7)`, () => {
        afterEach(() => { cleanup(); });

        it('ADR-0100: a toggle is named and pressed as the Sheet says, takes no keyboard and no Tab, and a press sets the Focus cell\'s format', async () => {
            const { element, grid, sheet } = await mounted(chrome, host, { showToolbar: true });
            await pressA1(element);
            const bold = itemNamed(element, 'Bold');
            expect([bold.getAttribute('aria-pressed'), bold.tabIndex, bold.getAttribute('data-ex-claims')?.split(' ').includes('click')]).toEqual(['false', -1, true]);
            expect(fireEvent.mouseDown(bold)).toBe(false);
            fireEvent.click(bold);
            await settled();
            expect([sheet.cellFormatAt(a1).font.bold, itemNamed(element, 'Bold').getAttribute('aria-pressed'), grid.contains(active())]).toEqual([true, 'true', true]);
        });

        it('ADR-0100: the toolbar\'s ↓ opens a menu\'s list with the keyboard on the Focus cell\'s choice, in the Sheet\'s order; Enter makes one and gives the keyboard back', async () => {
            const { element, grid, sheet } = await mounted(chrome, host, { showToolbar: true });
            await pressA1(element);
            await bandOn(element, 'Number Format');
            key('ArrowDown');
            await settled();
            const list = listNamed(element, 'Number Format');
            if (list === null) throw new Error('The toolbar\'s ↓ opened no list.');
            const choices = within(list).getAllByRole('menuitemradio');
            expect(choices.map(choice => choice.textContent.trim()).slice(0, 4)).toEqual(['General', 'Number', 'Currency', 'Accounting']);
            expect([active(), active().getAttribute('aria-checked')]).toEqual([choices[0], 'true']);
            for (let step = 0; step < 12 && active().textContent.trim() !== 'Percentage'; step++) key('ArrowDown');
            key('Enter');
            await settled();
            expect([listNamed(element, 'Number Format'), sheet.cellFormatAt(a1).numberFormat.code, grid.contains(active())]).toEqual([null, '0.00%', true]);
        });

        it('ADR-0100: a colour list moves ten across; Escape closes it choosing nothing, with the keyboard back on the Sheet', async () => {
            const { element, grid, sheet } = await mounted(chrome, host, { showToolbar: true });
            await pressA1(element);
            const more = itemNamed(element, 'Fill Colour, more');
            fireEvent.click(more);
            await settled();
            const list = listNamed(element, 'Fill Colour');
            if (list === null) throw new Error('A press on the arrow opened no list.');
            expect(active().getAttribute('aria-label') ?? active().textContent.trim()).toBe('No Fill');
            key('ArrowDown');
            const first = active().getAttribute('aria-label');
            key('ArrowDown');
            const below = active().getAttribute('aria-label');
            const named = within(list).getAllByRole('menuitemradio').map(choice => choice.getAttribute('aria-label'));
            expect(named.indexOf(below) - named.indexOf(first)).toBe(10);
            key('Escape');
            await settled();
            expect([listNamed(element, 'Fill Colour'), sheet.cellFormatAt(a1).fill.colour, grid.contains(active())]).toEqual([null, undefined, true]);
        });
    });
}

/** Registers the contract's tests for Format Cells against a Wrapper's Chrome, which fills `formatCells`. */
export function formatCellsSlotContract(name: string, chrome: ExSheetChrome, host: ContractHost = {}): void {
    describe(`${name}: Format Cells' slot's contract (ADR-0390 section 7)`, () => {
        afterEach(() => { cleanup(); });

        it('ADR-0071: Ctrl+1 draws the offer\'s tabs in its order and the Sheet\'s words, in the grid\'s frame, and the tab shown takes the keyboard', async () => {
            const { element } = await mounted(chrome, host);
            await pressA1(element);
            key('1', { ctrlKey: true });
            await settled();
            const frame = element.querySelector<HTMLElement>('.ex-popover-consumer');
            if (frame === null) throw new Error('Ctrl+1 opened no Format Cells.');
            const tabs = within(frame).getAllByRole('tab');
            expect(tabs.map(tab => tab.textContent.trim())).toEqual(['Number', 'Alignment', 'Font', 'Border', 'Fill']);
            expect([active(), active().getAttribute('aria-selected')]).toEqual([tabs[0], 'true']);
        });

        it('ADR-0071: a choice is set through the draft and OK sets it as one step; Format Cells goes and the keyboard is back on the grid', async () => {
            const { element, grid, sheet } = await mounted(chrome, host);
            await pressA1(element);
            key('1', { ctrlKey: true });
            await settled();
            const frame = (): HTMLElement => {
                const found = element.querySelector<HTMLElement>('.ex-popover-consumer');
                if (found === null) throw new Error('Format Cells is not open.');
                return found;
            };
            fireEvent.click(within(frame()).getByRole('tab', { name: 'Font' }));
            await settled();
            fireEvent.click(within(frame()).getByRole('radio', { name: 'Bold' }));
            await settled();
            fireEvent.click(within(frame()).getByRole('button', { name: 'OK' }));
            await settled();
            expect([element.querySelector('.ex-popover-consumer'), sheet.cellFormatAt(a1).font.bold, sheet.canUndo, grid.contains(active())]).toEqual([null, true, true, true]);
        });

        it('ADR-0071: an OK the draft refuses leaves Format Cells open, saying why in the Sheet\'s words; Cancel sets nothing', async () => {
            const { element, grid, sheet } = await mounted(chrome, host);
            await pressA1(element);
            key('1', { ctrlKey: true });
            await settled();
            const frame = (): HTMLElement => {
                const found = element.querySelector<HTMLElement>('.ex-popover-consumer');
                if (found === null) throw new Error('Format Cells is not open.');
                return found;
            };
            fireEvent.click(within(frame()).getByRole('radio', { name: 'Custom' }));
            await settled();
            const code = within(frame()).getByRole('textbox', { name: 'Type' });
            fireEvent.focus(code);
            fireEvent.change(code, { target: { value: '' } });
            await settled();
            fireEvent.click(within(frame()).getByRole('button', { name: 'OK' }));
            await settled();
            expect(within(frame()).getByRole('alert').textContent).toBe('Type a number format code under Custom.');
            fireEvent.click(within(frame()).getByRole('button', { name: 'Cancel' }));
            await settled();
            expect([element.querySelector('.ex-popover-consumer'), sheet.cellFormatAt(a1).numberFormat.code, sheet.canUndo, grid.contains(active())])
                .toEqual([null, 'General', false, true]);
        });
    });
}

/** Registers the contract's tests for the Sheet Tabs' faces against a Wrapper's Chrome, which fills `sheetTab`. */
export function sheetTabSlotContract(name: string, chrome: ExSheetChrome, host: ContractHost = {}): void {
    describe(`${name}: the Sheet Tab slot's contract (ADR-0390 section 7)`, () => {
        afterEach(() => { cleanup(); });

        it('ADR-0331 §4: each face shows its Sheet\'s name in the tab the Sheet keeps, and a press on a tab shows its Sheet', async () => {
            const { element, sheet } = await mounted(chrome, host, { document: documentOf({ A1: 'x' }, 'Second') });
            const tabs = (): HTMLElement[] => [...element.querySelectorAll<HTMLElement>('.ex-sheet-tab')];
            expect(tabs().map(tab => [tab.textContent.trim(), tab.getAttribute('aria-selected'), tab.querySelector('.ex-sheet-tab-face') !== null]))
                .toEqual([['Sheet1', 'true', true], ['Second', 'false', true]]);
            const second = tabs()[1];
            if (second === undefined) throw new Error('The second tab is not drawn.');
            fireEvent.mouseDown(second);
            fireEvent.click(second);
            await settled();
            expect([tabs().map(tab => tab.getAttribute('aria-selected')), sheet.workbook.shown.name, tabs()[1]?.textContent.trim()]).toEqual([['false', 'true'], 'Second', 'Second']);
        });
    });
}
