// @vitest-environment happy-dom
// MuiPivot (ADR-0390, ADR-0030): the pivot inside MUI's Paper, its parts at the size its Density maps onto, a
// Consumer's slot beating the Wrapper's, and a theme without CSS variables refused.
import { createElement, type ReactNode } from 'react';
import { act, cleanup, render, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { RecordPivotSource, createPivotLayout, pivotRecordField } from '@ex-grid/pivot-engine';
import { showDetails } from '../../pivot-react/test/pivot-slot-contract';
import { MuiPivot, type MuiPivotProps } from '../src/index';

interface Trade {
    readonly region: string;
    readonly amount: number;
}

const trades: readonly Trade[] = [{ region: 'EU', amount: 10 }, { region: 'US', amount: 5 }];
const fields = [
    pivotRecordField<Trade>({ name: 'Region', type: 'text', value: trade => trade.region }),
    pivotRecordField<Trade>({ name: 'Amount', type: 'number', value: trade => trade.amount }),
];
const layout = createPivotLayout({ rows: ['Region'], values: ['Amount'] });
/** The report's work yields to microtasks only, so what a test reads never depends on how fast it ran. */
const slicing = Object.freeze({ yield: () => Promise.resolve() });
const theme = createTheme({ cssVariables: true });

afterEach(() => { cleanup(); });

async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 3; turn++) await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

const wrapper = ({ children }: { readonly children: ReactNode }): ReactNode => createElement(ThemeProvider, { theme }, children);

/** A pivot given no width, as a Consumer's page often leaves it. */
const pivotProps = (props: Partial<MuiPivotProps>): MuiPivotProps => ({
    source: new RecordPivotSource(trades, fields), layout, slicing, locale: 'en-US', viewportHeight: 320, ...props,
});

async function mounted(props: Partial<MuiPivotProps> = {}): Promise<HTMLElement> {
    // Under this document the report paints its rows only at a width it is given.
    const { container } = render(createElement(MuiPivot, pivotProps({ viewportWidth: 600, ...props })), { wrapper });
    await settled();
    const paper = container.querySelector<HTMLElement>('.mui-ex-pivot');
    if (paper === null || paper.querySelector('.ex-pivot-sheet [role=grid]') === null) throw new Error('No pivot stands inside a Paper.');
    return paper;
}

const layoutButton = (paper: HTMLElement): HTMLElement => within(paper).getByRole('button', { name: /^Layout/ });

describe('MuiPivot', () => {
    it('ADR-0390: the pivot stands in MUI\'s outlined Paper, which carries the grid Wrapper\'s class for the report\'s tokens and the Consumer\'s own', async () => {
        const paper = await mounted({ className: 'mine' });
        expect([...paper.classList].filter(name => name.startsWith('mui-ex-') || name === 'mine').toSorted()).toEqual(['mine', 'mui-ex-grid', 'mui-ex-pivot']);
        expect([paper.classList.contains('MuiPaper-root'), paper.classList.contains('MuiPaper-outlined')]).toEqual([true, true]);
    });

    it('ADR-0045: a pivot given no width stretches its Paper to the container, as a Stretch grid\'s does, and one given a width does not', async () => {
        const { container } = render(createElement(MuiPivot, pivotProps({})), { wrapper });
        await settled();
        expect(container.querySelector('.mui-ex-pivot')?.classList.contains('mui-ex-grid-stretch')).toBe(true);
        cleanup();
        expect((await mounted()).classList.contains('mui-ex-grid-stretch')).toBe(false);
    });

    it('ADR-0390 section 10: the parts are drawn small where the Density is compact, as none given is, and at MUI\'s default otherwise', async () => {
        expect(layoutButton(await mounted()).classList.contains('MuiButton-sizeSmall')).toBe(true);
        cleanup();
        expect(layoutButton(await mounted({ density: 'comfortable' })).classList.contains('MuiButton-sizeMedium')).toBe(true);
    });

    it('ADR-0390: a slot the Consumer names is drawn by the Consumer, and the others stay MUI\'s', async () => {
        const paper = await mounted({ chrome: { toolbar: () => createElement('div', { className: 'my-toolbar' }, 'Mine') } });
        expect(paper.querySelector('.my-toolbar')?.textContent).toBe('Mine');
        expect(paper.querySelector('.mui-ex-pivot-toolbar')).toBeNull();
        expect(paper.querySelector('.mui-ex-pivot-pane')).not.toBeNull();
    });

    it('ADR-0390: a slot the Consumer hands as undefined leaves the Wrapper\'s, as one not named does', async () => {
        // As a Consumer's code without exactOptionalPropertyTypes writes it, which this package's own settings refuse.
        const paper = await mounted({ chrome: Object.fromEntries([['toolbar', undefined]]) });
        expect(paper.querySelector('.mui-ex-pivot-toolbar')).not.toBeNull();
    });

    it('ADR-0390 section 5: a theme made without CSS variables is refused by name, never drawn in colours not its own', () => {
        // React reports the throw on the console as well; the throw is what is judged.
        const quiet = vi.spyOn(console, 'error').mockImplementation(() => undefined);
        try {
            expect(() => render(createElement(ThemeProvider, { theme: createTheme() }, createElement(MuiPivot, pivotProps({})))))
                .toThrow(/MuiPivot needs a MUI theme with CSS variables/u);
        } finally {
            quiet.mockRestore();
        }
    });

    it('ADR-0059: Delete on a sheet tab closes it, as its close mark does, which takes no keyboard inside the tab\'s button', async () => {
        const paper = await mounted();
        await showDetails(paper);
        const tabs = within(paper).getAllByRole('tab');
        expect(tabs.map(tab => tab.textContent)).toEqual(['PivotTable', 'Details: EU']);
        await act(async () => {
            tabs[1]?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Delete', bubbles: true, cancelable: true }));
            await Promise.resolve();
        });
        await settled();
        expect(within(paper).queryAllByRole('tab')).toEqual([]);
    });
});
