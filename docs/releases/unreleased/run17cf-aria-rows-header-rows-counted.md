Product: ExGrid
Kind: Behaviour
Docs: exgrid/selection#the-keyboard
ADR: 0033

A screen reader now counts the header's rows before the data rows: the header row is `aria-rowindex` 1, each tier of Header Groups is a row of its own above it, and `aria-rowcount` is the data rows' total plus those rows, so a row is read by the same number near the top and far down. A test or script that reads `aria-rowindex` as a row's position, or `aria-rowcount` as the number of rows, now subtracts the header's rows (none while `HideHeader` is set); ExPivot's report counts its column Items' rows the same way.
