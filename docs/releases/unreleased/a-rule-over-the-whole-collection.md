Product: ExGrid
Kind: Added
ADR: 0381

`GridRuleset` takes rules over the whole collection: `new GridRuleset<TRow>(rowKey).Set(rule)`, where a rule answers Breaches that name their cells by Row Key. After each `Evaluate`, every cell a Breach names wears `Error` with its message, inside the Window and outside it; `Breaches` lists what stands, for the Consumer's save to refuse; and `FindError` answers the next or the previous error cell in the grid's order. A set rule on a ruleset made without a Row Key function throws.
