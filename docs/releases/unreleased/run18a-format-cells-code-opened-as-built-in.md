Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting
ADR: 0071

A Sheet Document opened under a culture whose Format Cells spells a built-in Number Format as a code of its own the document holds reads that code as the built-in, as Excel does: `d-mmm-yy` typed under en-GB, opened under en-US, is built-in 15.
