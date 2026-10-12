Product: ExSheet
Kind: Fixed
Docs: exsheet/functions
ADR: 0047

`SUBSTITUTE` with an `instance_num` counts the occurrences at every character, overlapping ones included, as Excel does: `SUBSTITUTE("aaaa","aa","b",2)` is `aba`, where it gave `aab`.
