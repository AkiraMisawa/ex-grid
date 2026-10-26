Product: ExSheet
Kind: Behaviour
ADR: 0047, 0370

EXP, LN, LOG, LOG10, a whole POWER, ASIN, ACOS, ATAN, ATAN2 and the hyperbolic functions are now the exact value rounded once to the nearest double, computed by the engine rather than the platform's math library, so the C# engine and `@ex-grid/sheet-engine` give one double on every platform. A result can differ from before in its last bit: glibc rounds `=ATAN(3.8946)` to 1.31946007910341, and it is 1.3194600791034101.
