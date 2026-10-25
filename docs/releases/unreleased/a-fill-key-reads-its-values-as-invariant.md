Product: ExGrid
Kind: Fixed
ADR: 0050

Ctrl+D and Ctrl+R now mark each value of their `GridPasteIntent` as `PasteFieldOrigin.Invariant`, the raw value it is, where they marked it `ShownText`: a Consumer reading `OriginFor` under a culture whose decimal separator is a comma read `1234.5` as another number. A filter condition reopened over a date with a fraction of a second is now written in ISO's T form with the fraction (`2026-01-05T10:00:00.25+09:00`), where it dropped the fraction and was applied again a fraction off; a date and time is written `2026-01-05T10:00:00.25`, and that form is read exactly in every culture.
