// @vitest-environment happy-dom
// MantineExGrid (ADR-0390, ADR-0030): the grid inside Mantine's Paper, its font and the widths #### is judged by
// handed over in the same render, Mantine's Table words mapped onto the grid's own, a value the grid is given
// beating the Wrapper's, and a Consumer's slot beating the Wrapper's Chrome.
import { createElement, type ReactNode } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { MantineProvider } from '@mantine/core';
import { GridSource, type GridColumn, type GridLoadingIndicatorSlotContext } from '@ex-grid/core';
import { MantineExGrid, mantineDefaultFont, mantineSizeOf, type MantineExGridProps } from '../src/index';

interface Trade {
    readonly book: string;
    readonly notional: number;
}

const rows: readonly Trade[] = Array.from({ length: 30 }, (_, at) => ({ book: `Book-${at}`, notional: (at + 1) * 1000 }));
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120 },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 80 },
];

afterEach(() => { cleanup(); });

async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 4; turn++) await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

const wrapper = ({ children }: { readonly children: ReactNode }): ReactNode => createElement(MantineProvider, null, children);

async function mounted(props: Partial<MantineExGridProps<Trade>> = {}): Promise<{ readonly paper: HTMLElement; readonly root: HTMLElement }> {
    const { container } = render(createElement(MantineExGrid<Trade>, { columns, ...(props.source === undefined ? { rows } : {}), viewportHeight: 320, viewportWidth: 480, ...props }), { wrapper });
    await settled();
    const paper = container.querySelector<HTMLElement>('.mantine-ex-grid');
    const root = container.querySelector<HTMLElement>('[role=grid]');
    if (paper === null || root === null || !paper.contains(root)) throw new Error('No grid stands inside a Paper.');
    return { paper, root };
}

/** A source whose rows never come: the loading indicator stays up. */
const pending = (): ReturnType<typeof GridSource.fetch<Trade>> => GridSource.fetch<Trade>(() => new Promise<never>(() => undefined), { readAheadRows: 0 });
const mine = (context: GridLoadingIndicatorSlotContext): ReactNode => (context.loading ? createElement('em', { className: 'mine' }) : null);
const rowsOf = (root: HTMLElement): HTMLElement[] => [...root.querySelectorAll<HTMLElement>('.ex-viewport > .ex-row')];

describe('MantineExGrid', () => {
    it('ADR-0027: the Paper sets the font whose widths the grid is handed, Mantine\'s default stack unless one is given', async () => {
        const { paper, root } = await mounted();
        expect(paper.style.getPropertyValue('--ex-font-family')).toBe(mantineDefaultFont.family);
        expect(root.querySelector('.ex-hashes')).toBeNull();
        cleanup();
        // A face whose digits are three times as wide: 9000 no longer fits 80 pixels.
        const wide = await mounted({ font: { family: 'Wide', widths: { wideWidthPx: 30, digitWidthPx: 30, narrowWidthPx: 15, fontSizePx: 14 } } });
        expect([wide.paper.style.getPropertyValue('--ex-font-family'), wide.root.querySelector('.ex-hashes') !== null]).toEqual(['Wide', true]);
    });

    it('ADR-0030 / ADR-0038: striped stands where the grid gives no stripeRows, and the grid\'s false beats it', async () => {
        const striped = await mounted({ striped: true });
        expect(rowsOf(striped.root).slice(0, 2).map(row => row.classList.contains('ex-row-stripe'))).toEqual([false, true]);
        cleanup();
        const plain = await mounted({ striped: true, stripeRows: false });
        expect(rowsOf(plain.root).some(row => row.classList.contains('ex-row-stripe'))).toBe(false);
    });

    it('ADR-0390: radius 0 squares the grid and withColumnBorders rules its columns, by the Paper\'s classes', async () => {
        const { paper } = await mounted({ radius: 0, withColumnBorders: true, className: 'mine' });
        expect(['mantine-ex-grid-square', 'mantine-ex-grid-column-borders', 'mine'].map(name => paper.classList.contains(name))).toEqual([true, true, true]);
    });

    it('ADR-0390 section 3: a slot the Consumer hands beats the Wrapper\'s, and the others stay Mantine\'s', async () => {
        const { root } = await mounted({ source: pending(), chrome: { loadingIndicator: mine } });
        expect([root.querySelector('.ex-loading-indicator .mine') !== null, root.querySelector('.ex-loading-indicator .mantine-Loader-root')]).toEqual([true, null]);
        cleanup();
        const theirs = await mounted({ source: pending() });
        expect(theirs.root.querySelector('.ex-loading-indicator .mantine-Loader-root')).not.toBeNull();
    });

    it('ADR-0390: the Density picks the size Mantine\'s components are drawn at', () => {
        expect((['compact', 'excel', 'standard', 'comfortable'] as const).map(mantineSizeOf)).toEqual(['xs', 'xs', 'sm', 'md']);
        expect(() => mantineSizeOf('huge' as never)).toThrow(RangeError);
    });
});
