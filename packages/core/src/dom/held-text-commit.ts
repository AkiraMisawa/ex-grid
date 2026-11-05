// A text field's commit for text a held input typed into it (ADR-0210, carried to the Script Core). A held input is
// taken again at its turn (ADR-0380 section 3). Into the field that holds the keyboard its text goes through the
// browser's own editing (pending-commit.ts, editsAsTyped), which the browser takes as the user's edit and commits itself
// when the field is left or a real Enter is typed there; only a held Enter, made again by the grid, has no default that
// would, and is supplied its `change`. Into any other field it is put in from script: the browser does not take that
// text as the user's own edit, so it sends no `change` at all, and a field whose handlers commit on `change` (Blazor's
// `@bind`, a MudBlazor text field) would show the text while its owner still held the old value. The grid knows which
// field it typed into and supplies only the notification the browser does not.

/** The field held typing last changed, its value before, the value the grid last wrote there, and whether that last
 * edit was the browser's own, which the browser commits itself. */
interface Typed {
    readonly field: HTMLInputElement | HTMLTextAreaElement;
    readonly before: string;
    written: string;
    byBrowser: boolean;
}

/**
 * The one field per grid whose held text is still to be committed. Its `blur`, `change` and `keydown` are heard only
 * while it is: leaving it supplies the `change` it lacks where it still holds what the grid wrote and that differs from
 * the value before, a `change` of its own first removes the supplement, and an Enter typed in a single-line field once
 * the hold is over supplies it before the field's own handlers hear the key. Where typing since has changed the value,
 * the browser commits it itself; where the field's owner has written it since, the value is the owner's, not the
 * user's. Typing into another field retires the record without committing a field left behind; a field removed while
 * it held the keyboard hears no `blur`, and is retired the same way, or when the grid goes.
 */
export class HeldTextCommit {
    #typed: Typed | undefined;
    #replaying = false;

    /** Notes that a held input changed `field`, whose value was `before` ahead of it, through the browser's own editing
     * (`byBrowser`) or from script. The last edit decides: the browser commits a value its own edit left, and not one
     * script wrote over it. */
    typed(field: HTMLInputElement | HTMLTextAreaElement, before: string, byBrowser: boolean): void {
        const typed = this.#typed;
        if (typed?.field === field) {
            typed.written = field.value;
            typed.byBrowser = byBrowser;
            return;
        }
        this.retire();
        this.#typed = { field, before, written: field.value, byBrowser };
        field.addEventListener('blur', this.#commit);
        field.addEventListener('change', this.#changed);
        // Ahead of the field's own listeners, which hear the key at the target after the capturing ones.
        field.addEventListener('keydown', this.#enter, true);
    }

    /**
     * A held key handed on to `field` by the grid: its keydown is the grid's own, so the Enter listener leaves it, and
     * a held Enter commits the held text after the field's handlers have heard it, before the form is submitted, as the
     * browser's own Enter does.
     */
    handOn(field: Element, key: KeyboardEvent, hand: () => void): void {
        this.#replaying = true;
        try {
            hand();
        } finally {
            this.#replaying = false;
        }
        if (key.type === 'keydown' && key.key === 'Enter' && field instanceof HTMLInputElement && this.#typed?.field === field) this.#supply();
    }

    /** Forgets the record without committing it: the grid is going, or held typing went to another field. */
    retire(): void {
        const typed = this.#typed;
        if (typed === undefined) return;
        this.#typed = undefined;
        typed.field.removeEventListener('blur', this.#commit);
        typed.field.removeEventListener('change', this.#changed);
        typed.field.removeEventListener('keydown', this.#enter, true);
    }

    // The field is left, or a real Enter is typed there: the browser commits its own edit itself, and the grid supplies
    // the commit of one it wrote.
    readonly #commit = (): void => {
        if (this.#typed?.byBrowser === true) {
            this.retire();
            return;
        }
        this.#supply();
    };

    // The `change` the browser does not send, where the field still holds what the grid wrote and that differs from before.
    #supply(): void {
        const typed = this.#typed;
        if (typed === undefined) return;
        this.retire();
        const { field, before, written } = typed;
        if (field.isConnected && field.value === written && written !== before) field.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // The field committed by itself: the supplement would be its duplicate.
    readonly #changed = (): void => {
        this.retire();
    };

    readonly #enter = (event: Event): void => {
        // A textarea takes Enter as a line, and commits nothing on it.
        if (this.#replaying || !(event instanceof KeyboardEvent) || event.key !== 'Enter' || event.isComposing) return;
        if (event.currentTarget instanceof HTMLInputElement) this.#commit();
    };
}
