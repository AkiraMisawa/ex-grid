// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the grid in a DOM emulation. Which rows the core creates, that a row
// whose values did not change is not touched (ADR-0003), and the DOM contract the C# core writes, which
// ex-grid.css and layer 3 read (ADR-0350 section 4). What the browser tells — the gutter, the ceiling,
// the Device Pixel — is told here by hand.
import { describe, expect, it } from 'vitest';
import { elementAt } from '../src/must';
import { GridView } from '../src/dom/grid-view';
import { stylesheetStamp } from '../src/dom/shipped-stylesheet';
import { GridSource } from '../src/source/grid-source';
import type { GridPage, GridQuery } from '../src/source/fetching-source';
import { columns, mount, painted, q, rowsOf, scrollTo, Told, trades, type Trade } from './grid-harness';

const noticeWords = (root: HTMLElement): string | null => q(root, '[data-ex-notice="stylesheet"]').textContent;

/** Starts hearing what is written under the root; the function returned stops, and gives every node written to,
 * the records still queued included. */
function watchWrites(root: HTMLElement): () => Node[] {
    const written: Node[] = [];
    const observer = new MutationObserver(records => { for (const record of records) written.push(record.target); });
    observer.observe(root, { subtree: true, childList: true, attributes: true, characterData: true });
    return () => {
        for (const record of observer.takeRecords()) written.push(record.target);
        observer.disconnect();
        return written;
    };
}

describe('createGrid', () => {
    it('ADR-0123: the stylesheet\'s notice is worded by commandLabel, at creation and on an update that changes it', () => {
        const { root, view } = mount({ columns, rows: trades(10), rowHeight: 24, viewportHeight: 480, commandLabel: id => (id === 'reload-notice-stylesheet' ? 'Reload.' : undefined) });
        expect(noticeWords(root)).toBe('Reload.');
        view.update({ columns, rows: trades(10), rowHeight: 24, viewportHeight: 480 });
        expect(noticeWords(root)).toBe('This page holds another version\'s stylesheet. Reload the page.');
    });

    it('ADR-0123 ERR-11: a Chrome\'s stylesheets each have a notice beside the grid\'s own, drawn again when the list changes', () => {
        const stamp = 'a'.repeat(64);
        const chrome = { stylesheets: [{ name: 'mud-ex-grid.css', stamp }] };
        const { root, view } = mount({ columns, rows: trades(10), rowHeight: 24, viewportHeight: 480, chrome });
        const notices = (): (string | null)[][] => [...root.querySelectorAll(':scope > [data-ex-notice="stylesheet"]')]
            .map(notice => [notice.getAttribute('data-ex-stylesheet'), notice.getAttribute('data-ex-stamp'), notice.textContent]);
        const words = 'This page holds another version\'s stylesheet. Reload the page.';
        expect(notices()).toEqual([['ex-grid.css', expect.any(String), words], ['mud-ex-grid.css', stamp, words]]);
        view.update({ columns, rows: trades(10), rowHeight: 24, viewportHeight: 480, chrome });
        expect(notices()).toHaveLength(2);
        view.update({ columns, rows: trades(10), rowHeight: 24, viewportHeight: 480 });
        expect(notices().map(([name]) => name)).toEqual(['ex-grid.css']);
        const unstamped: unknown = { name: 'x.css' };
        expect(() => mount({ columns, rows: trades(1), rowHeight: 24, viewportHeight: 480, chrome: { stylesheets: [unstamped as never] } }))
            .toThrow(/Each of chrome.stylesheets is \{ name, stamp \}/);
    });

    it('ADR-0350: the root, the scroller, the header and the rows are the C# core\'s DOM contract', async () => {
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(root.className).toBe('ex-grid');
        expect(root.getAttribute('style')).toBe('--ex-row-height: 24px; --ex-header-height: 24px; --ex-font-size: 14px; '
            + '--ex-cell-padding-x: 8px; --ex-action-padding-x: 6px; --ex-action-border-width: 1px; --ex-action-gap: 4px; '
            + '--ex-menu-button-width: 16px; --ex-menu-button-inset: 6px; --ex-sort-mark-width: 20px; --ex-resize-grip-width: 5px; '
            // A Header Group's toggle's box is the Script Core's alone: the C# core never draws a toggle (ADR-0400 section 10).
            + '--ex-column-guide-width: 2px; --ex-header-group-toggle-width: 20px; --ex-dp: 1px');
        expect(Object.fromEntries(['dir', 'role', 'aria-multiselectable', 'aria-rowcount', 'aria-colcount', 'tabindex']
            .map(name => [name, root.getAttribute(name)]))).toEqual({
            dir: 'ltr', role: 'grid', 'aria-multiselectable': 'true', 'aria-rowcount': '1001', 'aria-colcount': '2', tabindex: '0',
        });
        expect(root.id).toMatch(/^ex\d+-root$/);
        // The status line stands after the rows whenever the grid can summarise, as this one's source does (ADR-0130).
        expect([...root.children].map(child => (child.className === '' ? child.tagName : child.className)))
            .toEqual(['SPAN', 'DIV', 'ex-scroller', 'ex-status', 'ex-announce', 'ex-ceiling-probe']);
        expect(q(root, '[data-ex-notice]').getAttribute('data-ex-stamp')).toBe(stylesheetStamp);
        expect(q(root, '.ex-scroller').getAttribute('style')).toBe('width: 800px; height: 480px');
        expect(q(root, '.ex-spacer').getAttribute('style')).toBe(`width: 220px; height: ${24 + (1000 * 24)}px`);
        const headers = [...root.querySelectorAll('.ex-header > .ex-header-cell')];
        expect(headers.map(cell => [cell.textContent, cell.getAttribute('style'), cell.getAttribute('aria-colindex'), cell.getAttribute('aria-sort')]))
            .toEqual([['Book', 'width: 120px', '1', 'none'], ['Notional', 'width: 100px', '2', 'none']]);
        // (480 − the header's 24) / 24 rows fit, and one more straddles the bottom edge.
        const rows = rowsOf(root);
        expect(rows).toHaveLength(20);
        const first = elementAt(rows, 0);
        expect(first.getAttribute('aria-rowindex')).toBe('2');
        const cells = [...first.children];
        expect(cells.map(cell => [cell.className, cell.textContent, cell.id.replace(/^ex\d+-/, '')]))
            .toEqual([['ex-cell', 'Book-0', 'r0c0'], ['ex-cell ex-cell-numeric', '0', 'r0c1']]);
        expect(q(root, '.ex-viewport').getAttribute('data-ex-first-row')).toBe('0');
    });

    it('ADR-0003: a row that stays on screen is not painted again when the Viewport moves one row', async () => {
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480 });
        await painted();
        const before = rowsOf(root);
        await scrollTo(root, 24);
        const after = rowsOf(root);
        expect(after.map(row => row.getAttribute('aria-rowindex'))).toEqual(Array.from({ length: 20 }, (_, i) => String(i + 3)));
        // Every row but the one that left is the same element, untouched.
        expect(after.slice(0, 19)).toEqual(before.slice(1));
        expect(after.filter(row => !before.includes(row))).toHaveLength(1);
    });

    it('ADR-0038 RR-13: with Row Stripes on, a row that stays on screen is not touched when the Viewport moves one row', async () => {
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, stripeRows: true });
        await painted();
        const before = rowsOf(root);
        const watch = watchWrites(root);
        await scrollTo(root, 24);
        const written = watch();
        const after = rowsOf(root);
        // The same elements, not equal ones: every row but the one that left stayed.
        const surviving = after.slice(0, 19);
        expect(surviving.every((row, i) => row === before[i + 1])).toBe(true);
        // The stripe is counted by the row's place in the result, so it stays with its row: nothing under a row
        // that stayed was written, its stripe included.
        expect(written.filter(node => surviving.some(row => row.contains(node)))).toEqual([]);
        expect(surviving.map(row => row.classList.contains('ex-row-stripe')))
            .toEqual(surviving.map(row => Number(row.getAttribute('aria-rowindex')) % 2 === 1));
    });

    it('ADR-0018 MEM-4: destroying the grid removes every listener on its root and its Input Root, and stops every report it asked for', async () => {
        const inputRoot = document.createElement('div');
        inputRoot.setAttribute('data-ex-input-root', '');
        const root = document.createElement('div');
        inputRoot.append(root);
        document.body.append(inputRoot);
        // Each listener by its function, type and phase, as the DOM tells one from another; removing one never added
        // is harmless, and a grid may let go of what it holds only sometimes.
        const listening = new Set<string>();
        const ids = new Map<EventListenerOrEventListenerObject, number>();
        const key = (where: string, type: string, listener: EventListenerOrEventListenerObject, options?: boolean | EventListenerOptions): string => {
            if (!ids.has(listener)) ids.set(listener, ids.size);
            return `${where} ${type} ${String(typeof options === 'boolean' ? options : options?.capture === true)} ${String(ids.get(listener))}`;
        };
        for (const [where, target] of [['root', root], ['input root', inputRoot]] as const) {
            const add = target.addEventListener.bind(target);
            const remove = target.removeEventListener.bind(target);
            target.addEventListener = (type: string, listener: EventListenerOrEventListenerObject, options?: boolean | AddEventListenerOptions) => {
                listening.add(key(where, type, listener, options));
                add(type, listener, options);
            };
            target.removeEventListener = (type: string, listener: EventListenerOrEventListenerObject, options?: boolean | EventListenerOptions) => {
                listening.delete(key(where, type, listener, options));
                remove(type, listener, options);
            };
        }
        const reports = new Set<string>();
        const counted = <Stop extends (() => void) | undefined>(name: string, stop: Stop): Stop => {
            if (stop === undefined) return stop;
            reports.add(name);
            return (() => { reports.delete(name); stop(); }) as Stop;
        };
        class Counted extends Told {
            override observeViewport(scroller: HTMLElement, tell: Parameters<Told['observeViewport']>[1]): () => void {
                return counted('viewport', super.observeViewport(scroller, tell));
            }
            override observeCeiling(probe: HTMLElement, tell: (px: number) => void): () => void {
                return counted('ceiling', super.observeCeiling(probe, tell));
            }
            override observeDevicePixel(tell: (ratio: number) => void): () => void {
                return counted('device pixel', super.observeDevicePixel(tell));
            }
            override observeClipboard(element: HTMLElement, tell: (change: object) => void): (() => void) | undefined {
                return counted('clipboard', super.observeClipboard(element, tell));
            }
        }
        const view = new GridView(root, { columns, rows: trades(10), rowHeight: 24, viewportHeight: 480 }, new Counted());
        try {
            await painted();
            expect([...listening].some(entry => entry.startsWith('root keydown true ')), 'the capture-phase key listener is on the root').toBe(true);
            expect([...listening].some(entry => entry.startsWith('input root ')), 'the Input Root hears the input the grid holds').toBe(true);
            expect([...reports].toSorted()).toEqual(['ceiling', 'clipboard', 'device pixel', 'viewport']);
        } finally {
            view.destroy();
            inputRoot.remove();
        }
        expect([...listening]).toEqual([]);
        expect([...reports]).toEqual([]);
    });

    it('ADR-0003 RR-5: a new array of the same rows writes nothing; a column object made anew paints every row again', async () => {
        const rows = trades(100);
        const { root, view } = mount({ columns, rows, rowHeight: 24, viewportHeight: 480 });
        await painted();
        const before = rowsOf(root).map(row => [...row.children]);
        const watch = watchWrites(root);
        view.update({ columns, rows: [...rows], rowHeight: 24, viewportHeight: 480 });
        await painted();
        const written = watch();
        // The same cell elements, and nothing written under any row.
        expect(rowsOf(root).map(row => [...row.children]).every((cells, i) => cells.every((cell, j) => cell === before[i]?.[j]))).toBe(true);
        expect(written.filter(node => rowsOf(root).some(row => row.contains(node)))).toEqual([]);
        // Equal in every field, and still another column: the instance is the signal, never a comparison of what
        // a column holds, which would call two formats that capture alike the same (ADR-0003).
        view.update({ columns: columns.map(column => ({ ...column })), rows, rowHeight: 24, viewportHeight: 480 });
        await painted();
        const after = rowsOf(root).map(row => [...row.children]);
        expect(after).toHaveLength(before.length);
        expect(after.every((cells, i) => cells.every((cell, j) => cell !== before[i]?.[j]))).toBe(true);
    });

    it('ADR-0003: a pass with nothing changed writes nothing', async () => {
        const rows = trades(100);
        const { root, view } = mount({ columns, rows, rowHeight: 24, viewportHeight: 480 });
        await painted();
        const written: string[] = [];
        const observer = new MutationObserver(records => {
            for (const record of records) written.push(record.attributeName === null ? record.type : `${record.type} ${record.attributeName}`);
        });
        observer.observe(root, { subtree: true, childList: true, attributes: true, characterData: true });
        // A new array of the same columns, as a Framework Adapter hands on every render, is no change.
        view.update({ columns: [...columns], rows, rowHeight: 24, viewportHeight: 480 });
        await painted();
        for (const record of observer.takeRecords()) written.push(record.attributeName === null ? record.type : `${record.type} ${record.attributeName}`);
        observer.disconnect();
        // The Settled Mark goes off while the pass is owed, and comes back with it (ADR-0220); nothing else moves.
        expect(written.filter(record => record !== 'attributes data-ex-settled')).toEqual([]);
        expect(root.hasAttribute('data-ex-settled')).toBe(true);
    });

    it('ADR-0004: a fling paints Placeholders with the pinned cells alone until scrolling stands still', async () => {
        const { root, told } = mount({ columns, rows: trades(10_000), rowHeight: 24, viewportHeight: 480, pinnedColumnCount: 1 });
        await painted();
        await scrollTo(root, 24 * 5000);
        const flung = rowsOf(root);
        expect(flung.every(row => row.classList.contains('ex-placeholder') && row.getAttribute('aria-busy') === 'true')).toBe(true);
        expect(flung.map(row => row.children.length)).toEqual(flung.map(() => 1));
        expect(root.hasAttribute('data-ex-settled')).toBe(false);
        told.clock.advance(149);
        await painted();
        expect(elementAt(rowsOf(root), 0).classList.contains('ex-placeholder')).toBe(true);
        told.clock.advance(1);
        await painted();
        const landed = rowsOf(root);
        expect(landed.some(row => row.classList.contains('ex-placeholder'))).toBe(false);
        expect(elementAt(landed, 0).textContent).toBe('Book-50005000000');
        expect(root.hasAttribute('data-ex-settled')).toBe(true);
    });

    it('ADR-0220: the grid is settled only once the browser has told the Viewport and the ceiling', async () => {
        const root = document.createElement('div');
        document.body.append(root);
        const told = new Told();
        const view = new GridView(root, { columns, rows: trades(10) }, told);
        await painted();
        expect(root.hasAttribute('data-ex-settled')).toBe(false);
        told.viewport();
        await painted();
        expect(root.hasAttribute('data-ex-settled')).toBe(false);
        told.ceiling();
        await painted();
        expect(root.hasAttribute('data-ex-settled')).toBe(true);
        view.destroy();
        expect(told.listening).toBe(false);
        expect(root.childElementCount).toBe(0);
    });

    it('ADR-0220 ADR-0013: scroll content written is laid out and its box told before the grid is settled', async () => {
        const root = document.createElement('div');
        document.body.append(root);
        const told = new Told();
        told.holdLayout = true;
        const view = new GridView(root, { columns, rows: trades(10), rowHeight: 24, viewportHeight: 480 }, told);
        told.viewport();
        told.ceiling();
        await painted();
        // The rows' content is written: overflowing, it would show a scrollbar, told in the frame that lays it out.
        expect(root.hasAttribute('data-ex-settled')).toBe(false);
        told.laidOut();
        expect(root.hasAttribute('data-ex-settled')).toBe(true);

        // More rows lengthen the content, which is laid out again; the same content written again owes nothing.
        view.update({ columns, rows: trades(100), rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(root.hasAttribute('data-ex-settled')).toBe(false);
        told.viewport({ gutterWidthPx: 12, contentWidthPx: 788 });
        told.laidOut();
        await painted();
        expect(root.hasAttribute('data-ex-settled')).toBe(true);
        view.destroy();
    });

    it('ADR-0013: the Scrollbar Gutter comes out of the declared Viewport before the rows are counted', async () => {
        const { root, told } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(rowsOf(root)).toHaveLength(20);
        told.viewport({ gutterHeightPx: 15, contentHeightPx: 465 });
        await painted();
        // (465 − 24) / 24 rounds up to the same 19 rows and the one straddling the edge; a taller gutter
        // takes one away.
        expect(rowsOf(root)).toHaveLength(20);
        told.viewport({ gutterHeightPx: 30, contentHeightPx: 450 });
        await painted();
        expect(rowsOf(root)).toHaveLength(19);
    });

    it('ADR-0028 (VZ-12b): a stretched height as tall as its header band is heard once, until a real height arrives', async () => {
        let heard = 0;
        const { root, told } = mount({
            columns, rows: trades(100), rowHeight: 24, headerHeight: 32, viewportHeight: 'stretch',
            onParentWithoutHeight: () => { heard++; },
        });
        await painted();
        told.viewport({ contentHeightPx: 0 });
        expect(heard).toBe(0);
        told.viewport({ contentHeightPx: 32 });
        told.viewport({ contentHeightPx: 32, contentWidthPx: 700 });
        await painted();
        expect(heard).toBe(1);
        expect(rowsOf(root)).toHaveLength(0);
        told.viewport({ contentHeightPx: 320 });
        await painted();
        expect(rowsOf(root).length).toBeGreaterThan(0);
        told.viewport({ contentHeightPx: 32 });
        expect(heard).toBe(2);
    });

    it('ADR-0016: an Auto column grows to its widest painted value, and a number too wide for a Fixed one is ####', async () => {
        const rows = [{ book: 'A', notional: 123_456_789_012_345 }, { book: 'A much longer book name', notional: 1 }];
        const { root } = mount<Trade>({
            columns: [
                { name: 'Book', type: 'text', value: trade => trade.book },
                { name: 'Notional', type: 'number', value: trade => trade.notional, width: 60 },
            ],
            rows,
            rowHeight: 24,
            viewportHeight: 480,
        });
        await painted();
        const book = q(root, '.ex-header-cell');
        expect(Number(/width: ([\d.]+)px/.exec(String(book.getAttribute('style')))?.[1])).toBeGreaterThan(150);
        const hashed = q(root, `[id$='-r0c1']`);
        expect(hashed.querySelector('.ex-hashes')?.textContent).toMatch(/^#+$/);
        expect(hashed.getAttribute('aria-label')).toBe('123456789012345');
        expect(q(root, `[id$='-r1c1']`).textContent).toBe('1');
    });

    it('ADR-0001: a position the Window does not hold is a Placeholder, and its range is asked for once', async () => {
        const asked: GridQuery[] = [];
        const answers: ((page: GridPage<Trade>) => void)[] = [];
        const source = GridSource.fetch<Trade>(query => new Promise(resolve => {
            asked.push(query);
            answers.push(resolve);
        }), { readAheadRows: 0 });
        const { root } = mount({ columns, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(asked.map(({ range }) => [range.start, range.count])).toEqual([[0, 100]]);
        elementAt(answers, 0)({ rows: trades(10), start: 0, totalCount: 5000 });
        await new Promise(resolve => { setTimeout(resolve, 0); });
        const rows = rowsOf(root);
        expect(rows).toHaveLength(20);
        expect(rows.slice(10).every(row => row.classList.contains('ex-placeholder') && row.childElementCount === 0)).toBe(true);
        expect(root.getAttribute('aria-rowcount')).toBe('5001');
        // The rows on screen the Window missed, asked once however often the grid paints.
        expect(asked.map(({ range }) => [range.start, range.count])).toEqual([[0, 100], [0, 20]]);
        // A position still waiting keeps its Placeholder: a pass paints nothing for it (ADR-0003).
        const paint = q(root, '.ex-viewport').getAttribute('data-ex-paint');
        await scrollTo(root, 0, 1);
        expect(asked).toHaveLength(2);
        expect(rowsOf(root)).toEqual(rows);
        expect(q(root, '.ex-viewport').getAttribute('data-ex-paint')).toBe(paint);
    });

    it('ADR-0033: a header says the Sort in force, and follows it when the source sorts', async () => {
        const source = GridSource.from(trades(100));
        const { root } = mount({ columns, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        source.setSorts([{ column: 'Notional', direction: 'descending' }]);
        await painted();
        expect([...root.querySelectorAll('.ex-header-cell')].map(cell => cell.getAttribute('aria-sort'))).toEqual(['none', 'descending']);
        expect(q(root, '.ex-viewport .ex-cell').textContent).toBe('Book-99');
    });

    it('ADR-0011: rows handed as a new array are another sequence, whose source counts from 0 as any new one does', async () => {
        const { root, view } = mount({ columns, rows: trades(100), rowHeight: 24, viewportHeight: 480 });
        await painted();
        view.placeSelection({ topRow: 3, leftColumn: 0, rowCount: 2, columnCount: 1 }, { row: 3, column: 0 }, 0);
        await painted();
        view.update({ columns, rows: trades(100).toReversed(), rowHeight: 24, viewportHeight: 480 });
        await painted();
        // The positions named the rows of the old array, so the Selection goes, though the version a Consumer is
        // handed, the new source's own, is the one the old source began at.
        expect([view.readView().selection.isEmpty, q(root, '.ex-viewport').getAttribute('data-ex-sequence')]).toEqual([true, '0']);
    });

    it('ADR-0350: a grid that cannot be drawn leaves nothing listening, and the source it was handed unsubscribed', () => {
        const source = GridSource.from(trades(100));
        const subscribe = source.subscribe.bind(source);
        let listening = 0;
        source.subscribe = (listener) => {
            listening += 1;
            const stop = subscribe(listener);
            return () => {
                listening -= 1;
                stop();
            };
        };
        const root = document.createElement('div');
        const told = new Told();
        // A Viewport with no room below its header is refused on the first pass, after the listeners attach.
        expect(() => new GridView(root, { columns, source, rowHeight: 24, viewportHeight: 10 }, told)).toThrow(/Viewport/);
        expect([told.listening, listening, root.childElementCount, root.hasAttribute('role')]).toEqual([false, 0, 0, false]);
        // Columns refused before anything is made.
        expect(() => new GridView(root, { columns: [...columns, elementAt(columns, 0)], source }, told)).toThrow(/Two columns are named 'Book'/);
        expect([told.listening, listening, root.childElementCount]).toEqual([false, 0, 0]);
    });

    it('ADR-0350: options refused by update leave the grid as it was, and refused again when handed again', async () => {
        const source = GridSource.from(trades(100));
        source.setColumns(columns);
        source.setSorts([{ column: 'Notional', direction: 'ascending' }]);
        const { root, view } = mount({ columns, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        const bookOnly = [elementAt(columns, 0)];
        // The Sort names a column the new declaration drops, so the source refuses it.
        expect(() => { view.update({ columns: bookOnly, source, rowHeight: 24, viewportHeight: 480 }); }).toThrow(/Unknown column 'Notional'/);
        expect(() => { view.update({ columns: bookOnly, source, rowHeight: 24, viewportHeight: 480 }); }).toThrow(/Unknown column 'Notional'/);
        await painted();
        expect(root.getAttribute('aria-colcount')).toBe('2');
        expect(root.querySelectorAll('.ex-header-cell')).toHaveLength(2);
    });
});
