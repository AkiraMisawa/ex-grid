Product: ExGrid
Kind: Added
ADR: 0350, 0390

ExGrid for React's `chrome` prop takes `filterPanel`: a column's filter panel can be drawn by a design system of your own, from `useFilterPanel(context)`, while what each choice means, what OK applies and where the keyboard goes stay the grid's. The Script Core takes the same slot as a mount function, and hands it the panel's view and setters. It is published with 0.3.0.
