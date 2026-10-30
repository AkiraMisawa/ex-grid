// @vitest-environment happy-dom
// Layer 2 (ADR-0390 section 7): the find panel's Chrome slot, as the core holds it. The slot stands in the core's
// popover, inside the core's form and between its sentinels, and is handed what the panel holds and the steps: the
// search, its answer, the Focus it moves and the Replace are the core's, as under the built-in panel (ADR-0055,
// ADR-0332). The contents here are a plain Chrome written against the context alone.
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridChromeSlot, GridFindPanelSlotContext } from '../src/dom/grid-chrome';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import { replacedCells, type GridReplaceResult } from '../src/finding/replace';
import type { GridSelection } from '../src/selection/selection';
import { mount, painted, q, unmountAll } from './grid-harness';

afterEach(unmountAll);

interface Deal {
    readonly book: string;
    readonly trader: string;
}

// Trader repeats every three rows through four names: rows 3-5 are Novak.
const traders = ['Ishikawa', 'Novak', 'Osei', 'Lee'];
const deals: readonly Deal[] = Array.from({ length: 200 }, (_, i) => ({ book: `B${i % 4}`, trader: defined(traders[Math.floor(i / 3) % traders.length], 'a trader') }));
const columns: readonly GridColumn<Deal>[] = [
    { name: 'Book', type: 'text', value: deal => deal.book, width: 100 },
    { name: 'Trader', type: 'text', value: deal => deal.trader, width: 120, editable: true },
];

interface Drawn {
    readonly contexts: GridFindPanelSlotContext[];
    cleanups: number;
}

/** A find panel drawn by a Chrome of its own: drawn whole at each change, each change and step handed to the core,
 * and the field focused at each request. */
function plainFind(drawn: Drawn): GridChromeSlot<GridFindPanelSlotContext> {
    return (slot, context) => {
        drawn.contexts.push(context);
        const previous = drawn.contexts.at(-2);
        const field = document.createElement('input');
        field.className = 'plain-field';
        field.addEventListener('input', () => { context.setText(field.value); });
        const matchCase = document.createElement('input');
        matchCase.type = 'checkbox';
        matchCase.className = 'plain-match-case';
        matchCase.addEventListener('click', () => { context.setMatchCase(matchCase.checked); });
        const next = document.createElement('button');
        next.type = 'button';
        next.className = 'plain-next';
        next.textContent = context.label('find-next');
        next.addEventListener('click', () => { context.next(); });
        const replaceField = document.createElement('input');
        replaceField.className = 'plain-replace-field';
        replaceField.addEventListener('input', () => { context.setReplaceText(replaceField.value); });
        const replaceAll = document.createElement('button');
        replaceAll.type = 'button';
        replaceAll.className = 'plain-replace-all';
        replaceAll.addEventListener('click', () => { context.replaceAll(); });
        const outcome = document.createElement('p');
        outcome.className = 'plain-outcome';
        const draw = (): void => {
            const view = context.view();
            if (field.value !== view.text) field.value = view.text;
            matchCase.checked = view.matchCase;
            replaceAll.disabled = view.replaceWaiting;
            outcome.textContent = view.outcome;
            replaceField.hidden = !view.replaceShown;
            replaceAll.hidden = !view.replaceShown;
        };
        slot.append(field, matchCase, next, replaceField, replaceAll, outcome);
        draw();
        const stop = context.subscribe(draw);
        if (context.focusRequest !== previous?.focusRequest) context.takeFocus(field);
        return () => {
            drawn.cleanups += 1;
            stop();
            slot.replaceChildren();
        };
    };
}

function grid(options: Partial<GridOptions<Deal>> = {}): {
    readonly root: HTMLElement; readonly view: { destroy(): void }; readonly drawn: Drawn; readonly selections: GridSelection[];
} {
    const drawn: Drawn = { contexts: [], cleanups: 0 };
    const selections: GridSelection[] = [];
    const { root, view } = mount<Deal>({
        columns, rows: deals, rowHeight: 24, viewportHeight: 320, viewportWidth: 720, findsHeaders: false,
        onSelectionChanged: next => { selections.push(next); }, chrome: { findPanel: plainFind(drawn) }, ...options,
    });
    return { root, view, drawn, selections };
}
const focusOf = (selections: readonly GridSelection[]): unknown => defined(selections.at(-1), 'the Selection heard').focus;

function key(target: Element, name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    return event;
}
function typeInto(input: HTMLInputElement, text: string): void {
    input.value = text;
    input.dispatchEvent(new Event('input', { bubbles: true }));
}
const settle = async (): Promise<void> => {
    for (let i = 0; i < 4; i++) await new Promise(resolve => { setTimeout(resolve, 0); });
};
function active(): Element {
    const holder = document.activeElement;
    if (holder === null) throw new Error('Nothing holds the keyboard.');
    return holder;
}
const field = (root: HTMLElement): HTMLInputElement => q(root, '.ex-popover-find .plain-field') as HTMLInputElement;
const last = (drawn: Drawn): GridFindPanelSlotContext => defined(drawn.contexts.at(-1), 'a mount');
/** Enter in a field: its keydown says which way, and the core's form's implicit submission steps. */
function enter(root: HTMLElement, shift = false): void {
    key(field(root), 'Enter', { shiftKey: shift });
    q(root, '.ex-popover-find form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
}

describe('the find panel\'s slot', () => {
    it('ADR-0390 / ADR-0055: the slot stands in the core\'s popover and form, handed the core\'s words; the field takes the keyboard at the opening', async () => {
        const { root, drawn } = grid();
        await painted();
        key(root, 'f', { ctrlKey: true });
        const popover = q(root, ':scope > .ex-popover-find');
        expect([popover.getAttribute('role'), popover.getAttribute('aria-label'), [...popover.children].map(child => child.className)])
            .toEqual(['dialog', 'Find', ['ex-focus-wrap', 'ex-popover-find-body ex-popover-body', 'ex-focus-wrap']]);
        expect(field(root).form).toBe(q(root, '.ex-popover-find form'));
        expect([last(drawn).focusRequest, active(), q(root, '.plain-next').textContent]).toEqual([1, field(root), 'Find next']);
    });

    it('FD-5 / ADR-0055: what is typed is the core\'s to search; Enter steps from the Focus and Shift+Enter back, and the keyboard stays in the field', async () => {
        const { root, selections } = grid();
        await painted();
        key(root, 'f', { ctrlKey: true });
        typeInto(field(root), 'novak');
        enter(root);
        await settle();
        expect(focusOf(selections)).toEqual({ row: 3, column: 1 });
        q(root, '.plain-next').click();
        await settle();
        expect(focusOf(selections)).toEqual({ row: 4, column: 1 });
        enter(root, true);
        await settle();
        expect([focusOf(selections), active()]).toEqual([{ row: 3, column: 1 }, field(root)]);
    });

    it('ADR-0055 / ADR-0220: each change is one input answered and a new view; a step that matches nothing is said in the view', async () => {
        const { root, drawn } = grid();
        await painted();
        key(root, 'f', { ctrlKey: true });
        await painted();
        const before = last(drawn).view();
        const counted = Number(q(root, '[data-ex-answered]').getAttribute('data-ex-answered'));
        typeInto(field(root), 'nobody');
        await painted();
        expect([last(drawn).view() === before, last(drawn).view().text, Number(q(root, '[data-ex-answered]').getAttribute('data-ex-answered')) - counted])
            .toEqual([false, 'nobody', 1]);
        enter(root);
        await settle();
        expect([last(drawn).view().outcome, q(root, '.plain-outcome').textContent]).toEqual(['No match', 'No match']);
    });

    it('ADR-0055 / ADR-0332 §4: what the panel holds outlives it, and Tab off either end asks the contents for the field again', async () => {
        const { root, drawn } = grid();
        await painted();
        key(root, 'f', { ctrlKey: true });
        typeInto(field(root), 'osei');
        q(root, '.plain-match-case').click();
        q(root, '.ex-popover-find > .ex-focus-wrap:last-child').focus();
        // As under the built-in panel, the field comes back as it was: its text is not selected, so typing adds to it.
        expect([last(drawn).focusRequest, active(), drawn.cleanups, field(root).selectionStart === field(root).selectionEnd]).toEqual([2, field(root), 1, true]);
        key(field(root), 'Escape');
        expect([root.querySelector('.ex-popover-find'), active(), drawn.cleanups]).toEqual([null, root, 2]);
        key(root, 'f', { ctrlKey: true });
        const view = last(drawn).view();
        expect([view.text, view.matchCase, field(root).value]).toEqual(['osei', true, 'osei']);
    });

    it('ADR-0332 §2 / §3: Ctrl+H shows Replace in the same panel; Replace All waits for the Consumer\'s answer, and the view says the count', async () => {
        let later: ((result: GridReplaceResult) => void) | undefined;
        const { root, drawn } = grid({ onEdit: () => undefined, onReplace: () => new Promise(resolve => { later = resolve; }) });
        await painted();
        key(root, 'f', { ctrlKey: true });
        expect(last(drawn).view().replaceShown).toBe(false);
        key(field(root), 'h', { ctrlKey: true });
        expect([root.querySelectorAll('.ex-popover').length, last(drawn).view().replaceShown, q(root, '.ex-popover-find').getAttribute('aria-label')])
            .toEqual([1, true, 'Find and Replace']);
        typeInto(field(root), 'novak');
        typeInto(q(root, '.plain-replace-field') as HTMLInputElement, 'Rossi');
        q(root, '.plain-replace-all').click();
        await settle();
        expect([last(drawn).view().replaceWaiting, (q(root, '.plain-replace-all') as HTMLButtonElement).disabled]).toEqual([true, true]);
        defined(later, 'the answer')(replacedCells(100, 0));
        await settle();
        expect([last(drawn).view().replaceWaiting, last(drawn).view().outcome]).toEqual([false, 'Replaced 100 cells.']);
    });

    it('ADR-0055: a submit by a button the contents drew is that button\'s own, and a Shift+Enter on a button leaves no backward step behind', async () => {
        const { root, drawn, selections } = grid();
        await painted();
        key(root, 'f', { ctrlKey: true });
        typeInto(field(root), 'novak');
        enter(root);
        await settle();
        const own = document.createElement('button');
        own.addEventListener('click', () => { last(drawn).previous(); });
        q(root, '.ex-find-slot').append(own);
        key(own, 'Enter', { shiftKey: true });
        own.click();
        await settle();
        // One step back from the first Novak, wrapping to the last: the submit the click made stepped nothing.
        expect(focusOf(selections)).toEqual({ row: 197, column: 1 });
        enter(root);
        await settle();
        expect(focusOf(selections)).toEqual({ row: 3, column: 1 });
    });

    it('ADR-0332 §3: a panel opened again while a Replace All is out waits for its answer too', async () => {
        let later: ((result: GridReplaceResult) => void) | undefined;
        const { root, drawn } = grid({ onEdit: () => undefined, onReplace: () => new Promise(resolve => { later = resolve; }) });
        await painted();
        key(root, 'h', { ctrlKey: true });
        typeInto(field(root), 'novak');
        q(root, '.plain-replace-all').click();
        key(field(root), 'Escape');
        key(root, 'h', { ctrlKey: true });
        expect([last(drawn).view().replaceWaiting, (q(root, '.plain-replace-all') as HTMLButtonElement).disabled]).toEqual([true, true]);
        defined(later, 'the answer')(replacedCells(3, 0));
        await settle();
        expect(last(drawn).view().replaceWaiting).toBe(false);
    });

    it('ADR-0039: while the contents\' own popup is open, Escape is the popup\'s', async () => {
        const { root, drawn } = grid();
        await painted();
        key(root, 'f', { ctrlKey: true });
        last(drawn).innerPopupChanged(true);
        key(field(root), 'Escape');
        expect(root.querySelector('.ex-popover-find')).not.toBeNull();
        last(drawn).innerPopupChanged(false);
        key(field(root), 'Escape');
        expect(root.querySelector('.ex-popover-find')).toBeNull();
    });

    it('ADR-0039: contents drawn again are drawn without the popup they had open, so Escape is the panel\'s again', async () => {
        const { root, drawn } = grid();
        await painted();
        key(root, 'f', { ctrlKey: true });
        last(drawn).innerPopupChanged(true);
        key(field(root), 'f', { ctrlKey: true });
        key(field(root), 'Escape');
        expect(root.querySelector('.ex-popover-find')).toBeNull();
    });

    it('ADR-0390: the contents of a panel standing as the grid goes are taken away with it', async () => {
        const { root, view, drawn } = grid();
        await painted();
        key(root, 'f', { ctrlKey: true });
        view.destroy();
        expect(drawn.cleanups).toBe(1);
    });

    it('ADR-0390: a step after the panel went reaches nothing, and contents that cannot be drawn leave no panel standing', async () => {
        const { root, drawn, selections } = grid();
        await painted();
        key(root, 'f', { ctrlKey: true });
        const context = last(drawn);
        context.close();
        expect([root.querySelector('.ex-popover-find'), active()]).toEqual([null, root]);
        context.setText('novak');
        context.next();
        await settle();
        expect(selections).toEqual([]);
        unmountAll();
        const failing = grid({ chrome: { findPanel: () => { throw new Error('drawn nowhere'); } } });
        await painted();
        expect(() => { key(failing.root, 'f', { ctrlKey: true }); }).toThrow('drawn nowhere');
        expect(failing.root.querySelector('.ex-popover')).toBeNull();
    });
});
