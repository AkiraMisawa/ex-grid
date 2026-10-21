Product: ExSheet
Kind: Fixed
ADR: 0049

A Formula that reads a Linked Table's column whose name holds `@` (typed `=SUM(Positions[['@PV]])`) was stored as `=SUM(Positions[@PV])`, which ExSheet could not read back, so a Sheet Document holding it did not open again. The `@` is now stored escaped, `=SUM(Positions['@PV])`, as Excel's own form escapes it, and reads back as the same reference. A Sheet Document already saved with the unescaped `Positions[@PV]` still does not open: retype the Formula and save it again.
