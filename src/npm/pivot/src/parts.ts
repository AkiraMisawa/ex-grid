// The controls and the anchors of the pivot's standing parts (ADR-0061, ADR-0390): the Pivot Toolbar's buttons, a
// report filter's, Retry. The built-in markup and a Chrome's contents hand them in alike, each by its name, so the
// pivot gives the keyboard to the control it means and places a popup's frame in the anchor it belongs in, the same
// way whichever drew them. A part's contents are drawn by their framework when it gets to them: a control asked for
// before it is handed in takes the keyboard once it is, unless the keyboard has moved on meanwhile.

import type { PivotPartRef } from './chrome';

/** Whether DOM focus is inside `pivot`, or on nothing: the keyboard is still the pivot's (ADR-0204). */
export function keyboardIsThePivots(pivot: Element): boolean {
    const document = pivot.ownerDocument;
    const active = document.activeElement;
    return active === null || active === document.body || active === document.documentElement || pivot.contains(active);
}

/** The elements the pivot's standing parts handed in, by name. */
export class PartElements {
    readonly #root: HTMLElement;
    readonly #controls = new Map<string, HTMLElement>();
    readonly #anchors = new Map<string, HTMLElement>();
    // What each anchor is to hold: a popup's frame, or the report filter band, placed once the anchor is handed in.
    readonly #placed = new Map<string, HTMLElement>();
    // What hears an element placed land in its anchor, the first time it does.
    readonly #landing = new Map<HTMLElement, () => void>();
    // The control the keyboard is owed to, given once it is handed in; any focus moving or press meanwhile ends it,
    // since a press on nothing focusable moves the keyboard to the page without a focus event. A key does not: one
    // that moves the keyboard moves focus, and one typed while the control is on its way is held in the pivot's Input
    // Order on an adapter that holds (ADR-0410 section 5), to be taken where the control stands once it does.
    #owed: string | undefined;
    readonly #listening = new AbortController();

    constructor(root: HTMLElement) {
        this.#root = root;
        const settled = (): void => { this.#owed = undefined; };
        for (const type of ['focusin', 'pointerdown']) {
            root.ownerDocument.addEventListener(type, settled, { capture: true, signal: this.#listening.signal });
        }
    }

    /** The pivot is gone: nothing is heard, and nothing owed is given. */
    dispose(): void {
        this.#listening.abort();
        this.#owed = undefined;
    }

    /** The refs by which one mount of a Chrome's contents hands in its controls and anchors: the same function for a
     * name each time it is asked, and each taking back only what it handed in, so contents taken away late take nothing
     * the contents after them handed in under the same name. */
    refs(): PartRefs {
        const controls = new Map<string, PivotPartRef>();
        const anchors = new Map<string, PivotPartRef>();
        return {
            control: name => cachedRef(controls, name, () => elementRef(this.#controls, name, `An ExPivot part's '${name}'`, element => { this.#giveOwed(name, element); })),
            anchor: name => cachedRef(anchors, name, () => elementRef(this.#anchors, name, `An ExPivot part's '${name}'`, element => { this.#fill(name, element); })),
        };
    }

    /** The built-in markup's control named `name`, handed in as it stands: no Chrome's ref is involved, so a Chrome's
     * contents taken away later take nothing of it. */
    handInControl(name: string, element: HTMLElement): void {
        this.#controls.set(name, element);
        this.#giveOwed(name, element);
    }

    /** The built-in markup's anchor named `name`, handed in as it stands. */
    handInAnchor(name: string, element: HTMLElement): void {
        this.#anchors.set(name, element);
        this.#fill(name, element);
    }

    /** Takes back what was handed in as `name`, where it is still `element`: built-in markup that no longer stands. */
    forget(name: string, element: HTMLElement): void {
        if (this.#controls.get(name) === element) this.#controls.delete(name);
        if (this.#anchors.get(name) === element) this.#anchors.delete(name);
    }

    /** The anchor named `name` went with what held it: nothing is placed in it any longer, though its framework lets
     * go of it only later. */
    dropAnchor(name: string): void {
        this.#anchors.delete(name);
    }

    /** Whether an anchor named `name` stands: a popup opens only in one that does. */
    anchored(name: string): boolean {
        return this.#anchors.get(name)?.isConnected === true;
    }

    /** Whether the control named `name` holds the keyboard. */
    holds(name: string): boolean {
        const control = this.#controls.get(name);
        const active = this.#root.ownerDocument.activeElement;
        return control !== undefined && active !== null && control.contains(active);
    }

    /** Gives the keyboard to the control named `name`; one not drawn yet takes it once it is, while the keyboard is
     * still the pivot's and nothing else took it meanwhile. */
    focus(name: string): void {
        const control = this.#controls.get(name);
        if (control?.isConnected === true) {
            this.#owed = undefined;
            control.focus();
        } else {
            this.#owed = name;
        }
    }

    /** Places `element` in the anchor named `name`, now or once it is handed in; `undefined` places nothing there any
     * longer, as the pivot takes what it placed away itself. `landed` hears the first time `element` stands in an
     * anchor. */
    place(name: string, element: HTMLElement | undefined, landed?: () => void): void {
        if (element === undefined) {
            const placed = this.#placed.get(name);
            if (placed !== undefined) this.#landing.delete(placed);
            this.#placed.delete(name);
            return;
        }
        this.#placed.set(name, element);
        if (landed !== undefined) this.#landing.set(element, landed);
        const anchor = this.#anchors.get(name);
        if (anchor !== undefined) this.#fill(name, anchor);
    }

    /** `element` is placed nowhere any longer: the pivot took it away. */
    unplace(element: HTMLElement): void {
        for (const [name, placed] of this.#placed) {
            if (placed === element) this.#placed.delete(name);
        }
        this.#landing.delete(element);
    }

    #fill(name: string, anchor: HTMLElement): void {
        const placed = this.#placed.get(name);
        if (placed === undefined || placed.parentNode === anchor) return;
        anchor.append(placed);
        const landed = this.#landing.get(placed);
        this.#landing.delete(placed);
        landed?.();
    }

    // The keyboard owed to `name`, given to `element` once it is in the page: a framework hands an element in once it
    // is drawn, plain script often just before.
    #giveOwed(name: string, element: HTMLElement): void {
        if (this.#owed !== name) return;
        const give = (): void => {
            if (this.#owed !== name || !element.isConnected || this.#controls.get(name) !== element) return;
            this.#owed = undefined;
            if (keyboardIsThePivots(this.#root)) element.focus();
        };
        if (element.isConnected) give();
        else queueMicrotask(give);
    }
}

/** One mount's refs, by name. */
export interface PartRefs {
    /** The ref for the control named `name`. */
    readonly control: (name: string) => PivotPartRef;
    /** The ref for the anchor named `name`. */
    readonly anchor: (name: string) => PivotPartRef;
}

/** The ref `refs` holds for `key`, made by `make` the first time it is asked, so a framework handed it again on each
 * render does not hand the element in again. */
export function cachedRef<Key>(refs: Map<Key, PivotPartRef>, key: Key, make: () => PivotPartRef): PivotPartRef {
    let ref = refs.get(key);
    if (ref === undefined) {
        ref = make();
        refs.set(key, ref);
    }
    return ref;
}

/** A ref handing in the element `elements` holds as `key`, which `what` names when anything else is handed in: an
 * element handed in replaces the one before, and `null` takes away only the element this ref handed in, since a
 * framework may hand in the next before it takes back the last, and the built-in markup may stand in its place by
 * then. `handedIn` hears each element handed in. */
export function elementRef<Key>(elements: Map<Key, HTMLElement>, key: Key, what: string, handedIn?: (element: HTMLElement) => void): PivotPartRef {
    let current: HTMLElement | undefined;
    return element => {
        if (element === null) {
            if (current !== undefined && elements.get(key) === current) elements.delete(key);
            current = undefined;
            return;
        }
        if (!(element instanceof HTMLElement)) throw new TypeError(`${what} is handed in as an element, or null once it is gone (ADR-0390).`);
        current = element;
        elements.set(key, element);
        handedIn?.(element);
    };
}

/** `name`, checked as a script may hand it to a part's `control` or `anchor`: one of `names`, which `slot` hands in. */
export function checkedName<Name extends string>(name: Name, names: readonly Name[], slot: string): Name {
    if (!names.includes(name)) throw new RangeError(`An ExPivot's ${slot} hands in ${names.join(', ')}, not '${name}' (ADR-0390).`);
    return name;
}

/** `standing` in the place of `leaving`, where that stands. */
export function swap(leaving: HTMLElement, standing: HTMLElement): void {
    if (leaving.parentNode !== null) leaving.replaceWith(standing);
}
