Product: ExGrid
Kind: Fixed
Docs: exgrid/appearance
ADR: 0029

In `@ex-grid/mantine`, a grid given `highlightOnHover` hid every value in the row under the pointer: the hover band took
Mantine's Table hover shade, which is opaque, and the band lies over the cells' text. It is now that shade laid
translucently over the body, in both schemes, so the row reads as before and its values show through.
