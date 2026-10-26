// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): Format Cells under the built-in Chrome (ADR-0071), in the grid's popover frame
// (ADR-0050 item 16). What each choice means is the draft's, tested in format-cells-draft.test.ts; here, that Ctrl+1
// opens it over the Selection, that its controls set the draft, and that OK sets what was touched as one step while
// Cancel, Escape and a moved Selection set nothing.
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CellAddress, Culture, SheetEdit, Workbook } from '@ex-grid/sheet-engine';
import { createSheet, type SheetHandle, type SheetOptions } from '../src/create-sheet';
import { SheetWords } from '../src/sheet-words';

const mounted: SheetHandle[] = [];

afterEach(() => {
    for (const sheet of mounted.splice(0)) sheet.destroy();
    document.body.replaceChildren();
});

function sheetOf(entries: Readonly<Record<string, string>>, options: SheetOptions = {}): { readonly element: HTMLElement; readonly sheet: SheetHandle } {
    const workbook = new Workbook(Culture.of('en-US'));
    for (const [address, typed] of Object.entries(entries)) workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse(address), typed));
    const element = document.createElement('div');
    document.body.append(element);
    const sheet = createSheet(element, { viewportWidth: 600, viewportHeight: 400, document: workbook.toDocument(), ...options });
    mounted.push(sheet);
    return { element, sheet };
}

const cellText = (element: HTMLElement, row: number, column: number): string | undefined =>
    element.querySelector(`.ex-viewport > .ex-row[aria-rowindex="${String(row + 2)}"] > [aria-colindex="${String(column + 1)}"]`)?.textContent ?? undefined; // oxlint-disable-line ex-grid/no-stand-in-default -- a cell not painted reads as missing, which the expectation names

function press(element: HTMLElement, row: number, column: number, init: MouseEventInit = {}): void {
    const viewport = element.querySelector<HTMLElement>('.ex-viewport');
    if (viewport === null) throw new Error('The grid has no viewport.');
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0, ...init });
        Object.defineProperty(event, 'offsetX', { value: column * 99 + 20 });
        Object.defineProperty(event, 'offsetY', { value: row * 28 + 5 });
        viewport.dispatchEvent(event);
        if (type === 'mousedown' && !event.defaultPrevented) element.querySelector<HTMLElement>('.ex-scroller')?.focus();
    }
}

function key(name: string, init: KeyboardEventInit = {}): void {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init }));
}

function panelOf(element: HTMLElement): HTMLFormElement {
    const panel = element.querySelector<HTMLFormElement>('.ex-popover-consumer form.ex-format-cells');
    if (panel === null) throw new Error('Format Cells is not open.');
    return panel;
}

function labelled(panel: HTMLElement, text: string): HTMLInputElement {
    const label = [...panel.querySelectorAll('label')].find(each => each.textContent.trim() === text);
    const input = label?.querySelector('input');
    if (input === null || input === undefined) throw new Error(`No control is labelled ${text}.`);
    return input;
}

function choose(input: HTMLInputElement): void {
    input.checked = input.type === 'checkbox' ? !input.checked : true;
    input.dispatchEvent(new Event('change', { bubbles: true }));
}

function typeInto(input: HTMLInputElement, text: string): void {
    input.value = text;
    input.dispatchEvent(new Event('input', { bubbles: true }));
}

function button(panel: HTMLElement, text: string): HTMLButtonElement {
    const found = [...panel.querySelectorAll('button')].find(each => each.textContent === text);
    if (found === undefined) throw new Error(`No button reads ${text}.`);
    return found;
}

const submit = (panel: HTMLFormElement): void => { panel.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })); };

async function opened(entries: Readonly<Record<string, string>>, options: SheetOptions = {}): Promise<{
    readonly element: HTMLElement; readonly sheet: SheetHandle; readonly grid: Element | null;
}> {
    const { element, sheet } = sheetOf(entries, options);
    await vi.waitFor(() => { expect(element.querySelector('.ex-viewport')).not.toBeNull(); });
    press(element, 0, 0);
    const grid = document.activeElement;
    key('1', { ctrlKey: true });
    return { element, sheet, grid };
}

describe('Format Cells (ADR-0071)', () => {
    it('ADR-0071: Ctrl+1 opens it on the Focus cell\'s format, its tab taking the keyboard; OK sets what was touched as one step and gives the keyboard back', async () => {
        const { element, sheet, grid } = await opened({ A1: '0.5' });
        const panel = panelOf(element);
        expect(panel.closest('.ex-popover')?.getAttribute('aria-label')).toBe('Format Cells');
        expect(document.activeElement?.textContent).toBe('Number');
        expect(labelled(panel, 'General').checked).toBe(true);
        expect(labelled(panel, 'Accounting').disabled).toBe(true);
        choose(labelled(panel, 'Percentage'));
        typeInto(labelled(panel, 'Decimal places'), '1');
        submit(panel);
        expect(element.querySelector('.ex-popover-consumer')).toBeNull();
        expect(document.activeElement).toBe(grid);
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).toBe('50.0%'); });
        sheet.undo();
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).toBe('0.5'); });
    });

    it('ADR-0071: Cancel and Escape set nothing, and the next opening shows the tab last shown', async () => {
        const { element, sheet } = await opened({ A1: 'x' });
        let panel = panelOf(element);
        const tabs = panel.querySelectorAll<HTMLButtonElement>('[role="tab"]');
        tabs[0]?.focus();
        key('End');
        expect(document.activeElement?.textContent).toBe('Fill');
        expect(tabs[4]?.getAttribute('aria-selected')).toBe('true');
        choose(labelled(panel, 'No Colour'));
        button(panel, 'Cancel').click();
        expect(element.querySelector('.ex-popover-consumer')).toBeNull();
        key('1', { ctrlKey: true });
        panel = panelOf(element);
        expect(document.activeElement?.textContent).toBe('Fill');
        key('Escape');
        expect(element.querySelector('.ex-popover-consumer')).toBeNull();
        expect(sheet.undo()).toBe(false);
    });

    it('ADR-0071: the Font, Border and Fill tabs set their parts on every range of the Selection in one step', async () => {
        const { element, sheet } = sheetOf({ A1: 'a', B2: 'b' });
        await vi.waitFor(() => { expect(element.querySelector('.ex-viewport')).not.toBeNull(); });
        press(element, 0, 0);
        press(element, 1, 1, { shiftKey: true });
        key('1', { ctrlKey: true });
        const panel = panelOf(element);
        const tab = (name: string): void => { button(panel, name).click(); };
        tab('Font');
        choose(labelled(panel, 'Bold'));
        tab('Border');
        button(panel, 'Outline').click();
        expect(panel.querySelector('[data-edge="top"]')?.getAttribute('aria-pressed')).toBe('true');
        expect(panel.querySelectorAll('.ex-format-cells-preview line').length).toBe(4);
        tab('Fill');
        const yellow = panel.querySelector<HTMLInputElement>('input[aria-label="Yellow"]');
        if (yellow === null) throw new Error('The palette has no Yellow.');
        choose(yellow);
        submit(panel);
        const shown = sheet.workbook.shown;
        const b2 = shown.getCellFormat(CellAddress.parse('B2'));
        expect([b2.font.bold, b2.fill.colour?.toString(), b2.borders.right.style, b2.borders.bottom.style]).toEqual([true, '#FFFF00', 'thin', 'thin']);
        expect(shown.getCellFormat(CellAddress.parse('A1')).borders.top.style).toBe('thin');
        sheet.undo();
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('B2')).font.bold).toBe(false);
        expect(sheet.undo()).toBe(false);
    });

    it('ADR-0071: a Custom code ExSheet does not read is refused by name at OK, and Format Cells stays open until it is put right', async () => {
        const { element, sheet } = await opened({ A1: '5' });
        const panel = panelOf(element);
        choose(labelled(panel, 'Custom'));
        const code = panel.querySelector<HTMLInputElement>('input.ex-format-cells-code');
        if (code === null) throw new Error('Custom has no code field.');
        typeInto(code, '');
        submit(panel);
        expect(panel.isConnected).toBe(true);
        expect(panel.querySelector('[role="alert"]')?.textContent).toBe('Type a number format code under Custom.');
        typeInto(code, '0.000');
        expect(panel.querySelector<HTMLElement>('[role="alert"]')?.hidden).toBe(true);
        submit(panel);
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).toBe('5.000'); });
        expect(sheet.undo()).toBe(true);
    });

    it('ADR-0071: a Selection moved under it ends it, setting nothing; Ctrl+1 while an edit is open opens nothing and says why', async () => {
        const { element, sheet } = await opened({ A1: 'x' });
        choose(labelled(panelOf(element), 'Percentage'));
        const nameBox = element.querySelector<HTMLInputElement>('input.ex-name-box');
        if (nameBox === null) throw new Error('The Sheet has no Name Box.');
        nameBox.focus();
        typeInto(nameBox, 'C3');
        element.querySelector('form.ex-name-box-form')?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
        await vi.waitFor(() => { expect(element.querySelector('.ex-popover-consumer')).toBeNull(); });
        expect(sheet.undo()).toBe(false);
        press(element, 0, 0);
        key('Q');
        await vi.waitFor(() => { expect(element.querySelector('.ex-editor')).not.toBeNull(); });
        key('1', { ctrlKey: true });
        expect(element.querySelector('.ex-popover-consumer')).toBeNull();
        expect(element.querySelector('.ex-sheet-notice')?.textContent).toBe(SheetWords.english.formatKeyWhileEditing);
    });

    it('ADR-0071: another Sheet shown under it ends it, and its OK, arriving after, sets nothing on the Sheet now shown', async () => {
        const workbook = new Workbook(Culture.of('en-US'));
        workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse('A1'), '1'));
        workbook.do('Sheet1', SheetEdit.insertSheet(undefined, undefined));
        const { element, sheet } = await opened({}, { document: workbook.toDocument() });
        const panel = panelOf(element);
        choose(labelled(panel, 'Percentage'));
        key('PageDown', { ctrlKey: true });
        expect(element.querySelector('.ex-popover-consumer')).toBeNull();
        submit(panel);
        expect(sheet.workbook.sheets.map(each => each.getCellFormat(CellAddress.parse('A1')).numberFormat.code)).toEqual(['General', 'General']);
        expect(sheet.undo()).toBe(false);
    });

    it('ADR-0071: an operation under it, a Toolbar press as an undo would, ends it, and its OK, arriving after, sets nothing it read before', async () => {
        const { element, sheet } = await opened({ A1: '0.5' }, { showToolbar: true });
        const panel = panelOf(element);
        choose(labelled(panel, 'Percentage'));
        element.querySelector<HTMLButtonElement>('.ex-sheet-toolbar-item[aria-label="Bold"]')?.click();
        expect(element.querySelector('.ex-popover-consumer')).toBeNull();
        submit(panel);
        const a1 = sheet.workbook.shown.getCellFormat(CellAddress.parse('A1'));
        expect([a1.font.bold, a1.numberFormat.code]).toEqual([true, 'General']);
    });

    it('ADR-0100: the Toolbar\'s Format Cells… opens it, from the keys as from a press, and it keeps the keyboard', async () => {
        const { element } = sheetOf({ A1: 'x' }, { showToolbar: true });
        await vi.waitFor(() => { expect(element.querySelector('.ex-viewport')).not.toBeNull(); });
        press(element, 0, 0);
        const band = element.querySelector<HTMLElement>('.ex-sheet-toolbar');
        band?.focus();
        key('End');
        key('Enter');
        expect(panelOf(element).contains(document.activeElement)).toBe(true);
        key('Escape');
        element.querySelector<HTMLButtonElement>('.ex-sheet-toolbar-item[aria-label="Format Cells"]')?.click();
        expect(panelOf(element).contains(document.activeElement)).toBe(true);
    });
});
