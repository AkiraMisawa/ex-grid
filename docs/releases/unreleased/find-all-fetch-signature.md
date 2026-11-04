Product: ExGrid
Kind: Breaking
Docs: exgrid/sorting-filtering#find-all
ADR: 0470

`GridSource.Fetch` (and `FetchingGridSource`'s constructor) take a new optional `findAll` parameter at the end. Source
that calls it compiles unchanged; a library compiled against the old signature is rebuilt.
