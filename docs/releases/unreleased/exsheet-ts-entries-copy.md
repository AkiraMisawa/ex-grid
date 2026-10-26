Product: ExSheet
Kind: Added
ADR: 0048, 0049, 0050, 0370

The TypeScript Sheet (`@ex-grid/sheet`) answers a copy itself, as the Blazor component does: the clipboard carries the engine's copy of the range, its Values as shown for `text/plain` and unformatted for `text/html`, however far the range runs beyond what is drawn, and a copy reaching `#GETTING_DATA` is refused in the engine's words. That copy pasted back into the Sheet carries its Entries, relative References shifted, as one step on the undo stack.
