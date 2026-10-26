Product: ExSheet
Kind: Fixed
ADR: 0047

Text that only looks like a date, a time, a Reference or a table name stays text: a line break after it, or digits other than ASCII's (`١/٢`, which threw), no longer make it one, on entry, in a pasted invariant field or in `INDIRECT`. Text holding a character outside the Basic Multilingual Plane no longer throws when sorted or compared but orders in its own class (an emoji among the symbols, `𠮷` among the letters), and the fill handle recognises a day or month name the same way on every platform, composed or decomposed and through invisible characters, where it used the platform's collation.
