Product: ExGrid
Kind: Added
ADR: 0350

`@ex-grid/core` and `@ex-grid/react`, new npm packages, are ExGrid in TypeScript and its React component: the same grid the Blazor component draws, held to the C# core by the cases under `tests/cases/` and by ExGrid's Playwright specs run on a React host. The grid so far takes the Selection, the Cell Editor with IME composition, copy, paste and clear, the Overwrite Notice, the hover and Focus bands (`highlightHoverRow`, `highlightFocusRow`), and the header's drags: a resize from a column's grip or Size to fit by double-clicking it (`onColumnWidthChanged`), and a reorder of a column or a Header Group (`onColumnOrderChanged`), under which a press on a header grabs the column rather than selecting it. A column's width is declared as a number, `{ px, minPx, maxPx }` or `{ auto: true, minPx, maxPx }`. They are published with 0.3.0.
