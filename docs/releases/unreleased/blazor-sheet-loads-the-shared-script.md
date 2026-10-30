Product: ExSheet
Kind: Behaviour
ADR: 0411

Blazor's ExSheet loads the grid core and the data from the Shared Script the ExGrid package ships, `_content/ExGrid/ex-grid-script.min.js`, so a page with a grid and a sheet loads them once. A Content Security Policy that lists the packages' files names `ex-grid-script.min.js` too, and a Shared Script of another build is refused with the reload notice, as another version's script is.
