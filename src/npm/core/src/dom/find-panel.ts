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

/** The Replace part of the panel, shown while it was opened for Replace (ADR-0332 §2). */
interface ReplacePart {
    readonly field: HTMLInputElement;
    readonly actions: HTMLElement;
    readonly buttons: readonly HTMLButtonElement[];
}

/** The find panel, standing in the grid's top right under the header (ADR-0040). Built once per opening. */
export class FindPanel {
    /** The popover, `.ex-popover.ex-popover-find`, which the grid places under the root. */
    readonly element: HTMLElement;
    readonly #doc: Document;
    readonly #held: FindHeld;
    readonly #words: (id: string) => string;
    readonly #answers: FindPanelAnswers;
    readonly #form: HTMLFormElement;
    readonly #field: HTMLInputElement;
    readonly #outcome: HTMLElement;
    #replace: ReplacePart | undefined;
    // Whether the Enter that submits the form was typed with Shift: the step runs backward.
    #backward = false;
    // Whether a Replace All is out with the Consumer.
    #waiting = false;

    constructor(doc: Document, held: FindHeld, words: (id: string) => string, answers: FindPanelAnswers) {
        this.#doc = doc;
        this.#held = held;
        this.#words = words;
        this.#answers = answers;
        this.element = doc.createElement('div');
        this.element.className = 'ex-popover ex-popover-find';
        this.element.setAttribute('role', 'dialog');
        this.element.setAttribute('aria-label', words(findPanelLabelIds.title));
        const body = doc.createElement('div');
        body.className = 'ex-popover-find-body ex-popover-body';
        this.#form = doc.createElement('form');
        this.#form.className = 'ex-popover-form';
        this.#form.setAttribute('data-ex-claims', 'submit');
        this.#form.addEventListener('submit', event => {
            event.preventDefault();
            const backward = this.#backward;
            this.#backward = false;
            answers.answered();
            answers.step(backward);
        });
        // The default button Enter's implicit submission needs once the Replace field makes the form's second
        // field, which without one submits nothing.
        const submit = doc.createElement('button');
        submit.type = 'submit';
        submit.className = 'ex-find-default';
        submit.hidden = true;
        submit.tabIndex = -1;
        submit.setAttribute('aria-hidden', 'true');
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
            submit, this.#field,
            this.#option('ex-find-match-case', findPanelLabelIds.matchCase, held.matchCase, checked => { held.matchCase = checked; }),
            this.#option('ex-find-whole-cell', findPanelLabelIds.wholeCell, held.wholeCell, checked => { held.wholeCell = checked; }),
            actions, this.#outcome,
        );
        body.append(this.#form);
        this.element.append(this.#sentinel(), body, this.#sentinel());
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

    /** Whether a Replace All is out with the Consumer: its two buttons wait for the answer (ADR-0332 §3). */
    waitForReplace(waiting: boolean): void {
        this.#waiting = waiting;
        for (const button of this.#replace?.buttons ?? []) button.disabled = waiting; // oxlint-disable-line ex-grid/no-stand-in-default -- a panel without Replace has no button to wait
    }

    /** What the last step or Replace came to, as the grid words it; empty for one that says nothing. */
    say(text: string): void {
        if (this.#outcome.textContent !== text) this.#outcome.textContent = text;
    }

    /** The find field takes the keyboard, its text selected, so what is typed next replaces it (FD-1). */
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
        field.addEventListener('keydown', event => {
            if (event.key === 'Enter') this.#backward = event.shiftKey;
        });
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

    // Tab off either end comes back to the field (ADR-0039).
    #sentinel(): HTMLElement {
        const element = this.#doc.createElement('span');
        element.className = 'ex-focus-wrap';
        element.tabIndex = 0;
        element.setAttribute('data-ex-claims', 'focus');
        element.addEventListener('focus', () => {
            this.#answers.answered();
            this.#field.focus({ preventScroll: true });
        });
        return element;
    }
}
