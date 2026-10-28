Product: ExPivot
Kind: Behaviour
ADR: 0411

Blazor's ExPivot over the Script Core (`ExPivot.Script.ExPivot`) no longer carries a copy of ExGrid's core: it imports it from the Shared Script the ExGrid package ships, `_content/ExGrid/ex-grid-script.min.js`, so its own script is 426 KB where it was 899 KB, and a page with an ExGrid beside it loads the core once (1.06 MB of script where it was 1.39 MB). A Content Security Policy that lists the packages' files names it too, and a Shared Script of another build is refused with the reload notice.
