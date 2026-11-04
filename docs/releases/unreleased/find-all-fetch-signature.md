Product: ExGrid
Kind: Breaking
Docs: exgrid/sorting-filtering#find-all
ADR: 0470

`GridSource.Fetch` (and `FetchingGridSource`'s constructor) take a new optional `findAll` parameter at the end. Source
that calls it compiles unchanged; a library compiled against the old signature is rebuilt.

`GridFindWords` has a new required `FindAll` member, the Find All button's words: a Chrome that builds one sets it,
or it no longer compiles (CS9035). The words the grid sends carry it, so a Chrome that reads `GridFindView.Words`
changes nothing.
