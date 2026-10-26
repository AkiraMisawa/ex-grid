// The small DOM helpers the Sheet's own elements share: an attribute written only when it differs, so a repaint
// that changes nothing touches nothing, and a press whose default is taken from the browser, so it keeps the
// keyboard where it is.

/** Writes `value` into `name` only when it differs from what is there. */
export function setAttr(element: HTMLElement, name: string, value: string): void {
    if (element.getAttribute(name) !== value) element.setAttribute(name, value);
}

/** Prevents the event's default: on a mousedown, the move of the keyboard to what was pressed. */
export function preventDefault(event: Event): void {
    event.preventDefault();
}
