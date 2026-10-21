Product: ExSheet
Kind: Fixed
ADR: 0047

A Formula chaining thousands of operators, such as `=1+1+…+1` with 4,095 additions, which fits Excel's 8,192 characters, is computed; before, it overflowed the stack when it was recalculated and ended the process, on Blazor Server the whole server. Functions nested more than 64 levels deep, Excel's limit, and a Formula nested more than 256 levels deep (parentheses, signs, functions) are now refused as they are typed, with the reason, and a Formula the host's stack still cannot hold is refused with `#VALUE!` rather than crash.
