Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

Formulas that were refused now answer as Excel does, as Windows run 18a read it. `SORT` and `SORTBY` sort an Error Value key after numbers, text and booleans, before a blank, and first in a descending sort, and two different ones as Excel's `#DIV/0!` before `#N/A` where the readings agree; `SORT` truncates a `sort_index` that is not a whole number. The blank `FILTER`, `SORT`, `SORTBY`, `TRANSPOSE` and `UNIQUE` keep is passed over by `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT` and the other aggregates as a blank cell is, counted by `COUNTA` even taken out on its own, handed on by `IF` as it is, and sorted last by `SORT`. Each was `#VALUE!`: `=AVERAGE(FILTER(A2:A9,B2:B9="North"))` over a blank now averages the numbers.
