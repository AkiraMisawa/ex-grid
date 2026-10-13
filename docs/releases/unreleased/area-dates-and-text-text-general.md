Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0120

`TEXT` shows a negative number in `General` with its minus sign beside General's 11 characters, and a number under a lone `@` as `General` shows it, as Excel does: `TEXT(-1/3,"General")` is `-0.333333333` and `TEXT(1/3,"@")` is `0.333333333`, where both gave `#VALUE!`.
