// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the built-in filter panel under a column's commands (ADR-0009, ADR-0044), as
// the C# core's built-in panel answers it. A value list where the column declares one and the source listed
// its values, the condition form where it declares that or the values were TooMany; Apply sends one
// FilterSpec to the source, Cancel sends nothing, and either gives the keyboard back to the grid. A typed
// value is read in the grid's locale, and refused by name where it does not read (ADR-0023, ADR-0370 section
// 10).
import { Temporal } from 'temporal-polyfill';
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import type { FilterSpec, GridFilter } from '../src/query/filter';
import { GridSource } from '../src/source/grid-source';
import { mount, painted } from './grid-harness';

interface Trade {
    readonly trader: string | null;
    readonly id: number;
    readonly notional: number;
    readonly day: Temporal.PlainDate;
}

const traders = ['Novak', 'Osei', null, 'Ishikawa'];
const rows: readonly Trade[] = Array.from({ length: 1200 }, (_, i) => ({
    trader: defined(traders.at(i % traders.length), 'a trader'), id: i, notional: i * 1000,
    day: Temporal.PlainDate.from('2026-01-01').add({ days: i % 30 }),
}));
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Trader', type: 'text', value: trade => trade.trader, width: 120, filterUi: 'valueList' },
    { name: 'Id', type: 'number', value: trade => trade.id, width: 80, filterUi: 'valueList' },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100 },
    { name: 'Day', type: 'date', dateType: 'dateOnly', value: trade => trade.day, width: 100 },
];

function setUp(options: Partial<GridOptions<Trade>> = {}): { readonly root: HTMLElement; readonly source: GridSource<Trade> } {
    const source = GridSource.from(rows);
    const { root } = mount<Trade>({ columns, source, rowHeight: 24, viewportHeight: 480, viewportWidth: 720, temporal: Temporal, ...options });
    return { root, source };
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
    return found(document.activeElement, 'what holds the keyboard');
}
function found<T>(present: T | null | undefined, what: string): T {
    if (present === null || present === undefined) throw new Error(`No ${what}.`);
    return present;
}
/** The ▾ of `column`, pressed, and the answers it is waiting on given time to land. */
async function open(root: HTMLElement, column: number): Promise<HTMLElement> {
    click(defined(root.querySelectorAll('.ex-header-cell > .ex-menu-button')[column], `column ${column}'s ▾`));
    await settle();
    return panel(root);
}
async function settle(): Promise<void> {
    for (let i = 0; i < 5; i++) await painted();
}
const panel = (root: HTMLElement): HTMLElement => found(root.querySelector<HTMLElement>(':scope > .ex-popover .ex-popover-filter'), 'filter panel');
const entries = (root: HTMLElement): (string | null)[] => [...panel(root).querySelectorAll('.ex-popover-list label')].map(label => label.textContent);
const entry = (root: HTMLElement, name: string | null): HTMLInputElement =>
    found([...panel(root).querySelectorAll('.ex-popover-list label')].find(label => label.textContent === name)?.querySelector('input'), `entry ${String(name)}`);
const operators = (root: HTMLElement): HTMLSelectElement => found(panel(root).querySelector('select'), 'operator');
const button = (root: HTMLElement, words: string): HTMLButtonElement =>
    defined([...panel(root).querySelectorAll<HTMLButtonElement>('.ex-popover-actions button')].find(each => each.textContent === words), words);
const field = (root: HTMLElement, selector: string): HTMLInputElement => found(panel(root).querySelector<HTMLInputElement>(selector), selector);
const filterOf = (source: GridSource<Trade>): GridFilter | undefined => source.snapshot().filter;
const spec = (source: GridSource<Trade>, column: string): FilterSpec | undefined => filterOf(source)?.columns[column];

describe('the value list', () => {
    it('ADR-0009/0023: every value starts ticked, the Blank among them in words, and a search narrows the list', async () => {
        const { root } = setUp();
        await painted();
        const popover = await open(root, 0);
        expect(popover.parentElement?.getAttribute('role')).toBe('dialog');
        expect(popover.parentElement?.getAttribute('aria-label')).toBe('Trader');
        expect(entries(root)).toEqual(['Novak', 'Osei', '(Blanks)', 'Ishikawa']);
        expect(entry(root, 'Osei').checked).toBe(true);
        const all = field(root, '.ex-select-all');
        expect([all.getAttribute('aria-checked'), all.textContent]).toEqual(['true', '(Select All)']);
        typeInto(field(root, 'input[type=search]'), 'nov');
        expect(entries(root)).toEqual(['Novak']);
        expect(all.textContent).toBe('(Select All Search Results)');
        // The Blank is found by its words, which is what the list shows.
        typeInto(field(root, 'input[type=search]'), 'blank');
        expect(entries(root)).toEqual(['(Blanks)']);
    });

    it('ADR-0009 / FL-11: (Select All) clears every value shown, a tick makes it mixed, and Apply sends what was ticked', async () => {
        const { root, source } = setUp();
        await painted();
        await open(root, 0);
        const all = field(root, '.ex-select-all');
        click(all);
        expect([entry(root, 'Novak').checked, all.getAttribute('aria-checked')]).toEqual([false, 'false']);
        // Nothing ticked is no filter Apply may hand on (ticket 76).
        expect(button(root, 'OK').disabled).toBe(true);
        click(entry(root, 'Novak'));
        click(entry(root, '(Blanks)'));
        expect([all.getAttribute('aria-checked'), button(root, 'OK').disabled]).toEqual(['mixed', false]);
        click(button(root, 'OK'));
        await settle();
        expect(spec(source, 'Trader')).toEqual({ clauses: [{ operator: 'in', values: ['Novak', null] }] });
        expect(root.querySelector('.ex-popover')).toBeNull();
        expect(active()).toBe(root);
    });

    it('ADR-0009/0023, ticket 76: Enter in the search box submits, and with nothing ticked among its matches applies nothing', async () => {
        const { root, source } = setUp();
        await painted();
        await open(root, 0);
        const search = field(root, 'input[type=search]');
        click(entry(root, 'Novak'));
        typeInto(search, 'nov');
        search.form?.requestSubmit();
        await settle();
        expect([filterOf(source), root.querySelector('.ex-popover') !== null]).toEqual([undefined, true]);
        typeInto(search, 'osei');
        search.form?.requestSubmit();
        await settle();
        expect(spec(source, 'Trader')).toEqual({ clauses: [{ operator: 'in', values: ['Osei'] }] });
    });

    it('ADR-0009 / FL-13: a search over a column filtered by its values offers to add to the filter in force', async () => {
        const { root, source } = setUp();
        source.setFilter({ columns: { Trader: { clauses: [{ operator: 'in', values: ['Osei'] }] } } });
        await painted();
        await open(root, 0);
        expect(entries(root).filter(name => entry(root, name).checked)).toEqual(['Osei']);
        expect(panel(root).querySelector('.ex-popover-add')).toBeNull();
        typeInto(field(root, 'input[type=search]'), 'nov');
        click(field(root, '.ex-popover-add input'));
        click(entry(root, 'Novak'));
        click(button(root, 'OK'));
        await settle();
        expect(spec(source, 'Trader')).toEqual({ clauses: [{ operator: 'in', values: ['Novak', 'Osei'] }] });
    });

    it('ADR-0009: a column whose values are TooMany gets the condition form, starting on its first operator', async () => {
        const { root, source } = setUp();
        await painted();
        await open(root, 1);
        expect(panel(root).querySelector('.ex-popover-list')).toBeNull();
        const operator = operators(root);
        expect([...operator.options].map(option => option.textContent)).toContain('GreaterThan');
        operator.value = 'greaterThan';
        operator.dispatchEvent(new Event('change', { bubbles: true }));
        typeInto(field(root, 'input:not([type])'), '1000');
        click(button(root, 'OK'));
        await settle();
        expect(spec(source, 'Id')).toEqual({ clauses: [{ operator: 'greaterThan', value: 1000 }] });
    });

    it('ADR-0220: a value list given up as the popover closes settles the grid, and its aborted answer refuses nothing', async () => {
        let refuse: ((error: Error) => void) | undefined;
        const source = GridSource.from(rows);
        const asked = (): Promise<unknown> => new Promise((_, reject) => { refuse = reject; });
        const slow = new Proxy(source, {
            get: (target, member): unknown => {
                if (member === 'distinctValues') return asked;
                const held: unknown = Reflect.get(target, member);
                return typeof held === 'function' ? held.bind(target) : held;
            },
        });
        const { root } = mount<Trade>({ columns, source: slow, rowHeight: 24, viewportHeight: 480, viewportWidth: 720, temporal: Temporal });
        await settle();
        const opening = defined(root.querySelectorAll('.ex-header-cell > .ex-menu-button')[0], 'the ▾');
        click(opening);
        await settle();
        expect(root.hasAttribute('data-ex-settled')).toBe(false);
        click(opening);
        // The source's own answer to the abort: expected, and nothing to say.
        defined(refuse, 'the request')(new DOMException('Aborted', 'AbortError'));
        await settle();
        expect([root.querySelector('.ex-popover'), root.hasAttribute('data-ex-settled')]).toEqual([null, true]);
    });

    it('ADR-0220: the grid stands unsettled while the value list is on its way, and settled once it has landed', async () => {
        let answer: ((values: { kind: 'values'; values: readonly unknown[] }) => void) | undefined;
        const source = GridSource.from(rows);
        const asked = (): Promise<unknown> => new Promise(resolve => { answer = resolve; });
        const slow = new Proxy(source, {
            get: (target, member): unknown => {
                if (member === 'distinctValues') return asked;
                const held: unknown = Reflect.get(target, member);
                return typeof held === 'function' ? held.bind(target) : held;
            },
        });
        const { root } = mount<Trade>({ columns, source: slow, rowHeight: 24, viewportHeight: 480, viewportWidth: 720, temporal: Temporal });
        await settle();
        click(defined(root.querySelectorAll('.ex-header-cell > .ex-menu-button')[0], 'the ▾'));
        await settle();
        expect(root.hasAttribute('data-ex-settled')).toBe(false);
        // The condition form stands meanwhile.
        expect(panel(root).querySelector('select')).not.toBeNull();
        defined(answer, 'the request')({ kind: 'values', values: ['Novak'] });
        await settle();
        expect([entries(root), root.hasAttribute('data-ex-settled')]).toEqual([['Novak'], true]);
    });
});

describe('the condition form', () => {
    it('ADR-0009 / FL-14: two conditions joined by Or apply as one spec, and Cancel sends nothing', async () => {
        const { root, source } = setUp();
        await painted();
        await open(root, 2);
        const [first, second] = [...panel(root).querySelectorAll<HTMLSelectElement>('select')];
        if (first === undefined || second === undefined) throw new Error('Two operators stand.');
        expect(second.options[0]?.textContent).toBe('(none)');
        first.value = 'greaterThan';
        first.dispatchEvent(new Event('change', { bubbles: true }));
        typeInto(field(root, 'input:not([type]):not(.ex-popover-second)'), '2000000');
        click(button(root, 'Cancel'));
        await settle();
        expect([filterOf(source), active()]).toEqual([undefined, root]);

        await open(root, 2);
        const [again, other] = [...panel(root).querySelectorAll<HTMLSelectElement>('select')];
        if (again === undefined || other === undefined) throw new Error('Two operators stand.');
        again.value = 'greaterThan';
        again.dispatchEvent(new Event('change', { bubbles: true }));
        typeInto(field(root, 'input:not([type]):not(.ex-popover-second)'), '1000000');
        click(defined(panel(root).querySelectorAll<HTMLInputElement>('.ex-popover-join input')[1], 'Or'));
        other.value = 'lessThan';
        other.dispatchEvent(new Event('change', { bubbles: true }));
        typeInto(field(root, 'input.ex-popover-second'), '5000');
        click(button(root, 'OK'));
        await settle();
        expect(spec(source, 'Notional')).toEqual({
            clauses: [{ operator: 'greaterThan', value: 1_000_000 }, { operator: 'lessThan', value: 5000 }], combinator: 'or',
        });
    });

    it('ADR-0023, ADR-0370 section 10: a value is read in the grid\'s locale, and one that does not read is refused under its field, and applies nothing', async () => {
        const { root, source } = setUp({ locale: 'de-DE' });
        await painted();
        await open(root, 2);
        const operand = field(root, 'input:not([type])');
        typeInto(operand, '1234.5');
        expect(panel(root).querySelector('.ex-popover-refusal')?.textContent)
            .toBe('“1234.5” is not a value of this column as German (Germany) writes it.');
        click(button(root, 'OK'));
        await settle();
        expect([filterOf(source), root.querySelector('.ex-popover') !== null]).toEqual([undefined, true]);
        typeInto(operand, '1.234,5');
        expect(panel(root).querySelector('.ex-popover-refusal')).toBeNull();
        click(button(root, 'OK'));
        await settle();
        expect(spec(source, 'Notional')).toEqual({ clauses: [{ operator: 'equals', value: 1234.5 }] });
    });

    it('ADR-0006, ticket 96: a condition in force reopens as it was applied, its value written in the locale', async () => {
        const { root, source } = setUp({ locale: 'de-DE' });
        source.setFilter({ columns: { Notional: { clauses: [{ operator: 'greaterThan', value: 1234.5 }] } } });
        await painted();
        await open(root, 2);
        expect([operators(root).value, field(root, 'input:not([type])').value]).toEqual(['greaterThan', '1234,5']);
    });

    it('ADR-0023, ADR-0350 section 16: a date is made with the Temporal the grid was given, of the column\'s date type', async () => {
        const { root, source } = setUp({ locale: 'en-GB' });
        await painted();
        await open(root, 3);
        typeInto(field(root, 'input:not([type])'), '05/01/2026');
        click(button(root, 'OK'));
        await settle();
        const value = spec(source, 'Day')?.clauses[0]?.value;
        expect(value).toBeInstanceOf(Temporal.PlainDate);
        expect(String(value)).toBe('2026-01-05');
    });

    it('ADR-0350 section 16: a grid whose Date column offers a filter is refused without a Temporal to make its dates with, by the column\'s name', () => {
        const base = { columns, source: GridSource.from(rows), rowHeight: 24, viewportHeight: 480, viewportWidth: 720 };
        expect(() => mount<Trade>(base)).toThrow(/Column 'Day'.*temporal/u);
        // Nothing is typed into a filter that is not offered.
        expect(() => mount<Trade>({ ...base, hideColumnMenu: true })).not.toThrow();
        const unfiltered = columns.map(column => (column.name === 'Day' ? { ...column, queryable: false } : column));
        expect(() => mount<Trade>({ ...base, columns: unfiltered })).not.toThrow();
    });
});

describe('the keys', () => {
    it('ADR-0044: Tab on a command goes to the filter\'s first control, Shift+Tab to its last, and Tab off either end comes back to the first command', async () => {
        const { root } = setUp();
        await painted();
        await open(root, 0);
        const first = active();
        if (!(first instanceof HTMLElement)) throw new Error('A command holds the keyboard.');
        expect(first.textContent).toBe('Sort ascending');
        key(first, 'Tab');
        expect(active()).toBe(field(root, 'input[type=search]'));
        first.focus();
        key(first, 'Tab', { shiftKey: true });
        expect(active()).toBe(button(root, 'Cancel'));
        // The sentinel after the panel hands the keyboard back.
        found(root.querySelector<HTMLElement>('.ex-popover > .ex-focus-wrap:last-child'), 'sentinel').focus();
        expect(active()).toBe(first);
    });

    it('ADR-0044: E on a command goes to the search box, and on the value list S sorts and E goes back to the search box', async () => {
        const { root, source } = setUp();
        await painted();
        await open(root, 0);
        key(active(), 'e');
        expect(active()).toBe(field(root, 'input[type=search]'));
        const box = entry(root, 'Osei');
        box.focus();
        key(box, 'e');
        expect(active()).toBe(field(root, 'input[type=search]'));
        expect(key(box, 's').defaultPrevented).toBe(true);
        await settle();
        expect([source.snapshot().sorts, root.querySelector('.ex-popover')]).toEqual([[{ column: 'Trader', direction: 'ascending' }], null]);
    });

    it('ADR-0039: Escape from the search box closes the whole popover and gives the keyboard back', async () => {
        const { root } = setUp();
        await painted();
        await open(root, 0);
        const search = field(root, 'input[type=search]');
        search.focus();
        expect(key(search, 'Escape').defaultPrevented).toBe(true);
        expect([root.querySelector('.ex-popover'), active()]).toEqual([null, root]);
    });

    it('ADR-0010: a column that is not queryable gets its commands alone, and Tab closes them', async () => {
        const { root } = setUp({
            columns: columns.map(column => (column.name === 'Notional' ? { ...column, queryable: false } : column)), onColumnHidden: () => undefined,
        });
        await painted();
        click(defined(root.querySelectorAll('.ex-header-cell > .ex-menu-button')[2], 'the ▾'));
        expect([root.querySelector('.ex-popover')?.getAttribute('role'), root.querySelector('.ex-popover-filter')]).toEqual(['none', null]);
        key(active(), 'Tab');
        expect([root.querySelector('.ex-popover'), active()]).toEqual([null, root]);
    });
});

describe('the options', () => {
    it('ADR-0370 section 10: a locale that is no BCP 47 tag is refused by name, and filterUi takes its three values only', () => {
        expect(() => setUp({ locale: 'not a tag!' })).toThrow(/'not a tag!'/u);
        expect(() => setUp({ columns: [{ name: 'X', type: 'text', value: () => 'x', filterUi: 'list' as never }] })).toThrow(/filterUi/u);
    });
});
