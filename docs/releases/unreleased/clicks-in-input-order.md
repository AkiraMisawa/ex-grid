Product: ExGrid
Kind: Behaviour
ADR: 0240
Docs: exgrid/selection

A click on one of the grid's own controls — a heading, a column's ▾, a column menu or Context Menu item, a completion candidate, the pager, the status line's buttons, a grip's double click, a filter panel's OK and Cancel, Find's Previous and Next, the context menu on the rows and on the status line, under the built-in Chrome and the MudBlazor Wrapper alike — now takes its place in the grid's Input Order: it is handled after every key, press or click made before it and before every one made after it, on Blazor Server and WebAssembly alike, and one made while earlier keys are still being answered is held and carried out in its turn (ADR-0240). The Settled Mark waits for it. Nothing changes for a script that waited for each effect; one that sent such clicks at machine speed now gets the outcome it gets sending them slowly.
