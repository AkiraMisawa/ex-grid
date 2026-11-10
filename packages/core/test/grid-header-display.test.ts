// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the header's display (ADR-0500). `standard` is the header an untouched grid always
// drew, to the attribute; `compact` gives the ▾ and the marks of the column's Sort and filter one Header Box,
// charges a header that box and no more, and changes neither a declared width nor anything the grid holds when it
// is switched on a grid in use.
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridColumnWidthChange } from '../src/dom/column-gestures';
import type { GridOptions } from '../src/dom/grid-view';
import { GridMetrics } from '../src/grid-metrics';
import { japaneseGridWords } from '../src/chrome/japanese-grid-words';
import { GridSource } from '../src/source/grid-source';
import { gridHeaderRequiredPx } from '../src/text-geometry';
import { mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

// Two columns whose header decides their width, one that can be sorted and one that cannot, and one whose values do.
const auto: readonly GridColumn<Trade>[] = [
    { name: 'Ccy', type: 'text', value: () => 'USD' },
    { name: 'Tag', type: 'text', value: () => 'a', queryable: false },
    { name: 'Book', type: 'text', value: trade => `${trade.book} at a desk with a long name` },
];
const fixed: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100 },
];
const box = { rowHeight: 24, viewportHeight: 320, viewportWidth: 720 } as const;
const metrics = GridMetrics.resolve({ density: 'compact' });

function mouse(target: Element, type: string, init: MouseEventInit & { readonly offsetX?: number; readonly offsetY?: number } = {}): MouseEvent {
    const { offsetX, offsetY, ...rest } = init;
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, ...rest });
    if (offsetX !== undefined) Object.defineProperty(event, 'offsetX', { value: offsetX });
    if (offsetY !== undefined) Object.defineProperty(event, 'offsetY', { value: offsetY });
    target.dispatchEvent(event);
    return event;
}
/** A press and its click on the header at its own offsets. */
function clickHeader(root: HTMLElement, x: number): void {
    const header = q(root, '.ex-header');
    mouse(header, 'mousedown', { clientX: x, clientY: 10, offsetX: x, offsetY: 10, buttons: 1 });
    mouse(header, 'mouseup', { clientX: x, clientY: 10, offsetX: x, offsetY: 10 });
    mouse(header, 'click', { clientX: x, clientY: 10, offsetX: x, offsetY: 10 });
}
/** A press and its click on the Header Box's button of `column`. */
function pressBox(root: HTMLElement, column: number): void {
    const button = defined(cells(root)[column], `column ${column}'s header`).querySelector('.ex-menu-button');
    if (button === null) throw new Error(`Column ${column} has no ▾.`);
    mouse(button, 'mousedown');
    mouse(button, 'mouseup');
    mouse(button, 'click');
}
function key(target: Element, name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    return event;
}

/** What the DOM answers with `null` when it is missing, and the test knows is there. */
function present<T>(value: T | null, what: string): T {
    if (value === null) throw new Error(`${what} is missing.`);
    return value;
}
/** A place on the header band, at its own offsets, with the client x at the same place. */
const onBand = (x: number, init: MouseEventInit = {}): MouseEventInit & { offsetX: number; offsetY: number } =>
    ({ clientX: x, clientY: 10, offsetX: x, offsetY: 10, ...init });
const cells = (root: HTMLElement): HTMLElement[] => [...root.querySelectorAll<HTMLElement>('.ex-header-cell')];
/** Each header's width, as the grid writes it inline (ADR-0016). */
const widths = (root: HTMLElement): number[] => cells(root).map(cell => {
    const width = /(?:^|; )width: ([\d.]+)px/u.exec(present(cell.getAttribute('style'), 'a header cell\'s style'));
    return Number(present(width, 'a header cell\'s width')[1]);
});
/** What each header says of its column: its aria-sort, whether it is marked filtered, and its box's name. */
const said = (root: HTMLElement): string[] => cells(root).map(cell => {
    const headerBox = cell.querySelector('.ex-menu-button, .ex-header-state');
    return [
        String(cell.getAttribute('aria-sort')), cell.hasAttribute('data-ex-filtered') ? 'filtered' : 'unfiltered',
        headerBox === null ? 'no box' : `${headerBox.tagName.toLowerCase()}:${headerBox.getAttribute('aria-label')}`,
    ].join(' ');
});
const menu = (root: HTMLElement): HTMLElement | null => root.querySelector<HTMLElement>(':scope > .ex-popover > .ex-popover-commands[role=menu]');
const notBlank = { clauses: [{ operator: 'isNotBlank' }] } as const;

describe('the standard header', () => {
    it('ADR-0500: a grid given no headerDisplay draws the header it always drew, and so does one given standard', async () => {
        const source = GridSource.from(trades(50));
        const options = { columns: fixed, source, ...box };
        const { root } = mount<Trade>(options);
        const { root: named } = mount<Trade>({ ...options, source: GridSource.from(trades(50)), headerDisplay: 'standard' });
        source.setSorts([{ column: 'Book', direction: 'ascending' }]);
        source.setFilter({ columns: { Book: notBlank } });
        await painted();
        // No mark on the band, none of a filter on a cell, and the ▾ named as it always was.
        expect(q(root, '.ex-header').hasAttribute('data-ex-header-display')).toBe(false);
        expect(said(root)).toEqual(['ascending unfiltered button:Column menu', 'none unfiltered button:Column menu']);
        expect(q(named, '.ex-header').outerHTML.replace(/ex-\d+-/gu, '')).toBe(
            q(mount<Trade>({ ...options, source: GridSource.from(trades(50)) }).root, '.ex-header').outerHTML.replace(/ex-\d+-/gu, ''));
    });

    it('ADR-0500: a headerDisplay that is neither is refused by name, on a new grid and on one in use', async () => {
        const options = { columns: fixed, rows: trades(5), ...box };
        expect(() => mount<Trade>({ ...options, headerDisplay: 'dense' as never })).toThrow('A header display is \'standard\' or \'compact\', not "dense" (ADR-0500).');
        const { root, view } = mount<Trade>(options);
        await painted();
        expect(() => { view.update({ ...options, headerDisplay: 'excel' as never }); }).toThrow(RangeError);
        expect(q(root, '.ex-header').hasAttribute('data-ex-header-display')).toBe(false);
        expect(() => metrics.headerRequiredPx({ label: 'A', menuButton: true, sortable: true, display: 'dense' as never }))
            .toThrow('A header display is \'standard\' or \'compact\', not "dense" (ADR-0500).');
    });
});

describe('the compact header\'s Header Box', () => {
    it('ADR-0500: the box is the ▾ of a column that holds nothing, and says a Sort, a filter or both of one that does', async () => {
        const source = GridSource.from(trades(50));
        const { root } = mount<Trade>({ columns: fixed, source, ...box, headerDisplay: 'compact' });
        await painted();
        expect(q(root, '.ex-header').getAttribute('data-ex-header-display')).toBe('compact');
        expect(said(root)).toEqual(['none unfiltered button:Column menu', 'none unfiltered button:Column menu']);

        source.setSorts([{ column: 'Book', direction: 'descending' }]);
        await painted();
        expect(said(root)).toEqual(['descending unfiltered button:Column menu', 'none unfiltered button:Column menu']);

        source.setFilter({ columns: { Notional: notBlank } });
        await painted();
        expect(said(root)).toEqual(['descending unfiltered button:Column menu', 'none filtered button:Column menu, filtered']);

        // A Sort of several levels marks each of its columns, and a filter beside it marks its own.
        source.setSorts([{ column: 'Notional', direction: 'ascending' }, { column: 'Book', direction: 'descending' }]);
        await painted();
        expect(said(root)).toEqual(['descending unfiltered button:Column menu', 'ascending filtered button:Column menu, filtered']);

        source.setSorts([]);
        source.setFilter(undefined);
        await painted();
        expect(said(root)).toEqual(['none unfiltered button:Column menu', 'none unfiltered button:Column menu']);
    });

    it('ADR-0500: the box\'s button is never reached by Tab, and is named through commandLabel', async () => {
        const source = GridSource.from(trades(50));
        const { root } = mount<Trade>({ columns: fixed, source, ...box, headerDisplay: 'compact', commandLabel: japaneseGridWords });
        source.setFilter({ columns: { Book: notBlank } });
        await painted();
        expect(root.querySelectorAll('.ex-header-cell > .ex-menu-button[tabindex="-1"]')).toHaveLength(2);
        expect(said(root)).toEqual(['none filtered button:列のメニュー (フィルター適用中)', 'none unfiltered button:列のメニュー']);
    });

    it('ADR-0500: with the column menu hidden the box is a mark and nothing to press, on a column that can be sorted', async () => {
        const source = GridSource.from(trades(50));
        const { root } = mount<Trade>({ columns: auto, source, ...box, headerDisplay: 'compact', hideColumnMenu: true });
        source.setSorts([{ column: 'Ccy', direction: 'ascending' }]);
        source.setFilter({ columns: { Book: notBlank } });
        await painted();
        expect(said(root)).toEqual(['ascending unfiltered span:null', 'null unfiltered no box', 'none filtered span:Filtered']);
        const [sorted, , filtered] = cells(root).map(cell => cell.querySelector('.ex-header-state'));
        expect([sorted?.getAttribute('aria-hidden'), filtered?.getAttribute('role')]).toEqual(['true', 'img']);
    });

    it('ADR-0500 / ADR-0012: a click on a compact header still sorts, a press on its box opens the menu and sorts nothing, and Alt+↓ opens the Focus\'s', async () => {
        const source = GridSource.from(trades(50));
        const { root } = mount<Trade>({ columns: fixed, source, ...box, headerDisplay: 'compact' });
        await painted();
        clickHeader(root, 20);
        await painted();
        expect(source.snapshot().sorts).toEqual([{ column: 'Book', direction: 'ascending' }]);

        pressBox(root, 1);
        expect(menu(root)?.getAttribute('aria-label')).toBe('Notional');
        expect(source.snapshot().sorts).toEqual([{ column: 'Book', direction: 'ascending' }]);
        // Escape closes it, and the keyboard is the grid's again.
        key(present(document.activeElement, 'the menu\'s first command'), 'Escape');
        expect([menu(root), document.activeElement]).toEqual([null, root]);

        mouse(q(root, '.ex-viewport'), 'mousedown', { offsetX: 10, offsetY: 5, buttons: 1 });
        mouse(q(root, '.ex-viewport'), 'mouseup', { offsetX: 10, offsetY: 5 });
        key(root, 'ArrowDown', { altKey: true });
        expect(menu(root)?.getAttribute('aria-label')).toBe('Book');
    });
});

describe('what a header\'s gestures mean under either display', () => {
    // Book 110, Notional 140 and Quantity 120 wide, as grid-column-gestures.test.ts has them.
    const three: readonly GridColumn<Trade>[] = [
        { name: 'Book', type: 'text', value: trade => trade.book, width: 110 },
        { name: 'Notional', type: 'number', value: trade => trade.notional, width: 140 },
        { name: 'Quantity', type: 'number', value: trade => trade.notional / 10, width: 120 },
    ];

    /** A reorder by a drag, a resize from a grip, a Shift and a Ctrl click, and a click that selects: what the grid said of each. */
    async function gestures(headerDisplay: 'standard' | 'compact'): Promise<string[]> {
        const heard: string[] = [];
        const options: GridOptions<Trade> = {
            columns: three, rows: trades(20), ...box, headerHeight: 28, headerDisplay,
            onSelectionChanged: selection => { heard.push(`selection ${JSON.stringify(selection.ranges)}`); },
            onColumnOrderChanged: order => { heard.push(`order ${order.join(',')}`); },
            onColumnWidthChanged: change => { heard.push(`width ${change.column} ${change.widthPx}`); },
        };
        const { root, view } = mount<Trade>(options);
        await painted();
        const header = q(root, '.ex-header');

        mouse(header, 'mousedown', onBand(55, { buttons: 1 }));
        mouse(q(root, '.ex-viewport'), 'mousemove', { clientX: 600, clientY: 100, buttons: 1 });
        mouse(header, 'mouseup', { clientX: 600, clientY: 10 });
        mouse(header, 'click', onBand(600));

        const grip = defined(root.querySelectorAll('.ex-header-cell .ex-resize-grip')[1], 'Notional\'s grip');
        mouse(grip, 'mousedown', { clientX: 248, buttons: 1 });
        mouse(q(root, '.ex-viewport'), 'mousemove', { clientX: 286, clientY: 100, buttons: 1 });
        mouse(grip, 'mouseup', { clientX: 286, clientY: 10 });
        mouse(grip, 'click', { clientX: 286 });

        mouse(header, 'click', onBand(50, { shiftKey: true }));
        mouse(header, 'click', onBand(300, { ctrlKey: true }));
        await painted();
        heard.push(`sorted ${cells(root).map(cell => cell.getAttribute('aria-sort')).join(',')}`);

        // A press on a header is one or the other's: the reorder is dropped for the click that selects.
        const { onColumnOrderChanged: _, ...withoutReorder } = options;
        view.update({ ...withoutReorder, headerClickSelects: true });
        await painted();
        mouse(header, 'mousedown', onBand(150, { buttons: 1 }));
        mouse(header, 'mouseup', onBand(150));
        mouse(header, 'click', onBand(150));
        await painted();
        heard.push(`sorted ${cells(root).map(cell => cell.getAttribute('aria-sort')).join(',')}`);
        unmountAll();
        return heard;
    }

    it('ADR-0500 / ADR-0011 / ADR-0016: a drag reorders, a grip resizes, Shift and Ctrl select columns and headerClickSelects selects, the same under compact as under standard', async () => {
        const standard = await gestures('standard');
        // Book and Quantity whole by Shift and Ctrl, told once they are painted; then Notional whole by the click that selects.
        expect(standard).toEqual([
            'order Notional,Quantity,Book',
            'width Notional 178',
            'selection [{"topRow":0,"leftColumn":0,"rowCount":20,"columnCount":1},{"topRow":0,"leftColumn":2,"rowCount":20,"columnCount":1}]',
            'sorted none,none,none',
            'selection [{"topRow":0,"leftColumn":1,"rowCount":20,"columnCount":1}]',
            'sorted none,none,none',
        ]);
        expect(await gestures('compact')).toEqual(standard);
    });
});

describe('a compact header under Header Groups', () => {
    it('ADR-0500 / ADR-0032: the leaves wear the Header Box and a group\'s cell does not, and the groups stand where they stood', async () => {
        const three: readonly GridColumn<Trade>[] = [
            { name: 'Book', type: 'text', value: trade => trade.book, width: 110 },
            { name: 'Notional', type: 'number', value: trade => trade.notional, width: 140 },
            { name: 'Quantity', type: 'number', value: trade => trade.notional / 10, width: 120 },
        ];
        const source = GridSource.from(trades(20));
        const options: GridOptions<Trade> = { columns: three, source, ...box, headerGroups: [{ label: 'Money', columns: ['Notional', 'Quantity'] }] };
        const { root, view } = mount<Trade>(options);
        source.setSorts([{ column: 'Notional', direction: 'ascending' }]);
        await painted();
        const groups = (): (string | null)[] => [...root.querySelectorAll('.ex-header-group')].map(group => `${group.textContent} ${group.getAttribute('style')}`);
        const standard = { groups: groups(), widths: widths(root) };
        expect(standard.groups).toHaveLength(1);

        view.update({ ...options, headerDisplay: 'compact' });
        await painted();
        expect(said(root)).toEqual(['none unfiltered button:Column menu', 'ascending unfiltered button:Column menu', 'none unfiltered button:Column menu']);
        expect(root.querySelectorAll('.ex-header-group :is(.ex-menu-button, .ex-header-state)')).toHaveLength(0);
        expect({ groups: groups(), widths: widths(root) }).toEqual(standard);
    });
});

describe('what a compact header needs', () => {
    it('ADR-0500: an Auto column whose header decides is narrower by what the header no longer keeps aside', async () => {
        const { root: standard } = mount<Trade>({ columns: auto, rows: trades(30), ...box });
        const { root: compact } = mount<Trade>({ columns: auto, rows: trades(30), ...box, headerDisplay: 'compact' });
        await painted();
        const [ccy, tag, book] = widths(standard);
        // The ▾'s band and the sort mark's box against the one box: 22 + 20 against 14, and 22 against 14 where
        // the column cannot be sorted. A column its values decide is the same in both.
        expect(widths(compact)).toEqual([defined(ccy, 'Ccy') - 28, defined(tag, 'Tag') - 8, book]);
        // What is painted is what the header needs, each edge on a Device Pixel (ADR-0090): the label, the em of
        // slack and the box's 14px.
        const needs = [
            metrics.headerRequiredPx({ label: 'Ccy', menuButton: true, sortable: true, display: 'compact' }),
            metrics.headerRequiredPx({ label: 'Tag', menuButton: true, sortable: false, display: 'compact' }),
        ];
        expect(needs).toEqual([73.25, 73.25]);
        expect(widths(compact).slice(0, 2).map((width, at) => Math.abs(width - defined(needs[at], 'a need')) < 1)).toEqual([true, true]);
        // The entry a Consumer charges a header by, beside the grid, answers the same.
        expect(gridHeaderRequiredPx('Ccy', { menuButton: true, sortable: true, headerDisplay: 'compact' })).toBe(73.25);
        expect(gridHeaderRequiredPx('Ccy', { menuButton: true, sortable: true })).toBe(101.25);
    });

    it('ADR-0500: with the column menu hidden a header that can be sorted keeps the box for its marks, and one that cannot keeps nothing', async () => {
        const { root: standard } = mount<Trade>({ columns: auto, rows: trades(30), ...box, hideColumnMenu: true });
        const { root: compact } = mount<Trade>({ columns: auto, rows: trades(30), ...box, hideColumnMenu: true, headerDisplay: 'compact' });
        await painted();
        const [ccy, tag] = widths(standard);
        expect(widths(compact).slice(0, 2)).toEqual([defined(ccy, 'Ccy') - 6, tag]);
    });

    it('ADR-0500 / ADR-0016: a Sort or a filter taken or dropped moves no width', async () => {
        const source = GridSource.from(trades(30));
        const { root } = mount<Trade>({ columns: auto, source, ...box, headerDisplay: 'compact' });
        await painted();
        const before = widths(root);
        source.setSorts([{ column: 'Ccy', direction: 'descending' }]);
        source.setFilter({ columns: { Ccy: notBlank, Book: notBlank } });
        await painted();
        expect(said(root)[0]).toBe('descending filtered button:Column menu, filtered');
        expect(widths(root)).toEqual(before);
        source.setSorts([]);
        source.setFilter(undefined);
        await painted();
        expect(widths(root)).toEqual(before);
    });

    it('ADR-0500 / ADR-0016: Size to fit fixes a column at what its compact header needs, whatever stands open', async () => {
        const changes: GridColumnWidthChange[] = [];
        const narrow: readonly GridColumn<Trade>[] = [{ name: 'Ccy', type: 'text', value: () => 'USD', width: 200 }, ...auto.slice(1)];
        const options: GridOptions<Trade> = { columns: narrow, rows: trades(30), ...box, headerDisplay: 'compact', onColumnWidthChanged: change => { changes.push(change); } };
        const { root } = mount<Trade>(options);
        await painted();
        const grip = (): Element => q(root, '.ex-header-cell > .ex-resize-grip');
        mouse(grip(), 'dblclick', { clientX: 198 });
        // Asked again while the column's menu stands open: the same answer.
        pressBox(root, 0);
        expect(menu(root)).not.toBeNull();
        mouse(grip(), 'dblclick', { clientX: 198 });
        const fitted = metrics.headerRequiredPx({ label: 'Ccy', menuButton: true, sortable: true, display: 'compact' });
        expect(changes).toEqual([{ column: 'Ccy', widthPx: fitted }, { column: 'Ccy', widthPx: fitted }]);
    });
});

describe('changing the display on a grid in use', () => {
    it('ADR-0500: declared widths stand and no width is reported, and Auto widths are what each display gives a new grid, either way round', async () => {
        const changes: GridColumnWidthChange[] = [];
        const mixed: readonly GridColumn<Trade>[] = [{ name: 'Kept', type: 'text', value: () => 'x', width: 90 }, ...auto];
        const options: GridOptions<Trade> = { columns: mixed, rows: trades(30), ...box, onColumnWidthChanged: change => { changes.push(change); } };
        const { root, view } = mount<Trade>(options);
        await painted();
        const standard = widths(root);
        view.update({ ...options, headerDisplay: 'compact' });
        await painted();
        const compact = widths(root);
        expect(compact).toEqual(widths(mount<Trade>({ ...options, headerDisplay: 'compact' }).root));
        expect([compact[0], defined(standard[1], 'Ccy') - defined(compact[1], 'Ccy')]).toEqual([90, 28]);
        view.update({ ...options, headerDisplay: 'standard' });
        await painted();
        expect(widths(root)).toEqual(standard);
        expect(changes).toEqual([]);
    });

    it('ADR-0500 / ADR-0016: an Auto column keeps what its values taught it, and only its header\'s part follows the display', async () => {
        const learning: readonly GridColumn<Trade>[] = [
            { name: 'Ccy', type: 'text', value: () => 'USD' },
            { name: 'Note', type: 'text', value: trade => trade.book },
        ];
        const long = trades(30).map(trade => ({ ...trade, book: 'A desk whose name runs far past its header' }));
        const options: GridOptions<Trade> = { columns: learning, rows: long, ...box };
        const { root, view } = mount<Trade>(options);
        await painted();
        const [ccy, note] = widths(root);
        // The rows that grew Note leave the Window: an Auto width only ever grows, so it stands (ADR-0016).
        const short = trades(30);
        view.update({ ...options, rows: short });
        await painted();
        expect(widths(root)).toEqual([ccy, note]);
        // The display changes what a header needs and nothing a value needs: Ccy, which its header decides, gives
        // back the header's 28px, and Note is not measured down to the rows that happen to stand now.
        view.update({ ...options, rows: short, headerDisplay: 'compact' });
        await painted();
        expect(widths(root)).toEqual([defined(ccy, 'Ccy') - 28, note]);
        view.update({ ...options, rows: short });
        await painted();
        expect(widths(root)).toEqual([ccy, note]);
    });

    it('ADR-0500: the Sort, the Filter, the Selection, the scroll and the rows stand', async () => {
        const source = GridSource.from(trades(400));
        const selections: string[] = [];
        const options: GridOptions<Trade> = {
            columns: fixed, source, ...box,
            onSelectionChanged: selection => { selections.push(JSON.stringify(selection.ranges)); },
        };
        const { root, view } = mount<Trade>(options);
        source.setSorts([{ column: 'Notional', direction: 'descending' }]);
        source.setFilter({ columns: { Book: notBlank } });
        await painted();
        const scroller = q(root, '.ex-scroller');
        scroller.scrollTop = 1200;
        scroller.dispatchEvent(new Event('scroll'));
        await painted();
        mouse(q(root, '.ex-viewport'), 'mousedown', { offsetX: 130, offsetY: 1300, buttons: 1 });
        mouse(q(root, '.ex-viewport'), 'mouseup', { offsetX: 130, offsetY: 1300 });
        await painted();
        const heard = selections.length;
        const rows = (): (string | null)[] => [...root.querySelectorAll('.ex-viewport > .ex-row')].map(row => row.textContent);
        const before = { rows: rows(), focus: root.querySelector('.ex-focus')?.getAttribute('style'), state: source.snapshot() };

        view.update({ ...options, headerDisplay: 'compact' });
        await painted();
        expect(said(root)).toEqual(['none filtered button:Column menu, filtered', 'descending unfiltered button:Column menu']);
        expect({ rows: rows(), focus: root.querySelector('.ex-focus')?.getAttribute('style'), state: source.snapshot() }).toEqual(before);
        // The same snapshot object: the source was told nothing.
        expect(source.snapshot()).toBe(before.state);
        expect([scroller.scrollTop, selections.length]).toEqual([1200, heard]);
    });

    it('ADR-0500: an open edit keeps its text and the keyboard, and commits where it was opened', async () => {
        const edits: string[] = [];
        const options: GridOptions<Trade> = {
            columns: fixed, rows: trades(20), ...box, onEdit: intent => { edits.push(`${intent.column}=${intent.value} on ${intent.row.book}`); },
        };
        const { root, view } = mount<Trade>(options);
        await painted();
        // A press on the rows, as the browser lands it: the scroller takes DOM focus unless the grid prevents it.
        if (!mouse(q(root, '.ex-viewport'), 'mousedown', { offsetX: 10, offsetY: 53, buttons: 1 }).defaultPrevented) q(root, '.ex-scroller').focus();
        mouse(q(root, '.ex-viewport'), 'mouseup', { offsetX: 10, offsetY: 53 });
        await painted();
        key(present(document.activeElement, 'the Keyboard Field'), 'F2');
        await painted();
        const editor = q(root, 'input.ex-editor') as HTMLInputElement;
        editor.value = 'typed';
        editor.dispatchEvent(new Event('input', { bubbles: true }));

        view.update({ ...options, headerDisplay: 'compact' });
        await painted();
        expect(q(root, '.ex-header').getAttribute('data-ex-header-display')).toBe('compact');
        expect([q(root, 'input.ex-editor'), document.activeElement, editor.value]).toEqual([editor, editor, 'typed']);
        key(editor, 'Enter');
        await painted();
        expect(edits).toEqual(['Book=typed on Book-2']);
    });

    it('ADR-0500: a column menu standing open stays open on its column, with the keyboard where it was', async () => {
        const options: GridOptions<Trade> = { columns: fixed, rows: trades(20), ...box };
        const { root, view } = mount<Trade>(options);
        await painted();
        pressBox(root, 1);
        const held = document.activeElement;
        view.update({ ...options, headerDisplay: 'compact' });
        await painted();
        expect([menu(root)?.getAttribute('aria-label'), document.activeElement]).toEqual(['Notional', held]);
        // Its ▾, built anew with the header, still closes it as a toggle.
        pressBox(root, 1);
        expect([menu(root), document.activeElement]).toEqual([null, root]);
    });
});
