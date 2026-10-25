// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the column menu, as the C# core answers it (ADR-0010, ADR-0039, ADR-0044).
// The ▾ in a header opens its column's commands and is a toggle; Alt+↓ opens the Focus's; Excel's letters run
// the sorts and Clear filter; every command ends in the source or a Consumer's callback, and one nobody hears
// is shown disabled. However the menu closes, the keyboard is back on the grid.
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import { GridSource } from '../src/source/grid-source';
import { columns, mount, painted, q, trades, type Trade } from './grid-harness';

const base = { columns, rows: trades(100), rowHeight: 24, viewportHeight: 320, viewportWidth: 720 } as const;

function mouse(target: Element, type: string, init: MouseEventInit = {}): MouseEvent {
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, ...init });
    target.dispatchEvent(event);
    return event;
}
/** A press and its click on the ▾ of `column`, as a pointer makes them. */
function pressButton(root: HTMLElement, column: number): void {
    const button = defined(root.querySelectorAll('.ex-header-cell > .ex-menu-button')[column], `column ${column}'s ▾`);
    mouse(button, 'mousedown');
    mouse(button, 'mouseup');
    mouse(button, 'click');
}
function key(target: Element, name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    return event;
}
function active(): Element {
    const holder = document.activeElement;
    if (holder === null) throw new Error('Nothing holds the keyboard.');
    return holder;
}

const menu = (root: HTMLElement): HTMLElement | null => root.querySelector<HTMLElement>(':scope > .ex-popover > .ex-popover-commands[role=menu]');
const items = (root: HTMLElement): HTMLButtonElement[] => [...root.querySelectorAll<HTMLButtonElement>('.ex-popover button[role=menuitem]')];
/** Each item's label, with its letter in brackets where the markup underlines one, and whether it may run. */
const shown = (root: HTMLElement): string[] => items(root).map(item => {
    const label = [...item.childNodes].map(node => (node instanceof HTMLElement && node.tagName === 'U' ? `[${node.textContent}]` : node.textContent)).join('');
    return item.disabled ? `${label} (disabled)` : label;
});
const item = (root: HTMLElement, label: string): HTMLButtonElement =>
    defined(items(root).find(found => found.textContent === label), `the item ${label}`);
const ariaSorts = (root: HTMLElement): (string | null)[] =>
    [...root.querySelectorAll('.ex-header-cell')].map(cell => cell.getAttribute('aria-sort'));

describe('opening the column menu', () => {
    it('ADR-0044: the ▾ opens its column\'s commands with Excel\'s letters, its first takes the keyboard, and a second press closes it without sorting', async () => {
        const { root } = mount<Trade>(base);
        await painted();
        expect(root.querySelectorAll('.ex-header-cell > .ex-menu-button[tabindex="-1"][aria-label="Column menu"]')).toHaveLength(2);
        pressButton(root, 1);
        expect(menu(root)?.getAttribute('aria-label')).toBe('Notional');
        expect(shown(root)).toEqual([
            '[S]ort ascending', 'S[o]rt descending', '[C]lear filter (disabled)', 'Hide this column (disabled)',
            'Pin up to this column (disabled)', 'Unpin all columns (disabled)', 'Size to fit (disabled)',
        ]);
        expect(active()).toBe(item(root, 'Sort ascending'));
        // Below the header band, at the column's left edge (ADR-0040).
        expect(menu(root)?.parentElement?.getAttribute('style')).toBe('left: 120px; top: 24px; min-width: 200px; max-width: 320px; max-height: 296px');
        pressButton(root, 1);
        await painted();
        expect([menu(root), active(), ariaSorts(root)]).toEqual([null, root, ['none', 'none']]);
    });

    it('ADR-0044: the ▾ of another column opens that column\'s in its place, and a press elsewhere dismisses it', async () => {
        const { root } = mount<Trade>(base);
        await painted();
        pressButton(root, 0);
        pressButton(root, 1);
        expect([root.querySelectorAll('.ex-popover').length, menu(root)?.getAttribute('aria-label')]).toEqual([1, 'Notional']);
        mouse(q(root, '.ex-viewport'), 'mousedown');
        expect(menu(root)).toBeNull();
    });

    it('ADR-0039: Alt+↓ opens the Focus\'s column menu and never toggles it shut; with no Focus, or the menu hidden, nothing opens', async () => {
        const { root, view } = mount<Trade>(base);
        await painted();
        key(root, 'ArrowDown', { altKey: true });
        expect(menu(root)).toBeNull();
        key(root, 'ArrowRight');
        await painted();
        expect(key(root, 'ArrowDown', { altKey: true }).defaultPrevented).toBe(true);
        expect([menu(root)?.getAttribute('aria-label'), active()]).toEqual(['Book', item(root, 'Sort ascending')]);
        expect(key(root, 'ArrowDown', { altKey: true }).defaultPrevented).toBe(true);
        expect(root.querySelectorAll('.ex-popover')).toHaveLength(1);
        // Tab leaves the menu as a Cancel: no filter stands below its commands (ADR-0044).
        key(active(), 'Tab');
        expect([menu(root), active()]).toEqual([null, root]);
        view.update({ ...base, hideColumnMenu: true });
        await painted();
        key(root, 'ArrowDown', { altKey: true });
        expect([root.querySelectorAll('.ex-menu-button').length, menu(root)]).toEqual([0, null]);
    });
});

describe('the column menu\'s commands', () => {
    it('ADR-0044: S and O sort the column alone, and C clears its filter, keeping every other column\'s', async () => {
        const source = GridSource.from(trades(100));
        const { root } = mount<Trade>({ columns, source, rowHeight: 24, viewportHeight: 320, viewportWidth: 720 });
        source.setFilter({ columns: { Book: { clauses: [{ operator: 'isNotBlank' }] }, Notional: { clauses: [{ operator: 'isNotBlank' }] } } });
        await painted();
        pressButton(root, 1);
        key(active(), 'o');
        await painted();
        expect([menu(root), active(), ariaSorts(root)]).toEqual([null, root, ['none', 'descending']]);
        pressButton(root, 0);
        key(active(), 'S');
        await painted();
        expect(ariaSorts(root)).toEqual(['ascending', 'none']);
        pressButton(root, 0);
        key(active(), 'c');
        await painted();
        expect(Object.keys(defined(source.snapshot().filter, 'the filter').columns)).toEqual(['Notional']);
        // Nothing left to clear on Book: C is disabled, and runs nothing.
        pressButton(root, 0);
        expect(shown(root)[2]).toBe('[C]lear filter (disabled)');
        key(active(), 'c');
        expect(menu(root)).not.toBeNull();
    });

    it('ADR-0010: each command ends in the Consumer\'s callback, enabled only where it is heard and the grid would honour it', async () => {
        const heard: string[] = [];
        const options: GridOptions<Trade> = {
            ...base, pinnedColumnCount: 1,
            onColumnHidden: column => { heard.push(`hide ${column}`); },
            onPinnedCountChanged: count => { heard.push(`pin ${count}`); },
            onColumnWidthChanged: change => { heard.push(`width ${change.column} ${change.widthPx}`); },
        };
        const { root } = mount<Trade>(options);
        await painted();
        pressButton(root, 0);
        // Pinned up to Book already: pinning there again is no command.
        expect(shown(root).slice(3)).toEqual(['Hide this column', 'Pin up to this column (disabled)', 'Unpin all columns', 'Size to fit']);
        item(root, 'Hide this column').click();
        pressButton(root, 1);
        item(root, 'Pin up to this column').click();
        pressButton(root, 1);
        item(root, 'Unpin all columns').click();
        pressButton(root, 1);
        item(root, 'Size to fit').click();
        expect(heard).toEqual(['hide Book', 'pin 2', 'pin 0', expect.stringMatching(/^width Notional \d+$/u)]);
        expect([menu(root), active()]).toEqual([null, root]);
    });

    it('ADR-0032: a pin whose boundary would cut a Header Group is disabled', async () => {
        const three: readonly GridColumn<Trade>[] = [
            ...columns, { name: 'Quantity', type: 'number', value: trade => trade.notional / 10, width: 100 },
        ];
        const { root } = mount<Trade>({
            ...base, columns: three, headerGroups: [{ label: 'Money', columns: ['Notional', 'Quantity'] }], onPinnedCountChanged: () => undefined,
        });
        await painted();
        pressButton(root, 1);
        expect(shown(root)[4]).toBe('Pin up to this column (disabled)');
    });

    it('principle 6: a menu follows its column through a reorder, and its command runs against where the column is now', async () => {
        const pins: number[] = [];
        const options: GridOptions<Trade> = { ...base, onPinnedCountChanged: count => { pins.push(count); } };
        const { root, view } = mount<Trade>(options);
        await painted();
        pressButton(root, 0);
        view.update({ ...options, columns: columns.toReversed() });
        await painted();
        expect(menu(root)?.parentElement?.getAttribute('style')).toMatch(/^left: 100px; /u);
        item(root, 'Pin up to this column').click();
        // Book is second now: the pin reaches it.
        expect(pins).toEqual([2]);
        pressButton(root, 1);
        view.update({ ...options, columns: columns.slice(1) });
        await painted();
        expect([menu(root), active()]).toEqual([null, root]);
    });

    it('principle 6: a command no longer enabled when chosen runs nothing, and the menu is drawn again as the commands stand', async () => {
        const pins: number[] = [];
        const options: GridOptions<Trade> = { ...base, onPinnedCountChanged: count => { pins.push(count); } };
        const { root, view } = mount<Trade>(options);
        await painted();
        pressButton(root, 0);
        view.update({ ...options, pinnedColumnCount: 1 });
        await painted();
        item(root, 'Pin up to this column').click();
        expect([pins, shown(root)[4], shown(root)[5], active()]).toEqual([[], 'Pin up to this column (disabled)', 'Unpin all columns', item(root, 'Sort ascending')]);
    });

    it('ADR-0044: hidden while it stands, the column menu goes with its ▾, and an Auto header is measured again', async () => {
        const auto: readonly GridColumn<Trade>[] = [{ name: 'Book', type: 'text', value: trade => trade.book, width: { auto: true } }];
        const options: GridOptions<Trade> = { ...base, columns: auto };
        const { root, view } = mount<Trade>(options);
        await painted();
        const width = (): string | null => q(root, '.ex-header-cell').getAttribute('style');
        const withMenu = width();
        pressButton(root, 0);
        view.update({ ...options, hideColumnMenu: true });
        await painted();
        expect([menu(root), active()]).toEqual([null, root]);
        expect(width()).not.toBe(withMenu);
        view.update(options);
        await painted();
        expect(width()).toBe(withMenu);
    });

    it('ADR-0040: a grid shrunk until no row fits under the header band closes its column menu', async () => {
        const { root, view } = mount<Trade>(base);
        await painted();
        pressButton(root, 0);
        view.update({ ...base, viewportHeight: 60 });
        await painted();
        expect(menu(root)).not.toBeNull();
        view.update({ ...base, viewportHeight: 40 });
        await painted();
        expect([menu(root), active()]).toEqual([null, root]);
    });
});
