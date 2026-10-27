Product: ExGrid
Kind: Added
ADR: 0350, 0390

ExGrid for React's `chrome` prop takes `formulaBarText` and `nameBox`: the Formula Bar's text field and its Name Box can be drawn by a design system of your own, from `useFormulaBarText(context)` and `useNameBox(context)`, while what the fields show, their keys and Enter's form stay the grid's. The Script Core takes the same slots as mount functions. It is published with 0.3.0.
