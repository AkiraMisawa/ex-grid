// Format Cells filled with Razor (ADR-0410, ADR-0071): the slot's kind over the Shared Script's RazorSlotHost. The
// draft stays the Sheet's, in the browser: .NET is handed what the contents show, drawn from it at each choice, and
// every control the Razor draws is read here, as the browser fires it, and set through the draft. The tab shown, the
// controls' checked state and the texts of the fields are set here too, from the draft, so a choice a render has not yet
// reached is never drawn over, and OK, made at once after a choice, sets that choice (the spine's sixth rule). Nothing
// a key means waits on the circuit.

import type { RazorSlotKind } from '@ex-grid/blazor-shared';
import {
    formatCellsLineStroke, formatCellsOffer, isDisabledCategory, type BorderEdge, type BorderPreset, type ColourTarget, type FontStyle,
    type FormatCellsDraft, type FormatCellsLabel, type FormatCellsTab, type NumberFormatCategory, type PaletteSwatch, type SheetFormatCellsSlotContext,
    type SheetWords,
} from '@ex-grid/sheet';
import type { BorderLineStyle, CellColour, HorizontalAlignment } from '@ex-grid/sheet-engine';
import { crossed, marked } from './slot-markup';

/** The attributes Format Cells' Razor writes on its controls (ADR-0410 section 2), as `SheetScriptSlotAttributes` names
 * them in .NET. */
export const formatCellsAttributes = Object.freeze({
    /** On the form that holds every tab: its submission is OK, its fields checked first. */
    form: 'data-ex-format-form',
    /** On a tab's control, the tab it shows. */
    tab: 'data-ex-format-tab',
    /** On the element that holds a tab's controls, the tab: shown while that tab is. */
    panel: 'data-ex-format-panel',
    /** On a control that makes a choice: which, from `formatCellsSets`. */
    set: 'data-ex-format-set',
    /** Beside `set`, on a control that makes one of several: which one. */
    value: 'data-ex-format-value',
    /** On Cancel. */
    cancel: 'data-ex-format-cancel',
});

/** What a control marked {@link formatCellsAttributes.set} sets, and how it is read. */
const formatCellsSets = [
    'category', 'places', 'separator', 'negative', 'code', 'type', 'alignment', 'fontStyle', 'underline', 'strikethrough', 'normalFont',
    'lineStyle', 'preset', 'edge', 'colour', 'colourText',
] as const;
type FormatCellsSet = typeof formatCellsSets[number];

/** The labels Format Cells' contents are drawn with, every one of them, as .NET is handed them. */
const formatCellsLabels: readonly FormatCellsLabel[] = [
    'tabs', 'category', 'generalHint', 'textHint', 'decimalPlaces', 'thousandsSeparator', 'negativeNumbers', 'type', 'codes', 'horizontal',
    'fontStyle', 'underline', 'underlineNone', 'underlineSingle', 'strikethrough', 'normalFont', 'colour', 'lineStyle', 'presets', 'border',
    'preview', 'backgroundColour', 'automatic', 'noColour', 'moreColours', 'ok', 'cancel',
];

/** Every swatch of the palette, as its index in the view numbers it: the theme's rows, then the standard colours. */
const swatches: readonly PaletteSwatch[] = [...formatCellsOffer.themeColours.flat(), ...formatCellsOffer.standardColours];

/** What an opening keeps across the renders .NET makes of its contents: the tab shown, and whether OK was refused. */
interface Opening {
    tab: FormatCellsTab;
    refused: boolean;
    /** The fields holding text the draft did not take, which stay as typed until they are typed in again. */
    readonly kept: WeakSet<HTMLElement>;
    /** Sends the view again, once the draft has changed. */
    again: (() => void) | undefined;
}

const openings = new WeakMap<SheetFormatCellsSlotContext, Opening>();

function openingOf(context: SheetFormatCellsSlotContext): Opening {
    const known = openings.get(context);
    if (known !== undefined) return known;
    const made: Opening = { tab: context.tab, refused: false, kept: new WeakSet(), again: undefined };
    openings.set(context, made);
    return made;
}

/** Format Cells' kind: the view as data, drawn again at each choice; the controls read in the browser and set through
 * the draft. It takes the keyboard, on the tab it opens on. */
export const formatCellsKind: RazorSlotKind<SheetFormatCellsSlotContext> = {
    takesKeyboard: true,
    data: context => ({ json: JSON.stringify(viewOf(context, openingOf(context))), drawn: undefined, memo: undefined }),
    // The draft hears nothing of its own: a choice made here sends the view again.
    subscribe: (context, again) => {
        const opening = openingOf(context);
        opening.again = again;
        return () => { opening.again = undefined; };
    },
    wire: (contents, context, answered) => wireFormatCells(contents, context, answered),
};

/** A colour as a swatch and a preview paint it: Automatic is the Ink of the text around it. */
function paintOf(colour: CellColour): string {
    return colour.isAutomatic ? 'currentColor' : colour.toString();
}

/** One choice of a list as .NET is handed it. */
function choice(value: string, label: string, selected: boolean, enabled = true, note?: string): object {
    return { value, label, selected, enabled, note: crossed(note) };
}

/** The palette's swatches in rows, the theme's six and the standard one, each numbered as a choice of it names it:
 * sent once for the three palettes, which differ only in the one chosen. */
function swatchRowsOf(words: SheetWords): object[] {
    let at = 0;
    const row = (each: readonly PaletteSwatch[]): object[] => each.map(swatch => ({ index: at++, name: words.swatchName(swatch), paint: paintOf(swatch.colour) }));
    return [...formatCellsOffer.themeColours.map(row), row(formatCellsOffer.standardColours)];
}

/** A palette as .NET is handed it: the first choice, Automatic or No Colour, and the swatch chosen, by its number. */
function paletteOf(draft: FormatCellsDraft, words: SheetWords, target: ColourTarget): object {
    const isFill = target === 'fill';
    const has = draft.colourOf(target);
    const chosenAt = has === undefined || has.isAutomatic ? -1 : swatches.findIndex(swatch => has.equals(swatch.colour));
    return {
        target,
        legend: words.formatCellsLabel(isFill ? 'backgroundColour' : 'colour'),
        first: words.formatCellsLabel(isFill ? 'noColour' : 'automatic'),
        firstSelected: isFill ? has === undefined : has?.isAutomatic === true,
        selected: chosenAt < 0 ? null : chosenAt,
    };
}

/** What Format Cells' contents show, as .NET is handed it: the tabs, each tab's controls and their state, and the
 * refusal OK met, in the Sheet's words. */
function viewOf(context: SheetFormatCellsSlotContext, opening: Opening): object {
    const { draft, words } = context;
    const { category } = draft;
    const labels = Object.fromEntries(formatCellsLabels.map(label => [label, words.formatCellsLabel(label)]));
    const refusal = draft.refusal;
    const codeRefusal = draft.numberFormatRefusal;
    const pressedOf = (edge: BorderEdge): string => {
        const line = draft.edgeLine(edge);
        if (line === undefined) return 'mixed';
        return line.isNone ? 'false' : 'true';
    };
    return {
        title: words.formatCellsTitle,
        labels,
        tabs: formatCellsOffer.tabs.map(tab => ({ value: tab, label: words.tabName(tab) })),
        refusal: opening.refused && refusal !== undefined ? words.formatCellsRefusal(refusal) : null,
        swatches: swatchRowsOf(words),
        number: {
            categories: formatCellsOffer.categories.map(offer => choice(offer.category, words.categoryName(offer.category), offer.category === category,
                offer.enabled, isDisabledCategory(offer.category) ? words.categoryDisabled(offer.category) : undefined)),
            hint: category === 'general' ? words.formatCellsLabel('generalHint') : category === 'text' ? words.formatCellsLabel('textHint') : null,
            takesDecimalPlaces: draft.takesDecimalPlaces,
            thousandsSeparator: category === 'number' ? draft.thousandsSeparator : null,
            currencySymbol: category === 'currency' ? words.currencySymbolShown(draft.currencySymbol) : null,
            negativeNumbers: draft.negativeNumbers.map((code, index) => choice(String(index), code, draft.negativeNumber === index)),
            takesCode: category === 'custom',
            codeRefusal: codeRefusal === undefined ? null : words.formatCellsRefusal(codeRefusal),
            typesLegend: draft.types.length === 0 ? null : words.formatCellsLabel(category === 'custom' ? 'codes' : 'type'),
            types: draft.types.map(code => choice(code, code, draft.typeCode === code)),
        },
        alignments: formatCellsOffer.alignments.map(alignment => choice(alignment, words.alignmentName(alignment), draft.alignment === alignment)),
        font: {
            styles: formatCellsOffer.fontStyles.map(style => choice(style, words.fontStyleName(style), draft.fontStyle === style)),
            underline: draft.underline,
            strikethrough: draft.strikethrough,
            normalFont: draft.isNormalFont,
            palette: paletteOf(draft, words, 'font'),
        },
        border: {
            lineStyles: formatCellsOffer.lineStyleColumns.map(column => column.map(style => {
                const { width, dashes } = formatCellsLineStroke(style);
                return { value: style, label: words.lineStyleName(style), selected: draft.lineStyle === style, width, dashes: crossed(dashes) };
            })),
            presets: formatCellsOffer.presets.map(preset => choice(preset, words.presetName(preset), false, draft.canApply(preset))),
            edges: formatCellsOffer.edges.map(edge => {
                const line = draft.edgeLine(edge);
                const stroke = line === undefined ? undefined : formatCellsLineStroke(line.style);
                return {
                    value: edge, label: words.edgeName(edge), pressed: pressedOf(edge), enabled: draft.canSet(edge),
                    inPreview: edge === 'insideHorizontal' ? draft.hasInsideHorizontal : edge === 'insideVertical' ? draft.hasInsideVertical : true,
                    // An edge whose cells' sides differ crosses as no line, which the preview draws grey and dotted.
                    line: line === undefined || stroke === undefined
                        ? null
                        : { none: line.isNone, paint: paintOf(line.colour), width: stroke.width, dashes: crossed(stroke.dashes) },
                };
            }),
            palette: paletteOf(draft, words, 'border'),
        },
        fill: paletteOf(draft, words, 'fill'),
    };
}

/** One of `offered`, named by the attribute's value, refused by name where it is none of them. */
function oneOf<T extends string>(offered: readonly T[], value: string, what: string): T {
    const found = offered.find(each => each === value);
    if (found === undefined) throw new RangeError(`Format Cells' control is marked ${formatCellsAttributes.value}="${value}", which is no ${what} it offers.`);
    return found;
}

/** The colours a palette sets. */
const colourTargets: readonly ColourTarget[] = ['font', 'fill', 'border'];

/** A colour choice's target and swatch: `fill:12`, or `fill:none` for Automatic or No Colour. */
function colourChoice(value: string): { readonly target: ColourTarget; readonly colour: CellColour | undefined } {
    const [target, swatch, more] = value.split(':');
    if (target === undefined || swatch === undefined || more !== undefined) {
        throw new RangeError(`Format Cells' colour is marked ${formatCellsAttributes.value}="${value}": it reads target:index, or target:none.`);
    }
    const colourTarget = oneOf(colourTargets, target, 'colour target');
    if (swatch === 'none') return { target: colourTarget, colour: undefined };
    const index = Number(swatch);
    const chosen = Number.isSafeInteger(index) ? swatches[index] : undefined;
    if (chosen === undefined) throw new RangeError(`Format Cells' colour is marked ${formatCellsAttributes.value}="${value}": the palette has no swatch ${swatch}.`);
    return { target: colourTarget, colour: chosen.colour };
}

/** A control the Razor marked, read once as it is wired: what it sets, and the one of several it makes. */
interface Control {
    readonly element: HTMLElement;
    readonly set: FormatCellsSet;
    /** The value it carries, for a control that makes one of several. */
    readonly value: string | undefined;
    /** For a colour's radio, its target and colour; for More Colours, its target. */
    readonly colour: { readonly target: ColourTarget; readonly colour: CellColour | undefined } | undefined;
}

/** What a marked control sets, and the value it carries, refused by name where they are not Format Cells'. */
function controlOf(element: HTMLElement, set: string): Control {
    const known = oneOf(formatCellsSets, set, 'choice');
    const attribute = element.getAttribute(formatCellsAttributes.value);
    const value = attribute === null ? undefined : attribute;
    const valued = known === 'category' || known === 'negative' || known === 'type' || known === 'fontStyle' || known === 'lineStyle'
        || known === 'preset' || known === 'edge' || known === 'colour' || known === 'colourText';
    if (valued && value === undefined) throw new Error(`Format Cells' control marked ${formatCellsAttributes.set}="${known}" carries no ${formatCellsAttributes.value}.`);
    const colour = value === undefined ? undefined
        : known === 'colour' ? colourChoice(value)
            : known === 'colourText' ? { target: oneOf(colourTargets, value, 'colour target'), colour: undefined } : undefined;
    return { element, set: known, value, colour };
}

/** The value a control making one of several carries, read as it was wired. */
function valueOf(control: Control): string {
    if (control.value === undefined) throw new Error(`Internal: Format Cells' '${control.set}' was wired with no value.`);
    return control.value;
}

/** The target of a palette's control, read as it was wired. */
function colourOf(control: Control): { readonly target: ColourTarget; readonly colour: CellColour | undefined } {
    if (control.colour === undefined) throw new Error(`Internal: Format Cells' '${control.set}' was wired with no colour.`);
    return control.colour;
}

/** Whether a radio's or a checkbox's choice is the draft's now. */
function isChosen(draft: FormatCellsDraft, control: Control): boolean {
    switch (control.set) {
        case 'category': return draft.category === valueOf(control);
        case 'negative': return String(draft.negativeNumber) === valueOf(control);
        case 'type': return draft.typeCode === valueOf(control);
        case 'fontStyle': return draft.fontStyle === valueOf(control);
        case 'lineStyle': return draft.lineStyle === valueOf(control);
        case 'separator': return draft.thousandsSeparator;
        case 'strikethrough': return draft.strikethrough;
        case 'normalFont': return draft.isNormalFont;
        case 'colour': {
            const { target, colour } = colourOf(control);
            const has = draft.colourOf(target);
            if (colour === undefined) return target === 'fill' ? has === undefined : has?.isAutomatic === true;
            return has !== undefined && !has.isAutomatic && has.equals(colour);
        }
        case 'places': case 'code': case 'alignment': case 'underline': case 'preset': case 'edge': case 'colourText':
            throw new Error(`Format Cells' control marked ${formatCellsAttributes.set}="${control.set}" is drawn as a radio or a checkbox, which that choice is not.`);
        default: throw new Error(`Internal: '${String(control.set satisfies never)}' is no choice of Format Cells.`);
    }
}

/** The text a field shows while it does not hold the keyboard: the draft's. */
function fieldText(draft: FormatCellsDraft, control: Control): string {
    switch (control.set) {
        case 'places': return String(draft.decimalPlaces);
        case 'code': return draft.typeCode;
        case 'colourText': return draft.colourText(colourOf(control).target);
        case 'category': case 'separator': case 'negative': case 'type': case 'alignment': case 'fontStyle': case 'underline': case 'strikethrough':
        case 'normalFont': case 'lineStyle': case 'preset': case 'edge': case 'colour':
            throw new Error(`Format Cells' control marked ${formatCellsAttributes.set}="${control.set}" is drawn as a text field, which that choice is not.`);
        default: throw new Error(`Internal: '${String(control.set satisfies never)}' is no field of Format Cells.`);
    }
}

/** Whether `element` is a text field: a field the user types into, never a radio or a checkbox. */
function isField(element: HTMLElement): element is HTMLInputElement {
    return element instanceof HTMLInputElement && element.type !== 'radio' && element.type !== 'checkbox';
}

/**
 * Sets each control from the draft: what is checked, what a select shows, what an edge's button says, and the texts
 * of the fields, so a choice a render has not yet reached is drawn as the draft holds it. The contents draw none of
 * these, so no render a round trip behind writes over them. A field the keyboard is in keeps what is typed, and so does
 * one holding text the draft did not take, which OK refuses: its text is never quietly put back.
 */
function syncControls(contents: HTMLElement, draft: FormatCellsDraft, controls: readonly Control[], kept: WeakSet<HTMLElement>): void {
    const active = contents.ownerDocument.activeElement;
    for (const control of controls) {
        const { element } = control;
        if (element instanceof HTMLSelectElement) element.value = control.set === 'alignment' ? draft.alignment : draft.underline ? 'single' : 'none';
        else if (isField(element)) {
            if (element !== active && !kept.has(element)) element.value = fieldText(draft, control);
        } else if (element instanceof HTMLInputElement) element.checked = isChosen(draft, control);
        else if (control.set === 'edge') {
            const line = draft.edgeLine(oneOf(formatCellsOffer.edges, valueOf(control), 'edge'));
            element.setAttribute('aria-pressed', line === undefined ? 'mixed' : String(!line.isNone));
        }
    }
}

/** Makes the choice the control says, through the draft. Answers whether the draft was asked to change. */
function makeChoice(draft: FormatCellsDraft, control: Control): boolean {
    const { element } = control;
    const checked = element instanceof HTMLInputElement && element.checked;
    const text = element instanceof HTMLInputElement || element instanceof HTMLSelectElement ? element.value : '';
    switch (control.set) {
        case 'category': draft.selectCategory(oneOf<NumberFormatCategory>(formatCellsOffer.categories.map(offer => offer.category), valueOf(control), 'category')); return true;
        case 'places': {
            const value = Number(text);
            // Only a whole number of places Excel takes is set; anything else leaves the field invalid, which OK refuses.
            if (text.trim() === '' || !Number.isInteger(value) || value < 0 || value > 30) return false;
            draft.setDecimalPlaces(value);
            return true;
        }
        case 'separator': draft.setThousandsSeparator(checked); return true;
        case 'negative': draft.setNegativeNumber(Number(valueOf(control))); return true;
        case 'code': draft.setType(text); return true;
        case 'type': draft.setType(valueOf(control)); return true;
        case 'alignment': draft.setAlignment(oneOf<HorizontalAlignment>(formatCellsOffer.alignments, text, 'alignment')); return true;
        case 'fontStyle': draft.setFontStyle(oneOf<FontStyle>(formatCellsOffer.fontStyles, valueOf(control), 'font style')); return true;
        case 'underline': draft.setUnderline(oneOf(['none', 'single'], text, 'underline') === 'single'); return true;
        case 'strikethrough': draft.setStrikethrough(checked); return true;
        // Excel's Normal font (ADR-0071, 2026-10-02): checking it sets every part of the Font to its default; it shows
        // checked while they are, so unchecking it changes nothing.
        case 'normalFont':
            if (!checked) return false;
            draft.setNormalFont();
            return true;
        case 'lineStyle': draft.setLineStyle(oneOf<BorderLineStyle>(formatCellsOffer.lineStyleColumns.flat(), valueOf(control), 'line style')); return true;
        case 'preset': {
            const preset = oneOf<BorderPreset>(formatCellsOffer.presets, valueOf(control), 'preset');
            // Drawn enabled by a render a choice has since overtaken: the draft no longer offers it.
            if (!draft.canApply(preset)) return false;
            draft.applyPreset(preset);
            return true;
        }
        case 'edge': {
            const edge = oneOf<BorderEdge>(formatCellsOffer.edges, valueOf(control), 'edge');
            if (!draft.canSet(edge)) return false;
            draft.toggleEdge(edge);
            return true;
        }
        case 'colour': {
            const { target, colour } = colourOf(control);
            draft.setColour(target, colour);
            return true;
        }
        case 'colourText': draft.setColourText(colourOf(control).target, text); return true;
        default: throw new Error(`Internal: '${String(control.set satisfies never)}' is no choice of Format Cells.`);
    }
}

/** The event a control's choice is heard on: a field's typing, a select's or a box's change, a button's press. */
function heardOn(control: HTMLElement): 'input' | 'change' | 'click' {
    if (control instanceof HTMLSelectElement) return 'change';
    if (control instanceof HTMLInputElement) return control.type === 'radio' || control.type === 'checkbox' ? 'change' : 'input';
    return 'click';
}

/** Wires Format Cells' contents as .NET drew them: the tabs, every choice, OK and Cancel. */
function wireFormatCells(contents: HTMLElement, context: SheetFormatCellsSlotContext, answered: boolean): () => void {
    const opening = openingOf(context);
    const { draft } = context;
    const unwire: (() => void)[] = [];
    const listen = (element: EventTarget, type: string, handler: (event: Event) => void): void => {
        element.addEventListener(type, handler);
        unwire.push(() => { element.removeEventListener(type, handler); });
    };
    const forms = marked(contents, formatCellsAttributes.form);
    const form = forms[0]?.[0];
    if (forms.length !== 1 || !(form instanceof HTMLFormElement)) {
        throw new Error(`Format Cells draws ${String(forms.length)} forms marked ${formatCellsAttributes.form}: draw its tabs in one form, so OK is its submission.`);
    }
    const tabs = new Map<FormatCellsTab, HTMLElement>();
    for (const [control, value] of marked(contents, formatCellsAttributes.tab)) tabs.set(oneOf(formatCellsOffer.tabs, value, 'tab'), control);
    const panels = new Map<FormatCellsTab, HTMLElement>();
    for (const [panel, value] of marked(contents, formatCellsAttributes.panel)) panels.set(oneOf(formatCellsOffer.tabs, value, 'tab'), panel);
    for (const tab of formatCellsOffer.tabs) {
        if (!tabs.has(tab) || !panels.has(tab)) {
            throw new Error(`Format Cells draws no control marked ${formatCellsAttributes.tab}="${tab}" or no panel marked ${formatCellsAttributes.panel}="${tab}": draw every tab, each with its panel.`);
        }
    }
    const controls = marked(contents, formatCellsAttributes.set).map(([element, set]) => controlOf(element, set));
    const changed = (): void => {
        if (draft.refusal === undefined) opening.refused = false;
        syncControls(contents, draft, controls, opening.kept);
        opening.again?.();
    };
    // The tab shown is this opening's, kept here: a render a round trip behind the arrows never shows an older one.
    const show = (tab: FormatCellsTab, focus: boolean): void => {
        opening.tab = tab;
        for (const [each, control] of tabs) {
            control.setAttribute('role', 'tab');
            control.setAttribute('aria-selected', String(each === tab));
            control.tabIndex = each === tab ? 0 : -1;
        }
        for (const [each, panel] of panels) {
            panel.setAttribute('role', 'tabpanel');
            panel.hidden = each !== tab;
        }
        if (focus) tabs.get(tab)?.focus();
        context.tabShown(tab);
    };
    for (const [tab, control] of tabs) {
        listen(control, 'click', () => { show(tab, false); });
        // ARIA's tabs pattern: the arrows move to the previous or next tab, wrapping, and Home and End to the first and
        // the last; the tab reached is shown.
        listen(control, 'keydown', event => {
            if (!(event instanceof KeyboardEvent)) return;
            const all = formatCellsOffer.tabs;
            const at = all.indexOf(opening.tab);
            const to = { ArrowRight: (at + 1) % all.length, ArrowLeft: (at + all.length - 1) % all.length, Home: 0, End: all.length - 1 }[event.key];
            const next = to === undefined ? undefined : all[to];
            if (next === undefined) return;
            event.preventDefault();
            show(next, true);
        });
    }
    for (const control of controls) {
        const { element } = control;
        listen(element, heardOn(element), () => {
            const taken = makeChoice(draft, control);
            if (isField(element)) {
                if (taken) opening.kept.delete(element);
                else opening.kept.add(element);
            }
            if (taken) changed();
            else syncControls(contents, draft, controls, opening.kept);
        });
        // A field left shows the draft's text again, unless it holds text the draft did not take, which stays as typed.
        if (isField(element)) listen(element, 'blur', () => { if (!opening.kept.has(element)) element.value = fieldText(draft, control); });
    }
    // OK, its fields checked first on every tab: one the browser holds invalid is shown on its tab and reported there.
    listen(form, 'submit', event => {
        event.preventDefault();
        const invalid = [...form.querySelectorAll('input, select, textarea')]
            .find((field): field is HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement =>
                (field instanceof HTMLInputElement || field instanceof HTMLSelectElement || field instanceof HTMLTextAreaElement) && !field.checkValidity());
        if (invalid !== undefined) {
            const holding = [...panels].find(([, panel]) => panel.contains(invalid))?.[0];
            if (holding === undefined) throw new Error(`A field of Format Cells the browser holds invalid stands in no panel marked ${formatCellsAttributes.panel}.`);
            show(holding, false);
            invalid.reportValidity();
            return;
        }
        if (context.ok()) return;
        opening.refused = true;
        opening.again?.();
    });
    for (const [cancel] of marked(contents, formatCellsAttributes.cancel)) listen(cancel, 'click', () => { context.cancel(); });
    show(opening.tab, false);
    syncControls(contents, draft, controls, opening.kept);
    // The opening's keyboard goes to the tab shown, once, on the contents drawn from the data last sent.
    if (!answered) {
        const control = tabs.get(opening.tab);
        if (control === undefined) throw new Error('Internal: Format Cells stands with no control for the tab shown.');
        context.takeKeyboard(control);
    }
    return () => { for (const each of unwire) each(); };
}
