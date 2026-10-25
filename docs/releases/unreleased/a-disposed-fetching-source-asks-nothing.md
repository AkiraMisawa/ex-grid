Product: ExGrid
Kind: Fixed
ADR: 0025

A `GridSource.Fetch` source that has been disposed no longer fetches when it is handed a new Sort or Filter, so a grid tearing down after its Consumer disposed the source sends no question nobody can call off. A range asked for while the source's `StateChanged` is being heard, by a render that completes at once, is now the question in flight, rather than being overtaken by the one it superseded. A range that widens to the fetch in flight joins it, and the answer is held to reach the newer range, as it already was to reach the first.
