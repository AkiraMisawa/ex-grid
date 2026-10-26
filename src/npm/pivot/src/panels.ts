// ExPivot's built-in panels (ADR-0061): Field Settings… and Value Field Settings…, in the markup and under the
// classes the Razor component's PivotFieldSettingsView and PivotValueFieldSettingsView draw, so one stylesheet
// paints both (ADR-0350 section 4). A panel holds a draft until OK, which says the change as the next layout; Cancel
// and Escape drop it. Its controls are built once, when it opens, so nothing the pivot draws meanwhile writes over
// what the user is typing.

import {
    formatPivotNumber, pivotAggregationNameId, pivotAggregations, pivotCaptionId, pivotNumberFormatProblem, pivotShowValuesAsList,
    pivotShowValuesAsNameId, pivotSort, refusalWord, setSort, setSubtotals, setValueField, sortsEqual, type PivotAggregation, type PivotField,
    type PivotFieldPlacement, type PivotLayout, type PivotSort, type PivotSourceFeatures, type PivotValueFieldInput,
} from '@ex-grid/pivot-engine';
import { element as made } from './dom';
import { popupFrame } from './menu';
import { elementAt } from './must';
import type { PivotScreenWords } from './words';

/** A panel open under a field's entry. */
export interface PivotPanel {
    /** Its frame, which the pivot places. */
    readonly element: HTMLElement;
    /** Gives the keyboard to its first control. */
    takeKeyboard(): void;
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

function element<K extends keyof HTMLElementTagNameMap>(document: Document, tag: K, className?: string): HTMLElementTagNameMap[K] {
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

// OK and Cancel, which claim their click.
function actions(document: Document, words: PivotScreenWords, ok: () => void, cancel: () => void): { readonly box: HTMLElement; readonly ok: HTMLButtonElement } {
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

/** What Field Settings… is opened on. */
export interface FieldSettingsModel {
    readonly layout: PivotLayout;
    readonly placement: PivotFieldPlacement;
    readonly caption: string;
    /** The Value Fields' captions, which name the sorts by value. */
    readonly valueCaptions: readonly string[];
    readonly words: PivotScreenWords;
}

/** Field Settings… (ADR-0061): a row or column field's subtotals and order. */
export function fieldSettingsPanel(document: Document, model: FieldSettingsModel, handlers: PanelHandlers): PivotPanel {
    const { layout, placement, words } = model;
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
    const radio = (id: string, checked: boolean): { readonly row: HTMLLabelElement; readonly input: HTMLInputElement } => {
        const row = element(document, 'label', 'ex-pivot-choice');
        const input = element(document, 'input');
        input.type = 'radio';
        input.name = group;
        input.checked = checked;
        const text = element(document, 'span');
        text.textContent = words.word(id);
        row.append(input, text);
        return { row, input };
    };
    const automatic = radio('subtotals-automatic', placement.subtotals);
    const none = radio('subtotals-none', !placement.subtotals);
    fieldset.append(legend, automatic.row, none.row);

    // A to Z and Z to A by label, then each Value Field's two orders, as Excel's More Sort Options lists them.
    const sorts: { readonly sort: PivotSort; readonly label: string }[] = [
        { sort: pivotSort('ascending'), label: words.word('sort-label-ascending') },
        { sort: pivotSort('descending'), label: words.word('sort-label-descending') },
    ];
    model.valueCaptions.forEach((caption, i) => {
        sorts.push({ sort: pivotSort('ascending', i), label: words.fill('sort-value-ascending', caption) });
        sorts.push({ sort: pivotSort('descending', i), label: words.fill('sort-value-descending', caption) });
    });
    const select = element(document, 'select', 'ex-pivot-select');
    sorts.forEach(({ label }, i) => {
        const option = element(document, 'option');
        option.value = String(i);
        option.textContent = label;
        select.append(option);
    });
    const current = sorts.findIndex(({ sort }) => sortsEqual(sort, placement.sort));
    if (current < 0) throw new Error(`Internal: the field's order is none Field Settings… lists.`);
    select.value = String(current);

    const ok = (): void => {
        const sort = elementAt(sorts, Number(select.value)).sort;
        const subtotals = automatic.input.checked;
        if (subtotals === placement.subtotals && sortsEqual(sort, placement.sort)) {
            handlers.cancel();
            return;
        }
        handlers.apply(setSort(setSubtotals(layout, placement.field, subtotals), placement.field, sort));
    };
    panel.append(title, fieldset, labelled(document, words.word('sort-order'), select), actions(document, words, ok, handlers.cancel).box);
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

/** Value Field Settings… (ADR-0061): the caption, the Aggregation, Show Values As and the number format with a
 * sample. OK is refused, the sentence said in the panel, for a caption taken or empty and for a format that cannot
 * be used; an Aggregation the source does not answer is offered disabled, with the reason (ADR-0066). */
export function valueFieldSettingsPanel(document: Document, model: ValueFieldSettingsModel, handlers: PanelHandlers): PivotPanel {
    const { layout, index, words, features } = model;
    const value = elementAt(layout.values, index);
    const defaultCaption = (aggregation: PivotAggregation): string => words.fill(pivotCaptionId(aggregation), model.fieldCaption);
    const frame = popupFrame(document, 'field-list', 'dialog', handlers.cancel);
    frame.setAttribute('aria-label', words.word('value-field-settings'));
    const panel = element(document, 'div', 'ex-pivot-panel ex-pivot-value-settings');
    const title = element(document, 'div', 'ex-pivot-panel-title');
    title.textContent = words.word('value-field-settings');
    const source = element(document, 'div', 'ex-pivot-row ex-pivot-source');
    source.textContent = model.fieldCaption;

    const caption = element(document, 'input', 'ex-pivot-text');
    caption.type = 'text';
    caption.value = model.caption;
    let captionTouched = false;

    const aggregation = element(document, 'select', 'ex-pivot-select');
    const notOffered: HTMLElement[] = [];
    for (const choice of pivotAggregations) {
        const option = element(document, 'option');
        option.value = choice;
        const name = words.word(pivotAggregationNameId(choice));
        option.textContent = name;
        if (!features.offers(choice)) {
            const reason = words.fill('aggregation-not-offered', name);
            option.disabled = true;
            option.title = reason;
            const note = element(document, 'div', 'ex-pivot-note ex-pivot-not-offered');
            note.textContent = reason;
            notOffered.push(note);
        }
        aggregation.append(option);
    }
    aggregation.value = value.aggregation;
    let chosen = value.aggregation;

    const showAs = element(document, 'select', 'ex-pivot-select');
    for (const choice of pivotShowValuesAsList) {
        const option = element(document, 'option');
        option.value = choice;
        option.textContent = words.word(pivotShowValuesAsNameId(choice));
        showAs.append(option);
    }
    showAs.value = value.showValuesAs;

    const format = element(document, 'input', 'ex-pivot-text');
    format.type = 'text';
    format.placeholder = words.word('number-format-general');
    format.value = value.numberFormat ?? ''; // oxlint-disable-line ex-grid/no-stand-in-default -- no format of its own is General, which the field shows empty
    const sample = element(document, 'div', 'ex-pivot-sample');
    // The refusal is said under the sample while it stands, and is not on the page otherwise, as the Razor component's.
    const refusal = element(document, 'div', 'ex-pivot-refusal');
    refusal.setAttribute('role', 'alert');

    // A number shown in the draft's format, or the reason it cannot be, so a format is seen before it is applied.
    const drawSample = (): void => {
        const trimmed = format.value.trim();
        const percent = showAs.value !== 'noCalculation';
        if (trimmed !== '' && pivotNumberFormatProblem(trimmed) !== undefined) {
            sample.textContent = words.word('refused-number-format');
            return;
        }
        const written = formatPivotNumber(percent ? 0.1234 : -1234.5678, trimmed === '' ? (percent ? '0.00%' : 'G15') : trimmed, model.locale);
        sample.textContent = words.fill('sample', written);
    };
    const refuse = (sentence: string | undefined): void => {
        if (sentence === undefined) {
            refusal.remove();
            return;
        }
        refusal.textContent = sentence;
        sample.after(refusal);
    };
    caption.addEventListener('input', () => {
        captionTouched = true;
        refuse(undefined);
    });
    aggregation.addEventListener('change', () => {
        const next = aggregation.value;
        const picked = pivotAggregations.find(a => a === next);
        // An Aggregation the source does not answer is never chosen: it is never asked for (ADR-0066).
        if (picked === undefined || !features.offers(picked)) {
            aggregation.value = chosen;
            return;
        }
        // A caption the user has not written follows the Aggregation, as Excel's Custom Name box does.
        if (!captionTouched && value.caption === undefined) caption.value = defaultCaption(picked);
        chosen = picked;
        refuse(undefined);
    });
    showAs.addEventListener('change', () => {
        drawSample();
        refuse(undefined);
    });
    format.addEventListener('input', () => {
        drawSample();
        refuse(undefined);
    });
    drawSample();

    const ok = (): void => {
        const shown = pivotShowValuesAsList.find(s => s === showAs.value);
        if (shown === undefined) throw new Error(`Internal: '${showAs.value}' is not a Show Values As.`);
        // A Value Field the Consumer handed in with an Aggregation the source does not answer is refused here, never
        // asked for (ADR-0066).
        if (!features.offers(chosen)) {
            refuse(words.fill('aggregation-not-offered', words.word(pivotAggregationNameId(chosen))));
            return;
        }
        const trimmed = caption.value.trim();
        // A caption left as the pivot wrote it stays the pivot's, and follows the Aggregation and the words.
        const own = (!captionTouched && value.caption === undefined) || trimmed === defaultCaption(chosen) ? undefined : trimmed;
        const numberFormat = format.value.trim();
        if (chosen === value.aggregation && shown === value.showValuesAs && own === value.caption && (numberFormat === '' ? undefined : numberFormat) === value.numberFormat) {
            handlers.cancel();
            return;
        }
        const input: PivotValueFieldInput = {
            field: value.field,
            aggregation: chosen,
            showValuesAs: shown,
            ...(own === undefined ? {} : { caption: own }),
            ...(numberFormat === '' ? {} : { numberFormat }),
        };
        const result = setValueField(layout, index, input, model.fields, { locale: model.locale, ...(words.label === undefined ? {} : { label: words.label }) });
        if (result.refusal !== undefined) {
            refuse(words.word(refusalWord(result.refusal)));
            return;
        }
        handlers.apply(result.layout);
    };
    panel.append(
        title, source,
        labelled(document, words.word('custom-name'), caption),
        labelled(document, words.word('summarize-by'), aggregation),
        ...notOffered,
        labelled(document, words.word('show-values-as'), showAs),
        labelled(document, words.word('number-format'), format),
        sample,
        actions(document, words, ok, handlers.cancel).box,
    );
    frame.append(panel);
    return Object.freeze({ element: frame, takeKeyboard: () => { caption.focus(); } });
}
