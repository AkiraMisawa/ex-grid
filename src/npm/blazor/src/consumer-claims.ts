// The Consumer's own markup the grid draws, a popover's contents or a Template cell's, whose controls .NET handles
// (ADR-0441 section 4, ADR-0240 section 6): a control that names an event in `data-ex-claims` is numbered as the event
// happens, and told to .NET just before Blazor dispatches it, and the inputs after it wait until the control's handler
// hands its work to `InOrder(kind, work)`, which runs it in that place. Whether a handler hears the event is not the
// browser's to see: the contents' own script may stop it before it reaches Blazor's listener on the document, and a
// component may stop it inside Blazor's own dispatch (`@onmousedown:stopPropagation`). So every number is said passed
// once its dispatch is over, and one no handler took by then is void, so nothing waits on a handler that will not run.

/** Numbers an event of `kind` on a control of the Consumer's, holding the inputs after it until its handler's work has
 * run, and answers the number; `passed` says the dispatch of the event numbered is over. */
export interface Claims {
    claim(kind: string): number;
    passed(number: number): void;
}

/** Hears the claims on the contents wired to it. */
export interface ClaimsHeard {
    /** Hears the claims on `contents`, answering the function that stops it. */
    wire(contents: HTMLElement): () => void;
}

/** The events a control may claim that Blazor hears on the document as they bubble: heard on the contents in the
 * capture phase, after the grid's own listeners on its root, which hold or take an input first. */
const bubblingKinds: readonly string[] = ['mousedown', 'click', 'dblclick', 'contextmenu', 'keydown'];

/** The events Blazor hears on the document in the capture phase, ahead of any listener on the contents: heard in the
 * window's capture phase, filtered to the contents wired (ADR-0021's twelfth use). */
const capturedKinds: readonly string[] = ['submit', 'focus'];

/** The claims on the contents of one grid: a listener on each contents for the bubbling kinds, and one on the window,
 * whatever the number of contents, for the captured kinds. */
export class ConsumerClaims implements ClaimsHeard {
    readonly #view: Window;
    readonly #claims: Claims;
    readonly #contents = new Set<Element>();

    constructor(view: Window, claims: Claims) {
        this.#view = view;
        this.#claims = claims;
        for (const kind of capturedKinds) view.addEventListener(kind, this.#onCaptured, true);
    }

    wire(contents: HTMLElement): () => void {
        this.#contents.add(contents);
        const onBubbling = (event: Event): void => { this.#heard(contents, event); };
        for (const kind of bubblingKinds) contents.addEventListener(kind, onBubbling, true);
        return () => {
            this.#contents.delete(contents);
            for (const kind of bubblingKinds) contents.removeEventListener(kind, onBubbling, true);
        };
    }

    /** The grid is going: nothing is heard again. */
    dispose(): void {
        for (const kind of capturedKinds) this.#view.removeEventListener(kind, this.#onCaptured, true);
        this.#contents.clear();
    }

    readonly #onCaptured = (event: Event): void => {
        // The contents wired that hold the target, found from the target up.
        for (let element = event.target instanceof Element ? event.target : null; element !== null; element = element.parentElement) {
            if (this.#contents.has(element)) {
                this.#heard(element, event);
                return;
            }
        }
    };

    #heard(contents: Element, event: Event): void {
        if (!claimed(contents, event.target, event.type)) return;
        const number = this.#claims.claim(event.type);
        // A task after the dispatch, which ends before any task runs: a handler that heard the event has handed its work.
        this.#view.setTimeout(() => { this.#claims.passed(number); }, 0);
    }
}

/** Whether the control `target` stands on in `contents` claims an event of `kind`. */
function claimed(contents: Element, target: EventTarget | null, kind: string): boolean {
    if (!(target instanceof Element) || !contents.contains(target)) return false;
    const claimant = target.closest('[data-ex-claims]');
    if (claimant === null || !contents.contains(claimant)) return false;
    const names = claimant.getAttribute('data-ex-claims');
    return names !== null && names.split(/\s+/u).includes(kind);
}
