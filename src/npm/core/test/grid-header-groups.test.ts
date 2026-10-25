// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): Header Groups in the header band (ADR-0032), to the DOM contract the C#
// core writes: a row per tier of rectangles over the leaf row, the band's height off the rows' room, and a
// press on a rectangle naming no column.
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridColumn } from '../src/dom/grid-column';
import { GridSource } from '../src/source/grid-source';
import { mount, painted, q, rowsOf, trades, type Trade } from './grid-harness';

// Book 110, Notional 140 and Quantity 120 wide, as the Features page's first columns are.
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 110 },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 140 },
    { name: 'Quantity', type: 'number', value: trade => trade.notional / 10, width: 120 },
];

const options = { columns, rowHeight: 24, headerHeight: 28, viewportHeight: 320, viewportWidth: 720 } as const;

/** An event on the header at its own offsets, which is what the grid reads (ADR-0012). */
function onHeader(root: HTMLElement, type: string, x: number, y: number): void {
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, clientX: x, clientY: y });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    q(root, '.ex-header').dispatchEvent(event);
}

const attributes = (element: Element, names: readonly string[]): Record<string, string | null> =>
    Object.fromEntries(names.map(name => [name, element.getAttribute(name)]));

describe('Header Groups', () => {
    it('ADR-0032/0033: the header is a group of rows, a tier of rectangles over the leaf row, as the C# core writes it', async () => {
        const { root } = mount({ ...options, rows: trades(1000), headerGroups: [
            { label: 'Who', columns: ['Book'] }, { label: 'What', columns: ['Quantity', 'Notional'] },
        ] });
        await painted();
        const header = q(root, '.ex-header');
        expect(attributes(header, ['role', 'aria-rowindex', 'style'])).toEqual({
            role: 'rowgroup', 'aria-rowindex': null, style: 'height: 56px; line-height: 28px',
        });
        const [tier, leaves] = [...header.children];
        expect(attributes(defined(tier, 'the tier'), ['role', 'aria-rowindex', 'style']))
            .toEqual({ role: 'row', 'aria-rowindex': '1', style: 'display: contents' });
        expect([...defined(tier, 'the tier').children].map(group => [group.className, group.textContent,
            ...Object.values(attributes(group, ['style', 'role', 'aria-colindex', 'aria-colspan', 'aria-rowspan']))])).toEqual([
            ['ex-header-group', 'Who', 'left: 0px; top: 0px; width: 110px; height: 28px; line-height: 28px', 'columnheader', '1', '1', null],
            ['ex-header-group', 'What', 'left: 110px; top: 0px; width: 260px; height: 28px; line-height: 28px', 'columnheader', '2', '2', null],
        ]);
        expect(attributes(defined(leaves, 'the leaf row'), ['role', 'aria-rowindex', 'style']))
            .toEqual({ role: 'row', 'aria-rowindex': '2', style: 'display: contents' });
        expect([...defined(leaves, 'the leaf row').children].map(cell => [cell.textContent, cell.getAttribute('style')])).toEqual([
            ['Book', 'width: 110px; align-self: flex-end; height: 28px; line-height: 28px'],
            ['Notional', 'width: 140px; align-self: flex-end; height: 28px; line-height: 28px'],
            ['Quantity', 'width: 120px; align-self: flex-end; height: 28px; line-height: 28px'],
        ]);
        // The band moves where the rows begin, and counts in the accessibility tree ahead of them.
        expect(q(root, '.ex-viewport').getAttribute('style')).toBe('top: 56px; transform: translateY(round(nearest, 0px, var(--ex-dp, 1px)))');
        expect(q(root, '.ex-spacer').getAttribute('style')).toBe('width: 370px; height: 24056px');
        expect(root.getAttribute('aria-rowcount')).toBe('1002');
        expect(defined(rowsOf(root)[0], 'the first row').getAttribute('aria-rowindex')).toBe('3');
        // The rows have what the band leaves: 320 - 56 = 264 px, eleven rows, and the slice's one beyond.
        expect(rowsOf(root)).toHaveLength(12);
    });

    it('ADR-0032: a leaf no rectangle covers stands the full band, and a rectangle over two tiers says its rowspan', async () => {
        const { root } = mount({ ...options, rows: trades(1000), headerGroups: [
            { label: 'Money', columns: ['Notional', 'Quantity'], tier: 2, tierSpan: 2 },
        ] });
        await painted();
        expect(q(root, '.ex-header').getAttribute('style')).toBe('height: 84px; line-height: 28px');
        const group = q(root, '.ex-header-group');
        expect(attributes(group, ['style', 'aria-rowspan']))
            .toEqual({ style: 'left: 110px; top: 0px; width: 260px; height: 56px; line-height: 56px', 'aria-rowspan': '2' });
        // Two tiers' rows, the lower one holding nothing, then the leaves.
        expect([...q(root, '.ex-header').children].map(row => [row.getAttribute('aria-rowindex'), row.children.length]))
            .toEqual([['1', 1], ['2', 0], ['3', 3]]);
        expect([...root.querySelectorAll('.ex-header-cell')].map(cell => cell.getAttribute('style'))).toEqual([
            'width: 110px; align-self: flex-end; height: 84px; line-height: 84px',
            'width: 140px; align-self: flex-end; height: 28px; line-height: 28px',
            'width: 120px; align-self: flex-end; height: 28px; line-height: 28px',
        ]);
    });

    it('ADR-0032/0004: a rectangle inside the Pinned Columns rides the sticky anchor at the band\'s left edge', async () => {
        const { root } = mount({ ...options, rows: trades(1000), pinnedColumnCount: 1, headerGroups: [
            { label: 'Who', columns: ['Book'] }, { label: 'What', columns: ['Notional', 'Quantity'] },
        ] });
        await painted();
        const tier = q(root, '.ex-header > [role="row"]');
        expect([...tier.children].map(child => child.className)).toEqual(['ex-header-groups-pinned', 'ex-header-group']);
        expect(q(root, '.ex-header-groups-pinned > .ex-header-group').textContent).toBe('Who');
    });

    it('ADR-0032: groups whose members are apart, that straddle the pinned edge or that leave no room for a row are refused by name', () => {
        const rows = trades(10);
        expect(() => mount({ ...options, rows, headerGroups: [{ label: 'Apart', columns: ['Book', 'Quantity'] }] }))
            .toThrow(/'Apart' members are not adjacent/);
        expect(() => mount({ ...options, rows, pinnedColumnCount: 1, headerGroups: [{ label: 'Across', columns: ['Book', 'Notional'] }] }))
            .toThrow(/'Across' straddles the pinned boundary/);
        expect(() => mount({ ...options, rows, viewportHeight: 80, headerGroups: [{ label: 'Who', columns: ['Book'] }] }))
            .toThrow('viewportHeight (80px) must exceed the header band — 2 tiers × 28px = 56px — plus one row of 24px (ADR-0032).');
    });

    it('ADR-0032/0012: a click on a rectangle sorts nothing; a click on the leaf beneath it sorts its column', async () => {
        const source = GridSource.from(trades(100));
        const { root } = mount({ ...options, source, headerGroups: [{ label: 'Money', columns: ['Notional', 'Quantity'] }] });
        await painted();
        for (const type of ['mousedown', 'mouseup', 'click']) onHeader(root, type, 150, 10);
        await painted();
        expect(source.snapshot().sorts).toEqual([]);
        for (const type of ['mousedown', 'mouseup', 'click']) onHeader(root, type, 150, 40);
        await painted();
        expect(source.snapshot().sorts).toEqual([{ column: 'Notional', direction: 'ascending' }]);
        // Book, under no rectangle, stands the whole band and is pressed anywhere in it.
        for (const type of ['mousedown', 'mouseup', 'click']) onHeader(root, type, 10, 10);
        await painted();
        expect(source.snapshot().sorts).toEqual([{ column: 'Book', direction: 'ascending' }]);
    });

    it('ADR-0033: groups added by update move every row\'s place in the tree, and taken away move it back', async () => {
        const rows = trades(1000);
        const { root, view } = mount({ ...options, rows });
        await painted();
        expect(q(root, '.ex-header').getAttribute('role')).toBe('row');
        expect(defined(rowsOf(root)[0], 'the first row').getAttribute('aria-rowindex')).toBe('2');
        view.update({ ...options, rows, headerGroups: [{ label: 'Who', columns: ['Book'] }] });
        await painted();
        expect(defined(rowsOf(root)[0], 'the first row').getAttribute('aria-rowindex')).toBe('3');
        view.update({ ...options, rows });
        await painted();
        expect(attributes(q(root, '.ex-header'), ['role', 'aria-rowindex', 'style'])).toEqual({ role: 'row', 'aria-rowindex': '1', style: null });
        expect(q(root, '.ex-viewport').getAttribute('style')).toBe('transform: translateY(round(nearest, 0px, var(--ex-dp, 1px)))');
        expect(defined(rowsOf(root)[0], 'the first row').getAttribute('aria-rowindex')).toBe('2');
    });
});
