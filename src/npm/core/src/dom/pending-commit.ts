// A Pending Commit's input (ADR-0380 section 3): what the root holds while a commit's answer is out, and how a
// held input is taken again once the answer has come. Every input made meanwhile waits and is taken in the order it
// was made, against what the answer left, so typing fast or slow ends the same (spine rule 6). A clipboard event's
// data is readable only inside the event, so a held paste keeps its flavours, and a held copy or cut is done at its
// turn by the grid's asynchronous route.

/** The input events the root holds while a commit is pending. A key, a press, a drag and what follows a press, typed
 * text and a composition, and the clipboard's three commands. A move with no button down is the hover's, and passes. */
export const heldEventTypes: readonly string[] = Object.freeze([
    'keydown', 'keyup', 'mousedown', 'mousemove', 'mouseup', 'click', 'dblclick', 'auxclick', 'contextmenu',
    'beforeinput', 'input', 'compositionstart', 'compositionupdate', 'compositionend', 'copy', 'cut', 'paste',
]);

/** One input made while a commit was pending, in the order it was made. */
export type HeldInput =
    | { readonly kind: 'event'; readonly event: Event }
    /** A paste: its flavours, read while the event could still be read, or none where it carried no data. */
    | { readonly kind: 'paste'; readonly flavours: { readonly text: string; readonly html: string } | undefined }
    /** A copy or a cut: done at its turn, where the keyboard is then. */
    | { readonly kind: 'copy' | 'cut' }
    /** Work of the Consumer's placed in the order, what an Escape that named such work answers, or a held row command,
     * taken at its turn (ADR-0240 section 12); `dropped` hears that a commit this turn made pending did not land, so the
     * turn is not taken again as its gesture. */
    | { readonly kind: 'turn'; readonly take: () => void; readonly dropped?: () => void };

/** What a held event becomes. A clipboard event keeps what only it could tell, and every other event is kept to be
 * dispatched again. */
export function heldInputOf(event: Event): HeldInput {
    if (event instanceof ClipboardEvent && event.type === 'paste') {
        const data = event.clipboardData;
        return { kind: 'paste', flavours: data === null ? undefined : { text: data.getData('text/plain'), html: data.getData('text/html') } };
    }
    if (event.type === 'copy' || event.type === 'cut') return { kind: event.type };
    return { kind: 'event', event };
}

/** Whether a keydown is the Escape that cancels a Pending Commit: the plain key, as Escape cancels an edit
 * (ADR-0380 section 3, ADR-0240 section 12). */
export function cancelsPending(event: Event): boolean {
    if (!(event instanceof KeyboardEvent) || event.type !== 'keydown') return false;
    return event.key === 'Escape' && !event.ctrlKey && !event.altKey && !event.metaKey && !event.shiftKey && !event.isComposing;
}

/**
 * A held event made again, to be dispatched where it lands now. The browser's own event cannot be dispatched twice
 * as it was, so its fields are copied into a new one of the same kind. A press keeps the offset it was made at, which
 * the rows resolve it by.
 */
export function eventAgain(event: Event): Event {
    const base = { bubbles: event.bubbles, cancelable: event.cancelable, composed: event.composed };
    let again: Event;
    if (event instanceof KeyboardEvent) {
        again = new KeyboardEvent(event.type, {
            ...base, key: event.key, code: event.code, location: event.location, repeat: event.repeat,
            isComposing: event.isComposing, ctrlKey: event.ctrlKey, shiftKey: event.shiftKey, altKey: event.altKey,
            metaKey: event.metaKey,
        });
        // keyCode is read where an IME's key is told apart (229, ADR-0080); the constructor cannot set it.
        Object.defineProperty(again, 'keyCode', { value: event.keyCode });
    } else if (event instanceof MouseEvent) {
        again = new MouseEvent(event.type, {
            ...base, clientX: event.clientX, clientY: event.clientY, screenX: event.screenX, screenY: event.screenY,
            button: event.button, buttons: event.buttons, detail: event.detail, relatedTarget: event.relatedTarget,
            ctrlKey: event.ctrlKey, shiftKey: event.shiftKey, altKey: event.altKey, metaKey: event.metaKey,
        });
        Object.defineProperty(again, 'offsetX', { value: event.offsetX });
        Object.defineProperty(again, 'offsetY', { value: event.offsetY });
    } else if (typeof CompositionEvent !== 'undefined' && event instanceof CompositionEvent) {
        again = new CompositionEvent(event.type, { ...base, data: event.data });
    } else if (typeof InputEvent !== 'undefined' && event instanceof InputEvent) {
        again = new InputEvent(event.type, { ...base, data: event.data, inputType: event.inputType, isComposing: event.isComposing });
    } else {
        again = new Event(event.type, base);
    }
    return again;
}

/** A text field the browser would type into: an input or a text area that takes text now, at a caret. An email or a
 * number input has no selection to type at, and is not one. */
export function typedField(target: EventTarget | null): HTMLInputElement | HTMLTextAreaElement | undefined {
    if (target instanceof HTMLTextAreaElement) return target.readOnly || target.disabled ? undefined : target;
    if (target instanceof HTMLInputElement) {
        const takesText = ['text', 'search', 'url', 'tel', 'password'].includes(target.type);
        return takesText && !target.readOnly && !target.disabled ? target : undefined;
    }
    return undefined;
}

/** Whether a keydown is one of the clipboard's commands, whose default is the browser's copy, cut or paste event: the
 * root holds the event that follows, and so lets the key's default run. */
export function isClipboardKey(event: Event): boolean {
    if (!(event instanceof KeyboardEvent) || event.type !== 'keydown' || event.altKey) return false;
    const primary = event.ctrlKey || event.metaKey;
    const letter = event.key.toLowerCase();
    if (primary && !event.shiftKey && (letter === 'c' || letter === 'x' || letter === 'v')) return true;
    return (primary && event.key === 'Insert') || (event.shiftKey && !primary && (event.key === 'Insert' || event.key === 'Delete'));
}

/** Replaces a field's selected text, as typing or pasting it would, and tells the field's listeners. */
export function typeText(field: HTMLInputElement | HTMLTextAreaElement, text: string, inputType: string): void {
    field.setRangeText(text, selectionOf(field, 'start'), selectionOf(field, 'end'), 'end');
    field.dispatchEvent(new InputEvent('input', { bubbles: true, data: text, inputType }));
}

/** What the browser's own meaning of a key taken again came to: done, none to do, or one that cannot be done again
 * now, which is said. */
export type KeyDefault = 'done' | 'none' | 'lost';

/**
 * What the browser does with a key the page left alone in a text field, done again for a key taken at its turn: a
 * character is typed, Backspace and Delete delete, the caret keys move the caret or, with Shift, the selection, by a
 * character, a word or the line, the primary key with A selects all, and with Z or Y undoes or redoes. A key made again
 * is the page's own, and the browser does none of these for it. `metaIsPrimary` is the platform's: Command is the
 * primary key and Option moves by a word there, Control elsewhere.
 */
export function keyDefault(field: HTMLInputElement | HTMLTextAreaElement, key: KeyboardEvent, metaIsPrimary: boolean): KeyDefault {
    const value = field.value;
    const start = selectionOf(field, 'start');
    const end = selectionOf(field, 'end');
    const backward = field.selectionDirection === 'backward';
    const anchor = backward ? end : start;
    const moving = backward ? start : end;
    const primary = metaIsPrimary ? key.metaKey : key.ctrlKey;
    const word = metaIsPrimary ? key.altKey && !key.ctrlKey && !key.metaKey : key.ctrlKey && !key.altKey && !key.metaKey;
    const line = metaIsPrimary && key.metaKey && !key.ctrlKey && !key.altKey;
    const plain = !key.ctrlKey && !key.metaKey && !key.altKey;
    // The caret, or with Shift the selection's moving end, put at `to`.
    const reach = (to: number): KeyDefault => {
        if (key.shiftKey) field.setSelectionRange(Math.min(anchor, to), Math.max(anchor, to), to < anchor ? 'backward' : 'forward');
        else field.setSelectionRange(to, to);
        return 'done';
    };
    const remove = (from: number, to: number, inputType: string): KeyDefault => {
        if (from !== to) {
            field.setRangeText('', from, to, 'end');
            field.dispatchEvent(new InputEvent('input', { bubbles: true, inputType }));
        }
        return 'done';
    };
    if (primary && !key.altKey && (key.key === 'a' || key.key === 'A')) {
        field.setSelectionRange(0, value.length, 'forward');
        return 'done';
    }
    if (primary && !key.altKey && (key.key.toLowerCase() === 'z' || key.key.toLowerCase() === 'y')) {
        // A field's own history is the browser's, reached through the one command that walks it.
        const redo = key.key.toLowerCase() === 'y' || key.shiftKey;
        const doc = field.ownerDocument;
        return typeof doc.execCommand === 'function' && doc.execCommand(redo ? 'redo' : 'undo') ? 'done' : 'lost';
    }
    switch (key.key) {
        case 'Backspace':
            if (start !== end) return remove(start, end, 'deleteContentBackward');
            if (word || line) return remove(line ? 0 : wordStart(value, start), start, 'deleteWordBackward');
            return plain || key.shiftKey ? remove(stepBack(value, start), start, 'deleteContentBackward') : 'none';
        case 'Delete':
            if (start !== end) return remove(start, end, 'deleteContentForward');
            if (word) return remove(end, wordEnd(value, end, metaIsPrimary), 'deleteWordForward');
            return plain ? remove(end, stepOn(value, end), 'deleteContentForward') : 'none';
        case 'ArrowLeft':
            if (line) return reach(0);
            if (word) return reach(wordStart(value, moving));
            if (!plain) return 'none';
            return reach(key.shiftKey || start === end ? stepBack(value, moving) : start);
        case 'ArrowRight':
            if (line) return reach(value.length);
            if (word) return reach(wordEnd(value, moving, metaIsPrimary));
            if (!plain) return 'none';
            return reach(key.shiftKey || start === end ? stepOn(value, moving) : end);
        case 'Home':
            return plain || word ? reach(0) : 'none';
        case 'End':
            return plain || word ? reach(value.length) : 'none';
        default: {
            // A character: Control and Alt together type one, as AltGr does, and Option alone does where Command is
            // the primary key (ADR-0010).
            const types = !key.metaKey && (key.ctrlKey === key.altKey || (metaIsPrimary && key.altKey && !key.ctrlKey));
            // A key's value is one character, or the name of a key that types none: counted in code points, so a
            // character outside the Basic Multilingual Plane counts as one.
            // oxlint-disable-next-line typescript/no-misused-spread -- code points, not graphemes: a key types one
            if (!types || [...key.key].length !== 1) return 'none';
            typeText(field, key.key, 'insertText');
            return 'done';
        }
    }
}

/**
 * What the browser does with a key the page left alone on a control that is not a text field, or that a text field
 * does not take, done again: Enter submits the form a field stands in, Enter or Space presses a button or a link,
 * Space ticks a checkbox or a radio, and Tab moves the keyboard to the next control in the tab order, or with Shift
 * the previous. A Tab with nowhere left to go in the page would leave it, which cannot be done again.
 */
export function elementDefault(target: HTMLElement, key: KeyboardEvent): KeyDefault {
    if (key.ctrlKey || key.metaKey || key.altKey) return 'none';
    if (key.key === 'Tab') return tabFrom(target, key.shiftKey);
    if (key.shiftKey) return 'none';
    const presses = target instanceof HTMLButtonElement || (target instanceof HTMLAnchorElement && target.href !== '')
        || (target instanceof HTMLInputElement && ['button', 'submit', 'reset'].includes(target.type));
    const ticks = target instanceof HTMLInputElement && (target.type === 'checkbox' || target.type === 'radio');
    if (key.key === 'Enter') {
        if (presses) {
            target.click();
            return 'done';
        }
        if (target instanceof HTMLInputElement && target.form !== null && !ticks) {
            target.form.requestSubmit();
            return 'done';
        }
        return 'none';
    }
    if (key.key === ' ' && (ticks || (presses && !(target instanceof HTMLAnchorElement)))) {
        target.click();
        return 'done';
    }
    return 'none';
}

// The tab order of a document, as the browser walks it: positive tabindexes first, in order, then the rest as they
// stand; controls disabled, hidden or taken out of it are not in it.
function tabFrom(target: HTMLElement, back: boolean): KeyDefault {
    const tabbable = [...target.ownerDocument.querySelectorAll<HTMLElement>(
        'a[href], button, input, select, textarea, [tabindex], [contenteditable="true"]',
    )].filter(element => element.tabIndex >= 0 && !('disabled' in element && element.disabled === true)
        && !(element instanceof HTMLInputElement && element.type === 'hidden') && element.closest('[hidden], [inert]') === null);
    const order = [...tabbable.filter(element => element.tabIndex > 0).toSorted((a, b) => a.tabIndex - b.tabIndex),
        ...tabbable.filter(element => element.tabIndex === 0)];
    const at = order.indexOf(target);
    const next = at < 0 ? undefined : order[back ? at - 1 : at + 1];
    if (next === undefined) return 'lost';
    next.focus();
    return 'done';
}

// One character back or on from `at`, a surrogate pair counted as one.
function stepBack(value: string, at: number): number {
    if (at <= 0) return 0;
    return at >= 2 && isLowSurrogate(value.charCodeAt(at - 1)) && isHighSurrogate(value.charCodeAt(at - 2)) ? at - 2 : at - 1;
}

function stepOn(value: string, at: number): number {
    if (at >= value.length) return value.length;
    return at + 1 < value.length && isHighSurrogate(value.charCodeAt(at)) && isLowSurrogate(value.charCodeAt(at + 1)) ? at + 2 : at + 1;
}

const isHighSurrogate = (code: number): boolean => code >= 0xd800 && code <= 0xdbff;
const isLowSurrogate = (code: number): boolean => code >= 0xdc00 && code <= 0xdfff;
const isSpace = (character: string | undefined): boolean => character !== undefined && /\s/u.test(character);

// The start of the word before `at`: past the spaces, then past the word.
function wordStart(value: string, at: number): number {
    let i = at;
    while (i > 0 && isSpace(value[i - 1])) i--;
    while (i > 0 && !isSpace(value[i - 1])) i--;
    return i;
}

// Where a word move from `at` lands: the end of the word where Option moves, the start of the next elsewhere.
function wordEnd(value: string, at: number, toItsEnd: boolean): number {
    let i = at;
    if (toItsEnd) {
        while (i < value.length && isSpace(value[i])) i++;
        while (i < value.length && !isSpace(value[i])) i++;
    } else {
        while (i < value.length && !isSpace(value[i])) i++;
        while (i < value.length && isSpace(value[i])) i++;
    }
    return i;
}

/** Where a text field's selection starts or ends: one that takes text always has one. */
export function selectionOf(field: HTMLInputElement | HTMLTextAreaElement, edge: 'start' | 'end'): number {
    const at = edge === 'start' ? field.selectionStart : field.selectionEnd;
    if (at === null) throw new Error('A text field that takes text has a selection, and this one has none.');
    return at;
}
