Product: ExSheet
Kind: Fixed
ADR: 0100, 0071

On the Blazor Server host, giving the keyboard back from the Sheet Toolbar, and opening `ExSheet.MudBlazor`'s Format Cells, no longer logs Chromium's "Blocked aria-hidden on an element because its descendant retained focus" while the browser's accessibility tree is on, as it is under a screen reader: the empty element the keyboard passes through on its way out of a MudBlazor list or into Format Cells is no longer `aria-hidden`, and the toolbar's band now gives the keyboard back from itself, through no element of its own.
