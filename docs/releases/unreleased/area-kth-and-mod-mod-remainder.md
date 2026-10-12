Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`MOD` gives the exact remainder of Microsoft's `number - divisor*INT(number/divisor)`, rounded once, wherever every evaluation that gives Excel's observed answers agrees: `MOD(123456789012,0.7)` is 0.5000078322611261, as Excel's, where it gave 0.5000152587890625, and `MOD(10,0.3)` is 0.10000000000000037, where it gave 0.09999999999999964. Where those evaluations part, `MOD` is `#VALUE!` until Excel is asked, where it gave a number: where the double quotient rounds up onto a whole number, as in `MOD(1,0.1)` and `MOD(5,0.1)`, which gave 0, and `MOD(3.3,0.01)` and `MOD(0.7,0.01)`, which gave -4.440892098500626E-16 and -1.1102230246251565E-16; where the remainder is not 0 but within 2^-49 of the number, as in `MOD(1.1,0.1)`, which gave 0; and, rarely, where the remainder plus the divisor rounds to the divisor or would round twice in an x87 register.
