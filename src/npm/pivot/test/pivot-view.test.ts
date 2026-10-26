// @vitest-environment happy-dom
// ExPivot's Script Core in a DOM emulation (layer 2, ADR-0350 section 13). What the grid paints is the core's,
// tested there: here the grid is a stand-in that records what the pivot hands it, and the tests judge what the
// pivot decides — what it asks, which answer it shows, what the Field List draws and marks, what it refuses.
import { afterEach, describe, expect, it } from 'vitest';
import type { GridHandle, GridOptions } from '@ex-grid/core';
import {
    FetchingPivotSource, PivotSourceFeatures, RecordPivotSource, createPivotLayout, japanesePivotWords, pivotRecordField,
    type PivotDisplayRow, type PivotLayout, type PivotSlicing, type PivotSource,
} from '@ex-grid/pivot-engine';
import { browserPivotLocale } from '../src/browser-locale';
import { PivotView, type PivotViewOptions } from '../src/pivot-view';

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
