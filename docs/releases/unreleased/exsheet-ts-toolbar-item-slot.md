Product: ExSheet
Kind: Added
ADR: 0100, 0390

ExSheet for React takes `chrome={{ toolbarItem }}`: a Toolbar Item can be drawn by a Chrome of your own, its list included, while the toolbar keeps its keys and KeyTips; answering `null` leaves an item to the built-in Chrome. The Script Core takes the same slot as a mount function (`chrome.toolbarItem`), and `@ex-grid/react` exports `GridPortals` for the family's adapters. It is published with 0.3.0.
