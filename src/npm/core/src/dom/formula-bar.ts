// The Formula Bar and its Name Box (ADR-0051): a band inside the grid's root, above the header, where the Focus
// cell's text is read without editing it, and the Cell Editor's second surface: one uncommitted text, shown in
// two places, committed once from either. The Name Box names the Focus in the Consumer's words, and what is typed
// into it is handed to the Consumer, which resolves it and places the Selection (ADR-0050, item 4). Neither field
// is a tab stop: the grid is one (ADR-0020). The fields wear the C# core's classes, so one stylesheet lays out
// both cores' bars.

import type { CellPosition } from '../selection/cell';
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
    /** The text the bar shows dimmed, and read only, for a cell whose own text is empty: on a Sheet, the Formula
     * of the array a cell is spilled into (ADR-0125). Hold it in a field: its identity is the change signal. */
    readonly formulaBarBorrowedText?: (row: Row, column: GridColumn<Row>) => string | undefined;
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

/** The band's elements and the listeners on them. */
export class FormulaBar {
    readonly band: HTMLElement;
    readonly text: HTMLInputElement;
    readonly nameBox: HTMLInputElement;
    readonly #form: HTMLFormElement;
    // What the user has typed into the Name Box and not entered: shown in place of the label while it holds the
    // keyboard.
    #typed: string | undefined;

    constructor(doc: Document, hooks: FormulaBarHooks) {
        this.band = doc.createElement('div');
        this.band.className = 'ex-formula-bar';
        this.band.setAttribute('role', 'presentation');
        // The Name Box's Enter is its form's implicit submission, as the filter panel's is: no key handler, and an
        // IME's confirming Enter is left alone (ADR-0051).
        this.#form = doc.createElement('form');
        this.#form.className = 'ex-name-box-form';
        this.nameBox = field(doc, 'ex-name-box');
        this.#form.append(this.nameBox);
        // The text field wears the Cell Editor's class: it is the editor's second surface (ADR-0051).
        this.text = field(doc, 'ex-editor ex-formula-bar-text');
        this.band.append(this.#form, this.text);
        this.#form.addEventListener('submit', event => {
            event.preventDefault();
            const typed = this.#typed;
            this.#typed = undefined;
            // Enter on the label shown, nothing typed, names nothing new: the keyboard goes back, as Escape's does.
            if (typed === undefined) hooks.nameBoxLeft();
            else hooks.nameBoxEntered(typed);
        });
        this.nameBox.addEventListener('input', () => { this.#typed = this.nameBox.value; });
        // A press into the Name Box selects its text, so what is typed replaces the address shown (ADR-0051).
        this.nameBox.addEventListener('focus', () => {
            this.nameBox.select();
            hooks.nameBoxFocused();
        });
        this.nameBox.addEventListener('blur', () => { this.#typed = undefined; });
        this.nameBox.addEventListener('keydown', event => {
            if (event.key !== 'Escape' || event.isComposing) return;
            event.preventDefault();
            this.#typed = undefined;
            hooks.nameBoxLeft();
        });
        this.text.addEventListener('input', () => { hooks.textTyped(this.text.value); });
        this.text.addEventListener('focus', () => { hooks.textFocused(); });
    }

    /** Whether the Name Box holds text the user typed and has not entered. */
    get nameBoxTyped(): boolean {
        return this.#typed !== undefined;
    }

    paint(paint: FormulaBarPaint): void {
        setStyle(this.band, paint.widthPx === undefined ? `height: ${paint.heightPx}px; right: 0` : `height: ${paint.heightPx}px; width: ${paint.widthPx}px`);
        setStyle(this.nameBox, `width: ${paint.nameBoxWidthPx}px`);
        setAttr(this.nameBox, 'aria-label', paint.nameBoxLabel);
        setAttr(this.text, 'aria-label', paint.textLabel);
        if (this.#typed === undefined && this.nameBox.value !== paint.nameBox) this.nameBox.value = paint.nameBox;
        // Written only when it differs: a write moves the caret, and the text the user is typing is already there.
        if (this.text.value !== paint.text) this.text.value = paint.text;
        this.text.readOnly = !paint.editable;
        this.text.classList.toggle('ex-formula-bar-borrowed', paint.borrowed);
    }
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
