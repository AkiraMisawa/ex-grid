Product: ExSheet
Kind: Fixed
ADR: 0047

Text that only looks like a date, a time or a Reference stays text, as Excel keeps it. A date, a time or both typed with a line break after them were read as the date; one typed in digits other than ASCII's (`١/٢`) threw instead of entering, and an invariant clipboard field like it refused the paste; `INDIRECT` read R1C1 text with a line break after it as the cell; and a table name ending in a line break was declared. A `SORT`, a comparison or a lookup over text holding a character outside the Basic Multilingual Plane threw; such a character now orders in its own class, as every other character does: an emoji among the symbols, `𠮷` among the letters.

A day or month name that refuses the fill handle's copy is recognised the same way on every platform: in either case, composed or decomposed, and through a soft hyphen or a zero-width character a paste can carry, where it was the platform's collation before.
