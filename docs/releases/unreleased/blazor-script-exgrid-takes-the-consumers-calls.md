Product: ExGrid
Kind: Added
ADR: 0441

`ExGrid.Components.ExGrid` takes `ReadViewAsync` and `ShowViewWith` (with or without a place), which keep each set of rows' view when a grid shows several in turn, `ReadEditTextAsync`, which reads the open edit's text, and `InOrderAsync`, `CancellableInOrderAsync` and `SelectionAfterInputsAsync`, which take a place in the grid's Input Order (ADR-0441). Each crosses to the browser, which holds the view, the open edit and the order: `ShowViewWith` is synchronous and crosses ahead of the state that brings its rows, so hand it before the source says the rows changed. Called from a handler of the grid's own events, `InOrderAsync` runs at once as part of the event, and `CancellableInOrderAsync` is refused by name. In TypeScript, `GridHandle.showViewWith` takes the place `showNewView` takes, and either reveals the Focus it places.
