// @vitest-environment happy-dom
// MantinePivot (ADR-0390, ADR-0030): the pivot inside Mantine's Paper, its parts at the size its Density maps onto,
// and a Consumer's slot beating the Wrapper's.
import { createElement, type ReactNode } from 'react';
import { act, cleanup, render, within } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { MantineProvider } from '@mantine/core';
import { RecordPivotSource, createPivotLayout, pivotRecordField } from '@ex-grid/pivot-engine';
import { showDetails } from '../../pivot-react/test/pivot-slot-contract';
import { MantinePivot, type MantinePivotProps } from '../src/index';

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

afterEach(() => { cleanup(); });

async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 3; turn++) await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

const wrapper = ({ children }: { readonly children: ReactNode }): ReactNode => createElement(MantineProvider, null, children);

async function mounted(props: Partial<MantinePivotProps> = {}): Promise<HTMLElement> {
    const { container } = render(createElement(MantinePivot, {
        source: new RecordPivotSource(trades, fields), layout, slicing, locale: 'en-US', viewportWidth: 600, viewportHeight: 320, ...props,
    }), { wrapper });
    await settled();
    const paper = container.querySelector<HTMLElement>('.mantine-ex-pivot');
    if (paper === null || paper.querySelector('.ex-pivot-sheet [role=grid]') === null) throw new Error('No pivot stands inside a Paper.');
    return paper;
}

const layoutButton = (paper: HTMLElement): HTMLElement => within(paper).getByRole('button', { name: /^Layout/ });

describe('MantinePivot', () => {
    it('ADR-0390: the pivot stands in Mantine\'s Paper, which carries the grid Wrapper\'s class for the report\'s tokens and the Consumer\'s own', async () => {
        const paper = await mounted({ className: 'mine', withBorder: true });
        expect([...paper.classList].filter(name => name.startsWith('mantine-ex-') || name === 'mine').toSorted()).toEqual(['mantine-ex-grid', 'mantine-ex-pivot', 'mine']);
        expect(paper.classList.contains('mantine-Paper-root')).toBe(true);
    });

    it('ADR-0028: the parts are drawn at the size the Density maps onto, compact where none is given', async () => {
        expect(layoutButton(await mounted()).getAttribute('data-size')).toBe('xs');
        cleanup();
        expect(layoutButton(await mounted({ density: 'comfortable' })).getAttribute('data-size')).toBe('md');
    });

    it('ADR-0390: a slot the Consumer names is drawn by the Consumer, and the others stay Mantine\'s', async () => {
        const paper = await mounted({ chrome: { toolbar: () => createElement('div', { className: 'my-toolbar' }, 'Mine') } });
        expect(paper.querySelector('.my-toolbar')?.textContent).toBe('Mine');
        expect(paper.querySelector('.mantine-ex-pivot-toolbar')).toBeNull();
        expect(paper.querySelector('.mantine-ex-pivot-pane')).not.toBeNull();
    });

    it('ADR-0390: a slot the Consumer hands as undefined leaves the Wrapper\'s, as one not named does', async () => {
        // As a Consumer's code without exactOptionalPropertyTypes writes it, which this package's own settings refuse.
        const paper = await mounted({ chrome: Object.fromEntries([['toolbar', undefined]]) });
        expect(paper.querySelector('.mantine-ex-pivot-toolbar')).not.toBeNull();
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
