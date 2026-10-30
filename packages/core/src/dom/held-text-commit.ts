// A text field's commit for text a held input typed into it (ADR-0210, carried to the Script Core). A held input is
// taken again at its turn (ADR-0380 section 3), and text it types is put in from script: the browser does not take
// that text as the user's own edit, so it sends no `change` when the field is left, or when Enter is typed there,
// and a field whose handlers commit on `change` (Blazor's `@bind`, a MudBlazor text field) would show the text while
// its owner still held the old value. The grid knows which field it typed into and supplies only that notification.

/** The field held typing last changed, its value before, and the value the grid last wrote there. */
interface Typed {
    readonly field: HTMLInputElement | HTMLTextAreaElement;
    readonly before: string;
    written: string;
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

    /** Notes that a held input changed `field`, whose value was `before` ahead of it. */
    typed(field: HTMLInputElement | HTMLTextAreaElement, before: string): void {
        const typed = this.#typed;
        if (typed?.field === field) {
            typed.written = field.value;
            return;
        }
        this.retire();
        this.#typed = { field, before, written: field.value };
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
        if (key.type === 'keydown' && key.key === 'Enter' && field instanceof HTMLInputElement && this.#typed?.field === field) this.#commit();
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

    readonly #commit = (): void => {
        const typed = this.#typed;
        if (typed === undefined) return;
        this.retire();
        const { field, before, written } = typed;
        if (field.isConnected && field.value === written && written !== before) field.dispatchEvent(new Event('change', { bubbles: true }));
    };

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
