Product: ExGrid
Kind: Breaking
Docs: exgrid/sorting-filtering#find-all
ADR: 0470

`GridFindWords` has a new required `FindAll` member, the Find All button's words: a Chrome that builds one sets it, or
it no longer compiles (CS9035); a Chrome that reads `GridFindView.Words` changes nothing.
