// @vitest-environment happy-dom
// The adapter's own tests (ADR-0350 section 13): React Testing Library mounts it. What the grid paints is
// the core's, tested there; here, that the adapter mounts the core once, hands it each render's props, and
// destroys it with the component.
import { createElement, createRef, useLayoutEffect, useRef, useState, type ReactElement } from 'react';
import { StrictMode } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { GridSource, type GridColumn, type GridTemplateCell } from '@ex-grid/core';
import { ExGrid, type ExGridApi } from '../src/ex-grid';
import { templateColumn } from '../src/template-column';

interface Trade {
    readonly book: string;
}

const columns: readonly GridColumn<Trade>[] = [{ name: 'Book', type: 'text', value: trade => trade.book, width: 120 }];

afterEach(() => { cleanup(); });

const from = (): never => { throw new Error('Not read.'); };

describe('ExGrid', () => {
    it('ADR-0350: the element React renders is the grid\'s instance root, drawn before the browser paints', () => {
        const { container } = render(createElement(ExGrid<Trade>, { columns, rows: [{ book: 'Rates' }] }));
        const root = container.firstElementChild;
        expect(root?.getAttribute('role')).toBe('grid');
        expect(root?.querySelector('.ex-header-cell')?.textContent).toBe('Book');
    });

    it('ADR-0350: a render hands the grid its props, and the same root keeps drawing', async () => {
        const { container, rerender } = render(createElement(ExGrid<Trade>, { columns, rows: [{ book: 'Rates' }] }));
        const root = container.firstElementChild;
        rerender(createElement(ExGrid<Trade>, { columns, rows: [{ book: 'Credit' }, { book: 'FX' }] }));
        await Promise.resolve();
        expect(container.firstElementChild).toBe(root);
        expect(root?.getAttribute('aria-rowcount')).toBe('3');
    });

    it('ADR-0050: a ref reaches the grid\'s placement, which places the Selection, and throws once the grid is unmounted', async () => {
        const ref = createRef<ExGridApi<Trade>>();
        const { container, unmount } = render(createElement(ExGrid<Trade>, { columns, rows: [{ book: 'Rates' }, { book: 'Credit' }], ref }));
        const api = ref.current;
        if (api === null) throw new Error('The ref was handed the grid.');
        let placed = false;
        await act(async () => {
            placed = api.placeSelection({ topRow: 1, leftColumn: 0, rowCount: 1, columnCount: 1 }, { row: 1, column: 0 }, 0);
            await Promise.resolve();
        });
        expect(placed).toBe(true);
        expect(container.firstElementChild?.getAttribute('aria-activedescendant')).toMatch(/r1c0$/);
        unmount();
        expect(() => api.placeSelection({ topRow: 0, leftColumn: 0, rowCount: 1, columnCount: 1 }, { row: 0, column: 0 }, 0)).toThrow(/not mounted/);
    });

    it('ADR-0370 section 10: given no locale, the grid reads typed text in the browser\'s language, read once when it is made', () => {
        // An own property over the prototype's getter, taken away again after.
        Object.defineProperty(navigator, 'language', { value: 'de-DE', configurable: true });
        try {
            const numbers: readonly GridColumn<{ readonly notional: number }>[] = [{ name: 'Notional', type: 'number', value: row => row.notional, width: 120 }];
            const { container } = render(createElement(ExGrid<{ readonly notional: number }>, { columns: numbers, rows: [{ notional: 1 }] }));
            const menu = container.querySelector<HTMLElement>('.ex-menu-button');
            menu?.click();
            const operand = container.querySelector<HTMLInputElement>('.ex-popover-filter input:not([type])');
            if (operand === null) throw new Error('The filter panel stands.');
            operand.value = '1234.5';
            operand.dispatchEvent(new Event('input', { bubbles: true }));
            expect(container.querySelector('.ex-popover-refusal')?.textContent).toContain('as German (Germany) writes it');
        } finally {
            Reflect.deleteProperty(navigator, 'language');
        }
    });

    it('ADR-0350 section 16: a Date column\'s filter is never made with the browser\'s own Temporal, so every browser refuses a grid given none alike', () => {
        Reflect.set(globalThis, 'Temporal', { PlainDate: { from }, PlainDateTime: { from }, ZonedDateTime: { from } });
        try {
            const dates: readonly GridColumn<{ readonly day: unknown }>[] = [{ name: 'Day', type: 'date', value: row => row.day, width: 120 }];
            expect(() => render(createElement(ExGrid<{ readonly day: unknown }>, { columns: dates, rows: [] }))).toThrow(/Column 'Day'/u);
        } finally {
            Reflect.deleteProperty(globalThis, 'Temporal');
        }
    });

    it('ADR-0018: unmounted, the grid is destroyed and its root emptied; under StrictMode one grid stands', () => {
        const { container, unmount } = render(createElement(StrictMode, null, createElement(ExGrid<Trade>, { columns, rows: [] })));
        const root = container.firstElementChild;
        expect(root?.querySelectorAll('.ex-scroller')).toHaveLength(1);
        unmount();
        expect(root?.childElementCount).toBe(0);
    });

    it('ADR-0400: a Header Group\'s toggle reaches the Consumer\'s handler, whose state hands the grid its new columns', async () => {
        interface Leg { readonly rate: number }
        const legColumns: readonly GridColumn<Leg>[] = [
            { name: 'Rate', type: 'number', value: leg => leg.rate, width: 80 },
            { name: 'Calendar', type: 'text', value: () => 'TARGET', width: 100 },
        ];
        const heard: unknown[] = [];
        function Legs(): ReactElement {
            const [expanded, setExpanded] = useState(true);
            return createElement(ExGrid<Leg>, {
                rows: [{ rate: 1.25 }],
                columns: expanded ? legColumns : legColumns.slice(0, 1),
                headerGroups: [{ label: 'Leg 1', columns: expanded ? ['Rate', 'Calendar'] : ['Rate'], id: 'leg1', expanded }],
                onHeaderGroupToggled: toggle => { heard.push(toggle); setExpanded(toggle.expanded); },
            });
        }
        const { container } = render(createElement(Legs));
        const toggle = container.querySelector('.ex-header-group-toggle');
        if (toggle === null) throw new Error('The group draws its toggle.');
        await act(async () => {
            for (const type of ['mousedown', 'mouseup', 'click']) toggle.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button: 0 }));
            await Promise.resolve();
        });
        expect(heard).toEqual([{ id: 'leg1', expanded: false }]);
        expect([...container.querySelectorAll('.ex-header-cell')].map(header => header.textContent)).toEqual(['Rate']);
        expect(container.querySelector('.ex-header-group-toggle')?.getAttribute('aria-expanded')).toBe('false');
    });
});

interface Position {
    readonly book: string;
    readonly limit: number;
}

// A control that keeps state of its own, and answers a focus request from a layout effect, as ADR-0037 asks.
function Note({ cell }: { readonly cell: GridTemplateCell<Position> }): ReactElement {
    const input = useRef<HTMLInputElement>(null);
    const [mounts] = useState(() => { noteMounts += 1; return noteMounts; });
    useLayoutEffect(() => {
        if (cell.focusRequest !== 0 && input.current !== null) cell.takeKeyboard(input.current);
    }, [cell]);
    return createElement('input', {
        ref: input, className: 'ex-interactive', tabIndex: -1, 'aria-label': `Note for ${cell.row.book}`, 'data-limit': cell.row.limit,
        'data-mount': mounts, readOnly: true,
    });
}
let noteMounts = 0;

const positionColumns: readonly GridColumn<Position>[] = [
    { name: 'Book', type: 'text', value: row => row.book, width: 120 },
    templateColumn<Position>({ name: 'Note', type: 'text', value: row => row.book, width: 140 }, cell => createElement(Note, { cell })),
];

/** Lets the core's pass, and the render it asks of React, run. */
async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 4; turn++) await Promise.resolve();
    });
}

describe('templateColumn', () => {
    it('ADR-0020: React renders a Template cell\'s content into the slot the core keeps, a new version of its row under a Row Key keeping its state', async () => {
        const rows = [{ book: 'Rates', limit: 1 }, { book: 'Credit', limit: 2 }];
        const source = GridSource.from(rows, { rowKey: row => row.book });
        const { container, unmount } = render(createElement(StrictMode, null, createElement(ExGrid<Position>, { columns: positionColumns, source })));
        await settled();
        const note = (book: string): HTMLInputElement | null => container.querySelector<HTMLInputElement>(`input[aria-label="Note for ${book}"]`);
        const rates = note('Rates');
        expect(rates?.closest('[role="gridcell"]')?.getAttribute('aria-colindex')).toBe('2');
        const mount = rates?.getAttribute('data-mount');
        await act(async () => { source.replaceRow(rows[0] as Position, { book: 'Rates', limit: 9 }); });
        await settled();
        expect(note('Rates')).toBe(rates);
        expect([rates?.getAttribute('data-limit'), rates?.getAttribute('data-mount')]).toEqual(['9', mount]);
        unmount();
        expect(container.querySelector('input')).toBeNull();
    });

    it('ADR-0037: Space hands one render a focus request, and the control takes the keyboard before the next input', async () => {
        const rows = [{ book: 'Rates', limit: 1 }];
        const { container } = render(createElement(ExGrid<Position>, { columns: positionColumns, rows }));
        await settled();
        const root = container.firstElementChild as HTMLElement;
        const cell = container.querySelector<HTMLElement>('[aria-colindex="2"][role="gridcell"]');
        const press = new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0, buttons: 1 });
        Object.defineProperty(press, 'offsetX', { value: 130 });
        Object.defineProperty(press, 'offsetY', { value: 5 });
        root.querySelector('.ex-viewport')?.dispatchEvent(press);
        await settled();
        root.dispatchEvent(new KeyboardEvent('keydown', { key: ' ', bubbles: true, cancelable: true }));
        await settled();
        expect(document.activeElement).toBe(cell?.querySelector('input'));
    });

    it('ADR-0020: a React Template Column renders only inside an ExGrid of @ex-grid/react', () => {
        const [, note] = positionColumns;
        const slot = document.createElement('div');
        document.body.append(slot);
        expect(() => note?.template?.(slot, { row: { book: 'Rates', limit: 1 }, column: 'Note', focusRequest: 0, takeKeyboard: () => undefined }))
            .toThrow(/only inside an ExGrid/);
        slot.remove();
    });
});
