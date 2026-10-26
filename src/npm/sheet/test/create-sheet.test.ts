// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): ExSheet drawn by ExGrid as its Consumer (ADR-0046), over the engine. What the grid
// paints is the core's, tested there; here, that the Sheet answers the grid's intents as one step each on one undo
// stack (ADR-0048), hands back new instances only for the rows a change names (ADR-0003), and says in its own words
// what it refuses (ADR-0370 section 10).
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CellAddress, Culture, SheetEdit, SheetRefusedError, Workbook } from '@ex-grid/sheet-engine';
import { createSheet, type SheetHandle, type SheetOptions } from '../src/create-sheet';

const mounted: SheetHandle[] = [];

afterEach(() => {
    for (const sheet of mounted.splice(0)) sheet.destroy();
    document.body.replaceChildren();
});

function sheetOf(options: SheetOptions = {}): { readonly element: HTMLElement; readonly sheet: SheetHandle } {
    const element = document.createElement('div');
    document.body.append(element);
    const sheet = createSheet(element, { locale: 'en-US', viewportWidth: 600, viewportHeight: 300, ...options });
    mounted.push(sheet);
    return { element, sheet };
}

/** A Workbook of one Sheet holding `entries`, as a document. */
function documentOf(entries: Readonly<Record<string, string>>): ReturnType<Workbook['toDocument']> {
    const workbook = new Workbook(Culture.of('en-US'));
    for (const [address, typed] of Object.entries(entries)) workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse(address), typed));
    return workbook.toDocument();
}

const cellAt = (element: HTMLElement, row: number, column: number): HTMLElement | null =>
    element.querySelector<HTMLElement>(`.ex-viewport > .ex-row[aria-rowindex="${String(row + 2)}"] > [aria-colindex="${String(column + 1)}"]`);

const textAt = (element: HTMLElement, row: number, column: number): string | undefined => cellAt(element, row, column)?.textContent ?? undefined; // oxlint-disable-line ex-grid/no-stand-in-default -- a cell not painted reads as missing, which the expectation names

/** A press on the cell at `row`, `column` of a default Sheet, where the keyboard then is. */
function press(element: HTMLElement, row: number, column: number): void {
    const viewport = element.querySelector<HTMLElement>('.ex-viewport');
    if (viewport === null) throw new Error('The grid has no viewport.');
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
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

describe('createSheet (ADR-0046, ADR-0370 section 1)', () => {
    it('ADR-0050 item 11: a Formula\'s number in General is fitted to Excel\'s default column, and text and numbers align as Excel\'s', async () => {
        const { element } = sheetOf({ document: documentOf({ A1: '=1/3', B1: 'label', C1: '42' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('0.333333'); });
        expect(cellAt(element, 0, 0)?.className).toBe('ex-cell ex-cell-numeric');
        expect(cellAt(element, 0, 0)?.getAttribute('aria-label')).toBe('0.333333333333333');
        expect(textAt(element, 0, 1)).toBe('label');
        expect(cellAt(element, 0, 1)?.className).toBe('ex-cell');
        expect(textAt(element, 0, 2)).toBe('42');
        expect(element.querySelector('.ex-header-cell')?.textContent).toBe('A');
    });

    it('ADR-0048: an edit is one step, a Formula depending on it repaints, and Ctrl+Z puts both back', async () => {
        const documents: unknown[] = [];
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '2', A2: '=A1*10' }), onWorkbookChanged: document => { documents.push(document); } });
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('20'); });
        const untouched = cellAt(element, 2, 0);
        press(element, 0, 0);
        key('F2');
        const editor = element.querySelector<HTMLInputElement>('input.ex-editor');
        expect(editor?.value).toBe('2');
        if (editor === null) throw new Error('The editor opened.');
        editor.value = '5';
        editor.dispatchEvent(new Event('input', { bubbles: true }));
        key('Enter');
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('50'); });
        expect(textAt(element, 0, 0)).toBe('5');
        // A row the change did not name is not painted again (ADR-0003).
        expect(cellAt(element, 2, 0)).toBe(untouched);
        expect(documents).toHaveLength(1);
        expect(sheet.undo()).toBe(true);
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('20'); });
        expect(textAt(element, 0, 0)).toBe('2');
        expect(sheet.redo()).toBe(true);
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('50'); });
    });

    it('ADR-0034: a Formula the engine cannot read holds the editor, with the engine\'s reason in the Sheet\'s words', async () => {
        const { element } = sheetOf();
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        press(element, 0, 0);
        key('F2');
        const editor = element.querySelector<HTMLInputElement>('input.ex-editor');
        if (editor === null) throw new Error('The editor opened.');
        editor.value = '=1+';
        editor.dispatchEvent(new Event('input', { bubbles: true }));
        key('Enter');
        await vi.waitFor(() => { expect(element.querySelector('.ex-announce')?.textContent).toMatch(/cannot be read/u); });
        expect(element.querySelector('input.ex-editor')).not.toBeNull();
    });

    it('ADR-0054: Delete clears the Entries under the Selection as one step, and its formats stay', async () => {
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '1', A2: '=A1+1' }) });
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('2'); });
        press(element, 0, 0);
        key('Delete');
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('1'); });
        expect(textAt(element, 0, 0)).toBe('');
        sheet.undo();
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('2'); });
    });

    it('ADR-0370 section 10: the notice speaks the Sheet\'s words, Japanese chosen in one line, a Consumer\'s label first', () => {
        const { element, sheet } = sheetOf({ japanese: true });
        expect(sheet.undo()).toBe(false);
        const japanese = element.querySelector('.ex-sheet-notice')?.textContent;
        expect(japanese).not.toBe('');
        expect(japanese).not.toMatch(/[A-Za-z]{4}/u);
        sheet.update({ locale: 'en-US', viewportWidth: 600, viewportHeight: 300, japanese: true, label: id => (id === 'nothingToUndo' ? 'Rien à annuler.' : undefined) });
        sheet.undo();
        expect(element.querySelector('.ex-sheet-notice')?.textContent).toBe('Rien à annuler.');
    });

    it('ADR-0048: a Sheet started empty takes its culture once; a different one on a live Sheet is refused by name', () => {
        const { sheet } = sheetOf();
        // The culture it holds named again, in another spelling, changes nothing.
        sheet.update({ locale: 'en-us', viewportWidth: 600, viewportHeight: 300 });
        expect(() => { sheet.update({ locale: 'de-DE' }); }).toThrow(/culture is recorded in its document/u);
        expect(() => createSheet(document.createElement('div'), { locale: 'xx-XX' })).toThrow(/xx-XX/u);
    });
});

describe('Widening (ADR-0047, second round)', () => {
    it('SH-20: a number typed into a column too narrow for it widens the column, in the same step as the entry', async () => {
        const { element, sheet } = sheetOf();
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        const widthOfA = (): string | undefined => element.querySelector<HTMLElement>('.ex-header-cell')?.style.width;
        const before = widthOfA();
        press(element, 0, 0);
        key('F2');
        const editor = element.querySelector<HTMLInputElement>('input.ex-editor');
        if (editor === null) throw new Error('The editor opened.');
        editor.value = '1234567890';
        editor.dispatchEvent(new Event('input', { bubbles: true }));
        key('Enter');
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('1234567890'); });
        expect(widthOfA()).not.toBe(before);
        sheet.undo();
        await vi.waitFor(() => { expect(widthOfA()).toBe(before); });
        expect(textAt(element, 0, 0)).toBe('');
    });
});

const nameBox = (element: HTMLElement): HTMLInputElement => {
    const box = element.querySelector<HTMLInputElement>('input.ex-name-box');
    if (box === null) throw new Error('The Sheet shows no Name Box.');
    return box;
};
const barText = (element: HTMLElement): HTMLInputElement => {
    const text = element.querySelector<HTMLInputElement>('input.ex-formula-bar-text');
    if (text === null) throw new Error('The Sheet shows no Formula Bar.');
    return text;
};

const enter = (element: HTMLElement, typed: string): void => {
    const box = nameBox(element);
    box.focus();
    box.value = typed;
    box.dispatchEvent(new Event('input', { bubbles: true }));
    element.querySelector('form.ex-name-box-form')?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
};

describe('The Formula Bar and the Name Box (ADR-0051)', () => {
    it('ADR-0051: the bar shows the Focus cell\'s Formula and the Name Box its address; an address typed there selects it', async () => {
        const { element } = sheetOf({ document: documentOf({ A1: '2', B3: '=A1*10' }) });
        await vi.waitFor(() => { expect(textAt(element, 2, 1)).toBe('20'); });
        press(element, 2, 1);
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('B3'); });
        expect(barText(element).value).toBe('=A1*10');
        enter(element, 'c5:d6');
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('C5'); });
        enter(element, 'nowhere at all');
        await vi.waitFor(() => { expect(element.querySelector('.ex-sheet-notice')?.textContent).toMatch(/nowhere at all/u); });
        expect(nameBox(element).value).toBe('C5');
    });

    it('ADR-0125: a cell an array spills into shows its Anchor\'s Formula in the bar, dimmed and read only', async () => {
        const { element } = sheetOf({ document: documentOf({ A1: '=SEQUENCE(3)' }) });
        await vi.waitFor(() => { expect(textAt(element, 2, 0)).toBe('3'); });
        press(element, 2, 0);
        await vi.waitFor(() => { expect(barText(element).value).toBe('=SEQUENCE(3)'); });
        expect(barText(element).readOnly).toBe(true);
        expect(barText(element).classList.contains('ex-formula-bar-borrowed')).toBe(true);
    });

    it('ADR-0048: an undo selects what the step changed, as Excel\'s does', async () => {
        const { element, sheet } = sheetOf({ document: documentOf({ C4: '1' }) });
        await vi.waitFor(() => { expect(textAt(element, 3, 2)).toBe('1'); });
        press(element, 3, 2);
        key('Delete');
        await vi.waitFor(() => { expect(textAt(element, 3, 2)).toBe(''); });
        press(element, 0, 0);
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('A1'); });
        sheet.undo();
        await vi.waitFor(() => { expect(textAt(element, 3, 2)).toBe('1'); });
        expect(nameBox(element).value).toBe('C4');
    });
});

describe('The Sheet as a component of state (ADR-0048, ADR-0331)', () => {
    it('ADR-0048: a document the Sheet raised, handed back as a controlled component\'s state, keeps its undo stack', async () => {
        let latest: ReturnType<Workbook['toDocument']> | undefined;
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '1' }), onWorkbookChanged: raised => { latest = raised; } });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('1'); });
        press(element, 0, 0);
        key('Delete');
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe(''); });
        if (latest === undefined) throw new Error('The Sheet raised no document.');
        sheet.update({ locale: 'en-US', viewportWidth: 600, viewportHeight: 300, document: latest, onWorkbookChanged: raised => { latest = raised; } });
        expect(sheet.undo()).toBe(true);
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('1'); });
    });

    it('ADR-0331: an address on another Sheet typed into the Name Box shows that Sheet, and the Workbook records it as shown', async () => {
        const workbook = new Workbook(Culture.of('en-US'), ['Sheet1', 'Second']);
        workbook.do('Second', SheetEdit.enter(CellAddress.parse('B2'), 'there'));
        const documents: ReturnType<Workbook['toDocument']>[] = [];
        const { element, sheet } = sheetOf({ document: workbook.toDocument(), onWorkbookChanged: raised => { documents.push(raised); } });
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        enter(element, 'Second!B2');
        await vi.waitFor(() => { expect(textAt(element, 1, 1)).toBe('there'); });
        expect(nameBox(element).value).toBe('B2');
        expect(sheet.workbook.shown.name).toBe('Second');
        expect(documents.at(-1)?.shown).toBe('Second');
    });

    it('ADR-0048: an undo asked for while a cell is edited is refused by name, and changes nothing', async () => {
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '1' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('1'); });
        press(element, 0, 0);
        key('Delete');
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe(''); });
        key('F2');
        expect(() => sheet.undo()).toThrow(SheetRefusedError);
        expect(textAt(element, 0, 0)).toBe('');
    });
});

describe('Ctrl+arrow (ADR-0050, item 2)', () => {
    it('ADR-0050 item 2: Ctrl+arrow stops where the data ends, as Excel\'s does, and follows an edit', async () => {
        const { element } = sheetOf({ document: documentOf({ A1: '1', A2: '2', A3: '3', A7: '7' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('1'); });
        press(element, 0, 0);
        key('ArrowDown', { ctrlKey: true });
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('A3'); });
        key('ArrowDown', { ctrlKey: true });
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('A7'); });
        key('Delete');
        await vi.waitFor(() => { expect(textAt(element, 6, 0)).toBe(''); });
        key('ArrowUp', { ctrlKey: true });
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('A3'); });
        // End, which resolves to the same move, keeps the grid's meaning: the row's last column.
        key('End', { ctrlKey: false });
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('XFD3'); });
    });
});

const tabNames = (element: HTMLElement): string[] => [...element.querySelectorAll('.ex-sheet-tab')].map(tab => tab.textContent);
const selectedTab = (element: HTMLElement): string | undefined => element.querySelector('.ex-sheet-tab[aria-selected="true"]')?.textContent ?? undefined; // oxlint-disable-line ex-grid/no-stand-in-default -- no tab selected reads as missing, which the expectation names
const noticeOf = (element: HTMLElement): string | undefined => element.querySelector('.ex-sheet-notice')?.textContent ?? undefined; // oxlint-disable-line ex-grid/no-stand-in-default -- no notice reads as missing, which the expectation names

function click(target: Element | null | undefined): void {
    if (target === null || target === undefined) throw new Error('Nothing to press.');
    const down = new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0 });
    target.dispatchEvent(down);
    expect(down.defaultPrevented).toBe(true);
    target.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, button: 0 }));
}

function twoSheets(): ReturnType<Workbook['toDocument']> {
    const workbook = new Workbook(Culture.of('en-US'), ['Sheet1', 'Second']);
    workbook.do('Second', SheetEdit.enter(CellAddress.parse('A1'), 'there'));
    return workbook.toDocument();
}

describe('the Sheet Tabs (ADR-0331)', () => {
    it('ADR-0331 §4: a tab per Sheet under the grid, in the Formula Bar\'s height; a press shows its Sheet and leaves the keyboard in the grid', async () => {
        const { element, sheet } = sheetOf({ document: twoSheets() });
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        expect(tabNames(element)).toEqual(['Sheet1', 'Second']);
        expect(selectedTab(element)).toBe('Sheet1');
        const list = element.querySelector('.ex-sheet-tabs');
        expect([list?.getAttribute('role'), list?.getAttribute('aria-label')]).toEqual(['tablist', 'Sheets']);
        expect([...element.querySelectorAll('.ex-sheet-tab')].map(tab => tab.getAttribute('tabindex'))).toEqual(['0', '-1']);
        // The band comes off the Sheet's height, as tall as the Formula Bar.
        const area = element.querySelector<HTMLElement>('.ex-sheet-tab-area');
        const bar = element.querySelector<HTMLElement>('.ex-formula-bar');
        expect(area?.style.height).toBe(bar?.style.height);
        expect(element.firstElementChild?.nextElementSibling).toBe(area);
        press(element, 0, 0);
        const grid = document.activeElement;
        click(element.querySelectorAll('.ex-sheet-tab')[1]);
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('there'); });
        expect([sheet.workbook.shown.name, selectedTab(element), document.activeElement]).toEqual(['Second', 'Second', grid]);
    });

    it('ADR-0331 §1: each Sheet keeps the Selection it was left with, given back when its tab is selected again; a Sheet not yet shown opens on A1', async () => {
        const { element } = sheetOf({ document: twoSheets() });
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        press(element, 2, 1);
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('B3'); });
        key('PageDown', { ctrlKey: true });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('there'); });
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('A1'); });
        press(element, 4, 0);
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('A5'); });
        key('PageUp', { ctrlKey: true });
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('B3'); });
        click(element.querySelectorAll('.ex-sheet-tab')[1]);
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('A5'); });
    });

    it('ADR-0331 §3: + adds a Sheet after the one shown and shows it, saying so; one undo takes it out and shows the Sheet it was added from', async () => {
        const documents: ReturnType<Workbook['toDocument']>[] = [];
        const { element, sheet } = sheetOf({ document: twoSheets(), onWorkbookChanged: raised => { documents.push(raised); } });
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        click(element.querySelector('.ex-sheet-tab-add'));
        expect(tabNames(element)).toEqual(['Sheet1', 'Sheet2', 'Second']);
        expect(selectedTab(element)).toBe('Sheet2');
        expect(noticeOf(element)).toBe("Sheet 'Sheet2' was added.");
        expect(documents.at(-1)?.shown).toBe('Sheet2');
        expect(sheet.undo()).toBe(true);
        expect(tabNames(element)).toEqual(['Sheet1', 'Second']);
        expect(selectedTab(element)).toBe('Sheet1');
        expect(sheet.redo()).toBe(true);
        expect(selectedTab(element)).toBe('Sheet2');
    });

    it('ADR-0331 §4: Ctrl+PageDown and Ctrl+PageUp step through the Sheets, never past the last or the first, and Shift+F11 adds one before', async () => {
        const { element, sheet } = sheetOf({ document: twoSheets() });
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        press(element, 0, 0);
        key('PageDown', { ctrlKey: true });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('there'); });
        key('PageDown', { ctrlKey: true });
        expect(sheet.workbook.shown.name).toBe('Second');
        key('PageUp', { ctrlKey: true });
        expect(sheet.workbook.shown.name).toBe('Sheet1');
        key('PageUp', { ctrlKey: true });
        expect(sheet.workbook.shown.name).toBe('Sheet1');
        key('F11', { shiftKey: true });
        expect(tabNames(element)).toEqual(['Sheet2', 'Sheet1', 'Second']);
        expect(selectedTab(element)).toBe('Sheet2');
    });

    it('ADR-0331 §5: while a cell is edited no other Sheet is shown and none is added, and the notice says why', async () => {
        const { element, sheet } = sheetOf({ document: twoSheets() });
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        press(element, 0, 0);
        key('F2');
        key('PageDown', { ctrlKey: true });
        expect(sheet.workbook.shown.name).toBe('Sheet1');
        expect(noticeOf(element)).toMatch(/being edited/u);
        click(element.querySelectorAll('.ex-sheet-tab')[1]);
        expect(sheet.workbook.shown.name).toBe('Sheet1');
        click(element.querySelector('.ex-sheet-tab-add'));
        expect(tabNames(element)).toEqual(['Sheet1', 'Second']);
        expect(noticeOf(element)).toMatch(/No Sheet was changed/u);
        expect(element.querySelector('input.ex-editor')).not.toBeNull();
    });

    it('ADR-0331 §4: on the tab list, the arrows, Home and End select a tab and keep the keyboard on it; Escape gives it back to the grid', async () => {
        const workbook = new Workbook(Culture.of('en-US'), ['One', 'Two', 'Three']);
        const { element, sheet } = sheetOf({ document: workbook.toDocument() });
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        press(element, 0, 0);
        const grid = document.activeElement;
        const tabs = (): HTMLElement[] => [...element.querySelectorAll<HTMLElement>('.ex-sheet-tab')];
        tabs()[0]?.focus();
        key('ArrowRight');
        expect([sheet.workbook.shown.name, document.activeElement]).toEqual(['Two', tabs()[1]]);
        key('End');
        expect([sheet.workbook.shown.name, document.activeElement]).toEqual(['Three', tabs()[2]]);
        key('ArrowRight');
        expect(sheet.workbook.shown.name).toBe('Three');
        key('PageUp', { ctrlKey: true });
        expect([sheet.workbook.shown.name, document.activeElement]).toEqual(['Two', tabs()[1]]);
        key('Home');
        expect([sheet.workbook.shown.name, document.activeElement]).toEqual(['One', tabs()[0]]);
        // A press, and a Sheet added, move the keyboard with the selection, never leaving it on a tab not selected.
        click(tabs()[2]);
        expect([sheet.workbook.shown.name, document.activeElement]).toEqual(['Three', tabs()[2]]);
        key('F11', { shiftKey: true });
        expect([sheet.workbook.shown.name, document.activeElement]).toEqual(['Sheet1', tabs()[2]]);
        click(element.querySelector('.ex-sheet-tab-add'));
        expect([sheet.workbook.shown.name, document.activeElement]).toEqual(['Sheet2', tabs()[3]]);
        key('Escape');
        expect(document.activeElement).toBe(grid);
    });

    it('ADR-0331: a Sheet too short for its tabs\' band and a grid is refused by name, leaving the element, or the Sheet, as it was', () => {
        const element = document.createElement('div');
        element.textContent = 'mine';
        expect(() => createSheet(element, { locale: 'en-US', viewportHeight: 20 })).toThrow(/cannot hold its Sheet Tabs/u);
        expect([element.className, element.getAttribute('style'), element.textContent]).toEqual(['', null, 'mine']);
        const { sheet } = sheetOf();
        expect(() => { sheet.update({ locale: 'en-US', viewportWidth: 600, viewportHeight: 20, japanese: true }); }).toThrow(/cannot hold/u);
        expect(sheet.undo()).toBe(false);
        expect(document.querySelector('.ex-sheet-notice')?.textContent).toBe('There is nothing to undo.');
    });

    it('ADR-0331: without the tabs the grid takes the whole height, and the Sheet lays out as if they were not there', async () => {
        const element = document.createElement('div');
        element.style.margin = '3px';
        document.body.append(element);
        element.style.display = 'grid';
        const options: SheetOptions = { locale: 'en-US', viewportWidth: 600, viewportHeight: 300 };
        const sheet = createSheet(element, options);
        mounted.push(sheet);
        expect(element.style.display).toBe('block');
        sheet.update({ ...options, showSheetTabs: false });
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        expect(element.querySelector('.ex-sheet-tab-area')).toBeNull();
        // The Consumer's own style on the element stays theirs, and what the tabs' frame set gives back what it covered.
        expect(element.getAttribute('style')).toBe('margin: 3px; display: grid;');
        const bar = element.querySelector<HTMLElement>('.ex-formula-bar');
        if (bar === null) throw new Error('The Formula Bar is shown.');
        expect(element.querySelector<HTMLElement>('.ex-scroller')?.style.height).toBe(`${String(300 - Number.parseFloat(bar.style.height))}px`);
    });
});

const settle = async (): Promise<void> => {
    for (let i = 0; i < 4; i++) await new Promise(resolve => { setTimeout(resolve, 0); });
};

function findField(element: HTMLElement, selector: string): HTMLInputElement {
    const field = element.querySelector<HTMLInputElement>(`.ex-popover-find ${selector}`);
    if (field === null) throw new Error(`The find panel has no ${selector}.`);
    return field;
}

function typeInFind(element: HTMLElement, selector: string, text: string): void {
    const field = findField(element, selector);
    field.value = text;
    field.dispatchEvent(new Event('input', { bubbles: true }));
}

describe('Find and Replace (ADR-0055, ADR-0332)', () => {
    it('ADR-0055: Ctrl+F finds the text a cell is shown with, a Formula\'s Value, and selects it', async () => {
        const { element } = sheetOf({ document: documentOf({ A1: 'apple', B3: '=40+2', C5: '42 apples' }) });
        await vi.waitFor(() => { expect(textAt(element, 2, 1)).toBe('42'); });
        press(element, 0, 0);
        key('f', { ctrlKey: true });
        await vi.waitFor(() => { expect(element.querySelector('.ex-popover-find')).not.toBeNull(); });
        typeInFind(element, 'input.ex-find-field', '42');
        findField(element, 'input.ex-find-field').closest('form')?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
        await settle();
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('B3'); });
        findField(element, 'input.ex-find-field').closest('form')?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
        await settle();
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('C5'); });
    });

    it('ADR-0332 §3: Replace All rewrites the Entries that match, Formulas included, as one step on the undo stack', async () => {
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '2', A2: '=A1*10', B1: 'rate 2' }) });
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('20'); });
        press(element, 0, 0);
        key('h', { ctrlKey: true });
        await vi.waitFor(() => { expect(element.querySelector('.ex-popover-find input.ex-find-replace-field')).not.toBeNull(); });
        typeInFind(element, 'input.ex-find-field', '10');
        typeInFind(element, 'input.ex-find-replace-field', '100');
        element.querySelector<HTMLElement>('.ex-popover-find button.ex-find-replace-all')?.click();
        await settle();
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('200'); });
        expect(sheet.workbook.shown.getEntryText(CellAddress.parse('A2'))).toBe('=A1*100');
        expect(sheet.undo()).toBe(true);
        expect(sheet.workbook.shown.getEntryText(CellAddress.parse('A2'))).toBe('=A1*10');
    });
    it('ADR-0332 §2: Replace writes into the Entry of the match the Value found, so a Formula stays a Formula', async () => {
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '=B1*2', B1: '1' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('2'); });
        press(element, 2, 2);
        key('h', { ctrlKey: true });
        await vi.waitFor(() => { expect(element.querySelector('.ex-popover-find input.ex-find-replace-field')).not.toBeNull(); });
        typeInFind(element, 'input.ex-find-field', '2');
        typeInFind(element, 'input.ex-find-replace-field', '5');
        const replace = (): void => { element.querySelector<HTMLElement>('.ex-popover-find button.ex-find-replace')?.click(); };
        replace();
        await settle();
        await vi.waitFor(() => { expect(nameBox(element).value).toBe('A1'); });
        replace();
        await settle();
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('5'); });
        expect(sheet.workbook.shown.getEntryText(CellAddress.parse('A1'))).toBe('=B1*5');
    });
});

describe('the formatting and structure keys (ADR-0071, ADR-0050 item 14)', () => {
    it('ADR-0071 case 17: Ctrl+B sets bold on the Selection by the Focus cell\'s direction, Ctrl+Shift+% a Number Format; each is one step to undo', async () => {
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '0.5', A2: '0.25' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('0.5'); });
        press(element, 0, 0);
        key('ArrowDown', { shiftKey: true });
        key('b', { ctrlKey: true });
        const bold = (address: string): boolean => sheet.workbook.shown.getCellFormat(CellAddress.parse(address)).font.bold;
        expect([bold('A1'), bold('A2')]).toEqual([true, true]);
        key('%', { ctrlKey: true, shiftKey: true });
        await vi.waitFor(() => { expect([textAt(element, 0, 0), textAt(element, 1, 0)]).toEqual(['50%', '25%']); });
        expect(sheet.undo()).toBe(true);
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('0.5'); });
        key('B', { ctrlKey: true });
        expect([bold('A1'), bold('A2')]).toEqual([false, false]);
    });

    it('ADR-0071: a formatting key while a cell is edited changes nothing, and the notice says why', async () => {
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '1' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('1'); });
        press(element, 0, 0);
        key('F2');
        key('b', { ctrlKey: true });
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A1')).font.bold).toBe(false);
        expect(noticeOf(element)).toMatch(/^Nothing was formatted: a cell is being edited/u);
    });

    it('ADR-0050 item 14: Ctrl+- over whole rows deletes them, and over any other Selection says why and changes nothing', async () => {
        const { element, sheet } = sheetOf({ document: documentOf({ A1: 'one', A2: 'two', A3: 'three' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('one'); });
        press(element, 1, 0);
        key('-', { ctrlKey: true });
        expect(noticeOf(element)).toMatch(/^Nothing was deleted: select whole row/u);
        key(' ', { shiftKey: true });
        key('-', { ctrlKey: true });
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('three'); });
        expect(sheet.undo()).toBe(true);
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('two'); });
    });
});
