// @vitest-environment happy-dom
// Layer 2 (ADR-0410 sections 2 and 5): a menu slot's contents drawn later than their slot, as a Blazor adapter's
// arrive a round trip after the core mounts it, and wired from the attributes they write by the DOM functions both
// adapters call. The keys typed meanwhile are the menu's, answered against the place the core keeps, and the
// contents take the keyboard where that place then stands.
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { FilterPanelView } from '../src/chrome/filter-panel-model';
import type { GridChromeSlot, GridColumnMenuSlotContext, GridFilterPanelSlotContext } from '../src/dom/grid-chrome';
import { filterPanelAttributes, menuItemAttribute, wireFilterPanel, wireMenuItems } from '../src/dom/slot-wiring';
import type { FilterSpec } from '../src/query/filter';
import { GridSource } from '../src/source/grid-source';
import { columns, mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

const base = { columns, rows: trades(100), rowHeight: 24, viewportHeight: 320, viewportWidth: 720 } as const;

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
function openColumnMenu(root: HTMLElement, column: number): void {
    const button = defined(root.querySelectorAll('.ex-header-cell > .ex-menu-button')[column], `column ${column}'s ▾`);
    root.focus();
    for (const type of ['mousedown', 'mouseup', 'click']) button.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button: 0 }));
}

/** A column menu whose contents arrive when the test draws them, as a Blazor adapter's do: each mount is held, and
 * nothing is drawn in the slot until {@link Late.draw}. */
interface Late {
    readonly slots: HTMLElement[];
    readonly contexts: GridColumnMenuSlotContext[];
    cleanups: number;
    /** Draws the latest mount's contents, an item per command marked by its index, and wires them. */
    draw(): HTMLButtonElement[];
}

function lateMenu(): { readonly late: Late; readonly slot: GridChromeSlot<GridColumnMenuSlotContext> } {
    const late: Late = {
        slots: [],
        contexts: [],
        cleanups: 0,
        draw: () => {
            const slot = defined(late.slots.at(-1), 'a mounted slot');
            const context = defined(late.contexts.at(-1), 'its context');
            const items = context.commands.map((command, index) => {
                const item = document.createElement('button');
                item.type = 'button';
                item.textContent = context.label(command.id);
                item.setAttribute(menuItemAttribute, String(index));
                return item;
            });
            slot.append(...items);
            wireMenuItems(slot, context);
            return items;
        },
    };
    const slot: GridChromeSlot<GridColumnMenuSlotContext> = (element, context) => {
        late.slots.push(element);
        late.contexts.push(context);
        return () => {
            late.cleanups += 1;
            element.replaceChildren();
        };
    };
    return { late, slot };
}

function lateGrid(): { readonly root: HTMLElement; readonly late: Late } {
    const { late, slot } = lateMenu();
    const { root } = mount<Trade>({ ...base, chrome: { columnMenu: slot } });
    return { root, late };
}

describe('a menu slot\'s contents wired from their attributes', () => {
    it('ADR-0410 section 2: each marked control takes the menu\'s role and the core\'s state, and the first enabled one the keyboard', async () => {
        const { root, late } = lateGrid();
        await painted();
        openColumnMenu(root, 1);
        const items = late.draw();
        expect(items.map(item => [item.getAttribute('role'), item.tabIndex, item.getAttribute('aria-disabled'), item.getAttribute('data-ex-claims')]))
            .toEqual([
                ['menuitem', -1, 'false', 'click keydown'], ['menuitem', -1, 'false', 'click keydown'], ['menuitem', -1, 'true', null],
                ['menuitem', -1, 'true', null], ['menuitem', -1, 'true', null], ['menuitem', -1, 'true', null], ['menuitem', -1, 'true', null],
            ]);
        expect(active()).toBe(items[0]);
    });

    it('ADR-0039: a key on an item is the core\'s table against its place, and a press runs the item\'s command', async () => {
        const { root, late } = lateGrid();
        await painted();
        openColumnMenu(root, 1);
        const items = late.draw();
        expect(key(active(), 'ArrowDown').defaultPrevented).toBe(true);
        expect(active()).toBe(items[1]);
        defined(items[1], 'Sort descending').click();
        await painted();
        expect([root.querySelector('.ex-popover'), q(root, '.ex-header-cell:nth-child(2)').getAttribute('aria-sort')]).toEqual([null, 'descending']);
    });

    it('ADR-0410 section 2: a control marked with no command\'s index is refused by name', async () => {
        const { root, late } = lateGrid();
        await painted();
        openColumnMenu(root, 1);
        const slot = defined(late.slots[0], 'the slot');
        const stray = document.createElement('button');
        stray.setAttribute(menuItemAttribute, '9');
        slot.append(stray);
        expect(() => wireMenuItems(slot, defined(late.contexts[0], 'the context'))).toThrow('marked data-ex-menu-item="9"');
    });
});

describe('keys typed before a menu slot\'s contents arrive', () => {
    it('ADR-0410 section 5: End and Enter on the grid run the last enabled command, as on the items', async () => {
        const { root, late } = lateGrid();
        await painted();
        openColumnMenu(root, 1);
        expect(key(root, 'End').defaultPrevented).toBe(true);
        key(root, 'Enter');
        await painted();
        expect([root.querySelector('.ex-popover'), q(root, '.ex-header-cell:nth-child(2)').getAttribute('aria-sort')]).toEqual([null, 'descending']);
        expect(late.cleanups).toBe(late.slots.length);
    });

    it('ADR-0410 section 5: a move asks the contents again, and they take the keyboard on the item the place stands on', async () => {
        const { root, late } = lateGrid();
        await painted();
        openColumnMenu(root, 1);
        key(root, 'ArrowDown');
        const asked = defined(late.contexts.at(-1), 'the context asked again');
        expect([late.contexts.length, late.slots[1], asked.focusRequest, asked.focusItem]).toEqual([2, late.slots[0], 2, 1]);
        const items = late.draw();
        expect(active()).toBe(items[1]);
        // The contents hold the keyboard now: a key on the grid is the grid's again, and the item's keys the menu's.
        key(active(), 'ArrowUp');
        expect(active()).toBe(items[0]);
    });

    it('ADR-0410 section 5 / ADR-0044: E typed before the contents arrive puts the keyboard in the filter\'s search, and the contents leave it there', async () => {
        const { root, late } = lateGrid();
        await painted();
        openColumnMenu(root, 1);
        key(root, 'e');
        const search = active();
        expect(q(root, '.ex-popover .ex-popover-filter').contains(search)).toBe(true);
        late.draw();
        expect(active()).toBe(search);
    });
});

interface Seller {
    readonly trader: string | null;
    readonly id: number;
}

const traders: readonly (string | null)[] = ['Novak', 'Osei', null, 'Ishikawa'];
const sellers: readonly Seller[] = Array.from({ length: 40 }, (_, i) => ({ trader: defined(traders.at(i % traders.length), 'a trader'), id: i }));

/** A filter panel whose contents are drawn when the test draws them, from the view the test names, marked with the
 * attributes and wired by {@link wireFilterPanel}, as a Blazor adapter's are a round trip after the mount. */
interface LateFilter {
    readonly contexts: GridFilterPanelSlotContext[];
    readonly slots: HTMLElement[];
    /** Draws the latest mount's contents from `view` (the view standing, unless given) and wires them. */
    draw(view?: FilterPanelView): HTMLElement;
}

function lateFilterGrid(): { readonly root: HTMLElement; readonly source: GridSource<Seller>; readonly late: LateFilter } {
    const late: LateFilter = {
        contexts: [],
        slots: [],
        draw: given => {
            const slot = defined(late.slots.at(-1), 'a mounted slot');
            const context = defined(late.contexts.at(-1), 'its context');
            const view = given ?? context.view();
            const contents = document.createElement('div');
            const marked = (tag: string, attribute: string, value: string, extra: Record<string, string> = {}): HTMLElement => {
                const element = document.createElement(tag);
                element.setAttribute(attribute, value);
                for (const [name, each] of Object.entries(extra)) element.setAttribute(name, each);
                contents.append(element);
                return element;
            };
            const box = (attribute: string, value: string, checked: boolean): void => {
                const input = marked('input', attribute, value) as HTMLInputElement;
                input.type = 'checkbox';
                input.checked = checked;
            };
            if (view.kind === 'valueList') {
                marked('input', filterPanelAttributes.search, '', { [filterPanelAttributes.focus]: 'first search' });
                const list = marked('div', filterPanelAttributes.values, '');
                const all = document.createElement('input');
                all.type = 'checkbox';
                all.setAttribute(filterPanelAttributes.selectAll, '');
                list.append(all);
                view.entries.forEach((entry, index) => {
                    const input = document.createElement('input');
                    input.type = 'checkbox';
                    input.checked = entry.checked;
                    input.setAttribute(filterPanelAttributes.value, String(index));
                    list.append(input);
                });
            } else {
                const select = (which: string): void => {
                    const element = marked('select', filterPanelAttributes.operator, which) as HTMLSelectElement;
                    if (which === 'second') element.append(option('(none)', ''));
                    for (const operator of view.allowed) element.append(option(operator, operator));
                };
                select('first');
                const operand = marked('input', filterPanelAttributes.operand, 'first', { [filterPanelAttributes.focus]: 'search' }) as HTMLInputElement;
                operand.value = view.first.operand;
                for (const join of ['and', 'or']) {
                    const radio = marked('input', filterPanelAttributes.join, join) as HTMLInputElement;
                    radio.type = 'radio';
                    radio.name = 'join';
                }
                select('second');
                marked('input', filterPanelAttributes.operand, 'second');
            }
            const ok = document.createElement('button');
            ok.type = 'submit';
            contents.append(ok);
            marked('button', filterPanelAttributes.cancel, '', { [filterPanelAttributes.focus]: 'last', type: 'button' });
            box('data-unmarked', '', false);
            slot.replaceChildren(contents);
            wireFilterPanel(contents, context, () => view);
            return contents;
        },
    };
    const filterPanel: GridChromeSlot<GridFilterPanelSlotContext> = (element, context) => {
        late.slots.push(element);
        late.contexts.push(context);
        return () => { element.replaceChildren(); };
    };
    const source = GridSource.from(sellers);
    const { root } = mount<Seller>({
        columns: [
            { name: 'Trader', type: 'text', value: seller => seller.trader, width: 120, filterUi: 'valueList' },
            { name: 'Id', type: 'number', value: seller => seller.id, width: 80 },
        ],
        source, rowHeight: 24, viewportHeight: 480, viewportWidth: 720, chrome: { filterPanel },
    });
    return { root, source, late };
}

function option(text: string, value: string): HTMLOptionElement {
    const element = document.createElement('option');
    element.textContent = text;
    element.value = value;
    return element;
}
function changed(control: Element, value?: string | boolean): void {
    if (control instanceof HTMLSelectElement && typeof value === 'string') control.value = value;
    if (control instanceof HTMLInputElement && typeof value === 'boolean') control.checked = value;
    control.dispatchEvent(new Event('change', { bubbles: true }));
}
function typed(control: Element, text: string): void {
    (control as HTMLInputElement).value = text;
    control.dispatchEvent(new Event('input', { bubbles: true }));
}
function submitted(contents: HTMLElement): void {
    q(contents, 'button[type=submit]').click();
}
/** The value list the source answers, a few frames after the panel asked for it. */
async function arrived(): Promise<void> {
    for (let i = 0; i < 5; i++) await painted();
}
const filterOf = (source: GridSource<Seller>, column: string): FilterSpec | undefined => source.snapshot().filter?.columns[column];

describe('a filter panel slot\'s contents wired from their attributes', () => {
    it('ADR-0410 section 2: a value\'s checkbox ticks the entry it was drawn for, though a search typed since has moved the list, and OK applies', async () => {
        const { root, source, late } = lateFilterGrid();
        await painted();
        openColumnMenu(root, 0);
        await arrived();
        const drawn = defined(late.contexts.at(-1), 'the panel').view();
        const contents = late.draw(drawn);
        // The search moves the core's list to Osei and Ishikawa; the checkbox the user pressed was drawn for Ishikawa.
        typed(q(contents, `[${filterPanelAttributes.search}]`), 'i');
        changed(q(contents, `[${filterPanelAttributes.value}="3"]`), false);
        submitted(contents);
        await painted();
        expect([filterOf(source, 'Trader'), root.querySelector('.ex-popover')]).toEqual([{ clauses: [{ operator: 'in', values: ['Osei'] }] }, null]);
    });

    it('ADR-0410 section 2: the condition form\'s operators, values and join are read from the controls drawn, and the second\'s empty option is none', async () => {
        const { root, source, late } = lateFilterGrid();
        await painted();
        openColumnMenu(root, 1);
        const contents = late.draw();
        changed(q(contents, `[${filterPanelAttributes.operator}="first"]`), 'greaterThan');
        typed(q(contents, `[${filterPanelAttributes.operand}="first"]`), '30');
        changed(q(contents, `[${filterPanelAttributes.join}="or"]`), true);
        changed(q(contents, `[${filterPanelAttributes.operator}="second"]`), 'lessThan');
        typed(q(contents, `[${filterPanelAttributes.operand}="second"]`), '2');
        changed(q(contents, `[${filterPanelAttributes.operator}="second"]`), '');
        submitted(contents);
        await painted();
        expect(filterOf(source, 'Id')).toEqual({ clauses: [{ operator: 'greaterThan', value: 30 }] });
    });

    it('ADR-0044: a letter on the value list is the core\'s, and E asks the contents again for the search box, which takes the keyboard', async () => {
        const { root, late } = lateFilterGrid();
        await painted();
        openColumnMenu(root, 0);
        await arrived();
        const contents = late.draw();
        const value = q(contents, `[${filterPanelAttributes.value}="0"]`);
        expect(key(value, 'e').defaultPrevented).toBe(true);
        const asked = defined(late.contexts.at(-1), 'the panel asked again');
        expect([asked.focusRequest, asked.focusTarget]).toEqual([1, 'search']);
        const again = late.draw();
        expect(active()).toBe(q(again, `[${filterPanelAttributes.search}]`));
    });

    it('ADR-0009: Cancel closes the panel and applies nothing, and an operator the column does not offer is refused by name', async () => {
        const { root, source, late } = lateFilterGrid();
        await painted();
        openColumnMenu(root, 1);
        const contents = late.draw();
        const operator = q(contents, `[${filterPanelAttributes.operator}="first"]`) as HTMLSelectElement;
        operator.append(option('forged', 'forged'));
        expect(() => { changed(operator, 'forged'); }).toThrow("'forged' is not an operator this column's filter offers.");
        q(contents, `[${filterPanelAttributes.cancel}]`).click();
        await painted();
        expect([filterOf(source, 'Id'), root.querySelector('.ex-popover')]).toEqual([undefined, null]);
    });
});
