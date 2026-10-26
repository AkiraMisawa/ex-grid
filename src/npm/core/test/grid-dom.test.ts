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

describe('createGrid', () => {
    it('ADR-0350: the root, the scroller, the header and the rows are the C# core\'s DOM contract', async () => {
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(root.className).toBe('ex-grid');
        expect(root.getAttribute('style')).toBe('--ex-row-height: 24px; --ex-header-height: 24px; --ex-font-size: 14px; '
            + '--ex-cell-padding-x: 8px; --ex-action-padding-x: 6px; --ex-action-border-width: 1px; --ex-action-gap: 4px; '
            + '--ex-menu-button-width: 16px; --ex-menu-button-inset: 6px; --ex-sort-mark-width: 20px; --ex-resize-grip-width: 5px; '
            + '--ex-column-guide-width: 2px; --ex-dp: 1px');
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

    it('ADR-0011: rows handed as a new array are another sequence, and the Row Sequence Version moves on', async () => {
        const { root, view } = mount({ columns, rows: trades(100), rowHeight: 24, viewportHeight: 480 });
        await painted();
        const before = Number(q(root, '.ex-viewport').getAttribute('data-ex-sequence'));
        view.update({ columns, rows: trades(100).toReversed(), rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(Number(q(root, '.ex-viewport').getAttribute('data-ex-sequence'))).toBeGreaterThan(before);
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
