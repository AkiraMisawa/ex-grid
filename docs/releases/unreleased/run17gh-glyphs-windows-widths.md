Product: ExGrid
Kind: Fixed
ADR: 0016

On Windows, a Number or Date holding `ƒ`, `ť` or one of the Hebrew letters `ג` `ט` `י` `ס` `פ` is now charged what Segoe UI paints, so it shows `####` rather than a cut value. The MudBlazor Wrapper's Roboto widths take Windows' readings too, a few letters rising by up to 0.02px.
