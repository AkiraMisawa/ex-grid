// The pivot's own elements, made as every view of it makes them.

/** A new element of `document`, with `className` when given. */
export function element<K extends keyof HTMLElementTagNameMap>(document: Document, tag: K, className?: string): HTMLElementTagNameMap[K] {
    const made = document.createElement(tag);
    if (className !== undefined) made.className = className;
    return made;
}
