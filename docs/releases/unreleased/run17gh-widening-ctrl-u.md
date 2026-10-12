Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#cell-formats
ADR: 0071

Ctrl+U (and Ctrl+4) over a number showing `####` widens a column you have not sized, as Ctrl+5 and the Number Format keys do, in the key's one undo step, as Excel's does. A column you sized never widens, and Ctrl+B and Ctrl+I still widen nothing.
