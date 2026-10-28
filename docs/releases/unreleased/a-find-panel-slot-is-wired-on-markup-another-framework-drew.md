Product: ExGrid
Kind: Added
ADR: 0410

In the TypeScript core, `wireFindPanel`, `takeFindFocus` and `findPanelAttributes` wire a find panel slot's controls on markup another framework drew: what is typed in the find and Replace fields and each option ticked goes to the panel's context in the browser, and each marked button steps or writes. React's `useFindPanel` now answers its focus requests through `takeFindFocus`.
