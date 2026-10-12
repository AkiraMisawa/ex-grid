Product: ExGrid
Kind: Fixed
ADR: 0002

A `RowRange` written with `System.Text.Json` now reads back as the rows it held, through its constructor's own checks; it read back as no rows at 0.
