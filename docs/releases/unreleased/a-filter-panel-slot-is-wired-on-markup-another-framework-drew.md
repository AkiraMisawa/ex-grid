Product: ExGrid
Kind: Added
ADR: 0410

In the TypeScript core, `wireFilterPanel`, `answerValueListKey`, `takeFilterFocus` and `filterPanelAttributes` wire a filter panel slot's controls on markup another framework drew: each change goes to the panel's context in the browser, and a value's checkbox names the entry of the view its contents were drawn from, so the value the user saw is the one ticked. React's `useFilterPanel` now answers its value list's keys and its focus requests through them.
