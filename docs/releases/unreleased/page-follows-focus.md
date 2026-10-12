Product: ExGrid
Kind: Behaviour
Docs: exgrid/selection#the-page-follows-the-focus
ADR: 0204

A move the grid makes, and the editor opening, now bring the cell into the window at the nearest edge, so a grid taller than the window scrolls the page with the Focus; a resize never does. A page with a fixed bar sets `scroll-padding` on its scrolling element, or a cell may be brought in under the bar.
