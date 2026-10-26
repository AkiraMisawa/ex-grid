Product: Data
Kind: Fixed
ADR: 0064

A CSV Date column declared with the round-trip format `o` (or `O`) now holds a value with an offset as the clock it shows, as every other format with an offset does: `2026-09-30T14:05:09.1234567+09:00` reads as 14:05:09, where it was read as 05:05:09, the instant in UTC.
