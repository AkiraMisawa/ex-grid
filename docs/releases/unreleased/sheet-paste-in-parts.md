Product: ExSheet
Kind: Added
Docs: exsheet/formulas#the-engine-on-its-own
ADR: 0048, 0180

`SheetEdit.Paste(block, targets)` pastes a copied block over several ranges, and `SheetEdit.PasteText(fields, entries)` pastes typed fields with Entries kept exactly, each as one step on the undo stack and one change, so `RAND` and `RANDBETWEEN` draw once for it. ExSheet's own pastes of that kind, and their undo and redo, now draw once too.
