Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#format-cells
ADR: 0071

Over one cell that shows a line on exactly one of its edges, Format Cells' Border tab now opens its Style and Colour on that line, as Excel's does, so a single press of that edge's button takes the line away. It opened on Thin and Automatic before, and the same press set a thin line there. Anywhere else it still opens on Thin and Automatic.
