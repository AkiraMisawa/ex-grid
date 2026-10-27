// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): work of the Consumer's in the Input Order (ADR-0240 sections 6 and 12, ADR-0330
// section 10). A command no input of the grid's made — a file opened from a button outside it — takes one place in
// the order; the inputs made while it is out wait and are taken after it, and an Escape whose keydown sees cancellable
// work placed cancels it. Each work here is held by the test and ended when the test says, so "typed at once" and
// "typed after it ended" are the two orders spine rule 6 says must end the same.
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridSelection } from '../src/selection/selection';
import type { GridView } from '../src/dom/grid-view';
import { mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

/** Work the test ends when it chooses, with the signal it was handed. */
interface Held {
    readonly signal: AbortSignal;
    readonly land: () => void;
    readonly fail: (error: unknown) => void;
}

/** Work that waits for the test, as a file still being read does. */
function heldWork(works: Held[], landing: () => void = () => undefined): (signal: AbortSignal) => Promise<void> {
    return signal => new Promise<void>((resolve, reject) => {
        works.push({ signal, land: () => { landing(); resolve(); }, fail: reject });
    });
}

/** Work that looks at its signal up to the point where it lands, as the C# contract asks. */
function lookingWork(works: Held[], landing: () => void = () => undefined): (signal: AbortSignal) => Promise<void> {
    return signal => new Promise<void>((resolve, reject) => {
        works.push({
            signal,
            land: () => {
                resolve(Promise.resolve().then(() => {
                    signal.throwIfAborted();
                    landing();
                }));
            },
            fail: reject,
        });
    });
}

const settled = async (): Promise<void> => {
    for (let i = 0; i < 4; i++) await painted();
};

function grid(): { readonly root: HTMLElement; readonly view: GridView<Trade>; readonly selections: GridSelection[] } {
    const selections: GridSelection[] = [];
    const { root, view } = mount<Trade>({
        columns: [
            { name: 'Book', type: 'text', value: trade => trade.book, width: 120 },
            { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100 },
        ],
        rows: trades(20), rowHeight: 24, viewportHeight: 480,
        onSelectionChanged: selection => { selections.push(selection); },
    });
    return { root, view, selections };
}

/** A key typed where the keyboard is, as the browser types it. */
function key(name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    return event;
}

const stamp = (root: HTMLElement): HTMLElement => q(root, '[data-ex-answered]');

/** Places the Focus on `row`'s first cell, as a click leaves it. */
function placeOn(view: GridView<Trade>, row: number): void {
    view.placeSelection({ topRow: row, leftColumn: 0, rowCount: 1, columnCount: 1 }, { row, column: 0 }, view.readView().rowSequenceVersion);
}
const focusRow = (selections: readonly GridSelection[]): number | undefined => selections.at(-1)?.focus?.row;

describe('work of the Consumer\'s in the Input Order', () => {
    it('ADR-0240 section 6: keys typed while the work is out wait, and are taken against what it left', async () => {
        // Typed at once, while the work is out.
        const atOnce = grid();
        await painted();
        atOnce.view.focus();
        const works: Held[] = [];
        const ran = atOnce.view.inOrder(() => heldWork(works, () => { placeOn(atOnce.view, 5); })(new AbortController().signal));
        expect(key('ArrowDown').defaultPrevented).toBe(true);
        key('ArrowDown');
        await settled();
        expect(focusRow(atOnce.selections)).toBe(undefined);
        expect(atOnce.root.hasAttribute('data-ex-settled')).toBe(false);
        defined(works[0], 'the work').land();
        await ran;
        await settled();

        // Typed after it ended.
        const after = grid();
        await painted();
        after.view.focus();
        await after.view.inOrder(() => { placeOn(after.view, 5); });
        key('ArrowDown');
        key('ArrowDown');
        await settled();

        expect(focusRow(atOnce.selections)).toBe(7);
        expect(focusRow(after.selections)).toBe(7);
        expect(atOnce.root.hasAttribute('data-ex-settled')).toBe(after.root.hasAttribute('data-ex-settled'));
    });

    it('ADR-0240 section 12: while cancellable work is placed the stamp names it, and under way the root is busy', async () => {
        const { root, view } = grid();
        await painted();
        expect(stamp(root).hasAttribute('data-ex-escape-leaves')).toBe(true);
        const works: Held[] = [];
        const ran = view.cancellableInOrder(heldWork(works), () => { throw new Error('No Escape was typed.'); });
        await painted();
        expect([stamp(root).getAttribute('data-ex-work-placed'), stamp(root).hasAttribute('data-ex-escape-leaves')]).toEqual(['1', false]);
        expect([root.getAttribute('data-ex-work'), root.getAttribute('aria-busy')]).toEqual(['1', 'true']);
        defined(works[0], 'the work').land();
        expect(await ran).toBe(true);
        await painted();
        expect([stamp(root).hasAttribute('data-ex-work-placed'), root.hasAttribute('data-ex-work'), root.hasAttribute('aria-busy')]).toEqual([false, false, false]);
        expect(stamp(root).hasAttribute('data-ex-escape-leaves')).toBe(true);
    });

    it('ADR-0240 section 12: an Escape cancels the work, does nothing else, and the keys before it are taken after', async () => {
        const { view, selections } = grid();
        await painted();
        view.focus();
        placeOn(view, 0);
        const works: Held[] = [];
        const tooLate: boolean[] = [];
        const ran = view.cancellableInOrder(lookingWork(works), landed => { tooLate.push(landed); });
        key('ArrowDown');
        expect(key('Escape').defaultPrevented).toBe(true);
        key('Escape', { repeat: true });
        const work = defined(works[0], 'the work');
        expect(work.signal.aborted).toBe(true);
        work.land();
        expect(await ran).toBe(false);
        await settled();
        expect([focusRow(selections), tooLate]).toEqual([1, []]);
    });

    it('ADR-0240 section 12: an Escape that finds the work landed is said too late, and a second one nothing to cancel', async () => {
        const { view } = grid();
        await painted();
        view.focus();
        const landedWorks: Held[] = [];
        const tooLate: boolean[] = [];
        // The work is past the point where it lands: the signal no longer stops it.
        const landing = view.cancellableInOrder(heldWork(landedWorks), landed => { tooLate.push(landed); });
        key('Escape');
        key('Escape');
        defined(landedWorks[0], 'the work').land();
        expect(await landing).toBe(true);
        await settled();
        expect(tooLate).toEqual([true, true]);

        // Cancelled by the first Escape, the second finds nothing to cancel, answered once the work has ended.
        const cancelledWorks: Held[] = [];
        tooLate.length = 0;
        const cancelling = view.cancellableInOrder(lookingWork(cancelledWorks), landed => { tooLate.push(landed); });
        key('Escape');
        key('Escape');
        expect(tooLate).toEqual([]);
        defined(cancelledWorks[0], 'the work').land();
        expect(await cancelling).toBe(false);
        await settled();
        expect(tooLate).toEqual([false]);
    });

    it('ADR-0240 section 12: work placed behind work under way waits its turn, and a second Escape cancels it before it starts', async () => {
        const { view } = grid();
        await painted();
        view.focus();
        const works: Held[] = [];
        const first = view.cancellableInOrder(lookingWork(works), () => undefined);
        const second = view.cancellableInOrder(lookingWork(works), () => undefined);
        expect(works.length).toBe(1);
        key('Escape');
        key('Escape');
        defined(works[0], 'the first').land();
        expect(await first).toBe(false);
        await settled();
        // Its turn comes with its signal aborted already: it ends cancelled, changing nothing.
        const next = defined(works[1], 'the second');
        expect(next.signal.aborted).toBe(true);
        next.land();
        expect(await second).toBe(false);
    });

    it('ADR-0240 section 6: an Escape behind work no Escape cancels is an ordinary key, taken after it', async () => {
        const { root, view } = grid();
        await painted();
        view.focus();
        const works: Held[] = [];
        const ran = view.inOrder(() => heldWork(works)(new AbortController().signal));
        expect(stamp(root).hasAttribute('data-ex-work-placed')).toBe(false);
        expect(key('Escape').defaultPrevented).toBe(true);
        expect(defined(works[0], 'the work').signal.aborted).toBe(false);
        defined(works[0], 'the work').land();
        await ran;
        await settled();
        expect(root.hasAttribute('data-ex-settled')).toBe(true);
    });

    it('ADR-0240 section 12: work that fails is the caller\'s failure, and an Escape that cancelled it is told nothing to cancel', async () => {
        const { view } = grid();
        await painted();
        view.focus();
        const works: Held[] = [];
        const tooLate: boolean[] = [];
        const ran = view.cancellableInOrder(heldWork(works), landed => { tooLate.push(landed); });
        key('Escape');
        const refused = new Error('The file is not a workbook.');
        defined(works[0], 'the work').fail(refused);
        await expect(ran).rejects.toBe(refused);
        await settled();
        expect(tooLate).toEqual([false]);
    });

    it('ADR-0240 section 12: destroying the grid aborts the work under way, and work still waiting never runs', async () => {
        const { view } = grid();
        await painted();
        const works: Held[] = [];
        const first = view.cancellableInOrder(lookingWork(works), () => undefined);
        const second = view.cancellableInOrder(lookingWork(works), () => undefined);
        view.destroy();
        expect(defined(works[0], 'the first').signal.aborted).toBe(true);
        defined(works[0], 'the first').land();
        expect([await first, await second, works.length]).toEqual([false, false, 1]);
        expect(await view.inOrder(() => { throw new Error('A destroyed grid runs nothing.'); })).toBe(undefined);
    });

    it('ADR-0240 section 12: cancellable work placed while cancellable work starts is refused by name', async () => {
        const { view } = grid();
        await painted();
        let asked = false;
        await view.cancellableInOrder(() => {
            asked = true;
            expect(() => view.cancellableInOrder(() => undefined, () => undefined)).toThrow('one Escape could not say which it cancels');
        }, () => undefined);
        expect(asked).toBe(true);
    });
});
