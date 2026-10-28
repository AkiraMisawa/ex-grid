// @vitest-environment happy-dom
// The bridge's own tests (ADR-0370 section 1): what crosses between .NET and ExSheet's Script Core. What the Sheet
// draws and decides is the Script Core's, tested there, and the documents' JSON is the engines' shared cases'; here,
// that a document crosses as .NET wrote it and comes back as the documents the Razor component raises, that one the
// Sheet raised and handed back opens nothing, that a refusal crosses as data, and that the Consumer's words reach the
// grid and the Sheet by the Razor component's ids.
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createGrid, gridCommandIds, type GridHandle, type GridPointedAt } from '@ex-grid/core';
import { CellAddress, Culture, SheetDocument, SheetEdit, Workbook, WorkbookDocument } from '@ex-grid/sheet-engine';
import { awaitPointingScope, inlineBytes, type WireHeld } from '@ex-grid/blazor-shared';
import {
    attach, labelIds, wordsOf, type BlazorSheet, type DotNetHandle, type WireAttach, type WireDocuments, type WireOptions, type WireSaved,
} from '../src/blazor-sheet';

/** .NET, as the bridge calls it: every event recorded. */
class FakeDotNet implements DotNetHandle {
    readonly heard: { readonly kind: string; readonly payload: unknown }[] = [];
    /** The calls other than `Heard`, each with its arguments. */
    readonly called: { readonly method: string; readonly args: readonly unknown[] }[] = [];
    /** What .NET answers a call other than `Heard`, by its method. */
    readonly answers = new Map<string, (...args: readonly unknown[]) => Promise<unknown>>();

    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        if (method === 'Heard') {
            this.heard.push({ kind: String(args[0]), payload: args[1] });
            return Promise.resolve(true);
        }
        this.called.push({ method, args });
        const answer = this.answers.get(method);
        if (answer === undefined) throw new Error(`the bridge called ${method}`);
        return answer(...args);
    }

    of(kind: string): readonly unknown[] {
        return this.heard.filter(event => event.kind === kind).map(event => event.payload);
    }
}

/** An answer of .NET's that comes when the test says. */
class Deferred {
    resolve: (value: unknown) => void = Deferred.#unset;
    readonly promise = new Promise<unknown>(settle => { this.resolve = settle; });

    static #unset(this: void): void {
        throw new Error('Internal: the promise is not yet made.');
    }
}

const attached: BlazorSheet[] = [];

afterEach(() => {
    for (const sheet of attached.splice(0)) sheet.dispose();
    document.body.replaceChildren();
});

const options = (more: Partial<WireOptions> = {}): WireOptions =>
    ({ locale: 'en-US', viewportWidth: 600, viewportHeight: 300, hears: ['workbookChanged', 'documentChanged'], ...more });

function mount(init: Partial<WireAttach> = {}): { readonly element: HTMLElement; readonly sheet: BlazorSheet; readonly dotnet: FakeDotNet } {
    const element = document.createElement('div');
    document.body.append(element);
    const dotnet = new FakeDotNet();
    const sheet = attach(element, dotnet, JSON.stringify({ options: options(), document: null, documentsSent: 0, words: null, circuit: false, ...init }));
    attached.push(sheet);
    return { element, sheet, dotnet };
}

/** A Workbook holding `entries` on its Sheets, as the JSON .NET writes. */
function workbookJson(...sheets: readonly (readonly [string, Readonly<Record<string, string>>])[]): string {
    const workbook = new Workbook(Culture.of('en-US'));
    let previous = 'Sheet1';
    sheets.forEach(([name], at) => {
        workbook.do(previous, at === 0 ? SheetEdit.rename(name) : SheetEdit.insertSheet(name));
        previous = name;
    });
    for (const [name, entries] of sheets) {
        for (const [address, typed] of Object.entries(entries)) workbook.do(name, SheetEdit.enter(CellAddress.parse(address), typed));
    }
    return workbook.toDocument().toJson();
}

const cellText = (element: HTMLElement, row: number, column: number): string | null =>
    element.querySelector(`.ex-viewport > .ex-row[aria-rowindex="${String(row + 2)}"] > [aria-colindex="${String(column + 1)}"]`)?.textContent ?? null; // oxlint-disable-line ex-grid/no-stand-in-default -- a cell not painted reads as null, which the expectation names

function select(element: HTMLElement, address: string): void {
    const box = element.querySelector<HTMLInputElement>('input.ex-name-box');
    if (box === null) throw new Error('The Sheet shows no Name Box.');
    box.focus();
    box.value = address;
    box.dispatchEvent(new Event('input', { bubbles: true }));
    element.querySelector('form.ex-name-box-form')?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
}

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

/** A press on a cell of a grid of 100-pixel columns and 24-pixel rows, with no Row Headings. */
function pressGrid(element: HTMLElement, row: number, column: number, init: MouseEventInit = {}): void {
    const viewport = element.querySelector<HTMLElement>('.ex-viewport');
    if (viewport === null) throw new Error('The grid has no viewport.');
    const event = new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0, buttons: 1, ...init });
    Object.defineProperty(event, 'offsetX', { value: (column * 100) + 10 });
    Object.defineProperty(event, 'offsetY', { value: (row * 24) + 5 });
    viewport.dispatchEvent(event);
}

/** The text a test expects there. */
function must(text: string | null): string {
    if (text === null) throw new Error('No document was raised.');
    return text;
}

function key(name: string): void {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
}

/** The documents, which a test of a small Workbook expects in the answer itself. */
function documentsOf(sheet: BlazorSheet): WireDocuments {
    const answer = sheet.documents();
    if ('held' in answer) throw new Error('The documents were held.');
    return answer;
}

/** A held message, taken as .NET takes it: its stream, unpacked, read as JSON. */
async function taken(sheet: BlazorSheet, held: WireHeld): Promise<unknown> {
    const packed = await sheet.take(held.held);
    const bytes = new Uint8Array(await new Response(new Blob([packed]).stream().pipeThrough(new DecompressionStream('gzip'))).arrayBuffer());
    expect(bytes.byteLength).toBe(held.bytes);
    return JSON.parse(new TextDecoder().decode(bytes));
}

interface Raised {
    readonly workbook: string | null;
    readonly sheet: string | null;
    readonly documentsSent: number;
}

describe('documents across the bridge (ADR-0370 section 1, ADR-0048, ADR-0331)', () => {
    it('opens the document .NET wrote, and raises the Workbook and its one Sheet as documents after a change', async () => {
        const { element, dotnet } = mount({ document: workbookJson(['Prices', { A1: '2', A2: '=A1*3' }]), documentsSent: 1 });
        await vi.waitFor(() => { expect(cellText(element, 1, 0)).toBe('6'); });

        select(element, 'A1');
        key('Delete');

        await vi.waitFor(() => { expect(dotnet.of('workbookChanged')).toHaveLength(1); });
        const raised = dotnet.of('workbookChanged')[0] as Raised;
        expect(raised.documentsSent).toBe(1);
        expect(WorkbookDocument.fromJson(must(raised.workbook)).sheets.map(sheet => sheet.name)).toEqual(['Prices']);
        // A Sheet Document of the one Sheet, as the Razor component's DocumentChanged hands it.
        const sheet = SheetDocument.fromJson(must(raised.sheet));
        expect(sheet.name).toBe('Prices');
        expect(sheet.cells.map(cell => cell.address.toString())).toEqual(['A2']);
    });

    it('raises no Sheet Document while the Workbook holds several, and only the documents .NET listens to', async () => {
        const { element, dotnet } = mount({
            document: workbookJson(['One', { A1: '1' }], ['Two', {}]),
            documentsSent: 1,
            options: options({ hears: ['documentChanged'] }),
        });
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).toBe('1'); });

        select(element, 'A1');
        key('Delete');

        await vi.waitFor(() => { expect(dotnet.of('workbookChanged')).toEqual([{ workbook: null, sheet: null, documentsSent: 1 }]); });
    });

    it('keeps the user\'s change when the parameters move and no document of the Consumer\'s came, and opens one that did', async () => {
        const { element, sheet, dotnet } = mount({ document: workbookJson(['Sheet1', { A1: 'kept' }]), documentsSent: 1 });
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).toBe('kept'); });
        select(element, 'A1');
        key('Delete');
        await vi.waitFor(() => { expect(dotnet.of('workbookChanged')).toHaveLength(1); });

        // Another parameter: the Workbook the Sheet raised is the one it is handed again.
        sheet.update(JSON.stringify({ options: options({ showFormulaBar: false }) }));
        expect(cellText(element, 0, 0)).toBe('');
        expect(documentsOf(sheet).workbook).toBe((dotnet.of('workbookChanged')[0] as Raised).workbook);

        // A document of the Consumer's own making replaces it, and what the Sheet raises next says so.
        sheet.update(JSON.stringify({ options: options(), document: workbookJson(['Sheet1', { A1: 'new' }]), documentsSent: 2 }));
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).toBe('new'); });
        select(element, 'A1');
        key('Delete');
        await vi.waitFor(() => { expect(dotnet.of('workbookChanged')).toHaveLength(2); });
        expect((dotnet.of('workbookChanged')[1] as Raised).documentsSent).toBe(2);

        // None, handed after one: a Workbook of one empty Sheet.
        sheet.update(JSON.stringify({ options: options(), document: workbookJson(['Sheet1', { B2: 'x' }]), documentsSent: 3 }));
        sheet.update(JSON.stringify({ options: options(), document: null, documentsSent: 4 }));
        expect(JSON.parse(documentsOf(sheet).workbook)).toMatchObject({ sheets: [{ name: 'Sheet1' }] });
        expect(SheetDocument.fromJson(must(documentsOf(sheet).sheet)).cells).toEqual([]);
    });

    it('counts a document the Sheet refused as sent, as .NET does, and keeps the Sheet as it stood', async () => {
        const { element, sheet, dotnet } = mount({ document: workbookJson(['Sheet1', { A1: 'kept' }]), documentsSent: 1 });
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).toBe('kept'); });

        // A version this engine does not read: refused, and .NET has counted it.
        const unread = JSON.stringify({ ...JSON.parse(workbookJson(['Sheet1', { A1: 'other' }])), workbookVersion: 999 });
        expect(() => { sheet.update(JSON.stringify({ options: options(), document: unread, documentsSent: 2 })); }).toThrow('The Workbook Document is version 999');
        expect(cellText(element, 0, 0)).toBe('kept');

        // What the Sheet raises next is on screen after the refused one, so .NET does not hand it back to be reopened.
        select(element, 'A1');
        key('Delete');
        await vi.waitFor(() => { expect(dotnet.of('workbookChanged')).toHaveLength(1); });
        expect((dotnet.of('workbookChanged')[0] as Raised).documentsSent).toBe(2);
    });
});

describe('the Selection Summary across the bridge (ADR-0130)', () => {
    it('hands .NET the summary the Sheet answered, an exact figure as its digits and no column names, and none unheard', async () => {
        const { element, dotnet } = mount({
            document: workbookJson(['Prices', { A1: '0.1', A2: '0.2', A3: 'x' }]),
            documentsSent: 1,
            options: options({ hears: ['selectionSummaryChanged'] }),
        });
        await vi.waitFor(() => { expect(cellText(element, 1, 0)).toBe('0.2'); });

        select(element, 'A1:A3');

        const request = {
            ranges: [{ topRow: 0, leftColumn: 0, rowCount: 3, columnCount: 1 }],
            rowSequenceVersion: 0,
            figures: ['average', 'count', 'sum'],
            focus: { row: 0, column: 0 },
        };
        await vi.waitFor(() => {
            expect(dotnet.of('selectionSummaryChanged')).toEqual([
                { status: 'pending', request, result: null },
                {
                    status: 'answered',
                    request,
                    result: {
                        // Count is exact, so it crosses as its digits; the doubles' sums cross as numbers.
                        figures: { average: { kind: 'number', number: 0.15000000000000002 }, count: { kind: 'number', exact: '3' }, sum: { kind: 'number', number: 0.30000000000000004 } },
                        texts: { average: '0.15', sum: '0.3' },
                    },
                },
            ]);
        });

        const unheard = mount({ document: workbookJson(['Prices', { A1: '1', A2: '2' }]), documentsSent: 1 });
        await vi.waitFor(() => { expect(cellText(unheard.element, 1, 0)).toBe('2'); });
        select(unheard.element, 'A1:A2');
        // Answered on screen, and not raised to .NET by the time an event raised after it, the editor opening, has come.
        await vi.waitFor(() => { expect(unheard.element.querySelector('.ex-summary')?.textContent).toContain('3'); });
        key('F2');
        await vi.waitFor(() => { expect(unheard.dotnet.of('editingChanged')).toEqual([true]); });
        expect(unheard.dotnet.of('selectionSummaryChanged')).toEqual([]);
    });
});

describe('commands across the bridge (ADR-0048)', () => {
    it('answers a command as the Sheet did, and a refusal as data .NET throws again', async () => {
        const { element, sheet, dotnet } = mount();
        expect(sheet.undo()).toEqual({ done: false });

        await vi.waitFor(() => { expect(cellText(element, 0, 0)).not.toBeNull(); });
        press(element, 0, 0);
        key('F2');
        await vi.waitFor(() => { expect(dotnet.of('editingChanged')).toEqual([true]); });

        expect(sheet.undo()).toEqual({ refused: { reason: 'editIsOpen', message: expect.any(String) as string } });
        expect(sheet.openFormatCells()).toMatchObject({ refused: { reason: 'editIsOpen' } });
    });
});

describe('edits, Cell Formats and Linked Tables across the bridge (ADR-0370 section 1, ADR-0048, ADR-0071, ADR-0049)', () => {
    it('does an edit .NET wrote, on the one Sheet or the Sheet it names, and throws what the engine threw as data', async () => {
        const { element, sheet, dotnet } = mount();
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).not.toBeNull(); });

        expect(sheet.do(null, '{"enter":{"A1":"2","A2":"=A1*3"}}')).toEqual({ done: null });
        await vi.waitFor(() => { expect(cellText(element, 1, 0)).toBe('6'); });
        expect(dotnet.of('workbookChanged')).toHaveLength(1);

        // A Formula that cannot be read is refused where it was typed, and .NET throws it as a FormulaSyntaxException.
        expect(sheet.do(null, '{"enter":{"B1":"=1+"}}')).toMatchObject({ thrown: { kind: 'formulaSyntax', formula: '=1+', position: expect.any(Number) as number } });

        // With two Sheets, an edit that names none is refused, and one that names its Sheet is done there.
        expect(sheet.do(null, '{"insertSheet":{"name":"Two","before":false}}')).toEqual({ done: null });
        expect(sheet.do(null, '{"enter":{"A1":"x"}}')).toMatchObject({ refused: { reason: 'sheetNotNamed' } });
        expect(sheet.do('Two', '{"enter":{"A1":"x"}}')).toEqual({ done: null });
        expect(sheet.do('Three', '{"enter":{"A1":"x"}}')).toMatchObject({ thrown: { kind: 'outOfRange' } });
        expect(WorkbookDocument.fromJson(documentsOf(sheet).workbook).sheets.map(s => s.name)).toEqual(['Sheet1', 'Two']);
    });

    it('sets a change on the Selection, and reads a cell\'s Cell Format back as .NET reads one', async () => {
        const { element, sheet } = mount();
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).not.toBeNull(); });
        select(element, 'B2');

        expect(sheet.setCellFormat('{"bold":true,"numberFormat":"0.00"}')).toEqual({ done: true });
        expect(sheet.cellFormatAt(null, 'B2')).toEqual({
            done: JSON.stringify({
                numberFormat: '0.00',
                alignment: 'general',
                font: { colour: 'Automatic', bold: true, italic: false, underline: false, strikethrough: false },
                fill: 'none',
                borders: { top: 'none', bottom: 'none', left: 'none', right: 'none' },
            }),
        });
        expect(sheet.cellFormatAt('Nowhere', 'B2')).toMatchObject({ thrown: { kind: 'outOfRange' } });
    });

    it('declares a Linked Table, takes a snapshot, and hands back a refused one whole', async () => {
        const { element, sheet } = mount();
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).not.toBeNull(); });

        expect(sheet.declareLinkedTable('Rates', ['Code', 'Rate'], ['Code'])).toEqual({ done: null });
        expect(sheet.linkedTables()).toEqual([{ name: 'Rates', columns: ['Code', 'Rate'], keyColumns: ['Code'], isWaiting: true, rowCount: 0 }]);
        expect(sheet.pushLinkedTable('Rates', '[["USD",1.5],["EUR",null]]')).toEqual({ done: null });
        expect(sheet.linkedTables()).toMatchObject([{ isWaiting: false, rowCount: 2 }]);

        const refused = sheet.pushLinkedTable('Rates', '[["USD",1],["usd",2]]');
        if (!('thrown' in refused) || refused.thrown.kind !== 'repeatedKey') throw new Error(`The snapshot was not refused: ${JSON.stringify(refused)}`);
        expect(JSON.parse(refused.thrown.repeatedKey)).toMatchObject({
            table: 'Rates', keyColumns: ['Code'], keys: ['USD'], repeats: ['usd'], firstRow: 0, secondRow: 1,
            change: { valueChanges: [], recalculated: [], rows: [], columns: [], reformatsUnnamedRows: false },
        });
        expect(sheet.linkedTables()).toMatchObject([{ isWaiting: true }]);
        expect(sheet.pushLinkedTable('Nothing', '[]')).toMatchObject({ thrown: { kind: 'invalidOperation' } });
    });
});

describe('files across the bridge (ADR-0330 section 10)', () => {
    it('opens what .NET read in the opening\'s turn, and answers a read that failed as .NET will throw it', async () => {
        const { element, sheet, dotnet } = mount({ document: workbookJson(['Sheet1', { A1: 'before' }]), documentsSent: 1 });
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).toBe('before'); });

        dotnet.answers.set('Read', () => Promise.resolve(JSON.stringify({ document: workbookJson(['Opened', { A1: 'after' }]) })));
        await expect(sheet.open(7)).resolves.toEqual({ done: true });
        expect(dotnet.called).toEqual([{ method: 'Read', args: [7] }]);
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).toBe('after'); });
        expect(WorkbookDocument.fromJson(must((dotnet.of('workbookChanged').at(-1) as Raised).workbook)).sheets.map(s => s.name)).toEqual(['Opened']);

        dotnet.answers.set('Read', () => Promise.resolve(JSON.stringify({ failed: 'The file is not an .xlsx workbook.' })));
        await expect(sheet.open(8)).resolves.toEqual({ thrown: { kind: 'invalidOperation', message: 'The file is not an .xlsx workbook.' } });
        expect(cellText(element, 0, 0)).toBe('after');
    });

    it('tells .NET to cancel a read an Escape cancelled, and answers that nothing opened', async () => {
        const { element, sheet, dotnet } = mount({ document: workbookJson(['Sheet1', { A1: 'kept' }]), documentsSent: 1 });
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).toBe('kept'); });
        press(element, 0, 0);

        // .NET's read takes until its token is cancelled.
        const read = new Deferred();
        dotnet.answers.set('Read', () => read.promise);
        dotnet.answers.set('CancelRead', () => {
            read.resolve(JSON.stringify({ cancelled: true }));
            return Promise.resolve(null);
        });
        const opening = sheet.open(3);
        await vi.waitFor(() => { expect(dotnet.called.map(call => call.method)).toEqual(['Read']); });
        key('Escape');

        await expect(opening).resolves.toEqual({ done: false });
        expect(dotnet.called).toEqual([{ method: 'Read', args: [3] }, { method: 'CancelRead', args: [3] }]);
        expect(cellText(element, 0, 0)).toBe('kept');
    });

    it('hands .NET the Workbook a save sees, with what .NET needs to compute the same Values, and answers a failed write', async () => {
        const { element, sheet, dotnet } = mount({
            document: workbookJson(['Sheet1', { A1: '=RAND()' }]), documentsSent: 1,
            options: options({ seed: '42', today: { year: 2026, month: 10, day: 28 } }),
        });
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).not.toBe(''); });
        expect(sheet.declareLinkedTable('Rates', ['Code', 'Rate'], ['Code'])).toEqual({ done: null });
        expect(sheet.pushLinkedTable('Rates', '[["USD",1.5],["EUR",{"error":"#VALUE!","refused":true}]]')).toEqual({ done: null });
        expect(sheet.declareLinkedTable('Later', ['Id'], [])).toEqual({ done: null });

        dotnet.answers.set('Write', () => Promise.resolve(JSON.stringify({ done: true })));
        await expect(sheet.save(5)).resolves.toEqual({ done: null });
        const [write] = dotnet.called;
        expect(write?.args[0]).toBe(5);
        const saved = write?.args[1] as WireSaved;
        expect(WorkbookDocument.fromJson(saved.document).linkedTables.map(table => table.name)).toEqual(['Rates', 'Later']);
        expect(saved.tables).toEqual([
            { name: 'Rates', rows: '[["USD",1.5],["EUR",{"error":"#VALUE!","refused":true}]]' },
            { name: 'Later', rows: null },
        ]);
        expect(saved).toMatchObject({ seed: '42', changes: expect.any(Number) as number, today: { year: 2026, month: 10, day: 28 } });

        // NOW is the moment the last recalculation read, which .NET answers too, never the clock as the save is made.
        expect(saved.now).toMatchObject({ year: expect.any(Number) as number, millisecond: expect.any(Number) as number });
        await new Promise(resolve => { setTimeout(resolve, 20); });
        dotnet.answers.set('Write', () => Promise.resolve(JSON.stringify({ failed: 'The disk is full.' })));
        await expect(sheet.save(6)).resolves.toEqual({ thrown: { kind: 'invalidOperation', message: 'The disk is full.' } });
        expect(dotnet.called[1]?.args[1]).toMatchObject({ now: saved.now });
    });
});

describe('messages too large for a circuit (Blazor Server\'s hub limit, ADR-0005 as amended 2026-10-28)', () => {
    it('holds an event, an answer and a save larger than a message carries, and hands each to .NET once, as a stream', async () => {
        const entries = Object.fromEntries(Array.from({ length: 600 }, (_, at) => [`A${String(at + 1)}`, `a text of some length, row ${String(at + 1)}`]));
        const { element, sheet, dotnet } = mount({ document: workbookJson(['Long', entries]), documentsSent: 1, circuit: true });
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).toBe('a text of some length, row 1'); });
        // .NET takes a held call's arguments before the call answers, as `HeldCalls` does: the bridge lets go of them then.
        const takenArgs: unknown[] = [];
        dotnet.answers.set('Held', async (method, crossed) => {
            const args = await taken(sheet, crossed as WireHeld);
            takenArgs.push(args);
            return method === 'Write' ? JSON.stringify({ done: true }) : null;
        });

        select(element, 'A1');
        key('Delete');

        // The event: no Heard, which would close the circuit, but the call held, by its number and length.
        await vi.waitFor(() => { expect(takenArgs).toHaveLength(1); });
        expect(dotnet.called.map(call => call.method)).toEqual(['Held']);
        expect(dotnet.of('workbookChanged')).toEqual([]);
        const heldCall = dotnet.called[0];
        if (heldCall === undefined) throw new Error('Nothing crossed.');
        const [method, held] = heldCall.args;
        expect(method).toBe('Heard');
        const event = held as WireHeld;
        expect(event.bytes).toBeGreaterThan(inlineBytes);
        const [kind, raised] = takenArgs[0] as [string, Raised];
        expect(kind).toBe('workbookChanged');
        expect(raised.documentsSent).toBe(1);
        expect(WorkbookDocument.fromJson(must(raised.workbook)).sheets[0]?.cells).toHaveLength(599);
        // Taken once: the bridge holds it no longer.
        expect(() => sheet.take(event.held)).toThrow('which the bridge does not hold');

        // An answer, held as the event was.
        const answer = sheet.documents();
        if (!('held' in answer)) throw new Error('The documents crossed in the answer.');
        expect((await taken(sheet, answer) as WireDocuments).workbook).toBe(raised.workbook);

        // A save's Workbook, held for .NET's write.
        await expect(sheet.save(1)).resolves.toEqual({ done: null });
        expect(dotnet.called.map(call => call.args[0])).toEqual(['Heard', 'Write']);
        const [id, saved] = takenArgs[1] as [number, WireSaved];
        expect(id).toBe(1);
        expect(saved.document).toBe(raised.workbook);

        // A small answer still crosses in itself, and without a circuit, as WebAssembly's .NET is, so does a large one.
        expect(sheet.undo()).toEqual({ done: true });
        const wasm = mount({ document: workbookJson(['Long', entries]), documentsSent: 1 });
        expect(wasm.sheet.documents()).toHaveProperty('workbook');
    });
});

describe('a Pointing Scope named by .NET (ADR-0430)', () => {
    /** A row of a registered grid as .NET's ExGrid sends it: its own values, and the table's row .NET read of it. */
    interface Position { readonly id: string; readonly pv: number; readonly table: readonly unknown[] }
    const positions: readonly Position[] = [
        { id: 'R-1', pv: 110, table: ['R-1', 110] }, { id: 'R-2', pv: 250, table: ['R-2', 250] }, { id: 'R-3', pv: 75, table: ['R-3', 75] },
    ];
    const grids: GridHandle<Position>[] = [];
    // A member a failed test leaves would stand in the page's next Scope of its name.
    const leaving: (() => void)[] = [];
    afterEach(() => {
        for (const grid of grids.splice(0)) grid.destroy();
        for (const leave of leaving.splice(0)) leave();
    });

    it('joins a grid waiting for the Scope to the Sheet that brings it, which writes the key .NET read and tells .NET a refusal', async () => {
        // The grid's module waits for the Scope before any Sheet of it stands, as a page may render them.
        const gridElement = document.createElement('div');
        document.body.append(gridElement);
        let pointedAt: GridPointedAt<Position> | undefined;
        leaving.push(awaitPointingScope('scope-1', scope => {
            pointedAt = scope.registerGrid<Position>('scope-1-1', 'Positions', {}, row => row.table);
        }));
        expect(pointedAt).toBeUndefined();

        const { element, sheet, dotnet } = mount({ options: options({ pointingScope: 'scope-1', hears: ['pointingRefused'] }) });
        if (pointedAt === undefined) throw new Error('The Sheet brought no Scope.');
        grids.push(createGrid<Position>(gridElement, {
            columns: [
                { name: 'Id', type: 'text', value: row => row.id, width: 100 },
                { name: 'PV', type: 'number', value: row => row.pv, width: 100 },
            ],
            rows: positions, rowHeight: 24, viewportHeight: 200, pointedAt,
        }));
        expect(sheet.declareLinkedTable('Positions', ['Id', 'PV'], ['Id'])).toEqual({ done: null });
        await vi.waitFor(() => { expect(cellText(element, 0, 0)).not.toBeNull(); });
        await vi.waitFor(() => { expect(gridElement.querySelector('[role="row"] [role="gridcell"]')).not.toBeNull(); });

        press(element, 0, 0);
        key('=');
        await vi.waitFor(() => { expect(gridElement.classList.contains('ex-pointed-at')).toBe(true); });
        pressGrid(gridElement, 1, 1);
        const editor = element.querySelector<HTMLInputElement>('input.ex-editor');
        await vi.waitFor(() => { expect(editor?.value).toBe('=XLOOKUP("R-2", Positions[Id], Positions[PV])'); });

        pressGrid(gridElement, 0, 1, { shiftKey: true });
        await vi.waitFor(() => { expect(dotnet.of('pointingRefused')).toMatchObject([{ reason: 'severalCells' }]); });
        expect(dotnet.of('pointingRefused')[0]).toHaveProperty('message');

        // The same declaration asked again is the one registered: a grid attached again stands in the Scope once.
        let again: GridPointedAt<Position> | undefined;
        leaving.push(awaitPointingScope('scope-1', scope => { again = scope.registerGrid<Position>('scope-1-1', 'Positions', {}, row => row.table); }));
        expect(again).toBe(pointedAt);
    });

    it('lets the page\'s Scope go with its last member', () => {
        let first: GridPointedAt<Position> | undefined;
        const { sheet } = mount({ options: options({ pointingScope: 'scope-2' }) });
        const leave = awaitPointingScope('scope-2', scope => { first = scope.registerGrid<Position>('scope-2-1', 'Positions', {}, row => row.table); });
        leave();
        sheet.dispose();
        attached.splice(attached.indexOf(sheet), 1);

        // Every member gone, the name is a new Scope: the declaration is registered afresh in it.
        mount({ options: options({ pointingScope: 'scope-2' }) });
        let second: GridPointedAt<Position> | undefined;
        leaving.push(awaitPointingScope('scope-2', scope => { second = scope.registerGrid<Position>('scope-2-1', 'Positions', {}, row => row.table); }));
        expect(second).toBeDefined();
        expect(second).not.toBe(first);
    });
});

describe('words across the bridge (ADR-0370 section 10, ADR-0036)', () => {
    it('asks .NET for the grid\'s ids and ExSheet\'s by the Razor component\'s names', () => {
        const ids = labelIds();
        expect(ids).toContain(gridCommandIds.copy);
        expect(ids).toEqual(expect.arrayContaining([
            'exsheet.insert-rows', 'exsheet.delete-rows', 'exsheet.insert-columns', 'exsheet.delete-columns', 'exsheet.format-cells',
            'exsheet.summary-refused',
        ]));
        expect(new Set(ids).size).toBe(ids.length);
    });

    it('words the grid\'s ids through its command label, and ExSheet\'s through the Sheet\'s own ids', () => {
        const words = wordsOf({ [gridCommandIds.copy]: 'コピー', 'exsheet.summary-refused': '集計なし: {0}', 'exsheet.format-cells': 'セルの書式設定' });
        expect(words.commandLabel?.(gridCommandIds.copy)).toBe('コピー');
        expect(words.commandLabel?.('exsheet.format-cells')).toBeUndefined();
        expect(words.label?.('summaryRefused', 'English')).toBe('集計なし: {0}');
        expect(words.label?.('command.formatCells', 'English')).toBe('セルの書式設定');
        expect(words.label?.('command.insertRows', 'English')).toBeUndefined();
        expect(wordsOf(null)).toEqual({ label: undefined, commandLabel: undefined });
    });
});
