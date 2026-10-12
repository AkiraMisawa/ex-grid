Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047, 0124

A Formula whose `OFFSET` or `INDIRECT` reaches its own cell, or a cell in a circular reference, is now `#CIRC!`, and one that reaches data still on its way waits with `#GETTING_DATA`, even under `COUNT`, `COUNTA` or `IFERROR`, as a Formula that names those cells does. An `OFFSET` that did so used to show a number.
