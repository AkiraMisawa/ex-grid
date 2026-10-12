Product: ExSheet
Kind: Breaking
Docs: exsheet/formulas#supported-functions
ADR: 0047

A Formula that gives `RANK.EQ`'s `ref`, `SUBTOTAL`'s references or `AGGREGATE`'s fifth and later arguments something that can never be a Reference — a constant, an operator's result such as `A1:A3*1`, or a call such as `SEQUENCE(3)` — is now refused when it is entered, as Excel refuses it; `RANK.EQ(1,1)` used to show `#VALUE!`. A Sheet Document holding such a Formula is refused when it is read (`SheetDocumentException`, naming the Formula), and pasted it is text, so give the argument a Reference before opening such a document.
