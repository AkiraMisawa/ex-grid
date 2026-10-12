Product: ExSheet
Kind: Fixed
Docs: exsheet/formulas#supported-functions
ADR: 0047

`MOD` gives the exact remainder of Microsoft's `number - divisor*INT(number/divisor)`, rounded once, as Excel does: `MOD(123456789012,0.7)` is 0.5000078322611261, where evaluating it in doubles gave 0.5000152587890625. Where the double quotient rounds up to a whole number the exact one is below, the answer is now about the divisor rather than 0 — `MOD(1,0.1)` shows 0.1, as the exact remainder 0.09999999999999995 — and Excel has not been asked those yet.
