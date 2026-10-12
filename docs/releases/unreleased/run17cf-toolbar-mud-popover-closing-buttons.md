Product: ExGrid
Kind: Added
Docs: exgrid/appearance#built-in-chrome-or-mudblazor-s
ADR: 0010, 0044

A button of a Chrome's filter panel, or of your own popover's contents, that closes the popover (OK, Apply, Cancel) is marked with the class `ex-popover-closes`, and the keys typed while it closes are held and handed on where the keyboard lands, as after a command run in a menu: on a Blazor Server circuit ↓ typed straight after Enter on OK was lost on the closing button, and Enter pressed it again. The built-in panels, Format Cells under the built-in Chrome and `ExGrid.MudBlazor`'s filter panel carry it; a Chrome of your own adds it to its buttons that close.
