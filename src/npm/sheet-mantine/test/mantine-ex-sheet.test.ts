// @vitest-environment happy-dom
// MantineExSheet (ADR-0390, ADR-0030): the Sheet inside Mantine's Paper, its own Chrome and its grid's drawn in
// Mantine's components at the size its Density picks (compact's xs: a Sheet's million rows fit no taller), and a Consumer's slot beating the Wrapper's.
import { createElement, type ReactNode } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { MantineProvider } from '@mantine/core';
import { MantineExSheet, type MantineExSheetProps } from '../src/index';

afterEach(() => { cleanup(); });

async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 4; turn++) await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

const wrapper = ({ children }: { readonly children: ReactNode }): ReactNode => createElement(MantineProvider, { env: 'test' }, children);

async function mounted(props: Partial<MantineExSheetProps> = {}): Promise<{ readonly paper: HTMLElement; readonly sheet: HTMLElement }> {
    const { container } = render(createElement(MantineExSheet, { locale: 'en-US', viewportWidth: 480, viewportHeight: 240, showToolbar: true, ...props }), { wrapper });
    await settled();
    const paper = container.querySelector<HTMLElement>('.mantine-ex-sheet');
    const sheet = container.querySelector<HTMLElement>('.ex-sheet');
    if (paper === null || sheet === null || !paper.contains(sheet)) throw new Error('No Sheet stands inside a Paper.');
    return { paper, sheet };
}

/** The size Mantine drew the Bold toggle at. */
function boldSize(sheet: HTMLElement): string | null | undefined {
    return sheet.querySelector('.ex-sheet-toolbar-slot [aria-label="Bold"]')?.getAttribute('data-size');
}

describe('MantineExSheet', () => {
    it('ADR-0390: the Sheet stands in a Paper of both Wrappers\' classes, radius 0 squaring it, and its toolbar and tabs are Mantine\'s', async () => {
        const { paper, sheet } = await mounted({ radius: 0, className: 'mine' });
        expect(['mantine-ex-grid', 'mantine-ex-sheet', 'mantine-ex-grid-square', 'mine', 'mantine-Paper-root'].map(name => paper.classList.contains(name)))
            .toEqual([true, true, true, true, true]);
        expect([sheet.querySelector('.ex-sheet-toolbar-slot .mantine-ActionIcon-root') !== null, sheet.querySelector('.ex-sheet-tab .mantine-ex-sheet-tab-face')?.textContent])
            .toEqual([true, 'Sheet1']);
    });

    it('ADR-0390 section 3: a slot the Consumer hands beats the Wrapper\'s, and the others stay Mantine\'s', async () => {
        const { sheet } = await mounted({ chrome: { sheetTab: context => createElement('i', { className: 'own-tab' }, context.name) } });
        expect([sheet.querySelector('.ex-sheet-tab .own-tab')?.textContent, sheet.querySelector('.mantine-ex-sheet-tab-face'), boldSize(sheet)]).toEqual(['Sheet1', null, 'xs']);
    });

});
