// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): rules over the whole collection (ADR-0381). The bundled ruleset's set rules mark
// every cell a Breach names by Row Key, the rows that never changed included, and the error step, F8 and Shift+F8,
// is asked of the Consumer as a Find step is, so it reaches an error far outside the Window; so is the Error Count
// the status line shows. The C# ruleset answers the same cases in GridRulesetTests.
import { afterEach, describe, expect, it } from 'vitest';
import { GridRuleset, type Breach, type GridErrorCount, type GridErrorCountRequest, type GridErrorStepRequest } from '../src/cells/grid-ruleset';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridSelection } from '../src/selection/selection';
import { foundAt, notFound, type GridFindResult } from '../src/finding/find';
import { defined } from '../src/must';
import { GridSource } from '../src/source/grid-source';
import type { InMemoryGridSource } from '../src/source/in-memory-source';
import { mount, painted, q, unmountAll } from './grid-harness';

afterEach(unmountAll);

interface Deal {
    readonly id: string;
    readonly csa: string;
    readonly curve: string;
}

const dealColumns: readonly GridColumn<Deal>[] = [
    { name: 'Id', type: 'text', value: deal => deal.id, width: 80 },
    { name: 'Csa', type: 'text', value: deal => deal.csa, width: 80 },
    { name: 'Curve', type: 'text', value: deal => deal.curve, width: 80 },
];
const curve = defined(dealColumns[2], 'the Curve column');

// One discount curve per CSA: every trade of a CSA with two curves breaches, and the message names the other side
// by trade id, never by position (ADR-0381's example).
function groupBy<T>(items: readonly T[], keyOf: (item: T) => string): Map<string, T[]> {
    const groups = new Map<string, T[]>();
    for (const item of items) {
        const group = groups.get(keyOf(item));
        if (group === undefined) groups.set(keyOf(item), [item]);
        else group.push(item);
    }
    return groups;
}

function oneCurvePerCsa(deals: readonly Deal[]): Breach[] {
    return [...groupBy(deals, deal => deal.csa)].flatMap(([csa, trades]) => {
        const curves = [...groupBy(trades, deal => deal.curve)].toSorted(([, a], [, b]) => b.length - a.length);
        if (curves.length < 2) return [];
        const told = curves.map(([name, on]) => `${name} on ${on.map(deal => deal.id).join(' ')}`).join(', ');
        return [{
            rule: 'one-curve-per-csa', message: `CSA ${csa} has ${curves.length} discount curves: ${told}.`,
            cells: trades.map(deal => ({ rowKey: deal.id, column: 'Curve' })),
        }];
    });
}

const dealRules = (): GridRuleset<Deal> => new GridRuleset<Deal>(deal => deal.id).set(oneCurvePerCsa);
const step = (request: Partial<GridErrorStepRequest>): GridErrorStepRequest =>
    ({ backward: false, columns: ['Id', 'Csa', 'Curve'], rowSequenceVersion: 1, ...request });

describe('set rules (ADR-0381)', () => {
    it('ED-36: every trade of the CSA wears Error by Row Key, the next version of an unchanged row included', () => {
        const rules = dealRules();
        const deals: Deal[] = [
            { id: 'T1', csa: 'ABC', curve: 'SOFR' }, { id: 'T2', csa: 'ABC', curve: 'SOFR' },
            { id: 'T3', csa: 'ABC', curve: 'FF' }, { id: 'T4', csa: 'XYZ', curve: 'FF' },
        ];
        rules.evaluate(deals, dealColumns);
        const { cellState } = rules;
        expect(deals.map(deal => cellState(deal, curve))).toEqual(['error', 'error', 'error', 'normal']);
        expect(cellState(defined(deals[0], 'T1'), defined(dealColumns[1], 'Csa'))).toBe('normal');
        expect(cellState({ ...defined(deals[0], 'T1') }, curve)).toBe('error');
        expect(rules.breaches).toHaveLength(1);
    });

    it('ED-37: each involved cell says the Breach\'s message, which names the others by id', () => {
        const rules = dealRules();
        const deals: Deal[] = [{ id: 'T1', csa: 'ABC', curve: 'SOFR' }, { id: 'T2', csa: 'ABC', curve: 'SOFR' }, { id: 'T3', csa: 'ABC', curve: 'FF' }];
        rules.evaluate(deals, dealColumns);
        const expected = 'CSA ABC has 2 discount curves: SOFR on T1 T2, FF on T3.';
        expect(deals.map(deal => rules.cellMessage(deal, curve))).toEqual([expected, expected, expected]);
        expect(rules.breaches.map(breach => breach.message)).toEqual([expected]);
    });

    it('ED-36: the edit that restores one curve clears every mark, and the breaches with it', () => {
        const rules = dealRules();
        const broken: Deal[] = [{ id: 'T1', csa: 'ABC', curve: 'SOFR' }, { id: 'T2', csa: 'ABC', curve: 'FF' }];
        rules.evaluate(broken, dealColumns);
        const fixedUp = [defined(broken[0], 'T1'), { ...defined(broken[1], 'T2'), curve: 'SOFR' }];
        rules.evaluate(fixedUp, dealColumns);
        expect([fixedUp.map(deal => rules.cellState(deal, curve)), rules.breaches]).toEqual([['normal', 'normal'], []]);
    });

    it('ADR-0381 section 2: a set rule on a ruleset without a Row Key throws, naming the key', () => {
        expect(() => new GridRuleset<Deal>().set(oneCurvePerCsa)).toThrow(/Row Key/);
    });

    it('ADR-0381, spine rule 1: a Breach naming a key no row has, or no cell, throws', () => {
        const ghost = new GridRuleset<Deal>(deal => deal.id).set(() => [{ rule: 'ghost', message: 'T9 is wrong.', cells: [{ rowKey: 'T9', column: 'Curve' }] }]);
        expect(() => { ghost.evaluate([{ id: 'T1', csa: 'ABC', curve: 'SOFR' }], dealColumns); }).toThrow(/'T9'/);
        const empty = new GridRuleset<Deal>(deal => deal.id).set(() => [{ rule: 'empty', message: 'Something is wrong.', cells: [] }]);
        expect(() => { empty.evaluate([{ id: 'T1', csa: 'ABC', curve: 'SOFR' }], dealColumns); }).toThrow(TypeError);
    });

    it('ADR-0381, spine rule 1: a Breach naming a column no declared column is throws', () => {
        const typo = new GridRuleset<Deal>(deal => deal.id).set(() => [{ rule: 'typo', message: 'T1 is wrong.', cells: [{ rowKey: 'T1', column: 'curve' }] }]);
        expect(() => { typo.evaluate([{ id: 'T1', csa: 'ABC', curve: 'SOFR' }], dealColumns); }).toThrow(/'curve'/);
    });

    it('ADR-0140 / ADR-0381: two rows under one Row Key throw, rather than share one set of marks', () => {
        expect(() => { dealRules().evaluate([{ id: 'T1', csa: 'ABC', curve: 'SOFR' }, { id: 'T1', csa: 'XYZ', curve: 'FF' }], dealColumns); }).toThrow(/'T1'/);
    });

    it('ADR-0381 section 3, spine rule 1: a Focus outside the rows handed over throws, as they are not the grid\'s order', () => {
        const rules = dealRules();
        const deals: Deal[] = [{ id: 'A', csa: 'X', curve: 'SOFR' }, { id: 'B', csa: 'X', curve: 'FF' }];
        rules.evaluate(deals, dealColumns);
        expect(() => rules.findError(step({ from: { row: 120, column: 0 } }), deals)).toThrow(RangeError);
    });

    it('ED-38: the step walks the whole result in Find\'s order, wraps, runs backward, and a lone error finds itself', () => {
        const rules = dealRules();
        const deals: Deal[] = Array.from({ length: 1000 }, (_, i) => ({ id: `T${i}`, csa: `C${i}`, curve: 'SOFR' }));
        deals[900] = { id: 'T900', csa: 'C1', curve: 'FF' };
        rules.evaluate(deals, dealColumns);
        expect(rules.findError(step({ from: { row: 0, column: 0 } }), deals)).toEqual(foundAt(1, 'Curve'));
        expect(rules.findError(step({ from: { row: 1, column: 2 } }), deals)).toEqual(foundAt(900, 'Curve'));
        expect(rules.findError(step({ from: { row: 900, column: 2 } }), deals)).toEqual(foundAt(1, 'Curve'));
        expect(rules.findError(step({ from: { row: 1, column: 2 }, backward: true }), deals)).toEqual(foundAt(900, 'Curve'));
        expect(rules.findError(step({}), deals)).toEqual(foundAt(1, 'Curve'));

        const lone: Deal[] = [{ id: 'A', csa: 'X', curve: 'SOFR' }, { id: 'B', csa: 'X', curve: 'FF' }];
        rules.evaluate(lone, dealColumns);
        expect(rules.findError(step({ from: { row: 0, column: 2 } }), [defined(lone[0], 'A')])).toEqual(foundAt(0, 'Curve'));
        // A hidden column is not stepped to.
        expect(rules.findError(step({ columns: ['Id', 'Csa'] }), lone)).toEqual(notFound);
    });
});

function key(target: Element, name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    return event;
}
const settle = async (): Promise<void> => {
    for (let i = 0; i < 4; i++) await new Promise(resolve => { setTimeout(resolve, 0); });
};
function click(root: HTMLElement, x: number, y: number): void {
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        q(root, '.ex-viewport').dispatchEvent(event);
    }
}
const announced = (root: HTMLElement): string | null => q(root, '.ex-announce').textContent;

// A thousand trades, each its own CSA but for T900, which shares C1 with T1 on another curve: two errors, 899 rows
// apart, the second far below a Window of a dozen rows. The step reads the order from the Source, as a Consumer does.
function stepping(findError?: (request: GridErrorStepRequest, signal: AbortSignal) => GridFindResult | PromiseLike<GridFindResult>): {
    readonly root: HTMLElement;
    readonly source: InMemoryGridSource<Deal>;
    readonly selection: () => GridSelection;
    readonly asked: GridErrorStepRequest[];
} {
    const deals: Deal[] = Array.from({ length: 1000 }, (_, i) => ({ id: `T${i}`, csa: `C${i}`, curve: 'SOFR' }));
    deals[900] = { id: 'T900', csa: 'C1', curve: 'FF' };
    const rules = dealRules();
    rules.evaluate(deals, dealColumns);
    const source = GridSource.from(deals, { rowKey: deal => deal.id });
    const asked: GridErrorStepRequest[] = [];
    let selection: GridSelection | undefined;
    const { root } = mount<Deal>({
        columns: dealColumns, source, rowHeight: 24, viewportHeight: 300, viewportWidth: 600,
        cellState: rules.cellState, cellMessage: rules.cellMessage, onSelectionChanged: next => { selection = next; },
        findError: findError ?? (async (request, signal) => {
            asked.push(request);
            const total = defined(source.snapshot().totalCount, 'the count of the rows');
            return rules.findError(request, await source.rows({ start: 0, count: total }, signal));
        }),
    });
    return { root, source, asked, selection: () => defined(selection, 'the Selection heard') };
}

describe('the error step (ADR-0381 section 3)', () => {
    it('ED-38: F8 asks from the Focus and brings it to an error far below the Window, and Shift+F8 goes back', async () => {
        const { root, asked, selection } = stepping();
        await painted();
        click(root, 10, 5);
        expect(key(root, 'F8').defaultPrevented).toBe(true);
        await settle();
        expect(selection().focus).toEqual({ row: 1, column: 2 });
        key(root, 'F8');
        await settle();
        expect(selection().focus).toEqual({ row: 900, column: 2 });
        // Revealed: the row is painted, the Focus on it.
        expect(root.querySelector('[aria-rowindex="902"]')).not.toBeNull();
        key(root, 'F8', { shiftKey: true });
        await settle();
        expect(selection().focus).toEqual({ row: 1, column: 2 });
        expect(asked.map(request => [request.backward, request.from, request.columns])).toEqual([
            [false, { row: 0, column: 0 }, ['Id', 'Csa', 'Curve']],
            [false, { row: 1, column: 2 }, ['Id', 'Csa', 'Curve']],
            [true, { row: 900, column: 2 }, ['Id', 'Csa', 'Curve']],
        ]);
    });

    it('ADR-0381 section 3: with no findError, F8 is not the grid\'s, and the browser keeps it', async () => {
        const { root } = mount<Deal>({ columns: dealColumns, rows: [{ id: 'T1', csa: 'C1', curve: 'SOFR' }], rowHeight: 24, viewportHeight: 300 });
        await painted();
        expect([key(root, 'F8').defaultPrevented, key(root, 'F8', { shiftKey: true }).defaultPrevented]).toEqual([false, false]);
    });

    it('ADR-0381 section 3: F8 declared beside a findError is refused at mount, as two meanings of one key', () => {
        expect(() => mount<Deal>({
            columns: dealColumns, rows: [], rowHeight: 24, viewportHeight: 300,
            findError: () => notFound, declaredKeys: ['F8'], onDeclaredKey: () => undefined,
        })).toThrow(/F8/);
    });

    it('ADR-0381 section 3: none found is said, and an answer the order outlived moves nothing and is said', async () => {
        const none = stepping(() => notFound);
        await painted();
        click(none.root, 10, 5);
        key(none.root, 'F8');
        await settle();
        expect([none.selection().focus, announced(none.root)]).toEqual([{ row: 0, column: 0 }, 'No error among the rows and columns shown.']);

        let release: ((answer: GridFindResult) => void) | undefined;
        const late = stepping(() => new Promise(resolve => { release = resolve; }));
        await painted();
        click(late.root, 10, 5);
        key(late.root, 'F8');
        await settle();
        expect(late.root.getAttribute('data-ex-settled')).toBeNull();
        // The order moves while the answer is out: row 900 is another trade now, and the Selection goes (ADR-0011).
        late.source.setSorts([{ column: 'Id', direction: 'descending' }]);
        await settle();
        defined(release, 'the held answer')(foundAt(900, 'Curve'));
        await settle();
        expect([late.selection().focus, announced(late.root)])
            .toEqual([undefined, 'The rows moved while the next error was looked for, so the step was set aside. Press F8 again.']);
    });
});

const counted = (request: Partial<GridErrorCountRequest>): GridErrorCountRequest => ({ columns: ['Id', 'Csa', 'Curve'], rowSequenceVersion: 1, ...request });

// Two trades of C1 on two curves among a hundred, the count read from the Source in the grid's order, as the
// Docs Site's Example reads it.
function counting(countErrors?: (request: GridErrorCountRequest, signal: AbortSignal) => GridErrorCount | PromiseLike<GridErrorCount>) {
    const deals: Deal[] = Array.from({ length: 100 }, (_, i) => ({ id: `T${i}`, csa: `C${i}`, curve: 'SOFR' }));
    deals[90] = { id: 'T90', csa: 'C1', curve: 'FF' };
    const rules = dealRules();
    rules.evaluate(deals, dealColumns);
    const source = GridSource.from(deals, { rowKey: deal => deal.id });
    const inOrder = async (signal: AbortSignal): Promise<readonly Deal[]> =>
        source.rows({ start: 0, count: defined(source.snapshot().totalCount, 'the count of the rows') }, signal);
    let selection: GridSelection | undefined;
    const options = {
        columns: dealColumns, source, rowHeight: 24, viewportHeight: 300, viewportWidth: 600,
        cellState: rules.cellState, cellMessage: rules.cellMessage, onSelectionChanged: (next: GridSelection) => { selection = next; },
        findError: async (request: GridErrorStepRequest, signal: AbortSignal) => rules.findError(request, await inOrder(signal)),
        countErrors: countErrors ?? (async (request: GridErrorCountRequest, signal: AbortSignal) => rules.countErrors(request, await inOrder(signal))),
    };
    const { root, view } = mount<Deal>(options);
    return { root, view, rules, deals, source, options, selection: () => defined(selection, 'the Selection heard') };
}
const line = (root: HTMLElement): string | null => q(root, '.ex-error-count').textContent;

describe('the Error Count (ADR-0381 section 3\'s note of 2026-11-01)', () => {
    it('ED-39: every marked cell is counted, and those in rows the result leaves out or hidden columns are said as hidden', () => {
        const rules = dealRules().column('Id', (_, text) => (text.startsWith('T') ? { kind: 'accept' } : { kind: 'flag', message: 'An id starts with T.' }));
        const deals: Deal[] = [
            { id: 'T1', csa: 'ABC', curve: 'SOFR' }, { id: 'T2', csa: 'ABC', curve: 'FF' }, { id: 'X3', csa: 'XYZ', curve: 'FF' },
        ];
        rules.evaluate(deals, dealColumns);
        // Two Curve cells by the Breach, one Id cell by the column rule: cells, not Breaches.
        expect(rules.countErrors(counted({}), deals)).toEqual({ count: 3, hidden: 0 });
        // T2 filtered out: its Curve cell is still an error, and the step cannot reach it.
        expect(rules.countErrors(counted({}), [defined(deals[0], 'T1'), defined(deals[2], 'X3')])).toEqual({ count: 3, hidden: 1 });
        // The Curve column hidden: both of its marks are out of the step's reach.
        expect(rules.countErrors(counted({ columns: ['Id', 'Csa'] }), deals)).toEqual({ count: 3, hidden: 2 });
        expect(() => rules.countErrors(counted({}), [...deals, defined(deals[1], 'T2')])).toThrow(/twice/);
        rules.evaluate([{ id: 'T1', csa: 'ABC', curve: 'SOFR' }], dealColumns);
        expect(rules.countErrors(counted({}), [])).toEqual({ count: 0, hidden: 0 });
    });

    it('ED-39: the count stands in the status line, a Filter says the cells it hides apart, and the press steps as F8', async () => {
        const { root, source, selection } = counting();
        await painted();
        expect(line(root)).toBe('Counting errors…');
        await settle();
        expect(line(root)).toBe('Errors: 2');
        // Another order is another question: T90 filtered out is still an error, said as hidden, never left out.
        source.setFilter({ columns: { Id: { clauses: [{ operator: 'notEquals', value: 'T90' }] } } });
        await settle();
        expect(line(root)).toBe('Errors: 2 (1 hidden)');
        source.setFilter(undefined);
        await settle();
        click(root, 10, 5);
        q(root, '.ex-error-count').click();
        await settle();
        expect(selection().focus).toEqual({ row: 1, column: 2 });
    });

    it('ED-39: a new judgement asks again, an earlier answer arriving late is dropped, and the line stands at zero', async () => {
        const held: ((count: GridErrorCount) => void)[] = [];
        const { root, view, rules, deals, options } = counting(() => new Promise(resolve => { held.push(resolve); }));
        await painted();
        await settle();
        expect([held.length, root.getAttribute('data-ex-settled')]).toEqual([1, null]);
        // The trade moved back to SOFR: the ruleset hands new functions, which is another judgement.
        deals[90] = { id: 'T90', csa: 'C1', curve: 'SOFR' };
        rules.evaluate(deals, dealColumns);
        view.update({ ...options, cellState: rules.cellState, cellMessage: rules.cellMessage });
        await settle();
        expect(held).toHaveLength(2);
        defined(held[0], 'the first question')({ count: 2, hidden: 0 });
        await settle();
        expect(line(root)).toBe('Counting errors…');
        defined(held[1], 'the second question')({ count: 0, hidden: 0 });
        await settle();
        expect([line(root), root.querySelector('.ex-status')?.contains(q(root, '.ex-error-count'))]).toEqual(['Errors: 0', true]);
    });

    it('ED-39: countErrors without findError is refused at mount, and an answer that is not a count is named', async () => {
        expect(() => mount<Deal>({ columns: dealColumns, rows: [], rowHeight: 24, viewportHeight: 300, countErrors: () => ({ count: 0, hidden: 0 }) }))
            .toThrow(/findError/);
        const bad = counting(() => ({ count: 1, hidden: 2 }));
        const failures: unknown[] = [];
        const heard = (reason: unknown): void => { failures.push(reason); };
        process.on('unhandledRejection', heard);
        try {
            await painted();
            await settle();
        } finally {
            process.off('unhandledRejection', heard);
        }
        expect([failures.some(failure => failure instanceof TypeError && /hidden no more than count/.test(failure.message)), line(bad.root)])
            .toEqual([true, 'Counting errors…']);
    });
});
