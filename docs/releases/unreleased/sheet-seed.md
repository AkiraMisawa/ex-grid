Product: ExSheet
Kind: Added
Docs: exsheet/formulas#supported-functions
ADR: 0180

`Seed` fixes the Sheet Seed that `RAND` and `RANDBETWEEN` draw from, so a Sheet shows the same draws whenever it is opened, and `Sheet.SetSeed` gives the engine one on its own. Without `Seed`, ExSheet chooses a seed when it opens a document and a prerender chooses none; the draws change at each edit, insertion, deletion, undo, redo, Linked Table snapshot and Linked Table declaration, never with the clock or a rename.
