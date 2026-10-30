// The built-in Field List (ADR-0061): Excel's "PivotTable Fields" pane in plain markup, under the classes and
// the Visual Tokens of ExPivot's stylesheet, which the Razor component draws too, so one stylesheet and one
// layer 3 judge both (ADR-0350 section 4). It draws the layout it is handed and says what a gesture means as
// the next layout, taken against the layout it was drawn from; whether that layout is still the pane's is
// the pivot's to judge (spine rule 6).
//
// Beside each field it writes the field's count of Items where the source counted them, and while a field is
// dragged it marks a target in Rows or Columns where the layout the drop would make passes the leaf cap
// (ADR-0370 section 3). The mark is a hint from the counts, never a refusal: the drop is asked as any other.

import {
    formatPivotCount, isValuesPseudoField, moveEntry, pivotLeafBound, placeField, placementOf, placementsIn, placesField, removeEntry,
    tickField, untickField, valuesPseudoField, type PivotArea, type PivotEntry, type PivotField, type PivotItemCounts, type PivotLayout,
} from '@ex-grid/pivot-engine';
import { element } from './dom';
import { defined, elementAt } from './must';
import type { PivotScreenWords } from './words';

/** The Areas in the order Excel's pane lays them out, two by two. */
export const areaOrder: readonly PivotArea[] = Object.freeze(['filters', 'columns', 'rows', 'values']);

/** What a gesture in the pane moved into an Area, for the words of a refusal (ADR-0370 section 3). */
export interface FieldListMove {
    /** The field's name. */
    readonly field: string;
    /** The Area it was put in. */
    readonly area: PivotArea;
}

/** A gesture in the pane: the layout it makes, the layout it was taken against, and what it moved. */
export interface FieldListEdit {
    readonly layout: PivotLayout;
    readonly against: PivotLayout;
    readonly moved: FieldListMove | undefined;
}

/** What the pane draws. */
export interface FieldListModel {
    /** The pane's layout: the one last asked for, or the one the report shows. */
    readonly layout: PivotLayout;
    /** The fields the source offers. */
    readonly fields: readonly PivotField[];
    /** Each Value Field's caption under `layout`. */
    readonly valueCaptions: readonly string[];
    /** Each field's count of Items, where the source counted them. */
    readonly counts: PivotItemCounts | undefined;
    /** The most leaves a question may ask for. */
    readonly maxLeaves: number;
    /** The locale counts are written in. */
    readonly locale: string;
    /** The words. */
    readonly words: PivotScreenWords;
    /** Whether Defer Layout Update is ticked: the pane's changes build a pending layout, which Update applies. */
    readonly deferred: boolean;
    /** Whether Update would apply anything: the pending layout is not the report's. */
    readonly canUpdate: boolean;
    /** The field menu or panel open in the pane: the entry it was opened on, and its frame, which the pane places
     * under that entry. */
    readonly popup: { readonly entry: PivotEntry; readonly frame: HTMLElement } | undefined;
}

/** Whom the pane calls back. */
export interface FieldListHandlers {
    /** A gesture made a layout. */
    readonly edit: (edit: FieldListEdit) => void;
    /** The heading's close button was pressed. */
    readonly close: () => void;
    /** A drag started or ended: the report accepts a placed field dropped on it, which removes it. */
    readonly dragging: (removable: boolean) => void;
    /** An entry's button was pressed, which opens its menu or closes it (ADR-0061). */
    readonly menu: (entry: PivotEntry) => void;
    /** Defer Layout Update was ticked or unticked. */
    readonly defer: (deferred: boolean) => void;
    /** Update was pressed. */
    readonly update: () => void;
}

/** What is dragged: a field from the list, or an entry of an Area. */
export type DragSubject = { readonly kind: 'field'; readonly field: string } | { readonly kind: 'entry'; readonly entry: PivotEntry };

/** A drag in the pane: what is dragged, and the layout it started on, which a drop is taken against. */
export interface Drag {
    readonly subject: DragSubject;
    readonly layout: PivotLayout;
}

/** Where a drop would land: an Area, and the index of the entry it goes before (the Area's count for its end). */
export interface DropAt {
    readonly area: PivotArea;
    readonly index: number;
}

// What a gesture in the pane means is decided here, once, for the built-in pane and a Chrome's alike (ADR-0390):
// the layout a tick, a drop or a drag back to the list makes, which Area takes a drop, and when a drop's layout
// passes the leaf cap.

/** The edit a field's tick box makes: unticked out of the Areas, ticked into Rows, or a number into Values, as
 * Excel's does. */
export function tickEdit(layout: PivotLayout, field: PivotField): FieldListEdit {
    if (placesField(layout, field.name)) return { layout: untickField(layout, field.name), against: layout, moved: undefined };
    const next = tickField(layout, field);
    const at = placementOf(next, field.name);
    const inValues = next.values.some(value => value.field === field.name);
    const area = at !== undefined ? at.area : inValues ? 'values' : undefined;
    return { layout: next, against: layout, moved: area === undefined ? undefined : { field: field.name, area } };
}

/** Whether `area` takes a drop of `drag`: Σ Values goes only to Rows or Columns. */
export function acceptsDrag(drag: Drag, area: PivotArea): boolean {
    return drag.subject.kind === 'field' || !isValuesPseudoField(drag.subject.entry) || area === 'rows' || area === 'columns';
}

/** Whether a drop of `drag` on the report or the list removes it: a placed field's entry. */
export function removesOnDrop(drag: Drag | undefined): boolean {
    return drag?.subject.kind === 'entry' && !isValuesPseudoField(drag.subject.entry);
}

/** The edit a drop of `drag` at `area` and `index` makes, and what it moved. */
export function dropEdit(drag: Drag, area: PivotArea, index: number, fields: readonly PivotField[]): FieldListEdit {
    const layout = drag.layout;
    if (drag.subject.kind === 'field') {
        const name = drag.subject.field;
        const field = defined(fields.find(f => f.name === name), `the field '${name}'`);
        return { layout: placeField(layout, field, area, index), against: layout, moved: { field: name, area } };
    }
    const { entry } = drag.subject;
    if (isValuesPseudoField(entry)) return { layout: moveEntry(layout, entry, area, index, undefined), against: layout, moved: undefined };
    const name = fieldOf(layout, entry);
    const field = defined(fields.find(f => f.name === name), `the field '${name}'`);
    return { layout: moveEntry(layout, entry, area, index, field), against: layout, moved: { field: name, area } };
}

/** The edit a drop of `drag` back on the list of fields makes: a placed field taken out; none for anything else. */
export function listDropEdit(drag: Drag | undefined): FieldListEdit | undefined {
    if (drag === undefined || !removesOnDrop(drag) || drag.subject.kind !== 'entry') return undefined;
    return { layout: removeEntry(drag.layout, drag.subject.entry), against: drag.layout, moved: undefined };
}

/** Whether a drop of `drag` at `at` makes a layout whose leaves the counts bound above the cap (ADR-0370 section 3):
 * a hint from the counts, never a refusal. */
export function dropOverCap(drag: Drag, at: DropAt, model: Pick<FieldListModel, 'counts' | 'maxLeaves' | 'fields'>): boolean {
    const { counts, maxLeaves } = model;
    if (counts === undefined || (at.area !== 'rows' && at.area !== 'columns') || !acceptsDrag(drag, at.area)) return false;
    const bound = pivotLeafBound(dropEdit(drag, at.area, at.index, model.fields).layout, counts);
    return bound !== undefined && bound > maxLeaves;
}

/** The data a drag carries for the browser: Firefox starts a drag only with data. The pivot reads what is dragged
 * from its own state, so a drop on another pivot carries nothing it takes (ADR-0061). */
export function startDragData(event: DragEvent, subject: DragSubject, layout: PivotLayout): void {
    if (event.dataTransfer === null) return;
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', subject.kind === 'field' ? subject.field : fieldOrValues(layout, subject.entry));
}

/** The index past an Area's last entry: Σ Values, which always stands last, is not counted. */
export function endOf(layout: PivotLayout, area: PivotArea): number {
    return area === 'values' ? layout.values.length : placementsIn(layout, area).length;
}

/** A field's item in the list, and the parts of it a redraw changes. */
interface FieldItem {
    readonly item: HTMLElement;
    readonly box: HTMLInputElement;
    readonly count: HTMLElement;
    readonly filtered: HTMLElement;
}

/** Text lowered for the search as the pivot's locale lowers it, never as the host's (ADR-0370 section 10). */
export function lowered(text: string, locale: string): string {
    return locale === 'invariant' ? text.toLowerCase() : text.toLocaleLowerCase(locale);
}

interface AreaElements {
    readonly area: HTMLElement;
    readonly entries: HTMLElement;
    readonly cap: HTMLElement;
}


/** The field a placed entry stands for. */
export function fieldOf(layout: PivotLayout, entry: PivotEntry): string {
    return entry.area === 'values' ? elementAt(layout.values, entry.index).field : elementAt(placementsIn(layout, entry.area), entry.index).field;
}

/** Whether two entries name the same place: an Area and an index there, or Σ Values on one axis. */
export function sameEntry(a: PivotEntry, b: PivotEntry): boolean {
    return a.area === b.area && a.index === b.index;
}

/** The Field List of one pivot, drawn into the element it is given. */
export class FieldListView {
    /** The pane, which stands in the element the view was given. */
    readonly element: HTMLElement;
    readonly #root: HTMLElement;
    readonly #handlers: FieldListHandlers;
    readonly #title: HTMLElement;
    readonly #close: HTMLButtonElement;
    readonly #chooseCaption: HTMLElement;
    readonly #search: HTMLInputElement;
    readonly #list: HTMLUListElement;
    readonly #dragCaption: HTMLElement;
    readonly #defer: HTMLInputElement;
    readonly #deferCaption: HTMLElement;
    readonly #update: HTMLButtonElement;
    readonly #areas = new Map<PivotArea, AreaElements>();
    readonly #areaBoxes = new Set<HTMLElement>();
    #model: FieldListModel | undefined;
    #searchText = '';
    #drag: Drag | undefined;
    #dropAt: DropAt | undefined;
    // The elements the drag paints, by what they stand for, so a drag repaints classes and never rebuilds the
    // element the browser is dragging.
    #fieldItems = new Map<string, FieldItem>();
    #fieldsDrawnFrom: readonly PivotField[] | undefined;
    #areasDrawnFrom: { readonly layout: PivotLayout; readonly words: PivotScreenWords; readonly captions: string; readonly fields: readonly PivotField[] } | undefined;
    #entryItems: { readonly entry: PivotEntry; readonly element: HTMLElement; readonly button: HTMLButtonElement; readonly index: number }[] = [];

    constructor(root: HTMLElement, handlers: FieldListHandlers) {
        this.#root = root;
        this.#handlers = handlers;
        const document = root.ownerDocument;
        const pane = element(document, 'div', 'ex-pivot-pane');
        const heading = element(document, 'div', 'ex-pivot-pane-heading');
        this.#title = element(document, 'div', 'ex-pivot-pane-title');
        this.#close = element(document, 'button', 'ex-pivot-pane-close');
        this.#close.type = 'button';
        const cross = element(document, 'span');
        cross.setAttribute('aria-hidden', 'true');
        cross.textContent = '×';
        this.#close.append(cross);
        this.#close.addEventListener('click', () => { this.#handlers.close(); });
        heading.append(this.#title, this.#close);
        const body = element(document, 'div', 'ex-pivot-pane-body');
        this.#chooseCaption = element(document, 'div', 'ex-pivot-pane-caption');
        this.#search = element(document, 'input', 'ex-pivot-search');
        this.#search.type = 'search';
        // The search only narrows the list: it claims nothing of the pivot's input (ADR-0240 section 6).
        this.#search.setAttribute('data-ex-claims', '');
        this.#search.addEventListener('input', () => {
            this.#searchText = this.#search.value;
            this.#drawFields();
        });
        this.#list = element(document, 'ul', 'ex-pivot-fields');
        this.#list.addEventListener('dragover', event => { this.#acceptDrop(event); });
        this.#list.addEventListener('drop', event => {
            event.preventDefault();
            this.#dropOnList();
        });
        this.#dragCaption = element(document, 'div', 'ex-pivot-pane-caption');
        const areas = element(document, 'div', 'ex-pivot-areas');
        for (const area of areaOrder) {
            const box = element(document, 'div', 'ex-pivot-area');
            box.setAttribute('data-ex-area', area);
            const title = element(document, 'div', 'ex-pivot-area-title');
            const cap = element(document, 'div', 'ex-pivot-area-cap');
            cap.setAttribute('role', 'status');
            const entries = element(document, 'div', 'ex-pivot-area-entries');
            entries.setAttribute('role', 'list');
            box.append(title, cap, entries);
            box.addEventListener('dragover', event => { this.#acceptDrop(event); });
            box.addEventListener('dragenter', () => { this.#dragOver(area, this.#endOf(area)); });
            box.addEventListener('drop', event => {
                event.preventDefault();
                this.#drop(area, this.#endOf(area));
            });
            this.#areas.set(area, { area: box, entries, cap });
            this.#areaBoxes.add(box);
            areas.append(box);
        }
        // Excel's Defer Layout Update at the pane's foot (ADR-0061): while it is ticked, the pane's changes build a
        // pending layout, and Update applies it in one change.
        const defer = element(document, 'div', 'ex-pivot-defer');
        const deferLabel = element(document, 'label', 'ex-pivot-defer-label');
        this.#defer = element(document, 'input');
        this.#defer.type = 'checkbox';
        this.#defer.setAttribute('data-ex-claims', 'click');
        this.#defer.addEventListener('change', () => { this.#handlers.defer(this.#defer.checked); });
        this.#deferCaption = element(document, 'span');
        deferLabel.append(this.#defer, this.#deferCaption);
        this.#update = element(document, 'button', 'ex-pivot-update');
        this.#update.type = 'button';
        this.#update.setAttribute('data-ex-claims', 'click');
        this.#update.addEventListener('click', () => { this.#handlers.update(); });
        defer.append(deferLabel, this.#update);
        body.append(this.#chooseCaption, this.#search, this.#list, this.#dragCaption, areas, defer);
        pane.append(heading, body);
        root.append(pane);
        this.element = pane;
    }

    /** Draws `model`. A drag in progress is dropped when the layout it started on is no longer the pane's. */
    draw(model: FieldListModel): void {
        this.#model = model;
        if (this.#drag !== undefined && this.#drag.layout !== model.layout) this.#endDrag();
        const { words } = model;
        const title = words.word('field-list');
        this.#title.textContent = title;
        this.#title.title = title;
        const hide = words.word('hide-field-list');
        this.#close.setAttribute('aria-label', hide);
        this.#close.title = hide;
        this.#chooseCaption.textContent = words.word('choose-fields');
        const search = words.word('search-fields');
        this.#search.placeholder = search;
        this.#search.setAttribute('aria-label', search);
        this.#list.setAttribute('aria-label', title);
        this.#dragCaption.textContent = words.word('drag-fields');
        this.#deferCaption.textContent = words.word('defer-layout-update');
        this.#defer.checked = model.deferred;
        this.#update.textContent = words.word('update');
        this.#update.disabled = !model.canUpdate;
        this.#drawFields();
        this.#drawAreas();
        this.#placeMenu();
        this.#paintDrag();
    }

    /** Gives the keyboard to the button of the entry drawn at `entry`. */
    focusEntry(entry: PivotEntry): void {
        const drawn = this.#entryItems.find(item => sameEntry(item.entry, entry));
        if (drawn === undefined) throw new Error(`Internal: no entry is drawn at ${entry.area} ${entry.index}.`);
        drawn.button.focus();
    }

    /** Gives the keyboard to the tick box of the field named `name`; to the search when the search hides that field,
     * or when no field is named. */
    focusField(name: string | undefined): void {
        const drawn = name === undefined ? undefined : this.#fieldItems.get(name);
        if (drawn?.item.isConnected === true) drawn.box.focus();
        else this.#search.focus();
    }

    /** Whether a placed field is being dragged, which a drop on the report removes. */
    get removable(): boolean {
        return removesOnDrop(this.#drag);
    }

    /** A drag entered `target`: unless it is in an Area, which hears its own entry first, no Area is the target. */
    dragEntered(target: EventTarget | null): void {
        // A drag over text enters the text itself in some browsers: its element is what it is in.
        const entered = target instanceof Element ? target : target instanceof Node ? target.parentElement : null;
        const box = entered === null ? null : entered.closest('.ex-pivot-area');
        if (box instanceof HTMLElement && this.#areaBoxes.has(box)) return;
        this.leaveTargets();
    }

    /** The drag is over no Area of the pane now: none is marked as its target. */
    leaveTargets(): void {
        if (this.#dropAt === undefined) return;
        this.#dropAt = undefined;
        this.#paintDrag();
    }

    /** A drop on the report: a placed field dragged there is removed, as Excel removes it (ADR-0061). */
    dropOnReport(): void {
        this.#dropOnList();
    }

    get #current(): FieldListModel {
        return defined(this.#model, 'the Field List\'s model');
    }

    #drawFields(): void {
        const model = this.#current;
        const search = lowered(this.#searchText, model.locale);
        if (this.#fieldsDrawnFrom !== model.fields) {
            this.#fieldItems = new Map();
            this.#fieldsDrawnFrom = model.fields;
        }
        const items = model.fields
            .filter(field => search === '' || lowered(field.caption, model.locale).includes(search))
            .map(field => {
                // Each field keeps its elements while the source's fields are the same, and a redraw changes what
                // they say in place: the item the browser is dragging stays in the page, and the tick box the
                // user just pressed keeps the keyboard.
                let drawn = this.#fieldItems.get(field.name);
                if (drawn === undefined) {
                    drawn = this.#fieldItem(field);
                    this.#fieldItems.set(field.name, drawn);
                }
                const { item, box, count, filtered } = drawn;
                box.checked = placesField(model.layout, field.name);
                const counted = model.counts?.countOf(field.name);
                if (counted === undefined) {
                    count.remove();
                } else {
                    const written = formatPivotCount(counted, model.locale);
                    count.textContent = written;
                    const said = model.words.fill('field-item-count', written);
                    count.title = said;
                    count.setAttribute('aria-label', said);
                    if (count.parentNode !== item) item.insertBefore(count, filtered.parentNode === item ? filtered : null);
                }
                const at = placementOf(model.layout, field.name);
                if (at !== undefined && elementAt(placementsIn(model.layout, at.area), at.index).hiddenItems.length > 0) {
                    const word = model.words.word('filtered');
                    filtered.setAttribute('aria-label', word);
                    filtered.title = word;
                    if (filtered.parentNode !== item) item.append(filtered);
                } else {
                    filtered.remove();
                }
                return item;
            });
        if (items.length !== this.#list.children.length || items.some((item, i) => this.#list.children[i] !== item)) {
            this.#list.replaceChildren(...items);
        }
    }

    #fieldItem(field: PivotField): FieldItem {
        const document = this.#root.ownerDocument;
        const item = element(document, 'li', 'ex-pivot-field');
        item.draggable = true;
        item.setAttribute('data-ex-field', field.name);
        const label = element(document, 'label', 'ex-pivot-field-label');
        const box = element(document, 'input');
        box.type = 'checkbox';
        box.setAttribute('data-ex-claims', 'click');
        box.addEventListener('change', () => { this.#tick(field); });
        const caption = element(document, 'span', 'ex-pivot-field-caption');
        caption.textContent = field.caption;
        label.append(box, caption);
        item.append(label);
        const filtered = element(document, 'span', 'ex-pivot-filtered-mark');
        filtered.setAttribute('role', 'img');
        item.addEventListener('dragstart', event => { this.#startDrag(event, { kind: 'field', field: field.name }); });
        item.addEventListener('dragend', () => { this.#endDrag(); });
        return { item, box, count: element(document, 'span', 'ex-pivot-field-count'), filtered };
    }

    #drawAreas(): void {
        const model = this.#current;
        const { layout, words } = model;
        // The entries are what the layout, the words and the captions make; drawn again only when one of them
        // changed, so a redraw while an entry is dragged keeps the element the browser is dragging.
        const drawnFrom = { layout, words, captions: model.valueCaptions.join('\n'), fields: model.fields };
        const last = this.#areasDrawnFrom;
        if (last !== undefined && last.layout === layout && last.words === words && last.captions === drawnFrom.captions && last.fields === model.fields) return;
        this.#areasDrawnFrom = drawnFrom;
        const document = this.#root.ownerDocument;
        this.#entryItems = [];
        for (const area of areaOrder) {
            const { area: box, entries } = defined(this.#areas.get(area), 'an Area\'s box');
            const title = words.area(area);
            elementAt([...box.children], 0).textContent = title;
            entries.setAttribute('aria-label', title);
            const drawn: HTMLElement[] = [];
            const end = this.#endOf(area);
            const add = (entry: PivotEntry, caption: string, filtered: boolean): void => {
                const index = isValuesPseudoField(entry) ? end : entry.index;
                const item = element(document, 'div', isValuesPseudoField(entry) ? 'ex-pivot-entry ex-pivot-entry-values' : 'ex-pivot-entry');
                item.setAttribute('role', 'listitem');
                item.draggable = true;
                const button = element(document, 'button', 'ex-pivot-entry-button');
                button.type = 'button';
                button.setAttribute('data-ex-claims', 'click');
                button.setAttribute('aria-haspopup', 'menu');
                button.setAttribute('aria-label', words.fill('field-menu', caption));
                button.addEventListener('click', () => { this.#handlers.menu(entry); });
                const text = element(document, 'span', 'ex-pivot-entry-caption');
                text.textContent = caption;
                button.append(text);
                if (filtered) {
                    const mark = element(document, 'span', 'ex-pivot-filtered-mark');
                    mark.setAttribute('aria-hidden', 'true');
                    button.append(mark);
                }
                const arrow = element(document, 'span', 'ex-pivot-entry-arrow');
                arrow.setAttribute('aria-hidden', 'true');
                arrow.textContent = '▾';
                button.append(arrow);
                item.append(button);
                item.addEventListener('dragstart', event => { this.#startDrag(event, { kind: 'entry', entry }); });
                item.addEventListener('dragend', () => { this.#endDrag(); });
                item.addEventListener('dragenter', event => {
                    event.stopPropagation();
                    this.#dragOver(area, index);
                });
                item.addEventListener('dragover', event => { this.#acceptDrop(event); });
                item.addEventListener('drop', event => {
                    event.preventDefault();
                    event.stopPropagation();
                    this.#drop(area, index);
                });
                this.#entryItems.push({ entry, element: item, button, index });
                drawn.push(item);
            };
            if (area === 'values') {
                layout.values.forEach((_, i) => { add({ area, index: i }, elementAt(model.valueCaptions, i), false); });
            } else {
                placementsIn(layout, area).forEach((placement, i) => {
                    const field = model.fields.find(f => f.name === placement.field);
                    add({ area, index: i }, defined(field, `the field '${placement.field}'`).caption, placement.hiddenItems.length > 0);
                });
                if (layout.values.length >= 2 && area === (layout.valuesAxis === 'rows' ? 'rows' : 'columns')) {
                    add(valuesPseudoField(layout.valuesAxis), words.word('values-pseudo-field'), false);
                }
            }
            entries.replaceChildren(...drawn);
        }
    }

    // The open field menu stands under its entry, in the flow, as wide as the pane (ADR-0061); every other entry's
    // button says its menu is closed.
    #placeMenu(): void {
        const { popup: menu } = this.#current;
        for (const { entry, element: item, button } of this.#entryItems) {
            const open = menu !== undefined && sameEntry(menu.entry, entry);
            button.setAttribute('aria-expanded', open ? 'true' : 'false');
            // While a menu or a panel stands under the entry, a press that moves a little on it is a command's, or
            // selects text in a field, never a drag of the entry.
            item.draggable = !open;
            if (open && menu.frame.parentNode !== item) {
                // The entries were drawn again under it: the command that held the keyboard keeps it.
                const held = item.ownerDocument.activeElement;
                item.append(menu.frame);
                if (held instanceof HTMLElement && menu.frame.contains(held) && held !== item.ownerDocument.activeElement) held.focus();
            }
        }
    }

    // The index past an Area's last entry: Σ Values, which always stands last, is not counted.
    #endOf(area: PivotArea): number {
        return endOf(this.#current.layout, area);
    }

    #tick(field: PivotField): void {
        this.#handlers.edit(tickEdit(this.#current.layout, field));
    }

    #startDrag(event: DragEvent, subject: DragSubject): void {
        const layout = this.#current.layout;
        this.#drag = { subject, layout };
        this.#dropAt = undefined;
        startDragData(event, subject, layout);
        this.#paintDrag();
        this.#handlers.dragging(this.removable);
    }

    #endDrag(): void {
        if (this.#drag === undefined && this.#dropAt === undefined) return;
        this.#drag = undefined;
        this.#dropAt = undefined;
        this.#paintDrag();
        this.#handlers.dragging(false);
    }

    #accepts(area: PivotArea): boolean {
        return this.#drag !== undefined && acceptsDrag(this.#drag, area);
    }

    #acceptDrop(event: DragEvent): void {
        if (this.#drag === undefined) return;
        event.preventDefault();
        if (event.dataTransfer !== null) event.dataTransfer.dropEffect = 'move';
    }

    #dragOver(area: PivotArea, index: number): void {
        if (this.#drag === undefined) return;
        if (this.#dropAt?.area === area && this.#dropAt.index === index) return;
        this.#dropAt = { area, index };
        this.#paintDrag();
    }

    #drop(area: PivotArea, index: number): void {
        const drag = this.#drag;
        this.#endDrag();
        if (drag === undefined || !acceptsDrag(drag, area)) return;
        this.#handlers.edit(dropEdit(drag, area, index, this.#current.fields));
    }

    #dropOnList(): void {
        const drag = this.#drag;
        this.#endDrag();
        const edit = listDropEdit(drag);
        if (edit !== undefined) this.#handlers.edit(edit);
    }

    // The drag's marks, as classes on the elements drawn: never a rebuild, which would take away the element
    // the browser is dragging.
    #paintDrag(): void {
        const drag = this.#drag;
        const at = this.#dropAt;
        const model = this.#model;
        for (const [name, { item }] of this.#fieldItems) {
            item.classList.toggle('ex-pivot-dragging', drag?.subject.kind === 'field' && drag.subject.field === name);
        }
        for (const { entry, element: item, index } of this.#entryItems) {
            const dragged = drag?.subject.kind === 'entry' && sameEntry(drag.subject.entry, entry);
            item.classList.toggle('ex-pivot-dragging', dragged);
            item.classList.toggle('ex-pivot-drop-before', at !== undefined && this.#accepts(at.area) && at.area === entry.area && at.index === index);
        }
        const over = drag !== undefined && at !== undefined && model !== undefined && dropOverCap(drag, at, model);
        for (const [area, { area: box, cap }] of this.#areas) {
            const target = at !== undefined && at.area === area && this.#accepts(area);
            box.classList.toggle('ex-pivot-drop-target', target);
            box.classList.toggle('ex-pivot-drop-over-cap', target && over);
            const said = target && over ? model.words.fill('drop-over-cap', formatPivotCount(model.maxLeaves, model.locale)) : '';
            if (cap.textContent !== said) cap.textContent = said;
        }
        this.#list.classList.toggle('ex-pivot-drop-remove', this.removable);
    }
}

function fieldOrValues(layout: PivotLayout, entry: PivotEntry): string {
    return isValuesPseudoField(entry) ? 'values' : fieldOf(layout, entry);
}
