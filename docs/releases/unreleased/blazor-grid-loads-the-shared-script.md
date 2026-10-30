Product: ExGrid
Kind: Behaviour
ADR: 0411

Blazor's ExGrid loads the TypeScript core from the Shared Script the ExGrid package ships beside its bridge, `_content/ExGrid/ex-grid-script.min.js`, which every product over the core on the page shares, so a page with another product beside the grid loads the core once. A Content Security Policy that lists the package's files names `ex-grid-script.min.js` and `ex-grid-core.min.js` in place of `ex-grid.min.js`, and a Shared Script of another build is refused with the reload notice, as another version's script is.
