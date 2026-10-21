Product: ExGrid
Kind: Fixed

Text typed into the Cell Editor, Formula Bar or Name Box is taken before the next grid command,
even when it follows immediately on Blazor Server. The Settled Mark now covers typing in these
fields, so automation can wait for the mark and read once. The grid's hidden script-interop
signatures change; Consumers should load the script shipped with the same package version.
