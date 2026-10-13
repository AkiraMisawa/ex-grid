Product: ExGrid
Kind: Fixed
ADR: 0051

With `CycleReference` declared, F4 after the cycle had brought the text back to the very text the user had moved the caret in (`=SUM(A:A)`, F2, ←, F4 twice) no longer takes the caret the browser left at the end for the user's, so a third F4 cycles the Reference again (`=SUM($A:$A)`) where it changed nothing.
