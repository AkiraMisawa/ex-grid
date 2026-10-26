Product: ExSheet
Kind: Added
ADR: 0055, 0332

ExSheet in TypeScript finds and replaces on the Sheet shown, as the Blazor Sheet does: Ctrl+F finds the text a cell is shown with, a Formula's Value included, and Ctrl+H's Replace writes into the Entry of the match, so a Formula stays a Formula, while Replace All rewrites the Entries that match, Formulas' text included, as one step to undo, refusing it whole where a replaced Formula cannot be read. The column letters are never found. While the panel holds a search, the painted cells it finds wear the Match Mark, the current one apart. Searching the whole Workbook (Within) follows. `@ex-grid/core` exports `findMatches` and `replacedEntry`, the reference rules a Consumer's own Find and Replace match by, and `replacedCells` and `refusedCells`, which build the answer `onReplace` gives.
