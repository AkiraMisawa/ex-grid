Product: ExGrid
Kind: Added
ADR: 0240
Docs: exgrid/appearance

A control outside the grid's own markup — a Chrome's, a popover's contents, a command of your own beside the grid — can now take its place in the grid's Input Order: call `InOrderAsync(kind, handling)` on the grid, or the new `InOrder` delegate on `GridPopoverContext`, with the `GridInputKind` of the event it answers. A control that carries `data-ex-claims`, naming the events it claims, has its event numbered as it happens and handled after every input made before it and before every one made after it; an input no number was told for runs after every input the grid has received so far.
