Product: ExGrid
Kind: Fixed
ADR: 0005

A large paste on a slow Blazor Server connection now completes in seconds: the clipboard's text crosses gzip-compressed, taken from the browser when the grid reaches the paste, instead of in one round trip per 31 KB. Before this, a ~10 MB paste at a 150 ms round trip took close to a minute and could be dropped by the server's stream timeout. An `OnPaste`, `OnFill` or `OnClear` handler that changes the Grid Source row by row no longer has the grid re-apply it once per row.
