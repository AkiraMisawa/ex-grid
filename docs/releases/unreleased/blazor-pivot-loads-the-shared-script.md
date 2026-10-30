Product: ExPivot
Kind: Behaviour
ADR: 0411

Blazor's ExPivot imports ExGrid's TypeScript core from the Shared Script the ExGrid package ships, `_content/ExGrid/ex-grid-script.min.js`, so a page with an ExGrid beside it loads the core once. A Content Security Policy that lists the packages' files names it too, and a Shared Script of another build is refused with the reload notice.
