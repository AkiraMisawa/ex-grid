Product: ExGrid
Kind: Added
ADR: 0050

`@ex-grid/core`'s grid takes `declaredKeys` and `onDeclaredKey`, as the Blazor grid's `DeclaredKeys` and `OnDeclaredKey` (ADR-0050, item 14): keys the Consumer names in the canonical form are taken from the browser on the grid's root, its Keyboard Field or an editor surface, and raised with whether an edit is open and the Selection at the key, while the grid moves nothing and leaves an open edit as it was. A key the core answers itself, one in no canonical form, or a declaration nobody hears is refused by name.
