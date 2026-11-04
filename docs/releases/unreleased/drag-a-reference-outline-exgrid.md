Product: ExGrid
Kind: Added
Docs: exgrid#a-grid-drawn-in-the-browser
ADR: 0480

A grid that outlines a Formula's References takes `moveReference` (Blazor: `MoveReference`, a script function): with it,
an outline's edges and corner squares can be dragged to move or resize the Reference, and the grid writes the text the
function answers for the range reached.
