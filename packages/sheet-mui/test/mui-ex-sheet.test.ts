// @vitest-environment happy-dom
// MuiExSheet (ADR-0390, ADR-0030): the Sheet inside MUI's Paper, its own Chrome and its grid's drawn in MUI's
// components at the size its Density picks (compact's small: a Sheet's million rows fit no taller), a theme without CSS
// variables refused by name, and a Consumer's slot beating the Wrapper's.
import { Component, createElement, type ReactNode } from 'react';
import { act, cleanup, fireEvent, render, within } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { ThemeProvider, createTheme, type Theme } from '@mui/material/styles';
import { CellAddress } from '@ex-grid/sheet-engine';
import type { ExSheetApi } from '@ex-grid/sheet-react';
import { pressA1 } from '../../sheet-react/test/sheet-slot-contract';
import { MuiExSheet, type MuiExSheetProps } from '../src/index';

afterEach(() => { cleanup(); });

async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 4; turn++) await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

const variablesTheme = createTheme({ cssVariables: true });
const themed = (theme: Theme) => ({ children }: { readonly children: ReactNode }): ReactNode => createElement(ThemeProvider, { theme }, children);

async function mounted(props: Partial<MuiExSheetProps> = {}): Promise<{ readonly paper: HTMLElement; readonly sheet: HTMLElement }> {
    const { container } = render(createElement(MuiExSheet, { locale: 'en-US', viewportWidth: 480, viewportHeight: 240, showToolbar: true, ...props }),
        { wrapper: themed(variablesTheme) });
    await settled();
    const paper = container.querySelector<HTMLElement>('.mui-ex-sheet');
    const sheet = container.querySelector<HTMLElement>('.ex-sheet');
    if (paper === null || sheet === null || !paper.contains(sheet)) throw new Error('No Sheet stands inside a Paper.');
    return { paper, sheet };
}

/** A boundary, as a Consumer's page would put round a Sheet: what threw beneath it is drawn as nothing. */
class Caught extends Component<{ readonly children?: ReactNode }, { readonly failed: boolean }> {
    override state = { failed: false };
    static getDerivedStateFromError(): { readonly failed: boolean } { return { failed: true }; }
    override render(): ReactNode { return this.state.failed ? null : this.props.children; }
}

describe('MuiExSheet', () => {
    it('ADR-0390: the Sheet stands in an outlined Paper of both Wrappers\' classes, square setting it square, its toolbar and tabs MUI\'s at the small size', async () => {
        const { paper, sheet } = await mounted({ square: true, className: 'mine' });
        expect(['mui-ex-grid', 'mui-ex-sheet', 'mui-ex-grid-square', 'mine', 'MuiPaper-outlined', 'MuiPaper-rounded'].map(name => paper.classList.contains(name)))
            .toEqual([true, true, true, true, true, false]);
        const bold = sheet.querySelector('.ex-sheet-toolbar-slot [aria-label="Bold"]');
        expect([bold?.classList.contains('MuiToggleButton-sizeSmall'), sheet.querySelector('.ex-sheet-tab .mui-ex-sheet-tab-face')?.textContent])
            .toEqual([true, 'Sheet1']);
    });

    it('ADR-0390 section 3: a slot the Consumer hands beats the Wrapper\'s, and the others stay MUI\'s', async () => {
        const { sheet } = await mounted({ chrome: { sheetTab: context => createElement('i', { className: 'own-tab' }, context.name) } });
        expect([sheet.querySelector('.ex-sheet-tab .own-tab')?.textContent, sheet.querySelector('.mui-ex-sheet-tab-face'),
            sheet.querySelector('.ex-sheet-toolbar-slot .MuiToggleButton-root') !== null]).toEqual(['Sheet1', null, true]);
    });

    it('ADR-0390 sections 5 and 10: a theme the stylesheets cannot read is refused, naming the option, and nothing is drawn', async () => {
        const refusals: [Theme, string][] = [
            [createTheme(), 'createTheme({ cssVariables: true })'],
            [createTheme({ cssVariables: { cssVarPrefix: 'app' } }), 'cssVariables.cssVarPrefix'],
        ];
        for (const [theme, named] of refusals) {
            const errors: unknown[] = [];
            // Caught as a Consumer's page catches it, by a boundary; the root hears it in place of the console.
            const { container } = render(createElement(Caught, null, createElement(MuiExSheet, { locale: 'en-US', viewportWidth: 480, viewportHeight: 240 })),
                { wrapper: themed(theme), onCaughtError: (error: unknown) => { errors.push(error); } });
            await settled();
            expect([container.querySelector('.ex-sheet'), errors.map(error => error instanceof Error && error.message.includes(named))], named)
                .toEqual([null, [true]]);
            cleanup();
        }
    });

    it('ADR-0071: a number of places left out of range is refused by OK from another tab, which shows it again', async () => {
        const ref: { current: ExSheetApi | null } = { current: null };
        const { sheet } = await mounted({ showToolbar: false, ref });
        const api = ref.current;
        if (api === null) throw new Error('No Sheet was mounted.');
        await pressA1(sheet);
        act(() => { api.openFormatCells(); });
        await settled();
        const frame = (): HTMLElement => {
            const found = sheet.querySelector<HTMLElement>('.ex-popover-consumer');
            if (found === null) throw new Error('Format Cells is not open.');
            return found;
        };
        fireEvent.click(within(frame()).getByRole('radio', { name: 'Number' }));
        await settled();
        const places = within(frame()).getByRole('spinbutton', { name: 'Decimal places' });
        fireEvent.focus(places);
        fireEvent.change(places, { target: { value: '3' } });
        fireEvent.change(places, { target: { value: '31' } });
        fireEvent.click(within(frame()).getByRole('tab', { name: 'Font' }));
        await settled();
        fireEvent.click(within(frame()).getByRole('button', { name: 'OK' }));
        await settled();
        expect([frame().querySelector('[role=tab][aria-selected=true]')?.textContent, api.cellFormatAt(CellAddress.parse('A1')).numberFormat.code, api.canUndo])
            .toEqual(['Number', 'General', false]);
    });
});
