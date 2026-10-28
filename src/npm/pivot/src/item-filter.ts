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
import { ViewListeners, type PivotChromeSlot, type PivotItemFilterSlotContext, type PivotItemFilterView } from './chrome';
import { popupFrame, type PivotMenuPlace } from './menu';
import { defined, elementAt } from './must';
import { actions, element, slottedPanel, type PanelDraft, type PanelHandlers, type PivotPanel } from './panels';
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
    return words.reportRefused(problem.refusal);
}

/** Filter…' draft: the ticks and the search, and the source's answer to a search past the Items held. It is drawn
 * again by the pivot as the field's Items land, and by the built-in markup and a Chrome's slot alike. */
export class ItemFilterDraft implements PanelDraft<PivotItemFilterView> {
    readonly listeners = new ViewListeners();
    readonly #model: ItemFilterModel;
    readonly #handlers: ItemFilterHandlers;
    // The draft: the Items unticked, by key id.
    readonly #hidden = new Map<string, PivotItemKey>();
    #text = '';
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
    #view: PivotItemFilterView;

    constructor(model: ItemFilterModel, handlers: ItemFilterHandlers) {
        this.#model = model;
        this.#handlers = handlers;
        for (const key of placementHidden(model.layout, model.field)) this.#hidden.set(pivotItemKeyId(key), key);
        this.#items = handlers.items();
        this.#view = this.#compute();
    }

    get view(): PivotItemFilterView {
        return this.#view;
    }

    /** Each listed Item's key id, by its place in the view's `items`. */
    idAt(index: number): string {
        return this.#idOf(elementAt(this.#listed, index));
    }

    setSearch(text: string): void {
        if (typeof text !== 'string') throw new TypeError('setSearch is handed the search as typed.');
        this.#text = text;
        this.#refusal = undefined;
        this.#render();
    }

    setTicked(key: PivotItemKey, ticked: boolean): void {
        if (typeof ticked !== 'boolean') throw new TypeError('setTicked is handed the Item and whether it is ticked.');
        const id = pivotItemKeyId(key);
        // Only an Item the panel lists now is ticked: a press on one a newer listing took away names nothing the user
        // sees, and changes nothing (spine rule 6).
        if (!this.#listed.some(item => this.#idOf(item) === id)) return;
        this.#tick(key, ticked);
        this.#refusal = undefined;
        this.#render();
    }

    setAll(ticked: boolean): void {
        if (typeof ticked !== 'boolean') throw new TypeError('setAll is handed whether every Item listed is ticked.');
        for (const item of this.#listed) this.#tick(item.key, ticked);
        this.#refusal = undefined;
        this.#render();
    }

    /** Draws the field's Items again, when they have changed. */
    redraw(): void {
        const items = this.#handlers.items();
        if (items === this.#items) return;
        this.#items = items;
        this.#render();
    }

    /** Stops a search on its way: the panel has closed. */
    close(): void {
        this.#closed = true;
        this.#search?.controller.abort();
        this.#search = undefined;
        this.listeners.clear();
    }

    apply(): void {
        if (!this.#view.canApply) return;
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
        this.#view = this.#compute();
        this.listeners.tell();
    }

    #compute(): PivotItemFilterView {
        const { words, locale } = this.#model;
        const items = this.#items;
        const text = this.#text;
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
        const canApply = !loading && unavailable === undefined
            && (!this.#allHeld || this.#held.length === 0 || this.#held.some(item => !this.#hidden.has(this.#idOf(item))));
        const shown = unavailable === undefined && !loading;
        const choices = shown
            ? listed.map(item => Object.freeze({ key: item.key, label: item.label, ticked: !this.#hidden.has(this.#idOf(item)) }))
            : [];
        const ticked = choices.filter(choice => choice.ticked).length;
        const note = !shown ? undefined
            : matchCount > listed.length ? words.fill('too-many-items', formatPivotCount(itemListCap, locale))
            : listed.length === 0 ? words.word('no-items-match')
            : undefined;
        const refusal = this.#refusal ?? (canApply || loading || unavailable !== undefined ? undefined : words.word('refused-hides-every-item'));
        return Object.freeze({
            caption: this.#model.caption,
            search: text,
            items: Object.freeze(choices),
            allTicked: ticked === 0 ? false : ticked === choices.length ? true : undefined,
            loading,
            updating: items.pending && !loading,
            canApply,
            ...(unavailable === undefined ? {} : { unavailable }),
            ...(note === undefined ? {} : { note }),
            ...(refusal === undefined ? {} : { refusal }),
        });
    }
}

/** Filter…, drawn by `mount` where a Chrome gives one: drawn again by the pivot as the field's Items land. */
export function itemFilterPanel(document: Document, model: ItemFilterModel, handlers: ItemFilterHandlers,
    mount?: PivotChromeSlot<PivotItemFilterSlotContext>): PivotPanel {
    const draft = new ItemFilterDraft(model, handlers);
    const label = model.words.fill('filter-of', model.caption);
    if (mount === undefined) return new ItemFilterPanel(document, model, handlers, draft, label);
    return slottedPanel(document, label, model.place, 'itemFilter', mount, draft, handlers.cancel, model.words, input => ({
        setSearch: input((text: string) => { draft.setSearch(text); }),
        setTicked: input((key: PivotItemKey, ticked: boolean) => { draft.setTicked(key, ticked); }),
        setAll: input((ticked: boolean) => { draft.setAll(ticked); }),
    }), () => { draft.redraw(); }, () => { draft.close(); });
}

/** The built-in Filter…, drawn from its draft. */
class ItemFilterPanel implements PivotPanel {
    /** Its frame, which the pivot places. */
    readonly element: HTMLDivElement;
    readonly #draft: ItemFilterDraft;
    readonly #searchBox: HTMLInputElement;
    readonly #list: HTMLDivElement;
    readonly #selectAll: DrawnItem;
    // The notes the list shows in place of Items, or under them; each made once, so drawing again moves nothing.
    readonly #unavailable: HTMLDivElement;
    readonly #loading: HTMLDivElement;
    readonly #note: HTMLDivElement;
    readonly #refusalBox: HTMLDivElement;
    readonly #ok: HTMLButtonElement;
    readonly #drawn = new Map<string, DrawnItem>();

    constructor(document: Document, model: ItemFilterModel, handlers: ItemFilterHandlers, draft: ItemFilterDraft, label: string) {
        this.#draft = draft;
        const { words } = model;
        this.element = popupFrame(document, model.place, 'dialog', handlers.cancel);
        this.element.setAttribute('aria-label', label);
        const panel = element(document, 'div', 'ex-pivot-panel ex-pivot-item-filter');
        this.#searchBox = element(document, 'input', 'ex-pivot-search');
        this.#searchBox.type = 'search';
        this.#searchBox.placeholder = words.word('search-items');
        this.#searchBox.setAttribute('aria-label', words.word('search-items'));
        this.#searchBox.addEventListener('input', () => { draft.setSearch(this.#searchBox.value); });
        this.#list = element(document, 'div', 'ex-pivot-item-list');
        this.#list.setAttribute('role', 'group');
        this.#list.setAttribute('aria-label', model.caption);
        this.#selectAll = this.#item(document, 'ex-pivot-item ex-pivot-select-all', tick => { draft.setAll(tick); });
        this.#selectAll.text.textContent = words.word('select-all');
        this.#unavailable = element(document, 'div', 'ex-pivot-refusal ex-pivot-unavailable');
        this.#unavailable.setAttribute('role', 'alert');
        this.#loading = element(document, 'div', 'ex-pivot-note ex-pivot-loading');
        this.#loading.setAttribute('role', 'status');
        this.#loading.textContent = words.word('loading');
        this.#note = element(document, 'div', 'ex-pivot-note');
        this.#refusalBox = element(document, 'div', 'ex-pivot-refusal');
        this.#refusalBox.setAttribute('role', 'alert');
        const exits = actions(document, words, () => { draft.apply(); }, handlers.cancel);
        this.#ok = exits.ok;
        panel.append(this.#searchBox, this.#list, exits.box);
        this.element.append(panel);
        draft.listeners.subscribe(() => { this.#render(); });
        this.#render();
    }

    /** Draws the field's Items again, when they have changed. */
    redraw(): void {
        this.#draft.redraw();
    }

    /** Gives the keyboard to the search. */
    takeKeyboard(): void {
        this.#searchBox.focus();
    }

    /** Stops a search on its way: the panel has closed. */
    close(): void {
        this.#draft.close();
    }

    #item(document: Document, className: string, changed: (tick: boolean) => void): DrawnItem {
        const row = element(document, 'label', className);
        const box = element(document, 'input');
        box.type = 'checkbox';
        const text = element(document, 'span');
        row.append(box, text);
        box.addEventListener('change', () => { changed(box.checked); });
        return { row, box, text };
    }

    #render(): void {
        const view = this.#draft.view;
        if (view.loading || view.updating) this.#list.setAttribute('aria-busy', 'true');
        else this.#list.removeAttribute('aria-busy');
        const parts: HTMLElement[] = [];
        if (view.unavailable !== undefined) {
            this.#unavailable.textContent = view.unavailable;
            parts.push(this.#unavailable);
        } else if (view.loading) {
            parts.push(this.#loading);
        } else {
            const document = this.element.ownerDocument;
            if (view.items.length > 0) {
                this.#selectAll.box.checked = view.allTicked === true;
                parts.push(this.#selectAll.row);
            }
            // An Item keeps its row while it is listed, so the one with the keyboard keeps it.
            const kept = new Map<string, DrawnItem>();
            view.items.forEach((item, at) => {
                const id = this.#draft.idAt(at);
                const { key } = item;
                const drawn = this.#drawn.get(id) ?? this.#item(document, 'ex-pivot-item', tick => { this.#draft.setTicked(key, tick); });
                drawn.box.checked = item.ticked;
                if (drawn.text.textContent !== item.label) drawn.text.textContent = item.label;
                kept.set(id, drawn);
                parts.push(drawn.row);
            });
            this.#drawn.clear();
            for (const [id, drawn] of kept) this.#drawn.set(id, drawn);
            if (view.note !== undefined) {
                this.#note.textContent = view.note;
                parts.push(this.#note);
            }
        }
        const children = this.#list.children;
        if (parts.length !== children.length || parts.some((part, i) => children[i] !== part)) this.#list.replaceChildren(...parts);

        if (view.refusal === undefined) this.#refusalBox.remove();
        else {
            this.#refusalBox.textContent = view.refusal;
            if (this.#refusalBox.previousSibling !== this.#list) this.#list.after(this.#refusalBox);
        }
        this.#ok.disabled = !view.canApply;
    }
}

// The Hidden Items of `field` in `layout`, which Filter… is only opened on while the field is placed.
function placementHidden(layout: PivotLayout, field: string): readonly PivotItemKey[] {
    const at = defined(placementOf(layout, field), `the placement of '${field}'`);
    return elementAt(placementsIn(layout, at.area), at.index).hiddenItems;
}
