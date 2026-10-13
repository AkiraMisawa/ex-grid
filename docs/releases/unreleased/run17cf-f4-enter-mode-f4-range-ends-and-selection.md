Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#completion--hints-and-pointing
ADR: 0051

F4 cycles the Reference as Excel does: in a range only the end at the caret cycles, from its own form (`=SUM(A1:B2)` with the caret after `B2` gives `=SUM(A1:$B$2)`, where both ends cycled before), and the rewritten Reference's column letters are written in upper case (`=b2` gives `=$B$2`). After the caret is moved with a key or a press, or over a selection, the rewritten Reference is left selected, so the next F4 cycles it again; with the caret where typing left it, in the cell or in the Formula Bar, the caret stands at its end as before. While pointing, a pointed range still cycles whole (`=D11:D12` gives `=$D$11:$D$12`), as Excel's did.
