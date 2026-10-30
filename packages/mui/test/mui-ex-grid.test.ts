// @vitest-environment happy-dom
// MuiExGrid (ADR-0390, ADR-0030): the grid inside MUI's Paper, its font and the widths #### is judged by handed over
// in the same render, a theme without CSS variables refused by name, and a Consumer's slot beating the Wrapper's
// Chrome.
import { Component, createElement, type ReactNode } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { ThemeProvider, createTheme, type Theme } from '@mui/material/styles';
import { GridSource, type GridColumn, type GridLoadingIndicatorSlotContext } from '@ex-grid/core';
import { MuiExGrid, muiDefaultFont, muiSizeOf, type MuiExGridProps } from '../src/index';

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

const variablesTheme = createTheme({ cssVariables: true });
const themed = (theme: Theme) => ({ children }: { readonly children: ReactNode }): ReactNode => createElement(ThemeProvider, { theme }, children);

async function mounted(props: Partial<MuiExGridProps<Trade>> = {}): Promise<{ readonly paper: HTMLElement; readonly root: HTMLElement }> {
    const { container } = render(createElement(MuiExGrid<Trade>, { columns, ...(props.source === undefined ? { rows } : {}), viewportHeight: 320, viewportWidth: 480, ...props }),
        { wrapper: themed(variablesTheme) });
    await settled();
    const paper = container.querySelector<HTMLElement>('.mui-ex-grid');
    const root = container.querySelector<HTMLElement>('[role=grid]');
    if (paper === null || root === null || !paper.contains(root)) throw new Error('No grid stands inside a Paper.');
    return { paper, root };
}

/** A boundary, as a Consumer's page would put round a grid: what threw beneath it is drawn as nothing. */
class Caught extends Component<{ readonly children?: ReactNode }, { readonly failed: boolean }> {
    override state = { failed: false };
    static getDerivedStateFromError(): { readonly failed: boolean } { return { failed: true }; }
    override render(): ReactNode { return this.state.failed ? null : this.props.children; }
}

/** A source whose rows never come: the loading indicator stays up. */
const pending = (): ReturnType<typeof GridSource.fetch<Trade>> => GridSource.fetch<Trade>(() => new Promise<never>(() => undefined), { readAheadRows: 0 });
const mine = (context: GridLoadingIndicatorSlotContext): ReactNode => (context.loading ? createElement('em', { className: 'mine' }) : null);

describe('MuiExGrid', () => {
    it('ADR-0027: the Paper sets the font whose widths the grid is handed, MUI\'s default stack unless one is given', async () => {
        const { paper, root } = await mounted();
        expect(paper.style.getPropertyValue('--ex-font-family')).toBe(muiDefaultFont.family);
        expect(root.querySelector('.ex-hashes')).toBeNull();
        cleanup();
        // A face whose digits are three times as wide: 9000 no longer fits 80 pixels.
        const wide = await mounted({ font: { family: 'Wide', widths: { wideWidthPx: 30, digitWidthPx: 30, narrowWidthPx: 15, fontSizePx: 14 } } });
        expect([wide.paper.style.getPropertyValue('--ex-font-family'), wide.root.querySelector('.ex-hashes') !== null]).toEqual(['Wide', true]);
    });

    it('ADR-0390 sections 5 and 10: a theme the stylesheet cannot read is refused, naming the option, and nothing is drawn', async () => {
        const refusals: [Theme, string][] = [
            [createTheme(), 'createTheme({ cssVariables: true })'],
            [createTheme({ cssVariables: { cssVarPrefix: 'app' } }), 'cssVariables.cssVarPrefix'],
        ];
        for (const [theme, named] of refusals) {
            const errors: unknown[] = [];
            // Caught as a Consumer's page catches it, by a boundary; the root hears it in place of the console.
            const { container } = render(createElement(Caught, null, createElement(MuiExGrid<Trade>, { columns, rows, viewportHeight: 320, viewportWidth: 480 })),
                { wrapper: themed(theme), onCaughtError: (error: unknown) => { errors.push(error); } });
            await settled();
            expect([container.querySelector('[role=grid]'), errors.map(error => error instanceof Error && error.message.includes(named))], named)
                .toEqual([null, [true]]);
            cleanup();
        }
    });

    it('ADR-0390: square sets the grid square in its Paper and withColumnBorders rules its columns, by the Paper\'s classes', async () => {
        const { paper } = await mounted({ square: true, withColumnBorders: true, className: 'mine' });
        expect(['mui-ex-grid-square', 'mui-ex-grid-column-borders', 'mine', 'MuiPaper-rounded'].map(name => paper.classList.contains(name))).toEqual([true, true, true, false]);
    });

    it('ADR-0045: a Stretch grid\'s Paper follows its container, and a grid of declared width keeps a Paper of its own width', async () => {
        const stretched = await mounted({ viewportWidth: 'stretch' });
        const declared = stretched.paper.classList.contains('mui-ex-grid-stretch');
        cleanup();
        const { paper } = await mounted();
        expect([declared, paper.classList.contains('mui-ex-grid-stretch')]).toEqual([true, false]);
    });

    it('ADR-0390 section 3: a slot the Consumer hands beats the Wrapper\'s, and the others stay MUI\'s', async () => {
        const { root } = await mounted({ source: pending(), chrome: { loadingIndicator: mine } });
        expect([root.querySelector('.ex-loading-indicator .mine') !== null, root.querySelector('.ex-loading-indicator .MuiCircularProgress-root')]).toEqual([true, null]);
        cleanup();
        const theirs = await mounted({ source: pending() });
        expect(theirs.root.querySelector('.ex-loading-indicator .MuiCircularProgress-root')).not.toBeNull();
    });

    it('ADR-0390 section 10: the Density picks the size MUI\'s components are drawn at', () => {
        expect((['compact', 'excel', 'standard', 'comfortable'] as const).map(density => { const { size, dense } = muiSizeOf(density); return `${size}${dense ? ' dense' : ''}`; }))
            .toEqual(['small dense', 'small dense', 'medium', 'medium']);
        expect(() => muiSizeOf('huge' as never)).toThrow(RangeError);
    });
});
