// The built-in Chrome's find panel (ADR-0055) and its Replace (ADR-0332 §2): a popover by every popover rule
// (ADR-0039, ADR-0040), with a Tab-wrap sentinel either side that hands the keyboard back to its field. Enter
// in a field is the form's implicit submission, which an IME's candidate Enter is not, and Shift says which
// way the step runs. What the panel holds outlives it: the grid keeps the text and the two options, and the
// replacement, across a close and a reopen, as Excel's dialog does. The panel decides nothing: a step, a
// Replace and a Replace All are the grid's, and what they came to is said in the outcome line.
//
// The DOM is the C# core's built-in panel's, so ex-grid.css and layer 3 read both alike.

import { findPanelLabelIds } from '../chrome/find-panel-labels';

/** What the find panel holds, kept by the grid across a close and a reopen (ADR-0055, ADR-0332 §4). */
export interface FindHeld {
    /** What to look for. */
    text: string;
    /** Match case. */
    matchCase: boolean;
    /** Match entire cell contents. */
    wholeCell: boolean;
    /** What Replace writes in place of a match. */
    replaceText: string;
}

/** What the grid does for its panel. */
export interface FindPanelAnswers {
    /** A step: Find next, or Find previous (`backward`). */
    readonly step: (backward: boolean) => void;
    /** Replace: the current match written, then the next found. */
    readonly replaceOne: () => void;
    /** Replace All, asked of the Consumer. */
    readonly replaceAll: () => void;
    /** The text or an option changed: the Match Marks may move (ADR-0332 §4). */
    readonly heldChanged: () => void;
    /** An input reached the panel and was answered (ADR-0220). */
    readonly answered: () => void;
}

/** The find panel as the grid holds it while it stands: the built-in panel, or a Chrome slot's (ADR-0390). */
export interface FindPlace {
    /** The popover, `.ex-popover.ex-popover-find`, which the grid places under the root. */
    readonly element: HTMLElement;
    /** Whether the panel offers Replace. */
    readonly replaceShown: boolean;
    /** Whether a popup of the contents' own is open: Escape is then the popup's (ADR-0039). */
    readonly innerPopupOpen: boolean;
    /** Shows or takes away the Replace part (ADR-0332 §2). */
    showReplace(shown: boolean): void;
    /** Whether a Replace All is out with the Consumer: Replace and Replace All wait for its answer (ADR-0332 §3). */
    waitForReplace(waiting: boolean): void;
    /** What the last step or Replace came to, as the grid words it; empty for one that says nothing. */
    say(text: string): void;
    /** The find field takes the keyboard, its text selected, so what is typed next replaces it (FD-1). */
    focusField(): void;
    /** The panel is going. */
    dispose(): void;
}

/** The form a find panel's fields stand in: Enter in a field is its implicit submission, which an IME's candidate
 * Enter is not, and runs a step, backward where Shift was held (ADR-0055). The hidden default button lets a form of
 * two fields, Find's and Replace's, submit on Enter, which without one it does not. A submit by any other button
 * is that button's own, and steps nothing: a button the contents draw calls the step it means. */
export function findForm(doc: Document, answers: Pick<FindPanelAnswers, 'answered' | 'step'>): HTMLFormElement {
    const form = doc.createElement('form');
    form.className = 'ex-popover-form';
    form.setAttribute('data-ex-claims', 'submit');
    const submit = doc.createElement('button');
    submit.type = 'submit';
    submit.className = 'ex-find-default';
    submit.hidden = true;
    submit.tabIndex = -1;
    submit.setAttribute('aria-hidden', 'true');
    form.append(submit);
    // Whether the Enter that submits the form was typed with Shift in a field: the step runs backward. Each Enter
    // says it afresh, so a Shift+Enter on a button, which submits nothing, leaves nothing behind.
    let backward = false;
    form.addEventListener('keydown', event => {
        if (event.key !== 'Enter') return;
        const target = event.target;
        backward = event.shiftKey && target instanceof HTMLInputElement && !['button', 'submit', 'reset', 'image'].includes(target.type);
    });
    form.addEventListener('submit', event => {
        event.preventDefault();
        const back = backward;
        backward = false;
        const by: unknown = Reflect.get(event, 'submitter');
        // A requestSubmit() with no button names none (a DOM that names the form itself means the same).
        if (by instanceof HTMLElement && by !== submit && by !== form) return;
        answers.answered();
        answers.step(back);
    });
    return form;
}

/** A Tab-wrap sentinel at either end of the find panel: Tab off either end comes back to the field (ADR-0039). */
export function findSentinel(doc: Document, answered: () => void, toField: () => void): HTMLElement {
    const element = doc.createElement('span');
    element.className = 'ex-focus-wrap';
    element.tabIndex = 0;
    element.setAttribute('data-ex-claims', 'focus');
    element.addEventListener('focus', () => {
        answered();
        toField();
    });
    return element;
}

/** The find panel's popover, `.ex-popover.ex-popover-find`, named by its words. */
export function findElement(doc: Document, label: string): HTMLElement {
    const element = doc.createElement('div');
    element.className = 'ex-popover ex-popover-find';
    element.setAttribute('role', 'dialog');
    element.setAttribute('aria-label', label);
    return element;
}

/** The Replace part of the panel, shown while it was opened for Replace (ADR-0332 §2). */
interface ReplacePart {
    readonly field: HTMLInputElement;
    readonly actions: HTMLElement;
    readonly buttons: readonly HTMLButtonElement[];
}

/** The find panel, standing in the grid's top right under the header (ADR-0040). Built once per opening. */
export class FindPanel implements FindPlace {
    readonly element: HTMLElement;
    /** The built-in panel opens no popup of its own. */
    readonly innerPopupOpen = false;
    readonly #doc: Document;
    readonly #held: FindHeld;
    readonly #words: (id: string) => string;
    readonly #answers: FindPanelAnswers;
    readonly #form: HTMLFormElement;
    readonly #field: HTMLInputElement;
    readonly #outcome: HTMLElement;
    #replace: ReplacePart | undefined;
    // Whether a Replace All is out with the Consumer.
    #waiting = false;

    constructor(doc: Document, held: FindHeld, words: (id: string) => string, answers: FindPanelAnswers) {
        this.#doc = doc;
        this.#held = held;
        this.#words = words;
        this.#answers = answers;
        this.element = findElement(doc, words(findPanelLabelIds.title));
        const body = doc.createElement('div');
        body.className = 'ex-popover-find-body ex-popover-body';
        this.#form = findForm(doc, answers);
        this.#field = this.#textField('search', 'ex-find-field', findPanelLabelIds.field, held.text, text => {
            held.text = text;
            answers.heldChanged();
        });
        const actions = doc.createElement('div');
        actions.className = 'ex-popover-actions';
        actions.append(
            this.#button('ex-find-previous', findPanelLabelIds.previous, () => { answers.step(true); }),
            this.#button('ex-find-next', findPanelLabelIds.next, () => { answers.step(false); }),
        );
        this.#outcome = doc.createElement('div');
        this.#outcome.className = 'ex-find-outcome';
        this.#outcome.setAttribute('role', 'status');
        this.#form.append(
            this.#field,
            this.#option('ex-find-match-case', findPanelLabelIds.matchCase, held.matchCase, checked => { held.matchCase = checked; }),
            this.#option('ex-find-whole-cell', findPanelLabelIds.wholeCell, held.wholeCell, checked => { held.wholeCell = checked; }),
            actions, this.#outcome,
        );
        body.append(this.#form);
        const toField = (): void => { this.#field.focus({ preventScroll: true }); };
        this.element.append(findSentinel(doc, answers.answered, toField), body, findSentinel(doc, answers.answered, toField));
    }

    /** Nothing to take away: the panel goes with its element. */
    dispose(): void {
        // The listeners are on the element, which goes with the popover.
    }

    /** Whether the panel offers Replace. */
    get replaceShown(): boolean {
        return this.#replace !== undefined;
    }

    /** Shows or takes away the Replace part (ADR-0332 §2): Ctrl+H shows it, Ctrl+F turns the panel back into
     * Find alone. The panel's name says which it is. */
    showReplace(shown: boolean): void {
        if (shown === this.replaceShown) return;
        const replace = this.#replace;
        if (replace !== undefined) {
            replace.field.remove();
            replace.actions.remove();
            this.#replace = undefined;
        } else {
            const field = this.#textField('text', 'ex-find-replace-field', findPanelLabelIds.replaceField, this.#held.replaceText, text => {
                this.#held.replaceText = text;
            });
            const actions = this.#doc.createElement('div');
            actions.className = 'ex-popover-actions';
            const buttons = [
                this.#button('ex-find-replace', findPanelLabelIds.replace, this.#answers.replaceOne),
                this.#button('ex-find-replace-all', findPanelLabelIds.replaceAll, this.#answers.replaceAll),
            ];
            for (const button of buttons) button.disabled = this.#waiting;
            actions.append(...buttons);
            this.#outcome.before(field, actions);
            this.#replace = { field, actions, buttons };
        }
        this.element.setAttribute('aria-label', this.#words(shown ? findPanelLabelIds.replaceTitle : findPanelLabelIds.title));
        if (shown) this.element.setAttribute('data-ex-replace', '');
        else this.element.removeAttribute('data-ex-replace');
    }

    /** Its two buttons wait for the answer. */
    waitForReplace(waiting: boolean): void {
        this.#waiting = waiting;
        for (const button of this.#replace?.buttons ?? []) button.disabled = waiting; // oxlint-disable-line ex-grid/no-stand-in-default -- a panel without Replace has no button to wait
    }

    say(text: string): void {
        if (this.#outcome.textContent !== text) this.#outcome.textContent = text;
    }

    focusField(): void {
        this.#field.focus({ preventScroll: true });
        this.#field.select();
    }

    #textField(type: string, className: string, label: string, value: string, changed: (text: string) => void): HTMLInputElement {
        const field = this.#doc.createElement('input');
        field.type = type;
        field.className = className;
        field.setAttribute('aria-label', this.#words(label));
        field.value = value;
        field.addEventListener('input', () => { changed(field.value); });
        return field;
    }

    // An option, its tick claimed by the checkbox alone: a click on its words reaches the checkbox as a second
    // click, which is the one taken (ADR-0240).
    #option(className: string, label: string, checked: boolean, set: (checked: boolean) => void): HTMLLabelElement {
        const box = this.#doc.createElement('input');
        box.type = 'checkbox';
        box.className = className;
        box.checked = checked;
        box.setAttribute('data-ex-claims', 'click');
        box.addEventListener('change', () => {
            this.#answers.answered();
            set(box.checked);
            this.#answers.heldChanged();
        });
        const element = this.#doc.createElement('label');
        element.append(box, this.#words(label));
        return element;
    }

    #button(className: string, label: string, press: () => void): HTMLButtonElement {
        const element = this.#doc.createElement('button');
        element.type = 'button';
        element.className = className;
        element.setAttribute('data-ex-claims', 'click');
        element.textContent = this.#words(label);
        element.addEventListener('click', () => {
            this.#answers.answered();
            press();
        });
        return element;
    }
}
