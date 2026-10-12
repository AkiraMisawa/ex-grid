Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#completion--hints-and-pointing
ADR: 0051

F4 cycles the Reference as Excel does: in a range only the end at the caret cycles, from its own form (`=SUM(A1:B2)` with the caret after `B2` gives `=SUM(A1:$B$2)`, where both ends cycled before), and the rewritten Reference's column letters are written in upper case (`=b2` gives `=$B$2`). In Caret, Excel's Edit mode — after F2 in a cell, and in the Formula Bar until F2 takes it to Enter mode — the rewritten Reference is left selected, so the next F4 cycles it again; typed into a cell, after F2 in the Formula Bar, and while pointing, the caret stands at its end as before.
