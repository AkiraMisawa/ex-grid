// @vitest-environment happy-dom
// Layer 2: Find All in the grid (ADR-0470). The panel offers it where the source lists every match; the list is asked
// a page at a time under the source's version, with the headers the grid searches itself first; only the lines in
// view stand in the DOM; a line chosen moves the Focus as a step that found it would; and the list goes when the order
// moves, the search changes or the panel closes. The browser layer's find.spec.mjs presses it with real keys.
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import { findAllPage, type GridFindAllPage, type GridFindAllRequest } from '../src/finding/find-all';
import type { GridSelection } from '../src/selection/selection';
import { GridSource } from '../src/source/grid-source';
import type { InMemoryGridSource } from '../src/source/in-memory-source';
import { mount, painted, q, unmountAll } from './grid-harness';

interface Deal {
    readonly book: string;
    readonly trader: string;
    readonly notional: number;
}

// Trader repeats every three rows through four names: Novak in rows 3-5, 15-17, and so on, 100 rows of 400.
const traders = ['Ishikawa', 'Novak', 'Osei', 'Lee'];
const deals: readonly Deal[] = Array.from({ length: 400 }, (_, i) => ({
    book: `B${i % 4}`, trader: defined(traders[Math.floor(i / 3) % traders.length], 'a trader'), notional: i * 10,
}));
const columns: readonly GridColumn<Deal>[] = [
    { name: 'Book', type: 'text', value: deal => deal.book, width: 100 },
    { name: 'Trader', type: 'text', value: deal => deal.trader, width: 120 },
    { name: 'Notional', type: 'number', value: deal => deal.notional, width: 100 },
];

afterEach(() => { unmountAll(); });

function key(target: Element, name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    return event;
}
const settle = async (): Promise<void> => {
    for (let i = 0; i < 4; i++) await new Promise(resolve => { setTimeout(resolve, 0); });
};
const field = (root: HTMLElement): HTMLInputElement => q(root, '.ex-popover-find input.ex-find-field') as HTMLInputElement;
const findAllButton = (root: HTMLElement): HTMLButtonElement => q(root, '.ex-popover-find button.ex-find-all-button') as HTMLButtonElement;
const countLine = (root: HTMLElement): string | null => q(root, '.ex-popover-find .ex-find-all-count').textContent;
const list = (root: HTMLElement): HTMLElement => q(root, '.ex-popover-find .ex-find-all-list');
const lines = (root: HTMLElement): string[] => [...list(root).querySelectorAll<HTMLElement>('.ex-find-all-line')]
    .toSorted((a, b) => Number(a.dataset['exIndex']) - Number(b.dataset['exIndex']))
    .map(line => `${line.querySelector('.ex-find-all-where')?.textContent ?? ''} | ${line.querySelector('.ex-find-all-text')?.textContent ?? ''}`); // oxlint-disable-line ex-grid/no-stand-in-default -- a test's reading of a line not yet drawn

function typeInto(input: HTMLInputElement, text: string): void {
    input.value = text;
    input.dispatchEvent(new Event('input', { bubbles: true }));
}

/** `GridSource.from`'s source with its Find All's requests recorded, answered by it or by the test. */
function listing(source: InMemoryGridSource<Deal>, answer?: (request: GridFindAllRequest, signal: AbortSignal) => Promise<GridFindAllPage>,
    lists = true): {
    readonly source: GridSource<Deal>;
    readonly requests: GridFindAllRequest[];
} {
    const requests: GridFindAllRequest[] = [];
    const finding: GridSource<Deal> = {
        subscribe: listener => source.subscribe(listener),
        snapshot: () => source.snapshot(),
        setColumns: given => { source.setColumns(given); },
        setSorts: sorts => { source.setSorts(sorts); },
        setFilter: filter => { source.setFilter(filter); },
        needRange: range => source.needRange(range),
        rows: (range, signal) => source.rows(range, signal),
        distinctValues: (name, signal) => source.distinctValues(name, signal),
        find: (request, signal) => source.find(request, signal),
    };
    if (!lists) return { source: finding, requests };
    return {
        source: {
            subscribe: listener => source.subscribe(listener),
            snapshot: () => source.snapshot(),
            setColumns: given => { source.setColumns(given); },
            setSorts: sorts => { source.setSorts(sorts); },
            setFilter: filter => { source.setFilter(filter); },
            needRange: range => source.needRange(range),
            rows: (range, signal) => source.rows(range, signal),
            distinctValues: (name, signal) => source.distinctValues(name, signal),
            find: (request, signal) => source.find(request, signal),
            findAll: (request, signal) => {
                requests.push(request);
                return answer === undefined ? source.findAll(request, signal) : answer(request, signal);
            },
        },
        requests,
    };
}

function grid(options: Partial<GridOptions<Deal>> = {}): { readonly root: HTMLElement; readonly selection: () => GridSelection } {
    let selection: GridSelection | undefined;
    const { root } = mount<Deal>({
        columns, ...(options.source === undefined ? { rows: deals } : {}), rowHeight: 24, viewportHeight: 320, viewportWidth: 720,
        onSelectionChanged: next => { selection = next; }, ...options,
    });
    return { root, selection: () => defined(selection, 'the Selection heard') };
}

async function findAll(root: HTMLElement, text: string): Promise<void> {
    key(root, 'f', { ctrlKey: true });
    typeInto(field(root), text);
    await painted();
    findAllButton(root).click();
    await settle();
}

describe('Find All', () => {
    it('ADR-0470 §1: the panel offers Find All where the source lists every match, and not where it only finds the next', async () => {
        const inMemory = GridSource.from(deals);
        const { root } = grid({ source: listing(inMemory, undefined, false).source });
        await painted();
        key(root, 'f', { ctrlKey: true });
        await painted();
        expect(findAllButton(root).hidden).toBe(true);
        unmountAll();
        const { root: listed } = grid();
        await painted();
        key(listed, 'f', { ctrlKey: true });
        await painted();
        expect(findAllButton(listed).hidden).toBe(false);
        expect(findAllButton(listed).textContent).toBe('Find All');
    });

    it('ADR-0470 §2: the first page is asked with the search, the columns and the source\'s version, and the count is said exactly', async () => {
        const inMemory = GridSource.from(deals);
        const { source, requests } = listing(inMemory);
        const { root, selection } = grid({ source });
        await painted();
        await findAll(root, 'novak');
        expect(requests).toHaveLength(1);
        expect(requests[0]).toMatchObject({
            text: 'novak', matchCase: false, wholeCell: false, columns: ['Book', 'Trader', 'Notional'],
            rowSequenceVersion: inMemory.snapshot().rowSequenceVersion, start: 0, count: 100,
        });
        expect(requests[0]?.scope).toBeUndefined();
        expect(countLine(root)).toBe('100 cells found');
        // Only the lines in view stand in the DOM, however many matched (ADR-0470 §3).
        expect(lines(root).length).toBeLessThanOrEqual(9);
        expect(lines(root).slice(0, 2)).toEqual(['Row 4, Trader | Novak', 'Row 5, Trader | Novak']);
        // Find All moves nothing: the list is the whole set.
        expect(() => selection()).toThrow('the Selection heard');
    });

    it('ADR-0470 §1 / ADR-0333: the headers the grid searches come first, and a header line marks the header and leaves the Focus', async () => {
        const { root } = grid();
        await painted();
        await findAll(root, 'o');
        // Book and Notional match in the header row; Novak in 100 rows and Osei in 99.
        expect(countLine(root)).toBe('201 cells found');
        expect(lines(root).slice(0, 3)).toEqual(['Header Book | Book', 'Header Notional | Notional', 'Row 4, Trader | Novak']);
        list(root).querySelector<HTMLElement>('[data-ex-index="1"]')?.click();
        await settle();
        expect(q(root, '.ex-popover-find .ex-find-outcome').textContent).toBe('Found in the header Notional');
    });

    it('ADR-0470 §4: a line clicked moves the Focus there, and ↓, ↑, End and Home move through the list and the Focus with it', async () => {
        const { root, selection } = grid({ findsHeaders: false });
        await painted();
        await findAll(root, 'novak');
        list(root).querySelector<HTMLElement>('[data-ex-index="2"]')?.click();
        await settle();
        expect(selection().focus).toEqual({ row: 5, column: 1 });
        expect(list(root).getAttribute('aria-activedescendant')).toMatch(/-2$/u);
        key(list(root), 'ArrowDown');
        await settle();
        expect(selection().focus).toEqual({ row: 15, column: 1 });
        key(list(root), 'ArrowUp');
        await settle();
        expect(selection().focus).toEqual({ row: 5, column: 1 });
        key(list(root), 'End');
        await settle();
        expect(selection().focus).toEqual({ row: 399, column: 1 });
        key(list(root), 'Home');
        await settle();
        expect(selection().focus).toEqual({ row: 3, column: 1 });
        // The panel stays open, and the next step goes on from the line chosen.
        expect(root.querySelector(':scope > .ex-popover-find')).not.toBeNull();
    });

    it('ADR-0470 §2: scrolling the list asks the pages it comes to, and no page twice', async () => {
        const { source, requests } = listing(GridSource.from(deals));
        const { root } = grid({ source, findsHeaders: false });
        await painted();
        await findAll(root, 'o');
        expect(requests.map(({ start }) => start)).toEqual([0]);
        const box = list(root);
        box.scrollTop = 24 * 150;
        box.dispatchEvent(new Event('scroll'));
        await settle();
        expect(requests.map(({ start }) => start)).toEqual([0, 100]);
        box.scrollTop = 24 * 152;
        box.dispatchEvent(new Event('scroll'));
        await settle();
        expect(requests.map(({ start }) => start)).toEqual([0, 100]);
        expect(lines(root)[0]).toMatch(/\| (?:Novak|Osei)$/u);
    });

    it('ADR-0470 §5: a Sort moves the version, and the list goes and says so', async () => {
        const inMemory = GridSource.from(deals);
        const { root } = grid({ source: listing(inMemory).source });
        await painted();
        await findAll(root, 'novak');
        expect(countLine(root)).toBe('100 cells found');
        inMemory.setSorts([{ column: 'Notional', direction: 'descending' }]);
        await settle();
        expect(countLine(root)).toBe('The rows moved; find again');
        expect(lines(root)).toEqual([]);
    });

    it('ADR-0470 §5: a new search, or the panel closed, takes the list away', async () => {
        const { root } = grid();
        await painted();
        await findAll(root, 'novak');
        typeInto(field(root), 'nova');
        await settle();
        expect(root.querySelector('.ex-find-all')).toBeNull();
        findAllButton(root).click();
        await settle();
        expect(root.querySelector('.ex-find-all')).not.toBeNull();
        key(field(root), 'Escape');
        await settle();
        key(root, 'f', { ctrlKey: true });
        await painted();
        expect(root.querySelector('.ex-find-all')).toBeNull();
    });

    it('ADR-0470 §5: a page whose count moved since the first lists nothing more, and says the rows moved', async () => {
        const inMemory = GridSource.from(deals);
        let pages = 0;
        const { source } = listing(inMemory, async (request, signal) => {
            pages += 1;
            const page = await inMemory.findAll(request, signal);
            return pages === 1 ? page : findAllPage(page.total + 1, [...page.matches], page.rowSequenceVersion);
        });
        const { root } = grid({ source, findsHeaders: false });
        await painted();
        await findAll(root, 'o');
        const box = list(root);
        box.scrollTop = 24 * 150;
        box.dispatchEvent(new Event('scroll'));
        await settle();
        expect(countLine(root)).toBe('The rows moved; find again');
    });

    it('ADR-0470 §2 / ADR-0451: Escape while the first page is out cancels it, and no list is shown', async () => {
        const inMemory = GridSource.from(deals);
        let aborted = false;
        const { source } = listing(inMemory, (_request, signal) => new Promise((_resolve, reject) => {
            signal.addEventListener('abort', () => {
                aborted = true;
                reject(new DOMException('cancelled', 'AbortError'));
            });
        }));
        const { root } = grid({ source });
        await painted();
        await findAll(root, 'novak');
        key(field(root), 'Escape');
        await settle();
        expect(aborted).toBe(true);
        expect(root.querySelector('.ex-find-all')).toBeNull();
        // The Escape cancelled the page and closed nothing (ADR-0240 section 12).
        expect(root.querySelector(':scope > .ex-popover-find')).not.toBeNull();
    });
});
