Product: ExSheet
Kind: Behaviour
Docs: exsheet#a-first-sheet
ADR: 0033

The Column Headings are now the table's row 1 for a screen reader, so the grid numbers a Sheet row as its number plus one, A1 as row 2, near the top and far down alike: the number NVDA and Narrator counted near the top. Before, `aria-rowindex` held the Sheet's own number, one less. With `ShowColumnHeadings` off the two agree. What a screen reader says of a row far down is learned from users' reports (ADR-0033).
