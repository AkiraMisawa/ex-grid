Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#cell-formats
ADR: 0046

A column inserted to the right of one a typed number widened now takes that width as one the user set, as Excel's file holds it, so a longer number typed into the new column is fitted to it rather than widening it; the column to its left still widens. A Sheet Document saved before keeps the kinds it recorded.
