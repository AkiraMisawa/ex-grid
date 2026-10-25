Product: ExPivot
Kind: Fixed
ADR: 0066

`PivotJson` no longer rounds an exact number it reads: a sum, an extreme or a record's value past the 28 places a decimal holds (`0.12345678901234567890123456789`), too small for one (`1e-29`, which was read as 0) or past 2^96 is refused by name with a `FormatException`, as a number that is not one already was. A number written with an exponent (`1.5E2`) still reads as the decimal it writes.
