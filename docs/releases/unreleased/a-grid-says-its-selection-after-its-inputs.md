Product: ExGrid
Kind: Added
ADR: 0240

`SelectionAfterInputsAsync` gives the selection the grid holds once it has answered every input it received so far. A control outside the grid that acts on the selection reads it there: `SelectionChanged` is raised after the render that paints it, a round trip later on the Server host, so a button pressed straight after a click would otherwise act on the selection before it.
