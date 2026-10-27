// Formula entry's completion list and argument hint (ADR-0051, ADR-0058), as the C# core's ExGrid.FormulaEntry.cs
// paints and works them: the Consumer is handed the editor's text and caret as the user types, in the Cell Editor and
// in the Formula Bar alike, and its answer is painted beneath the surface typed in, a list of candidates and a hint.
// The grid does not know what a Formula is: what is offered, and what accepting it writes, are the Consumer's.

import { EditorCompletion } from '../cells/editor-text';
import { editingKeysByMode } from '../keys/editing-keys';
import { ShownSlot, type GridChromeSlot, type GridEditorCompletionSlotContext } from './grid-chrome';

/**
 * The Consumer's completion (ADR-0051): asked with the editor's text and caret, it answers what to offer there, now or
 * later. An answer for text that has since changed is dropped, never shown; nothing, or an answer with nothing in it,
 * shows nothing.
 */
export type CompleteEditorText = (text: string, caret: number) => EditorCompletion | undefined | PromiseLike<EditorCompletion | undefined>;

/** The declaration checked: a function, or none. */
export function checkedCompleteEditorText(given: { readonly completeEditorText?: CompleteEditorText }): CompleteEditorText | undefined {
    // Read as unknown first: a Consumer in plain JavaScript hands whatever it hands.
    const loose: { readonly completeEditorText?: unknown } = given;
    if (loose.completeEditorText !== undefined && typeof loose.completeEditorText !== 'function') {
        throw new TypeError('completeEditorText is a function (ADR-0051).');
    }
    return given.completeEditorText;
}

/** An answer checked: an EditorCompletion, or nothing. Anything else is refused by name, never shown as a guess. */
export function checkedCompletion(answer: unknown): EditorCompletion | undefined {
    if (answer === undefined || answer instanceof EditorCompletion) return answer;
    throw new TypeError('completeEditorText answers an EditorCompletion, or nothing (ADR-0051).');
}

/** What a key does while a list is open (ADR-0051, ADR-0058): `undefined` where the key is not the list's. */
export type CompletionKeyAction = 'down' | 'up' | 'accept' | 'close' | 'closeAndPoint';

// While open over Point, these close the list and point as they do without it (ADR-0058, Part B of the ninth run): the
// keys a list over Point claims beyond those of any list, read from the one declaration of both.
const pointingPastTheList: ReadonlySet<string> = new Set(
    editingKeysByMode.completionOverPoint.filter(key => !editingKeysByMode.completion.includes(key)),
);

/**
 * A key typed while a list is open (ADR-0051): ↑ and ↓ choose, Tab accepts the chosen candidate and Escape closes the
 * list, leaving the edit open. A list takes no other key (ADR-0058, the tenth Windows run): over Point, ←, →, Home, End
 * and the Shift+arrows close it and point; every other key keeps its meaning, and what it does to the edit takes the
 * list with it.
 */
export function completionKey(canonical: string, overPoint: boolean): CompletionKeyAction | undefined {
    switch (canonical) {
        case 'ArrowDown': return 'down';
        case 'ArrowUp': return 'up';
        case 'Tab': return 'accept';
        case 'Escape': return 'close';
        default: return overPoint && pointingPastTheList.has(canonical) ? 'closeAndPoint' : undefined;
    }
}

/** Where the box stands, in the grid's box from its top left, the Formula Bar's band included (ADR-0040). */
export interface CompletionPlace {
    readonly leftPx: number;
    readonly topPx: number;
    /** How tall it may grow. */
    readonly roomPx: number;
    /** Whether it hangs above the cell, by its own height. */
    readonly above: boolean;
}

/** The numbers the box is placed with: those the grid already holds, nothing measured (ADR-0027, ADR-0040). */
export interface CompletionGeometry {
    /** Whether the edit is typed in the Formula Bar's field, the bar shown. */
    readonly inBar: boolean;
    readonly barPx: number;
    readonly nameBoxWidthPx: number;
    readonly bandHeightPx: number;
    readonly rowHeightPx: number;
    /** The Viewport's visible height, the header band included. */
    readonly visibleHeightPx: number;
    /** The edited cell's left edge in the Viewport, and its top below the header band. */
    readonly cellLeftPx: number;
    readonly cellTopPx: number;
}

/**
 * Where the completion box stands (ADR-0040): beneath the surface the user is typing in, the Formula Bar's field from
 * its left edge, or the edited cell on the side of it with more room inside the grid, bounded by that room. None where
 * neither side has a row's height of room: a list that cannot show one candidate is not shown.
 */
export function completionPlace(at: CompletionGeometry): CompletionPlace | undefined {
    const { barPx, rowHeightPx, visibleHeightPx, bandHeightPx } = at;
    if (at.inBar) {
        const room = Math.max(0, visibleHeightPx);
        return room < rowHeightPx ? undefined : { leftPx: at.nameBoxWidthPx, topPx: barPx, roomPx: room, above: false };
    }
    const cellTop = clamp(bandHeightPx + at.cellTopPx, bandHeightPx, visibleHeightPx);
    const below = clamp(cellTop + rowHeightPx, 0, visibleHeightPx);
    const roomBelow = visibleHeightPx - below;
    // Above, the room reaches the top of the grid's box, the Formula Bar's band included.
    const roomAbove = barPx + cellTop;
    if (roomBelow >= roomAbove) {
        return roomBelow < rowHeightPx ? undefined : { leftPx: at.cellLeftPx, topPx: barPx + below, roomPx: roomBelow, above: false };
    }
    return roomAbove < rowHeightPx ? undefined : { leftPx: at.cellLeftPx, topPx: barPx + cellTop, roomPx: roomAbove, above: true };
}

function clamp(value: number, low: number, high: number): number {
    return Math.min(Math.max(value, low), Math.max(low, high));
}

/**
 * The box the answer is painted in (ADR-0051), the C# core's markup: a list of options, the chosen one marked, and the
 * hint with the argument the caret is in set off. A press in it keeps the keyboard in the editor; a press on a
 * candidate accepts it. The Chrome's editorCompletion slot may draw the list and the hint in the box instead
 * (ADR-0390): the box, its place and every key stay the core's.
 */
export class CompletionBox {
    readonly element: HTMLElement;
    readonly #listId: string;
    readonly #optionPrefix: string;
    readonly #accept: (index: number, answer: EditorCompletion) => void;
    // The Chrome's editorCompletion slot, standing in the box while it draws the answer (ADR-0390).
    readonly #slot: ShownSlot<GridEditorCompletionSlotContext>;
    #painted: { readonly answer: EditorCompletion; readonly selected: number } | undefined;
    // The answer the slot was last handed, and how many have been: the slot's key tells a new answer from the one
    // standing by the count.
    #answer: EditorCompletion | undefined;
    #answers = 0;

    /** `accept` takes a candidate's index with the answer it was shown from, which the grid refuses once another stands. */
    constructor(doc: Document, idPrefix: string, accept: (index: number, answer: EditorCompletion) => void) {
        this.#listId = `${idPrefix}completion`;
        this.#optionPrefix = `${idPrefix}completion-`;
        this.#accept = accept;
        this.#slot = new ShownSlot('editorCompletion', doc.createElement('div'));
        this.element = doc.createElement('div');
        this.element.className = 'ex-completion';
        this.element.setAttribute('role', 'presentation');
        // A press is the box's alone, whoever drew it: a slot's contents accept on the click that follows, which
        // reaches their framework where it listens (React, at its root).
        this.element.addEventListener('mousedown', event => {
            event.preventDefault();
            event.stopPropagation();
            const painted = this.#painted;
            if (event.button !== 0 || !(event.target instanceof Element) || painted === undefined) return;
            const option = event.target.closest('[data-ex-candidate]');
            const index = option === null ? undefined : Number(option.getAttribute('data-ex-candidate'));
            if (index !== undefined) accept(index, painted.answer);
        });
    }

    /** The list's id, which the editor's `aria-controls` names. */
    get listId(): string {
        return this.#listId;
    }

    /** The id of the candidate at `index`, which the editor's `aria-activedescendant` names. */
    optionId(index: number): string {
        return `${this.#optionPrefix}${index}`;
    }

    /** The slot's contents go: the box has left the grid, or the grid is going. */
    hide(): void {
        this.#slot.hide();
    }

    /** Paints the answer, built again only when it or the chosen candidate changed; or has `slot`, the Chrome's, draw
     * it in the box, mounted again when either changes. */
    paint(answer: EditorCompletion, selected: number, slot: GridChromeSlot<GridEditorCompletionSlotContext> | undefined): void {
        if (slot !== undefined) {
            this.#painted = undefined;
            if (this.#answer !== answer) {
                this.#answer = answer;
                this.#answers += 1;
            }
            this.#slot.show(slot, `${this.#answers}:${selected}`, standing => Object.freeze({
                candidates: answer.candidates, selected: answer.candidates.length === 0 ? -1 : selected, hint: answer.hint,
                listId: this.#listId,
                optionId: (index: number) => this.optionId(index),
                accept: (index: number) => { if (standing()) this.#accept(index, answer); },
            }));
            if (this.#slot.element.parentNode !== this.element || this.element.childNodes.length !== 1) {
                this.element.replaceChildren(this.#slot.element);
            }
            return;
        }
        this.#slot.hide();
        const painted = this.#painted;
        if (painted?.answer === answer && painted.selected === selected) return;
        this.#painted = { answer, selected };
        const doc = this.element.ownerDocument;
        const parts: HTMLElement[] = [];
        if (answer.candidates.length > 0) {
            const list = doc.createElement('div');
            list.className = 'ex-completion-list';
            list.id = this.#listId;
            list.setAttribute('role', 'listbox');
            answer.candidates.forEach((candidate, index) => {
                const item = doc.createElement('div');
                item.className = index === selected ? 'ex-completion-item ex-completion-selected' : 'ex-completion-item';
                item.id = this.optionId(index);
                item.setAttribute('role', 'option');
                item.setAttribute('aria-selected', index === selected ? 'true' : 'false');
                item.setAttribute('data-ex-candidate', String(index));
                item.textContent = candidate.label;
                list.append(item);
            });
            parts.push(list);
        }
        const { hint } = answer;
        if (hint !== undefined) {
            const [before, emphasis, after] = hint.parts();
            const line = doc.createElement('div');
            line.className = 'ex-completion-hint';
            line.append(before);
            if (emphasis.length > 0) {
                const strong = doc.createElement('strong');
                strong.textContent = emphasis;
                line.append(strong);
            }
            line.append(after);
            parts.push(line);
        }
        this.element.replaceChildren(...parts);
    }
}
