// @vitest-environment happy-dom
// The Escape that leaves the grid (ADR-0070, ADR-0012; DC-62): `onLeave` is raised beside the release of Tab, the
// release first, only by the outermost Escape, and once per press.
import { describe, expect, it } from 'vitest';
import { actionColumn, type GridColumn } from '../src/dom/grid-column';
import { mount, painted, q, trades, type Trade } from './grid-harness';

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100 },
];
// After Book and Notional, at 220 to 420: Space enters it, Escape leaves it (ADR-0037).
const review = actionColumn<Trade>('Review', [{ name: 'approve', label: 'Approve' }, { name: 'query', label: 'Query' }], { width: 200 });

/** A key typed where the keyboard is. */
function key(name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(event);
    return event;
}

/** A press on the first row, on Book unless told another left, as the browser lands it. */
function pressFirstCell(root: HTMLElement, left = 10): void {
    for (const name of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mouseup' ? 0 : 1 });
        Object.defineProperty(event, 'offsetX', { value: left });
        Object.defineProperty(event, 'offsetY', { value: 5 });
        q(root, '.ex-viewport').dispatchEvent(event);
        if (name === 'mousedown' && !event.defaultPrevented) q(root, '.ex-scroller').focus();
    }
}

/** A grid that edits, its first cell pressed. `probe` has the handler type a Tab to find whether the release was
 * made before it ran, which ends the release; otherwise it only counts. */
async function grid(probe = false, declared = columns): Promise<{ readonly root: HTMLElement; readonly left: string[] }> {
    const left: string[] = [];
    const { root } = mount<Trade>({
        columns: declared, rows: trades(20), rowHeight: 24, viewportHeight: 480, onEdit: () => { /* the edit is not the test's */ },
        onLeave: () => { left.push(probe ? (key('Tab').defaultPrevented ? 'Tab held' : 'Tab released') : 'left'); },
    });
    await painted();
    pressFirstCell(root);
    await painted();
    return { root, left };
}

const holder = (): Element | null => document.activeElement;

describe('onLeave (ADR-0070, DC-62)', () => {
    it('DC-62: an Escape with nothing to dismiss releases Tab, then raises onLeave once, and the grid keeps DOM focus', async () => {
        const { root, left } = await grid(true);
        const before = holder();
        expect(root.contains(before)).toBe(true);

        expect(key('Escape').defaultPrevented).toBe(true);

        expect(left).toEqual(['Tab released']);
        expect(holder()).toBe(before);
    });

    it('DC-62: a held Escape\'s repeats raise nothing more and leave the release standing', async () => {
        const { left } = await grid();
        key('Escape');

        key('Escape', { repeat: true });
        key('Escape', { repeat: true });

        expect(left).toEqual(['left']);
        expect(key('Tab').defaultPrevented).toBe(false);
    });

    it('DC-62: the Escape that closes a popover peels only it, and its repeats neither leave nor release Tab', async () => {
        const { left } = await grid();
        key('F10', { shiftKey: true });
        await painted();

        key('Escape');
        await painted();
        expect(key('Escape', { repeat: true }).defaultPrevented).toBe(true);

        expect(left).toEqual([]);
        expect(key('Tab').defaultPrevented).toBe(true);
        key('Escape');
        expect(left).toEqual(['left']);
    });

    it('DC-62: the Escape that cancels an edit peels only it, and its repeats leave nothing', async () => {
        const { root, left } = await grid();
        key('F2');
        await painted();
        expect(root.querySelector('.ex-editor')).not.toBeNull();

        key('Escape');
        await painted();
        // Taken, so the grid heard it, and Tab still cycles: the repeat released nothing.
        expect(key('Escape', { repeat: true }).defaultPrevented).toBe(true);

        expect(root.querySelector('.ex-editor')).toBeNull();
        expect(left).toEqual([]);
        expect(key('Tab').defaultPrevented).toBe(true);
    });

    it('DC-62 / ADR-0230: an Escape reaching the root while an edit is open cancels it, and neither leaves nor releases Tab', async () => {
        const { root, left } = await grid();
        key('F2');
        await painted();
        expect(root.querySelector('.ex-editor')).not.toBeNull();

        root.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
        await painted();

        expect(root.querySelector('.ex-editor')).toBeNull();
        expect(left).toEqual([]);
        expect(key('Tab').defaultPrevented).toBe(true);
    });

    it('DC-62: the Escape that leaves an Interactive cell, or removes the Copied Range, peels only it', async () => {
        const { root, left } = await grid(false, [...columns, review]);
        pressFirstCell(root, 230);
        await painted();
        key(' ');
        await painted();
        expect(root.querySelector('.ex-action-chosen')).not.toBeNull();

        key('Escape');
        await painted();
        expect(root.querySelector('.ex-action-chosen')).toBeNull();
        expect(left).toEqual([]);

        const copy = new Event('copy', { bubbles: true, cancelable: true });
        Object.defineProperty(copy, 'clipboardData', { value: { setData: () => { /* the text is not the test's */ } } });
        root.dispatchEvent(copy);
        await painted();
        expect(root.querySelector('.ex-copied-range')).not.toBeNull();

        key('Escape');
        await painted();
        expect(root.querySelector('.ex-copied-range')).toBeNull();
        expect(left).toEqual([]);
        expect(key('Tab').defaultPrevented).toBe(true);

        key('Escape');
        expect(left).toEqual(['left']);
    });

    it('DC-62: a held Escape with a modifier, which no layer answers, keeps its repeats for the page', async () => {
        const { left } = await grid();
        expect(key('Escape', { ctrlKey: true }).defaultPrevented).toBe(false);

        expect(key('Escape', { repeat: true, ctrlKey: true }).defaultPrevented).toBe(false);
        expect(left).toEqual([]);
    });

    it('DC-62: a held Shift+Escape, which no layer answers on the grid, keeps its repeats for the page', async () => {
        const { left } = await grid();
        expect(key('Escape', { shiftKey: true }).defaultPrevented).toBe(false);

        expect(key('Escape', { repeat: true, shiftKey: true }).defaultPrevented).toBe(false);
        expect(left).toEqual([]);
    });

    it('ADR-0012: without onLeave, the Escape with nothing to dismiss releases Tab as before', async () => {
        const { root } = mount<Trade>({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480 });
        await painted();
        pressFirstCell(root);
        await painted();

        key('Escape');

        expect(key('Tab').defaultPrevented).toBe(false);
    });

    it('onLeave is a function, refused by name otherwise', () => {
        expect(() => mount<Trade>({ columns, rows: trades(1), onLeave: 'close' as unknown as () => void })).toThrow(/onLeave is a function/u);
    });
});
