Product: ExGrid
Kind: Breaking
Docs: exgrid/clipboard#copy
ADR: 0005, 0201

`ClipboardData.Assemble` takes a header's raw form, `headerRawAt`, for the HTML flavour of a copy with headers, so Excel reads a rounded numeric header in full while plain text keeps its shown form; a header with no raw form and ExGrid's own grids copy as before. The optional fifth parameter replaces the four-parameter signature: rebuild calling assemblies, and replace a four-argument method-group conversion with a lambda or a five-argument delegate; direct calls can omit the new argument.
