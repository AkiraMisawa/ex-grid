// @vitest-environment happy-dom
// The adapter's own tests (ADR-0350 section 13): React Testing Library mounts it. What the Sheet does is
// `@ex-grid/sheet`'s, tested there; here, that the adapter mounts it once, hands it each render's props, and
// destroys it with the component.
import { createElement, createRef, StrictMode, useSyncExternalStore, type ReactNode } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import type { SheetToolbarItemSlotContext } from '@ex-grid/sheet';
import { afterEach, describe, expect, it } from 'vitest';
import { CellAddress, SheetEdit } from '@ex-grid/sheet-engine';
import { ExSheet, type ExSheetApi, type ExSheetProps } from '../src/ex-sheet';

afterEach(() => { cleanup(); });

// A Toolbar Item of a Chrome's own: a button showing the item's name, and whether its list is asked for.
function ListButton({ context }: { readonly context: SheetToolbarItemSlotContext }): ReactNode {
    const view = useSyncExternalStore(context.subscribe, context.view);
    return createElement('button', { className: 'own-list', 'aria-expanded': view.openRequest > view.closeRequest }, view.name);
}

// The split controls are the Chrome's; every other item is left to the built-in one.
const toolbarItem = (context: SheetToolbarItemSlotContext): ReactNode => (context.kind === 'split' ? createElement(ListButton, { context }) : null);

// A row of the Consumer's whose button shows `mark` as its own content.
const reviewRow = (mark: string): ExSheetProps['toolbar'] => [{
    label: 'Review', items: ['bold', { text: 'Approve', onPress: () => undefined, content: createElement('b', { className: 'own-icon' }, mark) }],
}];

describe('ExSheet', () => {
    it('ADR-0370: the element React renders is the Sheet\'s, drawn before the browser paints, the grid and its notice inside', () => {
        const { container } = render(createElement(ExSheet, { locale: 'en-US' }));
        const root = container.firstElementChild;
        expect(root?.classList.contains('ex-sheet')).toBe(true);
        expect(root?.querySelector('[role="grid"]')?.getAttribute('aria-colcount')).toBe('16384');
        expect(root?.querySelector('.ex-sheet-notice')?.getAttribute('role')).toBe('status');
    });

    it('ADR-0370 section 10: given no locale, a Sheet takes the browser\'s language once, and a render keeps it', () => {
        Object.defineProperty(navigator, 'language', { value: 'de-DE', configurable: true });
        try {
            const { container, rerender } = render(createElement(ExSheet, {}));
            const grid = container.querySelector('[role="grid"]');
            Object.defineProperty(navigator, 'language', { value: 'ja-JP', configurable: true });
            rerender(createElement(ExSheet, { viewportHeight: 200 }));
            expect(container.querySelector('[role="grid"]')).toBe(grid);
        } finally {
            Reflect.deleteProperty(navigator, 'language');
        }
    });

    it('ADR-0049, ADR-0331 §6: a ref reaches the mounted Sheet under StrictMode, both forms of its commands included, and asks nothing of one unmounted', () => {
        const sheet = createRef<ExSheetApi>();
        const { unmount } = render(createElement(StrictMode, null, createElement(ExSheet, { locale: 'en-US', ref: sheet })));
        const api = sheet.current;
        if (api === null) throw new Error('The ref was given the Sheet.');
        api.declareLinkedTable('Positions', ['Id', 'PV'], 'Id');
        api.pushLinkedTable('Positions', []);
        expect(api.workbook.linkedTables.map(table => table.name)).toEqual(['Positions']);
        expect(api.undo()).toBe(false);
        // Both forms of a command and of a read reach the Sheet: one naming no Sheet, and one naming it.
        api.do(SheetEdit.enter(CellAddress.parse('A1'), 'one'));
        api.do('Sheet1', SheetEdit.enter(CellAddress.parse('A2'), 'two'));
        expect([api.canUndo, api.canRedo, api.isEditing]).toEqual([true, false, false]);
        expect(api.workbook.shown.getEntryText(CellAddress.parse('A2'))).toBe('two');
        expect([api.cellFormatAt(CellAddress.parse('A1')).font.bold, api.cellFormatAt('Sheet1', CellAddress.parse('A2')).font.bold]).toEqual([false, false]);
        unmount();
        expect(() => api.workbook).toThrow(/not mounted/u);
    });

    it('ADR-0018: unmounted, the Sheet is destroyed and its element emptied; under StrictMode one Sheet stands', () => {
        const { container, unmount } = render(createElement(StrictMode, null, createElement(ExSheet, { locale: 'en-US' })));
        const root = container.firstElementChild;
        expect(root?.querySelectorAll('[role="grid"]')).toHaveLength(1);
        unmount();
        expect(root?.childElementCount).toBe(0);
    });

    it('ADR-0390: chrome.toolbarItem draws the items it answers for through a portal, null leaves the rest built in, and the view it subscribes to follows the Sheet', async () => {
        const { container, rerender, unmount } = render(createElement(StrictMode, null,
            createElement(ExSheet, { locale: 'en-US', showToolbar: true, chrome: { toolbarItem } })));
        await act(async () => { await Promise.resolve(); });
        const drawn = (): string[] => [...container.querySelectorAll('.ex-sheet-toolbar-slot .own-list')].map(button => button.textContent);
        expect(drawn()).toEqual(['Font Colour', 'Fill Colour', 'Borders']);
        expect(container.querySelector('.ex-sheet-toolbar-item[aria-label="Bold"]')).not.toBeNull();
        const buttons = [...container.querySelectorAll('.own-list')];
        // The same render function handed again draws nothing again.
        rerender(createElement(StrictMode, null, createElement(ExSheet, { locale: 'en-US', showToolbar: true, viewportHeight: 280, chrome: { toolbarItem } })));
        await act(async () => { await Promise.resolve(); });
        expect([...container.querySelectorAll('.own-list')]).toEqual(buttons);
        // ↓ on the band asks for the Fill's list, which the view says.
        const band = container.querySelector<HTMLElement>('.ex-sheet-toolbar');
        act(() => { band?.focus(); });
        for (let i = 0; i < 5; i++) band?.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }));
        act(() => { band?.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true })); });
        await act(async () => { await Promise.resolve(); });
        expect([...container.querySelectorAll('.own-list')].map(button => button.getAttribute('aria-expanded'))).toEqual(['false', 'true', 'false']);
        unmount();
        expect(buttons.map(button => button.isConnected)).toEqual([false, false, false]);
    });

    it('ADR-0100: an own button\'s React content is rendered in place of its text, kept in its place across renders, and shows the node handed last', async () => {
        const { container, rerender, unmount } = render(createElement(StrictMode, null, createElement(ExSheet, { locale: 'en-US', showToolbar: true, toolbar: reviewRow('A') })));
        await act(async () => { await Promise.resolve(); });
        const face = container.querySelector('.ex-sheet-toolbar-item[aria-label="Approve"]');
        const place = face?.querySelector('.ex-sheet-toolbar-content');
        expect([place?.querySelector('.own-icon')?.textContent, face?.querySelector('.ex-sheet-toolbar-text')]).toEqual(['A', null]);
        // A new toolbar with a new node: the face keeps its place, which renders the new node.
        rerender(createElement(StrictMode, null, createElement(ExSheet, { locale: 'en-US', showToolbar: true, toolbar: reviewRow('B') })));
        await act(async () => { await Promise.resolve(); });
        expect([container.querySelector('.ex-sheet-toolbar-content'), place?.querySelector('.own-icon')?.textContent]).toEqual([place, 'B']);
        unmount();
        expect(place?.isConnected).toBe(false);
    });
});
