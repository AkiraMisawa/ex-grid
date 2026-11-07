// An open edit's own undo history (ADR-0007's last bullet, ADR-0380 section 3's note of 2026-11-07): what Ctrl+Z and
// Ctrl+Y undo and redo while an edit is open, kept by the grid rather than by the field. The browser's own history in a
// text field closes its typing run whenever anything else in the document is removed or has its text changed, so where
// it would undo to depended on when the page repainted beside the typing: the Hunt of 2026-11-07, ExGrid's differential
// seed 1165771808, where `7.5Bravo 2` typed at machine speed undid to `7.5`, the previous commit's cells repainting
// between the `5` and the `B`, and typed one key at a time undid to `7`. Kept here, a run is decided by the edit alone.

/** An edit's text and its field's selection at one moment. */
export interface EditState {
    readonly text: string;
    readonly start: number;
    readonly end: number;
}

/** What kind of change an input made: typing joins typing and deleting joins deleting, a composition joins itself until
 * it ends, and anything else is a step of its own. */
type ChangeKind = 'insert' | 'delete' | 'compose' | 'own';

/** One undoable step: the state before it and after it. */
interface Step {
    readonly before: EditState;
    after: EditState;
}

/** The input types that are a step of their own, as the browser makes a paste, a drop and a cut. */
const ownSteps: ReadonlySet<string> = new Set(['insertFromPaste', 'insertFromPasteAsQuotation', 'insertFromDrop', 'insertReplacementText',
    'insertFromYank', 'deleteByCut', 'deleteByDrag']);

/** Whether a field's input of `inputType` is the history's own, an undo or a redo. */
export function isHistoryInput(inputType: string): boolean {
    return inputType === 'historyUndo' || inputType === 'historyRedo';
}

/**
 * The undo history of one open edit, over both of its surfaces, the cell's editor and the Formula Bar (ADR-0051): one
 * text, one history. A run of characters typed one after another at the caret is undone together, and so is a run of
 * deletions, as ADR-0380's note of 2026-11-05 has it; a caret moved between two inputs, a change of kind, a paste, a
 * drop, a cut and text the grid writes itself (a Reference pointed at, a completion taken) each start a step of their
 * own. Nothing outside the edit splits a run.
 */
export class EditHistory {
    readonly #undo: Step[] = [];
    readonly #redo: Step[] = [];
    // The state the history last saw, and the kind of the run its last step is, while that run may take more.
    #now: EditState;
    #run: ChangeKind | undefined;

    /** The history of an edit that opened on `opened`, with nothing to undo. */
    constructor(opened: EditState) {
        this.#now = opened;
    }

    /** Whether there is a step to undo. */
    get canUndo(): boolean {
        return this.#undo.length > 0;
    }

    /** Whether there is a step to redo. */
    get canRedo(): boolean {
        return this.#redo.length > 0;
    }

    /**
     * The edit's field took an input of `inputType`, and now holds `after`. Typed text joins the run open before it
     * where it was put at the caret that run left; a deletion likewise, at either side of that caret. Anything typed
     * after an undo makes what was undone no longer redoable, as in a field.
     */
    typed(inputType: string, after: EditState): void {
        if (isHistoryInput(inputType)) throw new Error(`Internal: the edit's history heard its own ${inputType} as typing.`);
        const before = this.#now;
        if (before.text === after.text) {
            // The selection alone moved, or nothing did: no step, but a run does not go on past a caret moved.
            if (before.start !== after.start || before.end !== after.end) this.#run = undefined;
            this.#now = after;
            return;
        }
        const kind = changeKind(inputType, before, after);
        const last = this.#undo.at(-1);
        if (last !== undefined && kind !== 'own' && kind === this.#run && continues(kind, before, after)) {
            last.after = after;
        } else {
            this.#undo.push({ before: placedBefore(inputType, before, after), after });
        }
        this.#redo.length = 0;
        this.#run = kind === 'own' ? undefined : kind;
        this.#now = after;
    }

    /** The edit's text was written by the grid itself, a Reference pointed at or a completion taken: a step of its own.
     * The same text written again is no step. */
    written(after: EditState): void {
        if (after.text === this.#now.text) {
            this.#now = after;
            return;
        }
        this.#undo.push({ before: this.#now, after });
        this.#redo.length = 0;
        this.#run = undefined;
        this.#now = after;
    }

    /** The selection moved with the text unchanged, by a key or a press: the open run takes no more. */
    moved(after: EditState): void {
        if (after.text !== this.#now.text) throw new Error('Internal: the edit\'s text changed where only its caret was said to move.');
        if (after.start !== this.#now.start || after.end !== this.#now.end) this.#run = undefined;
        this.#now = after;
    }

    /** Takes the last step back, and answers the state the edit is to show, or none where there is nothing to undo. */
    undo(): EditState | undefined {
        const step = this.#undo.pop();
        if (step === undefined) return undefined;
        this.#redo.push(step);
        this.#run = undefined;
        this.#now = step.before;
        return step.before;
    }

    /** Does the last step undone again, and answers the state the edit is to show, or none where there is nothing to redo. */
    redo(): EditState | undefined {
        const step = this.#redo.pop();
        if (step === undefined) return undefined;
        this.#undo.push(step);
        this.#run = undefined;
        this.#now = step.after;
        return step.after;
    }
}

// What an input of `inputType` did to the text: typed text added with nothing taken, a deletion taken with nothing added,
// a composition's text, or another change.
function changeKind(inputType: string, before: EditState, after: EditState): ChangeKind {
    if (ownSteps.has(inputType)) return 'own';
    if (inputType === 'insertCompositionText') return 'compose';
    const { removed, added } = changed(before.text, after.text, after.start);
    if (inputType.startsWith('insert') && removed === 0 && added > 0) return 'insert';
    if (inputType.startsWith('delete') && added === 0 && removed > 0) return 'delete';
    return 'own';
}

// Whether a change of `kind` goes on from where the run before it left the caret: text put at that caret, or a deletion
// that ends there (Backspace) or starts there (Delete). A run never goes on from a selection.
function continues(kind: ChangeKind, before: EditState, after: EditState): boolean {
    if (kind === 'compose') return true;
    if (before.start !== before.end) return false;
    const { at, removed, added } = changed(before.text, after.text, after.start);
    if (kind === 'insert') return at === before.start && at + added === after.start;
    return at === before.start || at + removed === before.start;
}

// Where a step starts, as undoing it shows: the selection its first input was made at. The history's own record of the
// caret is the one it saw last, so a caret moved since, which no input tells, is read from the change itself: typed text
// was put where its first character now stands, and a deletion was made at its own edge.
function placedBefore(inputType: string, before: EditState, after: EditState): EditState {
    const { at, removed, added } = changed(before.text, after.text, after.start);
    if (added > 0 && removed > 0) return { text: before.text, start: at, end: at + removed };
    if (added > 0) return { text: before.text, start: at, end: at };
    const caret = inputType.includes('Backward') ? at + removed : at;
    return { text: before.text, start: caret, end: caret };
}

// Where `before` and `after` differ: the first character changed, and how many were taken and added there. Text added
// beside a run of the same characters, or taken from one, could be read as changed at more than one place; it is read
// where the caret the change left says, `caret`, so a character typed beside its twin is read where it was typed.
function changed(before: string, after: string, caret: number): { readonly at: number; readonly removed: number; readonly added: number } {
    const shorter = Math.min(before.length, after.length);
    let prefix = 0;
    while (prefix < shorter && before.charCodeAt(prefix) === after.charCodeAt(prefix)) prefix += 1;
    let suffix = 0;
    while (suffix < shorter && before.charCodeAt(before.length - 1 - suffix) === after.charCodeAt(after.length - 1 - suffix)) suffix += 1;
    const grown = after.length - before.length;
    if (prefix + suffix >= shorter) {
        // Only added or only taken: the place may slide over equal characters, between the two bounds.
        const added = Math.max(grown, 0);
        const removed = Math.max(-grown, 0);
        const earliest = shorter - suffix;
        const wanted = caret - added;
        const at = Math.min(prefix, Math.max(earliest, wanted));
        return { at, removed, added };
    }
    return { at: prefix, removed: before.length - prefix - suffix, added: after.length - prefix - suffix };
}
