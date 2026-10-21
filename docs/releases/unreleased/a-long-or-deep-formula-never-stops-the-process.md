Product: ExSheet
Kind: Fixed
ADR: 0047

A Formula chaining thousands of operators, such as `=1+1+…+1` with 4,095 additions, which fits Excel's 8,192 characters, is computed; before, it overflowed the stack when it was recalculated and ended the process, on Blazor Server the whole server. A Formula the host's stack still cannot hold is refused, as it is typed or with `#VALUE!`, rather than crash.
