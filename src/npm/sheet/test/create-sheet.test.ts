// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): ExSheet drawn by ExGrid as its Consumer (ADR-0046), over the engine. What the grid
// paints is the core's, tested there; here, that the Sheet answers the grid's intents as one step each on one undo
// stack (ADR-0048), hands back new instances only for the rows a change names (ADR-0003), and says in its own words
// what it refuses (ADR-0370 section 10).
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CellAddress, CellFormatChange, Culture, RepeatedKeyError, SheetEdit, SheetRefusedError, Value, Workbook } from '@ex-grid/sheet-engine';
import { createGrid, type GridHandle } from '@ex-grid/core';
import { createSheet, type SheetChrome, type SheetHandle, type SheetOptions, type SheetsChange } from '../src/create-sheet';
import type { SheetToolbarItemSlotContext } from '../src/sheet-toolbar-slot';
import { PointingScope } from '../src/pointing-scope';
import type { PointingRefusal } from '../src/pointing-refusal';

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

/** The Row Headings' width, as the grid emitted it inline, or none where the band does not stand (ADR-0050). */
function rowHeadingWidthPx(element: HTMLElement): number {
    const style = element.querySelector('.ex-row-heading')?.getAttribute('style');
    if (style === null || style === undefined) return 0;
    const width = /width: ([\d.]+)px/u.exec(style);
    if (width?.[1] === undefined) throw new Error(`A Row Heading carries no width: ${style}`);
    return Number(width[1]);
}

/** A press on the cell at `row`, `column` of a default Sheet, where the keyboard then is. */
function press(element: HTMLElement, row: number, column: number): void {
    const viewport = element.querySelector<HTMLElement>('.ex-viewport');
    if (viewport === null) throw new Error('The grid has no viewport.');
    // The Row Headings stand at the Viewport's left edge, and a press in that band selects a whole row
    // (ADR-0050): the columns start after it.
    const headingPx = rowHeadingWidthPx(element);
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
        Object.defineProperty(event, 'offsetX', { value: headingPx + (column * 99) + 20 });
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

/** A Linked Table's rows of an Id and a PV, as a page pushes them. */
const rowsOf = (...rows: readonly (readonly [string, number])[]): (readonly Value[])[] => rows.map(([id, pv]) => [Value.fromText(id), Value.fromNumber(pv)]);

describe('Linked Tables (ADR-0049)', () => {
    it('ADR-0049: a declared table\'s readers wait for its first snapshot, which repaints them; the document hears the declaration, never the rows, and neither is a step to undo', async () => {
        const documents: unknown[] = [];
        const { element, sheet } = sheetOf({ document: documentOf({ A1: 'Total', B1: '=SUM(Positions[PV])', A3: 'Untouched' }), onWorkbookChanged: document => { documents.push(document); } });
        sheet.declareLinkedTable('Positions', ['Id', 'PV'], 'Id');
        expect(documents).toHaveLength(1);
        await vi.waitFor(() => { expect(textAt(element, 0, 1)).toBe('#GETTING_DATA'); });
        // The same declaration again changes nothing the document records.
        sheet.declareLinkedTable('Positions', ['Id', 'PV'], 'Id');
        expect(documents).toHaveLength(1);
        const untouched = cellAt(element, 2, 0);
        sheet.pushLinkedTable('Positions', rowsOf(['R-1', 1250], ['R-2', -420.5]));
        await vi.waitFor(() => { expect(textAt(element, 0, 1)).toBe('829.5'); });
        expect(cellAt(element, 2, 0)).toBe(untouched);
        expect(documents).toHaveLength(1);
        expect(sheet.undo()).toBe(false);
    });

    it('ADR-0049: a snapshot in which a key repeats is refused with the engine\'s error, and its readers wait again', async () => {
        const { element, sheet } = sheetOf({ document: documentOf({ B1: '=SUM(Positions[PV])' }) });
        sheet.declareLinkedTable('Positions', ['Id', 'PV'], 'Id');
        sheet.pushLinkedTable('Positions', rowsOf(['R-1', 1]));
        await vi.waitFor(() => { expect(textAt(element, 0, 1)).toBe('1'); });
        expect(() => { sheet.pushLinkedTable('Positions', rowsOf(['R-1', 1], ['r-1', 2])); }).toThrow(RepeatedKeyError);
        await vi.waitFor(() => { expect(textAt(element, 0, 1)).toBe('#GETTING_DATA'); });
    });

    it('ADR-0049, ADR-0331: a reader on a Sheet other than the first follows a push, and waits again when a key repeats', async () => {
        const workbook = new Workbook(Culture.of('en-US'), ['Sheet1', 'Second']);
        workbook.do('Second', SheetEdit.enter(CellAddress.parse('B1'), '=SUM(Positions[PV])'));
        const { element, sheet } = sheetOf({ document: workbook.toDocument() });
        sheet.declareLinkedTable('Positions', ['Id', 'PV'], 'Id');
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        click(element.querySelectorAll('.ex-sheet-tab')[1]);
        await vi.waitFor(() => { expect(textAt(element, 0, 1)).toBe('#GETTING_DATA'); });
        sheet.pushLinkedTable('Positions', rowsOf(['R-1', 1], ['R-2', 2]));
        await vi.waitFor(() => { expect(textAt(element, 0, 1)).toBe('3'); });
        expect(() => { sheet.pushLinkedTable('Positions', rowsOf(['R-1', 1], ['r-1', 2])); }).toThrow(RepeatedKeyError);
        await vi.waitFor(() => { expect(textAt(element, 0, 1)).toBe('#GETTING_DATA'); });
    });
});

describe('The formula entry aids (ADR-0051, ADR-0057)', () => {
    it('DC-49: a Formula being edited outlines the cells it names, and tells each Linked Table column it reads in its colour, once per change', async () => {
        const told: (readonly { readonly table: string; readonly column: string; readonly colour: number }[])[] = [];
        const { element, sheet } = sheetOf({
            onLinkedColumnColoursChanged: columns => { told.push(columns.map(({ column, colour }) => ({ table: column.table, column: column.column, colour }))); },
        });
        sheet.declareLinkedTable('Positions', ['Id', 'PV'], 'Id');
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        press(element, 3, 3);
        key('F2');
        const editor = element.querySelector<HTMLInputElement>('input.ex-editor');
        if (editor === null) throw new Error('The editor opened.');
        // The table and column are matched ignoring case, and told under the names declared.
        editor.value = '=A1+B2:C3+a1+SUM(positions[pv])';
        editor.dispatchEvent(new Event('input', { bubbles: true }));
        await vi.waitFor(() => {
            expect([...element.querySelectorAll('.ex-selection .ex-reference-outline')].map(piece => piece.className)).toEqual([
                'ex-reference-outline ex-reference-1', 'ex-reference-outline ex-reference-2', 'ex-reference-outline ex-reference-1',
            ]);
        });
        expect(told).toEqual([[{ table: 'Positions', column: 'PV', colour: 3 }]]);
        key('Escape');
        await vi.waitFor(() => { expect(element.querySelector('.ex-reference-outline')).toBeNull(); });
        expect(told).toEqual([[{ table: 'Positions', column: 'PV', colour: 3 }], []]);
    });

    it('DC-49: a table declared while its column\'s Formula is edited is told at once', async () => {
        const told: (readonly string[])[] = [];
        const { element, sheet } = sheetOf({ onLinkedColumnColoursChanged: columns => { told.push(columns.map(({ column }) => `${column.table}[${column.column}]`)); } });
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        press(element, 0, 0);
        key('F2');
        const editor = element.querySelector<HTMLInputElement>('input.ex-editor');
        if (editor === null) throw new Error('The editor opened.');
        editor.value = '=SUM(Costs[Amount])';
        editor.dispatchEvent(new Event('input', { bubbles: true }));
        sheet.declareLinkedTable('Costs', ['Id', 'Amount'], 'Id');
        await vi.waitFor(() => { expect(told).toEqual([['Costs[Amount]']]); });
    });

    it('ADR-0051, ADR-0058: the declared functions and the Linked Tables are listed as a name is typed, and Tab writes the name and shows its argument hint', async () => {
        const { element, sheet } = sheetOf();
        sheet.declareLinkedTable('Positions', ['Id', 'PV'], 'Id');
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        press(element, 0, 0);
        key('=');
        const editor = element.querySelector<HTMLInputElement>('input.ex-editor');
        if (editor === null) throw new Error('The editor opened.');
        const type = (text: string): void => {
            editor.setRangeText(text, editor.value.length, editor.value.length, 'end');
            editor.dispatchEvent(new Event('input', { bubbles: true }));
        };
        const listed = (): (string | null)[] => [...element.querySelectorAll('.ex-completion-item')].map(item => item.textContent);
        type('PO');
        await vi.waitFor(() => { expect(listed()).toContain('Positions'); });
        type('X');
        await vi.waitFor(() => { expect(element.querySelector('.ex-completion')).toBeNull(); });
        editor.value = '=';
        type('SUMI');
        await vi.waitFor(() => { expect(listed()).toEqual(['SUMIF', 'SUMIFS']); });
        key('Tab');
        expect(editor.value).toBe('=SUMIF(');
        await vi.waitFor(() => { expect(element.querySelector('.ex-completion-hint strong')?.textContent).toBe('range'); });
        expect(listed()).toEqual([]);
        expect(element.querySelector('.ex-completion-hint')?.textContent).toMatch(/^SUMIF\(range, criteria/u);
    });

    it('ADR-0051: = and ↓ point at the cell below, F4 cycles it to $A$3, and Enter commits the Formula', async () => {
        const { element } = sheetOf({ document: documentOf({ A3: '7' }) });
        await vi.waitFor(() => { expect(textAt(element, 2, 0)).toBe('7'); });
        press(element, 1, 0);
        key('=');
        key('ArrowDown');
        const editor = element.querySelector<HTMLInputElement>('input.ex-editor');
        expect(editor?.value).toBe('=A3');
        key('F4');
        expect(editor?.value).toBe('=$A$3');
        key('Enter');
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('7'); });
    });
});

// A press on a cell of the registered grid, its default prevented or not, where the keyboard then is.
function pressGrid(element: HTMLElement, row: number, column: number, init: MouseEventInit = {}): MouseEvent {
    const viewport = element.querySelector<HTMLElement>('.ex-viewport');
    if (viewport === null) throw new Error('The grid has no viewport.');
    const event = new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0, buttons: 1, ...init });
    Object.defineProperty(event, 'offsetX', { value: (column * 100) + 10 });
    Object.defineProperty(event, 'offsetY', { value: (row * 24) + 5 });
    viewport.dispatchEvent(event);
    return event;
}

describe('The Pointing Scope (ADR-0058)', () => {
    interface Position { readonly id: string; readonly pv: number }
    const positions: readonly Position[] = [{ id: 'R-1', pv: 110 }, { id: 'R-2', pv: 250 }, { id: 'R-3', pv: 75 }];
    const grids: GridHandle<Position>[] = [];
    afterEach(() => { for (const grid of grids.splice(0)) grid.destroy(); });

    it('a press on a registered grid writes the cell\'s lookup where the Sheet points, the arrows move it there, and a range is refused', async () => {
        const scope = new PointingScope();
        const refusals: PointingRefusal[] = [];
        const { element, sheet } = sheetOf({ pointingScope: scope, onPointingRefused: refusal => { refusals.push(refusal); } });
        sheet.declareLinkedTable('Positions', ['Id', 'PV'], 'Id');
        const gridElement = document.createElement('div');
        document.body.append(gridElement);
        grids.push(createGrid<Position>(gridElement, {
            columns: [
                { name: 'Id', type: 'text', value: row => row.id, width: 100 },
                { name: 'PV', type: 'number', value: row => row.pv, width: 100 },
            ],
            rows: positions, rowHeight: 24, viewportHeight: 200,
            pointedAt: scope.registerGrid<Position>('Positions', row => [Value.fromText(row.id), Value.fromNumber(row.pv)]),
        }));
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        await vi.waitFor(() => { expect(gridElement.querySelector('[role="row"] [role="gridcell"]')).not.toBeNull(); });
        press(element, 1, 0);
        key('=');
        await vi.waitFor(() => { expect(gridElement.classList.contains('ex-pointed-at')).toBe(true); });
        const editor = element.querySelector<HTMLInputElement>('input.ex-editor');
        expect(pressGrid(gridElement, 1, 1).defaultPrevented).toBe(true);
        await vi.waitFor(() => { expect(editor?.value).toBe('=XLOOKUP("R-2", Positions[Id], Positions[PV])'); });
        expect(document.activeElement).toBe(editor);
        await vi.waitFor(() => { expect(gridElement.querySelectorAll('.ex-point-dashes')).toHaveLength(1); });
        key('ArrowDown');
        await vi.waitFor(() => { expect(editor?.value).toBe('=XLOOKUP("R-3", Positions[Id], Positions[PV])'); });
        pressGrid(gridElement, 0, 1, { shiftKey: true });
        await vi.waitFor(() => { expect(refusals.map(refusal => refusal.reason)).toEqual(['severalCells']); });
        expect(editor?.value).toBe('=XLOOKUP("R-3", Positions[Id], Positions[PV])');
        // Typing ends Point over what the press wrote: the dashes go, and the grid is still pointed at while a
        // Reference can go at the caret.
        editor?.setRangeText('+', editor.value.length, editor.value.length, 'end');
        editor?.dispatchEvent(new Event('input', { bubbles: true }));
        await vi.waitFor(() => { expect(gridElement.querySelectorAll('.ex-point-dashes')).toHaveLength(0); });
        key('Escape');
        await vi.waitFor(() => { expect(gridElement.classList.contains('ex-pointed-at')).toBe(false); });
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

    it('ADR-0332 §4: while the panel holds a search, the painted cells it finds wear the Match Mark, by the rule the step finds by', async () => {
        const { element } = sheetOf({ document: documentOf({ A1: 'apple', B3: '=40+2', C5: '42 apples' }) });
        await vi.waitFor(() => { expect(textAt(element, 2, 1)).toBe('42'); });
        press(element, 0, 0);
        key('f', { ctrlKey: true });
        await vi.waitFor(() => { expect(element.querySelector('.ex-popover-find')).not.toBeNull(); });
        typeInFind(element, 'input.ex-find-field', '42');
        await vi.waitFor(() => { expect(element.querySelectorAll('.ex-viewport .ex-match-mark')).toHaveLength(2); });
        expect(cellAt(element, 2, 1)?.className).toMatch(/ex-match-mark/);
        expect(cellAt(element, 4, 2)?.className).toMatch(/ex-match-mark/);
        expect(cellAt(element, 0, 0)?.className).not.toMatch(/ex-match-mark/);
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

/** The copy event the browser's copy command fires on the keyboard's element, and what the grid wrote on it. */
function copyEvent(): { readonly event: Event; readonly data: Map<string, string> } {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    const data = new Map<string, string>();
    const event = new Event('copy', { bubbles: true, cancelable: true });
    Object.defineProperty(event, 'clipboardData', { value: { setData: (type: string, value: string) => { data.set(type, value); } } });
    target.dispatchEvent(event);
    return { event, data };
}

/** The paste event the browser's paste command fires on the keyboard's element, carrying `data`. */
function pasteEvent(data: ReadonlyMap<string, string>): void {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    const event = new Event('paste', { bubbles: true, cancelable: true });
    Object.defineProperty(event, 'clipboardData', { value: { getData: (type: string) => data.get(type) ?? '' } }); // oxlint-disable-line ex-grid/no-stand-in-default -- a flavour the copy did not write reads as the browser's empty one
    target.dispatchEvent(event);
}

describe('a copy of the Sheet\'s own (ADR-0050 item 9, ADR-0048)', () => {
    it('ADR-0048: a copy pasted back carries its Entries, relative References shifted, as one step on the undo stack', async () => {
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '=B1', A2: '=B2*2', B1: '5', B2: '7' }) });
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('14'); });
        press(element, 0, 0);
        key('ArrowDown', { shiftKey: true });
        await vi.waitFor(() => { expect(sheet.workbook.shown.getEntryText(CellAddress.parse('A1'))).toBe('=B1'); });
        const { event, data } = copyEvent();
        expect(event.defaultPrevented).toBe(true);
        // The engine's copy: the Values as shown for text/plain.
        expect(data.get('text/plain')).toBe('5\r\n14\r\n');
        press(element, 0, 2);
        pasteEvent(data);
        const shown = sheet.workbook.shown;
        await vi.waitFor(() => { expect([shown.getEntryText(CellAddress.parse('C1')), shown.getEntryText(CellAddress.parse('C2'))]).toEqual(['=D1', '=D2*2']); });
        key('z', { ctrlKey: true });
        await vi.waitFor(() => { expect([shown.getEntryText(CellAddress.parse('C1')), shown.getEntryText(CellAddress.parse('C2'))]).toEqual(['', '']); });
    });

    it('ADR-0050 item 10: what another program put on the clipboard is pasted as the Values it holds, even where it reads as the Sheet\'s copy did', async () => {
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '=B1', B1: '5' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('5'); });
        press(element, 0, 0);
        const { data } = copyEvent();
        press(element, 0, 2);
        pasteEvent(new Map([['text/plain', '6\r\n']]));
        await vi.waitFor(() => { expect(sheet.workbook.shown.getEntryText(CellAddress.parse('C1'))).toBe('6'); });
        expect(data.get('text/plain')).toBe('5\r\n');
        // Text from another program that reads as the copy did is still its text: the copy's Formula is not pasted.
        press(element, 0, 3);
        pasteEvent(new Map([['text/plain', '5\r\n']]));
        await vi.waitFor(() => { expect(sheet.workbook.shown.getEntryText(CellAddress.parse('D1'))).toBe('5'); });
    });
});

// A split control's face or arrow, by the control's name.
function splitPart(element: HTMLElement, name: string, part: 'face' | 'arrow'): HTMLButtonElement {
    const found = element.querySelector<HTMLButtonElement>(`.ex-sheet-toolbar-split[aria-label="${name}"] > .ex-sheet-toolbar-${part}`);
    if (found === null) throw new Error(`No split control is named ${name}.`);
    return found;
}

// The list a Toolbar Item opened in the grid's popover frame, and its choices by their names.
function choicesOf(element: HTMLElement): { readonly list: HTMLElement; readonly choice: (name: string) => HTMLButtonElement } {
    const list = element.querySelector<HTMLElement>('.ex-popover .ex-sheet-choices');
    if (list === null) throw new Error('No Toolbar Item\'s list stands.');
    return {
        list,
        choice: (name: string): HTMLButtonElement => {
            const found = [...list.querySelectorAll<HTMLButtonElement>('button')].find(button => (button.getAttribute('aria-label') ?? button.querySelector('.ex-sheet-choice-label')?.textContent) === name);
            if (found === undefined) throw new Error(`The list offers no choice named ${name}.`);
            return found;
        },
    };
}

function toolbarItem(element: HTMLElement, name: string): HTMLButtonElement {
    const found = [...element.querySelectorAll<HTMLButtonElement>('.ex-sheet-toolbar-item')].find(button => button.getAttribute('aria-label') === name);
    if (found === undefined) throw new Error(`No Toolbar Item is named ${name}.`);
    return found;
}

describe('the Sheet Toolbar (ADR-0100)', () => {

    it('ADR-0100: shown only when asked for, above the grid, its band off the Sheet\'s height; Excel\'s Home tab in its order', async () => {
        const plain = sheetOf();
        expect(plain.element.querySelector('.ex-sheet-toolbar')).toBeNull();
        const { element } = sheetOf({ showToolbar: true, showSheetTabs: false });
        const band = element.querySelector<HTMLElement>('.ex-sheet-toolbar');
        expect(band?.nextElementSibling?.querySelector('.ex-grid, [data-ex-answered]')).not.toBeNull();
        expect([band?.getAttribute('role'), band?.getAttribute('aria-label'), band?.tabIndex]).toEqual(['toolbar', 'Formatting', 0]);
        expect([...element.querySelectorAll('.ex-sheet-toolbar-item')].map(button => button.getAttribute('aria-label'))).toEqual([
            'Bold', 'Italic', 'Underline', 'Strikethrough', 'Font Colour', 'Fill Colour', 'Borders', 'Align Left', 'Centre', 'Align Right',
            'Number Format', 'Percent Style', 'Comma Style', 'Format Cells',
        ]);
        expect(toolbarItem(element, 'Bold').title).toBe('Bold (Ctrl+B)');
        expect(toolbarItem(element, 'Comma Style').disabled).toBe(true);
        await vi.waitFor(() => { expect(element.querySelector<HTMLElement>('.ex-scroller')?.style.height).not.toBe(''); });
        const bandPx = Number.parseFloat(band?.style.height ?? ''); // oxlint-disable-line ex-grid/no-stand-in-default -- a band without a height reads as NaN, which the expectation names
        expect(bandPx).toBeGreaterThan(0);
    });

    it('ADR-0100: a press sets the format on the Selection as one step, keeps the keyboard on the Sheet, and the toggle follows the Focus cell', async () => {
        const { element, sheet } = sheetOf({ showToolbar: true, document: documentOf({ A1: 'x', A2: 'y' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('x'); });
        press(element, 0, 0);
        const grid = document.activeElement;
        const bold = toolbarItem(element, 'Bold');
        const down = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
        bold.dispatchEvent(down);
        expect(down.defaultPrevented).toBe(true);
        bold.click();
        expect(document.activeElement).toBe(grid);
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A1')).font.bold).toBe(true);
        await vi.waitFor(() => { expect(bold.getAttribute('aria-pressed')).toBe('true'); });
        press(element, 1, 0);
        await vi.waitFor(() => { expect(bold.getAttribute('aria-pressed')).toBe('false'); });
        toolbarItem(element, 'Centre').click();
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A2')).alignment).toBe('center');
        // Pressed again where the Focus cell has it, General.
        toolbarItem(element, 'Centre').click();
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A2')).alignment).toBe('general');
        sheet.undo();
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A2')).alignment).toBe('center');
    });

    it('ADR-0100: one tab stop; arrows move among the items that can be pressed, Enter presses one and gives the keyboard back', async () => {
        const { element, sheet } = sheetOf({ showToolbar: true, document: documentOf({ A1: '0.5' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('0.5'); });
        press(element, 0, 0);
        const grid = document.activeElement;
        const band = element.querySelector<HTMLElement>('.ex-sheet-toolbar');
        band?.focus();
        expect(band?.getAttribute('aria-activedescendant')).toBe(toolbarItem(element, 'Bold').id);
        key('End');
        expect(band?.getAttribute('aria-activedescendant')).toBe(toolbarItem(element, 'Format Cells').id);
        // Comma Style cannot be pressed, so ← passes over it.
        key('ArrowLeft');
        expect(band?.getAttribute('aria-activedescendant')).toBe(toolbarItem(element, 'Percent Style').id);
        key('Enter');
        expect(document.activeElement).toBe(grid);
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('50%'); });
        // Back on the band, the keyboard is on the item it last stood on.
        band?.focus();
        expect(band?.getAttribute('aria-activedescendant')).toBe(toolbarItem(element, 'Percent Style').id);
        key('Home');
        key('ArrowRight');
        expect(band?.getAttribute('aria-activedescendant')).toBe(toolbarItem(element, 'Italic').id);
        // Ctrl+I on the band is the Sheet's, as its tooltip says.
        key('i', { ctrlKey: true });
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A1')).font.italic).toBe(true);
        sheet.undo();
        key('Escape');
        expect(document.activeElement).toBe(grid);
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A1')).font.italic).toBe(false);
    });

    it('ADR-0100: a colour control\'s arrow opens the palette on the Focus cell\'s colour; a choice sets it, and the face sets it from then on', async () => {
        const { element, sheet } = sheetOf({ showToolbar: true, document: documentOf({ A1: 'x', A2: 'y' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('x'); });
        press(element, 0, 0);
        const grid = document.activeElement;
        expect(splitPart(element, 'Fill Colour', 'arrow').getAttribute('aria-label')).toBe('Fill Colour, more');
        splitPart(element, 'Fill Colour', 'arrow').click();
        const { list, choice } = choicesOf(element);
        expect(list.getAttribute('aria-label')).toBe('Fill Colour');
        // No Fill, the Office theme's sixty colours and the ten standard ones; the keyboard on the Focus cell's, none.
        expect(list.querySelectorAll('button')).toHaveLength(71);
        expect(document.activeElement).toBe(choice('No Fill'));
        expect(choice('No Fill').getAttribute('aria-checked')).toBe('true');
        choice('Red').click();
        expect(element.querySelector('.ex-sheet-choices')).toBeNull();
        expect(document.activeElement).toBe(grid);
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A1')).fill.colour?.toString()).toBe('#FF0000');
        press(element, 1, 0);
        splitPart(element, 'Fill Colour', 'face').click();
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A2')).fill.colour?.toString()).toBe('#FF0000');
        // Opened over the red cell, the palette marks red; No Fill takes it away, and the face still sets red.
        splitPart(element, 'Fill Colour', 'arrow').click();
        expect(choicesOf(element).choice('Red').getAttribute('aria-checked')).toBe('true');
        choicesOf(element).choice('No Fill').click();
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A2')).fill.isNone).toBe(true);
        splitPart(element, 'Font Colour', 'face').click();
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A2')).font.colour.toString()).toBe('#FF0000');
        sheet.undo();
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A2')).font.colour.isAutomatic).toBe(true);
    });

    it('ADR-0100: the Number Format menu shows the Focus cell\'s category, opens from the band\'s keys, and the keys choose; Accounting says why it cannot be chosen', async () => {
        const { element, sheet } = sheetOf({ showToolbar: true, document: documentOf({ A1: '0.5' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('0.5'); });
        press(element, 0, 0);
        const grid = document.activeElement;
        const menu = toolbarItem(element, 'Number Format');
        expect(menu.textContent).toBe('General');
        const band = element.querySelector<HTMLElement>('.ex-sheet-toolbar');
        band?.focus();
        while (band?.getAttribute('aria-activedescendant') !== menu.id) key('ArrowRight');
        key('Enter');
        const { choice } = choicesOf(element);
        expect(document.activeElement).toBe(choice('General'));
        expect(choice('Accounting').getAttribute('aria-disabled')).toBe('true');
        expect(choice('Accounting').textContent).toContain('Accounting');
        expect(() => choice('Special')).toThrow(/no choice named Special/u);
        // General, Number, Currency, Date, Time, Percentage: ↓ passes over Accounting, which cannot be chosen.
        for (let i = 0; i < 5; i++) key('ArrowDown');
        expect(document.activeElement).toBe(choice('Percentage'));
        key('Enter');
        expect(document.activeElement).toBe(grid);
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('50.00%'); });
        await vi.waitFor(() => { expect(toolbarItem(element, 'Number Format').textContent).toBe('Percentage'); });
        // More Number Formats… opens Format Cells in the list's place.
        toolbarItem(element, 'Number Format').click();
        choicesOf(element).choice('More Number Formats…').click();
        expect(element.querySelector('.ex-sheet-choices')).toBeNull();
        expect(element.querySelector('.ex-popover .ex-format-cells')).not.toBeNull();
        expect(sheet.undo()).toBe(true);
    });

    it('ADR-0100: ↓ on Borders opens its presets; one chosen is set relative to every selected range, and the face sets it from then on', async () => {
        const { element, sheet } = sheetOf({ showToolbar: true, document: documentOf({ A1: '1', B2: '2' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('1'); });
        press(element, 0, 0);
        const band = element.querySelector<HTMLElement>('.ex-sheet-toolbar');
        band?.focus();
        const borders = element.querySelector<HTMLElement>('.ex-sheet-toolbar-split[aria-label="Borders"]');
        while (band?.getAttribute('aria-activedescendant') !== borders?.id) key('ArrowRight');
        key('ArrowDown');
        const { choice } = choicesOf(element);
        expect(document.activeElement).toBe(choice('Bottom Border'));
        choice('Thick Outside Borders').click();
        const a1 = sheet.workbook.shown.getCellFormat(CellAddress.parse('A1')).borders;
        expect([a1.top.style, a1.bottom.style, a1.left.style, a1.right.style]).toEqual(['thick', 'thick', 'thick', 'thick']);
        press(element, 1, 1);
        splitPart(element, 'Borders', 'face').click();
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('B2')).borders.left.style).toBe('thick');
    });

    it('ADR-0100, ADR-0331: a list reads the Sheet it opened over: another Sheet shown ends it, and its choices set nothing after', async () => {
        const { element, sheet } = sheetOf({ showToolbar: true, document: twoSheets() });
        await vi.waitFor(() => { expect(cellAt(element, 0, 0)).not.toBeNull(); });
        press(element, 0, 0);
        splitPart(element, 'Fill Colour', 'arrow').click();
        const red = choicesOf(element).choice('Red');
        click(element.querySelectorAll('.ex-sheet-tab')[1]);
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('there'); });
        expect(element.querySelector('.ex-sheet-choices')).toBeNull();
        red.click();
        expect(sheet.workbook.sheets.map(shown => shown.getCellFormat(CellAddress.parse('A1')).fill.isNone)).toEqual([true, true]);
    });

    it('ADR-0100: the Alt key alone shows the rows\' KeyTip; H shows the items\' letters, and 1 presses Bold and gives the keyboard back', async () => {
        const { element, sheet } = sheetOf({ showToolbar: true, document: documentOf({ A1: 'x' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('x'); });
        press(element, 0, 0);
        const grid = document.activeElement;
        const band = element.querySelector<HTMLElement>('.ex-sheet-toolbar');
        key('Alt', { altKey: true });
        const release = new KeyboardEvent('keyup', { key: 'Alt', bubbles: true, cancelable: true });
        grid?.dispatchEvent(release);
        expect(release.defaultPrevented).toBe(true);
        expect(document.activeElement).toBe(band);
        expect([...element.querySelectorAll('.ex-sheet-keytip')].map(badge => badge.textContent)).toEqual(['H']);
        key('h');
        expect([...element.querySelectorAll('.ex-sheet-keytip')].map(badge => badge.textContent)).toEqual([
            '1', '2', '3', '4', 'FC', 'H', 'B', 'AL', 'AC', 'AR', 'N', 'P', 'K', 'OE',
        ]);
        // A typed letter narrows the level: A leaves the alignments' letters; Escape shows them all again.
        key('a');
        expect([...element.querySelectorAll('.ex-sheet-keytip')].map(badge => badge.textContent)).toEqual(['AL', 'AC', 'AR']);
        key('Escape');
        key('1');
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A1')).font.bold).toBe(true);
        expect(document.activeElement).toBe(grid);
        expect(element.querySelectorAll('.ex-sheet-keytip')).toHaveLength(0);
        // An Alt that is a chord's starts nothing; F10 starts them too, and a key that is no letter ends them.
        key('Alt', { altKey: true });
        key('ArrowDown', { altKey: true });
        grid?.dispatchEvent(new KeyboardEvent('keyup', { key: 'Alt', bubbles: true, cancelable: true }));
        expect(document.activeElement).toBe(grid);
        key('F10');
        expect(document.activeElement).toBe(band);
        expect(band?.hasAttribute('aria-activedescendant')).toBe(false);
        key('ArrowLeft');
        expect(document.activeElement).toBe(grid);
        expect(element.querySelectorAll('.ex-sheet-keytip')).toHaveLength(0);
        // A key the Sheet would claim, typed while they stand, is theirs too: it ends them and sets nothing.
        key('F10');
        key('b', { ctrlKey: true });
        expect(document.activeElement).toBe(grid);
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A1')).font.bold).toBe(true);
        // Shift before a letter waits for the letter: the KeyTips are matched in capitals.
        key('F10');
        key('Shift', { shiftKey: true });
        key('H', { shiftKey: true });
        expect(element.querySelectorAll('.ex-sheet-keytip')).toHaveLength(14);
        key('Escape');
        key('Escape');
        expect(document.activeElement).toBe(grid);
        // An Alt held through a press is not alone.
        key('Alt', { altKey: true });
        grid?.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, altKey: true }));
        grid?.dispatchEvent(new KeyboardEvent('keyup', { key: 'Alt', bubbles: true, cancelable: true }));
        expect(document.activeElement).toBe(grid);
        expect(element.querySelectorAll('.ex-sheet-keytip')).toHaveLength(0);
    });

    it('ADR-0100: the toolbar hidden while the KeyTips stand takes them away, and shown again it shows none', async () => {
        const options = { showToolbar: true } as const;
        const { element, sheet } = sheetOf(options);
        await vi.waitFor(() => { expect(element.querySelector('.ex-viewport')).not.toBeNull(); });
        press(element, 0, 0);
        const grid = document.activeElement;
        key('F10');
        key('h');
        key('h');
        expect(element.querySelector('.ex-sheet-choices')).not.toBeNull();
        sheet.update({ locale: 'en-US', viewportWidth: 600, viewportHeight: 300, showToolbar: false });
        expect(element.querySelector('.ex-sheet-choices')).toBeNull();
        expect(document.activeElement).toBe(grid);
        sheet.update({ locale: 'en-US', viewportWidth: 600, viewportHeight: 300, ...options });
        expect(element.querySelector('.ex-sheet-toolbar')).not.toBeNull();
        expect(element.querySelectorAll('.ex-sheet-keytip')).toHaveLength(0);
    });

    it('ADR-0100: Alt, H, H opens the Fill\'s palette; Escape there goes back to the row\'s letters, then to the rows\', then to the Sheet', async () => {
        const { element } = sheetOf({ showToolbar: true });
        await vi.waitFor(() => { expect(element.querySelector('.ex-viewport')).not.toBeNull(); });
        press(element, 0, 0);
        const grid = document.activeElement;
        const band = element.querySelector<HTMLElement>('.ex-sheet-toolbar');
        key('F10');
        key('h');
        key('h');
        const { list, choice } = choicesOf(element);
        expect(document.activeElement).toBe(choice('No Fill'));
        // ↑ from the palette's first row goes back to No Fill, which stands above it.
        key('ArrowDown');
        expect(document.activeElement).toBe(list.querySelectorAll('.ex-sheet-swatch-choice')[0]);
        key('ArrowUp');
        expect(document.activeElement).toBe(choice('No Fill'));
        key('Escape');
        expect(element.querySelector('.ex-sheet-choices')).toBeNull();
        expect(document.activeElement).toBe(band);
        expect(element.querySelectorAll('.ex-sheet-keytip')).toHaveLength(14);
        key('Escape');
        expect([...element.querySelectorAll('.ex-sheet-keytip')].map(badge => badge.textContent)).toEqual(['H']);
        key('Escape');
        expect(document.activeElement).toBe(grid);
        // O narrows to Format Cells, and E opens it.
        key('F10');
        key('h');
        key('o');
        expect([...element.querySelectorAll('.ex-sheet-keytip')].map(badge => badge.textContent)).toEqual(['OE']);
        key('e');
        expect(element.querySelector('.ex-popover .ex-format-cells')).not.toBeNull();
    });

    it('ADR-0100: the Consumer\'s rows stand in its order, each a row of the band, and its own button is pressed from its KeyTip', async () => {
        const pressed: string[] = [];
        const { element } = sheetOf({
            showToolbar: true, showSheetTabs: false,
            toolbar: ['default', { label: 'Review', keyTip: 'r', items: ['bold', 'separator', { text: 'Mark reviewed', keyTip: 'm', onPress: () => { pressed.push('reviewed'); } }] }],
        });
        await vi.waitFor(() => { expect(element.querySelector('.ex-viewport')).not.toBeNull(); });
        const rows = [...element.querySelectorAll('.ex-sheet-toolbar-row')];
        expect(rows.map(row => row.getAttribute('aria-label'))).toEqual(['Home', 'Review']);
        const review = rows[1];
        if (review === undefined) throw new Error('The Consumer\'s row is not drawn.');
        expect([...review.querySelectorAll('.ex-sheet-toolbar-item')].map(button => button.getAttribute('aria-label'))).toEqual(['Bold', 'Mark reviewed']);
        const band = element.querySelector<HTMLElement>('.ex-sheet-toolbar');
        const rowPx = Number.parseFloat((rows[0] as HTMLElement | undefined)?.style.height ?? ''); // oxlint-disable-line ex-grid/no-stand-in-default -- a row without a height reads as NaN, which the expectation names
        expect(Number.parseFloat(band?.style.height ?? '')).toBe(2 * rowPx); // oxlint-disable-line ex-grid/no-stand-in-default -- as above
        press(element, 0, 0);
        const grid = document.activeElement;
        key('F10');
        expect([...element.querySelectorAll('.ex-sheet-row-keytip')].map(badge => badge.textContent)).toEqual(['H', 'R']);
        key('r');
        expect([...element.querySelectorAll('.ex-sheet-keytip')].map(badge => badge.textContent)).toEqual(['1', 'M']);
        key('m');
        expect(pressed).toEqual(['reviewed']);
        expect(document.activeElement).toBe(grid);
        // While an edit is open the Consumer's button is unavailable too, and says why.
        key('Z');
        await vi.waitFor(() => { expect(toolbarItem(element, 'Mark reviewed').disabled).toBe(true); });
        expect(toolbarItem(element, 'Mark reviewed').title).toBe('Mark reviewed: A cell is being edited. Press Enter to commit the edit or Escape to cancel it first.');
    });

    it('ADR-0100: KeyTips that would be taken for one another at one level are refused by name, and so is a command ExSheet has not', () => {
        expect(() => sheetOf({ showToolbar: true, toolbar: ['default', { label: 'Help', keyTip: 'H', items: ['bold'] }] }))
            .toThrow("The KeyTips 'H' of ExSheet's default row and 'H' of the Toolbar Row 'Help' collide at one level");
        expect(() => sheetOf({ showToolbar: true, toolbar: [{ label: 'Mine', items: ['borders', { text: 'Book', keyTip: 'BK', onPress: () => undefined }] }] }))
            .toThrow("The KeyTips 'B' of the item 'borders' and 'BK' of the button 'Book' collide at one level");
        const unknown: unknown = { showToolbar: true, toolbar: [{ label: 'Mine', items: ['wrapText'] }] };
        expect(() => sheetOf(unknown as SheetOptions)).toThrow("'wrapText' is not one of ExSheet's toolbar commands");
        expect(() => sheetOf({ showToolbar: true, toolbar: [] })).toThrow("A Sheet Toolbar's rows are a list of one row or more");
        expect(() => sheetOf({ showToolbar: true, toolbar: [{ label: 'Later', keyTip: 'L', items: [] }] })).toThrow("The items of the Toolbar Row 'Later' are a list of one item or more");
    });

    it('ADR-0100: Ctrl+F1 tells the toolbar\'s other state where the Consumer hears it, and is the browser\'s where it does not', async () => {
        const told: boolean[] = [];
        const { element, sheet } = sheetOf({ showToolbar: true });
        await vi.waitFor(() => { expect(element.querySelector('.ex-viewport')).not.toBeNull(); });
        press(element, 0, 0);
        const unheard = new KeyboardEvent('keydown', { key: 'F1', ctrlKey: true, bubbles: true, cancelable: true });
        document.activeElement?.dispatchEvent(unheard);
        expect(unheard.defaultPrevented).toBe(false);
        sheet.update({ locale: 'en-US', viewportWidth: 600, viewportHeight: 300, showToolbar: true, onShowToolbarChanged: shown => { told.push(shown); } });
        const heard = new KeyboardEvent('keydown', { key: 'F1', ctrlKey: true, bubbles: true, cancelable: true });
        document.activeElement?.dispatchEvent(heard);
        expect(heard.defaultPrevented).toBe(true);
        sheet.update({ locale: 'en-US', viewportWidth: 600, viewportHeight: 300, showToolbar: false, onShowToolbarChanged: shown => { told.push(shown); } });
        key('F1', { ctrlKey: true });
        // The Sheet holds nothing: each press tells the other value of what the Consumer handed it.
        expect(told).toEqual([false, true]);
    });

    it('ADR-0100: while an edit is open every item is unavailable, and says why', async () => {
        const { element } = sheetOf({ showToolbar: true });
        await vi.waitFor(() => { expect(element.querySelector('.ex-viewport')).not.toBeNull(); });
        press(element, 0, 0);
        key('Z');
        await vi.waitFor(() => { expect(toolbarItem(element, 'Bold').disabled).toBe(true); });
        expect(toolbarItem(element, 'Bold').title).toBe('Bold (Ctrl+B): A cell is being edited. Press Enter to commit the edit or Escape to cancel it first.');
        key('Escape');
        await vi.waitFor(() => { expect(toolbarItem(element, 'Bold').disabled).toBe(false); });
    });
});

// A pointer event on the Viewport at the offsets the grid reads.
function onViewport(element: HTMLElement, type: string, x: number, y: number): void {
    const viewport = element.querySelector('.ex-viewport');
    if (viewport === null) throw new Error('The Sheet has no Viewport.');
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mouseup' ? 0 : 1 });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    viewport.dispatchEvent(event);
}

// A Chrome of the test's own for the toolbar's lists, as the Docs' ToolbarOwnList draws them: a button for each menu and
// split control, its list its own; every other item left to the built-in Chrome.
function ownLists(): { readonly chrome: SheetChrome; readonly contexts: Map<string, SheetToolbarItemSlotContext>; readonly cleanups: string[] } {
    const contexts = new Map<string, SheetToolbarItemSlotContext>();
    const cleanups: string[] = [];
    const chrome: SheetChrome = {
        toolbarItem: (slot, context) => {
            if (context.kind !== 'menu' && context.kind !== 'split') return undefined;
            const button = document.createElement('button');
            button.className = 'own-list';
            button.textContent = context.view().name;
            slot.append(button);
            contexts.set(context.view().name, context);
            return () => { cleanups.push(context.view().name); button.remove(); };
        },
    };
    return { chrome, contexts, cleanups };
}

function own(contexts: Map<string, SheetToolbarItemSlotContext>, name: string): SheetToolbarItemSlotContext {
    const found = contexts.get(name);
    if (found === undefined) throw new Error(`The Chrome drew no item named ${name}.`);
    return found;
}

const choiceIndex = (context: SheetToolbarItemSlotContext, label: string): number => context.view().choices.findIndex(choice => choice.label === label);

describe('the Sheet Toolbar drawn by a Chrome (ADR-0100, ADR-0390)', () => {
    it('ADR-0390: the slot draws the items it answers for and the built-in Chrome the rest; the band\'s keys ask for the list, and a choice handed back is made with the keyboard on the Sheet', async () => {
        const { chrome, contexts } = ownLists();
        const { element, sheet } = sheetOf({ showToolbar: true, chrome, document: documentOf({ A1: 'x' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('x'); });
        press(element, 0, 0);
        const grid = document.activeElement;
        expect([...element.querySelectorAll('.ex-sheet-toolbar-slot .own-list')].map(button => button.textContent))
            .toEqual(['Font Colour', 'Fill Colour', 'Borders', 'Number Format']);
        expect(toolbarItem(element, 'Bold').getAttribute('aria-pressed')).toBe('false');
        const fill = own(contexts, 'Fill Colour');
        expect([fill.kind, fill.view().choices.length, fill.view().choices[0], fill.view().openRequest])
            .toEqual(['split', 71, { label: 'No Fill', swatch: undefined, selected: true, disabledReason: undefined }, 0]);
        // ↓ on the item asks for its list; the contents take the keyboard to their own.
        const band = element.querySelector<HTMLElement>('.ex-sheet-toolbar');
        band?.focus();
        while (!fill.view().active) key('ArrowRight');
        expect(band?.getAttribute('aria-activedescendant')).toBe(element.querySelectorAll('.ex-sheet-toolbar-slot')[1]?.id);
        let heard = 0;
        const stop = fill.subscribe(() => { heard += 1; });
        key('ArrowDown');
        expect([fill.view().openRequest, fill.view().closeRequest, heard > 0]).toEqual([1, 0, true]);
        stop();
        const list = document.createElement('div');
        list.tabIndex = -1;
        document.body.append(list);
        list.focus();
        fill.choicesClosed(choiceIndex(fill, 'Red'), false);
        expect(document.activeElement).toBe(grid);
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A1')).fill.colour?.toString()).toBe('#FF0000');
        await vi.waitFor(() => { expect(fill.view().choices.find(choice => choice.label === 'Red')?.selected).toBe(true); });
        // The face sets what was chosen last, as the built-in list's choice leaves it.
        sheet.undo();
        fill.invoke();
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A1')).fill.colour?.toString()).toBe('#FF0000');
        // A choice that is not the list's, or cannot be chosen, is refused by name.
        expect(() => { fill.choicesClosed(71, false); }).toThrow(RangeError);
        const numberFormat = own(contexts, 'Number Format');
        expect(() => { numberFormat.choicesClosed(choiceIndex(numberFormat, 'Accounting'), false); }).toThrow(/Accounting/u);
        // A menu's own press opens the built-in list.
        numberFormat.invoke();
        expect(choicesOf(element).list.getAttribute('aria-label')).toBe('Number Format');
    });

    it('ADR-0100: Escape in a list a KeyTip asked for goes back to the row\'s KeyTips; the band giving the keyboard back first closes the list it asked for', async () => {
        const { chrome, contexts } = ownLists();
        const { element } = sheetOf({ showToolbar: true, chrome, document: documentOf({ A1: 'x' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('x'); });
        press(element, 0, 0);
        const grid = document.activeElement;
        const band = element.querySelector<HTMLElement>('.ex-sheet-toolbar');
        const fill = own(contexts, 'Fill Colour');
        key('F10');
        key('h');
        expect(element.querySelector('.ex-sheet-toolbar-slot .ex-sheet-keytip')?.textContent).toBe('FC');
        key('h');
        expect(fill.view().openRequest).toBe(1);
        fill.choicesClosed(undefined, true);
        expect(document.activeElement).toBe(band);
        expect(element.querySelectorAll('.ex-sheet-keytip')).toHaveLength(14);
        key('Escape');
        key('Escape');
        expect(document.activeElement).toBe(grid);
        // Asked for from the band's keys, and the band gives the keyboard back before the list takes it.
        band?.focus();
        while (!fill.view().active) key('ArrowRight');
        key('ArrowDown');
        key('Escape');
        expect([fill.view().openRequest, fill.view().closeRequest, document.activeElement]).toEqual([2, 2, grid]);
    });

    it('ADR-0100: a list the Chrome opened by a press reads the Selection it opened over: a Selection moved, or an operation run, asks it to close', async () => {
        const { chrome, contexts } = ownLists();
        const { element, sheet } = sheetOf({ showToolbar: true, chrome, document: documentOf({ A1: 'x', A2: 'y' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('x'); });
        press(element, 0, 0);
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('x'); });
        const fill = own(contexts, 'Fill Colour');
        const before = fill.view();
        expect(before.openRequest).toBe(before.closeRequest);
        press(element, 1, 0);
        await vi.waitFor(() => { expect(fill.view().closeRequest).toBeGreaterThan(before.closeRequest); });
        // Both move on together: there is no request for the contents to answer, only a list to close.
        expect(fill.view().openRequest).toBe(fill.view().closeRequest);
        const moved = fill.view().closeRequest;
        sheet.do(SheetEdit.enter(CellAddress.parse('B1'), '1'));
        expect(fill.view().closeRequest).toBeGreaterThan(moved);
    });

    it('ADR-0390: a slot that throws is thrown once, and its item is the built-in Chrome\'s; a context left to the built-in Chrome reaches nothing', async () => {
        const declined: SheetToolbarItemSlotContext[] = [];
        let throws = 0;
        const chrome: SheetChrome = {
            toolbarItem: (_slot, context) => {
                declined.push(context);
                if (context.kind === 'menu') {
                    throws += 1;
                    throw new Error('The menu could not be drawn.');
                }
                return undefined;
            },
        };
        const options: SheetOptions = { locale: 'en-US', viewportWidth: 600, viewportHeight: 300, showToolbar: true, document: documentOf({ A1: 'x' }) };
        const { element, sheet } = sheetOf(options);
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('x'); });
        expect(() => { sheet.update({ ...options, chrome }); }).toThrow('The menu could not be drawn.');
        press(element, 0, 0);
        await vi.waitFor(() => { expect(toolbarItem(element, 'Number Format').textContent).toBe('General'); });
        expect(throws).toBe(1);
        const bold = declined.find(context => context.view().name === 'Bold');
        bold?.invoke();
        expect(sheet.workbook.shown.getCellFormat(CellAddress.parse('A1')).font.bold).toBe(false);
    });

    it('ADR-0390: contents go with their item, the slot withdrawn and the Sheet; a slot that answers anything else is refused by name', async () => {
        const { chrome, cleanups } = ownLists();
        const { element, sheet } = sheetOf({ showToolbar: true, chrome });
        await vi.waitFor(() => { expect(element.querySelectorAll('.own-list')).toHaveLength(4); });
        sheet.update({ locale: 'en-US', viewportWidth: 600, viewportHeight: 300, showToolbar: true, chrome, toolbar: [{ label: 'Mine', items: ['bold', 'fill'] }] });
        expect([...cleanups]).toEqual(['Font Colour', 'Borders', 'Number Format']);
        sheet.update({ locale: 'en-US', viewportWidth: 600, viewportHeight: 300, showToolbar: true, toolbar: [{ label: 'Mine', items: ['bold', 'fill'] }] });
        expect(cleanups).toHaveLength(4);
        expect(splitPart(element, 'Fill Colour', 'arrow').getAttribute('aria-label')).toBe('Fill Colour, more');
        sheet.update({ locale: 'en-US', viewportWidth: 600, viewportHeight: 300, showToolbar: true, chrome });
        expect(element.querySelectorAll('.own-list')).toHaveLength(4);
        sheet.destroy();
        expect(cleanups).toHaveLength(8);
        const loose: unknown = { toolbarItem: 'draw' };
        expect(() => sheetOf({ showToolbar: true, chrome: loose as SheetChrome })).toThrow('chrome.toolbarItem is a mount function (ADR-0390).');
        expect(() => sheetOf({ showToolbar: true, chrome: { toolbarItem: () => (1 as unknown as () => void) } }))
            .toThrow(/answers the function that takes its contents away/u);
    });
});

describe('the fill handle (ADR-0050, item 5)', () => {
    it('ADR-0050: dragging the handle down fills the series the engine reads, as one step on the undo stack, and selects source and target', async () => {
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '1', A2: '2' }) });
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('2'); });
        press(element, 0, 0);
        key('ArrowDown', { shiftKey: true });
        const handle = await vi.waitFor(() => {
            const found = element.querySelector<HTMLElement>('.ex-selection .ex-fill-handle');
            if (found === null) throw new Error('No fill handle is painted.');
            return found;
        });
        const centre = (side: 'left' | 'top'): number => Number.parseFloat(handle.style[side]) + (Number.parseFloat(handle.style.width) / 2);
        const x = centre('left');
        onViewport(element, 'mousedown', x, centre('top'));
        onViewport(element, 'mousemove', x - 20, (4 * 28) + 5);
        onViewport(element, 'mouseup', x - 20, (4 * 28) + 5);
        await vi.waitFor(() => { expect([2, 3, 4].map(row => textAt(element, row, 0))).toEqual(['3', '4', '5']); });
        await vi.waitFor(() => { expect(element.querySelector('.ex-selection .ex-range')?.getAttribute('style')).toContain(`height: ${String(5 * 28)}px`); });
        expect(sheet.undo()).toBe(true);
        await vi.waitFor(() => { expect(textAt(element, 2, 0)).toBe(''); });
        expect(textAt(element, 4, 0)).toBe('');
    });
});

/** The refusal's reason a command throws, or none where it does not throw one. */
function refusalOf(command: () => unknown): string | undefined {
    try {
        command();
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal is what the test reads; anything else is rethrown
        if (error instanceof SheetRefusedError) return error.refusal.reason;
        throw error;
    }
    return undefined;
}

describe("the Consumer's commands (ADR-0048, ADR-0331 §6, ADR-0071)", () => {
    it('ADR-0048: a command is one step on the user\'s undo stack, which canUndo and canRedo follow', async () => {
        const documents: unknown[] = [];
        const { element, sheet } = sheetOf({ document: documentOf({ A1: 'first' }), onWorkbookChanged: document => { documents.push(document); } });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('first'); });
        expect([sheet.canUndo, sheet.canRedo]).toEqual([false, false]);
        sheet.do(SheetEdit.insertRows(0));
        await vi.waitFor(() => { expect(textAt(element, 1, 0)).toBe('first'); });
        expect([sheet.canUndo, sheet.canRedo, documents.length]).toEqual([true, false, 1]);
        expect(sheet.undo()).toBe(true);
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('first'); });
        expect([sheet.canUndo, sheet.canRedo]).toEqual([false, true]);
    });

    it('ADR-0331 §6: with several Sheets a command or a read naming none is refused; one naming its Sheet acts there, shown or not, and Sheet operations are heard', async () => {
        const changes: SheetsChange[] = [];
        const { element, sheet } = sheetOf({ document: twoSheets(), onSheetsChanged: change => { changes.push(change); } });
        await vi.waitFor(() => { expect(element.querySelectorAll('.ex-sheet-tab')).toHaveLength(2); });
        expect(refusalOf(() => { sheet.do(SheetEdit.insertRows(0)); })).toBe('sheetNotNamed');
        expect(refusalOf(() => sheet.cellFormatAt(CellAddress.parse('A1')))).toBe('sheetNotNamed');
        expect(() => { sheet.do('Third', SheetEdit.insertRows(0)); }).toThrow(RangeError);
        sheet.do('Second', SheetEdit.enter(CellAddress.parse('A2'), 'below'));
        expect(sheet.workbook.shown.name).toBe('Sheet1');
        expect(sheet.workbook.find('Second')?.getEntryText(CellAddress.parse('A2'))).toBe('below');
        expect(changes).toEqual([]);
        sheet.do('Second', SheetEdit.rename('Rates'));
        expect(changes).toEqual([{ before: ['Sheet1', 'Second'], after: ['Sheet1', 'Rates'], shown: 'Sheet1' }]);
        // An undo shows the Sheet its step was taken on (ADR-0331 §3).
        expect(sheet.undo()).toBe(true);
        expect(changes.at(-1)).toEqual({ before: ['Sheet1', 'Rates'], after: ['Sheet1', 'Second'], shown: 'Second' });
    });

    it('ADR-0071: setCellFormat sets what it names on the Selection the grid holds, as one step, and cellFormatAt reads it back', async () => {
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '1', A2: '2' }) });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('1'); });
        expect(() => sheet.setCellFormat(new CellFormatChange({}))).toThrow(RangeError);
        press(element, 0, 0);
        key('ArrowDown', { shiftKey: true });
        expect(sheet.setCellFormat(new CellFormatChange({ bold: true }))).toBe(true);
        expect([sheet.cellFormatAt(CellAddress.parse('A1')).font.bold, sheet.cellFormatAt(CellAddress.parse('A2')).font.bold]).toEqual([true, true]);
        expect(sheet.cellFormatAt(CellAddress.parse('A3')).font.bold).toBe(false);
        expect(sheet.undo()).toBe(true);
        expect(sheet.cellFormatAt(CellAddress.parse('A2')).font.bold).toBe(false);
    });

    it('ADR-0048: while a cell is edited every command is refused by name and the reads answer; onEditingChanged hears the edit open and end', async () => {
        const heard: boolean[] = [];
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '1' }), onEditingChanged: open => { heard.push(open); } });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('1'); });
        press(element, 0, 0);
        key('F2');
        expect([sheet.isEditing, heard]).toEqual([true, [true]]);
        expect(refusalOf(() => { sheet.do(SheetEdit.insertRows(0)); })).toBe('editIsOpen');
        expect(refusalOf(() => sheet.setCellFormat(new CellFormatChange({ bold: true })))).toBe('editIsOpen');
        expect(refusalOf(() => sheet.openFormatCells())).toBe('editIsOpen');
        expect(sheet.cellFormatAt(CellAddress.parse('A1')).font.bold).toBe(false);
        key('Escape');
        expect([sheet.isEditing, heard]).toEqual([false, [true, false]]);
    });

    it('ADR-0071: openFormatCells opens Format Cells over the Selection the grid holds, and answers false over none', async () => {
        const selections: number[] = [];
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '1' }), onSelectionChanged: selection => { selections.push(selection.ranges.length); } });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('1'); });
        expect(sheet.openFormatCells()).toBe(false);
        expect(element.querySelector('.ex-format-cells')).toBeNull();
        press(element, 0, 0);
        await vi.waitFor(() => { expect(selections.at(-1)).toBe(1); });
        expect(sheet.openFormatCells()).toBe(true);
        expect(element.querySelector('.ex-format-cells')).not.toBeNull();
    });
});

describe('the Sheet Seed (ADR-0180)', () => {
    it('ADR-0180: a fixed seed draws the same whenever the document is opened; one out of range is refused, and a new one draws again without a step or a document', async () => {
        const document = documentOf({ A1: '=RANDBETWEEN(1, 1000000)' });
        const documents: unknown[] = [];
        const first = sheetOf({ document, seed: 20261011n, onWorkbookChanged: raised => { documents.push(raised); } });
        const second = sheetOf({ document, seed: 20261011n });
        await vi.waitFor(() => { expect(textAt(first.element, 0, 0)).toMatch(/^\d+$/u); });
        const drawn = textAt(first.element, 0, 0);
        expect(textAt(second.element, 0, 0)).toBe(drawn);
        // A seed out of range is refused before the Sheet takes anything of the options.
        expect(() => { first.sheet.update({ locale: 'en-US', viewportWidth: 600, viewportHeight: 300, document: documentOf({}), seed: -1n }); }).toThrow(RangeError);
        expect(textAt(first.element, 0, 0)).toBe(drawn);
        first.sheet.update({ locale: 'en-US', viewportWidth: 600, viewportHeight: 300, document, seed: 7n, onWorkbookChanged: raised => { documents.push(raised); } });
        await vi.waitFor(() => { expect(textAt(first.element, 0, 0)).not.toBe(drawn); });
        expect([first.sheet.canUndo, documents.length]).toEqual([false, 0]);
    });

    it('ADR-0180: without a seed the Sheet chooses one as it opens the document, so its draws show at once', async () => {
        const { element } = sheetOf({ document: documentOf({ A1: '=RAND()' }) });
        await vi.waitFor(() => { expect(Number(textAt(element, 0, 0))).toBeGreaterThanOrEqual(0); });
        expect(textAt(element, 0, 0)).not.toBe('#GETTING_DATA');
    });
});

describe('the Sheet Day and NOW (ADR-0121, ADR-0124)', () => {
    afterEach(() => { vi.useRealTimers(); });

    it('ADR-0121: TODAY is the day in the browser\'s zone at once, and a fixed day goes before the clock', async () => {
        vi.useFakeTimers({ toFake: ['Date', 'setTimeout', 'clearTimeout'], now: new Date('2026-10-27T12:00:00Z') });
        const document = documentOf({ A1: '=DAY(TODAY())', B1: '=MONTH(TODAY())' });
        const clock = sheetOf({ document, timeZone: 'UTC' });
        const fixed = sheetOf({ document, timeZone: 'UTC', today: { year: 2024, month: 2, day: 29 } });
        const browser = sheetOf({ document });
        await vi.waitFor(() => { expect([textAt(clock.element, 0, 0), textAt(clock.element, 0, 1)]).toEqual(['27', '10']); });
        await vi.waitFor(() => { expect([textAt(fixed.element, 0, 0), textAt(fixed.element, 0, 1)]).toEqual(['29', '2']); });
        await vi.waitFor(() => { expect(Number(textAt(browser.element, 0, 0))).toBeGreaterThan(0); });
    });

    it('ADR-0121: the day is the zone\'s, read again as midnight passes there, and a new zone reads at once', async () => {
        vi.useFakeTimers({ toFake: ['Date', 'setTimeout', 'clearTimeout'], now: new Date('2026-10-27T14:59:30Z') });
        const document = documentOf({ A1: '=DAY(TODAY())' });
        const options = { locale: 'en-US', viewportWidth: 600, viewportHeight: 300, document } as const;
        const { element, sheet } = sheetOf({ ...options, timeZone: 'Asia/Tokyo' });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('27'); });
        vi.advanceTimersByTime(31_000);
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('28'); });
        sheet.update({ ...options, timeZone: 'UTC' });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('27'); });
        expect(sheet.canUndo).toBe(false);
    });

    it('ADR-0124: NOW moves on as the minute turns, with no step to undo', async () => {
        vi.useFakeTimers({ toFake: ['Date', 'setTimeout', 'clearTimeout'], now: new Date('2026-10-27T10:15:30Z') });
        const { element, sheet } = sheetOf({ document: documentOf({ A1: '=MINUTE(NOW())' }), timeZone: 'UTC' });
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('15'); });
        vi.advanceTimersByTime(30_000);
        await vi.waitFor(() => { expect(textAt(element, 0, 0)).toBe('16'); });
        expect(sheet.canUndo).toBe(false);
    });

    it('ADR-0121: a zone the browser does not know, and a day that is not one, are refused by name', () => {
        expect(() => sheetOf({ timeZone: 'Mars/Olympus_Mons' })).toThrow(RangeError);
        expect(() => sheetOf({ today: { year: 2026, month: 2, day: 30 } })).toThrow(RangeError);
    });
});
