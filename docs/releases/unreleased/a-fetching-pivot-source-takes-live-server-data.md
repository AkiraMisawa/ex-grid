Product: ExPivot
Kind: Added
ADR: 0370

`@ex-grid/pivot-engine`'s `FetchingPivotSource` takes live data over a transport the Consumer owns: its fetchers' `subscribe` hands it either a `changed` notice, which asks the question again, or a `changeBatch` naming the records the data lost and gained, which the engine folds into the answer it holds where counts, exact sums and extremes allow and asks the server again where they do not. A `LocalPivotReportSource` over it tells its listeners each notice. It is published with 0.3.0.
