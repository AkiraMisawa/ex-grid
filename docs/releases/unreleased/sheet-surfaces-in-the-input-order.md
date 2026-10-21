Product: ExSheet
Kind: Behaviour
ADR: 0240
Docs: exsheet/formatting

The Sheet's own surfaces now take their place in its Input Order, on Blazor Server and WebAssembly alike: a press on a Sheet Toolbar item or a key on the toolbar, a press on a Sheet Tab or a key on the tab strip, Format Cells' OK and Cancel under the built-in Chrome, and the keyboard coming into the Sheet or leaving it (which a Pointing Scope listens to). What is typed straight after any of them is taken once it has been answered, where it left the keyboard — Bold pressed and ↓ typed at once set bold on the Focus cell and then move the Focus; a press on a tab and `77`, Enter at once land on the Sheet shown. The Sheet's outermost element carries `data-ex-input-root`. Under ExSheet.MudBlazor, Format Cells' dialog and the toolbar's MudBlazor lists keep the holds they had.
