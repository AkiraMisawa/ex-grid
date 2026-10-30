// A caret placed from script at either end of a field's text, brought into view as the field brings the user's own:
// setting the value or the selection from script moves no view (tickets 32 and 75, the C# core's showCaret). At the
// start the field shows its start; at the end the offset is set past the far end, and the browser clamps it, so the
// caret stands at the field's right edge. Nothing is read but the field's value (ADR-0021). A caret short of the end
// is left where the browser shows it: no place for it can be set without the width of the text before it (ADR-0058,
// decided 2026-10-01). The layer that colours the field follows the offset through the field's scroll event, as it
// does when the user types (ADR-0057).
export function showCaret(field: HTMLInputElement | HTMLTextAreaElement, at: number): void {
    if (at <= 0) field.scrollLeft = 0;
    else if (at >= field.value.length) field.scrollLeft = Number.MAX_SAFE_INTEGER;
}
