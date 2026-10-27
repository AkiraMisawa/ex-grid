// @vitest-environment happy-dom
// Layer 2 (ADR-0390 section 7): the filter panel's Chrome slot, as the core holds it. The slot stands under the
// column's commands, between the core's sentinels, and is handed what the panel shows and the choices that change
// it: what a choice means, what OK applies, the letters and where the keyboard goes are the core's, as the built-in
// panel's are (ADR-0009, ADR-0044). The contents here are a plain Chrome written against the context alone.
import { Temporal } from 'temporal-polyfill';
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridChromeSlot, GridFilterPanelSlotContext } from '../src/dom/grid-chrome';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import type { FilterSpec } from '../src/query/filter';
import { GridSource } from '../src/source/grid-source';
import { mount, painted, q, unmountAll } from './grid-harness';

afterEach(unmountAll);

interface Trade {
    readonly trader: string | null;
    readonly id: number;
}

const traders = ['Novak', 'Osei', null, 'Ishikawa'];
const rows: readonly Trade[] = Array.from({ length: 40 }, (_, i) => ({ trader: defined(traders.at(i % traders.length), 'a trader'), id: i }));
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Trader', type: 'text', value: trade => trade.trader, width: 120, filterUi: 'valueList' },
    { name: 'Id', type: 'number', value: trade => trade.id, width: 80 },
];

/** What the plain Chrome's panel was handed and did. */
interface Drawn {
    readonly contexts: GridFilterPanelSlotContext[];
    cleanups: number;
}

/**
 * A filter panel drawn by a Chrome of its own, as a Wrapper draws one: it draws the view again at each change, hands
 * each choice to its setter, and answers each focus request through takeFocus. Its value list's keys go to the core.
 */
function plainPanel(drawn: Drawn): GridChromeSlot<GridFilterPanelSlotContext> {
    return (slot, context) => {
        drawn.contexts.push(context);
        const previous = drawn.contexts.at(-2);
        // Drawn whole at each change, and the control that held the keyboard holds it again, as a framework keeps it.
        const draw = (): void => {
            const held = slot.contains(document.activeElement) ? document.activeElement?.className : undefined;
            const view = context.view();
            const controls: HTMLElement[] = [];
            if (view.kind === 'valueList') {
                const search = document.createElement('input');
                search.className = 'plain-search';
                search.value = view.search;
                search.addEventListener('input', () => { context.setSearch(search.value); });
                const list = document.createElement('div');
                list.className = 'plain-list';
                list.addEventListener('keydown', event => {
                    if (context.valueListKey(event.key, event.ctrlKey || event.altKey || event.metaKey)) event.preventDefault();
                });
                for (const entry of view.entries) {
                    const box = document.createElement('input');
                    box.type = 'checkbox';
                    box.className = 'plain-entry';
                    box.checked = entry.checked;
                    box.title = entry.blank ? 'Empty' : entry.text;
                    box.addEventListener('click', () => { context.setChecked(entry.value, box.checked); });
                    list.append(box);
                }
                controls.push(search, list);
            } else {
                const operand = document.createElement('input');
                operand.className = 'plain-operand';
                operand.value = view.first.operand;
                operand.addEventListener('input', () => { context.setOperand(operand.value); });
                const refusal = document.createElement('p');
                refusal.className = 'plain-refusal';
                if (view.first.refusal !== undefined) refusal.textContent = view.first.refusal;
                controls.push(operand, refusal);
            }
            const ok = document.createElement('button');
            ok.className = 'plain-ok';
            ok.disabled = !view.canApply;
            ok.addEventListener('click', () => { context.apply(); });
            controls.push(ok);
            slot.replaceChildren(...controls);
            if (held !== undefined) slot.querySelector<HTMLElement>(`.${held}`)?.focus();
        };
        draw();
        const stop = context.subscribe(draw);
        // A request the contents have not answered before takes the keyboard, onto the control it names.
        if (context.focusRequest !== 0 && context.focusRequest !== previous?.focusRequest) {
            const target = context.focusTarget === 'first' ? slot.firstElementChild
                : context.focusTarget === 'last' ? slot.lastElementChild : slot.querySelector('.plain-search, .plain-operand');
            if (target instanceof HTMLElement) context.takeFocus(target);
        }
        return () => {
            drawn.cleanups += 1;
            stop();
            slot.replaceChildren();
        };
    };
}

/** Contents whose cleanup throws. */
const uncleanable: GridChromeSlot<GridFilterPanelSlotContext> = () => () => { throw new Error('cleaned nowhere'); };

function setUp(options: Partial<GridOptions<Trade>> = {}): { readonly root: HTMLElement; readonly source: GridSource<Trade>; readonly drawn: Drawn } {
    const drawn: Drawn = { contexts: [], cleanups: 0 };
    const source = GridSource.from(rows);
    const { root } = mount<Trade>({
        columns, source, rowHeight: 24, viewportHeight: 480, viewportWidth: 720, temporal: Temporal,
        chrome: { filterPanel: plainPanel(drawn) }, ...options,
    });
    return { root, source, drawn };
}

function mouse(target: Element, type: string): void {
    target.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button: 0 }));
}
function click(target: Element): void {
    mouse(target, 'mousedown');
    mouse(target, 'mouseup');
    (target as HTMLElement).click();
}
function key(target: Element, name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    return event;
}
function typeInto(field: HTMLInputElement, text: string): void {
    field.value = text;
    field.dispatchEvent(new Event('input', { bubbles: true }));
}
function active(): Element {
    const holder = document.activeElement;
    if (holder === null) throw new Error('Nothing holds the keyboard.');
    return holder;
}
async function settle(): Promise<void> {
    for (let i = 0; i < 5; i++) await painted();
}
async function open(root: HTMLElement, column: number): Promise<void> {
    root.focus();
    click(defined(root.querySelectorAll('.ex-header-cell > .ex-menu-button')[column], `column ${column}'s ▾`));
    await settle();
}
const field = (root: HTMLElement, selector: string): HTMLInputElement => q(root, `.ex-popover-filter ${selector}`) as HTMLInputElement;
const answered = (root: HTMLElement): number => Number(q(root, '[data-ex-answered]').getAttribute('data-ex-answered'));
const spec = (source: GridSource<Trade>, column: string): FilterSpec | undefined => source.snapshot().filter?.columns[column];
const last = (drawn: Drawn): GridFilterPanelSlotContext => defined(drawn.contexts.at(-1), 'a mount');

describe('the filter panel\'s slot', () => {
    it('ADR-0390 / ADR-0009: the slot is the filter the core places under the commands, handed the column, the spec in force and the core\'s view', async () => {
        const { root, drawn } = setUp();
        await settle();
        await open(root, 0);
        const popover = q(root, ':scope > .ex-popover');
        expect([...popover.children].map(child => child.className)).toEqual(['ex-popover-commands', 'ex-focus-wrap', 'ex-popover-filter', 'ex-focus-wrap']);
        const context = last(drawn);
        expect([context.column, context.columnType, context.mode, context.current, context.focusRequest, context.focusTarget])
            .toEqual(['Trader', 'text', 'valueList', undefined, 0, undefined]);
        const view = context.view();
        expect(view.kind === 'valueList' && view.entries.map(entry => [entry.text, entry.blank, entry.checked])).toEqual([
            ['Novak', false, true], ['Osei', false, true], ['(Blanks)', true, true], ['Ishikawa', false, true],
        ]);
        // The commands hold the keyboard at the opening: the panel is asked for nothing.
        expect(active().getAttribute('role')).toBe('menuitem');
    });

    it('ADR-0009 / ADR-0220: each choice is one input answered and a new view; OK applies the core\'s answer, closes and gives the keyboard back', async () => {
        const { root, source, drawn } = setUp();
        await settle();
        await open(root, 0);
        const before = last(drawn).view();
        const counted = answered(root);
        typeInto(field(root, '.plain-search'), 'o');
        await settle();
        const after = last(drawn).view();
        expect([after === before, after === last(drawn).view(), answered(root) - counted]).toEqual([false, true, 1]);
        expect(after.kind === 'valueList' && after.entries.map(entry => entry.text)).toEqual(['Novak', 'Osei']);
        click(defined(root.querySelectorAll('.plain-entry')[1], 'Osei'));
        click(q(root, '.plain-ok'));
        await settle();
        expect([spec(source, 'Trader'), root.querySelector('.ex-popover'), active(), drawn.cleanups]).toEqual([
            { clauses: [{ operator: 'in', values: ['Novak'] }] }, null, root, 1,
        ]);
    });

    it('ADR-0009 / ADR-0023: a typed value that does not read applies nothing, and the view says why', async () => {
        const { root, source } = setUp();
        await settle();
        await open(root, 1);
        typeInto(field(root, '.plain-operand'), '12abc');
        click(q(root, '.plain-ok'));
        await settle();
        expect([spec(source, 'Id'), root.querySelector('.ex-popover') !== null, q(root, '.plain-refusal').textContent.length > 0]).toEqual([undefined, true, true]);
    });

    it('ADR-0009 / FN-18: an operator the core did not offer the column is refused by name', async () => {
        const { root, drawn } = setUp();
        await settle();
        await open(root, 1);
        expect(() => { last(drawn).setOperator('contains'); }).toThrow("'contains' is not an operator this column's filter offers");
    });

    it('ADR-0044: Tab, Shift+Tab and E on a command ask the contents for the keyboard on the same slot, and Tab off its end goes back to the commands', async () => {
        const { root, drawn } = setUp();
        await settle();
        await open(root, 0);
        const command = active();
        key(command, 'Tab');
        expect([last(drawn).focusRequest, last(drawn).focusTarget, drawn.cleanups, active().className]).toEqual([1, 'first', 1, 'plain-search']);
        q(root, '.ex-popover > .ex-focus-wrap:last-child').focus();
        expect(active()).toBe(command);
        key(command, 'Tab', { shiftKey: true });
        expect([last(drawn).focusRequest, last(drawn).focusTarget, active().className]).toEqual([2, 'last', 'plain-ok']);
        q(root, '.ex-popover > .ex-focus-wrap:last-child').focus();
        key(active(), 'e');
        expect([last(drawn).focusRequest, last(drawn).focusTarget, active().className]).toEqual([3, 'search', 'plain-search']);
    });

    it('ADR-0044: Excel\'s letters on the value list run their command through the core; another key is the list\'s own', async () => {
        const { root } = setUp();
        await settle();
        await open(root, 0);
        const box = q(root, '.plain-entry');
        box.focus();
        expect(key(box, 'x').defaultPrevented).toBe(false);
        expect(key(box, 's').defaultPrevented).toBe(true);
        await settle();
        expect([root.querySelector('.ex-popover'), root.querySelectorAll('.ex-header-cell')[0]?.getAttribute('aria-sort')]).toEqual([null, 'ascending']);
    });

    it('ADR-0009: the value list arriving while the keyboard is in the panel asks the contents to put it on the new form\'s first control', async () => {
        let answer: ((values: { kind: 'values'; values: readonly unknown[] }) => void) | undefined;
        const source = GridSource.from(rows);
        const slow = new Proxy(source, {
            get: (target, member): unknown => {
                if (member === 'distinctValues') return (): Promise<unknown> => new Promise(resolve => { answer = resolve; });
                const held: unknown = Reflect.get(target, member);
                return typeof held === 'function' ? held.bind(target) : held;
            },
        });
        const { root, drawn } = setUp({ source: slow });
        await settle();
        await open(root, 0);
        expect(last(drawn).view().kind).toBe('conditions');
        key(active(), 'Tab');
        expect(active().className).toBe('plain-operand');
        defined(answer, 'the request')({ kind: 'values', values: ['Novak'] });
        await settle();
        expect([last(drawn).view().kind, last(drawn).focusRequest, active().className]).toEqual(['valueList', 2, 'plain-search']);
    });

    it('ADR-0039: while the contents\' own popup is open, Escape is the popup\'s; once it closes, Escape closes the popover', async () => {
        const { root, drawn } = setUp();
        await settle();
        await open(root, 0);
        key(active(), 'Tab');
        last(drawn).innerPopupChanged(true);
        key(active(), 'Escape');
        expect(root.querySelector('.ex-popover')).not.toBeNull();
        last(drawn).innerPopupChanged(false);
        key(active(), 'Escape');
        expect([root.querySelector('.ex-popover'), active()]).toEqual([null, root]);
    });

    it('ADR-0390: the contents go with the popover, and a choice made after reaches nothing', async () => {
        const { root, source, drawn } = setUp();
        await settle();
        await open(root, 0);
        const context = last(drawn);
        context.cancel();
        await settle();
        expect([root.querySelector('.ex-popover'), drawn.cleanups, active()]).toEqual([null, 1, root]);
        context.toggleAll();
        context.apply();
        expect(spec(source, 'Trader')).toBeUndefined();
    });

    it('ADR-0010 / KB-31: the slot is a form, and its submission, Enter in a text field, applies exactly what OK applies', async () => {
        const { root, source } = setUp();
        await settle();
        await open(root, 1);
        typeInto(field(root, '.plain-operand'), '12');
        const form = q(root, '.ex-popover > form.ex-popover-filter');
        expect(form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))).toBe(false);
        await settle();
        expect([spec(source, 'Id'), root.querySelector('.ex-popover')]).toEqual([{ clauses: [{ operator: 'equals', value: 12 }] }, null]);
    });

    it('ADR-0009: a value the list does not hold is refused by name, not ticked unseen', async () => {
        const { root, drawn } = setUp();
        await settle();
        await open(root, 0);
        expect(() => { last(drawn).setChecked('Nobody', false); }).toThrow('Nobody is not one of the values the list holds');
    });

    it('ADR-0204: a request the contents left unanswered is not owed once the user has made an input in the panel', async () => {
        const drawn: Drawn = { contexts: [], cleanups: 0 };
        // Contents that draw a field and never answer a request.
        const deaf: GridChromeSlot<GridFilterPanelSlotContext> = (slot, context) => {
            drawn.contexts.push(context);
            const operand = document.createElement('input');
            slot.append(operand);
            return () => { operand.remove(); };
        };
        const { root } = setUp({ chrome: { filterPanel: deaf } });
        await settle();
        await open(root, 1);
        key(active(), 'Tab');
        last(drawn).setOperand('1');
        const page = document.body.appendChild(document.createElement('input'));
        page.focus();
        last(drawn).takeFocus(q(root, '.ex-popover-filter input'));
        expect(active()).toBe(page);
        page.remove();
    });

    it('ADR-0390: a cleanup of the Consumer\'s that throws as the popover closes still leaves no part of it standing', async () => {
        const { root } = setUp({ chrome: { filterPanel: uncleanable } });
        await settle();
        await open(root, 0);
        expect(() => { key(active(), 'Escape'); }).toThrow('cleaned nowhere');
        expect([root.querySelector('.ex-popover'), active()]).toEqual([null, root]);
    });

    it('ADR-0350 section 9: contents that cannot be drawn leave no popover standing', async () => {
        const { root } = setUp({ chrome: { filterPanel: () => { throw new Error('drawn nowhere'); } } });
        await settle();
        root.focus();
        expect(() => { click(q(root, '.ex-header-cell > .ex-menu-button')); }).toThrow('drawn nowhere');
        expect([root.querySelector('.ex-popover'), active()]).toEqual([null, root]);
    });
});
