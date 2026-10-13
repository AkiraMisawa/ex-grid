Product: ExGrid
Kind: Fixed
ADR: 0016

On Windows under a Japanese display language, where the grid's font is Yu Gothic UI, a Number or Date holding `₦`, `₱`, `₡`, `฿`, `ď` or the Cyrillic `Ѐ`, `ѐ` or `ѝ` is now charged what Yu Gothic UI paints, so it shows `####` rather than a cut value.
