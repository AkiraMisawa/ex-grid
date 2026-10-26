// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): Find and Replace in the grid, as the C# core answers them (ADR-0055, ADR-0332,
// ADR-0333). Ctrl+F opens the panel or is refused where nothing can search; a step asks the source with the
// Focus, the scope and the source's version, and an answer the order or the source outlived moves nothing;
// the headers come first in the walk; the painted matches are marked, and a row whose marks did not move is
// not painted again; Replace writes through an ordinary Edit Intent, and Replace All is the Consumer's.
// The browser layer's find.spec.mjs runs the same panel with real keys on the React host.
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridEditIntent } from '../src/cells/edit-intent';
import { actionColumn, type GridColumn } from '../src/dom/grid-column';
import type { GridRefusal } from '../src/dom/grid-refusal';
import type { GridOptions } from '../src/dom/grid-view';
import { notFound, type GridFindRequest, type GridFindResult } from '../src/finding/find';
import { refusedCells, replacedCells, type GridReplaceRequest, type GridReplaceResult } from '../src/finding/replace';
import type { GridSelection } from '../src/selection/selection';
import { GridSource } from '../src/source/grid-source';
import type { InMemoryGridSource } from '../src/source/in-memory-source';
import { mount, painted, q } from './grid-harness';

interface Deal {
    readonly book: string;
    readonly trader: string;
    readonly notional: number;
}

// Trader repeats every three rows through four names: rows 3-5 are Novak, then 15-17, and so on.
const traders = ['Ishikawa', 'Novak', 'Osei', 'Lee'];
const deals: readonly Deal[] = Array.from({ length: 400 }, (_, i) => ({
    book: `B${i % 4}`, trader: defined(traders[Math.floor(i / 3) % traders.length], 'a trader'), notional: i * 10,
}));
const columns: readonly GridColumn<Deal>[] = [
    { name: 'Book', type: 'text', value: deal => deal.book, width: 100 },
    { name: 'Trader', type: 'text', value: deal => deal.trader, width: 120, editable: true },
    {
        name: 'Notional', type: 'number', value: deal => deal.notional, width: 100, editable: true,
        validate: (_deal, text) => (Number.isFinite(Number(text)) ? { kind: 'accept' } : { kind: 'reject', message: `'${text}' is not a number` }),
    },
];

const rowY = (row: number): number => (row * 24) + 5;

function mouse(root: HTMLElement, name: string, x: number, y: number, init: MouseEventInit = {}): void {
    const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, ...init });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    q(root, '.ex-viewport').dispatchEvent(event);
}
function click(root: HTMLElement, x: number, y: number, init: MouseEventInit = {}): void {
    mouse(root, 'mousedown', x, y, init);
    mouse(root, 'mouseup', x, y, init);
}
function key(target: Element, name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    return event;
}
const settle = async (): Promise<void> => {
    for (let i = 0; i < 4; i++) await new Promise(resolve => { setTimeout(resolve, 0); });
};

const panel = (root: HTMLElement): HTMLElement | null => root.querySelector<HTMLElement>(':scope > .ex-popover-find');
const field = (root: HTMLElement): HTMLInputElement => q(root, '.ex-popover-find input.ex-find-field') as HTMLInputElement;
const outcome = (root: HTMLElement): string | null => q(root, '.ex-popover-find .ex-find-outcome').textContent;
const cell = (root: HTMLElement, row: number, column: number): HTMLElement => q(root, `[id$='r${row}c${column}']`);

function typeInto(input: HTMLInputElement, text: string): void {
    input.value = text;
    input.dispatchEvent(new Event('input', { bubbles: true }));
}
/** Enter in a panel field: its keydown says which way, and the form's implicit submission steps. */
function enter(root: HTMLElement, input: HTMLInputElement, shift = false): void {
    key(input, 'Enter', { shiftKey: shift });
    q(root, '.ex-popover-find form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
}
function press(root: HTMLElement, selector: string): void {
    q(root, `.ex-popover-find ${selector}`).click();
}

/** A source that is `GridSource.from`'s, its Find answered by the test, now or later. */
function asking(
    source: InMemoryGridSource<Deal>, answer?: (request: GridFindRequest, signal: AbortSignal) => Promise<GridFindResult>, finds = true,
): {
    readonly source: GridSource<Deal>;
    readonly requests: GridFindRequest[];
} {
    const requests: GridFindRequest[] = [];
    const wrapped: GridSource<Deal> = {
        subscribe: listener => source.subscribe(listener),
        snapshot: () => source.snapshot(),
        setColumns: given => { source.setColumns(given); },
        setSorts: sorts => { source.setSorts(sorts); },
        setFilter: filter => { source.setFilter(filter); },
        needRange: range => source.needRange(range),
        rows: (range, signal) => source.rows(range, signal),
        distinctValues: (name, signal) => source.distinctValues(name, signal),
    };
    if (!finds) return { source: wrapped, requests };
    return {
        source: {
            ...wrapped,
            find: (request, signal) => {
                requests.push(request);
                return answer === undefined ? source.find(request, signal) : answer(request, signal);
            },
        },
        requests,
    };
}

function grid(options: Partial<GridOptions<Deal>> = {}): {
    readonly root: HTMLElement;
    readonly view: ReturnType<typeof mount<Deal>>['view'];
    readonly refusals: GridRefusal[];
    readonly selection: () => GridSelection;
} {
    const refusals: GridRefusal[] = [];
    let selection: GridSelection | undefined;
    const { root, view } = mount<Deal>({
        columns, ...(options.source === undefined ? { rows: deals } : {}), rowHeight: 24, viewportHeight: 320, viewportWidth: 720,
        onRefused: refusal => { refusals.push(refusal); }, onSelectionChanged: next => { selection = next; }, ...options,
    });
    return { root, view, refusals, selection: () => defined(selection, 'the Selection heard') };
}

async function find(root: HTMLElement, text: string): Promise<void> {
    key(root, 'f', { ctrlKey: true });
    typeInto(field(root), text);
    await painted();
}

describe('opening the find panel', () => {
    it('FD-1 / ADR-0055: Ctrl+F is taken on every grid; where the source cannot find, it opens nothing and is refused', async () => {
        const { source } = asking(GridSource.from(deals), undefined, false);
        const { root, refusals } = grid({ source });
        await painted();
        expect(key(root, 'f', { ctrlKey: true }).defaultPrevented).toBe(true);
        expect(key(root, 'h', { ctrlKey: true }).defaultPrevented).toBe(true);
        expect(panel(root)).toBeNull();
        expect(refusals).toEqual([{ kind: 'find', reason: 'unavailable' }, { kind: 'find', reason: 'replaceUnavailable' }]);
    });

    it('ADR-0332 §2: Ctrl+H without a Replace Request handler opens nothing and is refused, and Ctrl+F still opens Find', async () => {
        const { root, refusals } = grid();
        await painted();
        expect(key(root, 'h', { ctrlKey: true }).defaultPrevented).toBe(true);
        expect(panel(root)).toBeNull();
        expect(refusals).toEqual([{ kind: 'find', reason: 'replaceUnavailable' }]);
        key(root, 'f', { ctrlKey: true });
        expect(panel(root)?.getAttribute('aria-label')).toBe('Find');
        expect(document.activeElement).toBe(field(root));
    });

    it('ADR-0055 / ADR-0332 §4: the text, the options and the replacement outlive the panel, and what a step came to does not', async () => {
        const { root } = grid({ onReplace: () => replacedCells(0, 0) });
        await painted();
        await find(root, 'nobody');
        q(root, '.ex-popover-find input.ex-find-match-case').click();
        enter(root, field(root));
        await settle();
        expect(outcome(root)).toBe('No match');
        key(field(root), 'Escape');
        expect(panel(root)).toBeNull();
        expect(document.activeElement).toBe(root);

        key(root, 'h', { ctrlKey: true });
        expect(panel(root)?.getAttribute('aria-label')).toBe('Find and Replace');
        expect(field(root).value).toBe('nobody');
        expect((q(root, '.ex-popover-find input.ex-find-match-case') as HTMLInputElement).checked).toBe(true);
        expect(outcome(root)).toBe('');
        typeInto(q(root, '.ex-popover-find input.ex-find-replace-field') as HTMLInputElement, 'Rossi');
        // Ctrl+F in the panel turns it back into Find alone, in its place.
        key(q(root, '.ex-popover-find input.ex-find-replace-field'), 'f', { ctrlKey: true });
        expect(root.querySelectorAll('.ex-popover')).toHaveLength(1);
        expect(root.querySelector('.ex-find-replace-field')).toBeNull();
        expect(document.activeElement).toBe(field(root));
        key(root, 'h', { ctrlKey: true });
        expect((q(root, '.ex-popover-find input.ex-find-replace-field') as HTMLInputElement).value).toBe('Rossi');
    });
});

describe('a Find step', () => {
    it('FD-5 / ADR-0055: Enter steps from the Focus and Shift+Enter back; the answer becomes the Focus', async () => {
        const { root, selection } = grid({ findsHeaders: false });
        await painted();
        click(root, 10, rowY(0));
        await find(root, 'novak');
        enter(root, field(root));
        await settle();
        expect(selection().focus).toEqual({ row: 3, column: 1 });
        enter(root, field(root));
        await settle();
        expect(selection().focus).toEqual({ row: 4, column: 1 });
        enter(root, field(root), true);
        await settle();
        expect(selection().focus).toEqual({ row: 3, column: 1 });
        expect(document.activeElement).toBe(field(root));
    });

    it('ADR-0055: a step asks with the Focus, the selected ranges as the scope, the columns and the source\'s version', async () => {
        const { source, requests } = asking(GridSource.from(deals), async () => notFound);
        const { root, refusals } = grid({ source });
        await painted();
        click(root, 10, rowY(1));
        mouse(root, 'mousedown', 150, rowY(4), { shiftKey: true });
        mouse(root, 'mouseup', 150, rowY(4), { shiftKey: true });
        await find(root, 'novak');
        press(root, 'button.ex-find-previous');
        await settle();
        expect(requests).toHaveLength(1);
        const [request] = requests;
        expect(request).toMatchObject({
            text: 'novak', matchCase: false, wholeCell: false, backward: true, from: { row: 1, column: 0 },
            columns: ['Book', 'Trader', 'Notional'], rowSequenceVersion: source.snapshot().rowSequenceVersion,
        });
        expect(request?.scope).toEqual([{ topRow: 1, leftColumn: 0, rowCount: 4, columnCount: 2 }]);
        expect(outcome(root)).toBe('No match');
        expect(refusals).toEqual([{ kind: 'find', reason: 'notFound' }]);
    });

    it('ADR-0011 / ADR-0055: an answer the order moved under is set aside and said; a new step makes the last one\'s answer nobody\'s', async () => {
        const answers: ((result: GridFindResult) => void)[] = [];
        const inMemory = GridSource.from(deals);
        const { source } = asking(inMemory, () => new Promise(resolve => { answers.push(resolve); }));
        const { root, refusals, selection } = grid({ source, findsHeaders: false });
        await painted();
        click(root, 10, rowY(0));
        await find(root, 'novak');
        enter(root, field(root));
        enter(root, field(root));
        // The first answer arrives after the second step was asked: it is nobody's, and moves nothing.
        defined(answers[0], 'the first step')({ found: true, row: 100, column: 'Trader' });
        await settle();
        expect(selection().focus).toEqual({ row: 0, column: 0 });
        expect(root.getAttribute('data-ex-settled')).toBeNull();
        inMemory.setSorts([{ column: 'Notional', direction: 'descending' }]);
        defined(answers[1], 'the second step')({ found: true, row: 3, column: 'Trader' });
        await settle();
        expect(outcome(root)).toBe('The rows moved; find again');
        expect(refusals).toEqual([{ kind: 'find', reason: 'orderChanged' }]);
    });

    it('ADR-0142 / ADR-0055: an answer from a source since replaced moves nothing, whatever its version says', async () => {
        let answer: ((result: GridFindResult) => void) | undefined;
        const { source } = asking(GridSource.from(deals), () => new Promise(resolve => { answer = resolve; }));
        const options = { columns, source, rowHeight: 24, viewportHeight: 320, viewportWidth: 720, findsHeaders: false } as const;
        const { root, view, refusals } = grid(options);
        await painted();
        await find(root, 'novak');
        enter(root, field(root));
        // Another source, counting from the same version, handed over while the question is out.
        view.update({ ...options, source: GridSource.from(deals), onRefused: refusal => { refusals.push(refusal); } });
        defined(answer, 'the step')({ found: true, row: 3, column: 'Trader' });
        await settle();
        expect(outcome(root)).toBe('The data was replaced; find again');
        expect(refusals).toEqual([{ kind: 'find', reason: 'sourceChanged' }]);
    });

    it('ADR-0142 / ADR-0055: a step given up by a source since replaced is said as the data replaced', async () => {
        let giveUp: (() => void) | undefined;
        const { source } = asking(GridSource.from(deals), () => new Promise((_resolve, reject) => {
            giveUp = () => { reject(new DOMException('The report it read is gone.', 'AbortError')); };
        }));
        const options = { columns, source, rowHeight: 24, viewportHeight: 320, viewportWidth: 720, findsHeaders: false } as const;
        const { root, view, refusals } = grid(options);
        await painted();
        await find(root, 'novak');
        enter(root, field(root));
        view.update({ ...options, source: GridSource.from(deals), onRefused: refusal => { refusals.push(refusal); } });
        defined(giveUp, 'the step')();
        await settle();
        expect(outcome(root)).toBe('The data was replaced; find again');
        expect(refusals).toEqual([{ kind: 'find', reason: 'sourceChanged' }]);
    });

    it('ADR-0055 / ADR-0025: a step given up by whoever answers moves nothing, and claims no "No match"', async () => {
        const { source } = asking(GridSource.from(deals), async () => { throw new DOMException('The server went away.', 'AbortError'); });
        const { root, refusals } = grid({ source });
        await painted();
        await find(root, 'novak');
        enter(root, field(root));
        await settle();
        expect(outcome(root)).toBe('');
        expect(refusals).toEqual([]);
    });
});

describe('the headers (ADR-0333)', () => {
    it('ADR-0333: from nothing, a step forward lands on a header that matches before any row; the Focus stays, and the panel names it', async () => {
        const { source, requests } = asking(GridSource.from(deals));
        const { root, selection } = grid({ source });
        await painted();
        click(root, 10, rowY(0));
        await find(root, 'trade');
        const trader = q(root, '.ex-header-cell[aria-colindex="2"]');
        // Marked as it is typed, no step asked.
        expect(trader.className).toMatch(/(^|\s)ex-match-mark(\s|$)/);
        expect(q(root, '.ex-header-cell[aria-colindex="1"]').className).not.toMatch(/ex-match-mark/);
        // From the Focus the rows come first, and none says "trade": the walk wraps to the headers.
        enter(root, field(root));
        await settle();
        expect(requests).toHaveLength(1);
        expect(trader.className).toMatch(/ex-match-mark-current/);
        expect(trader.getAttribute('aria-current')).toBe('true');
        expect(outcome(root)).toBe('Found in the header Trader');
        expect(selection().focus).toEqual({ row: 0, column: 0 });
        // From the header, the next matching header is reached before the rows; there is none, so the rows
        // are asked from their start, none matches, and the walk wraps to the same header.
        enter(root, field(root));
        await settle();
        expect(requests).toHaveLength(2);
        expect(requests[1]?.from).toBeUndefined();
        expect(outcome(root)).toBe('Found in the header Trader');
        // The user moves: the header is no longer the current match, and the panel no longer names it.
        key(root, 'ArrowDown');
        await painted();
        expect(trader.className).not.toMatch(/ex-match-mark-current/);
        expect(trader.hasAttribute('aria-current')).toBe(false);
        expect(outcome(root)).toBe('');
    });

    it('ADR-0333: with findsHeaders off, or a Selection as the scope, no header is searched or marked', async () => {
        const { root, refusals } = grid({ findsHeaders: false });
        await painted();
        click(root, 10, rowY(0));
        await find(root, 'trade');
        expect(root.querySelector('.ex-header-cell.ex-match-mark')).toBeNull();
        enter(root, field(root));
        await settle();
        expect(outcome(root)).toBe('No match');
        expect(refusals).toEqual([{ kind: 'find', reason: 'notFound' }]);
    });
});

describe('the Match Mark (ADR-0332 §4)', () => {
    it('ADR-0332 §4: the painted matches are marked as they are typed, the current one apart, and the mark goes with the Focus', async () => {
        const { root } = grid({ findsHeaders: false });
        await painted();
        click(root, 10, rowY(0));
        await find(root, 'novak');
        for (const row of [3, 4, 5]) expect(cell(root, row, 1).className).toMatch(/(^|\s)ex-match-mark(\s|$)/);
        expect(cell(root, 2, 1).className).not.toMatch(/ex-match-mark/);
        enter(root, field(root));
        await settle();
        expect(cell(root, 3, 1).className).toMatch(/ex-match-mark-current/);
        expect(root.querySelectorAll('.ex-match-mark-current')).toHaveLength(1);
        // Moved off, it is forgotten, not hidden: coming back does not make it current again.
        key(root, 'ArrowDown');
        await painted();
        key(root, 'ArrowUp');
        await painted();
        expect(root.querySelectorAll('.ex-match-mark-current')).toHaveLength(0);
        // Closed, every mark goes.
        key(root, 'f', { ctrlKey: true });
        key(field(root), 'Escape');
        await painted();
        expect(root.querySelectorAll('.ex-match-mark')).toHaveLength(0);
    });

    it('ADR-0332 §4 / ADR-0003: a new search paints again only the rows whose marks moved', async () => {
        const { root } = grid({ findsHeaders: false });
        await painted();
        await find(root, 'nova');
        const rows = [...root.querySelectorAll('.ex-viewport > .ex-row')];
        // By identity: an element painted again is another element, however alike.
        const kept = (now: readonly Element[], indices: readonly number[]): boolean[] => indices.map(i => now[i] === rows[i]);
        typeInto(field(root), 'novak');
        await painted();
        const all = rows.map((_, i) => i);
        expect(kept([...root.querySelectorAll('.ex-viewport > .ex-row')], all)).toEqual(all.map(() => true));
        typeInto(field(root), 'osei');
        await painted();
        // Rows 0-2 hold Ishikawa, whose marks did not move; rows 3-8 lost or gained one.
        expect(kept([...root.querySelectorAll('.ex-viewport > .ex-row')], [0, 1, 2, 3, 6, 9])).toEqual([true, true, true, false, false, true]);
    });

    it('ADR-0332 §4: a source that finds by a rule of its own marks nothing, unless the Consumer passes the rule', async () => {
        const { source } = asking(GridSource.from(deals), async () => notFound);
        const { root, view } = grid({ source, findsHeaders: false });
        await painted();
        await find(root, 'novak');
        expect(root.querySelectorAll('.ex-match-mark')).toHaveLength(0);
        view.update({
            columns, source, rowHeight: 24, viewportHeight: 320, viewportWidth: 720, findsHeaders: false,
            findMatch: (deal, column, pattern) => column.name === 'Book' && deal.book === 'B1' && pattern.text === 'novak',
        });
        await painted();
        expect(cell(root, 1, 0).className).toMatch(/ex-match-mark/);
        expect(cell(root, 3, 1).className).not.toMatch(/ex-match-mark/);
    });

    it('ADR-0332 §4: a Consumer\'s rule is asked about a column that is not queryable, and never about an Action cell', async () => {
        // ExPivot's report columns carry values and are neither sorted nor filtered: its own Find searches them.
        const unqueryable: readonly GridColumn<Deal>[] = [
            { ...defined(columns[0], 'Book'), queryable: false },
            actionColumn<Deal>('Open', [{ name: 'open', label: 'Open' }], { width: 80 }),
        ];
        const asked: string[] = [];
        const { source } = asking(GridSource.from(deals), async () => notFound);
        const { root } = grid({
            columns: unqueryable, source, findsHeaders: false,
            findMatch: (deal, column) => { asked.push(column.name); return deal.book === 'B1'; },
        });
        await painted();
        await find(root, 'b1');
        expect(cell(root, 1, 0).className).toMatch(/ex-match-mark/);
        expect(cell(root, 1, 1).className).not.toMatch(/ex-match-mark/);
        expect(new Set(asked)).toEqual(new Set(['Book']));
    });
});

function replacing(onReplace?: GridOptions<Deal>['onReplace']): {
    readonly root: HTMLElement;
    readonly refusals: GridRefusal[];
    readonly edits: GridEditIntent<Deal>[];
    readonly requests: GridReplaceRequest[];
    readonly selection: () => GridSelection;
} {
    const edits: GridEditIntent<Deal>[] = [];
    const requests: GridReplaceRequest[] = [];
    const { root, refusals, selection } = grid({
        findsHeaders: false,
        onEdit: intent => { edits.push(intent); },
        onReplace: onReplace ?? (request => { requests.push(request); return replacedCells(3, request.rowSequenceVersion); }),
    });
    return { root, refusals, edits, requests, selection };
}
const replaceWith = (root: HTMLElement, text: string): void => {
    typeInto(q(root, '.ex-popover-find input.ex-find-replace-field') as HTMLInputElement, text);
};

describe('Replace (ADR-0332 §2 and §3)', () => {
    it('ADR-0332 §2: Replace finds a match first, then writes the current one through an Edit Intent and finds the next', async () => {
        const { root, edits, selection } = replacing();
        await painted();
        click(root, 10, rowY(0));
        key(root, 'h', { ctrlKey: true });
        typeInto(field(root), 'nov');
        replaceWith(root, 'Mov');
        press(root, 'button.ex-find-replace');
        await settle();
        expect(edits).toHaveLength(0);
        expect(selection().focus).toEqual({ row: 3, column: 1 });
        press(root, 'button.ex-find-replace');
        await settle();
        expect(edits.map(({ column, value, seenText, replacedText }) => ({ column, value, seenText, replacedText }))).toEqual([
            { column: 'Trader', value: 'Movak', seenText: 'Novak', replacedText: 'Novak' },
        ]);
        expect(selection().focus).toEqual({ row: 4, column: 1 });
    });

    it('ADR-0332 §2 / ADR-0034: a match that cannot be written writes nothing, is named with why, and stays current', async () => {
        const { root, edits, refusals, selection } = replacing();
        await painted();
        key(root, 'h', { ctrlKey: true });
        // Row 1's Notional is 10, and "1" in it replaced by "x" does not read as a number.
        typeInto(field(root), '10');
        q(root, '.ex-popover-find input.ex-find-whole-cell').click();
        replaceWith(root, 'x');
        press(root, 'button.ex-find-replace');
        await settle();
        expect(selection().focus).toEqual({ row: 1, column: 2 });
        press(root, 'button.ex-find-replace');
        await settle();
        expect(edits).toHaveLength(0);
        expect(outcome(root)).toBe("Nothing was replaced. Row 2, Notional: 'x' is not a number.");
        expect(refusals).toEqual([{ kind: 'find', reason: 'replaceRefused' }]);
        expect(cell(root, 1, 2).className).toMatch(/ex-match-mark-current/);
    });

    it('ADR-0332 §3: Replace All is one request the Consumer answers, now or later, and the panel says the count', async () => {
        let later: ((result: GridReplaceResult) => void) | undefined;
        const requests: GridReplaceRequest[] = [];
        const { root, refusals } = replacing(request => {
            requests.push(request);
            return new Promise(resolve => { later = resolve; });
        });
        await painted();
        key(root, 'h', { ctrlKey: true });
        typeInto(field(root), 'novak');
        replaceWith(root, 'Rossi');
        press(root, 'button.ex-find-replace-all');
        expect(requests).toEqual([{
            text: 'novak', matchCase: false, wholeCell: false, replacement: 'Rossi', scope: undefined,
            columns: ['Book', 'Trader', 'Notional'], editableColumns: ['Trader', 'Notional'], rowSequenceVersion: 0,
        }]);
        await settle();
        expect(root.getAttribute('data-ex-settled')).toBeNull();
        // While the answer is out, neither Replace can be pressed again.
        const buttons = ['button.ex-find-replace', 'button.ex-find-replace-all'].map(selector => q(root, `.ex-popover-find ${selector}`) as HTMLButtonElement);
        expect(buttons.map(button => button.disabled)).toEqual([true, true]);
        defined(later, 'the answer')(replacedCells(100, 0));
        await settle();
        expect(outcome(root)).toBe('Replaced 100 cells.');
        expect(buttons.map(button => button.disabled)).toEqual([false, false]);
        expect(requests).toHaveLength(1);
        expect(refusals).toEqual([]);
    });

    it('ADR-0332 §3: a refusal names the cells by their headers, none replaced is "No match", and an answer under another version is set aside', async () => {
        const answers: GridReplaceResult[] = [
            refusedCells([{ row: 0, column: 'Book', message: undefined }, { row: 4, column: 'Notional', message: 'too large.' }], 0),
            replacedCells(0, 0),
            replacedCells(5, 7),
        ];
        const { root, refusals } = replacing(() => defined(answers.shift(), 'an answer'));
        await painted();
        key(root, 'h', { ctrlKey: true });
        typeInto(field(root), 'b');
        press(root, 'button.ex-find-replace-all');
        expect(outcome(root)).toBe('Nothing was replaced. Row 1, Book: this column cannot be edited. Row 5, Notional: too large.');
        press(root, 'button.ex-find-replace-all');
        expect(outcome(root)).toBe('No match');
        press(root, 'button.ex-find-replace-all');
        expect(outcome(root)).toBe('The rows moved; find again');
        expect(refusals.map(refusal => refusal.kind === 'find' && refusal.reason)).toEqual(['replaceRefused', 'notFound', 'orderChanged']);
    });
});
