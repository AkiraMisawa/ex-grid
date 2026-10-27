Product: ExPivot
Kind: Added
ADR: 0390, 0059

ExPivot's Script Core and `@ex-grid/pivot-react` take Show Details' sheet tabs (`detailsTabs`) and the contents of its dialog (`detailsDialog`) as Chrome slots, drawn in React with `usePivotPart`. A tab's control is handed in by its `key` and the tab that opens takes the keyboard; the dialog hands in Close, which takes the keyboard as it opens, and the element its records stand in. The records' grid, the dialog's modal frame, its backdrop and Escape stay the pivot's. It is published with 0.3.0.
