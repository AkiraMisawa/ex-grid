// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): a Header Group's toggle (ADR-0400). The grid draws the toggle and notifies; the
// Consumer pushes the columns that open or close; the Focus survives the push by name (ADR-0011). The declaration's
// refusals and the group a key picks are layer 1's (tests/cases/columns/header-group-toggle*.json).
import { afterEach, describe, expect, it } from 'vitest';
import type { HeaderGroupToggle } from '../src/columns/header-groups';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridHeaderGroup, GridOptions } from '../src/dom/grid-view';
import { GridSource } from '../src/source/grid-source';
import { cell } from '../src/selection/cell';
import { selectionRange } from '../src/selection/range';
import { mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

const book: GridColumn<Trade> = { name: 'Book', type: 'text', value: trade => trade.book, width: 110 };
const rate: GridColumn<Trade> = { name: 'Rate', type: 'number', value: trade => trade.notional / 1000, width: 100 };
const dayCount: GridColumn<Trade> = { name: 'DayCount', type: 'text', value: () => 'ACT/360', width: 100 };
const calendar: GridColumn<Trade> = { name: 'Calendar', type: 'text', value: () => 'TARGET', width: 100 };

const open = [book, rate, dayCount, calendar] as const;
const closed = [book, rate] as const;

const groupOf = (expanded: boolean, extra: Partial<GridHeaderGroup> = {}): GridHeaderGroup => ({
    label: 'Leg 1', columns: expanded ? ['Rate', 'DayCount', 'Calendar'] : ['Rate'], id: 'leg1', expanded, ...extra,
});

const base = { rowHeight: 24, headerHeight: 28, viewportHeight: 320, viewportWidth: 720 } as const;

/** A Consumer that holds the group's state and answers a toggle when asked to, or at once. */
function consumer(options: { readonly rows?: readonly Trade[]; readonly source?: GridSource<Trade>; readonly answerAtOnce?: boolean } = {}) {
    const heard: HeaderGroupToggle[] = [];
    let expanded = true;
    const rows = options.rows ?? trades(20);
    const optionsFor = (): GridOptions<Trade> => ({
        ...base,
        ...(options.source === undefined ? { rows } : { source: options.source }),
        columns: expanded ? open : closed,
        headerGroups: [groupOf(expanded)],
        onHeaderGroupToggled: toggle => {
            heard.push(toggle);
            if (options.answerAtOnce === true) answer(toggle);
        },
    });
    const mounted = mount(optionsFor());
    function answer(toggle: HeaderGroupToggle): void {
        expanded = toggle.expanded;
        mounted.view.update(optionsFor());
    }
    return { ...mounted, heard, answer, names: () => [...mounted.root.querySelectorAll('.ex-header-cell')].map(header => header.textContent) };
}

function click(target: Element): void {
    for (const type of ['mousedown', 'mouseup', 'click']) {
        target.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 }));
    }
}

/** An event on the header at its own offsets, which is what the grid reads (ADR-0012). */
function onHeader(root: HTMLElement, type: string, x: number, y: number): void {
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, clientX: x, clientY: y });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    q(root, '.ex-header').dispatchEvent(event);
}

function groupKey(root: HTMLElement, key: 'ArrowDown' | 'ArrowUp'): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key, shiftKey: true, altKey: true, bubbles: true, cancelable: true });
    root.dispatchEvent(event);
    return event;
}

describe('a Header Group\'s toggle', () => {
    it('ADR-0400 / HG-18: a group declared expanded draws a named toggle at its left; one given none draws none', async () => {
        const { root } = mount({
            ...base, rows: trades(5), columns: open, onHeaderGroupToggled: () => undefined,
            headerGroups: [groupOf(true), { label: 'Who', columns: ['Book'] }],
        });
        await painted();
        const toggles = [...root.querySelectorAll('.ex-header-group-toggle')];
        expect(toggles.map(toggle => [toggle.parentElement?.firstChild === toggle, toggle.getAttribute('aria-expanded'),
            toggle.getAttribute('aria-label'), toggle.getAttribute('tabindex'), toggle.getAttribute('data-ex-claims')]))
            .toEqual([[true, 'true', 'Collapse Leg 1', '-1', 'mousedown click']]);
        // The group's own text stays its label, and the toggle's box is a Geometry Token the stylesheet reads.
        expect(toggles[0]?.parentElement?.textContent).toBe('Leg 1');
        // Named by its label alone, not by the toggle's words beside it (ADR-0400 section 8).
        expect(toggles[0]?.parentElement?.getAttribute('aria-label')).toBe('Leg 1');
        expect(root.getAttribute('style')).toContain('--ex-header-group-toggle-width: 20px');
    });

    it('ADR-0400 section 8: the toggle\'s name is worded through commandLabel', async () => {
        const { root } = mount({
            ...base, rows: trades(5), columns: closed, onHeaderGroupToggled: () => undefined, headerGroups: [groupOf(false)],
            commandLabel: id => (id === 'header-group-expand' ? '{label} を展開' : undefined),
        });
        await painted();
        expect(q(root, '.ex-header-group-toggle').getAttribute('aria-label')).toBe('Leg 1 を展開');
    });

    it('ADR-0400 section 8: a commandLabel handed over later words the toggle again, the groups unchanged', async () => {
        const options: GridOptions<Trade> = { ...base, rows: trades(5), columns: closed, onHeaderGroupToggled: () => undefined, headerGroups: [groupOf(false)] };
        const { root, view } = mount(options);
        await painted();
        expect(q(root, '.ex-header-group-toggle').getAttribute('aria-label')).toBe('Expand Leg 1');
        view.update({ ...options, commandLabel: id => (id === 'header-group-expand' ? '{label} を展開' : undefined) });
        await painted();
        expect(q(root, '.ex-header-group-toggle').getAttribute('aria-label')).toBe('Leg 1 を展開');
    });

    it('ADR-0400 / HG-18: a press notifies the opposite of the state shown, and the columns stay the ones pushed', async () => {
        const { root, heard, names } = consumer();
        await painted();
        click(q(root, '.ex-header-group-toggle'));
        await painted();
        expect(heard).toEqual([{ id: 'leg1', expanded: false }]);
        expect(names()).toEqual(['Book', 'Rate', 'DayCount', 'Calendar']);
    });

    it('ADR-0400 / HG-20: a press and release on the group off the toggle never toggles', async () => {
        const { root, heard } = consumer();
        await painted();
        // On the rectangle over Rate, right of the toggle's box: the group's own press, as before.
        onHeader(root, 'mousedown', 200, 10);
        onHeader(root, 'mouseup', 200, 10);
        onHeader(root, 'click', 200, 10);
        await painted();
        expect(heard).toEqual([]);
    });

    it('ADR-0400 / HG-22: two presses before the Consumer answers notify the same value twice, and both answers land as one', async () => {
        const { root, heard, answer, names } = consumer();
        await painted();
        click(q(root, '.ex-header-group-toggle'));
        click(q(root, '.ex-header-group-toggle'));
        await painted();
        expect(heard).toEqual([{ id: 'leg1', expanded: false }, { id: 'leg1', expanded: false }]);
        for (const toggle of heard) answer(toggle);
        await painted();
        expect(names()).toEqual(['Book', 'Rate']);
        expect(q(root, '.ex-header-group-toggle').getAttribute('aria-expanded')).toBe('false');
    });

    it('ADR-0400 / HG-21: Alt+Shift+Up collapses the group over the Focus\'s column and Alt+Shift+Down reopens it', async () => {
        const { root, view, heard } = consumer({ answerAtOnce: true });
        await painted();
        view.placeSelection(selectionRange(2, 2, 1, 1), cell(2, 2), 0);
        await painted();
        expect(groupKey(root, 'ArrowUp').defaultPrevented).toBe(true);
        await painted();
        expect(groupKey(root, 'ArrowDown').defaultPrevented).toBe(true);
        await painted();
        expect(heard).toEqual([{ id: 'leg1', expanded: false, focusColumn: 'DayCount' }, { id: 'leg1', expanded: true, focusColumn: 'Rate' }]);
    });

    it('ADR-0400 / HG-21: where no group over the Focus\'s column answers, the key reaches the page', async () => {
        const { root, view, heard } = consumer();
        await painted();
        view.placeSelection(selectionRange(0, 0, 1, 1), cell(0, 0), 0);
        await painted();
        // Book is under no group, and Rate's group is open, so there is nothing to expand.
        expect(groupKey(root, 'ArrowDown').defaultPrevented).toBe(false);
        view.placeSelection(selectionRange(0, 1, 1, 1), cell(0, 1), 0);
        await painted();
        expect(groupKey(root, 'ArrowDown').defaultPrevented).toBe(false);
        expect(heard).toEqual([]);
    });

    it('ADR-0400 / HG-21: on a grid not given onHeaderGroupToggled, the keys are the page\'s and declarable; beside it, refused', async () => {
        const { root } = mount({ ...base, rows: trades(5), columns: open, declaredKeys: ['Shift+Alt+ArrowDown'], onDeclaredKey: () => undefined });
        await painted();
        expect(groupKey(root, 'ArrowUp').defaultPrevented).toBe(false);
        expect(() => mount({
            ...base, rows: trades(5), columns: open, declaredKeys: ['Shift+Alt+ArrowUp'], onDeclaredKey: () => undefined,
            onHeaderGroupToggled: () => undefined,
        })).toThrow(/declaredKeys names Shift\+Alt\+ArrowUp, which opens or closes a Header Group/u);
    });

    it('ADR-0400 / HG-19: a group declared expanded on a grid nobody hears a toggle on is refused, naming the group', () => {
        expect(() => mount({ ...base, rows: trades(5), columns: open, headerGroups: [groupOf(true)] }))
            .toThrow(/Header Group 'Leg 1' is declared expanded or collapsed, but no onHeaderGroupToggled/u);
    });

    it('ADR-0400 / HG-19: taking onHeaderGroupToggled away from a grid that keeps its toggles is refused, and the grid stays', async () => {
        const options: GridOptions<Trade> = { ...base, rows: trades(5), columns: open, headerGroups: [groupOf(true)], onHeaderGroupToggled: () => undefined };
        const { root, view } = mount(options);
        await painted();
        const { onHeaderGroupToggled: _, ...without } = options;
        expect(() => { view.update(without); }).toThrow(/no onHeaderGroupToggled/u);
        expect(root.querySelector('.ex-header-group-toggle')).not.toBeNull();
    });
});

describe('the Focus across a toggle (ADR-0011, ADR-0400 section 9)', () => {
    it('ADR-0400 / HG-23: a collapse that hides the Focus\'s column puts the Focus on the group\'s first member, same row', async () => {
        const { root, view, heard, answer } = consumer();
        await painted();
        view.placeSelection(selectionRange(1, 1, 3, 3), cell(3, 3), 0);
        await painted();
        groupKey(root, 'ArrowUp');
        await painted();
        answer(heard[0] as HeaderGroupToggle);
        await painted();
        const { selection } = view.readView();
        expect([selection.focus, selection.ranges]).toEqual([cell(3, 1), [selectionRange(3, 1, 1, 1)]]);
    });

    it('ADR-0400 / HG-23: an expand keeps the Focus in its column by name, wherever the push puts it', async () => {
        const { root, view, heard } = consumer({ answerAtOnce: true });
        await painted();
        groupKey(root, 'ArrowUp');
        await painted();
        expect(heard).toEqual([]);
        // Collapse with no Focus: nothing is remembered, and nothing comes back.
        click(q(root, '.ex-header-group-toggle'));
        await painted();
        expect(view.readView().selection.focus).toBeUndefined();
        view.placeSelection(selectionRange(4, 1, 1, 1), cell(4, 1), 0);
        await painted();
        groupKey(root, 'ArrowDown');
        await painted();
        expect(heard.at(-1)).toEqual({ id: 'leg1', expanded: true, focusColumn: 'Rate' });
        expect(view.readView().selection.focus).toEqual(cell(4, 1));
    });

    it('ADR-0400 / HG-23: with a Row Key the Focus follows its row through an order that moved with the push', async () => {
        const rows = trades(10);
        const source = GridSource.from(rows, { rowKey: trade => trade.book });
        const { root, view, heard, answer } = consumer({ source });
        await painted();
        view.placeSelection(selectionRange(2, 2, 1, 1), cell(2, 2), source.snapshot().rowSequenceVersion);
        await painted();
        groupKey(root, 'ArrowUp');
        await painted();
        // The rows are sorted descending before the columns arrive: Book-2 now stands at 7 of Book-9 to Book-0.
        source.setSorts([{ column: 'Book', direction: 'descending' }]);
        answer(heard[0] as HeaderGroupToggle);
        await painted();
        expect(view.readView().selection.focus).toEqual(cell(7, 1));
    });

    it('ADR-0400 / HG-23: with a Row Key, a Focus row the Window no longer holds comes back by position while the order holds', async () => {
        const rows = trades(200);
        const inner = GridSource.from(rows);
        const listeners = new Set<() => void>();
        let windowStart = 0;
        // A source that holds a Window of 20 rows, as a fetching one does, moved by hand.
        const source: GridSource<Trade> = {
            subscribe: listener => { listeners.add(listener); return () => { listeners.delete(listener); }; },
            snapshot: () => ({ ...inner.snapshot(), window: rows.slice(windowStart, windowStart + 20), windowStart, totalCount: rows.length }),
            setColumns: columns => { inner.setColumns(columns); },
            setSorts: sorts => { inner.setSorts(sorts); },
            setFilter: filter => { inner.setFilter(filter); },
            needRange: () => Promise.resolve(),
            rows: range => Promise.resolve(rows.slice(range.start, range.start + range.count)),
            distinctValues: () => Promise.resolve({ kind: 'tooMany' }),
            rowKey: trade => trade.book,
        };
        const { root, view, heard, answer } = consumer({ source });
        await painted();
        const version = source.snapshot().rowSequenceVersion;
        view.placeSelection(selectionRange(2, 2, 1, 1), cell(2, 2), version);
        await painted();
        windowStart = 150;
        for (const listener of listeners) listener();
        await painted();
        groupKey(root, 'ArrowUp');
        await painted();
        answer(heard[0] as HeaderGroupToggle);
        await painted();
        expect(view.readView().selection.focus).toEqual(cell(2, 1));
    });

    it('ADR-0400 / HG-23: without a Row Key, an order that moved before the push leaves no Focus', async () => {
        const source = GridSource.from(trades(10));
        const { root, view, heard, answer } = consumer({ source });
        await painted();
        view.placeSelection(selectionRange(2, 2, 1, 1), cell(2, 2), source.snapshot().rowSequenceVersion);
        await painted();
        groupKey(root, 'ArrowUp');
        await painted();
        source.setSorts([{ column: 'Book', direction: 'descending' }]);
        answer(heard[0] as HeaderGroupToggle);
        await painted();
        expect(view.readView().selection.focus).toBeUndefined();
    });

    it('ADR-0400 / HG-23: a Focus placed by the user before the answer is let go: the answer drops the Focus as before', async () => {
        const { root, view, heard, answer } = consumer();
        await painted();
        view.placeSelection(selectionRange(2, 2, 1, 1), cell(2, 2), 0);
        await painted();
        groupKey(root, 'ArrowUp');
        await painted();
        view.placeSelection(selectionRange(5, 3, 1, 1), cell(5, 3), 0);
        await painted();
        answer(heard[0] as HeaderGroupToggle);
        await painted();
        expect(view.readView().selection.focus).toBeUndefined();
    });

    it('ADR-0400 / HG-23: a push that does not show the group as asked drops the Focus, and a later one brings nothing back', async () => {
        const { root, view, heard, answer } = consumer();
        await painted();
        view.placeSelection(selectionRange(2, 2, 1, 1), cell(2, 2), 0);
        await painted();
        groupKey(root, 'ArrowUp');
        await painted();
        // The Consumer refuses the collapse, but changes the columns' widths, a columns push all the same.
        view.update({
            ...base, rows: trades(20), columns: open.map(column => ({ ...column })), headerGroups: [groupOf(true)],
            onHeaderGroupToggled: toggle => { heard.push(toggle); },
        });
        await painted();
        expect(view.readView().selection.focus).toBeUndefined();
        answer(heard[0] as HeaderGroupToggle);
        await painted();
        expect(view.readView().selection.focus).toBeUndefined();
    });
});

describe('the Hidden Mark (ADR-0400 section 6)', () => {
    it.each(['stale', 'missing', 'error', 'modified'] as const)('ADR-0400 / HG-24: a collapsed group wears its %s mark, the reason its description', async state => {
        const { root } = mount({
            ...base, rows: trades(5), columns: closed, onHeaderGroupToggled: () => undefined,
            headerGroups: [groupOf(false, { hiddenMark: { state, reason: '2 user-set fields in hidden columns' } })],
        });
        await painted();
        const group = q(root, '.ex-header-group');
        const mark = q(group, '.ex-hidden-mark');
        expect([mark.className, mark.title, mark.getAttribute('aria-hidden'), group.getAttribute('aria-description'), group.textContent])
            .toEqual([`ex-hidden-mark ex-hidden-mark-${state}`, '2 user-set fields in hidden columns', 'true', '2 user-set fields in hidden columns', 'Leg 1']);
    });

    it('ADR-0400: a mark taken away by the Consumer goes from the header', async () => {
        const options: GridOptions<Trade> = {
            ...base, rows: trades(5), columns: closed, onHeaderGroupToggled: () => undefined,
            headerGroups: [groupOf(false, { hiddenMark: { state: 'modified', reason: 'set' } })],
        };
        const { root, view } = mount(options);
        await painted();
        view.update({ ...options, headerGroups: [groupOf(false)] });
        await painted();
        expect([root.querySelector('.ex-hidden-mark'), q(root, '.ex-header-group').getAttribute('aria-description')]).toEqual([null, null]);
    });
});
