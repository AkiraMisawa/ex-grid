// Format Cells under the built-in Chrome (ADR-0071), the counterpart of C#'s FormatCellsPanel: the contents ExSheet
// shows in the grid's popover frame (ADR-0050 item 16). The frame is the grid's — where it stands, its bound,
// Escape, a press elsewhere and the box shrinking — and these contents draw what ExSheet offers (formatCellsOffer),
// set what the user chooses through the draft, and call back on OK and Cancel. Nothing here decides what a choice
// means: that is FormatCellsDraft's, under every Chrome.
//
// The tabs are ARIA's tabs pattern: one tab stop, the arrow keys and Home and End move between the tabs and show
// the one they reach. Each list of choices is a group of native radio buttons, so the arrow keys move inside it as
// the browser moves them. A choice never redraws the control it was made in: every control is built once for the
// tab shown, and after each choice its state is set from the draft, a field the user is typing in excepted, whose
// text outranks the draft's (ADR-0240 section 1). It wears the C# panel's classes, so one stylesheet lays out both.

import type { BorderLine, BorderLineStyle, CellColour } from '@ex-grid/sheet-engine';
import { paintOf } from './cell-colour';
import type { FormatCellsDraft } from './format-cells-draft';
import { formatCellsOffer, isDisabledCategory, type BorderEdge, type ColourTarget, type FormatCellsTab, type PaletteSwatch } from './format-cells-offer';
import { unreachable } from './must';
import type { SheetWords } from './sheet-words';

/** What Format Cells calls back on (ADR-0071). */
export interface FormatCellsPanelCallbacks {
    /** OK, by its button or Enter in a field: whether it was taken. One that was not leaves the draft's refusal shown. */
    readonly ok: () => boolean;
    /** Cancel: sets nothing. */
    readonly cancel: () => void;
    /** A tab was shown, so that the next opening shows it again, as Excel's does. */
    readonly tabShown: (tab: FormatCellsTab) => void;
}

const svgNamespace = 'http://www.w3.org/2000/svg';

// Unique on the page, for the tabs' aria-controls and the radio groups' names: two Sheets each have their own.
let instances = 0;

/**
 * A line style's stroke width and dashes, as Format Cells' Border tab pictures it in an SVG (ADR-0071): only a picture
 * of the style, which a Chrome's Format Cells draws too; how a Border is painted on the Sheet is the grid's. `double`
 * is two strokes of the width given, and `none` is none.
 */
export function formatCellsLineStroke(style: BorderLineStyle): { readonly width: string; readonly dashes: string | undefined } {
    switch (style) {
        case 'hair': return { width: '1', dashes: '1 1' };
        case 'dotted': return { width: '1', dashes: '1 2' };
        case 'dashDotDot': return { width: '1', dashes: '6 2 1 2 1 2' };
        case 'dashDot': return { width: '1', dashes: '6 2 1 2' };
        case 'dashed': return { width: '1', dashes: '4 2' };
        case 'mediumDashDotDot': return { width: '2', dashes: '6 2 2 2 2 2' };
        case 'slantedDashDot': return { width: '2', dashes: '8 2 4 2' };
        case 'mediumDashDot': return { width: '2', dashes: '8 2 2 2' };
        case 'mediumDashed': return { width: '2', dashes: '6 3' };
        case 'medium': return { width: '2', dashes: undefined };
        case 'thick': return { width: '3', dashes: undefined };
        case 'none': case 'thin': case 'double': return { width: '1', dashes: undefined };
        default: return unreachable(style);
    }
}

// An edge shows its line; one whose cells' sides differ reads mixed, as Excel reads its button.
function pressedOf(line: BorderLine | undefined): string {
    if (line === undefined) return 'mixed';
    return line.isNone ? 'false' : 'true';
}

/** Format Cells' contents under the built-in Chrome, over a draft (ADR-0071). */
export class FormatCellsPanel {
    /** The form the grid's popover frame holds. */
    readonly element: HTMLFormElement;
    readonly #doc: Document;
    readonly #draft: FormatCellsDraft;
    readonly #words: SheetWords;
    readonly #callbacks: FormatCellsPanelCallbacks;
    readonly #id: string;
    readonly #tabs = new Map<FormatCellsTab, HTMLButtonElement>();
    readonly #panel: HTMLElement;
    readonly #refusal: HTMLElement;
    #tab: FormatCellsTab;
    // What sets each control of the tab shown from the draft, run after every choice.
    #syncs: (() => void)[] = [];

    constructor(doc: Document, draft: FormatCellsDraft, words: SheetWords, tab: FormatCellsTab, callbacks: FormatCellsPanelCallbacks) {
        this.#doc = doc;
        this.#draft = draft;
        this.#words = words;
        this.#callbacks = callbacks;
        this.#tab = tab;
        instances++;
        this.#id = `ex-format-cells-${String(instances)}`;
        this.element = doc.createElement('form');
        this.element.className = 'ex-format-cells';
        this.element.addEventListener('submit', event => {
            event.preventDefault();
            this.#ok();
        });
        const tabList = this.#el('div', 'ex-format-cells-tabs');
        tabList.setAttribute('role', 'tablist');
        tabList.setAttribute('aria-label', words.formatCellsLabel('tabs'));
        for (const shown of formatCellsOffer.tabs) {
            const button = this.#button('ex-format-cells-tab', words.tabName(shown), () => { this.#show(shown, false); });
            button.id = this.#tabId(shown);
            button.setAttribute('role', 'tab');
            button.setAttribute('aria-controls', this.#panelId);
            button.addEventListener('keydown', this.#onTabKey);
            this.#tabs.set(shown, button);
            tabList.append(button);
        }
        this.#panel = this.#el('div', 'ex-format-cells-panel');
        this.#panel.id = this.#panelId;
        this.#panel.setAttribute('role', 'tabpanel');
        this.#refusal = this.#el('div', 'ex-format-cells-refusal');
        this.#refusal.setAttribute('role', 'alert');
        this.#refusal.hidden = true;
        const ok = this.#el('button', 'ex-format-cells-ok ex-popover-closes', words.formatCellsLabel('ok'));
        ok.setAttribute('type', 'submit');
        const cancel = this.#button('ex-format-cells-cancel ex-popover-closes', words.formatCellsLabel('cancel'), () => { callbacks.cancel(); });
        this.element.append(tabList, this.#panel, this.#refusal, this.#el('div', 'ex-popover-actions ex-format-cells-actions', ok, cancel));
        this.#show(tab, false);
    }

    get #panelId(): string {
        return `${this.#id}-panel`;
    }

    #tabId(tab: FormatCellsTab): string {
        return `${this.#id}-tab-${tab}`;
    }

    // Shows a tab, its controls built afresh from the draft; `focus` moves the keyboard onto its tab, as the arrows do.
    #show(tab: FormatCellsTab, focus: boolean): void {
        this.#tab = tab;
        for (const [each, button] of this.#tabs) {
            button.setAttribute('aria-selected', each === tab ? 'true' : 'false');
            button.tabIndex = each === tab ? 0 : -1;
        }
        this.#panel.setAttribute('aria-labelledby', this.#tabId(tab));
        this.#syncs = [];
        this.#panel.replaceChildren(this.#tabContents(tab));
        this.#changed();
        if (focus) this.#tabs.get(tab)?.focus();
        this.#callbacks.tabShown(tab);
    }

    #tabContents(tab: FormatCellsTab): HTMLElement {
        switch (tab) {
            case 'number': return this.#numberTab();
            case 'alignment': return this.#alignmentTab();
            case 'font': return this.#fontTab();
            case 'border': return this.#borderTab();
            case 'fill': return this.#fillTab();
            default: return unreachable(tab);
        }
    }

    // ARIA's tabs pattern: the arrows move to the previous or next tab, wrapping, and Home and End to the first and
    // the last; the tab reached is shown.
    readonly #onTabKey = (event: KeyboardEvent): void => {
        const tabs = formatCellsOffer.tabs;
        const at = tabs.indexOf(this.#tab);
        const to = { ArrowRight: (at + 1) % tabs.length, ArrowLeft: (at + tabs.length - 1) % tabs.length, Home: 0, End: tabs.length - 1 }[event.key];
        const next = to === undefined ? undefined : tabs[to];
        if (next === undefined) return;
        event.preventDefault();
        this.#show(next, true);
    };

    // A choice was made: every control of the tab shown takes the draft's state, and a refusal OK showed goes once
    // its cause has.
    #changed(): void {
        for (const sync of this.#syncs) sync();
        if (this.#draft.refusal === undefined) this.#showRefusal(undefined);
    }

    // OK, its fields checked first: one the browser holds invalid, a number of places out of range, is reported where
    // it stands, and nothing is set.
    #ok(): void {
        if (!this.element.checkValidity()) {
            this.element.reportValidity();
            return;
        }
        if (this.#callbacks.ok()) return;
        const refusal = this.#draft.refusal;
        this.#showRefusal(refusal === undefined ? undefined : this.#words.formatCellsRefusal(refusal));
    }

    #showRefusal(text: string | undefined): void {
        this.#refusal.textContent = text === undefined ? '' : text;
        this.#refusal.hidden = text === undefined;
    }

    // ---- Number ----

    #numberTab(): HTMLElement {
        const words = this.#words;
        const draft = this.#draft;
        const categories = this.#fieldset('ex-format-cells-categories', words.formatCellsLabel('category'));
        for (const offer of formatCellsOffer.categories) {
            const { category } = offer;
            const radio = this.#radio(`${this.#id}-category`, () => draft.category === category, () => { draft.selectCategory(category); });
            radio.disabled = !offer.enabled;
            categories.append(this.#choice(radio, words.categoryName(category)));
            if (isDisabledCategory(category)) {
                const reason = this.#el('span', 'ex-format-cells-reason', words.categoryDisabled(category));
                reason.id = `${this.#id}-reason-${category}`;
                radio.setAttribute('aria-describedby', reason.id);
                categories.append(reason);
            }
        }
        const options = this.#el('div', 'ex-format-cells-options');
        // The options are the category's: built afresh when the category changes, and set from the draft otherwise.
        let builtFor = draft.category;
        let optionSyncs: (() => void)[] = [];
        const build = (): void => {
            const outer = this.#syncs;
            this.#syncs = [];
            options.replaceChildren(...this.#numberOptions());
            optionSyncs = this.#syncs;
            this.#syncs = outer;
            builtFor = draft.category;
        };
        build();
        this.#syncs.push(() => {
            if (builtFor !== draft.category) build();
            for (const sync of optionSyncs) sync();
        });
        return this.#el('div', 'ex-format-cells-columns', categories, options);
    }

    #numberOptions(): HTMLElement[] {
        const words = this.#words;
        const draft = this.#draft;
        const { category } = draft;
        const shown: HTMLElement[] = [];
        if (category === 'general') shown.push(this.#el('p', '', words.formatCellsLabel('generalHint')));
        else if (category === 'text') shown.push(this.#el('p', '', words.formatCellsLabel('textHint')));
        if (draft.takesDecimalPlaces) {
            const places = this.#field('number', 'ex-format-cells-places', () => String(draft.decimalPlaces), typed => {
                const value = Number(typed);
                // Only a whole number of places Excel takes is set; anything else leaves the field invalid, which OK
                // refuses rather than set the places the field no longer shows.
                if (typed.trim() !== '' && Number.isInteger(value) && value >= 0 && value <= 30) draft.setDecimalPlaces(value);
            });
            places.min = '0';
            places.max = '30';
            places.required = true;
            shown.push(this.#labelled(words.formatCellsLabel('decimalPlaces'), places));
        }
        if (category === 'number') {
            shown.push(this.#choice(this.#checkbox('ex-format-cells-separator', () => draft.thousandsSeparator,
                checked => { draft.setThousandsSeparator(checked); }), words.formatCellsLabel('thousandsSeparator')));
        }
        if (category === 'currency') shown.push(this.#el('p', 'ex-format-cells-symbol', words.currencySymbolShown(draft.currencySymbol)));
        if (draft.negativeNumbers.length > 0) {
            shown.push(this.#codeList('ex-format-cells-negatives', words.formatCellsLabel('negativeNumbers'), `${this.#id}-negative`,
                () => draft.negativeNumbers, index => draft.negativeNumber === index, index => { draft.setNegativeNumber(index); }));
        }
        if (category === 'custom') {
            const code = this.#field('text', 'ex-format-cells-code', () => draft.typeCode, typed => { draft.setType(typed); });
            const refusal = this.#el('span', 'ex-format-cells-refusal');
            refusal.id = `${this.#id}-code-refusal`;
            code.setAttribute('aria-describedby', refusal.id);
            this.#syncs.push(() => {
                const why = draft.numberFormatRefusal;
                refusal.textContent = why === undefined ? '' : words.formatCellsRefusal(why);
            });
            shown.push(this.#labelled(words.formatCellsLabel('type'), code), refusal);
        }
        if (draft.types.length > 0) {
            shown.push(this.#codeList('ex-format-cells-types', words.formatCellsLabel(category === 'custom' ? 'codes' : 'type'), `${this.#id}-type`,
                () => draft.types, (_, code) => draft.typeCode === code, (_, code) => { draft.setType(code); }));
        }
        return shown;
    }

    // A list of codes to choose one of, its codes the draft's at each choice: a list of another length is built
    // afresh, and one of the same length has its codes and its choice set in place.
    #codeList(className: string, legend: string, name: string, codes: () => readonly string[],
        chosen: (index: number, code: string) => boolean, choose: (index: number, code: string) => void): HTMLElement {
        const fieldset = this.#fieldset(className, legend);
        let shown: { readonly radio: HTMLInputElement; readonly code: HTMLElement }[] = [];
        this.#syncs.push(() => {
            const now = codes();
            if (now.length !== shown.length) {
                for (const { radio } of shown) radio.closest('label')?.remove();
                shown = now.map((_, index) => {
                    const radio = this.#input('radio');
                    radio.name = name;
                    radio.addEventListener('change', () => {
                        const code = codes()[index];
                        if (code !== undefined) choose(index, code);
                        this.#changed();
                    });
                    const code = this.#el('code', '');
                    fieldset.append(this.#choice(radio, code));
                    return { radio, code };
                });
            }
            shown.forEach(({ radio, code }, index) => {
                const text = now[index];
                if (text === undefined) return;
                code.textContent = text;
                radio.checked = chosen(index, text);
            });
        });
        return fieldset;
    }

    // ---- Alignment ----

    #alignmentTab(): HTMLElement {
        const draft = this.#draft;
        const select = this.#doc.createElement('select');
        select.className = 'ex-format-cells-alignment';
        for (const alignment of formatCellsOffer.alignments) {
            const option = this.#el('option', '', this.#words.alignmentName(alignment));
            option.setAttribute('value', alignment);
            select.append(option);
        }
        select.addEventListener('change', () => {
            const alignment = formatCellsOffer.alignments.find(each => each === select.value);
            if (alignment !== undefined) draft.setAlignment(alignment);
            this.#changed();
        });
        this.#syncs.push(() => { select.value = draft.alignment; });
        return this.#labelled(this.#words.formatCellsLabel('horizontal'), select);
    }

    // ---- Font ----

    #fontTab(): HTMLElement {
        const words = this.#words;
        const draft = this.#draft;
        const styles = this.#fieldset('ex-format-cells-font-styles', words.formatCellsLabel('fontStyle'));
        for (const style of formatCellsOffer.fontStyles) {
            styles.append(this.#choice(this.#radio(`${this.#id}-font-style`, () => draft.fontStyle === style, () => { draft.setFontStyle(style); }),
                words.fontStyleName(style)));
        }
        const underline = this.#doc.createElement('select');
        underline.className = 'ex-format-cells-underline';
        for (const [value, label] of [['none', words.formatCellsLabel('underlineNone')], ['single', words.formatCellsLabel('underlineSingle')]] as const) {
            const option = this.#el('option', '', label);
            option.setAttribute('value', value);
            underline.append(option);
        }
        underline.addEventListener('change', () => {
            draft.setUnderline(underline.value === 'single');
            this.#changed();
        });
        this.#syncs.push(() => { underline.value = draft.underline ? 'single' : 'none'; });
        const strikethrough = this.#checkbox('ex-format-cells-strikethrough', () => draft.strikethrough, checked => { draft.setStrikethrough(checked); });
        // Excel's Normal font (ADR-0071, 2026-10-02): checking it sets every part of the Font to its default. It shows
        // checked while they are, so unchecking it changes nothing, and the click that would only clear the box is
        // not let through.
        const normal = this.#checkbox('ex-format-cells-normal-font', () => draft.isNormalFont, checked => { if (checked) draft.setNormalFont(); });
        normal.addEventListener('click', event => { if (draft.isNormalFont) event.preventDefault(); });
        const options = this.#el('div', 'ex-format-cells-options',
            this.#labelled(words.formatCellsLabel('underline'), underline),
            this.#choice(strikethrough, words.formatCellsLabel('strikethrough')),
            this.#choice(normal, words.formatCellsLabel('normalFont')));
        const colour = this.#fieldset('ex-format-cells-colour', words.formatCellsLabel('colour'), this.#palette('font', () => draft.colourOf('font')));
        return this.#el('div', '', this.#el('div', 'ex-format-cells-columns', styles, options), colour);
    }

    // ---- Border ----

    #borderTab(): HTMLElement {
        const words = this.#words;
        const draft = this.#draft;
        const styles = this.#fieldset('ex-format-cells-line-styles', words.formatCellsLabel('lineStyle'));
        const columns = this.#el('div', 'ex-format-cells-columns');
        for (const column of formatCellsOffer.lineStyleColumns) {
            const shown = this.#el('div', 'ex-format-cells-line-column');
            for (const style of column) {
                const name = words.lineStyleName(style);
                const radio = this.#radio(`${this.#id}-line-style`, () => draft.lineStyle === style, () => { draft.setLineStyle(style); });
                radio.setAttribute('aria-label', name);
                const choice = this.#choice(radio, this.#lineSample(style));
                choice.title = name;
                shown.append(choice);
            }
            columns.append(shown);
        }
        styles.append(columns);
        const presets = this.#el('div', 'ex-format-cells-presets');
        presets.setAttribute('role', 'group');
        presets.setAttribute('aria-label', words.formatCellsLabel('presets'));
        for (const preset of formatCellsOffer.presets) {
            const button = this.#button('ex-format-cells-preset', words.presetName(preset), () => { draft.applyPreset(preset); this.#changed(); });
            this.#syncs.push(() => { button.disabled = !draft.canApply(preset); });
            presets.append(button);
        }
        const edges = this.#el('div', 'ex-format-cells-edges');
        edges.setAttribute('role', 'group');
        edges.setAttribute('aria-label', words.formatCellsLabel('border'));
        for (const edge of formatCellsOffer.edges) {
            const button = this.#button('ex-format-cells-edge', words.edgeName(edge), () => { draft.toggleEdge(edge); this.#changed(); });
            button.dataset['edge'] = edge;
            this.#syncs.push(() => {
                button.disabled = !draft.canSet(edge);
                button.setAttribute('aria-pressed', pressedOf(draft.edgeLine(edge)));
            });
            edges.append(button);
        }
        const preview = this.#doc.createElementNS(svgNamespace, 'svg');
        preview.setAttribute('class', 'ex-format-cells-preview');
        for (const [name, value] of [['width', '120'], ['height', '72'], ['viewBox', '0 0 120 72'], ['role', 'img']] as const) preview.setAttribute(name, value);
        preview.setAttribute('aria-label', words.formatCellsLabel('preview'));
        this.#syncs.push(() => { this.#drawPreview(preview); });
        const colour = this.#fieldset('ex-format-cells-colour', words.formatCellsLabel('colour'), this.#palette('border', () => draft.colourOf('border')));
        return this.#el('div', '', this.#el('div', 'ex-format-cells-columns', styles, this.#el('div', 'ex-format-cells-options', presets, edges, preview)), colour);
    }

    // The preview: the range's edges as they will be — outer ones round the box, inner ones across it where a range
    // has them — and an edge whose cells' sides differ as a grey dotted line, as Excel draws it (the eleventh Windows
    // run, case 24).
    #drawPreview(preview: SVGElement): void {
        const draft = this.#draft;
        const edges: [BorderEdge, number, number, number, number][] = [
            ['top', 4, 4, 116, 4], ['bottom', 4, 68, 116, 68], ['left', 4, 4, 4, 68], ['right', 116, 4, 116, 68],
        ];
        if (draft.hasInsideHorizontal) edges.push(['insideHorizontal', 4, 36, 116, 36]);
        if (draft.hasInsideVertical) edges.push(['insideVertical', 60, 4, 60, 68]);
        const lines: SVGElement[] = [];
        for (const [edge, x1, y1, x2, y2] of edges) {
            const line = draft.edgeLine(edge);
            if (line?.isNone === true) continue;
            const drawn = this.#svgLine(x1, y1, x2, y2);
            drawn.dataset['edge'] = edge;
            if (line === undefined) {
                drawn.setAttribute('class', 'ex-format-cells-differs');
                for (const [name, value] of [['stroke', '#808080'], ['stroke-width', '1'], ['stroke-dasharray', '1 2']] as const) drawn.setAttribute(name, value);
            } else {
                const { width, dashes } = formatCellsLineStroke(line.style);
                drawn.setAttribute('stroke', paintOf(line.colour));
                drawn.setAttribute('stroke-width', width);
                if (dashes !== undefined) drawn.setAttribute('stroke-dasharray', dashes);
            }
            lines.push(drawn);
        }
        preview.replaceChildren(...lines);
    }

    // A line style's sample, as the Border tab draws it: a stroke and its dashes.
    #lineSample(style: BorderLineStyle): SVGElement {
        const svg = this.#doc.createElementNS(svgNamespace, 'svg');
        svg.setAttribute('class', 'ex-format-cells-line');
        svg.setAttribute('width', '56');
        svg.setAttribute('height', '8');
        svg.setAttribute('aria-hidden', 'true');
        if (style === 'double') {
            svg.append(this.#svgLine(0, 2.5, 56, 2.5), this.#svgLine(0, 5.5, 56, 5.5));
        } else if (style !== 'none') {
            const { width, dashes } = formatCellsLineStroke(style);
            const line = this.#svgLine(0, 4, 56, 4);
            line.setAttribute('stroke-width', width);
            if (dashes !== undefined) line.setAttribute('stroke-dasharray', dashes);
            svg.append(line);
        }
        return svg;
    }

    #svgLine(x1: number, y1: number, x2: number, y2: number): SVGElement {
        const line = this.#doc.createElementNS(svgNamespace, 'line');
        for (const [name, value] of [['x1', x1], ['y1', y1], ['x2', x2], ['y2', y2]] as const) line.setAttribute(name, String(value));
        line.setAttribute('stroke', 'currentColor');
        line.setAttribute('stroke-width', '1');
        return line;
    }

    // ---- Fill ----

    #fillTab(): HTMLElement {
        const draft = this.#draft;
        return this.#fieldset('ex-format-cells-colour', this.#words.formatCellsLabel('backgroundColour'), this.#palette('fill', () => draft.colourOf('fill')));
    }

    // ---- The palette ----

    // Case 23's palette as one group of radio buttons: Automatic (the Font and the Border) or No Colour (the Fill)
    // first, then the theme's sixty and the ten standard colours, each named as Excel names it, and More Colours,
    // which takes a hex value. `chosen` is `undefined` for No Colour.
    #palette(target: ColourTarget, chosen: () => CellColour | undefined): HTMLElement {
        const words = this.#words;
        const name = `${this.#id}-palette-${target}`;
        const isFill = target === 'fill';
        const palette = this.#el('div', 'ex-format-cells-palette');
        palette.setAttribute('role', 'radiogroup');
        palette.setAttribute('aria-label', words.formatCellsLabel(isFill ? 'backgroundColour' : 'colour'));
        const first = this.#radio(name, () => (isFill ? chosen() === undefined : chosen()?.isAutomatic === true), () => { this.#choose(target, undefined); });
        const firstChoice = this.#choice(first, words.formatCellsLabel(isFill ? 'noColour' : 'automatic'));
        firstChoice.classList.add('ex-format-cells-first-colour');
        const swatches = (row: readonly PaletteSwatch[]): HTMLElement => this.#el('div', 'ex-format-cells-swatches', ...row.map(swatch => {
            const radio = this.#radio(name, () => {
                const colour = chosen();
                return colour !== undefined && !colour.isAutomatic && colour.equals(swatch.colour);
            }, () => { this.#choose(target, swatch.colour); });
            radio.classList.add('ex-format-cells-swatch');
            radio.style.backgroundColor = paintOf(swatch.colour);
            const swatchName = words.swatchName(swatch);
            radio.setAttribute('aria-label', swatchName);
            radio.title = swatchName;
            radio.dataset['rgb'] = paintOf(swatch.colour);
            return radio;
        }));
        const hex = this.#field('text', 'ex-format-cells-hex', () => this.#draft.colourText(target), typed => { this.#draft.setColourText(target, typed); });
        hex.dataset['target'] = target;
        hex.placeholder = '#RRGGBB';
        hex.maxLength = 7;
        palette.append(firstChoice, swatches(formatCellsOffer.themeColours.flat()), swatches(formatCellsOffer.standardColours),
            this.#labelled(words.formatCellsLabel('moreColours'), hex));
        return palette;
    }

    // A colour chosen for a target: `undefined` is Automatic for the Font and the Border, and No Colour for the Fill.
    #choose(target: ColourTarget, colour: CellColour | undefined): void {
        this.#draft.setColour(target, colour);
    }

    // ---- Controls ----

    #el<K extends keyof HTMLElementTagNameMap>(tag: K, className: string, ...children: (Node | string)[]): HTMLElementTagNameMap[K] {
        const element = this.#doc.createElement(tag);
        if (className !== '') element.className = className;
        element.append(...children);
        return element;
    }

    #input(type: string): HTMLInputElement {
        const input = this.#doc.createElement('input');
        input.type = type;
        return input;
    }

    #button(className: string, text: string, press: () => void): HTMLButtonElement {
        const button = this.#el('button', className, text);
        button.type = 'button';
        button.addEventListener('click', press);
        return button;
    }

    #fieldset(className: string, legend: string, ...children: Node[]): HTMLFieldSetElement {
        return this.#el('fieldset', className, this.#el('legend', '', legend), ...children);
    }

    #choice(input: HTMLInputElement, ...label: (Node | string)[]): HTMLLabelElement {
        return this.#el('label', 'ex-format-cells-choice', input, ...label);
    }

    #labelled(label: string, control: HTMLElement): HTMLLabelElement {
        return this.#el('label', 'ex-format-cells-field', label, control);
    }

    // A radio button of a group, checked while `checked` holds.
    #radio(name: string, checked: () => boolean, choose: () => void): HTMLInputElement {
        const radio = this.#input('radio');
        radio.name = name;
        radio.addEventListener('change', () => {
            choose();
            this.#changed();
        });
        this.#syncs.push(() => { radio.checked = checked(); });
        return radio;
    }

    #checkbox(className: string, checked: () => boolean, set: (checked: boolean) => void): HTMLInputElement {
        const box = this.#input('checkbox');
        box.className = className;
        box.addEventListener('change', () => {
            set(box.checked);
            this.#changed();
        });
        this.#syncs.push(() => { box.checked = checked(); });
        return box;
    }

    // A field typed into: each keystroke is handed to the draft, and the field shows the draft's text only while the
    // user is not typing in it, so a choice never writes over what is being typed (ADR-0240 section 1).
    #field(type: 'text' | 'number', className: string, value: () => string, typed: (text: string) => void): HTMLInputElement {
        const field = this.#input(type);
        field.className = className;
        field.spellcheck = false;
        field.addEventListener('input', () => {
            typed(field.value);
            this.#changed();
        });
        this.#syncs.push(() => { if (this.#doc.activeElement !== field) field.value = value(); });
        // Left, it shows the draft's text again, unless the draft did not take what it holds: that stays as typed, so OK
        // refuses it rather than set what the field no longer shows.
        field.addEventListener('blur', () => { if (field.checkValidity()) field.value = value(); });
        return field;
    }
}
