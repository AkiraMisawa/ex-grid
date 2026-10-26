// @vitest-environment happy-dom
// ExPivot's Script Core in a DOM emulation (layer 2, ADR-0350 section 13). What the grid paints is the core's,
// tested there: here the grid is a stand-in that records what the pivot hands it, and the tests judge what the
// pivot decides — what it asks, which answer it shows, what the Field List draws and marks, what it refuses.
import { afterEach, describe, expect, it } from 'vitest';
import type { CellPosition, GridCommand, GridHandle, GridOptions, GridSelection, SelectionRange } from '@ex-grid/core';
import {
    FetchingPivotSource, PivotDetailPage, PivotItemPage, PivotSourceFeatures, RecordPivotSource, createPivotLayout, japanesePivotWords, pivotRecordField,
    type PivotDetailRecord, type PivotDisplayRow, type PivotLayout, type PivotSlicing, type PivotSource,
} from '@ex-grid/pivot-engine';
import { browserPivotLocale } from '../src/browser-locale';
import { defined } from '../src/must';
import type { PivotShowDetailsEvent } from '../src/details';
import { PivotView, type PivotSelectionChangedEvent, type PivotViewOptions } from '../src/pivot-view';

interface Trade {
    readonly region: string;
    readonly desk: string;
    readonly amount: number;
}

const trades: readonly Trade[] = [
    { region: 'EU', desk: 'Rates', amount: 10 },
    { region: 'EU', desk: 'Credit', amount: 20 },
    { region: 'US', desk: 'Rates', amount: 5 },
    { region: 'US', desk: 'FX', amount: 7 },
];

const fields = [
    pivotRecordField<Trade>({ name: 'Region', type: 'text', value: trade => trade.region }),
    pivotRecordField<Trade>({ name: 'Desk', type: 'text', value: trade => trade.desk }),
    pivotRecordField<Trade>({ name: 'Amount', type: 'number', value: trade => trade.amount }),
];

function local(): RecordPivotSource<Trade> {
    return new RecordPivotSource(trades, fields);
}

/** A server's source over the same trades, which counts each field's Items (ADR-0370 section 3). */
function server(): FetchingPivotSource {
    const behind = local();
    return new FetchingPivotSource(behind.fields, new PivotSourceFeatures(['sum', 'count'], false, true), {
        aggregate: (query, signal) => behind.aggregate(query, signal),
        details: (query, signal) => behind.details(query, signal),
        items: (query, signal) => behind.items(query, signal),
        itemCounts: signal => behind.itemCounts(signal),
    });
}

/** A source whose answers wait until the test lets each one through, in any order. */
class HeldSource implements PivotSource {
    readonly #behind: PivotSource;
    readonly held: (() => void)[] = [];
    readonly fields: PivotSource['fields'];
    readonly features: PivotSource['features'];

    constructor(behind: PivotSource = local()) {
        this.#behind = behind;
        this.fields = behind.fields;
        this.features = behind.features;
    }

    aggregate(...args: Parameters<PivotSource['aggregate']>): ReturnType<PivotSource['aggregate']> {
        // Asked once let through, as a source asks: one whose question was given up meanwhile refuses it then.
        return new Promise(resolve => { this.held.push(() => { resolve(Promise.resolve().then(() => this.#behind.aggregate(...args))); }); });
    }

    details(...args: Parameters<PivotSource['details']>): ReturnType<PivotSource['details']> {
        return this.#behind.details(...args);
    }

    items(...args: Parameters<PivotSource['items']>): ReturnType<PivotSource['items']> {
        return this.#behind.items(...args);
    }
}

/** A template cell mounted by a test: its slot, the controls it handed the keyboard to, and how to take it away or
 * mount it again into the same slot. */
interface PaintedCell {
    readonly slot: HTMLElement;
    readonly handed: readonly HTMLElement[];
    readonly dispose: () => void;
    readonly repaint: () => PaintedCell;
    readonly button: () => HTMLButtonElement;
}

/** A details grid as the pivot handed it: its element, the options it was last handed, and whether it is gone. */
interface DetailsGrid {
    readonly element: HTMLElement;
    options: GridOptions<PivotDetailRecord>;
    destroyed: boolean;
}

/** A Selection the pivot placed in the report's grid, and the Row Sequence Version it placed it under. */
interface Placed {
    readonly range: SelectionRange;
    readonly focus: CellPosition;
    readonly version: number;
}

/** The grids as the pivot sees them: the report's, by the options it was last handed, and each details grid. */
class GridStandIn {
    options: GridOptions<PivotDisplayRow> | undefined;
    mounts = 0;
    destroyed = false;
    readonly details: DetailsGrid[] = [];
    readonly placed: Placed[] = [];
    /** The element the report's grid was mounted into. */
    element: HTMLElement | undefined;

    readonly mount = <Row>(element: HTMLElement, options: GridOptions<Row>): GridHandle<Row> => {
        const handle = (update: (next: GridOptions<Row>) => void, destroy: () => void): GridHandle<Row> => ({
            update,
            destroy,
            placeSelection: (range, focus, version) => {
                if (element.closest('.ex-pivot-details-panel, .ex-pivot-dialog') !== null) throw new Error('The pivot places no selection in a details grid.');
                // The real grid refuses a version its source has moved past: the pivot places under the one it holds.
                const held = this.options?.source?.snapshot().rowSequenceVersion;
                if (version !== held) throw new Error(`A placement under version ${version}, but the source holds ${String(held)}.`);
                this.placed.push({ range, focus, version });
                return true;
            },
            readView: () => { throw new Error('The pivot reads no view.'); },
            showViewWith: () => { throw new Error('The pivot shows no view.'); },
            focus: () => { throw new Error('The pivot focuses no grid.'); },
            keyName: () => { throw new Error('The pivot names no key.'); },
            openPopover: () => { throw new Error('The pivot opens no popover.'); },
            closePopover: () => { throw new Error('The pivot closes no popover.'); },
            canSummarize: false,
            refreshSummary: () => { throw new Error('The pivot refreshes no summary.'); },
        });
        // A details grid stands in a details panel or the dialog; the report's in the report's sheet.
        if (element.closest('.ex-pivot-details-panel, .ex-pivot-dialog') !== null) {
            element.tabIndex = 0;
            const grid: DetailsGrid = { element, options: options as unknown as GridOptions<PivotDetailRecord>, destroyed: false };
            this.details.push(grid);
            return handle(next => { grid.options = next as unknown as GridOptions<PivotDetailRecord>; }, () => { grid.destroyed = true; });
        }
        this.mounts++;
        this.element = element;
        // The grid's root holds the keyboard on a grid that does not edit (ADR-0080).
        element.tabIndex = 0;
        this.options = options as unknown as GridOptions<PivotDisplayRow>;
        return handle(next => { this.options = next as unknown as GridOptions<PivotDisplayRow>; }, () => { this.destroyed = true; });
    };

    /** The details grids standing now. */
    get detailsShown(): readonly DetailsGrid[] {
        return this.details.filter(grid => !grid.destroyed);
    }

    get current(): GridOptions<PivotDisplayRow> {
        if (this.options === undefined) throw new Error('No grid was mounted.');
        return this.options;
    }

    /** The headers of the columns handed to the grid. */
    get headers(): readonly string[] {
        return this.current.columns.map(column => column.header ?? column.name);
    }

    /** Mounts a template cell of the rows the grid would show first, as the grid mounts it. */
    async paint(row: number, column: number, focusRequest = 0): Promise<PaintedCell> {
        const { source, columns } = this.current;
        const template = columns[column]?.template;
        if (source === undefined || template === undefined) throw new Error('The cell has no template.');
        const [shown] = await source.rows({ start: row, count: 1 }, new AbortController().signal);
        if (shown === undefined) throw new Error(`The report has no row ${row}.`);
        const slot = document.createElement('div');
        document.body.append(slot);
        const into = (request: number): PaintedCell => {
            const handed: HTMLElement[] = [];
            const dispose = template(slot, { row: shown, column: columns[column]?.name ?? '', focusRequest: request, takeKeyboard: control => { handed.push(control); } }); // oxlint-disable-line ex-grid/no-stand-in-default -- the column was read above
            return {
                slot, handed, dispose, repaint: () => into(0),
                button: () => {
                    const button = slot.querySelector('button');
                    if (button === null) throw new Error('The cell paints no button.');
                    return button;
                },
            };
        };
        return into(focusRequest);
    }

    /** The painted text of the rows the grid would show first, label and value columns alike. */
    async texts(count = 20): Promise<readonly (readonly string[])[]> {
        const { source, columns } = this.current;
        if (source === undefined) throw new Error('The grid was handed no source.');
        const rows = await source.rows({ start: 0, count }, new AbortController().signal);
        return rows.map(row => columns.map(column => {
            const value = column.value(row);
            if (value === undefined || value === null) return '';
            return column.format === undefined ? String(value) : column.format(value); // oxlint-disable-line typescript/no-base-to-string -- a label is text
        }));
    }
}

/** The report's work yields to microtasks only, so what a test reads never depends on how fast it ran. */
const inTurn: PivotSlicing = Object.freeze({ yield: () => Promise.resolve() });

let mounted: PivotView[] = [];

afterEach(() => {
    for (const view of mounted) view.destroy();
    mounted = [];
    document.body.replaceChildren();
});

function mount(options: PivotViewOptions, grid = new GridStandIn()): { readonly view: PivotView; readonly root: HTMLElement; readonly grid: GridStandIn } {
    const root = document.createElement('div');
    document.body.append(root);
    const view = new PivotView(root, { slicing: inTurn, ...options }, grid.mount);
    mounted.push(view);
    return { view, root, grid };
}

/** Lets every answer already given land: the report's work yields only microtasks here, and the sources
 * answer with resolved promises, so a macrotask comes after all of it, however long it takes. */
async function settled(): Promise<void> {
    await new Promise(resolve => { setTimeout(resolve, 0); });
}

const byRegion: PivotLayout = createPivotLayout({ rows: ['Region'], values: ['Amount'] });

function field(root: HTMLElement, name: string): HTMLElement {
    const item = root.querySelector<HTMLElement>(`.ex-pivot-field[data-ex-field="${name}"]`);
    if (item === null) throw new Error(`The Field List shows no field '${name}'.`);
    return item;
}

function area(root: HTMLElement, name: string): HTMLElement {
    const box = root.querySelector<HTMLElement>(`.ex-pivot-area[data-ex-area="${name}"]`);
    if (box === null) throw new Error(`The Field List shows no Area '${name}'.`);
    return box;
}

function drag(target: Element, type: string): void {
    // The emulation's DragEvent carries no DataTransfer of its own, as a browser's does.
    const event = new DragEvent(type, { bubbles: true, cancelable: true });
    Object.defineProperty(event, 'dataTransfer', { value: new DataTransfer() });
    target.dispatchEvent(event);
}

function tick(root: HTMLElement, name: string): void {
    const box = field(root, name).querySelector('input');
    if (box === null) throw new Error('The field has no tick box.');
    box.checked = !box.checked;
    box.dispatchEvent(new Event('change', { bubbles: true }));
}

describe('ExPivot\'s report', () => {
    it('ADR-0059: a layout handed in is drawn as one grid, its labels pinned and its rows the report\'s', async () => {
        const { grid } = mount({ source: local(), layout: byRegion });
        await settled();
        expect(grid.mounts).toBe(1);
        expect(grid.headers).toEqual(['Row Labels', 'Sum of Amount']);
        expect(grid.current.pinnedColumnCount).toBe(1);
        expect(grid.current.hideColumnMenu).toBe(true);
        expect(await grid.texts()).toEqual([['EU', '30'], ['US', '12'], ['Grand Total', '42']]);
    });

    it('ADR-0059: the column Items stand as Header Groups over the value columns, never over the labels', async () => {
        const { grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region'], columns: ['Desk'], values: ['Amount', { field: 'Amount', aggregation: 'count' }] }) });
        await settled();
        const groups = grid.current.headerGroups;
        if (groups === undefined) throw new Error('The grid was handed Header Groups.');
        expect(groups.map(group => group.label)).toEqual(['Credit', 'FX', 'Rates', 'Grand Total']);
        const labels = new Set(grid.current.columns.slice(0, grid.current.pinnedColumnCount).map(column => column.name));
        for (const group of groups) for (const column of group.columns) expect(labels.has(column)).toBe(false);
    });

    it('ADR-0005: a value cell copies its own number, not the text it shows', async () => {
        const { grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region'], values: [{ field: 'Amount', numberFormat: '#,##0.00' }] }) });
        await settled();
        const { source, columns } = grid.current;
        if (source === undefined) throw new Error('The grid was handed no source.');
        const [first] = await source.rows({ start: 0, count: 1 }, new AbortController().signal);
        const amount = columns[1];
        if (first === undefined || amount?.format === undefined) throw new Error('The report has a first row and a value column.');
        const value = amount.value(first);
        expect(amount.format(value)).toBe('30.00');
        expect(String(value)).toBe('30');
    });

    it('ADR-0027: a style the page set on the root stays through a redraw, until the pivot\'s own geometry changes', async () => {
        const options: PivotViewOptions = { source: local(), layout: byRegion };
        const { root, view } = mount(options);
        await settled();
        root.style.height = '260px';
        tick(root, 'Desk');
        await settled();
        expect(root.style.height).toBe('260px');
        expect(root.style.getPropertyValue('--ex-pivot-field-list-width')).toBe('300px');
        view.update({ ...options, fieldListWidth: 200 });
        expect(root.style.getPropertyValue('--ex-pivot-field-list-width')).toBe('200px');
        expect(root.style.height).toBe('');
    });

    it('ADR-0059: with nothing placed, the report says how to start and no grid stands', async () => {
        const { root, grid } = mount({ source: local() });
        await settled();
        expect(grid.mounts).toBe(0);
        const empty = root.querySelector('.ex-pivot-empty');
        expect(empty?.textContent).toBe('To build a report, choose fields from the PivotTable Fields list.');
    });

    it('ADR-0063: a double click on an outer Item\'s label collapses it, and the report is asked again', async () => {
        const changed: PivotLayout[] = [];
        const { grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }), onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        expect((await grid.texts()).map(row => row[0])).toEqual(['EU', 'Credit', 'Rates', 'US', 'FX', 'Rates', 'Grand Total']);
        await grid.current.source?.needRange({ start: 0, count: 10 });
        grid.current.onCellDoubleClick?.({ row: 0, column: 0 });
        await settled();
        expect((await grid.texts()).map(row => row[0])).toEqual(['EU', 'US', 'FX', 'Rates', 'Grand Total']);
        expect(changed).toHaveLength(1);
    });

    it('ADR-0059: a label cell paints its indent, its Item\'s button and its text, as the Razor component\'s does', async () => {
        const { root, grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }) });
        await settled();
        const outer = await grid.paint(0, 0);
        const button = outer.slot.querySelector('button.ex-pivot-toggle.ex-interactive');
        expect(button?.getAttribute('aria-label')).toBe('Collapse EU');
        expect(button?.getAttribute('aria-expanded')).toBe('true');
        expect(button?.getAttribute('tabindex')).toBe('-1');
        expect(button?.textContent).toBe('\u2212');
        expect(outer.slot.querySelector('.ex-pivot-label-text')?.textContent).toBe('EU');
        const inner = await grid.paint(1, 0);
        // A level's indent is the em of the grid's font, which the root's token sizes the button by too.
        expect(inner.slot.querySelector<HTMLElement>('.ex-pivot-label')?.style.paddingLeft).toBe('14px');
        expect(inner.slot.querySelector('button')).toBeNull();
        expect(root.style.getPropertyValue('--ex-pivot-indent')).toBe('14px');
        outer.dispose();
        expect(outer.slot.childElementCount).toBe(0);
    });

    it('ADR-0063: the button beside an outer Item collapses it, and expands it once collapsed', async () => {
        const changed: PivotLayout[] = [];
        const { grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }), onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        (await grid.paint(0, 0)).button().click();
        await settled();
        expect((await grid.texts()).map(row => row[0])).toEqual(['EU', 'US', 'FX', 'Rates', 'Grand Total']);
        const collapsed = await grid.paint(0, 0);
        expect(collapsed.button().getAttribute('aria-label')).toBe('Expand EU');
        expect(collapsed.button().textContent).toBe('+');
        collapsed.button().click();
        await settled();
        expect((await grid.texts()).map(row => row[0])).toEqual(['EU', 'Credit', 'Rates', 'US', 'FX', 'Rates', 'Grand Total']);
        expect(changed).toHaveLength(2);
    });

    it('ADR-0050: the Item expanded or collapsed keeps the Focus, placed once the grid holds the report it brings', async () => {
        const { grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }) });
        await settled();
        (await grid.paint(3, 0)).button().click();
        await settled();
        expect(grid.placed).toEqual([]);
        // The grid asks for its rows once it holds the new source; the Focus is placed under the order they came in.
        const source = defined(grid.current.source, 'the report\'s rows');
        await source.needRange({ start: 0, count: 10 });
        expect((await grid.texts()).map(row => row[0])).toEqual(['EU', 'Credit', 'Rates', 'US', 'Grand Total']);
        const at = { row: 3, column: 0 };
        expect(grid.placed).toEqual([{ range: { topRow: 3, leftColumn: 0, rowCount: 1, columnCount: 1 }, focus: at, version: source.snapshot().rowSequenceVersion }]);
    });

    it('ADR-0050: an Item under several parents keeps the Focus on the row it was toggled on', async () => {
        interface Booked { readonly region: string; readonly desk: string; readonly book: string; readonly amount: number }
        const booked: readonly Booked[] = [
            { region: 'EU', desk: 'Rates', book: 'B1', amount: 1 },
            { region: 'EU', desk: 'Rates', book: 'B2', amount: 2 },
            { region: 'US', desk: 'Rates', book: 'B3', amount: 3 },
            { region: 'US', desk: 'FX', book: 'B4', amount: 4 },
        ];
        const source = new RecordPivotSource(booked, [
            pivotRecordField<Booked>({ name: 'Region', type: 'text', value: trade => trade.region }),
            pivotRecordField<Booked>({ name: 'Desk', type: 'text', value: trade => trade.desk }),
            pivotRecordField<Booked>({ name: 'Book', type: 'text', value: trade => trade.book }),
            pivotRecordField<Booked>({ name: 'Amount', type: 'number', value: trade => trade.amount }),
        ]);
        const { grid } = mount({ source, layout: createPivotLayout({ rows: ['Region', 'Desk', 'Book'], values: ['Amount'] }) });
        await settled();
        expect((await grid.texts()).map(row => row[0])).toEqual(['EU', 'Rates', 'B1', 'B2', 'US', 'FX', 'B4', 'Rates', 'B3', 'Grand Total']);
        // US > Rates: an Item is collapsed wherever it stands, and the Focus stays on the one pressed.
        (await grid.paint(7, 0)).button().click();
        await settled();
        await grid.current.source?.needRange({ start: 0, count: 10 });
        expect((await grid.texts()).map(row => row[0])).toEqual(['EU', 'Rates', 'US', 'FX', 'B4', 'Rates', 'Grand Total']);
        expect(grid.placed.map(placed => placed.focus)).toEqual([{ row: 5, column: 0 }]);
    });

    const gestures: readonly (readonly [string, () => Event])[] = [
        ['a press', () => new PointerEvent('pointerdown', { bubbles: true })],
        ['a wheel', () => new WheelEvent('wheel', { bubbles: true })],
        ['a key', () => new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true })],
    ];
    it.each(gestures)('spine rule 6: %s in the grid after the toggle, while its question is out, is newer, and the toggle places nothing', async (_, gesture) => {
        const { grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }) });
        await settled();
        (await grid.paint(3, 0)).button().click();
        defined(grid.element, 'the report\'s grid').dispatchEvent(gesture());
        await settled();
        await grid.current.source?.needRange({ start: 0, count: 10 });
        expect(grid.placed).toEqual([]);
    });

    it('spine rule 6: a key pressed once the toggled report is shown, before its rows come, is newer too', async () => {
        const { grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }) });
        await settled();
        (await grid.paint(3, 0)).button().click();
        await settled();
        defined(grid.element, 'the report\'s grid').dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
        await grid.current.source?.needRange({ start: 0, count: 10 });
        expect(grid.placed).toEqual([]);
    });

    it('ADR-0050: a modifier pressed alone before the toggled report\'s rows come is no gesture, and the Item keeps the Focus', async () => {
        const { grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }) });
        await settled();
        (await grid.paint(3, 0)).button().click();
        await settled();
        const element = defined(grid.element, 'the report\'s grid');
        element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Control', bubbles: true }));
        element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Process', bubbles: true }));
        await grid.current.source?.needRange({ start: 0, count: 10 });
        expect(grid.placed.map(placed => placed.focus)).toEqual([{ row: 3, column: 0 }]);
    });

    it('spine rule 6: an Item toggled and then moved past keeps no Focus: only the newest question\'s report is shown', async () => {
        const { root, grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }) });
        await settled();
        (await grid.paint(0, 0)).button().click();
        tick(root, 'Amount');
        await settled();
        await grid.current.source?.needRange({ start: 0, count: 10 });
        expect(grid.placed).toEqual([]);
    });

    it('spine rule 6: a press does what its button said, and one taken on a button a press already acted on is refused', async () => {
        const layout = createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] });
        const { grid } = mount({ source: local(), layout });
        await settled();
        const before = await grid.texts();
        const button = (await grid.paint(0, 0)).button();
        button.click();
        button.click();
        await settled();
        expect((await grid.texts()).map(row => row[0])).toEqual(['EU', 'US', 'FX', 'Rates', 'Grand Total']);
        // The button painted for the collapsed Item says Expand, and does it.
        (await grid.paint(0, 0)).button().click();
        await settled();
        expect(await grid.texts()).toEqual(before);
    });

    it('ADR-0037: Space on a label cell hands the keyboard to its button, and no other mount takes it', async () => {
        const { grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }) });
        await settled();
        const asked = await grid.paint(0, 0, 1);
        expect(asked.handed).toEqual([asked.button()]);
        asked.dispose();
        // The same cell mounted again, its row's new version, takes nothing: the request was acted on once.
        expect(asked.repaint().handed).toEqual([]);
    });

    it('ADR-0059: a label column is as wide as its widest label, and a report that keeps a column hands the grid the same one', async () => {
        const wide = 'A region whose name is far wider than the header';
        const source = new RecordPivotSource([...trades, { region: wide, desk: 'FX', amount: 1 }], fields);
        const options: PivotViewOptions = { source, layout: byRegion };
        const { view, grid } = mount(options);
        await settled();
        const [label] = grid.current.columns;
        const width = label?.width;
        if (width === undefined || typeof width === 'number' || 'auto' in width) throw new Error('A label column is handed a width of its own.');
        // Every character is charged at least the narrow class, and both sides' padding.
        expect(width.px).toBeGreaterThan(wide.length * 4);
        expect(width.px).toBeLessThanOrEqual(480);
        view.update({ ...options, layout: createPivotLayout({ rows: ['Region'], values: ['Amount'] }) });
        await settled();
        expect(grid.current.columns[0]).toBe(label);
    });
});

const named = (items: readonly { readonly field: string; readonly item: { readonly value?: string } }[]): string =>
    items.map(({ field: name, item }) => {
        if (item.value === undefined) throw new Error('These trades have no Blank Item.');
        return `${name}=${item.value}`;
    }).join(' ');

async function selected(layout: PivotLayout, ranges: GridSelection['ranges']): Promise<readonly PivotSelectionChangedEvent[]> {
    const said: PivotSelectionChangedEvent[] = [];
    const { grid } = mount({ source: local(), layout, onSelectionChanged: event => said.push(event) });
    await settled();
    await grid.current.source?.needRange({ start: 0, count: 10 });
    grid.current.onSelectionChanged?.({ ranges } as GridSelection); // oxlint-disable-line typescript/consistent-type-assertions -- the pivot reads only the ranges of the selection the core makes
    return said;
}

describe('ExPivot\'s selection (ADR-0340)', () => {
    it('ADR-0340: each selected cell is said with its row\'s Items and, for a value cell, its column\'s, once and in the order selected', async () => {
        const layout = createPivotLayout({ rows: ['Region'], columns: ['Desk'], values: ['Amount'] });
        const [event] = await selected(layout, [
            { topRow: 1, leftColumn: 0, rowCount: 1, columnCount: 3 },
            { topRow: 0, leftColumn: 2, rowCount: 2, columnCount: 1 },
        ]);
        const items = event?.items;
        if (items === undefined) throw new Error('The selection is held drawn, so its Items are said.');
        expect(items.map(cell => [named(cell.row), named(cell.column)])).toEqual([
            ['Region=US', ''],
            ['Region=US', 'Desk=Credit'],
            ['Region=US', 'Desk=FX'],
            ['Region=EU', 'Desk=FX'],
        ]);
    });

    it('ADR-0340: a label cell of its own field stands for the path down to that field', async () => {
        const [event] = await selected(createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'], form: 'tabular' }), [
            { topRow: 0, leftColumn: 0, rowCount: 1, columnCount: 2 },
        ]);
        expect(event?.items?.map(cell => named(cell.row))).toEqual(['Region=EU', 'Region=EU Desk=Credit']);
    });

    it('ADR-0340: a selection dropped is said with no Items, so what it narrowed is let go', async () => {
        const [event] = await selected(byRegion, []);
        expect(event?.items).toEqual([]);
    });

    it('spine rule 1: a selection reaching a row the grid does not hold is said with no Items rather than some', async () => {
        const [event] = await selected(byRegion, [{ topRow: 0, leftColumn: 0, rowCount: 50, columnCount: 1 }]);
        expect(event?.selection.ranges).toHaveLength(1);
        expect(event?.items).toBeUndefined();
    });
});

describe('ExPivot\'s Field List', () => {
    it('ADR-0061: a tick places the field, and the layout is raised once the report shows it', async () => {
        const changed: PivotLayout[] = [];
        const { root, grid } = mount({ source: local(), onLayoutChanged: event => changed.push(event.layout) });
        tick(root, 'Region');
        expect(changed).toHaveLength(0);
        tick(root, 'Amount');
        await settled();
        expect(changed).toHaveLength(1);
        expect(changed[0]?.rows.map(p => p.field)).toEqual(['Region']);
        expect(changed[0]?.values.map(v => v.field)).toEqual(['Amount']);
        expect(grid.headers).toEqual(['Row Labels', 'Sum of Amount']);
        expect([...area(root, 'values').querySelectorAll('.ex-pivot-entry-caption')].map(e => e.textContent)).toEqual(['Sum of Amount']);
    });

    it('spine rule 6: moves made while a question is out are shown only as the newest question\'s answer, whatever order the answers come in', async () => {
        const source = new HeldSource();
        const changed: PivotLayout[] = [];
        const { root, grid } = mount({ source, layout: byRegion, onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        source.held.shift()?.();
        await settled();
        tick(root, 'Desk');
        // Taken against the layout the question for Region and Desk asks for, before it is answered.
        tick(root, 'Region');
        // Every answer let through, the newest first, until none is held.
        for (let round = 0; round < 5; round++) {
            for (const release of source.held.splice(0).toReversed()) release();
            await settled();
        }
        expect(changed).toHaveLength(1);
        expect(changed[0]?.rows.map(p => p.field)).toEqual(['Desk']);
        expect((await grid.texts()).map(row => row[0])).toEqual(['Credit', 'FX', 'Rates', 'Grand Total']);
    });

    it('ADR-0061: a gesture taken against a layout that was replaced is refused, and changes nothing', async () => {
        const changed: PivotLayout[] = [];
        const { root, view } = mount({ source: local(), layout: byRegion, onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        drag(field(root, 'Desk'), 'dragstart');
        view.update({ source: local(), layout: createPivotLayout({ columns: ['Region'], values: ['Amount'] }) });
        drag(area(root, 'rows'), 'drop');
        await settled();
        expect(changed).toHaveLength(0);
        expect(view.shownLayout.rows).toHaveLength(0);
    });

    it('ADR-0061: a placed field dropped back on the list leaves the report', async () => {
        const changed: PivotLayout[] = [];
        const { root } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }), onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        const entry = area(root, 'rows').querySelectorAll('.ex-pivot-entry')[1];
        if (entry === undefined) throw new Error('Desk stands in Rows.');
        drag(entry, 'dragstart');
        expect(root.querySelector('.ex-pivot-fields')?.classList.contains('ex-pivot-drop-remove')).toBe(true);
        drag(root.querySelector('.ex-pivot-fields') ?? root, 'drop');
        await settled();
        expect(changed[0]?.rows.map(p => p.field)).toEqual(['Region']);
    });

    it('ADR-0061: what a drag starts on stays in the page while the pane marks the drag, or the browser ends the drag at once', async () => {
        const { root } = mount({ source: server(), layout: byRegion });
        const desk = field(root, 'Desk');
        drag(desk, 'dragstart');
        // The counts land while the field is dragged, and the pane is drawn again with them.
        await settled();
        expect(root.querySelector('.ex-pivot-field[data-ex-field="Desk"] .ex-pivot-field-count')?.textContent).toBe('3');
        expect(desk.isConnected).toBe(true);
        drag(desk, 'dragend');
        const region = area(root, 'rows').querySelector('.ex-pivot-entry');
        if (region === null) throw new Error('Rows shows no Region entry.');
        // A placed field's drag shows the report's remove mark, which draws the pivot again.
        drag(region, 'dragstart');
        expect(root.querySelector('.ex-pivot-report-drop-remove')).not.toBeNull();
        expect(region.isConnected).toBe(true);
        drag(region, 'dragend');
    });

    it('ADR-0061: the toggle and the close button hide the Field List and say so', async () => {
        const shown: boolean[] = [];
        const { root } = mount({ source: local(), onShowFieldListChanged: event => shown.push(event.shown) });
        const list = root.querySelector<HTMLElement>('.ex-pivot-field-list');
        root.querySelector<HTMLElement>('.ex-pivot-pane-close')?.click();
        expect(list?.hidden).toBe(true);
        root.querySelector<HTMLElement>('.ex-pivot-field-list-toggle')?.click();
        expect(list?.hidden).toBe(false);
        expect(shown).toEqual([false, true]);
    });

    it('ADR-0370 section 10: the words are English unless the Consumer chooses others, and the layout\'s captions follow them', async () => {
        const { root } = mount({ source: local(), layout: byRegion, label: japanesePivotWords });
        await settled();
        expect(root.querySelector('.ex-pivot-pane-title')?.textContent).toBe('ピボットテーブルのフィールド');
        expect(area(root, 'values').querySelector('.ex-pivot-entry-caption')?.textContent).toBe('合計 / Amount');
    });
});

describe('ExPivot over a server\'s Leaf Aggregates (ADR-0370 section 3)', () => {
    it('ADR-0066: Refresh is offered by a source that can be asked again, and asks it again under the layout on screen', async () => {
        let behind = local();
        const source = new FetchingPivotSource(behind.fields, new PivotSourceFeatures(['sum', 'count'], true, false), {
            aggregate: (query, signal) => behind.aggregate(query, signal),
            details: (query, signal) => behind.details(query, signal),
            items: (query, signal) => behind.items(query, signal),
        });
        const { root, grid } = mount({ source, layout: byRegion });
        await settled();
        expect(await grid.texts()).toEqual([['EU', '30'], ['US', '12'], ['Grand Total', '42']]);
        behind = new RecordPivotSource([...trades, { region: 'US', desk: 'FX', amount: 100 }], fields);
        const refresh = root.querySelector<HTMLButtonElement>('.ex-pivot-toolbar-end .ex-pivot-refresh-button');
        expect(refresh?.textContent).toBe('Refresh');
        refresh?.click();
        await settled();
        expect(await grid.texts()).toEqual([['EU', '30'], ['US', '112'], ['Grand Total', '142']]);
        const { root: other } = mount({ source: local(), layout: byRegion });
        expect(other.querySelector('.ex-pivot-refresh-button')).toBeNull();
    });

    it('ADR-0066, spine rule 6: a move made while a Refresh is out is answered from the source asked again, never from the data before it', async () => {
        let behind = local();
        const source = new FetchingPivotSource(behind.fields, new PivotSourceFeatures(['sum', 'count'], true, false), {
            aggregate: (query, signal) => behind.aggregate(query, signal),
            details: (query, signal) => behind.details(query, signal),
            items: (query, signal) => behind.items(query, signal),
        });
        const { root, grid } = mount({ source, layout: byRegion });
        await settled();
        behind = new RecordPivotSource([...trades, { region: 'US', desk: 'FX', amount: 100 }], fields);
        root.querySelector<HTMLButtonElement>('.ex-pivot-refresh-button')?.click();
        entryButton(root, 'rows', 0).click();
        command(root, 'Sort Z to A').click();
        await settled();
        expect(await grid.texts()).toEqual([['US', '112'], ['EU', '30'], ['Grand Total', '142']]);
    });

    it('ADR-0370 section 3: a source that counts its Items has each count written beside its field, in the pivot\'s locale', async () => {
        const { root } = mount({ source: server(), locale: 'de-DE' });
        await settled();
        expect(field(root, 'Desk').querySelector('.ex-pivot-field-count')?.textContent).toBe('3');
        expect(field(root, 'Desk').querySelector('.ex-pivot-field-count')?.getAttribute('aria-label')).toBe('3 items');
        const { root: uncounted } = mount({ source: local() });
        await settled();
        expect(uncounted.querySelector('.ex-pivot-field-count')).toBeNull();
    });

    it('ADR-0370 section 3: while a field is dragged, a target whose layout the counts bound past the cap is marked, and one under it is not', async () => {
        const { root } = mount({ source: server(), layout: byRegion, maxLeaves: 3 });
        await settled();
        drag(field(root, 'Desk'), 'dragstart');
        drag(area(root, 'rows'), 'dragenter');
        expect(area(root, 'rows').classList.contains('ex-pivot-drop-target')).toBe(true);
        expect(area(root, 'rows').classList.contains('ex-pivot-drop-over-cap')).toBe(true);
        expect(area(root, 'rows').querySelector('.ex-pivot-area-cap')?.textContent).toBe('More than 3 combinations, the most server data allows');
        drag(area(root, 'filters'), 'dragenter');
        expect(area(root, 'rows').classList.contains('ex-pivot-drop-over-cap')).toBe(false);
        expect(area(root, 'filters').classList.contains('ex-pivot-drop-over-cap')).toBe(false);
        drag(field(root, 'Desk'), 'dragend');
        expect(area(root, 'filters').classList.contains('ex-pivot-drop-target')).toBe(false);
    });

    it('ADR-0370 section 3: a drop past the cap is refused naming the field, its count and the cap, and the pane goes back to the report\'s layout', async () => {
        const changed: PivotLayout[] = [];
        const refused: string[] = [];
        const { root, view } = mount({
            source: server(), layout: byRegion, maxLeaves: 3,
            onLayoutChanged: event => changed.push(event.layout), onRefused: event => refused.push(event.message),
        });
        await settled();
        drag(field(root, 'Desk'), 'dragstart');
        drag(area(root, 'rows'), 'dragenter');
        drag(area(root, 'rows'), 'drop');
        expect([...area(root, 'rows').querySelectorAll('.ex-pivot-entry-caption')].map(e => e.textContent)).toEqual(['Region', 'Desk']);
        await settled();
        const sentence = 'Desk (3 items) in Rows makes more than 3 combinations, the most server data allows. Put Desk in Filters, or filter Desk first.';
        expect(refused).toEqual([sentence]);
        expect(root.querySelector('.ex-pivot-refusal-notice')?.textContent).toBe(sentence);
        expect(changed).toHaveLength(0);
        expect(view.shownLayout).toBe(byRegion);
        expect([...area(root, 'rows').querySelectorAll('.ex-pivot-entry-caption')].map(e => e.textContent)).toEqual(['Region']);
    });

    it('ADR-0066: data the pivot computes itself is refused past the cap by the cells the layout needs', async () => {
        const refused: string[] = [];
        const { root } = mount({ source: local(), layout: byRegion, maxLeaves: 3, onRefused: event => refused.push(event.message) });
        await settled();
        tick(root, 'Desk');
        await settled();
        expect(refused).toEqual(['This layout needs more than 3 cells.']);
    });

    it('ADR-0370 section 3: every move taken against a question that is refused is refused with it, and each is named', async () => {
        const refused: { readonly message: string; readonly fields: readonly string[] }[] = [];
        const { root } = mount({ source: server(), layout: byRegion, maxLeaves: 3, onRefused: event => refused.push(event) });
        await settled();
        drag(field(root, 'Desk'), 'dragstart');
        drag(area(root, 'rows'), 'dragenter');
        drag(area(root, 'rows'), 'drop');
        // Before that question is answered, Region is moved to Columns: taken against it, and refused with it.
        const region = area(root, 'rows').querySelector('.ex-pivot-entry');
        if (region === null) throw new Error('Rows shows no Region entry.');
        drag(region, 'dragstart');
        drag(area(root, 'columns'), 'dragenter');
        drag(area(root, 'columns'), 'drop');
        await settled();
        expect(refused).toEqual([{
            message: 'Desk (3 items) in Rows makes more than 3 combinations, the most server data allows. Put Desk in Filters, or filter Desk first. '
                + 'Region (2 items) in Columns makes more than 3 combinations, the most server data allows. Put Region in Filters, or filter Region first.',
            fields: ['Desk', 'Region'],
        }]);
        expect([...area(root, 'rows').querySelectorAll('.ex-pivot-entry-caption')].map(e => e.textContent)).toEqual(['Region']);
    });

    it('ADR-0370 section 3: a field moved and taken out again while the question was out is not named in its refusal', async () => {
        const refused: { readonly message: string; readonly fields: readonly string[] }[] = [];
        const { root } = mount({ source: server(), layout: createPivotLayout({ values: ['Amount'] }), maxLeaves: 1, onRefused: event => refused.push(event) });
        await settled();
        drag(field(root, 'Desk'), 'dragstart');
        drag(area(root, 'rows'), 'dragenter');
        drag(area(root, 'rows'), 'drop');
        tick(root, 'Desk');
        drag(field(root, 'Region'), 'dragstart');
        drag(area(root, 'columns'), 'dragenter');
        drag(area(root, 'columns'), 'drop');
        await settled();
        expect(refused).toEqual([{
            message: 'Region (2 items) in Columns makes more than 1 combinations, the most server data allows. Put Region in Filters, or filter Region first.',
            fields: ['Region'],
        }]);
    });

    it('ADR-0370 section 3: a number ticked into Values is named when the question it went with is refused', async () => {
        const refused: (readonly string[])[] = [];
        const { root } = mount({ source: server(), maxLeaves: 1, onRefused: event => refused.push(event.fields) });
        tick(root, 'Amount');
        drag(field(root, 'Region'), 'dragstart');
        drag(area(root, 'rows'), 'dragenter');
        drag(area(root, 'rows'), 'drop');
        await settled();
        expect(refused).toEqual([['Amount', 'Region']]);
    });

    it('ADR-0061: a drag that moves on from an Area to the report leaves the Area unmarked', async () => {
        const { root } = mount({ source: server(), layout: byRegion });
        await settled();
        drag(field(root, 'Desk'), 'dragstart');
        drag(area(root, 'columns'), 'dragenter');
        expect(area(root, 'columns').classList.contains('ex-pivot-drop-target')).toBe(true);
        const sheet = root.querySelector('.ex-pivot-sheet');
        if (sheet === null) throw new Error('The pivot draws no report sheet.');
        drag(sheet, 'dragenter');
        expect(area(root, 'columns').classList.contains('ex-pivot-drop-target')).toBe(false);
        drag(field(root, 'Desk'), 'dragend');
    });

    it('ADR-0061: a drag that moves on from an Area to the list leaves the Area unmarked', async () => {
        const { root } = mount({ source: server(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }) });
        await settled();
        const desk = area(root, 'rows').querySelectorAll('.ex-pivot-entry')[1];
        if (desk === undefined) throw new Error('Desk stands in Rows.');
        drag(desk, 'dragstart');
        drag(area(root, 'columns'), 'dragenter');
        expect(area(root, 'columns').classList.contains('ex-pivot-drop-target')).toBe(true);
        drag(root.querySelector('.ex-pivot-fields') ?? root, 'dragenter');
        expect(area(root, 'columns').classList.contains('ex-pivot-drop-target')).toBe(false);
        expect(area(root, 'columns').querySelector('.ex-pivot-area-cap')?.textContent).toBe('');
        drag(desk, 'dragend');
    });

    it('ADR-0061: the tick box the user pressed keeps the keyboard while the pane is drawn again', async () => {
        const { root } = mount({ source: local(), layout: byRegion });
        await settled();
        const box = field(root, 'Desk').querySelector('input');
        if (box === null) throw new Error('The field has no tick box.');
        box.focus();
        tick(root, 'Desk');
        await settled();
        expect(box.checked).toBe(true);
        expect(root.ownerDocument.activeElement).toBe(box);
    });
});

function menu(root: HTMLElement): HTMLElement {
    const frame = root.querySelector<HTMLElement>('.ex-pivot-popup[role="menu"]');
    if (frame === null) throw new Error('No menu is open.');
    return frame;
}

function command(root: HTMLElement, label: string): HTMLButtonElement {
    const found = [...menu(root).querySelectorAll<HTMLButtonElement>('.ex-pivot-menu-item')].find(button => button.textContent.endsWith(label));
    if (found === undefined) throw new Error(`The menu has no command '${label}'.`);
    return found;
}

function entryButton(root: HTMLElement, areaName: string, index: number): HTMLButtonElement {
    const button = area(root, areaName).querySelectorAll<HTMLButtonElement>('.ex-pivot-entry-button')[index];
    if (button === undefined) throw new Error(`The Area '${areaName}' has no entry ${index}.`);
    return button;
}

function layoutButton(root: HTMLElement): HTMLButtonElement {
    const button = root.querySelector<HTMLButtonElement>('.ex-pivot-layout-button');
    if (button === null) throw new Error('The Pivot Toolbar has no Layout button.');
    return button;
}

describe('ExPivot\'s menus (ADR-0061)', () => {
    it('ADR-0061: Layout opens Excel\'s Design choices under their headings, the current ones marked and a choice that changes nothing disabled', async () => {
        const { root } = mount({ source: local(), layout: byRegion });
        await settled();
        const button = layoutButton(root);
        expect(button.getAttribute('aria-expanded')).toBe('false');
        button.click();
        expect(button.getAttribute('aria-expanded')).toBe('true');
        const frame = menu(root);
        expect(frame.parentElement?.className).toBe('ex-pivot-toolbar-anchor');
        expect(frame.className).toBe('ex-pivot-popup ex-pivot-popup-overlay ex-pivot-popup-end');
        expect([...frame.querySelectorAll('.ex-pivot-menu-heading')].map(heading => heading.textContent)).toEqual(['Subtotals', 'Grand Totals', 'Report Layout']);
        const compact = command(root, 'Show in Compact Form');
        expect(compact.getAttribute('role')).toBe('menuitemradio');
        expect(compact.getAttribute('aria-checked')).toBe('true');
        expect(compact.disabled).toBe(true);
        expect(command(root, 'Show in Tabular Form').getAttribute('aria-checked')).toBe('false');
        expect(root.querySelector('.ex-pivot-backdrop')).not.toBeNull();
        // The first enabled choice takes the keyboard.
        const first = [...frame.querySelectorAll<HTMLButtonElement>('.ex-pivot-menu-item')].find(item => !item.disabled);
        expect(document.activeElement).toBe(first);
    });

    it('ADR-0061: a Layout choice closes the menu, lays the report out under it and hands the keyboard back to Layout', async () => {
        const changed: PivotLayout[] = [];
        const { root, view } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }), onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        layoutButton(root).click();
        command(root, 'Show in Tabular Form').click();
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(root.querySelector('.ex-pivot-backdrop')).toBeNull();
        expect(document.activeElement).toBe(layoutButton(root));
        await settled();
        expect(changed).toHaveLength(1);
        expect(view.shownLayout.form).toBe('tabular');
        layoutButton(root).click();
        expect(command(root, 'Show in Tabular Form').getAttribute('aria-checked')).toBe('true');
    });

    it('ADR-0061: Escape and a press on the backdrop close the Layout menu and hand the keyboard back', async () => {
        const { root } = mount({ source: local(), layout: byRegion });
        await settled();
        layoutButton(root).click();
        menu(root).dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(document.activeElement).toBe(layoutButton(root));
        layoutButton(root).click();
        root.querySelector<HTMLElement>('.ex-pivot-backdrop')?.click();
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(document.activeElement).toBe(layoutButton(root));
    });

    it('ADR-0061: a placed field\'s menu stands under its entry, offers only the moves that change something, and a move hands the keyboard to the field\'s entry where it went', async () => {
        const { root, view } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }) });
        await settled();
        const region = entryButton(root, 'rows', 0);
        expect(region.getAttribute('aria-label')).toBe('Options for Region');
        region.click();
        expect(region.getAttribute('aria-expanded')).toBe('true');
        expect(menu(root).parentElement).toBe(region.parentElement);
        expect(menu(root).getAttribute('aria-label')).toBe('Options for Region');
        expect(command(root, 'Move Up').disabled).toBe(true);
        expect(command(root, 'Move to Row Labels').disabled).toBe(true);
        expect(command(root, 'Collapse Entire Field').disabled).toBe(false);
        expect(command(root, 'Expand Entire Field').disabled).toBe(true);
        expect(document.activeElement).toBe(command(root, 'Move Down'));
        command(root, 'Move to Column Labels').click();
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(document.activeElement).toBe(entryButton(root, 'columns', 0));
        expect(entryButton(root, 'columns', 0).textContent).toContain('Region');
        await settled();
        expect(view.shownLayout.columns.map(placement => placement.field)).toEqual(['Region']);
        // Moved back, it stands last in Rows, and its entry there takes the keyboard; moved up, the keyboard goes with it.
        entryButton(root, 'columns', 0).click();
        command(root, 'Move to Row Labels').click();
        expect(document.activeElement).toBe(entryButton(root, 'rows', 1));
        expect(entryButton(root, 'rows', 1).textContent).toContain('Region');
        entryButton(root, 'rows', 1).click();
        command(root, 'Move Up').click();
        expect(document.activeElement).toBe(entryButton(root, 'rows', 0));
        expect(entryButton(root, 'rows', 0).textContent).toContain('Region');
    });

    it('ADR-0061: a Value Field removed while its field stands in Rows hands the keyboard to that entry', async () => {
        const { root } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Amount'], values: ['Amount'] }) });
        await settled();
        entryButton(root, 'values', 0).click();
        command(root, 'Remove Field').click();
        expect(document.activeElement).toBe(entryButton(root, 'rows', 1));
    });

    it('ADR-0061: a field menu hidden with the Field List hands the keyboard it held to the toggle', async () => {
        const source = local();
        const { root, view } = mount({ source, layout: byRegion });
        await settled();
        entryButton(root, 'rows', 0).click();
        view.update({ source, slicing: inTurn, layout: byRegion, showFieldList: false });
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(document.activeElement).toBe(root.querySelector('.ex-pivot-field-list-toggle'));
    });

    it('ADR-0061: a field removed from its menu hands the keyboard to its tick box, never to the page', async () => {
        const { root } = mount({ source: local(), layout: byRegion });
        await settled();
        entryButton(root, 'rows', 0).click();
        command(root, 'Remove Field').click();
        expect(document.activeElement).toBe(field(root, 'Region').querySelector('input'));
    });

    it('ADR-0061: a press elsewhere in the pivot dismisses a field menu and leaves the keyboard where it went; a press on its entry closes it', async () => {
        const { root } = mount({ source: local(), layout: byRegion });
        await settled();
        entryButton(root, 'rows', 0).click();
        expect(menu(root)).not.toBeNull();
        command(root, 'Move Down').dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
        expect(root.querySelector('.ex-pivot-popup')).not.toBeNull();
        layoutButton(root).dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        entryButton(root, 'rows', 0).click();
        entryButton(root, 'rows', 0).click();
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(document.activeElement).toBe(entryButton(root, 'rows', 0));
    });

    it('ADR-0061, spine rule 6: a field menu goes when the pane leaves the layout it was opened on, and the keyboard it held goes to its field\'s entry', async () => {
        const source = local();
        const { root, view } = mount({ source, layout: byRegion });
        await settled();
        entryButton(root, 'rows', 0).click();
        view.update({ source, slicing: inTurn, layout: createPivotLayout({ rows: ['Desk'], columns: ['Region'], values: ['Amount'] }) });
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(entryButton(root, 'rows', 0).getAttribute('aria-expanded')).toBe('false');
        expect(document.activeElement).toBe(entryButton(root, 'columns', 0));
    });

    it('ADR-0061: the keyboard leaving the Layout menu closes it, so its backdrop takes no press meant elsewhere', async () => {
        const { root } = mount({ source: local(), layout: byRegion });
        await settled();
        layoutButton(root).click();
        const toggle = root.querySelector<HTMLButtonElement>('.ex-pivot-field-list-toggle');
        toggle?.focus();
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(root.querySelector('.ex-pivot-backdrop')).toBeNull();
        expect(document.activeElement).toBe(toggle);
    });

    it('ADR-0061: Escape in a menu goes no further than the menu', async () => {
        const { root } = mount({ source: local(), layout: byRegion });
        await settled();
        let heard = 0;
        const listening = new AbortController();
        document.body.addEventListener('keydown', () => { heard++; }, { signal: listening.signal });
        layoutButton(root).click();
        command(root, 'Show in Tabular Form').dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
        listening.abort();
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(heard).toBe(0);
    });

    it('ADR-0061: Σ Values moves only between Rows and Columns', async () => {
        const { root } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region'], values: ['Amount', { field: 'Amount', aggregation: 'count' }] }) });
        await settled();
        const values = root.querySelector<HTMLButtonElement>('.ex-pivot-entry-values .ex-pivot-entry-button');
        values?.click();
        const labels = [...menu(root).querySelectorAll<HTMLButtonElement>('.ex-pivot-menu-item')].map(item => [item.textContent, item.disabled]);
        expect(labels).toEqual([['Move to Row Labels', false], ['Move to Column Labels', true]]);
    });
});

function panel(root: HTMLElement): HTMLElement {
    const frame = root.querySelector<HTMLElement>('.ex-pivot-popup[role="dialog"]');
    if (frame === null) throw new Error('No panel is open.');
    return frame;
}

function choose(select: HTMLSelectElement | undefined, value: string): void {
    if (select === undefined) throw new Error('The panel has no such list.');
    select.value = value;
    select.dispatchEvent(new Event('change', { bubbles: true }));
}

function typeInto(input: HTMLInputElement | undefined, text: string): void {
    if (input === undefined) throw new Error('The panel has no such field.');
    input.value = text;
    input.dispatchEvent(new Event('input', { bubbles: true }));
}

/** The open panel's `index`th text field. */
function panelText(root: HTMLElement, index = 0): HTMLInputElement | undefined {
    return panel(root).querySelectorAll<HTMLInputElement>('input.ex-pivot-text')[index];
}

/** The open panel's `index`th list. */
function panelList(root: HTMLElement, index = 0): HTMLSelectElement | undefined {
    return panel(root).querySelectorAll<HTMLSelectElement>('.ex-pivot-select')[index];
}

function press(root: HTMLElement, className: string): void {
    const button = panel(root).querySelector<HTMLButtonElement>(`.${className}`);
    if (button === null) throw new Error(`The panel has no ${className}.`);
    button.click();
}

describe('ExPivot\'s panels (ADR-0061)', () => {
    it('ADR-0061: Field Settings… opens in its menu\'s place, takes the keyboard, and OK applies its subtotals and order', async () => {
        const { root, view } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }) });
        await settled();
        entryButton(root, 'rows', 0).click();
        command(root, 'Field Settings…').click();
        const frame = panel(root);
        expect(root.querySelector('.ex-pivot-popup[role="menu"]')).toBeNull();
        expect(frame.parentElement).toBe(entryButton(root, 'rows', 0).parentElement);
        expect(frame.getAttribute('aria-label')).toBe('Field Settings…');
        expect(frame.querySelector('.ex-pivot-panel-title')?.textContent).toBe('Region');
        const radios = frame.querySelectorAll<HTMLInputElement>('input[type="radio"]');
        expect(document.activeElement).toBe(radios[0]);
        const sorts = panelList(root);
        expect([...frame.querySelectorAll('.ex-pivot-select option')].map(option => option.textContent)).toEqual([
            'Ascending (A to Z) by label', 'Descending (Z to A) by label', 'Ascending by Sum of Amount', 'Descending by Sum of Amount',
        ]);
        radios[1]?.click();
        choose(sorts, '3');
        press(root, 'ex-pivot-ok');
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(document.activeElement).toBe(entryButton(root, 'rows', 0));
        await settled();
        const region = view.shownLayout.rows[0];
        expect(region?.subtotals).toBe(false);
        expect(region?.sort).toEqual({ direction: 'descending', byValue: 0 });
    });

    it('ADR-0061: Cancel and Escape drop the draft and hand the keyboard back to the entry', async () => {
        const changed: PivotLayout[] = [];
        const { root } = mount({ source: local(), layout: byRegion, onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        entryButton(root, 'rows', 0).click();
        command(root, 'Field Settings…').click();
        panel(root).querySelectorAll<HTMLInputElement>('input[type="radio"]')[1]?.click();
        press(root, 'ex-pivot-cancel');
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(document.activeElement).toBe(entryButton(root, 'rows', 0));
        entryButton(root, 'rows', 0).click();
        command(root, 'Field Settings…').click();
        panel(root).dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(document.activeElement).toBe(entryButton(root, 'rows', 0));
        await settled();
        expect(changed).toHaveLength(0);
    });

    it('ADR-0061: OK with nothing changed asks nothing, and the entry pressed under a panel opens its menu in the panel\'s place', async () => {
        const changed: PivotLayout[] = [];
        const { root } = mount({ source: local(), layout: byRegion, onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        entryButton(root, 'values', 0).click();
        command(root, 'Value Field Settings…').click();
        press(root, 'ex-pivot-ok');
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(document.activeElement).toBe(entryButton(root, 'values', 0));
        entryButton(root, 'values', 0).click();
        command(root, 'Value Field Settings…').click();
        entryButton(root, 'values', 0).click();
        expect(root.querySelector('.ex-pivot-popup[role="dialog"]')).toBeNull();
        expect(menu(root).getAttribute('aria-label')).toBe('Options for Sum of Amount');
        await settled();
        expect(changed).toHaveLength(0);
    });

    it('ADR-0061: in Value Field Settings… a caption the user has not written follows the Aggregation, and OK applies them', async () => {
        const { root, view } = mount({ source: local(), layout: byRegion });
        await settled();
        entryButton(root, 'values', 0).click();
        command(root, 'Value Field Settings…').click();
        const frame = panel(root);
        const caption = panelText(root);
        expect(document.activeElement).toBe(caption);
        expect(caption?.value).toBe('Sum of Amount');
        choose(panelList(root), 'average');
        expect(caption?.value).toBe('Average of Amount');
        expect(frame.querySelector('.ex-pivot-sample')?.textContent).toBe('Sample: -1234.5678');
        typeInto(panelText(root, 1), '#,##0.0');
        expect(frame.querySelector('.ex-pivot-sample')?.textContent).toBe('Sample: -1,234.6');
        choose(panelList(root, 1), 'percentOfGrandTotal');
        press(root, 'ex-pivot-ok');
        await settled();
        expect(view.shownLayout.values[0]).toEqual({ field: 'Amount', aggregation: 'average', showValuesAs: 'percentOfGrandTotal', numberFormat: '#,##0.0' });
        expect(document.activeElement).toBe(entryButton(root, 'values', 0));
    });

    it('ADR-0061: a caption another field holds is refused in the panel, which stays open with the draft', async () => {
        const changed: PivotLayout[] = [];
        const { root } = mount({ source: local(), layout: byRegion, onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        entryButton(root, 'values', 0).click();
        command(root, 'Value Field Settings…').click();
        typeInto(panelText(root), 'Region');
        press(root, 'ex-pivot-ok');
        expect(panel(root).querySelector('.ex-pivot-refusal')?.textContent).toBe('PivotTable field name already exists.');
        expect(panel(root).querySelector<HTMLInputElement>('input.ex-pivot-text')?.value).toBe('Region');
        typeInto(panelText(root), 'Total');
        expect(panel(root).querySelector('.ex-pivot-refusal')).toBeNull();
        typeInto(panelText(root, 1), 'N31');
        expect(panel(root).querySelector('.ex-pivot-sample')?.textContent).toBe('This number format cannot be used.');
        await settled();
        expect(changed).toHaveLength(0);
    });

    it('ADR-0066: an Aggregation the source does not answer is offered disabled, with the reason', async () => {
        const { root } = mount({ source: server(), layout: byRegion });
        await settled();
        entryButton(root, 'values', 0).click();
        command(root, 'Value Field Settings…').click();
        const options = [...panel(root).querySelectorAll<HTMLOptionElement>('.ex-pivot-select option')];
        expect(options.find(option => option.value === 'average')?.disabled).toBe(true);
        expect(options.find(option => option.value === 'count')?.disabled).toBe(false);
        expect([...panel(root).querySelectorAll('.ex-pivot-not-offered')].map(note => note.textContent)).toContain('The source does not answer Average.');
    });
});

/** The open Filter…'s Items, each with whether it is ticked, (Select All) first when listed. */
function filterItems(root: HTMLElement): readonly string[] {
    return [...panel(root).querySelectorAll<HTMLLabelElement>('.ex-pivot-item')].map(row => {
        const box = row.querySelector('input');
        return `${box?.checked === true ? '☑' : '☐'} ${row.textContent}`;
    });
}

function tickItem(root: HTMLElement, label: string): void {
    const row = [...panel(root).querySelectorAll<HTMLLabelElement>('.ex-pivot-item')].find(item => item.textContent === label);
    const box = row?.querySelector('input');
    if (box === undefined || box === null) throw new Error(`Filter… lists no '${label}'.`);
    box.checked = !box.checked;
    box.dispatchEvent(new Event('change', { bubbles: true }));
}

function search(root: HTMLElement): HTMLInputElement {
    const box = panel(root).querySelector<HTMLInputElement>('input.ex-pivot-search');
    if (box === null) throw new Error('Filter… has no search.');
    return box;
}

/** A source of `count` codes, which records each search of its Items. */
class ManyCodes implements PivotSource {
    readonly #behind: RecordPivotSource<{ readonly code: string; readonly amount: number }>;
    readonly searches: string[] = [];

    constructor(count: number) {
        const records = Array.from({ length: count }, (_, i) => ({ code: `C${String(i).padStart(5, '0')}`, amount: i }));
        this.#behind = new RecordPivotSource(records, [
            pivotRecordField<{ readonly code: string }>({ name: 'Code', type: 'text', value: record => record.code }),
            pivotRecordField<{ readonly amount: number }>({ name: 'Amount', type: 'number', value: record => record.amount }),
        ]);
    }

    get fields(): PivotSource['fields'] {
        return this.#behind.fields;
    }

    get features(): PivotSource['features'] {
        return this.#behind.features;
    }

    aggregate(...args: Parameters<PivotSource['aggregate']>): ReturnType<PivotSource['aggregate']> {
        return this.#behind.aggregate(...args);
    }

    details(...args: Parameters<PivotSource['details']>): ReturnType<PivotSource['details']> {
        return this.#behind.details(...args);
    }

    items(...args: Parameters<PivotSource['items']>): ReturnType<PivotSource['items']> {
        const [query] = args;
        if (query.search !== undefined) this.searches.push(query.search);
        return this.#behind.items(...args);
    }
}

describe('ExPivot\'s Filter… (ADR-0061, ADR-0066)', () => {
    it('ADR-0061: Filter… lists the field\'s Items in its order, each ticked while shown, and OK hides those unticked', async () => {
        const { root, view } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }) });
        await settled();
        entryButton(root, 'rows', 1).click();
        command(root, 'Filter…').click();
        expect(panel(root).getAttribute('aria-label')).toBe('Filter Desk');
        expect(document.activeElement).toBe(search(root));
        await settled();
        expect(filterItems(root)).toEqual(['☑ (Select All)', '☑ Credit', '☑ FX', '☑ Rates']);
        tickItem(root, 'FX');
        expect(filterItems(root)).toEqual(['☐ (Select All)', '☑ Credit', '☐ FX', '☑ Rates']);
        press(root, 'ex-pivot-ok');
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(document.activeElement).toBe(entryButton(root, 'rows', 1));
        await settled();
        expect(view.shownLayout.rows[1]?.hiddenItems).toEqual([{ kind: 'text', value: 'FX' }]);
        entryButton(root, 'rows', 1).click();
        command(root, 'Filter…').click();
        await settled();
        expect(filterItems(root)).toEqual(['☐ (Select All)', '☑ Credit', '☐ FX', '☑ Rates']);
    });

    it('ADR-0060: unticking every Item is refused in the panel, and (Select All) ticks again only the Items the search lists', async () => {
        const changed: PivotLayout[] = [];
        const { root } = mount({ source: local(), layout: createPivotLayout({ rows: ['Desk'], values: ['Amount'] }), onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        entryButton(root, 'rows', 0).click();
        command(root, 'Filter…').click();
        await settled();
        tickItem(root, '(Select All)');
        expect(filterItems(root)).toEqual(['☐ (Select All)', '☐ Credit', '☐ FX', '☐ Rates']);
        expect(panel(root).querySelector('.ex-pivot-refusal')?.textContent).toBe('Select at least one item.');
        expect(panel(root).querySelector<HTMLButtonElement>('.ex-pivot-ok')?.disabled).toBe(true);
        typeInto(search(root), 'r');
        expect(filterItems(root)).toEqual(['☐ (Select All)', '☐ Credit', '☐ Rates']);
        tickItem(root, '(Select All)');
        expect(panel(root).querySelector('.ex-pivot-refusal')).toBeNull();
        typeInto(search(root), '');
        expect(filterItems(root)).toEqual(['☐ (Select All)', '☑ Credit', '☐ FX', '☑ Rates']);
        typeInto(search(root), 'zz');
        expect(panel(root).querySelector('.ex-pivot-note')?.textContent).toBe('No items match.');
        press(root, 'ex-pivot-cancel');
        await settled();
        expect(changed).toHaveLength(0);
    });

    it('ADR-0066: a field in Filters alone is listed under a report of its own, and the report still says how to start', async () => {
        const { root, view, grid } = mount({ source: local(), layout: createPivotLayout({ filters: ['Region'] }) });
        await settled();
        expect(grid.mounts).toBe(0);
        entryButton(root, 'filters', 0).click();
        command(root, 'Filter…').click();
        await settled();
        expect(filterItems(root)).toEqual(['☑ (Select All)', '☑ EU', '☑ US']);
        tickItem(root, 'US');
        press(root, 'ex-pivot-ok');
        await settled();
        expect(view.shownLayout.filters[0]?.hiddenItems).toEqual([{ kind: 'text', value: 'US' }]);
        expect(root.querySelector('.ex-pivot-empty')?.textContent).toBe('To build a report, choose fields from the PivotTable Fields list.');
    });

    it('ADR-0066: Items the source cannot list are said why, never listed empty, and asked again when Filter… opens again', async () => {
        const behind = local();
        let refusals = 1;
        const refusing: PivotSource = {
            fields: behind.fields,
            features: behind.features,
            aggregate: (query, signal) => behind.aggregate(query, signal),
            details: (query, signal) => behind.details(query, signal),
            items: (query, signal) => refusals-- > 0
                ? Promise.resolve(PivotItemPage.refused({ kind: 'unknownField', message: 'Desk is not listed here.' }))
                : behind.items(query, signal),
        };
        const { root } = mount({ source: refusing, layout: createPivotLayout({ rows: ['Desk'], values: ['Amount'] }) });
        await settled();
        entryButton(root, 'rows', 0).click();
        command(root, 'Filter…').click();
        await settled();
        expect(panel(root).querySelector('.ex-pivot-unavailable')?.textContent).toBe('The source refused to answer: Desk is not listed here.');
        expect(filterItems(root)).toEqual([]);
        expect(panel(root).querySelector<HTMLButtonElement>('.ex-pivot-ok')?.disabled).toBe(true);
        press(root, 'ex-pivot-cancel');
        entryButton(root, 'rows', 0).click();
        command(root, 'Filter…').click();
        await settled();
        expect(filterItems(root)).toEqual(['☑ (Select All)', '☑ Credit', '☑ FX', '☑ Rates']);
    });

    // Drawing 10,001 Items in jsdom takes about 3.5 s on its own, past Vitest's 5 s default on a loaded runner:
    // the longer budget is CPU time for the DOM, not a wait for anything to happen.
    it('ADR-0061: past the Items Filter… holds, the list says so, and a search is the source\'s to answer', async () => {
        const source = new ManyCodes(10_001);
        const { root } = mount({ source, layout: createPivotLayout({ rows: ['Code'], values: ['Amount'] }) });
        await settled();
        entryButton(root, 'rows', 0).click();
        command(root, 'Filter…').click();
        await settled();
        expect(filterItems(root)).toHaveLength(10_001);
        expect(panel(root).querySelector('.ex-pivot-note')?.textContent).toBe('More than 10,000 items. Search to narrow the list.');
        typeInto(search(root), 'C1000');
        expect(panel(root).querySelector('.ex-pivot-loading')?.textContent).toBe('Loading…');
        await settled();
        expect(source.searches).toEqual(['C1000']);
        expect(filterItems(root)).toEqual(['☑ (Select All)', '☑ C10000']);
        typeInto(search(root), '');
        // The Item with the keyboard keeps it as the list under the note is drawn again.
        const box = panel(root).querySelectorAll<HTMLInputElement>('.ex-pivot-item input')[3];
        box?.focus();
        tickItem(root, 'C00002');
        expect(document.activeElement).toBe(box);
        tickItem(root, 'C00002');
        typeInto(search(root), 'C1000');
        await settled();
        tickItem(root, 'C10000');
        expect(panel(root).querySelector<HTMLButtonElement>('.ex-pivot-ok')?.disabled).toBe(false);
    }, 30_000);
});

function bandButton(root: HTMLElement, caption: string): HTMLButtonElement {
    const filter = [...root.querySelectorAll('.ex-pivot-filters .ex-pivot-filter')].find(f => f.querySelector('.ex-pivot-filter-caption')?.textContent === caption);
    const button = filter?.querySelector<HTMLButtonElement>('.ex-pivot-filter-button');
    if (button === undefined || button === null) throw new Error(`The band has no report filter '${caption}'.`);
    return button;
}

function deferBox(root: HTMLElement): HTMLInputElement {
    const box = root.querySelector<HTMLInputElement>('.ex-pivot-defer input[type=checkbox]');
    if (box === null) throw new Error('The Field List has no Defer Layout Update.');
    return box;
}

function updateButton(root: HTMLElement): HTMLButtonElement {
    const button = root.querySelector<HTMLButtonElement>('.ex-pivot-defer .ex-pivot-update');
    if (button === null) throw new Error('The Field List has no Update.');
    return button;
}

function setDeferred(root: HTMLElement, deferred: boolean): void {
    const box = deferBox(root);
    box.checked = deferred;
    box.dispatchEvent(new Event('change', { bubbles: true }));
}

const rowFields = (layout: PivotLayout | undefined): readonly string[] => defined(layout, 'a layout').rows.map(placement => placement.field);

describe('ExPivot\'s Defer Layout Update (ADR-0061)', () => {
    it('ADR-0061: while it is ticked the pane\'s changes wait, and Update applies them in one change', async () => {
        const changed: PivotLayout[] = [];
        const { root, view } = mount({ source: local(), layout: byRegion, onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        expect(deferBox(root).checked).toBe(false);
        expect(updateButton(root).disabled).toBe(true);
        expect(deferBox(root).nextElementSibling?.textContent).toBe('Defer Layout Update');
        expect(updateButton(root).textContent).toBe('Update');
        setDeferred(root, true);
        expect(updateButton(root).disabled).toBe(true);
        tick(root, 'Desk');
        await settled();
        // The pane shows the pending layout; the report and the Consumer are left alone.
        expect([...area(root, 'rows').querySelectorAll('.ex-pivot-entry-caption')].map(e => e.textContent)).toEqual(['Region', 'Desk']);
        expect(rowFields(view.shownLayout)).toEqual(['Region']);
        expect(changed).toHaveLength(0);
        expect(updateButton(root).disabled).toBe(false);
        updateButton(root).click();
        await settled();
        expect(rowFields(view.shownLayout)).toEqual(['Region', 'Desk']);
        expect(changed.map(rowFields)).toEqual([['Region', 'Desk']]);
        // Still ticked, with nothing left to apply.
        expect(deferBox(root).checked).toBe(true);
        expect(updateButton(root).disabled).toBe(true);
    });

    it('ADR-0061: unticking it applies the pending layout, as Excel does', async () => {
        const changed: PivotLayout[] = [];
        const { root, view } = mount({ source: local(), layout: byRegion, onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        setDeferred(root, true);
        tick(root, 'Desk');
        await settled();
        expect(changed).toHaveLength(0);
        setDeferred(root, false);
        await settled();
        expect(rowFields(view.shownLayout)).toEqual(['Region', 'Desk']);
        expect(changed).toHaveLength(1);
        expect(updateButton(root).disabled).toBe(true);
    });

    it('ADR-0061: a gesture on the report applies at once and reaches the pending layout, so Update does not take it back', async () => {
        const changed: PivotLayout[] = [];
        const { root, grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }), onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        setDeferred(root, true);
        // The pending layout sorts Desk Z to A, which the report does not show yet.
        entryButton(root, 'rows', 1).click();
        command(root, 'Sort Z to A').click();
        await settled();
        expect(changed).toHaveLength(0);
        // EU collapsed by its button: the report shows it at once.
        (await grid.paint(0, 0)).button().click();
        await settled();
        expect((await grid.texts()).map(row => row[0])).toEqual(['EU', 'US', 'FX', 'Rates', 'Grand Total']);
        expect(changed).toHaveLength(1);
        updateButton(root).click();
        await settled();
        // Desk sorted Z to A under the US, and EU still collapsed.
        expect((await grid.texts()).map(row => row[0])).toEqual(['EU', 'US', 'Rates', 'FX', 'Grand Total']);
        expect(changed).toHaveLength(2);
    });

    it('ADR-0061: a gesture on the report made with nothing pending leaves nothing for Update to apply', async () => {
        const changed: PivotLayout[] = [];
        const { root, grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }), onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        setDeferred(root, true);
        (await grid.paint(0, 0)).button().click();
        await settled();
        expect(changed).toHaveLength(1);
        expect(updateButton(root).disabled).toBe(true);
        setDeferred(root, false);
        await settled();
        expect(changed).toHaveLength(1);
    });

    it('ADR-0370 section 3: an Update the source refuses names the fields the pane moved while it was deferred', async () => {
        const refused: (readonly string[])[] = [];
        const { root } = mount({ source: server(), layout: createPivotLayout({ values: ['Amount'] }), maxLeaves: 1, onRefused: event => refused.push(event.fields) });
        await settled();
        setDeferred(root, true);
        drag(field(root, 'Region'), 'dragstart');
        drag(area(root, 'rows'), 'dragenter');
        drag(area(root, 'rows'), 'drop');
        await settled();
        expect(refused).toEqual([]);
        updateButton(root).click();
        await settled();
        expect(refused).toEqual([['Region']]);
        // The pending layout is the user's work in the pane, and stays, with the moves it names.
        expect([...area(root, 'rows').querySelectorAll('.ex-pivot-entry-caption')].map(e => e.textContent)).toEqual(['Region']);
        expect(updateButton(root).disabled).toBe(false);
        setDeferred(root, false);
        await settled();
        expect(refused).toEqual([['Region'], ['Region']]);
    });

    it('ADR-0061: a gesture on the report while Update is asked for leaves nothing for Update to apply once both are shown', async () => {
        const source = new HeldSource();
        const changed: PivotLayout[] = [];
        const { root, grid } = mount({ source, layout: byRegion, onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        source.held.shift()?.();
        await settled();
        setDeferred(root, true);
        tick(root, 'Desk');
        updateButton(root).click();
        await settled();
        expect(source.held).toHaveLength(1);
        // The Layout menu's choice, made while Update's question for Desk is out.
        layoutButton(root).click();
        command(root, 'Show in Tabular Form').click();
        for (let round = 0; round < 5; round++) {
            for (const release of source.held.splice(0).toReversed()) release();
            await settled();
        }
        expect(grid.headers).toEqual(['Region', 'Desk', 'Sum of Amount']);
        expect(changed).toHaveLength(1);
        expect(updateButton(root).disabled).toBe(true);
        setDeferred(root, false);
        await settled();
        expect(changed).toHaveLength(1);
    });

    it('ADR-0370 section 3: a move Update has shown is not named again when a later Update is refused', async () => {
        const source = new HeldSource(server());
        const refused: (readonly string[])[] = [];
        const { root } = mount({ source, layout: createPivotLayout({ values: ['Amount'] }), maxLeaves: 3, onRefused: event => refused.push(event.fields) });
        await settled();
        source.held.shift()?.();
        await settled();
        setDeferred(root, true);
        drag(field(root, 'Region'), 'dragstart');
        drag(area(root, 'rows'), 'dragenter');
        drag(area(root, 'rows'), 'drop');
        updateButton(root).click();
        await settled();
        // Desk to Columns while Update's question for Region is out.
        drag(field(root, 'Desk'), 'dragstart');
        drag(area(root, 'columns'), 'dragenter');
        drag(area(root, 'columns'), 'drop');
        for (const release of source.held.splice(0)) release();
        await settled();
        expect(refused).toEqual([]);
        updateButton(root).click();
        for (let round = 0; round < 5; round++) {
            for (const release of source.held.splice(0)) release();
            await settled();
        }
        expect(refused).toEqual([['Desk']]);
    });

    it('ADR-0061: a new source without a field the pending layout places takes the pane back to the report\'s layout', async () => {
        const options: PivotViewOptions = { source: local(), layout: byRegion };
        const { root, view } = mount(options);
        await settled();
        setDeferred(root, true);
        tick(root, 'Desk');
        const withoutDesk = new RecordPivotSource(trades, fields.filter(declared => declared.name !== 'Desk'));
        view.update({ ...options, source: withoutDesk });
        await settled();
        expect([...area(root, 'rows').querySelectorAll('.ex-pivot-entry-caption')].map(e => e.textContent)).toEqual(['Region']);
        expect(rowFields(view.shownLayout)).toEqual(['Region']);
        expect(updateButton(root).disabled).toBe(true);
    });

    it('ADR-0061: a layout the Consumer hands in replaces the pending one', async () => {
        const changed: PivotLayout[] = [];
        const options: PivotViewOptions = { source: local(), layout: byRegion, onLayoutChanged: event => changed.push(event.layout) };
        const { root, view } = mount(options);
        await settled();
        setDeferred(root, true);
        tick(root, 'Desk');
        const byDesk = createPivotLayout({ rows: ['Desk'], values: ['Amount'] });
        view.update({ ...options, layout: byDesk });
        await settled();
        expect(rowFields(view.shownLayout)).toEqual(['Desk']);
        expect([...area(root, 'rows').querySelectorAll('.ex-pivot-entry-caption')].map(e => e.textContent)).toEqual(['Desk']);
        expect(deferBox(root).checked).toBe(true);
        expect(updateButton(root).disabled).toBe(true);
        expect(changed).toHaveLength(0);
    });
});

describe('ExPivot\'s report filter band (ADR-0061)', () => {
    it('ADR-0061: each field in Filters stands in the band with what it shows, and its Filter… opens over the report and applies there', async () => {
        const { root, view } = mount({ source: local(), layout: createPivotLayout({ filters: ['Region'], rows: ['Desk'], values: ['Amount'] }) });
        await settled();
        const band = root.querySelector('.ex-pivot-toolbar-start .ex-pivot-filters');
        expect(band?.getAttribute('aria-label')).toBe('Report filters');
        const button = bandButton(root, 'Region');
        expect(button.textContent).toBe('(All)▾');
        expect(button.getAttribute('aria-label')).toBe('Filter Region');
        expect(button.classList.contains('ex-pivot-filter-active')).toBe(false);
        button.click();
        const frame = panel(root);
        expect(frame.className).toBe('ex-pivot-popup ex-pivot-popup-overlay');
        expect(frame.parentElement?.className).toBe('ex-pivot-filter-anchor');
        expect(button.getAttribute('aria-expanded')).toBe('true');
        expect(root.querySelector('.ex-pivot-backdrop')).not.toBeNull();
        expect(document.activeElement).toBe(search(root));
        await settled();
        tickItem(root, 'US');
        press(root, 'ex-pivot-ok');
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(root.querySelector('.ex-pivot-backdrop')).toBeNull();
        expect(document.activeElement).toBe(button);
        await settled();
        expect(view.shownLayout.filters[0]?.hiddenItems).toEqual([{ kind: 'text', value: 'US' }]);
        expect(button.textContent).toBe('EU▾');
        expect(button.classList.contains('ex-pivot-filter-active')).toBe(true);
    });

    it('ADR-0061: a filter showing several Items says (Multiple Items), and the backdrop and Escape close its Filter… with the draft', async () => {
        const changed: PivotLayout[] = [];
        const { root } = mount({
            source: local(),
            layout: createPivotLayout({ filters: [{ field: 'Desk', hiddenItems: [{ kind: 'text', value: 'FX' }] }], rows: ['Region'], values: ['Amount'] }),
            onLayoutChanged: event => changed.push(event.layout),
        });
        await settled();
        const button = bandButton(root, 'Desk');
        expect(button.textContent).toBe('(Multiple Items)▾');
        button.click();
        await settled();
        tickItem(root, 'Rates');
        root.querySelector<HTMLElement>('.ex-pivot-backdrop')?.click();
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(document.activeElement).toBe(button);
        button.click();
        await settled();
        expect(filterItems(root)).toEqual(['☐ (Select All)', '☑ Credit', '☐ FX', '☑ Rates']);
        panel(root).dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(document.activeElement).toBe(button);
        await settled();
        expect(changed).toHaveLength(0);
    });

    it('ADR-0061, spine rule 6: a report filter\'s Filter… goes when the pane leaves its layout, and the keyboard it held goes to the button', async () => {
        const options: PivotViewOptions = { source: local(), layout: createPivotLayout({ filters: ['Region'], rows: ['Desk'], values: ['Amount'] }) };
        const { root, view } = mount(options);
        await settled();
        bandButton(root, 'Region').click();
        await settled();
        tickItem(root, 'US');
        view.update({ ...options, layout: createPivotLayout({ filters: ['Region'], columns: ['Desk'], values: ['Amount'] }) });
        expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        expect(root.querySelector('.ex-pivot-backdrop')).toBeNull();
        expect(document.activeElement).toBe(bandButton(root, 'Region'));
        await settled();
        expect(view.shownLayout.filters[0]?.hiddenItems).toEqual([]);
    });

    it('ADR-0061: the band stands only while Filters holds a field', async () => {
        const { root } = mount({ source: local(), layout: byRegion });
        await settled();
        expect(root.querySelector('.ex-pivot-filters')).toBeNull();
        entryButton(root, 'rows', 0).click();
        command(root, 'Move to Report Filter').click();
        expect(bandButton(root, 'Region').textContent).toBe('(All)▾');
    });
});

const byRegionAndDesk: PivotLayout = createPivotLayout({ rows: ['Region'], columns: ['Desk'], values: ['Amount'] });

/** Double clicks a cell of the report, once its first rows are held. */
async function doubleClick(grid: GridStandIn, row: number, column: number): Promise<void> {
    await grid.current.source?.needRange({ start: 0, count: 10 });
    grid.current.onCellDoubleClick?.({ row, column });
    await settled();
}

/** The painted text of a details grid's records. */
async function recordTexts(grid: DetailsGrid): Promise<readonly (readonly string[])[]> {
    const { source, columns } = grid.options;
    if (source === undefined) throw new Error('The details grid was handed no source.');
    const rows = await source.rows({ start: 0, count: 20 }, new AbortController().signal);
    return rows.map(row => columns.map(column => column.format?.(column.value(row)) ?? '')); // oxlint-disable-line ex-grid/no-stand-in-default -- a column without format shows nothing here
}

function tabTitles(root: HTMLElement): readonly string[] {
    return [...root.querySelectorAll('.ex-pivot-tab-button')].map(tab => `${tab.textContent}${tab.getAttribute('aria-selected') === 'true' ? '*' : ''}`);
}

/** A source that holds no Source Version the records are asked under: the data moved on under the report. */
function movedOn(): PivotSource {
    const behind = local();
    return {
        fields: behind.fields,
        features: behind.features,
        aggregate: (...args) => behind.aggregate(...args),
        items: (...args) => behind.items(...args),
        details: () => Promise.resolve(PivotDetailPage.refused({ kind: 'sourceVersionNotHeld', message: 'Moved on.' })),
    };
}

describe('ExPivot\'s Show Details (ADR-0059, ADR-0063)', () => {
    it('ADR-0063: a double click on a value cell opens its records in a tab that covers the report and takes the keyboard', async () => {
        const { root, grid } = mount({ source: local(), layout: byRegionAndDesk });
        await settled();
        await doubleClick(grid, 0, 3);
        expect(tabTitles(root)).toEqual(['PivotTable', 'Details: EU / Rates*']);
        expect(root.querySelector('.ex-pivot-tabs')?.getAttribute('aria-label')).toBe('Sheets');
        const sheet = root.querySelector('.ex-pivot-sheet');
        expect(sheet?.classList.contains('ex-pivot-sheet-covered')).toBe(true);
        const covering = sheet?.querySelector('.ex-pivot-details-panel');
        const tab = root.querySelectorAll('.ex-pivot-tab-button')[1];
        expect(covering?.getAttribute('aria-labelledby')).toBe(tab?.id);
        expect(tab?.getAttribute('aria-controls')).toBe(covering?.id);
        expect(document.activeElement).toBe(tab);
        const [details] = grid.detailsShown;
        if (details === undefined) throw new Error('A details grid stands.');
        expect(details.element.parentElement?.getAttribute('data-ex-input-root')).toBe('');
        expect(details.options.columns.map(column => column.header)).toEqual(['Region', 'Desk', 'Amount']);
        expect(details.options).toMatchObject({ headerClickSelects: true, hideColumnMenu: true, viewportHeight: 'stretch' });
        expect(details.options.onLeave).toBeUndefined();
        expect(await recordTexts(details)).toEqual([['EU', 'Rates', '10']]);
    });

    it('ADR-0063: an empty cell has no records, a total\'s are its Items\', and the grand total\'s are named so', async () => {
        const { root, grid } = mount({ source: local(), layout: byRegionAndDesk });
        await settled();
        await doubleClick(grid, 0, 2);
        expect(tabTitles(root)).toEqual([]);
        await doubleClick(grid, 0, 4);
        await doubleClick(grid, 2, 4);
        expect(tabTitles(root)).toEqual(['PivotTable', 'Details: EU', 'Details: Grand Total*']);
        expect(await recordTexts(defined(grid.detailsShown[0], 'a details grid'))).toHaveLength(4);
        expect(grid.details.map(details => details.destroyed)).toEqual([true, false]);
    });

    it('ADR-0059: closing the selected tab selects the next, and closing the last gives the keyboard back to the report', async () => {
        const { root, grid } = mount({ source: local(), layout: byRegionAndDesk });
        await settled();
        await doubleClick(grid, 0, 3);
        await doubleClick(grid, 1, 3);
        const closes = (): HTMLButtonElement[] => [...root.querySelectorAll<HTMLButtonElement>('.ex-pivot-tab-close')];
        expect(closes().map(close => close.getAttribute('aria-label'))).toEqual(['Close Details: EU / Rates', 'Close Details: US / Rates']);
        root.querySelectorAll<HTMLButtonElement>('.ex-pivot-tab-button')[1]?.click();
        expect(tabTitles(root)).toEqual(['PivotTable', 'Details: EU / Rates*', 'Details: US / Rates']);
        closes()[0]?.click();
        expect(tabTitles(root)).toEqual(['PivotTable', 'Details: US / Rates*']);
        expect(document.activeElement).toBe(root.querySelectorAll('.ex-pivot-tab-button')[1]);
        // A tab behind the selected one closes and leaves the selected one as it was, holding the keyboard.
        await doubleClick(grid, 0, 3);
        root.querySelectorAll<HTMLButtonElement>('.ex-pivot-tab-button')[1]?.click();
        const shownGrid = grid.detailsShown.at(-1);
        closes()[1]?.click();
        expect(tabTitles(root)).toEqual(['PivotTable', 'Details: US / Rates*']);
        expect(grid.detailsShown.at(-1)).toBe(shownGrid);
        expect(document.activeElement).toBe(root.querySelectorAll('.ex-pivot-tab-button')[1]);
        closes()[0]?.click();
        expect(root.querySelector('.ex-pivot-tabs')).toBeNull();
        expect(root.querySelector('.ex-pivot-sheet')?.className).toBe('ex-pivot-sheet');
        expect(document.activeElement?.parentElement?.classList.contains('ex-pivot-sheet')).toBe(true);
        expect(grid.detailsShown).toEqual([]);
    });

    it('ADR-0059: in the dialog, the report and the Field List are inert, Close holds the keyboard, and Escape closes it', async () => {
        const { root, grid } = mount({ source: local(), layout: byRegionAndDesk, detailsView: 'dialog' });
        await settled();
        await doubleClick(grid, 0, 3);
        const dialog = root.querySelector<HTMLElement>('.ex-pivot-dialog');
        expect(dialog?.getAttribute('aria-label')).toBe('Details: EU / Rates');
        expect(dialog?.querySelector('.ex-pivot-dialog-title')?.textContent).toBe('Details: EU / Rates');
        expect(root.querySelector('.ex-pivot-dialog-backdrop')).not.toBeNull();
        expect([root.querySelector<HTMLElement>('.ex-pivot-report')?.inert, root.querySelector<HTMLElement>('.ex-pivot-field-list')?.inert]).toEqual([true, true]);
        expect(document.activeElement?.className).toBe('ex-pivot-close');
        expect(tabTitles(root)).toEqual([]);
        document.activeElement?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
        expect(root.querySelector('.ex-pivot-dialog')).toBeNull();
        expect(root.querySelector<HTMLElement>('.ex-pivot-report')?.inert).toBe(false);
        expect(document.activeElement?.parentElement?.classList.contains('ex-pivot-sheet')).toBe(true);

        // The Escape the dialog's grid has nothing left to dismiss closes it too (ADR-0070).
        await doubleClick(grid, 0, 3);
        defined(grid.detailsShown[0], 'a details grid').options.onLeave?.();
        expect(root.querySelector('.ex-pivot-dialog')).toBeNull();
        expect(grid.detailsShown).toEqual([]);
    });

    it('ADR-0059: a Consumer that hears Show Details takes the records, and the pivot opens nothing', async () => {
        const heard: PivotShowDetailsEvent[] = [];
        const { root, grid } = mount({ source: local(), layout: byRegionAndDesk, onShowDetails: event => heard.push(event) });
        await settled();
        await doubleClick(grid, 0, 3);
        expect(root.querySelector('.ex-pivot-tabs')).toBeNull();
        const [event] = heard;
        if (event === undefined) throw new Error('Show Details was heard.');
        expect([event.title, event.rowItems, event.columnItems, event.valueField]).toEqual([
            'Details: EU / Rates', [{ field: 'Region', item: 'EU' }], [{ field: 'Desk', item: 'Rates' }], 'Sum of Amount',
        ]);
        const page = await event.records(0, 10);
        expect(page.records.map(record => record.values)).toEqual([['EU', 'Rates', 10]]);
        // The question is plain data the Consumer may send elsewhere, and a page it asks for is checked as the question is.
        expect(event.query.rowItems).toEqual([{ field: 'Region', item: { kind: 'text', value: 'EU' } }]);
        expect(() => event.records(-1, 10)).toThrow(RangeError);
    });

    it('ADR-0063: records the source no longer holds under the report\'s Source Version are refused in words, never shown', async () => {
        const { root, grid } = mount({ source: movedOn(), layout: byRegionAndDesk });
        await settled();
        await doubleClick(grid, 0, 3);
        // The keyboard in the records' grid goes to the tab when the sentence replaces the grid.
        const details = defined(grid.detailsShown[0], 'a details grid');
        details.element.focus();
        await details.options.source?.needRange({ start: 0, count: 10 });
        await settled();
        expect(document.activeElement).toBe(root.querySelectorAll('.ex-pivot-tab-button')[1]);
        const status = root.querySelector('.ex-pivot-details-panel .ex-pivot-details-status');
        expect(status?.getAttribute('role')).toBe('alert');
        expect(status?.textContent).toBe('The data has changed — refresh.');
        expect(grid.detailsShown).toEqual([]);
    });
});

/** The report's commands in the Context Menu opened on a cell of the rows the grid holds first. */
async function contextMenu(grid: GridStandIn, row: number, column: number): Promise<readonly GridCommand[]> {
    const { source, columns, contextCommands } = grid.current;
    if (source === undefined || contextCommands === undefined) throw new Error('The report offers commands.');
    await source.needRange({ start: 0, count: 20 });
    const { window } = source.snapshot();
    const shown = defined(columns[column], 'the column');
    return contextCommands({
        row: defined(window[row], 'the row'), column: shown.name, columnType: shown.type, selection: [], rowSequenceVersion: 0, commands: [],
    });
}

function labelled(grid: GridStandIn, commands: readonly GridCommand[]): readonly string[] {
    // An id the pivot has no word for reads as itself here.
    return commands.map(offered => `${grid.current.commandLabel?.(offered.id) ?? offered.id}${offered.enabled ? '' : ' (off)'}`);
}

function run(commands: readonly GridCommand[], id: string): void {
    defined(commands.find(offered => offered.id === id), `the command ${id}`).invoke();
}

describe('ExPivot\'s Context Menu (ADR-0036, ADR-0059)', () => {
    it('ADR-0059: on an outer Item\'s label it offers Expand and Collapse, the field\'s sorts and Remove, and Collapse collapses it', async () => {
        const changed: PivotLayout[] = [];
        const { grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }), onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        const commands = await contextMenu(grid, 0, 0);
        expect(labelled(grid, commands)).toEqual([
            'Expand (off)', 'Collapse', 'Expand Entire Field (off)', 'Collapse Entire Field', 'Sort A to Z (off)', 'Sort Z to A', 'Remove "Region"', 'Hide Field List',
        ]);
        expect(grid.current.commandLabel?.('copy')).toBeUndefined();
        run(commands, 'collapse');
        await settled();
        expect((await grid.texts()).map(row => row[0])).toEqual(['EU', 'US', 'FX', 'Rates', 'Grand Total']);
        expect(changed).toHaveLength(1);
        // The collapsed Item keeps the Focus, as one collapsed by its button does.
        await grid.current.source?.needRange({ start: 0, count: 10 });
        expect(grid.placed.map(placed => placed.focus)).toEqual([{ row: 0, column: 0 }]);
    });

    it('ADR-0059: on a value cell it sorts its row field by the Value Field, shows its records and opens Value Field Settings… in the pane', async () => {
        const { root, grid } = mount({ source: local(), layout: byRegionAndDesk, showFieldList: false });
        await settled();
        const commands = await contextMenu(grid, 0, 3);
        expect(labelled(grid, commands)).toEqual([
            'Sort Smallest to Largest', 'Sort Largest to Smallest', 'Show Details', 'Value Field Settings…', 'Remove "Region"', 'Show Field List',
        ]);
        run(commands, 'sort-largest-to-smallest');
        await settled();
        expect((await grid.texts()).map(row => row[0])).toEqual(['EU', 'US', 'Grand Total']);
        run(await contextMenu(grid, 0, 3), 'show-details');
        expect(tabTitles(root)).toEqual(['PivotTable', 'Details: EU / Rates*']);
        expect(labelled(grid, await contextMenu(grid, 0, 2)).filter(label => label.startsWith('Show Details'))).toEqual(['Show Details (off)']);
        run(await contextMenu(grid, 0, 3), 'value-field-settings');
        expect(root.querySelector<HTMLElement>('.ex-pivot-field-list')?.hidden).toBe(false);
        expect(panelText(root)?.value).toBe('Sum of Amount');
        expect(document.activeElement).toBe(panelText(root));
    });

    it('ADR-0370 section 10: the Consumer\'s words name the report\'s commands and the grid\'s own', async () => {
        const { grid } = mount({ source: local(), layout: byRegionAndDesk, label: japanesePivotWords });
        await settled();
        const labels = labelled(grid, await contextMenu(grid, 0, 3));
        expect(labels).toContain('値フィールドの設定...');
        expect(labels.some(label => label.includes('Region'))).toBe(true);
        expect(grid.current.commandLabel?.('copy')).toBe('コピー');
    });

    it('spine rule 6: a sort by a Value Field, or its settings, chosen once the pane moved that Value Field, change nothing', async () => {
        const changed: PivotLayout[] = [];
        const layout = createPivotLayout({ rows: ['Region'], values: ['Amount', 'Desk'] });
        const { root, grid } = mount({ source: local(), layout, onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        const commands = await contextMenu(grid, 0, 1);
        entryButton(root, 'values', 0).click();
        command(root, 'Move Down').click();
        await settled();
        expect(changed).toHaveLength(1);
        run(commands, 'sort-largest-to-smallest');
        run(commands, 'value-field-settings');
        await settled();
        expect(changed).toHaveLength(1);
        expect(root.querySelector('.ex-pivot-panel')).toBeNull();
    });

    it('a command that would change nothing in the pane now asks nothing: an Item collapsed since the menu opened', async () => {
        const changed: PivotLayout[] = [];
        const { grid } = mount({ source: local(), layout: createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] }), onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        const commands = await contextMenu(grid, 0, 0);
        (await grid.paint(0, 0)).button().click();
        await settled();
        run(commands, 'collapse');
        await settled();
        expect(changed).toHaveLength(1);
    });

    it('spine rule 6: a command chosen once the pane no longer holds its field changes nothing', async () => {
        const changed: PivotLayout[] = [];
        const { root, grid } = mount({ source: local(), layout: byRegionAndDesk, onLayoutChanged: event => changed.push(event.layout) });
        await settled();
        const commands = await contextMenu(grid, 0, 0);
        entryButton(root, 'rows', 0).click();
        command(root, 'Remove Field').click();
        await settled();
        expect(changed).toHaveLength(1);
        run(commands, 'sort-descending');
        await settled();
        expect(changed).toHaveLength(1);
    });
});

describe('ExPivot\'s options', () => {
    it('ADR-0059: a layout this pivot raised is never asked for when handed back, and a copy of an earlier one is', async () => {
        const changed: PivotLayout[] = [];
        const options: PivotViewOptions = { source: local(), layout: byRegion, onLayoutChanged: event => changed.push(event.layout) };
        const { root, view } = mount(options);
        await settled();
        tick(root, 'Desk');
        await settled();
        tick(root, 'Region');
        await settled();
        const [both, deskOnly] = changed;
        if (both === undefined || deskOnly === undefined) throw new Error('Two layouts were raised.');
        // Bound back, however late: the pivot's own layout, which asks nothing, as the Razor component's.
        view.update({ ...options, layout: both });
        await settled();
        expect(view.shownLayout).toBe(deskOnly);
        // An undo hands in a layout of the Consumer's own making.
        const undone = createPivotLayout({ rows: ['Region', 'Desk'], values: ['Amount'] });
        view.update({ ...options, layout: undone });
        await settled();
        expect(view.shownLayout).toBe(undone);
    });

    it('ADR-0370 section 10: the user\'s move asked again under a new locale is still the user\'s, raised once shown in that locale', async () => {
        const changed: PivotLayout[] = [];
        const options: PivotViewOptions = { source: local(), layout: byRegion, onLayoutChanged: event => changed.push(event.layout) };
        const { root, view, grid } = mount(options);
        await settled();
        tick(root, 'Desk');
        view.update({ ...options, locale: 'de-DE' });
        await settled();
        expect(changed.map(layout => layout.rows.map(p => p.field))).toEqual([['Region', 'Desk']]);
        expect(grid.current.locale).toBe('de-DE');
    });

    it('ADR-0059: layouts this pivot raised, handed back late and in turn, ask for nothing', async () => {
        const changed: PivotLayout[] = [];
        const options: PivotViewOptions = { source: local(), layout: byRegion, onLayoutChanged: event => changed.push(event.layout) };
        const { root, view } = mount(options);
        await settled();
        tick(root, 'Desk');
        await settled();
        tick(root, 'Region');
        await settled();
        const [both, deskOnly] = changed;
        if (both === undefined || deskOnly === undefined) throw new Error('Two layouts were raised.');
        view.update({ ...options, layout: both });
        view.update({ ...options, layout: deskOnly });
        await settled();
        expect(view.shownLayout).toBe(deskOnly);
        expect(changed).toHaveLength(2);
    });

    it('ADR-0059, spine rule 6: a layout raised, handed back while the user\'s newer move is out, does not undo that move', async () => {
        const changed: PivotLayout[] = [];
        const options: PivotViewOptions = { source: local(), layout: byRegion, onLayoutChanged: event => changed.push(event.layout) };
        const { root, view } = mount(options);
        await settled();
        tick(root, 'Desk');
        await settled();
        const [both] = changed;
        if (both === undefined) throw new Error('A layout was raised.');
        tick(root, 'Region');
        view.update({ ...options, layout: both });
        await settled();
        expect(view.shownLayout.rows.map(p => p.field)).toEqual(['Desk']);
    });

    it('ADR-0370 section 3: a field moved into Values is named when the question it went with is refused', async () => {
        const refused: (readonly string[])[] = [];
        const { root } = mount({ source: server(), layout: createPivotLayout({ values: ['Amount'] }), maxLeaves: 1, onRefused: event => refused.push(event.fields) });
        await settled();
        drag(field(root, 'Desk'), 'dragstart');
        drag(area(root, 'values'), 'dragenter');
        drag(area(root, 'values'), 'drop');
        drag(field(root, 'Region'), 'dragstart');
        drag(area(root, 'rows'), 'dragenter');
        drag(area(root, 'rows'), 'drop');
        await settled();
        expect(refused).toEqual([['Desk', 'Region']]);
    });

    it('ADR-0059: a layout naming a field the source does not offer is refused, and the pivot stays as it was', async () => {
        const options: PivotViewOptions = { source: local(), layout: byRegion };
        const { view } = mount(options);
        await settled();
        const wrong = createPivotLayout({ rows: ['Customer'], values: ['Amount'] });
        expect(() => { view.update({ ...options, layout: wrong }); }).toThrow(/Customer/);
        expect(() => { view.update({ ...options, layout: wrong }); }).toThrow(/Customer/);
        view.update(options);
        await settled();
        expect(view.shownLayout).toBe(byRegion);
    });

    it('ADR-0013: a row height handed in reaches the report grid at once', async () => {
        const options: PivotViewOptions = { source: local(), layout: byRegion };
        const { view, grid } = mount(options);
        await settled();
        view.update({ ...options, rowHeight: 32 });
        expect(grid.current.rowHeight).toBe(32);
    });

    it('ADR-0059: a new source leaves the rows on screen readable until its answer replaces them', async () => {
        const many = Array.from({ length: 1200 }, (_, i): Trade => ({ region: `Region ${i}`, desk: 'Rates', amount: i }));
        const options: PivotViewOptions = { source: new RecordPivotSource(many, fields), layout: byRegion };
        const { view, grid } = mount(options);
        await settled();
        const before = grid.current.source;
        if (before === undefined) throw new Error('The grid was handed no source.');
        const next = new HeldSource();
        view.update({ ...options, source: next });
        // Rows past the first answer's are asked of the report source that answered it, and come.
        const rows = await before.rows({ start: 600, count: 10 }, new AbortController().signal);
        expect(rows).toHaveLength(10);
        expect(rows.map(row => row.labels[0]?.text?.slice(0, 7))).toEqual(Array.from({ length: 10 }, () => 'Region '));
        next.held.splice(0).forEach(release => { release(); });
        await settled();
        expect(grid.current.source).not.toBe(before);
    });
});

describe('browserPivotLocale', () => {
    it('ADR-0370 section 10: the first of the browser\'s languages the pivot writes in, never another standing in for one', () => {
        expect(browserPivotLocale(['xx-YY', 'de-DE'])).toBe('de-DE');
        expect(browserPivotLocale(['EN-us'])).toBe('en-US');
        // Latin American Spanish is not written as Spain's, which groups thousands with a point.
        expect(browserPivotLocale(['es-419', 'en-US'])).toBe('en-US');
    });

    it('ADR-0370 section 10: a browser with none of the pivot\'s languages is refused, saying to pass locale', () => {
        expect(() => browserPivotLocale(['xx-YY'])).toThrow(/pass locale/);
    });
});
