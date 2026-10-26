Product: ExSheet
Kind: Fixed
ADR: 0047, 0370

A number whose exact value has a 5 as its sixteenth significant digit is now read at 15 digits half away from zero, as ADR-0047 decided, everywhere the engine reads 15 digits: General shows `=1+2^-15` as `1.00003051757813`, not `1.00003051757812`, and `=1+2^-15=1.00003051757813` is TRUE. A SUMPRODUCT that overflows is `#NUM!`, and a number that a percent format scales past the largest double cannot be shown (`TEXT` of it is `#VALUE!`); each of these used to stop the edit with an exception.
