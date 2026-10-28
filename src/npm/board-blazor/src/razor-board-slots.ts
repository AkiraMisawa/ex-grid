// A Board's Chrome slots filled with Razor (ADR-0410): one Shared Script RazorSlotHost for every view of the Board,
// its grids' slots of the grid's kinds, its pivots' of the pivot's and the report grid's, and its charts' of the
// chart's, so each crosses to .NET and is wired as ExGrid's, ExPivot's and ExChart's own adapters cross and wire them.
// A chart's slots cross under `chart.` and their name, since the chart names its toolbar and menu as the pivot does. What a slot holds, the Settled Mark and the
// inputs (section 5), is held in the view the slot was mounted by: each view is made with mount functions of its own,
// which note the view of every slot they are handed. A slot is known by the view that mounted it, never by where it
// stands, since a view mounts a part before it places it.

import { gridRazorSlotKinds, isGridRazorSlotName, RazorSlotHost, type HostedSlotKind, type RazorSlotHostOptions } from '@ex-grid/blazor-shared';
import type { BoardChrome, BoardMounts, ShapedRow } from '@ex-grid/board';
import type { ChartChrome, ChartHandle, ChartOptions } from '@ex-grid/chart';
import { chartRazorSlotKinds } from '@ex-grid/chart-blazor';
import type { GridChrome, GridHandle, GridOptions } from '@ex-grid/core';
import type { PivotChrome, PivotHandle, PivotViewOptions } from '@ex-grid/pivot';
import { isPivotRazorSlotName, pivotRazorSlotKinds, pivotSlotHostOptions, type PivotGrids } from '@ex-grid/pivot-blazor';
import type { DotNetHandle } from './blazor-board';

/** The slots .NET fills, by the core's names: the grids', the pivots', the pivots' report grids' and the charts'. A
 * Board's grid Chrome dresses its pivots' report grids too, unless it has none (the Razor ExBoard's rule): .NET says
 * which Chrome's slots the report grids have, and the browser hands them on as named. */
export interface WireRazorSlots {
    readonly grid: readonly string[];
    readonly pivot: readonly string[];
    readonly pivotGrid: readonly string[];
    readonly chart: readonly string[];
}

/** The name a chart's slot crosses under, apart from the pivot's of the same name. */
const chartSlotPrefix = 'chart.';

type Mount = (slot: HTMLElement, context: unknown) => () => void;

type Mounts = Readonly<Record<string, Mount>>;

/** A hold on the Settled Mark of a view still being made, taken once it is. */
interface EarlyHold {
    readonly slot: HTMLElement;
    release: (() => void) | undefined;
    gone: boolean;
}

/** A view of the Board, as its slots know it: its kind, its handle once made, and the holds asked of it meanwhile. */
type ViewOfSlots =
    | { readonly kind: 'grid'; handle: GridHandle<ShapedRow> | undefined; readonly early: EarlyHold[] }
    | { readonly kind: 'pivot'; handle: PivotHandle | undefined; readonly early: EarlyHold[] }
    | { readonly kind: 'chart' };

/** The view of each slot the Board's views mounted. */
class BoardViews {
    // By the slot, so a slot a view let go goes with its element.
    readonly #ofSlot = new WeakMap<HTMLElement, ViewOfSlots>();

    /** The family's mounts, each making its view with mount functions that note the view of the slots they are handed. */
    mounts(family: BoardMounts): BoardMounts {
        return Object.freeze({
            grid: (element: HTMLElement, options: GridOptions<ShapedRow>) => {
                const view: ViewOfSlots = { kind: 'grid', handle: undefined, early: [] };
                const dressed = dressing<GridChrome<ShapedRow>>(chrome => this.#noting(view, chrome));
                const grid = family.grid(element, gridDressed(options, dressed));
                view.handle = grid;
                for (const early of view.early.splice(0)) if (!early.gone) early.release = grid.holdSettled();
                // The grid's own members, its getters among them, with an update that dresses what it is handed.
                // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- Object.create answers any; the members are the grid's
                return Object.freeze(Object.create(grid, {
                    update: { value: (next: GridOptions<ShapedRow>) => { grid.update(gridDressed(next, dressed)); }, enumerable: true },
                })) as GridHandle<ShapedRow>;
            },
            pivot: (element: HTMLElement, options: PivotViewOptions) => {
                const view: ViewOfSlots = { kind: 'pivot', handle: undefined, early: [] };
                const dressed = dressing<PivotChrome>(chrome => {
                    const { grid, ...own } = chrome;
                    const pivotOwn = this.#noting(view, own) as Omit<PivotChrome, 'grid'>;
                    return Object.freeze({ ...pivotOwn, ...(grid === undefined ? {} : { grid: this.#noting(view, grid) }) });
                });
                const pivot = family.pivot(element, pivotDressed(options, dressed));
                view.handle = pivot;
                for (const early of view.early.splice(0)) if (!early.gone) early.release = pivot.holdSettled(early.slot);
                // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- Object.create answers any; the members are the pivot's
                return Object.freeze(Object.create(pivot, {
                    update: { value: (next: PivotViewOptions) => { pivot.update(pivotDressed(next, dressed)); }, enumerable: true },
                })) as PivotHandle;
            },
            chart: (element: HTMLElement, options: ChartOptions) => {
                const view: ViewOfSlots = { kind: 'chart' };
                const dressed = dressing<ChartChrome>(chrome => this.#noting(view, chrome));
                const chart = family.chart(element, chartDressed(options, dressed));
                // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- Object.create answers any; the members are the chart's
                return Object.freeze(Object.create(chart, {
                    update: { value: (next: ChartOptions) => { chart.update(chartDressed(next, dressed)); }, enumerable: true },
                })) as ChartHandle;
            },
        } satisfies BoardMounts);
    }

    /** The view that mounted `slot`. */
    of(slot: Element): ViewOfSlots {
        const view = slot instanceof HTMLElement ? this.#ofSlot.get(slot) : undefined;
        if (view === undefined) throw new Error('Internal: a Razor slot of the Board that none of its views mounted.');
        return view;
    }

    // A Chrome's mount functions, each noting the view of the slot it is handed, then mounting it.
    #noting(view: ViewOfSlots, chrome: object): Mounts {
        // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- the Board's Chrome is this adapter's own mounts, each handed its own slot's context
        const mounts = chrome as Mounts;
        return Object.freeze(Object.fromEntries(Object.entries(mounts).map(([name, mount]): [string, Mount] => [name, (slot, context) => {
            this.#ofSlot.set(slot, view);
            return mount(slot, context);
        }])));
    }
}

/** A view's Chrome dressed as `dress` dresses it, once for each Chrome the Board hands the view: the Board hands the
 * same object while its Chrome stands, so a part the view keeps standing keeps its mount function, and is not drawn
 * again for an update. */
function dressing<Chrome extends object>(dress: (chrome: Chrome) => Chrome): (chrome: Chrome) => Chrome {
    const made = new WeakMap<Chrome, Chrome>();
    return chrome => {
        const known = made.get(chrome);
        if (known !== undefined) return known;
        const dressed = dress(chrome);
        made.set(chrome, dressed);
        return dressed;
    };
}

// A grid view's options, its Chrome's mount functions noting the view.
function gridDressed(options: GridOptions<ShapedRow>, dressed: (chrome: GridChrome<ShapedRow>) => GridChrome<ShapedRow>): GridOptions<ShapedRow> {
    const { chrome } = options;
    return chrome === undefined ? options : { ...options, chrome: dressed(chrome) };
}

// A chart view's options, its Chrome's mount functions noting the view.
function chartDressed(options: ChartOptions, dressed: (chrome: ChartChrome) => ChartChrome): ChartOptions {
    const { chrome } = options;
    return chrome === undefined ? options : { ...options, chrome: dressed(chrome) };
}

// A pivot view's options, its Chrome's mount functions and its report grids' noting the view.
function pivotDressed(options: PivotViewOptions, dressed: (chrome: PivotChrome) => PivotChrome): PivotViewOptions {
    const { chrome } = options;
    return chrome === undefined ? options : { ...options, chrome: dressed(chrome) };
}

/**
 * The Razor Chrome of one Board (ADR-0410): mount functions for the slots .NET fills, for every view of the Board, and
 * the contents .NET renders for each.
 */
export class RazorBoardSlots {
    readonly #host: RazorSlotHost;
    readonly #views = new BoardViews();
    readonly #names: WireRazorSlots;
    #chrome: BoardChrome | undefined;

    constructor(dotnet: DotNetHandle, names: WireRazorSlots) {
        for (const name of [...names.grid, ...names.pivotGrid]) {
            if (!isGridRazorSlotName(name)) throw new TypeError(`Internal: .NET named '${name}' as a grid slot its Chrome fills.`);
        }
        for (const name of names.pivot) {
            if (!isPivotRazorSlotName(name)) throw new TypeError(`Internal: .NET named '${name}' as a pivot slot its Chrome fills.`);
        }
        for (const name of names.chart) {
            if (!chartRazorSlotKinds.has(name)) throw new TypeError(`Internal: .NET named '${name}' as a chart slot its Chrome fills.`);
        }
        this.#names = names;
        const views = this.#views;
        // A pivot's slots hold as ExPivot's adapter holds them, in the grid of the pivot the slot stands for: the
        // report's, a Show Details grid's, or every one while the slot is not yet placed (ExPivot's own rule).
        const pivotGrids: PivotGrids = {
            gridAround: element => {
                const view = views.of(element);
                return view.kind === 'pivot' ? view.handle?.gridAround(element) : undefined;
            },
            holdSettled: element => hold(views.of(element), element),
        };
        const { order: pivotOrder, keyboardBack: pivotKeyboardBack } = pivotSlotHostOptions(pivotGrids);
        if (pivotOrder === undefined || pivotKeyboardBack === undefined) {
            throw new Error('Internal: ExPivot\'s slot host options hold no Input Order or no way to hand the keyboard back.');
        }
        const options: RazorSlotHostOptions = {
            hold: slot => hold(views.of(slot), slot),
            order: (until, slot) => {
                const view = views.of(slot);
                if (view.kind === 'chart') throw new Error('Internal: a chart\'s Razor slot took the keyboard, which no chart slot takes.');
                if (view.kind === 'pivot') pivotOrder(until, slot);
                else void gridOf(view).inOrder(() => until);
            },
            // A chart's contents hand the keyboard nowhere as they go, a menu whose command was clicked among them: the
            // chart takes it back from none of its own parts, as ExChart's own adapter has it (ADR-0281).
            keyboardBack: slot => {
                const view = views.of(slot);
                if (view.kind === 'pivot') pivotKeyboardBack(slot);
                else if (view.kind === 'grid') gridOf(view).focus();
            },
        };
        const kinds = new Map<string, HostedSlotKind>([
            ...gridRazorSlotKinds, ...pivotRazorSlotKinds, ...[...chartRazorSlotKinds].map(([name, kind]): [string, HostedSlotKind] => [chartSlotPrefix + name, kind]),
        ]);
        this.#host = new RazorSlotHost(dotnet, kinds, options);
    }

    /** The family's mounts, each making its view with mount functions that note the view of its slots, so a slot holds
     * what the view that mounted it holds. */
    mounts(family: BoardMounts): BoardMounts {
        return this.#views.mounts(family);
    }

    /** The Board's Chrome for the slots .NET fills, the same object for the Board's life. */
    chrome(): BoardChrome {
        if (this.#chrome !== undefined) return this.#chrome;
        const { grid, pivot, pivotGrid, chart } = this.#names;
        // One call for every slot, so each keeps its mount function: a standing part is not drawn again for an update.
        const mounts = this.#host.mounts([...new Set([...grid, ...pivot, ...pivotGrid]), ...chart.map(name => chartSlotPrefix + name)]);
        // The mounts of the slots named, each under the name its view reads, crossed to .NET under `prefix`'s.
        const pick = (names: readonly string[], prefix = ''): Record<string, Mount> =>
            Object.fromEntries(names.map(name => {
                const mount = mounts[prefix + name];
                if (mount === undefined) throw new Error(`Internal: the host made no mount for the slot '${prefix + name}'.`);
                return [name, mount];
            }));
        const gridChrome: GridChrome<unknown> | undefined = grid.length === 0 ? undefined : pick(grid);
        const pivotGridChrome: GridChrome<unknown> | undefined = pivotGrid.length === 0 ? undefined : pick(pivotGrid);
        // Each mount is handed the context of its own slot, the one its kind reads.
        const pivotOwn: Omit<PivotChrome, 'grid'> = pick(pivot);
        const pivotChrome: PivotChrome | undefined = pivot.length === 0 && pivotGridChrome === undefined ? undefined
            : Object.freeze({ ...pivotOwn, ...(pivotGridChrome === undefined ? {} : { grid: pivotGridChrome }) });
        const chartChrome: ChartChrome | undefined = chart.length === 0 ? undefined : Object.freeze(pick(chart, chartSlotPrefix));
        this.#chrome = Object.freeze({
            ...(gridChrome === undefined ? {} : { grid: gridChrome }),
            ...(pivotChrome === undefined ? {} : { pivot: pivotChrome }),
            ...(chartChrome === undefined ? {} : { chart: chartChrome }),
        });
        return this.#chrome;
    }

    /** .NET rendered the contents of slot `id` for the data numbered `version` in `contents`. */
    rendered(id: number, version: number, contents: HTMLElement | null): void {
        this.#host.rendered(id, version, contents);
    }

    /** The Board is going: every slot's contents go back to their holders, and nothing is asked of .NET again. */
    dispose(): void {
        this.#host.dispose();
    }
}

// The grid view a slot that takes the keyboard stands in, as ExGrid's adapter has it: only a part the user opened takes
// the keyboard, and the user opens nothing in a grid before it stands.
function gridOf(view: ViewOfSlots): GridHandle<ShapedRow> {
    if (view.kind !== 'grid') throw new Error(`Internal: a ${view.kind}'s Razor slot asked for its grid.`);
    if (view.handle === undefined) throw new Error('Internal: a Razor slot that takes the keyboard was mounted while its grid was being made.');
    return view.handle;
}

// Holds the Settled Mark of the view that mounted `slot`, or of the one being made once it is: a grid view's own, or
// the grid of the pivot the slot stands for. A chart has no Settled Mark, and ExChart's own adapter holds none.
function hold(view: ViewOfSlots, slot: Element): () => void {
    if (!(slot instanceof HTMLElement)) throw new TypeError('Internal: a Razor slot that is no HTML element.');
    if (view.kind === 'chart') return noHold;
    if (view.kind === 'grid' && view.handle !== undefined) return view.handle.holdSettled();
    if (view.kind === 'pivot' && view.handle !== undefined) return view.handle.holdSettled(slot);
    const early: EarlyHold = { slot, release: undefined, gone: false };
    view.early.push(early);
    return () => { early.gone = true; early.release?.(); };
}

// What a chart's slot holds: nothing, as the chart's own adapter holds nothing.
function noHold(): void {
    // A chart has no Settled Mark to let go.
}
