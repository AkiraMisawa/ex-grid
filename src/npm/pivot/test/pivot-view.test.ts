// @vitest-environment happy-dom
// ExPivot's Script Core in a DOM emulation (layer 2, ADR-0350 section 13). What the grid paints is the core's,
// tested there: here the grid is a stand-in that records what the pivot hands it, and the tests judge what the
// pivot decides — what it asks, which answer it shows, what the Field List draws and marks, what it refuses.
import { afterEach, describe, expect, it } from 'vitest';
import type { GridHandle, GridOptions, GridSelection } from '@ex-grid/core';
import {
    FetchingPivotSource, PivotItemPage, PivotSourceFeatures, RecordPivotSource, createPivotLayout, japanesePivotWords, pivotRecordField,
    type PivotDisplayRow, type PivotLayout, type PivotSlicing, type PivotSource,
} from '@ex-grid/pivot-engine';
import { browserPivotLocale } from '../src/browser-locale';
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
    readonly #behind = local();
    readonly held: (() => void)[] = [];
    readonly fields = this.#behind.fields;
    readonly features = this.#behind.features;

    aggregate(...args: Parameters<PivotSource['aggregate']>): ReturnType<PivotSource['aggregate']> {
        return new Promise(resolve => { this.held.push(() => { resolve(this.#behind.aggregate(...args)); }); });
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

/** The grid as the pivot sees it: the options it was last handed. */
class GridStandIn {
    options: GridOptions<PivotDisplayRow> | undefined;
    mounts = 0;
    destroyed = false;

    readonly mount = (_element: HTMLElement, options: GridOptions<PivotDisplayRow>): GridHandle<PivotDisplayRow> => {
        this.mounts++;
        this.options = options;
        return {
            update: next => { this.options = next; },
            placeSelection: () => { throw new Error('The pivot places no selection.'); },
            readView: () => { throw new Error('The pivot reads no view.'); },
            showViewWith: () => { throw new Error('The pivot shows no view.'); },
            focus: () => { throw new Error('The pivot focuses no grid.'); },
            keyName: () => { throw new Error('The pivot names no key.'); },
            destroy: () => { this.destroyed = true; },
        };
    };

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
    });
});

function bandButton(root: HTMLElement, caption: string): HTMLButtonElement {
    const filter = [...root.querySelectorAll('.ex-pivot-filters .ex-pivot-filter')].find(f => f.querySelector('.ex-pivot-filter-caption')?.textContent === caption);
    const button = filter?.querySelector<HTMLButtonElement>('.ex-pivot-filter-button');
    if (button === undefined || button === null) throw new Error(`The band has no report filter '${caption}'.`);
    return button;
}

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
