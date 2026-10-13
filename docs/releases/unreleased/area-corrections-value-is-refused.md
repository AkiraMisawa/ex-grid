Product: ExSheet
Kind: Added
Docs: exsheet/formulas#supported-functions
ADR: 0047

`Value.IsRefused` says whether a cell's `#VALUE!` is a Refused Formula's — a Formula ExSheet cannot answer as Excel does — rather than Excel's own `#VALUE!`. The two are equal as Values and shown alike; a refusal pushed into a Linked Table refuses every Formula that reads its column.
