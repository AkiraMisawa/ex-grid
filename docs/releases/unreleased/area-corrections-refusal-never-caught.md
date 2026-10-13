Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`IFERROR` and the error tests no longer catch a refusal, anywhere: where ExSheet cannot answer a Formula as Excel does and shows `#VALUE!` in its place, every Formula that reads that cell — directly, through a range, a `LET` name, `INDIRECT`, `OFFSET`, a Spill Range or a Linked Table — is refused in turn, whatever it does with it, as one that reads a cycle is `#CIRC!`. `=IFERROR(B1,0)` over a refused B1 is `#VALUE!` where it used to be 0, and so are `ISERROR`, `ERROR.TYPE`, `IFNA`, `COUNT`, `COUNTA` and `AGGREGATE`'s options that pass over errors, in the same Formula as the refusal or in any other; Excel's own `#VALUE!`, such as `=1+"a"`'s, is caught as before. A copy writes a refused cell as `#VALUE!`; a copy of one range pastes its Formula, and a copy of several ranges or with the headers that holds one is refused when pasted back into the Sheet, and `Entry.FromValue` refuses its Value.
