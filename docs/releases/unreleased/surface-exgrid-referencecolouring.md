Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Cells.ReferenceColouring` is no longer public: it is how the grid hands out the Reference Outline palette to an editor's References (ADR-0057). A Consumer is told the colour each key was given as a `ReferenceKeyColour`, and nothing replaces the type.
