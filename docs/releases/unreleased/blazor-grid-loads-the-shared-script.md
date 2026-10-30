Product: ExGrid
Kind: Behaviour
ADR: 0411

Blazor's ExGrid over the Script Core (`ExGrid.Components.ExGrid`) loads the core from the Shared Script the ExGrid package now ships beside its bridge, `_content/ExGrid/ex-grid-script.min.js`, which every product over the core on the page shares. A page with ExGrid alone loads 650 KB of script where it loaded 487 KB; a page with another product beside it loads the core once. A Content Security Policy that lists the package's files names `ex-grid-script.min.js` too, and a Shared Script of another build is refused with the reload notice, as another version's script is.
