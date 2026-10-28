// What the kinds of the Sheet's Razor slots share (ADR-0410): how a part of a view crosses to .NET, and how the
// controls the Razor marked are found.

/** A part of the view as JSON carries it: JSON has no undefined, and .NET reads null as none. */
export function crossed<T>(value: T | undefined): T | null {
    return value === undefined ? null : value;
}

/** The elements of the contents marked `attribute`, each with its value. */
export function marked(contents: HTMLElement, attribute: string): [HTMLElement, string][] {
    const found: [HTMLElement, string][] = [];
    for (const element of contents.querySelectorAll<HTMLElement>(`[${attribute}]`)) {
        const value = element.getAttribute(attribute);
        if (value === null) throw new Error(`Internal: an element found by ${attribute} does not carry it.`);
        found.push([element, value]);
    }
    return found;
}
