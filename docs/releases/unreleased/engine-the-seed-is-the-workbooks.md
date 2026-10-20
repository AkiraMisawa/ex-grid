Product: ExSheet
Kind: Breaking
ADR: 0180

`sheet.Seed`, `Changes` and `SetSeed` are now the `Workbook`'s members of the same names, and `SetSeed` answers a `WorkbookChange`. A change on any Sheet now draws `RAND` and `RANDBETWEEN` again on every Sheet, as Excel does, and a draw names its Sheet, so the same seed and count give different draws than before and `=RAND()` at the same cell of two Sheets draws two numbers.
