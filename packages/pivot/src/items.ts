// A field's Items, asked of the report source (ADR-0066): what Filter… lists and the report filter band reads its
// summary from. They are listed under the Source Version of the report on screen, in the field's order, and listed
// again when the report moves to another Source Version, settings or order; meanwhile the earlier listing stays in
// view, marked as updating, as the Razor component's does (ADR-0066 refined). A source that cannot list them says
// why, never an empty list in its place.

import type {
    LocalPivotReportSource, PivotItemInfo, PivotReportItemsResult, PivotReportRefusal, PivotReportSettings, PivotSort,
} from '@ex-grid/pivot-engine';

/** The most Items Filter… lists (ADR-0061). */
export const itemListCap = 10_000;

/** What a field's Items are now. */
export interface FieldItems {
    /** The Items listed, in the field's order, and how many there are; none while a first listing is on its way, or
     * when they cannot be listed. */
    readonly listed: { readonly items: readonly PivotItemInfo[]; readonly total: number } | undefined;
    /** Why they cannot be listed. */
    readonly problem: ItemsProblem | undefined;
    /** Whether a listing is on its way: a first one, or the report's while an earlier one is in view. */
    readonly pending: boolean;
}

/** Why a field's Items cannot be listed: the source's refusal, or an error it threw. */
export type ItemsProblem = { readonly refusal: PivotReportRefusal } | { readonly error: unknown };

/** What a listing is asked under: the report on screen, and the field's order in it. */
export interface ItemsUnder {
    readonly reportSource: LocalPivotReportSource;
    /** The Report Version the Items are asked for. */
    readonly version: string;
    readonly sourceVersion: string;
    readonly settings: PivotReportSettings;
    /** The field's order, which the Items are listed in. */
    readonly sort: PivotSort | undefined;
}

/** The Items of `field` matching `search`, asked of the source under `under`: Filter…'s search past the Items
 * listed, which only the source can answer. */
export function searchItems(field: string, search: string, under: ItemsUnder, signal: AbortSignal): Promise<PivotReportItemsResult> {
    return under.reportSource.items(Object.freeze({ version: under.version, field, search, max: itemListCap }), signal);
}

interface Load {
    readonly under: ItemsUnder;
    /** Stops it once a newer listing of its field is asked for, or its source is no longer the pivot's. */
    readonly controller: AbortController;
    /** What it shows: none while it is on its way. */
    shown: FieldItems | undefined;
}

// Whether two listings are of the same Items in the same order: the same source, Source Version, settings and order.
// An order by a Value Field is the report's, which any change of the layout may change: such a listing is the report's
// alone.
function sameListing(a: ItemsUnder, b: ItemsUnder): boolean {
    return a.reportSource === b.reportSource && a.sourceVersion === b.sourceVersion && a.settings === b.settings
        && a.sort?.direction === b.sort?.direction && a.sort?.byValue === b.sort?.byValue
        && (a.sort?.byValue === undefined || a.version === b.version);
}

/** Nothing listed yet, and a first listing on its way. */
export const pendingFirst: FieldItems = Object.freeze({ listed: undefined, problem: undefined, pending: true });

/** The Items of each field the pivot has asked about, under its report source now. */
export class ItemListings {
    readonly #listed: () => void;
    // The newest listing asked for each field, and the last one that listed Items, kept in view while a newer one is
    // out. Both hold only the listings of one report source: a new source's Items are listed from nothing.
    readonly #loads = new Map<string, Load>();
    readonly #answered = new Map<string, FieldItems>();
    #reportSource: LocalPivotReportSource | undefined;

    /** `listed` is called when a listing lands. */
    constructor(listed: () => void) {
        this.#listed = listed;
    }

    /** The Items of `field` as they stand under `under`, asked for if they are not listed under it nor on their way.
     * The same listing is answered with the same object, so a caller can tell nothing changed. */
    of(field: string, under: ItemsUnder): FieldItems {
        if (under.reportSource !== this.#reportSource) this.#forgetAll(under.reportSource);
        let load = this.#loads.get(field);
        if (load === undefined || !sameListing(load.under, under)) {
            // A listing asked over is stopped: its Items would be shown only until the newer one's land.
            if (load?.shown === undefined) load?.controller.abort();
            load = { under, controller: new AbortController(), shown: undefined };
            this.#loads.set(field, load);
            void this.#list(field, load);
        }
        if (load.shown !== undefined) return load.shown;
        // An earlier listing stays in view until this one lands; with none, the first listing is on its way.
        return this.#answered.get(field) ?? pendingFirst;
    }

    /** Asks for `field`'s Items again the next time they are read, when its listing failed: Filter… opened again
     * lists them anew rather than repeat a failure that may have passed. */
    retry(field: string): void {
        if (this.#loads.get(field)?.shown?.problem !== undefined) this.#loads.delete(field);
    }

    /** Stops every listing on its way and forgets every listing: the pivot is gone. */
    dispose(): void {
        this.#forgetAll(undefined);
    }

    #forgetAll(reportSource: LocalPivotReportSource | undefined): void {
        for (const load of this.#loads.values()) load.controller.abort();
        this.#loads.clear();
        this.#answered.clear();
        this.#reportSource = reportSource;
    }

    async #list(field: string, load: Load): Promise<void> {
        const { under, controller: { signal } } = load;
        let result;
        try {
            result = await under.reportSource.items(Object.freeze({ version: under.version, field, max: itemListCap }), signal);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a listing asked over, or of a source no longer the pivot's, is nobody's; the current one's failure is said where its Items are shown, and rethrown
            if (signal.aborted) return;
            this.#landed(field, load, Object.freeze({ listed: undefined, problem: Object.freeze({ error }), pending: false }));
            throw error;
        }
        if (signal.aborted) return;
        this.#landed(field, load, Object.freeze(result.refusal === undefined
            ? { listed: Object.freeze({ items: result.items, total: result.total }), problem: undefined, pending: false }
            : { listed: undefined, problem: Object.freeze({ refusal: result.refusal }), pending: false }));
    }

    // Its Items replace the ones kept in view while a newer listing is out; Items that could not be listed leave none
    // in view, as the Razor component's do.
    #landed(field: string, load: Load, shown: FieldItems): void {
        load.shown = shown;
        if (shown.listed === undefined) this.#answered.delete(field);
        else this.#answered.set(field, Object.freeze({ ...shown, pending: true }));
        this.#listed();
    }
}
