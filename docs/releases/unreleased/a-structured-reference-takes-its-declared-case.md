Product: ExSheet
Kind: Behaviour
ADR: 0049

A structured reference is stored in its Linked Table's declared case, the table's name and the column's, as Excel writes one back: `=sum(limit[line])` over `LIMIT` with a column `Line` is stored, shown and saved as `=SUM(LIMIT[Line])`. A Formula typed before its table is declared keeps the case it was typed in.
