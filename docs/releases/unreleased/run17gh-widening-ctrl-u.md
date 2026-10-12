Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#cell-formats
ADR: 0071

Ctrl+U over a number showing `####` widens a column you have not sized, in the key's one undo step, as Excel's Ctrl+U does and as Ctrl+5 and the Number Format keys do; Ctrl+4, the same key in ExSheet, widens too, which is a reading, since no run has pressed Excel's Ctrl+4 there. A column you sized never widens, and Ctrl+B and Ctrl+I still widen nothing.
