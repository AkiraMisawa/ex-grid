Product: ExSheet
Kind: Behaviour
Docs: exsheet#a-first-sheet
ADR: 0033

A screen reader counts the Column Headings as the table's row 1, so it names a Sheet row by its number plus one, A1 as row 2, near the top and far down alike; before, `aria-rowindex` held the Sheet's own number, one less than the number a reader counted near the top. With `ShowColumnHeadings` off the two agree.
