// Filter… (ADR-0061): a field's Items, each ticked while shown, (Select All), a search that narrows the list, and
// OK / Cancel, in the markup and under the classes the Razor component's PivotItemFilterView draws, so one stylesheet
// paints both (ADR-0350 section 4). The ticks are the panel's draft. The Items are the source's, listed under the
// report's Source Version (ADR-0066); while a first listing is on its way the panel says so, and when the source
// cannot list them it says why, never an empty list in its place. While a new version's Items are on their way, the
// earlier version's stay listed and the list is marked busy: Hidden Items are keys, which name the same Items under
// any version, so ticking and applying against them is safe (ADR-0066 refined).
//
// The search narrows the Items held, as the pivot's locale lowers text; past the Items Filter… holds, only the
// source can answer it, and the answer to a search typed since is dropped.

import {
    formatPivotCount, pivotItemKeyId, placementOf, placementsIn, refusalWord, setHiddenItems, type PivotItemInfo, type PivotItemKey, type PivotLayout,
    type PivotReportItemsResult,
} from '@ex-grid/pivot-engine';
import { lowered } from './field-list';
import { itemListCap, type FieldItems, type ItemsProblem } from './items';
import { popupFrame, type PivotMenuPlace } from './menu';
import { defined, elementAt } from './must';
import { actions, element, type PanelHandlers, type PivotPanel } from './panels';
import type { PivotScreenWords } from './words';

/** What Filter… is opened on. */
export interface ItemFilterModel {
    /** The layout OK's change is taken against. */
    readonly layout: PivotLayout;
    readonly field: string;
    readonly caption: string;
    /** Where it stands: under the field's entry in the Field List, or under its button in the report filter band. */
    readonly place: PivotMenuPlace;
    /** The locale the search lowers text in, and the count in the note is written in. */
    readonly locale: string;
    readonly words: PivotScreenWords;
}

/** Whom Filter… calls back: OK and Cancel, the field's Items, and the source's search past the Items held. */
export interface ItemFilterHandlers extends PanelHandlers {
    /** The field's Items as they stand now: the same object while they have not changed. */
    readonly items: () => FieldItems;
    /** The Items matching `search`, asked of the source under the report on screen. */
    readonly search: (search: string, signal: AbortSignal) => Promise<PivotReportItemsResult>;
}

/** What the source answered for a search: the Items, why it could not, or nothing yet. */
type Found = { readonly listed: PivotReportItemsResult } | { readonly problem: ItemsProblem } | 'asked';

interface Search {
    readonly text: string;
    // The listing it searched past, so a newer listing searches again.
    readonly under: FieldItems['listed'];
    readonly controller: AbortController;
    found: Found;
}

interface DrawnItem {
    readonly row: HTMLLabelElement;
    readonly box: HTMLInputElement;
    readonly text: HTMLSpanElement;
}

/** The sentence for a source's refusal or failure to list Items, as the Razor component's ProblemText says it. */
export function itemsProblemText(problem: ItemsProblem, words: PivotScreenWords): string {
    if ('error' in problem) return words.failed(problem.error);
    const { refusal } = problem;
    if (refusal.sourceRefusal !== undefined) return words.sourceRefused(refusal.sourceRefusal);
    // A report the source no longer holds asks for a refresh, as data that moved on does (ADR-0066).
    return refusal.kind === 'reportVersionNotHeld' ? words.word('data-changed') : words.fill('source-refused', refusal.message);
}

/** Filter…: drawn again by the pivot as the field's Items land. */
export class ItemFilterPanel implements PivotPanel {
    /** Its frame, which the pivot places. */
    readonly element: HTMLDivElement;
    readonly #model: ItemFilterModel;
    readonly #handlers: ItemFilterHandlers;
    readonly #searchBox: HTMLInputElement;
    readonly #list: HTMLDivElement;
    readonly #selectAll: DrawnItem;
    // The notes the list shows in place of Items, or under them; each made once, so drawing again moves nothing.
    readonly #unavailable: HTMLDivElement;
    readonly #loading: HTMLDivElement;
    readonly #tooMany: HTMLDivElement;
    readonly #noMatch: HTMLDivElement;
    readonly #refusalBox: HTMLDivElement;
    readonly #ok: HTMLButtonElement;
    // The draft: the Items unticked, by key id.
    readonly #hidden = new Map<string, PivotItemKey>();
    readonly #drawn = new Map<string, DrawnItem>();
    #items: FieldItems;
    #search: Search | undefined;
    #refusal: string | undefined;
    #closed = false;
    // What a press of (Select All) or OK acts on: the Items listed now, and those held.
    #listed: readonly PivotItemInfo[] = [];
    #held: readonly PivotItemInfo[] = [];
    // Each Item's key id and lowered label, worked out once while its listing is in view: a draw on every key typed
    // and every tick reads them for every Item.
    readonly #ids = new WeakMap<PivotItemInfo, string>();
    readonly #labels = new WeakMap<PivotItemInfo, string>();
    #allHeld = true;
    #canApply = false;

    constructor(document: Document, model: ItemFilterModel, handlers: ItemFilterHandlers) {
        this.#model = model;
        this.#handlers = handlers;
        const { words } = model;
        for (const key of placementHidden(model.layout, model.field)) this.#hidden.set(pivotItemKeyId(key), key);
        this.element = popupFrame(document, model.place, 'dialog', handlers.cancel);
        this.element.setAttribute('aria-label', words.fill('filter-of', model.caption));
        const panel = element(document, 'div', 'ex-pivot-panel ex-pivot-item-filter');
        this.#searchBox = element(document, 'input', 'ex-pivot-search');
        this.#searchBox.type = 'search';
        this.#searchBox.placeholder = words.word('search-items');
        this.#searchBox.setAttribute('aria-label', words.word('search-items'));
        this.#searchBox.addEventListener('input', () => {
            this.#refusal = undefined;
            this.#render();
        });
        this.#list = element(document, 'div', 'ex-pivot-item-list');
        this.#list.setAttribute('role', 'group');
        this.#list.setAttribute('aria-label', model.caption);
        this.#selectAll = this.#item(document, 'ex-pivot-item ex-pivot-select-all', tick => {
            for (const item of this.#listed) this.#tick(item.key, tick);
        });
        this.#selectAll.text.textContent = words.word('select-all');
        this.#unavailable = element(document, 'div', 'ex-pivot-refusal ex-pivot-unavailable');
        this.#unavailable.setAttribute('role', 'alert');
        this.#loading = element(document, 'div', 'ex-pivot-note ex-pivot-loading');
        this.#loading.setAttribute('role', 'status');
        this.#loading.textContent = words.word('loading');
        this.#tooMany = element(document, 'div', 'ex-pivot-note');
        this.#tooMany.textContent = words.fill('too-many-items', formatPivotCount(itemListCap, model.locale));
        this.#noMatch = element(document, 'div', 'ex-pivot-note');
        this.#noMatch.textContent = words.word('no-items-match');
        this.#refusalBox = element(document, 'div', 'ex-pivot-refusal');
        this.#refusalBox.setAttribute('role', 'alert');
        const exits = actions(document, words, () => { this.#apply(); }, handlers.cancel);
        this.#ok = exits.ok;
        panel.append(this.#searchBox, this.#list, exits.box);
        this.element.append(panel);
        this.#items = handlers.items();
        this.#render();
    }

    /** Draws the field's Items again, when they have changed. */
    redraw(): void {
        const items = this.#handlers.items();
        if (items === this.#items) return;
        this.#items = items;
        this.#render();
    }

    /** Gives the keyboard to the search. */
    takeKeyboard(): void {
        this.#searchBox.focus();
    }

    /** Stops a search on its way: the panel has closed. */
    close(): void {
        this.#closed = true;
        this.#search?.controller.abort();
        this.#search = undefined;
    }

    #item(document: Document, className: string, changed: (tick: boolean) => void): DrawnItem {
        const row = element(document, 'label', className);
        const box = element(document, 'input');
        box.type = 'checkbox';
        const text = element(document, 'span');
        row.append(box, text);
        box.addEventListener('change', () => {
            changed(box.checked);
            this.#refusal = undefined;
            this.#render();
        });
        return { row, box, text };
    }

    #idOf(item: PivotItemInfo): string {
        let id = this.#ids.get(item);
        if (id === undefined) {
            id = pivotItemKeyId(item.key);
            this.#ids.set(item, id);
        }
        return id;
    }

    #loweredLabel(item: PivotItemInfo): string {
        let label = this.#labels.get(item);
        if (label === undefined) {
            label = lowered(item.label, this.#model.locale);
            this.#labels.set(item, label);
        }
        return label;
    }

    #tick(key: PivotItemKey, tick: boolean): void {
        const id = pivotItemKeyId(key);
        if (tick) this.#hidden.delete(id);
        else this.#hidden.set(id, key);
    }

    // The source's answer for the search typed, past the Items held; asked again for a newer listing.
    #found(text: string, under: FieldItems['listed']): Found {
        const search = this.#search;
        if (search?.text === text && search.under === under) return search.found;
        search?.controller.abort();
        const asked: Search = { text, under, controller: new AbortController(), found: 'asked' };
        this.#search = asked;
        void this.#ask(asked);
        return asked.found;
    }

    async #ask(asked: Search): Promise<void> {
        let listed;
        try {
            listed = await this.#handlers.search(asked.text, asked.controller.signal);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a search typed over, or of a panel closed, is nobody's; the current one's failure is said in the panel and rethrown
            if (this.#search !== asked) return;
            asked.found = Object.freeze({ problem: Object.freeze({ error }) });
            this.#render();
            throw error;
        }
        if (this.#search !== asked) return;
        asked.found = Object.freeze(listed.refusal === undefined ? { listed } : { problem: Object.freeze({ refusal: listed.refusal }) });
        this.#render();
    }

    #render(): void {
        if (this.#closed) return;
        const { words, locale } = this.#model;
        const items = this.#items;
        const text = this.#searchBox.value;
        let loading = items.listed === undefined && items.problem === undefined;
        let problem = items.problem;
        let matches: readonly PivotItemInfo[] = [];
        let matchCount = 0;
        const page = items.listed;
        this.#allHeld = page === undefined || page.total <= page.items.length;
        if (page !== undefined && !this.#allHeld && text !== '') {
            // More Items than Filter… holds: the search is the source's to answer.
            const found = this.#found(text, page);
            if (found === 'asked') loading = true;
            else if ('problem' in found) problem = found.problem;
            else {
                matches = found.listed.items;
                matchCount = found.listed.total;
            }
        } else if (page !== undefined) {
            this.#search?.controller.abort();
            this.#search = undefined;
            const search = lowered(text, locale);
            matches = search === '' ? page.items : page.items.filter(item => this.#loweredLabel(item).includes(search));
            matchCount = search === '' ? page.total : matches.length;
        }
        const listed = matches.slice(0, itemListCap);
        this.#listed = listed;
        // The Items in view, an earlier version's while the report's are on their way (ADR-0066 refined).
        this.#held = page === undefined ? [] : page.items;
        const unavailable = problem === undefined ? undefined : itemsProblemText(problem, words);
        this.#canApply = !loading && unavailable === undefined
            && (!this.#allHeld || this.#held.length === 0 || this.#held.some(item => !this.#hidden.has(this.#idOf(item))));

        if (loading || items.pending) this.#list.setAttribute('aria-busy', 'true');
        else this.#list.removeAttribute('aria-busy');
        const parts: HTMLElement[] = [];
        if (unavailable !== undefined) {
            this.#unavailable.textContent = unavailable;
            parts.push(this.#unavailable);
        } else if (loading) {
            parts.push(this.#loading);
        } else {
            const document = this.element.ownerDocument;
            const ticked = listed.filter(item => !this.#hidden.has(this.#idOf(item))).length;
            if (listed.length > 0) {
                this.#selectAll.box.checked = ticked === listed.length;
                parts.push(this.#selectAll.row);
            }
            // An Item keeps its row while it is listed, so the one with the keyboard keeps it.
            const kept = new Map<string, DrawnItem>();
            for (const item of listed) {
                const id = this.#idOf(item);
                const { key } = item;
                const drawn = this.#drawn.get(id) ?? this.#item(document, 'ex-pivot-item', tick => { this.#tick(key, tick); });
                drawn.box.checked = !this.#hidden.has(id);
                if (drawn.text.textContent !== item.label) drawn.text.textContent = item.label;
                kept.set(id, drawn);
                parts.push(drawn.row);
            }
            this.#drawn.clear();
            for (const [id, drawn] of kept) this.#drawn.set(id, drawn);
            if (matchCount > listed.length) parts.push(this.#tooMany);
            if (listed.length === 0) parts.push(this.#noMatch);
        }
        const children = this.#list.children;
        if (parts.length !== children.length || parts.some((part, i) => children[i] !== part)) this.#list.replaceChildren(...parts);

        const refusal = this.#refusal ?? (this.#canApply || loading || unavailable !== undefined ? undefined : words.word('refused-hides-every-item'));
        if (refusal === undefined) this.#refusalBox.remove();
        else {
            this.#refusalBox.textContent = refusal;
            if (this.#refusalBox.previousSibling !== this.#list) this.#list.after(this.#refusalBox);
        }
        this.#ok.disabled = !this.#canApply;
    }

    #apply(): void {
        if (!this.#canApply) return;
        const { layout, field, words } = this.#model;
        // With more Items than Filter… holds, that every one is unticked cannot be told, and nothing is refused for
        // it: the rule needs every Item (ADR-0060).
        const result = setHiddenItems(layout, field, this.#hidden.values(), this.#allHeld ? this.#held.map(item => item.key) : []);
        if (result.refusal !== undefined) {
            this.#refusal = words.word(refusalWord(result.refusal));
            this.#render();
            return;
        }
        const before = new Set(placementHidden(layout, field).map(key => pivotItemKeyId(key)));
        const after = placementHidden(result.layout, field).map(key => pivotItemKeyId(key));
        if (after.length === before.size && after.every(id => before.has(id))) {
            this.#handlers.cancel();
            return;
        }
        this.#handlers.apply(result.layout);
    }
}

// The Hidden Items of `field` in `layout`, which Filter… is only opened on while the field is placed.
function placementHidden(layout: PivotLayout, field: string): readonly PivotItemKey[] {
    const at = defined(placementOf(layout, field), `the placement of '${field}'`);
    return elementAt(placementsIn(layout, at.area), at.index).hiddenItems;
}
