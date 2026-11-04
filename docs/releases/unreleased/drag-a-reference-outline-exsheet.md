Product: ExSheet
Kind: Added
Docs: exsheet/formulas#completion--hints-and-pointing
ADR: 0480

While a Formula is edited, a Reference Outline can be dragged as in Excel: by its edge to move the Reference, and by the
square at each of its corners to resize it. The Formula is rewritten in the Reference's own form (`$A$1` moved is `$C$1`),
and Escape during the drag puts the Reference back.
