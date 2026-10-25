Product: ExSheet
Kind: Fixed
ADR: 0047

Text that only looks like a date, a time or a Reference stays text, as Excel keeps it. A date or a time typed with a line break after it (`1/2`, then Alt+Enter) was read as the date; one typed in digits other than ASCII's (`١/٢`) threw instead of entering; `INDIRECT` read R1C1 text with a line break after it as the cell; and a table name ending in a line break was declared. A `SORT` or `UNIQUE` over text holding an emoji threw; it now sorts the emoji among the symbols.
