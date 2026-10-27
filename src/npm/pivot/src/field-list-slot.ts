// The Field List a Chrome's slot draws (ADR-0061, ADR-0390): Excel's PivotTable Fields pane, drawn from the view the
// pivot makes of the layout it is handed. What each gesture means is the built-in pane's rules (`field-list.ts`),
// so the layout a tick, a drop or a drag back makes is the same whichever draws the pane; whether that layout is
// still the pane's is the pivot's to judge (spine rule 6). The contents hand in their controls, the anchors under
// the entries and the Areas' boxes; the pivot places an entry's menu or panel in its anchor and gives the keyboard to
// the control it means.

import { formatPivotCount, isValuesPseudoField, placementOf, placementsIn, placesField, valuesPseudoField, type PivotArea, type PivotEntry } from '@ex-grid/pivot-engine';
import type {
    PivotChromeSlot, PivotDragSubject, PivotFieldListAreaView, PivotFieldListEntryView, PivotFieldListFieldView, PivotFieldListSlotContext, PivotFieldListView,
    PivotPartRef, PivotStandingSlotContext,
} from './chrome';
import { StandingSlot } from './chrome';
import {
    acceptsDrag, areaOrder, dropEdit, dropOverCap, endOf, listDropEdit, lowered, removesOnDrop, sameEntry, startDragData, tickEdit, type Drag, type DropAt,
    type FieldListHandlers, type FieldListModel,
} from './field-list';
import { defined, elementAt } from './must';
import { cachedRef, elementRef, type PartElements } from './parts';

/** The name an entry's button and the anchor around it are handed in by. */
export function entryKey(entry: PivotEntry): string {
    return `entry:${entry.area}:${entry.index}`;
}

/** The name a field's tick box is handed in by. */
export function fieldKey(field: string): string {
    return `field:${field}`;
}

const emptyView: PivotFieldListView = Object.freeze({
    title: '', hideLabel: '', chooseCaption: '', searchLabel: '', search: '', fields: Object.freeze([]), dragCaption: '', areas: Object.freeze([]),
    dropRemoves: false, defer: Object.freeze({ label: '', ticked: false }), update: Object.freeze({ label: '', enabled: false }),
});

/** The Field List of one pivot, drawn by a Chrome's slot. */
export class SlottedFieldList {
    readonly #slot: StandingSlot<PivotFieldListView, PivotFieldListSlotContext>;
    readonly #handlers: FieldListHandlers;
    readonly #parts: PartElements;
    readonly #areaBoxes = new Map<PivotArea, HTMLElement>();
    #model: FieldListModel | undefined;
    #searchText = '';
    #drag: Drag | undefined;
    #dropAt: DropAt | undefined;

    constructor(document: Document, handlers: FieldListHandlers, parts: PartElements) {
        this.#slot = new StandingSlot('fieldList', document, emptyView);
        this.#handlers = handlers;
        this.#parts = parts;
    }

    /** The slot, which the pivot stands in the pane's place. */
    get element(): HTMLElement {
        return this.#slot.element;
    }

    /** Shows the pane by `mount`; answers as the slot does. */
    show(mount: PivotChromeSlot<PivotFieldListSlotContext>, label: (id: string) => string): { readonly shown: boolean; readonly error?: Error } {
        // A drag the contents before these started reaches nothing once they are gone: no dragend is heard from them.
        if (!this.#slot.stands(mount)) {
            this.#drag = undefined;
            this.#dropAt = undefined;
        }
        return this.#slot.show(mount, (shared, input) => this.#context(shared, input), label);
    }

    // The context of one mount of the contents, whose controls, anchors and Areas' boxes are handed in by refs of
    // its own.
    #context(shared: PivotStandingSlotContext<PivotFieldListView>, input: <Args extends unknown[]>(change: (...args: Args) => void) => (...args: Args) => void): PivotFieldListSlotContext {
        const refs = this.#parts.refs();
        const areas = new Map<PivotArea, PivotPartRef>();
        return {
            ...shared,
            setSearch: input((text: string) => {
                if (typeof text !== 'string') throw new TypeError('An ExPivot\'s Field List takes the search as typed, a string (ADR-0390).');
                this.#searchText = text;
                this.#tell();
            }),
            toggle: input((field: string) => {
                const model = this.#current;
                const declared = model.fields.find(f => f.name === field);
                if (declared === undefined) throw new RangeError(`The Field List has no field '${field}' to tick (ADR-0390).`);
                this.#handlers.edit(tickEdit(model.layout, declared));
            }),
            openMenu: input((entry: PivotEntry) => { if (this.#stands(entry)) this.#handlers.menu(entry); }),
            close: input(() => { this.#handlers.close(); }),
            startDrag: input((event: DragEvent, subject: PivotDragSubject) => { this.#startDrag(event, subject); }),
            dragOver: input((event: DragEvent) => {
                if (this.#drag === undefined) return;
                event.preventDefault();
                if (event.dataTransfer !== null) event.dataTransfer.dropEffect = 'move';
            }),
            dragEnter: input((area: PivotArea, index: number) => {
                if (this.#drag === undefined || (this.#dropAt?.area === area && this.#dropAt.index === index)) return;
                this.#dropAt = { area: checkedArea(area), index: checkedIndex(index) };
                this.#tell();
            }),
            drop: input((event: DragEvent, area: PivotArea, index: number) => {
                event.preventDefault();
                const drag = this.#drag;
                this.#endDrag();
                if (drag === undefined || !acceptsDrag(drag, checkedArea(area))) return;
                this.#handlers.edit(dropEdit(drag, area, checkedIndex(index), this.#current.fields));
            }),
            dropOnList: input((event: DragEvent) => {
                event.preventDefault();
                this.dropOnReport();
            }),
            endDrag: input(() => { this.#endDrag(); }),
            setDeferred: input((deferred: boolean) => {
                if (typeof deferred !== 'boolean') throw new TypeError('Defer Layout Update is handed whether it is ticked (ADR-0390).');
                this.#handlers.defer(deferred);
            }),
            update: input(() => { if (this.#slot.view.update.enabled) this.#handlers.update(); }),
            control: (key: string) => refs.control(checkedKey(key)),
            anchor: (key: string) => refs.anchor(checkedKey(key)),
            area: (area: PivotArea) => cachedRef(areas, checkedArea(area), () => elementRef(this.#areaBoxes, area, `An ExPivot's Field List's box of '${area}'`)),
        };
    }

    /** The pane is the built-in one's, or gone: the contents are taken away, and a drag they started with them. */
    hide(): void {
        this.#slot.hide();
        this.#drag = undefined;
        this.#dropAt = undefined;
    }

    /** Draws `model`. A drag in progress is dropped when the layout it started on is no longer the pane's. */
    draw(model: FieldListModel): void {
        this.#model = model;
        if (this.#drag !== undefined && this.#drag.layout !== model.layout) this.#endDrag();
        if (model.popup !== undefined) this.#parts.place(entryKey(model.popup.entry), model.popup.frame);
        this.#tell();
    }

    /** Gives the keyboard to the button of the entry drawn at `entry`. */
    focusEntry(entry: PivotEntry): void {
        this.#parts.focus(entryKey(entry));
    }

    /** Gives the keyboard to the tick box of the field named `name`; to the search when the search hides that field,
     * or when no field is named. */
    focusField(name: string | undefined): void {
        const shown = name !== undefined && this.#slot.view.fields.some(field => field.name === name);
        this.#parts.focus(shown ? fieldKey(name) : 'search');
    }

    /** Whether a placed field is being dragged, which a drop on the report removes. */
    get removable(): boolean {
        return removesOnDrop(this.#drag);
    }

    /** A drag entered `target`: unless it is in an Area, which hears its own entry first, no Area is the target. */
    dragEntered(target: EventTarget | null): void {
        const entered = target instanceof Element ? target : target instanceof Node ? target.parentElement : null;
        for (const box of this.#areaBoxes.values()) {
            if (entered !== null && box.contains(entered)) return;
        }
        this.leaveTargets();
    }

    /** The drag is over no Area of the pane now: none is marked as its target. */
    leaveTargets(): void {
        if (this.#dropAt === undefined) return;
        this.#dropAt = undefined;
        this.#tell();
    }

    /** A drop on the report, or on the list of fields: a placed field dragged there is removed, as Excel removes it. */
    dropOnReport(): void {
        const drag = this.#drag;
        this.#endDrag();
        const edit = listDropEdit(drag);
        if (edit !== undefined) this.#handlers.edit(edit);
    }

    get #current(): FieldListModel {
        return defined(this.#model, 'the Field List\'s model');
    }

    // Whether `entry` stands in the pane's layout now: a press on an entry the pane has since left asks nothing.
    #stands(entry: PivotEntry): boolean {
        const layout = this.#current.layout;
        if (isValuesPseudoField(entry)) return layout.values.length >= 2 && sameEntry(entry, valuesPseudoField(layout.valuesAxis));
        return entry.index >= 0 && entry.index < endOf(layout, entry.area);
    }

    #startDrag(event: DragEvent, subject: PivotDragSubject): void {
        const layout = this.#current.layout;
        const drag: Drag = 'field' in subject
            ? { subject: { kind: 'field', field: subject.field }, layout }
            : { subject: { kind: 'entry', entry: subject.entry }, layout };
        if (drag.subject.kind === 'entry' && !this.#stands(drag.subject.entry)) return;
        this.#drag = drag;
        this.#dropAt = undefined;
        startDragData(event, drag.subject, layout);
        this.#tell();
        this.#handlers.dragging(this.removable);
    }

    #endDrag(): void {
        if (this.#drag === undefined && this.#dropAt === undefined) return;
        this.#drag = undefined;
        this.#dropAt = undefined;
        this.#tell();
        this.#handlers.dragging(false);
    }

    #tell(): void {
        if (this.#model !== undefined) this.#slot.viewOf(this.#view(this.#model));
    }

    #view(model: FieldListModel): PivotFieldListView {
        const { layout, words, locale } = model;
        const drag = this.#drag;
        const at = this.#dropAt;
        const search = lowered(this.#searchText, locale);
        const fields = model.fields
            .filter(field => search === '' || lowered(field.caption, locale).includes(search))
            .map((field): PivotFieldListFieldView => {
                const counted = model.counts?.countOf(field.name);
                const written = counted === undefined ? undefined : formatPivotCount(counted, locale);
                const placed = placementOf(layout, field.name);
                return Object.freeze({
                    name: field.name,
                    caption: field.caption,
                    key: fieldKey(field.name),
                    placed: placesField(layout, field.name),
                    filtered: placed !== undefined && elementAt(placementsIn(layout, placed.area), placed.index).hiddenItems.length > 0,
                    ...(written === undefined ? {} : { count: written, countLabel: words.fill('field-item-count', written) }),
                    dragging: drag?.subject.kind === 'field' && drag.subject.field === field.name,
                });
            });
        const over = drag !== undefined && at !== undefined && dropOverCap(drag, at, model);
        const areas = areaOrder.map((area): PivotFieldListAreaView => {
            const target = drag !== undefined && at !== undefined && at.area === area && acceptsDrag(drag, area);
            return Object.freeze({
                area,
                title: words.area(area),
                entries: Object.freeze(this.#entries(model, area)),
                end: endOf(layout, area),
                dropTarget: target,
                ...(target && over ? { overCap: words.fill('drop-over-cap', formatPivotCount(model.maxLeaves, locale)) } : {}),
            });
        });
        return Object.freeze({
            title: words.word('field-list'),
            hideLabel: words.word('hide-field-list'),
            chooseCaption: words.word('choose-fields'),
            searchLabel: words.word('search-fields'),
            search: this.#searchText,
            fields: Object.freeze(fields),
            dragCaption: words.word('drag-fields'),
            areas: Object.freeze(areas),
            dropRemoves: this.removable,
            defer: Object.freeze({ label: words.word('defer-layout-update'), ticked: model.deferred }),
            update: Object.freeze({ label: words.word('update'), enabled: model.canUpdate }),
        });
    }

    #entries(model: FieldListModel, area: PivotArea): PivotFieldListEntryView[] {
        const { layout, words, popup } = model;
        const drag = this.#drag;
        const at = this.#dropAt;
        const end = endOf(layout, area);
        const entry = (placed: PivotEntry, caption: string, filtered: boolean): PivotFieldListEntryView => {
            const index = isValuesPseudoField(placed) ? end : placed.index;
            return Object.freeze({
                entry: placed,
                key: entryKey(placed),
                caption,
                menuLabel: words.fill('field-menu', caption),
                filtered,
                values: isValuesPseudoField(placed),
                menuOpen: popup !== undefined && sameEntry(popup.entry, placed),
                dragging: drag?.subject.kind === 'entry' && sameEntry(drag.subject.entry, placed),
                dropBefore: drag !== undefined && at !== undefined && acceptsDrag(drag, at.area) && at.area === area && at.index === index,
                dropIndex: index,
            });
        };
        if (area === 'values') return layout.values.map((_, i) => entry({ area, index: i }, elementAt(model.valueCaptions, i), false));
        const entries = placementsIn(layout, area).map((placement, i) => {
            const field = defined(model.fields.find(f => f.name === placement.field), `the field '${placement.field}'`);
            return entry({ area, index: i }, field.caption, placement.hiddenItems.length > 0);
        });
        if (layout.values.length >= 2 && area === (layout.valuesAxis === 'rows' ? 'rows' : 'columns')) {
            entries.push(entry(valuesPseudoField(layout.valuesAxis), words.word('values-pseudo-field'), false));
        }
        return entries;
    }
}

// An Area, as a script may hand it.
function checkedArea(area: PivotArea): PivotArea {
    if (!areaOrder.includes(area)) throw new RangeError(`An ExPivot's Field List has the Areas ${areaOrder.join(', ')}, not '${area}' (ADR-0390).`);
    return area;
}

// An index a drop lands before, as a script may hand it: a whole number, zero or more.
function checkedIndex(index: number): number {
    if (!Number.isInteger(index) || index < 0) throw new RangeError(`A drop in an ExPivot's Field List lands before an entry's index, a whole number from 0, not ${index} (ADR-0390).`);
    return index;
}

// A name a control or an anchor is handed in by, as a script may hand it.
function checkedKey(key: string): string {
    const given: unknown = key;
    if (typeof given !== 'string' || given === '') throw new TypeError('An ExPivot\'s Field List hands in a control or an anchor by the name its view gives it (ADR-0390).');
    return given;
}
