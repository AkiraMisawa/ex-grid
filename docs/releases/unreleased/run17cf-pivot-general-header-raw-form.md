Product: ExGrid
Kind: Added
Docs: exgrid/clipboard#copy
ADR: 0005

`ClipboardData.Assemble` takes a header's raw form, `headerRawAt`, for the HTML flavour of a copy with headers, as `rawAt` is a cell's: a header that shows a number rounded can carry the number in full where Excel reads it, and its shown text stays in the text flavour. A header with no raw form copies as before, and ExGrid's own grids copy exactly as they did.
