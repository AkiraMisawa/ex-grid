Product: ExPivot
Kind: Behaviour
Docs: expivot/values#number-formats-and-----
ADR: 0060

A number Item whose field has no `format` is labelled in Excel's General, as a value with no number format is shown: at most eleven characters besides a minus sign, where up to 15 significant digits were shown, so `1.23456789012345` reads `1.23456789` and `123456789012.345` reads `1.23457E+11`, in the report and in Filter…'s list alike. A negative Item takes `-` in every culture, where sv-SE, fi-FI and nb-NO wrote `−` (U+2212). The Item itself, and a Hidden Item's key, are unchanged; a Consumer that wants more digits gives the field a `format`.
