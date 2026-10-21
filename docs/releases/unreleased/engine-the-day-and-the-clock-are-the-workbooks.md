Product: ExSheet
Kind: Breaking
ADR: 0331

`sheet.Today`, `SetToday`, `NowSource`, `RecalculateVolatile` and `HasVolatileFormulas` are now the `Workbook`'s members of the same names, which answer for every Sheet, so a Sheet added or copied reads the Sheet Day too, where it showed `#GETTING_DATA` before.
