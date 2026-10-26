// The pivot's own elements, made as every view of it makes them.

/** A new element of `document`, with `className` when given. */
export function element<K extends keyof HTMLElementTagNameMap>(document: Document, tag: K, className?: string): HTMLElementTagNameMap[K] {
    const made = document.createElement(tag);
    if (className !== undefined) made.className = className;
    return made;
}

// The style each element was last written, by the pivot.
const written = new WeakMap<Element, string>();

/** Writes `style` on `target` when it differs from what the pivot last wrote there, as a Razor render writes an
 * attribute only when its own value changed: a style the page set on the element since stays until the pivot's
 * own changes. */
export function writeStyle(target: Element, style: string): void {
    if (written.get(target) === style) return;
    written.set(target, style);
    target.setAttribute('style', style);
}

/** Takes away the style the pivot wrote on `target`, which it no longer draws. */
export function clearStyle(target: Element): void {
    written.delete(target);
    target.removeAttribute('style');
}
