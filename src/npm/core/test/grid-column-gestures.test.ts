// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the header's drags, as the C# core answers them. A resize from a column's
// grip is applied on release and never live (ADR-0016); a reorder moves the unit grabbed, and never across
// the pinned boundary or a Header Group's edge (ADR-0011, ADR-0032). The grid changes nothing: it reports.
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridColumnWidthChange } from '../src/dom/column-gestures';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import { mount, painted, q, trades, type Trade } from './grid-harness';

// Book 110, Notional 140 and Quantity 120 wide, as the Features page's first columns are.
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 110 },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 140 },
    { name: 'Quantity', type: 'number', value: trade => trade.notional / 10, width: 120 },
];

const base = { columns, rows: trades(100), rowHeight: 24, headerHeight: 28, viewportHeight: 320, viewportWidth: 720 } as const;

function mouse(target: Element, type: string, init: MouseEventInit & { readonly offsetX?: number; readonly offsetY?: number }): void {
    const { offsetX, offsetY, ...rest } = init;
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, ...rest });
    if (offsetX !== undefined) Object.defineProperty(event, 'offsetX', { value: offsetX });
    if (offsetY !== undefined) Object.defineProperty(event, 'offsetY', { value: offsetY });
    target.dispatchEvent(event);
}

/** A press on the header at its own offsets, with the client x at the same place. */
const pressHeader = (root: HTMLElement, x: number, y: number, init: MouseEventInit = {}): void => {
    mouse(q(root, '.ex-header'), 'mousedown', { clientX: x, clientY: y, offsetX: x, offsetY: y, buttons: 1, ...init });
};
const clickHeader = (root: HTMLElement, x: number, y: number, init: MouseEventInit = {}): void => {
    mouse(q(root, '.ex-header'), 'click', { clientX: x, clientY: y, offsetX: x, offsetY: y, ...init });
};
/** The pointer moved to `clientX` with the button held, anywhere over the grid. */
const moveTo = (root: HTMLElement, clientX: number, buttons = 1): void => {
    mouse(q(root, '.ex-viewport'), 'mousemove', { clientX, clientY: 100, buttons });
};
const releaseAt = (root: HTMLElement, clientX: number, target: Element = q(root, '.ex-header')): void => {
    mouse(target, 'mouseup', { clientX, clientY: 10 });
};

const grip = (root: HTMLElement, column: number): Element =>
    defined(root.querySelectorAll('.ex-header-cell .ex-resize-grip')[column], `column ${column}'s grip`);
const line = (root: HTMLElement, className: string): string | null | undefined =>
    root.querySelector(`.ex-spacer > .${className}`)?.getAttribute('style');
const ariaSort = (root: HTMLElement, column: number): string | null =>
    defined(root.querySelectorAll('.ex-header-cell')[column], `column ${column}'s header`).getAttribute('aria-sort');

function heardWidths(options: Partial<GridOptions<Trade>> = {}): { root: HTMLElement; widths: GridColumnWidthChange[] } {
    const widths: GridColumnWidthChange[] = [];
    const { root } = mount<Trade>({ ...base, ...options, onColumnWidthChanged: change => { widths.push(change); } });
    return { root, widths };
}

function heardOrders(options: Partial<GridOptions<Trade>> = {}): { root: HTMLElement; orders: (readonly string[])[]; view: ReturnType<typeof mount<Trade>>['view'] } {
    const orders: (readonly string[])[] = [];
    const { root, view } = mount<Trade>({ ...base, ...options, onColumnOrderChanged: order => { orders.push(order); } });
    return { root, orders, view };
}

describe('a resize from a grip', () => {
    it('ADR-0016: a grip stands at each header\'s edge only while a resize is heard', async () => {
        const { root, view } = mount<Trade>(base);
        await painted();
        expect(root.querySelectorAll('.ex-resize-grip')).toHaveLength(0);
        view.update({ ...base, onColumnWidthChanged: () => undefined });
        await painted();
        expect(root.querySelectorAll('.ex-header-cell > .ex-resize-grip')).toHaveLength(3);
    });

    it('ADR-0016: the guide follows the pointer, the width is reported on release, and the click that ends it does not sort', async () => {
        const { root, widths } = heardWidths();
        await painted();
        mouse(grip(root, 1), 'mousedown', { clientX: 248, buttons: 1 });
        expect(line(root, 'ex-resize-guide')).toBe('left: 250px');
        moveTo(root, 286);
        expect([line(root, 'ex-resize-guide'), widths]).toEqual(['left: 288px', []]);
        releaseAt(root, 286, grip(root, 1));
        mouse(grip(root, 1), 'click', { clientX: 286 });
        await painted();
        expect([widths, line(root, 'ex-resize-guide'), ariaSort(root, 1)]).toEqual([[{ column: 'Notional', widthPx: 178 }], undefined, 'none']);
    });

    it('ADR-0016: a press that barely moves reports nothing, a drag is bounded below by MinWidth, and one released outside the grid applies nothing', async () => {
        const { root, widths } = heardWidths();
        await painted();
        mouse(grip(root, 0), 'mousedown', { clientX: 108, buttons: 1 });
        releaseAt(root, 112);
        mouse(grip(root, 0), 'mousedown', { clientX: 108, buttons: 1 });
        moveTo(root, -500);
        expect(line(root, 'ex-resize-guide')).toBe('left: 40px');
        releaseAt(root, -500);
        mouse(grip(root, 0), 'mousedown', { clientX: 108, buttons: 1 });
        moveTo(root, 200);
        // The button came up outside: the next move over the grid finds it up and drops the drag.
        moveTo(root, 200, 0);
        releaseAt(root, 200);
        expect([widths, line(root, 'ex-resize-guide')]).toEqual([[{ column: 'Book', widthPx: 40 }], undefined]);
    });

    it('FN-12c: a grip on a column wholly selected resizes every whole column selected, each heard on its own', async () => {
        const { root, widths } = heardWidths();
        await painted();
        clickHeader(root, 50, 10, { shiftKey: true });
        clickHeader(root, 150, 10, { shiftKey: true });
        mouse(grip(root, 1), 'mousedown', { clientX: 248, buttons: 1 });
        moveTo(root, 208);
        releaseAt(root, 208);
        // Quantity, not selected, keeps its width; the other two take the grabbed one's.
        expect(widths).toEqual([{ column: 'Book', widthPx: 100 }, { column: 'Notional', widthPx: 100 }]);
    });

    it('ADR-0016: a Fixed width keeps its bounds, so the width a drag reports can be handed back as it came', async () => {
        const bounded = (px: number): GridColumn<Trade> => ({ ...defined(columns[0], 'Book'), width: { px, minPx: 20, maxPx: 300 } });
        const { root, widths } = heardWidths({ columns: [bounded(110), ...columns.slice(1)] });
        await painted();
        mouse(grip(root, 0), 'mousedown', { clientX: 108, buttons: 1 });
        moveTo(root, -500);
        expect(line(root, 'ex-resize-guide')).toBe('left: 20px');
        releaseAt(root, 33);
        expect(widths).toEqual([{ column: 'Book', widthPx: 35 }]);
        const { view } = mount<Trade>({ ...base, columns: [bounded(35), ...columns.slice(1)] });
        expect(() => { view.update({ ...base, columns: [bounded(19), ...columns.slice(1)] }); }).toThrow(/below its MinWidth \(20\)/);
    });

    it('FN-12c: whole columns take the width the guide showed, bounded first by the grabbed column\'s MinWidth', async () => {
        const wide = { ...defined(columns[1], 'Notional'), width: { px: 140, minPx: 80 } };
        const { root, widths } = heardWidths({ columns: [defined(columns[0], 'Book'), wide, defined(columns[2], 'Quantity')] });
        await painted();
        clickHeader(root, 50, 10, { shiftKey: true });
        clickHeader(root, 150, 10, { shiftKey: true });
        mouse(grip(root, 1), 'mousedown', { clientX: 248, buttons: 1 });
        moveTo(root, 0);
        releaseAt(root, 0);
        expect(widths).toEqual([{ column: 'Book', widthPx: 80 }, { column: 'Notional', widthPx: 80 }]);
    });

    it('FN-12a: a double click on a grip fits the column to its values, bounded by its MaxWidth', async () => {
        const long = { name: 'Book', type: 'text', value: (trade: Trade) => `${trade.book} ${'x'.repeat(200)}`, width: 110 } as const;
        const { root, widths } = heardWidths({ columns: [long, ...columns.slice(1)] });
        await painted();
        mouse(grip(root, 0), 'dblclick', { clientX: 108 });
        expect(widths).toEqual([{ column: 'Book', widthPx: 400 }]);
    });
});

describe('a reorder by dragging a header', () => {
    it('ADR-0011: the leaf moves past the threshold, the indicator stands at the boundary it will drop at, and the click does not sort', async () => {
        const { root, orders } = heardOrders();
        await painted();
        pressHeader(root, 55, 10);
        moveTo(root, 59);
        expect(line(root, 'ex-drop-indicator')).toBeUndefined();
        moveTo(root, 600);
        expect(line(root, 'ex-drop-indicator')).toBe('left: 370px');
        releaseAt(root, 600);
        clickHeader(root, 600, 10);
        await painted();
        expect([orders, line(root, 'ex-drop-indicator'), ariaSort(root, 0)]).toEqual([[['Notional', 'Quantity', 'Book']], undefined, 'none']);
    });

    it('ADR-0011: with a reorder heard, a click without a drag still sorts and selects nothing', async () => {
        const { root, orders } = heardOrders();
        await painted();
        pressHeader(root, 55, 10);
        releaseAt(root, 57);
        clickHeader(root, 57, 10);
        await painted();
        expect([orders, ariaSort(root, 0), root.querySelector('.ex-range')]).toEqual([[], 'ascending', null]);
    });

    it('ADR-0032: a leaf clamps at its group\'s edge, and a group\'s rectangle moves the group whole', async () => {
        const groups = [{ label: 'Money', columns: ['Notional', 'Quantity'] }];
        const { root, orders } = heardOrders({ headerGroups: groups });
        await painted();
        // The leaf tier is the band's lower 28px; Notional dragged far left stops at Money's left edge.
        pressHeader(root, 180, 42);
        moveTo(root, -300);
        expect(line(root, 'ex-drop-indicator')).toBe('left: 110px');
        moveTo(root, 600);
        releaseAt(root, 600);
        // Money's rectangle, in the tier above, dragged past Book.
        pressHeader(root, 200, 14);
        moveTo(root, 0);
        releaseAt(root, 0);
        expect(orders).toEqual([['Book', 'Quantity', 'Notional'], ['Notional', 'Quantity', 'Book']]);
    });

    it('ADR-0011: a drag never crosses the pinned boundary, and a drop on the unit\'s own edge is heard as nothing', async () => {
        const { root, orders } = heardOrders({ pinnedColumnCount: 1 });
        await painted();
        pressHeader(root, 180, 10);
        moveTo(root, -300);
        expect(line(root, 'ex-drop-indicator')).toBe('left: 110px');
        releaseAt(root, -300);
        expect(orders).toEqual([]);
    });

    it('principle 6: columns handed over in another order end the drag, which then drops nowhere', async () => {
        const { root, orders, view } = heardOrders();
        await painted();
        pressHeader(root, 55, 10);
        moveTo(root, 300);
        view.update({ ...base, columns: columns.toReversed(), onColumnOrderChanged: order => { orders.push(order); } });
        moveTo(root, 600);
        releaseAt(root, 600);
        expect([orders, line(root, 'ex-drop-indicator')]).toEqual([[], undefined]);
    });
});
