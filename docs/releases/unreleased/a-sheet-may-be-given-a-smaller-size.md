Product: ExSheet
Kind: Added
ADR: 0460

A Sheet may be given fewer rows and columns than Excel's 1,048,576 by 16,384: `SheetSize="new(10, 10)"` on Blazor,
`sheetSize={{ rows: 10, columns: 10 }}` under React, draws `A1:J10` and nothing past it. Every move stops at its edge,
a paste, a fill or an insertion that would put a filled cell past it is refused by name, a spill past it is `#SPILL!`,
and a document with a filled cell past it is refused when opened; a smaller size given later empties the undo stack. A Reference past it reads the empty cell it names,
so a Formula means what it means in Excel. The block a refused paste runs past is now named by the Sheet's last cell,
`XFD1048576` or the size's, in the refusal's words (`{0}` in `pasteRefused.spillPastExtent`).
