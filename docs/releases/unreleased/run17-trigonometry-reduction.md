Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#supported-functions
ADR: 0047

`SIN`, `COS`, `TAN`, `COT`, `CSC` and `SEC` now give Excel's doubles to the last bit, on every runtime: an angle is reduced by pi as Excel holds it, to 64 bits rather than a double's 53, so `SIN(PI())` is 1.22514845490862E-16, `COS(PI()/2)` 6.1257422745431E-17 and `TAN(PI()/2)` 16324552277619072, as in Excel, where they were 1.2246467991473532E-16, 6.123233995736766E-17 and 16331239353195370. Other results move by the difference between the two pi: by at most a unit or two in the last digit for most angles, from about the twelfth digit for an angle near 2^27, and as early as the fourth for a value within about 1E-15 of 0.
