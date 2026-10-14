Product: ExGrid
Kind: Behaviour
Docs: exgrid/selection#the-keyboard
ADR: 0033

The rows a screen reader is given now count the header's rows first: the header row is `aria-rowindex` 1, each tier of Header Groups is a row of its own above it, and `aria-rowcount` is the data rows' total plus those rows. So the grid writes a row's number the same way near the top, where NVDA and Narrator counted the rows before it, and far down; Chromium's accessibility tree is what checks it, and what a screen reader says of a row far down is learned from users' reports (ADR-0033). A test or script that reads `aria-rowindex` as a row's position, or `aria-rowcount` as the number of rows, now subtracts the header's rows (none while `HideHeader` is set); ExPivot's report counts its column Items' rows the same way.
