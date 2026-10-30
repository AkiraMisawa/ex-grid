// @vitest-environment happy-dom
// ST-1: after any sequence of operations the invariants hold. A scripted randomised sequence — presses, drags with
// the button held, every claimed key, scrolls, sorts, filters, row-height changes, Viewport resizes, density steps,
// a pager put on, resized and turned (ADR-0015), and the Layout Ceiling the browser reports moved low enough to
// compress the scroll (ADR-0053) — asserting after each step that every selection rectangle lies inside the extent,
// the Focus and the Extent lie inside the range holding the Focus (ADR-0052), the cell count is the sum of rectangle
// areas and so is the count the status line says, both scroll offsets lie within [0, max], the painted rows are
// exactly `sliceAt` of where the browser stands — the page's rows, placed where the arithmetic places them, under
// the ceiling's compression — the page on screen is one the result holds, a page the pager turned is the next one
// and starts at the top, the spacer is the arithmetic's height, and nothing throws. Each seed must act often enough
// paged, past the first page, compressed, compressed without a pager and compressed past the first page, and must
// turn a page while compressed, or the run fails; thirty steps halfway through are spent where paging and
// compression meet. The C# core's ConsistencyTests held its grid to the same, until that grid was removed.
//
// The painted rows are judged against ViewportGeometry itself, so a fault inside it is invisible here by
// construction: viewport-compression.test.ts and the shared viewport cases pin that arithmetic, and this test pins
// that the grid paints by it. The browser is modelled: it clamps every offset written to what the content reaches,
// and reports each move with a scroll event, as a browser does.
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridDensity } from '../src/grid-metrics';
import type { GridSelection } from '../src/selection/selection';
import { GridSource } from '../src/source/grid-source';
import { PageGeometry } from '../src/viewport/page-geometry';
import { ViewportGeometry } from '../src/viewport/viewport-geometry';
import { mount, painted, q, rowsOf, unmountAll, type Told } from './grid-harness';
import { Random } from './random';

afterEach(unmountAll);

interface Trade {
    readonly book: string;
    readonly amount: number;
    readonly desk: string;
    readonly active: boolean;
}

const headerHeightPx = 20;
const columnsWidthPx = 4 * 100;

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 100, editable: true },
    { name: 'Amount', type: 'number', value: trade => trade.amount, width: 100 },
    { name: 'Desk', type: 'text', value: trade => trade.desk, width: 100 },
    { name: 'Active', type: 'boolean', value: trade => trade.active, width: 100 },
];

function tradesOf(count: number): readonly Trade[] {
    return Array.from({ length: count }, (_, i) => ({
        book: `Row ${String(i).padStart(7, '0')}`, amount: ((i * 7919) % 1000) - 500, desk: `Desk ${i % 13}`, active: i % 3 !== 0,
    }));
}

const keys = [
    'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End',
    'PageUp', 'PageDown', 'Enter', 'Tab', ' ', 'a', 'Escape', 'F2', '5', '.', 'Backspace',
] as const;

/** What the grid is handed and told: its geometry, the page size, and the Layout Ceiling the browser reports. */
interface Layout {
    readonly rowHeightPx: number;
    readonly viewportHeightPx: number;
    readonly viewportWidthPx: number;
    readonly pageSize: number | undefined;
    readonly ceilingPx: number;
}

/** An element's inline style, refused by name where it has none. */
function styleOf(element: HTMLElement): string {
    const style = element.getAttribute('style');
    if (style === null) throw new Error(`${element.className} has no style.`);
    return style;
}

/** What a pattern matched in a text, refused by name where it matched nothing. */
function matched(pattern: RegExp, text: string, what: string): RegExpExecArray {
    const match = pattern.exec(text);
    if (match === null) throw new Error(`No ${what} in '${text}'.`);
    return match;
}

/** The scroller as a browser keeps it: every offset written is clamped to what the spacer reaches under the
 * ceiling, and a move is reported with a scroll event once the task that wrote it is done. */
class Browser {
    top = 0;
    left = 0;
    #moved = false;
    #inFrame = false;
    /** Every offset the grid wrote outside a frame since the last look, with the bound it had then. */
    readonly writes: { readonly axis: 'top' | 'left'; readonly px: number; readonly maxPx: number }[] = [];

    readonly #root: HTMLElement;
    readonly #told: Told;
    readonly #layout: () => Layout;

    constructor(root: HTMLElement, told: Told, layout: () => Layout) {
        this.#root = root;
        this.#told = told;
        this.#layout = layout;
        const scroller = q(root, '.ex-scroller');
        Object.defineProperty(scroller, 'scrollTop', {
            get: () => this.top,
            set: (px: number) => { this.#write('top', px); },
        });
        Object.defineProperty(scroller, 'scrollLeft', {
            get: () => this.left,
            set: (px: number) => { this.#write('left', px); },
        });
    }

    get maxTopPx(): number {
        const style = styleOf(q(this.#root, '.ex-spacer'));
        const spacer = Number(matched(/height: ([0-9.e+-]+)px/, style, 'the spacer\'s height')[1]);
        return Math.max(0, Math.min(spacer, this.#layout().ceilingPx) - this.#layout().viewportHeightPx);
    }

    get maxLeftPx(): number {
        return Math.max(0, columnsWidthPx - this.#layout().viewportWidthPx);
    }

    #write(axis: 'top' | 'left', px: number): void {
        const maxPx = axis === 'top' ? this.maxTopPx : this.maxLeftPx;
        // A reveal's repaint moves a pixel off in a frame and leans on the browser's clamp to learn which axis can move
        // (ADR-0012's note of 2026-10-08): that write is the browser's to clamp, and is not judged.
        if (!this.#inFrame) this.writes.push({ axis, px, maxPx });
        const clamped = Math.min(Math.max(px, 0), maxPx);
        if (clamped === (axis === 'top' ? this.top : this.left)) return;
        if (axis === 'top') this.top = clamped;
        else this.left = clamped;
        this.#moved = true;
    }

    /** The user scrolls to an offset, which the browser clamps. */
    async scroll(top: number, left: number): Promise<void> {
        this.top = Math.min(Math.max(top, 0), this.maxTopPx);
        this.left = Math.min(Math.max(left, 0), this.maxLeftPx);
        this.#moved = true;
        await this.follow();
    }

    /** Whatever the grid wrote, the browser now is, and says so; a position the content no longer reaches is clamped,
     * and said too. The frames asked for run, and the clock moves on while anything is armed: a fling settles, and a
     * drag held at an edge steps its band — four ticks of 200 ms, as a user sees under a second of it. */
    async follow(): Promise<void> {
        let ticks = 0;
        for (let round = 0; round < 100; round++) {
            await painted();
            const top = Math.min(this.top, this.maxTopPx);
            const left = Math.min(this.left, this.maxLeftPx);
            if (top !== this.top || left !== this.left) {
                [this.top, this.left] = [top, left];
                this.#moved = true;
            }
            if (this.#moved) {
                this.#moved = false;
                q(this.#root, '.ex-scroller').dispatchEvent(new Event('scroll'));
                continue;
            }
            if (this.#told.framesAsked > 0) {
                this.#inFrame = true;
                try {
                    this.#told.frame();
                } finally {
                    this.#inFrame = false;
                }
                continue;
            }
            if (ticks === 4 || this.#told.clock.pending === 0) return;
            ticks++;
            this.#told.clock.advance(200);
        }
        throw new Error('The grid and the browser did not come to rest in a hundred rounds.');
    }
}

/** The page on screen, as the pager says it: its index, where it starts and how many rows it holds — the whole
 * result without a pager (ADR-0015). */
function pageOf(root: HTMLElement, layout: Layout, total: number): { readonly index: number; readonly start: number; readonly rows: number } {
    const size = layout.pageSize;
    if (size === undefined) return { index: 0, start: 0, rows: total };
    const said = matched(/(\d+) \/ (\d+)/, q(root, '.ex-pager').textContent, 'the pager\'s page');
    const index = Number(said[1]) - 1;
    const start = index * size;
    return { index, start, rows: Math.min(Math.max(total - start, 0), size) };
}

/** The arithmetic the grid paints by, as ADR-0053 builds it: the page's rows under the header, in what the ceiling
 * leaves them once the header band is taken from it. */
function geometryOf(root: HTMLElement, layout: Layout, total: number): ViewportGeometry {
    const page = pageOf(root, layout, total);
    const rows = {
        rowHeightPx: layout.rowHeightPx,
        viewportHeightPx: layout.viewportHeightPx - headerHeightPx,
        totalRowCount: page.rows,
        scrollRoomPx: Math.max(1, layout.ceilingPx - headerHeightPx),
    };
    return layout.pageSize === undefined ? new ViewportGeometry(rows) : new PageGeometry(page.start, rows);
}

function expectInvariants(
    root: HTMLElement, selection: GridSelection | undefined, layout: Layout, total: number, browser: Browser, at: string,
): void {
    const page = pageOf(root, layout, total);
    expect(page.index >= 0 && (page.rows > 0 || total === 0), `the page shown holds rows of the result: ${at}`).toBe(true);
    if (selection !== undefined && !selection.isEmpty) {
        for (const range of selection.ranges) {
            expect(range.topRow >= 0 && range.leftColumn >= 0 && range.rowCount >= 1 && range.columnCount >= 1
                && range.topRow + range.rowCount <= total && range.leftColumn + range.columnCount <= columns.length,
            `${JSON.stringify(range)} lies inside ${total} x ${columns.length}: ${at}`).toBe(true);
        }
        const holds = (cell: { readonly row: number; readonly column: number }): boolean => {
            const range = defined(selection.focusRange, 'the Focus range');
            return cell.row >= range.topRow && cell.row < range.topRow + range.rowCount
                && cell.column >= range.leftColumn && cell.column < range.leftColumn + range.columnCount;
        };
        expect(holds(defined(selection.focus, 'the Focus')) && holds(defined(selection.extent, 'the Extent')),
            `the Focus and the Extent lie in the Focus range: ${at}`).toBe(true);
        const area = selection.ranges.reduce((sum, range) => sum + (range.rowCount * range.columnCount), 0);
        expect(selection.cellCount, `the cell count is the sum of the areas: ${at}`).toBe(area);
        const said = /(\d+) cells selected/.exec(root.querySelector('.ex-status')?.textContent ?? ''); // oxlint-disable-line ex-grid/no-stand-in-default -- the status line is taken out while it says nothing
        if (said !== null) expect(Number(said[1]), `the status line counts the areas: ${at}`).toBe(area);
    }

    for (const write of browser.writes) {
        expect(write.px >= 0 && write.px <= write.maxPx, `the grid wrote ${write.axis} ${write.px} within [0, ${write.maxPx}]: ${at}`).toBe(true);
    }
    browser.writes.length = 0;
    const view = q(root, '.ex-scroller');
    expect(view.scrollTop >= 0 && view.scrollTop <= browser.maxTopPx && view.scrollLeft >= 0 && view.scrollLeft <= browser.maxLeftPx,
        `the browser stands within its reach: ${at}`).toBe(true);

    const geometry = geometryOf(root, layout, total);
    const style = styleOf(q(root, '.ex-spacer'));
    expect(style, `the spacer is the arithmetic's height: ${at}`).toContain(`height: ${headerHeightPx + geometry.scrollHeightPx}px`);
    const slice = geometry.sliceAt(browser.top);
    const shown = rowsOf(root).map(row => Number(row.getAttribute('aria-rowindex')) - 2).toSorted((a, b) => a - b);
    const expected = slice === undefined ? [] : Array.from({ length: slice.count }, (_, i) => slice.start + i);
    expect(shown, `the painted rows are sliceAt(${browser.top}): ${at}`).toEqual(expected);
    if (slice !== undefined) {
        expect(styleOf(q(root, '.ex-viewport')), `the slice stands where the arithmetic places it: ${at}`)
            .toContain(`translateY(round(nearest, ${geometry.paintedTopPxOf(slice.start, browser.top)}px,`);
    }
}

async function run(seed: number, totalRows: number): Promise<void> {
    const random = new Random(seed);
    const rows = tradesOf(totalRows);
    const source = GridSource.from(rows);
    let selection: GridSelection | undefined;
    let layout: Layout = { rowHeightPx: 20, viewportHeightPx: 140, viewportWidthPx: 350, pageSize: undefined, ceilingPx: ViewportGeometry.maxScrollHeightPx };
    let density: GridDensity = 'standard';
    const edits: unknown[] = [];
    const refusals: unknown[] = [];
    const options = () => ({
        columns, source, density,
        rowHeight: layout.rowHeightPx, headerHeight: headerHeightPx,
        viewportHeight: layout.viewportHeightPx, viewportWidth: layout.viewportWidthPx,
        ...(layout.pageSize === undefined ? {} : { pageSize: layout.pageSize }),
        onSelectionChanged: (told: GridSelection) => { selection = told; },
        onEdit: (intent: unknown) => { edits.push(intent); },
        onRefused: (refusal: unknown) => { refusals.push(refusal); },
    });
    const { root, view, told } = mount(options());
    const browser = new Browser(root, told, () => layout);
    const total = (): number => defined(source.snapshot().totalCount, 'the source\'s count');
    const viewport = (): HTMLElement => q(root, '.ex-viewport');
    const mouse = (type: string, x: number, y: number, buttons: number): void => {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons, clientX: x, clientY: y });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        viewport().dispatchEvent(event);
    };
    await browser.follow();

    let paged = 0;
    let pastFirstPage = 0;
    let compressed = 0;
    let compressedUnpaged = 0;
    let compressedPastFirstPage = 0;
    let turnedWhileCompressed = 0;
    for (let step = 0; step < 500; step++) {
        // Halfway, the run is put where paging and compression meet — a pager of 60 rows under an 800px ceiling —
        // and acts there for thirty steps (presses, drags, keys, scrolls, page turns), whatever it drew before.
        const operation = step === 250 ? 13 : step > 250 && step < 280 ? random.pick([0, 1, 2, 3, 11, 11]) : random.below(13);
        let turnedTo: number | undefined;
        const pageBefore = pageOf(root, layout, total()).index;
        const compressedBefore = geometryOf(root, layout, total()).isCompressed;
        switch (operation) {
            case 0: // a press somewhere on screen
                mouse('mousedown', random.below(400), random.below(120), 1);
                mouse('mouseup', 0, 0, 0);
                break;
            case 1: { // a drag: press, move with the button held, and usually release
                mouse('mousedown', random.below(400), random.below(120), 1);
                mouse('mousemove', random.below(400), random.below(120), 1);
                if (random.below(4) !== 0) mouse('mouseup', 0, 0, 0);
                break;
            }
            case 2: // a claimed key, sometimes chorded
                root.dispatchEvent(new KeyboardEvent('keydown', {
                    key: random.pick(keys), ctrlKey: random.below(4) === 0, shiftKey: random.below(3) === 0, bubbles: true, cancelable: true,
                }));
                break;
            case 3: // the user scrolls to an arbitrary offset, which the browser clamps
                await browser.scroll(random.below(1000) / 1000 * 1.1 * geometryOf(root, layout, total()).maxScrollTopPx, random.below(100));
                break;
            case 4: // sort by a random column, sometimes back to none: the Consumer's source sorts (ADR-0001)
                source.setSorts(random.below(3) === 0
                    ? []
                    : [{ column: defined(columns[random.below(2)], 'a column').name, direction: random.below(2) === 0 ? 'ascending' : 'descending' }]);
                break;
            case 5: // filter on Active, or clear
                source.setFilter(random.below(2) === 0
                    ? undefined
                    : { columns: { Active: { clauses: [{ operator: 'equals', value: random.below(2) === 0 }] } } });
                break;
            case 6: // a geometry change: the row height
                layout = { ...layout, rowHeightPx: 18 + (random.below(4) * 4) };
                view.update(options());
                break;
            case 7: // the clock ticks while whatever is armed runs: an edge band's step, a fling's settle
                told.clock.advance(50);
                break;
            case 8: // a resize of the Viewport, on either axis
                layout = { ...layout, viewportHeightPx: random.pick([100, 140, 200]), viewportWidthPx: random.pick([250, 350, 500]) };
                view.update(options());
                break;
            case 9: // a density step: the heights are declared, so it moves neither (ADR-0028)
                density = random.pick(['compact', 'comfortable', 'standard', 'excel'] as const);
                view.update(options());
                break;
            case 10: // a pager put on, taken off, or set to another page size (ADR-0015)
                layout = { ...layout, pageSize: random.pick([undefined, undefined, 25, 60]) };
                view.update(options());
                break;
            case 11: { // the pager turned a page back or on, where it can be
                const buttons = [...root.querySelectorAll<HTMLButtonElement>('.ex-pager button')];
                const direction = random.below(2);
                const pressed = buttons[direction];
                if (buttons.length === 2 && pressed !== undefined && !pressed.disabled) {
                    // Known before the press, not read back from the pager afterwards. An edit that cannot be committed —
                    // its row moved under a sort while it was typed in (ADR-0142) — keeps the page, and says so.
                    const before = pageOf(root, layout, total()).index;
                    const refused = refusals.length;
                    // Pressed as a mouse presses it: a drag left held ends at the press, as it does in a browser.
                    for (const type of ['mousedown', 'mouseup', 'click']) {
                        pressed.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 }));
                    }
                    turnedTo = refusals.length === refused ? before + (direction === 0 ? -1 : 1) : before;
                }
                break;
            }
            case 12: // the browser reports another Layout Ceiling: a zoom, a scale (ADR-0053)
                layout = { ...layout, ceilingPx: random.pick([ViewportGeometry.maxScrollHeightPx, 1_500, 800, 6_000]) };
                told.ceiling(layout.ceilingPx);
                break;
            default: // 13, scripted: a pager and a low ceiling at once
                layout = { ...layout, pageSize: 60, ceilingPx: 800 };
                view.update(options());
                told.ceiling(800);
                break;
        }

        await browser.follow();
        const at = `seed ${seed}, step ${step}, operation ${operation}, ${JSON.stringify(layout)}, ${total()} rows, browser at ${browser.top}`;
        expectInvariants(root, selection, layout, total(), browser, at);
        // A page turned by the pager is the page next to the one before, and starts at the top (ADR-0015); a turn an
        // edit refused leaves the page as it was.
        if (turnedTo !== undefined) {
            const page = pageOf(root, layout, total()).index;
            expect(page, `the turn's page: ${at}`).toBe(turnedTo);
            if (page !== pageBefore) {
                expect(browser.top, `a turned page starts at the top: ${at}`).toBe(0);
                if (compressedBefore) turnedWhileCompressed++;
            }
        }
        // Counted where an operation acted in the state, not where the run merely stood in it.
        const acting = [0, 1, 2, 3, 11].includes(operation);
        const compressedNow = geometryOf(root, layout, total()).isCompressed;
        if (acting && layout.pageSize !== undefined) {
            paged++;
            if (pageOf(root, layout, total()).index > 0) pastFirstPage++;
        }
        if (acting && compressedNow) {
            compressed++;
            if (layout.pageSize === undefined) compressedUnpaged++;
            else if (pageOf(root, layout, total()).index > 0) compressedPastFirstPage++;
        }
    }

    const acted = { paged, pastFirstPage, compressed, compressedUnpaged, compressedPastFirstPage, turnedWhileCompressed };
    expect(paged >= 20 && pastFirstPage >= 20 && compressed >= 20 && compressedUnpaged >= 5
        && compressedPastFirstPage >= 1 && turnedWhileCompressed >= 1, `seed ${seed} acted ${JSON.stringify(acted)}`).toBe(true);
}

// ST-1's 10⁶ case is the Consumer's sorting and filtering of a million rows nearly all through (ADR-0001), so it runs
// only when asked for, as the Definition of Done's §22 Step 5 asks for it.
const millionSwitch = process.env['EXGRID_ST1_MILLION'] === '1';

describe('ST-1: five hundred random operations', () => {
    it.each([
        [20260901, 200],
        [424242, 200],
        [20260901, 1_000],
        [424242, 1_000],
    ])('ST-1: seed %i over %i rows leaves every invariant standing', async (seed, totalRows) => {
        await run(seed, totalRows);
    }, 60_000);

    it.runIf(millionSwitch)('ST-1: seed 20260901 over 10⁶ rows leaves every invariant standing (EXGRID_ST1_MILLION=1)', async () => {
        await run(20260901, 1_000_000);
    }, 600_000);
});
