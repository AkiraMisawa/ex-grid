Product: ExSheet
Kind: Added
Docs: exsheet/formulas#supported-functions
ADR: 0047

`SUBTOTAL` and `AGGREGATE` follow what Excel was seen to do: a cell whose Formula calls either anywhere — inside a larger expression, under `IF`, or in its Spill Range's Anchor — is nested, passed over by `SUBTOTAL` and by `AGGREGATE`'s options 0 to 3 and counted by options 4 to 7; a function number or option that is not whole is truncated; and `AGGREGATE`'s array form takes a number given directly. Completion lists their function numbers with Excel's own texts.
