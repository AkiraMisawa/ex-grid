Product: ExGrid
Kind: Added
ADR: 0070, 0370

`@ex-grid/core` takes `onLeave`, the Blazor component's `OnLeave`: it hears the Escape pressed on the grid with nothing left to dismiss, beside the release of Tab, once per press. A Consumer that shows the grid in a dialog closes the dialog on it, as ExPivot's Show Details does.
