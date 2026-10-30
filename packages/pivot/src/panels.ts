// ExPivot's built-in panels (ADR-0061): Field Settings… and Value Field Settings…, in the markup and under the
// classes the Razor component's PivotFieldSettingsView and PivotValueFieldSettingsView draw, so one stylesheet
// paints both (ADR-0350 section 4). A panel holds a draft until OK, which says the change as the next layout; Cancel
// and Escape drop it. Its controls are built once, when it opens, so nothing the pivot draws meanwhile writes over
// what the user is typing.
//
// What a draft holds and what OK makes of it is the draft's, which the built-in markup and a Chrome's slot draw alike
// (ADR-0390): swapping the Chrome changes no behaviour.

import {
    formatPivotNumber, pivotAggregationNameId, pivotAggregations, pivotCaptionId, pivotNumberFormatProblem, pivotShowValuesAsList,
    pivotShowValuesAsNameId, pivotSort, refusalWord, setSort, setSubtotals, setValueField, sortsEqual, type PivotAggregation, type PivotField,
    type PivotFieldPlacement, type PivotLayout, type PivotShowValuesAs, type PivotSort, type PivotSourceFeatures, type PivotValueFieldInput,
} from '@ex-grid/pivot-engine';
import {
    SlotMount, ViewListeners, type PivotChoice, type PivotChromeSlot, type PivotFieldSettingsSlotContext, type PivotFieldSettingsView, type PivotPanelSlotContext,
    type PivotValueFieldSettingsSlotContext, type PivotValueFieldSettingsView,
} from './chrome';
import { element as made } from './dom';
import { popupFrame, type PivotMenuPlace } from './menu';
import { elementAt } from './must';
import type { PivotScreenWords } from './words';

/** A panel open under a field's entry. */
export interface PivotPanel {
    /** Its frame, which the pivot places. */
    readonly element: HTMLElement;
    /** Gives the keyboard to its first control. */
    takeKeyboard(): void;
    /** Draws again what it shows of the pivot's state, which the pivot has drawn again. */
    redraw?(): void;
    /** Closed: stops what it has asked that is still on its way. */
    close?(): void;
    /** Whether a popup of a Chrome's contents is open, which may stand outside the frame and take the keyboard there. */
    innerPopupOpen?(): boolean;
}

/** Whom a panel calls back. */
export interface PanelHandlers {
    /** OK made `layout`, taken against the layout the panel was opened on. */
    readonly apply: (layout: PivotLayout) => void;
    /** Cancel or Escape, or OK with nothing changed. */
    readonly cancel: () => void;
}

// Each panel's radio group is named apart from every other's on the page (ADR-0018).
let groups = 0;

/** A panel's element. */
export function element<K extends keyof HTMLElementTagNameMap>(document: Document, tag: K, className?: string): HTMLElementTagNameMap[K] {
    const control = made(document, tag, className);
    // A draft's control claims nothing of the pivot's input: holding the keyboard, it is the pivot's own (ADR-0240).
    if (tag === 'input' || tag === 'select') control.setAttribute('data-ex-claims', '');
    return control;
}

function labelled(document: Document, words: string, control: HTMLElement): HTMLLabelElement {
    const row = element(document, 'label', 'ex-pivot-row');
    const text = element(document, 'span');
    text.textContent = words;
    row.append(text, control);
    return row;
}

/** OK and Cancel, which claim their click. */
export function actions(document: Document, words: PivotScreenWords, ok: () => void, cancel: () => void): { readonly box: HTMLElement; readonly ok: HTMLButtonElement } {
    const box = element(document, 'div', 'ex-pivot-panel-actions');
    const make = (className: string, id: string, pressed: () => void): HTMLButtonElement => {
        const button = element(document, 'button', className);
        button.type = 'button';
        button.setAttribute('data-ex-claims', 'click');
        button.textContent = words.word(id);
        button.addEventListener('click', pressed);
        return button;
    };
    const okButton = make('ex-pivot-ok', 'ok', ok);
    box.append(okButton, make('ex-pivot-cancel', 'cancel', cancel));
    return { box, ok: okButton };
}

/** A draft a panel draws: its view, told to its listeners at each change, and OK. */
export interface PanelDraft<View> {
    /** What the panel shows now: a new object at each change, and the same one otherwise. */
    readonly view: View;
    /** Hears each change to the view. */
    readonly listeners: ViewListeners;
    /** OK: the draft as the next layout, a refusal said in the view, or Cancel where nothing changed. */
    apply(): void;
}

/**
 * A panel whose contents a Chrome's slot draws (ADR-0390), inside the pivot's frame, from its draft: every choice is
 * the draft's, and OK and Cancel the panel's handlers'. A request for the keyboard mounts the contents again with the
 * next count; once the panel is closed, nothing they call back reaches the pivot.
 */
export function slottedPanel<View, Context extends PivotPanelSlotContext>(document: Document, frameLabel: string, place: PivotMenuPlace, name: string,
    mount: PivotChromeSlot<Context>, draft: PanelDraft<View>, cancel: () => void, words: PivotScreenWords,
    choices: (input: <Args extends unknown[]>(change: (...args: Args) => void) => (...args: Args) => void) => Omit<Context, keyof PivotPanelSlotContext | 'view'>,
    redraw?: () => void, closing?: () => void): PivotPanel {
    // A popup of the contents' own takes Escape first.
    const frame = popupFrame(document, place, 'dialog', cancel, () => slot.innerPopupOpen);
    frame.setAttribute('aria-label', frameLabel);
    const body = document.createElement('div');
    frame.append(body);
    const slot: SlotMount<Context> = new SlotMount<Context>(name, mount, body, focusRequest => {
        const input = <Args extends unknown[]>(change: (...args: Args) => void): ((...args: Args) => void) => slot.input(change);
        const parts: PivotPanelSlotContext & { readonly view: () => View } = {
            label: (id: string) => words.word(id),
            focusRequest,
            takeFocus: slot.takeFocus,
            subscribe: draft.listeners.subscribe,
            view: () => draft.view,
            apply: input(() => { draft.apply(); }),
            cancel: input(cancel),
            innerPopupChanged: slot.innerPopupChanged,
        };
        // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- the parts every panel's context shares, and the choices this panel's adds, make its context
        return Object.freeze({ ...parts, ...choices(input) }) as unknown as Context;
    }, cancel);
    // Mounted first by the request for the keyboard its opening makes, once the pivot holds the panel as open: contents
    // that cannot be drawn then close a panel that stands.
    return Object.freeze({
        element: frame,
        takeKeyboard: () => { slot.request(); },
        innerPopupOpen: () => slot.innerPopupOpen,
        ...(redraw === undefined ? {} : { redraw }),
        close: () => {
            closing?.();
            draft.listeners.clear();
            slot.dispose();
        },
    });
}

/** What Field Settings… is opened on. */
export interface FieldSettingsModel {
    readonly layout: PivotLayout;
    readonly placement: PivotFieldPlacement;
    readonly caption: string;
    /** The Value Fields' captions, which name the sorts by value. */
    readonly valueCaptions: readonly string[];
    readonly words: PivotScreenWords;
}

/** Field Settings…' draft: the subtotals and the order, until OK. */
export class FieldSettingsDraft implements PanelDraft<PivotFieldSettingsView> {
    readonly listeners = new ViewListeners();
    readonly #model: FieldSettingsModel;
    readonly #handlers: PanelHandlers;
    readonly #sorts: readonly { readonly sort: PivotSort; readonly label: string }[];
    #view: PivotFieldSettingsView;

    constructor(model: FieldSettingsModel, handlers: PanelHandlers) {
        this.#model = model;
        this.#handlers = handlers;
        const { placement, words } = model;
        // A to Z and Z to A by label, then each Value Field's two orders, as Excel's More Sort Options lists them.
        const sorts: { readonly sort: PivotSort; readonly label: string }[] = [
            { sort: pivotSort('ascending'), label: words.word('sort-label-ascending') },
            { sort: pivotSort('descending'), label: words.word('sort-label-descending') },
        ];
        model.valueCaptions.forEach((caption, i) => {
            sorts.push({ sort: pivotSort('ascending', i), label: words.fill('sort-value-ascending', caption) });
            sorts.push({ sort: pivotSort('descending', i), label: words.fill('sort-value-descending', caption) });
        });
        this.#sorts = sorts;
        const current = sorts.findIndex(({ sort }) => sortsEqual(sort, placement.sort));
        if (current < 0) throw new Error(`Internal: the field's order is none Field Settings… lists.`);
        this.#view = Object.freeze({
            caption: model.caption, subtotals: placement.subtotals, sorts: Object.freeze(sorts.map(({ label }) => label)), sort: current,
        });
    }

    get view(): PivotFieldSettingsView {
        return this.#view;
    }

    setSubtotals(automatic: boolean): void {
        if (typeof automatic !== 'boolean') throw new TypeError('setSubtotals is handed whether the subtotals are Automatic.');
        this.#change({ subtotals: automatic });
    }

    setSort(index: number): void {
        if (!Number.isSafeInteger(index) || index < 0 || index >= this.#sorts.length) {
            throw new RangeError(`setSort is handed the index of one of the panel's ${this.#sorts.length} orders, not ${index}.`);
        }
        this.#change({ sort: index });
    }

    apply(): void {
        const { layout, placement } = this.#model;
        const sort = elementAt(this.#sorts, this.#view.sort).sort;
        const { subtotals } = this.#view;
        if (subtotals === placement.subtotals && sortsEqual(sort, placement.sort)) {
            this.#handlers.cancel();
            return;
        }
        this.#handlers.apply(setSort(setSubtotals(layout, placement.field, subtotals), placement.field, sort));
    }

    #change(change: Partial<PivotFieldSettingsView>): void {
        this.#view = Object.freeze({ ...this.#view, ...change });
        this.listeners.tell();
    }
}

/** Field Settings… (ADR-0061): a row or column field's subtotals and order, drawn by `mount` where a Chrome gives one. */
export function fieldSettingsPanel(document: Document, model: FieldSettingsModel, handlers: PanelHandlers,
    mount?: PivotChromeSlot<PivotFieldSettingsSlotContext>): PivotPanel {
    const { words } = model;
    const draft = new FieldSettingsDraft(model, handlers);
    if (mount !== undefined) {
        return slottedPanel(document, words.word('field-settings'), 'field-list', 'fieldSettings', mount, draft, handlers.cancel, words, input => ({
            setSubtotals: input((automatic: boolean) => { draft.setSubtotals(automatic); }),
            setSort: input((index: number) => { draft.setSort(index); }),
        }));
    }
    const frame = popupFrame(document, 'field-list', 'dialog', handlers.cancel);
    frame.setAttribute('aria-label', words.word('field-settings'));
    const panel = element(document, 'div', 'ex-pivot-panel ex-pivot-field-settings');
    const title = element(document, 'div', 'ex-pivot-panel-title');
    title.textContent = model.caption;

    const fieldset = element(document, 'fieldset', 'ex-pivot-fieldset');
    const legend = element(document, 'legend');
    legend.textContent = words.word('subtotals');
    groups++;
    const group = `ex-pivot-subtotals-${groups}`;
    const radio = (id: string, automatic: boolean): { readonly row: HTMLLabelElement; readonly input: HTMLInputElement } => {
        const row = element(document, 'label', 'ex-pivot-choice');
        const input = element(document, 'input');
        input.type = 'radio';
        input.name = group;
        input.checked = draft.view.subtotals === automatic;
        input.addEventListener('change', () => { if (input.checked) draft.setSubtotals(automatic); });
        const text = element(document, 'span');
        text.textContent = words.word(id);
        row.append(input, text);
        return { row, input };
    };
    const automatic = radio('subtotals-automatic', true);
    const none = radio('subtotals-none', false);
    fieldset.append(legend, automatic.row, none.row);

    const select = element(document, 'select', 'ex-pivot-select');
    draft.view.sorts.forEach((label, i) => {
        const option = element(document, 'option');
        option.value = String(i);
        option.textContent = label;
        select.append(option);
    });
    select.value = String(draft.view.sort);
    select.addEventListener('change', () => { draft.setSort(Number(select.value)); });

    panel.append(title, fieldset, labelled(document, words.word('sort-order'), select), actions(document, words, () => { draft.apply(); }, handlers.cancel).box);
    frame.append(panel);
    return Object.freeze({ element: frame, takeKeyboard: () => { automatic.input.focus(); } });
}

/** What Value Field Settings… is opened on. */
export interface ValueFieldSettingsModel {
    readonly layout: PivotLayout;
    readonly index: number;
    /** The field's caption. */
    readonly fieldCaption: string;
    /** The Value Field's caption as the report shows it. */
    readonly caption: string;
    readonly fields: readonly PivotField[];
    readonly features: PivotSourceFeatures;
    readonly locale: string;
    readonly words: PivotScreenWords;
}

/** Value Field Settings…' draft: the caption, the Aggregation, Show Values As and the number format, until OK. */
export class ValueFieldSettingsDraft implements PanelDraft<PivotValueFieldSettingsView> {
    readonly listeners = new ViewListeners();
    readonly #model: ValueFieldSettingsModel;
    readonly #handlers: PanelHandlers;
    #captionTouched = false;
    #view: PivotValueFieldSettingsView;

    constructor(model: ValueFieldSettingsModel, handlers: PanelHandlers) {
        this.#model = model;
        this.#handlers = handlers;
        const { layout, index, words, features } = model;
        const value = elementAt(layout.values, index);
        const aggregations = pivotAggregations.map(choice => {
            const name = words.word(pivotAggregationNameId(choice));
            return Object.freeze<PivotChoice<PivotAggregation>>(features.offers(choice)
                ? { value: choice, label: name, enabled: true }
                : { value: choice, label: name, enabled: false, reason: words.fill('aggregation-not-offered', name) });
        });
        const showValuesAsChoices = pivotShowValuesAsList.map(choice => Object.freeze<PivotChoice<PivotShowValuesAs>>({
            value: choice, label: words.word(pivotShowValuesAsNameId(choice)), enabled: true,
        }));
        const numberFormat = value.numberFormat ?? ''; // oxlint-disable-line ex-grid/no-stand-in-default -- no format of its own is General, which the field shows empty
        this.#view = Object.freeze({
            sourceCaption: model.fieldCaption,
            caption: model.caption,
            aggregation: value.aggregation,
            aggregations: Object.freeze(aggregations),
            showValuesAs: value.showValuesAs,
            showValuesAsChoices: Object.freeze(showValuesAsChoices),
            numberFormat,
            numberFormatPlaceholder: words.word('number-format-general'),
            sample: this.#sampleOf(value.showValuesAs, numberFormat),
        });
    }

    get view(): PivotValueFieldSettingsView {
        return this.#view;
    }

    setCaption(text: string): void {
        if (typeof text !== 'string') throw new TypeError('setCaption is handed the caption as typed.');
        this.#captionTouched = true;
        this.#change({ caption: text });
    }

    setAggregation(aggregation: PivotAggregation): void {
        if (!pivotAggregations.includes(aggregation)) throw new RangeError(`'${aggregation}' is not an Aggregation.`);
        // An Aggregation the source does not answer is never chosen: it is never asked for (ADR-0066).
        if (!this.#model.features.offers(aggregation)) {
            this.listeners.tell();
            return;
        }
        const value = elementAt(this.#model.layout.values, this.#model.index);
        // A caption the user has not written follows the Aggregation, as Excel's Custom Name box does.
        this.#change({ aggregation, ...(!this.#captionTouched && value.caption === undefined ? { caption: this.#defaultCaption(aggregation) } : {}) });
    }

    setShowValuesAs(showValuesAs: PivotShowValuesAs): void {
        if (!pivotShowValuesAsList.includes(showValuesAs)) throw new RangeError(`'${showValuesAs}' is not a Show Values As.`);
        this.#change({ showValuesAs, sample: this.#sampleOf(showValuesAs, this.#view.numberFormat) });
    }

    setNumberFormat(text: string): void {
        if (typeof text !== 'string') throw new TypeError('setNumberFormat is handed the number format as typed.');
        this.#change({ numberFormat: text, sample: this.#sampleOf(this.#view.showValuesAs, text) });
    }

    apply(): void {
        const { layout, index, words, features } = this.#model;
        const value = elementAt(layout.values, index);
        const { aggregation: chosen, showValuesAs: shown } = this.#view;
        // A Value Field the Consumer handed in with an Aggregation the source does not answer is refused here, never
        // asked for (ADR-0066).
        if (!features.offers(chosen)) {
            this.#refuse(words.fill('aggregation-not-offered', words.word(pivotAggregationNameId(chosen))));
            return;
        }
        const trimmed = this.#view.caption.trim();
        // A caption left as the pivot wrote it stays the pivot's, and follows the Aggregation and the words.
        const own = (!this.#captionTouched && value.caption === undefined) || trimmed === this.#defaultCaption(chosen) ? undefined : trimmed;
        const numberFormat = this.#view.numberFormat.trim();
        if (chosen === value.aggregation && shown === value.showValuesAs && own === value.caption && (numberFormat === '' ? undefined : numberFormat) === value.numberFormat) {
            this.#handlers.cancel();
            return;
        }
        const input: PivotValueFieldInput = {
            field: value.field,
            aggregation: chosen,
            showValuesAs: shown,
            ...(own === undefined ? {} : { caption: own }),
            ...(numberFormat === '' ? {} : { numberFormat }),
        };
        const result = setValueField(layout, index, input, this.#model.fields, { locale: this.#model.locale, ...(words.label === undefined ? {} : { label: words.label }) });
        if (result.refusal !== undefined) {
            this.#refuse(words.word(refusalWord(result.refusal)));
            return;
        }
        this.#handlers.apply(result.layout);
    }

    #defaultCaption(aggregation: PivotAggregation): string {
        return this.#model.words.fill(pivotCaptionId(aggregation), this.#model.fieldCaption);
    }

    // A number shown in the draft's format, or the reason it cannot be, so a format is seen before it is applied.
    #sampleOf(showValuesAs: PivotShowValuesAs, format: string): string {
        const { words, locale } = this.#model;
        const trimmed = format.trim();
        const percent = showValuesAs !== 'noCalculation';
        if (trimmed !== '' && pivotNumberFormatProblem(trimmed) !== undefined) return words.word('refused-number-format');
        return words.fill('sample', formatPivotNumber(percent ? 0.1234 : -1234.5678, trimmed === '' ? (percent ? '0.00%' : 'G15') : trimmed, locale));
    }

    #refuse(sentence: string): void {
        this.#view = Object.freeze({ ...this.#view, refusal: sentence });
        this.listeners.tell();
    }

    // Any change of the draft takes back a refusal said for the draft before it.
    #change(change: Partial<PivotValueFieldSettingsView>): void {
        const { refusal: _said, ...kept } = this.#view;
        this.#view = Object.freeze({ ...kept, ...change });
        this.listeners.tell();
    }
}

/** Value Field Settings… (ADR-0061): the caption, the Aggregation, Show Values As and the number format with a
 * sample, drawn by `mount` where a Chrome gives one. OK is refused, the sentence said in the panel, for a caption taken
 * or empty and for a format that cannot be used; an Aggregation the source does not answer is offered disabled, with
 * the reason (ADR-0066). */
export function valueFieldSettingsPanel(document: Document, model: ValueFieldSettingsModel, handlers: PanelHandlers,
    mount?: PivotChromeSlot<PivotValueFieldSettingsSlotContext>): PivotPanel {
    const { words } = model;
    const draft = new ValueFieldSettingsDraft(model, handlers);
    if (mount !== undefined) {
        return slottedPanel(document, words.word('value-field-settings'), 'field-list', 'valueFieldSettings', mount, draft, handlers.cancel, words, input => ({
            setCaption: input((text: string) => { draft.setCaption(text); }),
            setAggregation: input((aggregation: PivotAggregation) => { draft.setAggregation(aggregation); }),
            setShowValuesAs: input((showValuesAs: PivotShowValuesAs) => { draft.setShowValuesAs(showValuesAs); }),
            setNumberFormat: input((text: string) => { draft.setNumberFormat(text); }),
        }));
    }
    const view = (): PivotValueFieldSettingsView => draft.view;
    const frame = popupFrame(document, 'field-list', 'dialog', handlers.cancel);
    frame.setAttribute('aria-label', words.word('value-field-settings'));
    const panel = element(document, 'div', 'ex-pivot-panel ex-pivot-value-settings');
    const title = element(document, 'div', 'ex-pivot-panel-title');
    title.textContent = words.word('value-field-settings');
    const source = element(document, 'div', 'ex-pivot-row ex-pivot-source');
    source.textContent = view().sourceCaption;

    const caption = element(document, 'input', 'ex-pivot-text');
    caption.type = 'text';
    caption.value = view().caption;

    const aggregation = element(document, 'select', 'ex-pivot-select');
    const notOffered: HTMLElement[] = [];
    for (const choice of view().aggregations) {
        const option = element(document, 'option');
        option.value = choice.value;
        option.textContent = choice.label;
        if (choice.reason !== undefined) {
            option.disabled = true;
            option.title = choice.reason;
            const note = element(document, 'div', 'ex-pivot-note ex-pivot-not-offered');
            note.textContent = choice.reason;
            notOffered.push(note);
        }
        aggregation.append(option);
    }
    aggregation.value = view().aggregation;

    const showAs = element(document, 'select', 'ex-pivot-select');
    for (const choice of view().showValuesAsChoices) {
        const option = element(document, 'option');
        option.value = choice.value;
        option.textContent = choice.label;
        showAs.append(option);
    }
    showAs.value = view().showValuesAs;

    const format = element(document, 'input', 'ex-pivot-text');
    format.type = 'text';
    format.placeholder = view().numberFormatPlaceholder;
    format.value = view().numberFormat;
    const sample = element(document, 'div', 'ex-pivot-sample');
    // The refusal is said under the sample while it stands, and is not on the page otherwise, as the Razor component's.
    const refusal = element(document, 'div', 'ex-pivot-refusal');
    refusal.setAttribute('role', 'alert');

    // Each control shows the draft: a text field is written only where the draft differs from what is typed in it, so
    // the caret stays where the user put it.
    const drawn = (): void => {
        const now = view();
        if (caption.value !== now.caption) caption.value = now.caption;
        aggregation.value = now.aggregation;
        showAs.value = now.showValuesAs;
        if (format.value !== now.numberFormat) format.value = now.numberFormat;
        sample.textContent = now.sample;
        if (now.refusal === undefined) {
            refusal.remove();
        } else {
            refusal.textContent = now.refusal;
            sample.after(refusal);
        }
    };
    draft.listeners.subscribe(drawn);
    caption.addEventListener('input', () => { draft.setCaption(caption.value); });
    aggregation.addEventListener('change', () => {
        const picked = pivotAggregations.find(a => a === aggregation.value);
        if (picked === undefined) throw new Error(`Internal: '${aggregation.value}' is not an Aggregation.`);
        draft.setAggregation(picked);
    });
    showAs.addEventListener('change', () => {
        const picked = pivotShowValuesAsList.find(s => s === showAs.value);
        if (picked === undefined) throw new Error(`Internal: '${showAs.value}' is not a Show Values As.`);
        draft.setShowValuesAs(picked);
    });
    format.addEventListener('input', () => { draft.setNumberFormat(format.value); });
    drawn();

    panel.append(
        title, source,
        labelled(document, words.word('custom-name'), caption),
        labelled(document, words.word('summarize-by'), aggregation),
        ...notOffered,
        labelled(document, words.word('show-values-as'), showAs),
        labelled(document, words.word('number-format'), format),
        sample,
        actions(document, words, () => { draft.apply(); }, handlers.cancel).box,
    );
    frame.append(panel);
    return Object.freeze({ element: frame, takeKeyboard: () => { caption.focus(); }, close: () => { draft.listeners.clear(); } });
}
