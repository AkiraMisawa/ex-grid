Product: ExBoard
Kind: Added
ADR: 0370, 0065, 0340

`ExBoard.Components.ExBoard` draws a Board with ExBoard's Script Core in the browser on both hosting models, beside the Razor `ExBoard`, taking the same `Board`, `Data`, `Width`, `Culture`, `OnOutput`, `Diff` and `OnDiff`. Each input's Snapshot crosses to the browser once, as its Arrow stream, so the `ExBoard` package now depends on `ExGrid.Data.Arrow` and ships its script. An output is read back against the Snapshot it was taken from. `Sources` and the Chromes are not carried yet.
