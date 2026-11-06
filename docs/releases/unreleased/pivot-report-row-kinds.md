Product: ExPivot
Kind: Fixed
Docs: expivot/layout#report-forms--subtotals-and-grand-totals
ADR: 0024

A report's group rows, subtotals and grand total are set apart again, in bold, a group row on a tint and a total under a
rule, and a press on a report header selects its column, as `0.1.0` did; since the report moved onto the Script Core
every row was painted as a detail row and a header press did nothing.
