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
}

/** Whom the pane calls back. */
export interface FieldListHandlers {
    /** A gesture made a layout. */
    readonly edit: (edit: FieldListEdit) => void;
    /** The heading's close button was pressed. */
    readonly close: () => void;
    /** A drag started or ended: the report accepts a placed field dropped on it, which removes it. */
    readonly dragging: (removable: boolean) => void;
}

/** What is dragged: a field from the list, or an entry of an Area. */
type DragSubject = { readonly kind: 'field'; readonly field: string } | { readonly kind: 'entry'; readonly entry: PivotEntry };

interface Drag {
    readonly subject: DragSubject;
    /** The layout the drag started on: a drop is taken against it. */
    readonly layout: PivotLayout;
}

interface DropAt {
    readonly area: PivotArea;
    readonly index: number;
}

/** A field's item in the list, and the parts of it a redraw changes. */
interface FieldItem {
    readonly item: HTMLElement;
    readonly box: HTMLInputElement;
    readonly count: HTMLElement;
    readonly filtered: HTMLElement;
}

/** Text lowered for the search as the pivot's locale lowers it, never as the host's (ADR-0370 section 10). */
function lowered(text: string, locale: string): string {
    return locale === 'invariant' ? text.toLowerCase() : text.toLocaleLowerCase(locale);
}

interface AreaElements {
    readonly area: HTMLElement;
    readonly entries: HTMLElement;
    readonly cap: HTMLElement;
}

function element<K extends keyof HTMLElementTagNameMap>(document: Document, tag: K, className?: string): HTMLElementTagNameMap[K] {
    const made = document.createElement(tag);
    if (className !== undefined) made.className = className;
    return made;
}

function fieldOf(layout: PivotLayout, entry: PivotEntry): string {
    return entry.area === 'values' ? elementAt(layout.values, entry.index).field : elementAt(placementsIn(layout, entry.area), entry.index).field;
}

function sameEntry(a: PivotEntry, b: PivotEntry): boolean {
    return a.area === b.area && a.index === b.index;
}

/** The Field List of one pivot, drawn into the element it is given. */
export class FieldListView {
    readonly #root: HTMLElement;
    readonly #handlers: FieldListHandlers;
    readonly #title: HTMLElement;
    readonly #close: HTMLButtonElement;
    readonly #chooseCaption: HTMLElement;
    readonly #search: HTMLInputElement;
    readonly #list: HTMLUListElement;
    readonly #dragCaption: HTMLElement;
    readonly #areas = new Map<PivotArea, AreaElements>();
    #model: FieldListModel | undefined;
    #searchText = '';
    #drag: Drag | undefined;
    #dropAt: DropAt | undefined;
    // The elements the drag paints, by what they stand for, so a drag repaints classes and never rebuilds the
    // element the browser is dragging.
    #fieldItems = new Map<string, FieldItem>();
    #fieldsDrawnFrom: readonly PivotField[] | undefined;
    #areasDrawnFrom: { readonly layout: PivotLayout; readonly words: PivotScreenWords; readonly captions: string; readonly fields: readonly PivotField[] } | undefined;
    #entryItems: { readonly entry: PivotEntry; readonly element: HTMLElement; readonly index: number }[] = [];

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
            areas.append(box);
        }
        body.append(this.#chooseCaption, this.#search, this.#list, this.#dragCaption, areas);
        pane.append(heading, body);
        root.append(pane);
        // A drag that moves on from an Area, to the list or out of the pane, leaves that Area unmarked: an Area
        // hears its own entry first, so what reaches the pane from outside every Area is somewhere else.
        root.addEventListener('dragenter', event => {
            const target = event.target;
            if (!(target instanceof Node) || ![...this.#areas.values()].some(({ area }) => area.contains(target))) this.leaveTargets();
        });
        root.addEventListener('dragleave', event => {
            const to = event.relatedTarget;
            if (!(to instanceof Node) || !root.contains(to)) this.leaveTargets();
        });
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
        this.#drawFields();
        this.#drawAreas();
        this.#paintDrag();
    }

    /** Whether a placed field is being dragged, which a drop on the report removes. */
    get removable(): boolean {
        return this.#drag?.subject.kind === 'entry' && !isValuesPseudoField(this.#drag.subject.entry);
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
                    if (!count.isConnected) item.insertBefore(count, filtered.isConnected ? filtered : null);
                }
                const at = placementOf(model.layout, field.name);
                if (at !== undefined && elementAt(placementsIn(model.layout, at.area), at.index).hiddenItems.length > 0) {
                    const word = model.words.word('filtered');
                    filtered.setAttribute('aria-label', word);
                    filtered.title = word;
                    item.append(filtered);
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
                const text = element(document, 'span', 'ex-pivot-entry-caption');
                text.textContent = caption;
                button.append(text);
                if (filtered) {
                    const mark = element(document, 'span', 'ex-pivot-filtered-mark');
                    mark.setAttribute('aria-hidden', 'true');
                    button.append(mark);
                }
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
                this.#entryItems.push({ entry, element: item, index });
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

    // The index past an Area's last entry: Σ Values, which always stands last, is not counted.
    #endOf(area: PivotArea): number {
        const layout = this.#current.layout;
        return area === 'values' ? layout.values.length : placementsIn(layout, area).length;
    }

    #tick(field: PivotField): void {
        const layout = this.#current.layout;
        if (placesField(layout, field.name)) {
            this.#handlers.edit({ layout: untickField(layout, field.name), against: layout, moved: undefined });
            return;
        }
        const next = tickField(layout, field);
        const at = placementOf(next, field.name);
        this.#handlers.edit({ layout: next, against: layout, moved: at === undefined ? undefined : { field: field.name, area: at.area } });
    }

    #startDrag(event: DragEvent, subject: DragSubject): void {
        const layout = this.#current.layout;
        this.#drag = { subject, layout };
        this.#dropAt = undefined;
        // Firefox starts a drag only with data; the pivot reads what is dragged from its own state, so a drop on
        // another pivot carries nothing it takes (ADR-0061).
        if (event.dataTransfer !== null) {
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('text/plain', subject.kind === 'field' ? subject.field : fieldOrValues(layout, subject.entry));
        }
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
        return this.#drag !== undefined && this.#acceptsDrag(this.#drag, area);
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

    // The layout a drop of the drag at `area` and `index` makes, and what it moved.
    #dropped(drag: Drag, area: PivotArea, index: number): FieldListEdit {
        const { fields } = this.#current;
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

    #drop(area: PivotArea, index: number): void {
        const drag = this.#drag;
        this.#endDrag();
        if (drag === undefined || !this.#acceptsDrag(drag, area)) return;
        this.#handlers.edit(this.#dropped(drag, area, index));
    }

    #acceptsDrag(drag: Drag, area: PivotArea): boolean {
        return drag.subject.kind === 'field' || !isValuesPseudoField(drag.subject.entry) || area === 'rows' || area === 'columns';
    }

    #dropOnList(): void {
        const drag = this.#drag;
        this.#endDrag();
        if (drag?.subject.kind !== 'entry' || isValuesPseudoField(drag.subject.entry)) return;
        this.#handlers.edit({ layout: removeEntry(drag.layout, drag.subject.entry), against: drag.layout, moved: undefined });
    }

    // Whether a drop at `at` makes a layout whose leaves the counts bound above the cap (ADR-0370 section 3).
    #overCap(drag: Drag, at: DropAt): boolean {
        const { counts, maxLeaves } = this.#current;
        if (counts === undefined || (at.area !== 'rows' && at.area !== 'columns')) return false;
        const bound = pivotLeafBound(this.#dropped(drag, at.area, at.index).layout, counts);
        return bound !== undefined && bound > maxLeaves;
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
        const over = drag !== undefined && at !== undefined && this.#accepts(at.area) && this.#overCap(drag, at);
        for (const [area, { area: box, cap }] of this.#areas) {
            const target = at !== undefined && at.area === area && this.#accepts(area);
            box.classList.toggle('ex-pivot-drop-target', target);
            box.classList.toggle('ex-pivot-drop-over-cap', target && over);
            const said = target && over && model !== undefined ? model.words.fill('drop-over-cap', formatPivotCount(model.maxLeaves, model.locale)) : '';
            if (cap.textContent !== said) cap.textContent = said;
        }
        this.#list.classList.toggle('ex-pivot-drop-remove', this.removable);
    }
}

function fieldOrValues(layout: PivotLayout, entry: PivotEntry): string {
    return isValuesPseudoField(entry) ? 'values' : fieldOf(layout, entry);
}
