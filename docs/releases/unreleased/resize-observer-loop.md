Product: ExGrid
Kind: Fixed
Docs: exgrid/appearance#density-and-row-height
ADR: 0013

On WebAssembly a grid no longer raises "ResizeObserver loop completed with undelivered notifications" on the page's `window` as its scrollbar appears. The grid's reports of its Scrollbar Gutter and its Layout Ceiling are now told from a task of their own after the browser's observation, rather than inside it, where the render they caused moved the observed box again. Seen at attach on a Stretch-width grid with a Header Group; the Server host never raised it. ExSheet and ExPivot, drawn by ExGrid, are fixed with it.
