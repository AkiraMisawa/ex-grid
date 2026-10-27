// The Formula Bar and its Name Box (ADR-0051): a band inside the grid's root, above the header, where the Focus
// cell's text is read without editing it, and the Cell Editor's second surface: one uncommitted text, shown in
// two places, committed once from either. The Name Box names the Focus in the Consumer's words, and what is typed
// into it is handed to the Consumer, which resolves it and places the Selection (ADR-0050, item 4). Neither field
// is a tab stop: the grid is one (ADR-0020). The fields wear the C# core's classes, so one stylesheet lays out
// both cores' bars.

import { GridMetrics, type GridDensity } from '../grid-metrics';
import type { CellPosition } from '../selection/cell';
import {
    BarField, ViewListeners, type EditorField, type FormulaBarTextView, type GridChromeSlot, type GridFormulaBarTextSlotContext, type GridNameBoxSlotContext,
} from './grid-chrome';
import type { GridColumn } from './grid-column';

/** The Formula Bar's declarations of a grid's options. */
export interface FormulaBarOptions<Row> {
    /** Shows the Formula Bar (ADR-0051): a band above the header with the Name Box and the Focus cell's text,
     * which is also where an edit can be typed. The band's height comes off `viewportHeight`. Off unless given. */
    readonly showFormulaBar?: boolean;
    /** The Name Box's words for the Focus (ADR-0051): `D200` on a Sheet. The box is empty unless given. */
    readonly nameBoxLabel?: (cell: CellPosition) => string;
    /** Hears what was typed into the Name Box when Enter entered it (ADR-0051). The Consumer resolves it, and
     * places the Selection with the handle's `placeSelection` (ADR-0050, item 4). */
    readonly onNameBoxEntered?: (typed: string) => void;
    /** The text the bar shows dimmed, and read only, in place of a cell's own, where it answers any: on a Sheet,
     * the Formula of the array a cell is spilled into (ADR-0125). Hold it in a field: its identity is the change
     * signal. */
    readonly formulaBarBorrowedText?: (row: Row, column: GridColumn<Row>) => string | undefined;
}

/** The Formula Bar's height in pixels for a grid of `density` and `rowHeight` (ADR-0051, ADR-0028): the header's,
 * for a Consumer that draws a band of its own beside the grid at the same height, as a Sheet's tabs are. */
export function formulaBarHeight(density: GridDensity, rowHeight?: number): number {
    return GridMetrics.resolve({ density, ...(rowHeight === undefined ? {} : { rowHeightPx: rowHeight }) }).formulaBarHeightPx;
}

/** The Formula Bar's declarations, taken and checked. */
export interface FormulaBarDeclarations<Row> {
    readonly shown: boolean;
    readonly nameBoxLabel: ((cell: CellPosition) => string) | undefined;
    readonly onNameBoxEntered: ((typed: string) => void) | undefined;
    readonly borrowedText: ((row: Row, column: GridColumn<Row>) => string | undefined) | undefined;
}

/** The declarations in `options`, checked as an untyped caller may hand them (CONVENTIONS.md). */
export function formulaBarDeclarations<Row>(options: FormulaBarOptions<Row>): FormulaBarDeclarations<Row> {
    const { showFormulaBar, nameBoxLabel, onNameBoxEntered, formulaBarBorrowedText }: {
        readonly showFormulaBar?: unknown; readonly nameBoxLabel?: unknown; readonly onNameBoxEntered?: unknown;
        readonly formulaBarBorrowedText?: unknown;
    } = options;
    if (showFormulaBar !== undefined && typeof showFormulaBar !== 'boolean') throw new TypeError('showFormulaBar is true or false.');
    for (const [name, value] of Object.entries({ nameBoxLabel, onNameBoxEntered, formulaBarBorrowedText })) {
        if (value !== undefined && typeof value !== 'function') throw new TypeError(`${name} is a function (ADR-0051).`);
    }
    return {
        shown: showFormulaBar === true,
        nameBoxLabel: options.nameBoxLabel,
        onNameBoxEntered: options.onNameBoxEntered,
        borrowedText: options.formulaBarBorrowedText,
    };
}

/** What the grid tells the bar it hears. */
export interface FormulaBarHooks {
    /** The text field was typed in. */
    readonly textTyped: (text: string) => void;
    /** The text field took the keyboard. */
    readonly textFocused: () => void;
    /** Enter was pressed in the Name Box. */
    readonly nameBoxEntered: (typed: string) => void;
    /** Escape was pressed in the Name Box, or Enter with nothing typed. */
    readonly nameBoxLeft: () => void;
    /** The Name Box took the keyboard. */
    readonly nameBoxFocused: () => void;
}

/** What one pass shows in the bar. */
export interface FormulaBarPaint {
    readonly heightPx: number;
    /** The band's width in pixels, or `undefined` to stretch with the root. */
    readonly widthPx: number | undefined;
    readonly nameBoxWidthPx: number;
    readonly nameBox: string;
    readonly text: string;
    readonly borrowed: boolean;
    readonly editable: boolean;
    readonly nameBoxLabel: string;
    readonly textLabel: string;
}

/** The Chrome slots the bar's two fields may be drawn by (ADR-0390). */
export interface FormulaBarSlots {
    readonly text: GridChromeSlot<GridFormulaBarTextSlotContext> | undefined;
    readonly nameBox: GridChromeSlot<GridNameBoxSlotContext> | undefined;
}

/** The band's elements and the listeners on them. Each field is the built-in input or one a Chrome slot draws in the
 * core's box (ADR-0390): the listeners are on the band, and hear whichever field stands. */
export class FormulaBar {
    readonly band: HTMLElement;
    readonly #doc: Document;
    #text: BarField<GridFormulaBarTextSlotContext>;
    #nameBox: BarField<GridNameBoxSlotContext>;
    readonly #form: HTMLFormElement;
    readonly #listeners = new ViewListeners();
    // What the user has typed into the Name Box and not entered: shown in place of the label while it holds the
    // keyboard.
    #typed: string | undefined;
    // The label the last pass showed, written back when the box is left with text typed and not entered.
    #label = '';
    // A press into the Name Box selects its text on focus, and the press's own mouseup would put a caret back.
    #pressSelecting = false;
    // What the last pass showed, which a field handed over anew is made to show.
    #paint: FormulaBarPaint | undefined;
    // The text field's text, as the bar last showed it or the user typed it.
    #shownText = '';
    #view: FormulaBarTextView = Object.freeze({ readOnly: true, borrowed: false });

    constructor(doc: Document, hooks: FormulaBarHooks, slots: FormulaBarSlots) {
        this.#doc = doc;
        this.band = doc.createElement('div');
        this.band.className = 'ex-formula-bar';
        this.band.setAttribute('role', 'presentation');
        // The Name Box's Enter is its form's implicit submission, as the filter panel's is: no key handler, and an
        // IME's confirming Enter is left alone (ADR-0051). The form's default button is its first: the core's, which
        // keeps Enter the form's whatever a Chrome's contents draw in the box after it.
        this.#form = doc.createElement('form');
        this.#form.className = 'ex-name-box-form';
        const submit = doc.createElement('button');
        submit.type = 'submit';
        submit.hidden = true;
        submit.tabIndex = -1;
        submit.setAttribute('aria-hidden', 'true');
        this.#nameBox = this.#makeNameBox(slots.nameBox);
        this.#text = this.#makeText(slots.text);
        this.#form.append(submit, this.#nameBox.box);
        this.band.append(this.#form, this.#text.box);
        this.#form.addEventListener('submit', event => {
            event.preventDefault();
            // Only Enter's implicit submission, by the default button or with none named (a script's requestSubmit(),
            // which some DOMs name the form for): a button of the contents' that submits is no Enter in the box.
            const by: unknown = Reflect.get(event, 'submitter');
            if (by instanceof HTMLElement && by !== submit && by !== this.#form) return;
            const typed = this.#typed;
            this.#typed = undefined;
            // Enter on the label shown, nothing typed, names nothing new: the keyboard goes back, as Escape's does.
            if (typed === undefined) hooks.nameBoxLeft();
            else hooks.nameBoxEntered(typed);
        });
        this.#form.addEventListener('input', event => {
            const typed = this.#nameBox.field();
            if (typed !== undefined && this.#nameBox.is(event.target)) this.#typed = typed.value;
        });
        // A press into the Name Box selects its text, so what is typed replaces the address shown (ADR-0051).
        this.#form.addEventListener('mousedown', event => {
            if (this.#nameBox.is(event.target)) this.#pressSelecting = doc.activeElement !== event.target;
        });
        this.#form.addEventListener('mouseup', event => {
            if (!this.#pressSelecting || !this.#nameBox.is(event.target)) return;
            this.#pressSelecting = false;
            event.preventDefault();
        });
        this.#form.addEventListener('focusin', event => {
            const taken = this.#nameBox.field();
            if (taken === undefined || !this.#nameBox.is(event.target)) return;
            taken.select();
            hooks.nameBoxFocused();
        });
        // Left without Enter: what was typed goes, and the box names the Focus again.
        this.#form.addEventListener('focusout', event => {
            const left = this.#nameBox.field();
            if (left === undefined || !this.#nameBox.is(event.target)) return;
            this.#typed = undefined;
            this.#pressSelecting = false;
            if (left.value !== this.#label) left.value = this.#label;
        });
        this.#form.addEventListener('keydown', event => {
            if (event.key !== 'Escape' || event.isComposing || !this.#nameBox.is(event.target)) return;
            event.preventDefault();
            this.#typed = undefined;
            hooks.nameBoxLeft();
        });
        this.band.addEventListener('input', event => {
            const typed = this.#text.field();
            if (typed === undefined || !this.#text.is(event.target)) return;
            this.#shownText = typed.value;
            hooks.textTyped(typed.value);
        });
        this.band.addEventListener('focusin', event => {
            if (this.#text.is(event.target)) hooks.textFocused();
        });
    }

    /** The text field, while it stands: the built-in input, or the one the slot's contents handed over. */
    get text(): EditorField | undefined {
        return this.#text.field();
    }

    /** The Name Box's field, while it stands. */
    get nameBox(): EditorField | undefined {
        return this.#nameBox.field();
    }

    /** Whether the text field takes typing, as the last pass showed. */
    get editable(): boolean {
        return !this.#view.readOnly;
    }

    /** Whether the Name Box holds text the user typed and has not entered. */
    get nameBoxTyped(): boolean {
        return this.#typed !== undefined;
    }

    /**
     * Draws each field by the slot `slots` names, where another mount function than the one it stands by is named: the
     * field is made again in its place, its contents taken away, unless `keeps` says the field is in use, which holds
     * the contents until a pass where it is not (ADR-0390).
     */
    adopt(slots: FormulaBarSlots, keeps: (box: HTMLElement) => boolean): void {
        if (slots.nameBox !== this.#nameBox.mount && !keeps(this.#nameBox.box)) {
            const old = this.#nameBox;
            this.#nameBox = this.#makeNameBox(slots.nameBox);
            this.#typed = undefined;
            old.box.replaceWith(this.#nameBox.box);
            old.dispose();
        }
        if (slots.text !== this.#text.mount && !keeps(this.#text.box)) {
            const old = this.#text;
            this.#text = this.#makeText(slots.text);
            old.box.replaceWith(this.#text.box);
            old.dispose();
        }
    }

    /** Shows `text` in the text field, as the editor's, between passes. */
    showText(text: string): void {
        this.#shownText = text;
        const shown = this.#text.field();
        if (shown !== undefined && shown.value !== text) shown.value = text;
    }

    paint(paint: FormulaBarPaint): void {
        this.#paint = paint;
        setStyle(this.band, paint.widthPx === undefined ? `height: ${paint.heightPx}px; right: 0` : `height: ${paint.heightPx}px; width: ${paint.widthPx}px`);
        setStyle(this.#nameBox.box, `width: ${paint.nameBoxWidthPx}px`);
        this.#label = paint.nameBox;
        const name = this.#nameBox.field();
        if (name !== undefined) this.#showName(name);
        this.#shownText = paint.text;
        this.#text.box.classList.toggle('ex-formula-bar-borrowed', paint.borrowed);
        const view = this.#view;
        if (view.readOnly === paint.editable || view.borrowed !== paint.borrowed) {
            this.#view = Object.freeze({ readOnly: !paint.editable, borrowed: paint.borrowed });
            this.#listeners.tell();
        }
        const text = this.#text.field();
        if (text !== undefined) this.#showText(text);
        // Mounted once the bar has painted: contents that cannot be drawn are thrown with the bar laid out.
        this.#nameBox.open();
        this.#text.open();
    }

    /** The bar's slots' contents are taken away: as the band goes, the fields are drawn again when it shows. */
    hide(): void {
        this.adopt({ text: undefined, nameBox: undefined }, () => false);
    }

    /** The bar is going: the slots' contents are taken away. */
    dispose(): void {
        this.#text.dispose();
        this.#nameBox.dispose();
        this.#listeners.clear();
        this.band.remove();
    }

    #makeNameBox(mount: GridChromeSlot<GridNameBoxSlotContext> | undefined): BarField<GridNameBoxSlotContext> {
        if (mount === undefined) return BarField.builtIn(field(this.#doc, 'ex-name-box'));
        const box = this.#doc.createElement('div');
        box.className = 'ex-name-box';
        // Enter in a textarea submits nothing: the Name Box takes a text input alone.
        return BarField.slotted('nameBox', box, mount, true, takeField => Object.freeze({ takeField }), handed => {
            fieldTraits(handed);
            this.#showName(handed);
        });
    }

    #makeText(mount: GridChromeSlot<GridFormulaBarTextSlotContext> | undefined): BarField<GridFormulaBarTextSlotContext> {
        // The text field wears the Cell Editor's class: it is the editor's second surface (ADR-0051).
        if (mount === undefined) return BarField.builtIn(field(this.#doc, 'ex-editor ex-formula-bar-text'));
        const box = this.#doc.createElement('div');
        box.className = 'ex-editor ex-formula-bar-text';
        box.classList.toggle('ex-formula-bar-borrowed', this.#view.borrowed);
        return BarField.slotted('formulaBarText', box, mount, false, takeField => Object.freeze({
            view: () => this.#view,
            subscribe: this.#listeners.subscribe,
            takeField,
        }), handed => {
            fieldTraits(handed);
            this.#showText(handed);
        });
    }

    #showName(name: EditorField): void {
        const paint = this.#paint;
        if (paint === undefined) return;
        setAttr(name, 'aria-label', paint.nameBoxLabel);
        if (this.#typed === undefined && name.value !== paint.nameBox) name.value = paint.nameBox;
    }

    #showText(text: EditorField): void {
        const paint = this.#paint;
        if (paint === undefined) return;
        setAttr(text, 'aria-label', paint.textLabel);
        // Written only when it differs: a write moves the caret, and the text the user is typing is already there.
        if (text.value !== this.#shownText) text.value = this.#shownText;
        text.readOnly = !paint.editable;
    }
}

// A field of a Chrome's, as the built-in one is: no tab stop (the grid is one, ADR-0020), and nothing the browser
// would fill in or mark.
function fieldTraits(handed: EditorField): void {
    for (const [name, value] of Object.entries({ tabindex: '-1', autocomplete: 'off', spellcheck: 'false' })) setAttr(handed, name, value);
}

function field(doc: Document, className: string): HTMLInputElement {
    const input = doc.createElement('input');
    input.className = className;
    for (const [name, value] of Object.entries({ tabindex: '-1', autocomplete: 'off', spellcheck: 'false' })) input.setAttribute(name, value);
    return input;
}

function setStyle(element: HTMLElement, style: string): void {
    if (element.getAttribute('style') !== style) element.setAttribute('style', style);
}

function setAttr(element: HTMLElement, name: string, value: string): void {
    if (element.getAttribute(name) !== value) element.setAttribute(name, value);
}
