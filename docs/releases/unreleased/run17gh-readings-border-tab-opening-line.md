Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#format-cells
ADR: 0071

Over one cell that shows a line on exactly one of its edges, Format Cells' Border tab now opens its Style and Colour on that line, so a single press of that edge's button takes the line away. It opened on Thin and Automatic before, and the same press set a thin line there. Anywhere else it still opens on Thin and Automatic. Excel's was seen to open on the line shown for a top edge and a bottom edge, in Automatic or black, and on Thin and Automatic over a cell with a thin top and a thick right, over a range whose cells show different lines, and over a cell with no line. The rest is a reading, not yet asked of Excel: a line on a left or right edge, a style other than Thick or Thin, a colour other than Automatic or black, and every other Selection.
