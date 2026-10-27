Product: ExSheet
Kind: Added
ADR: 0100

In TypeScript and React, a toolbar button of your own takes `content`, such as an icon, drawn in place of its text, which still names it, as Blazor's `ToolbarButton` takes child content. A `chrome.toolbarItem` slot is handed it as `view().content`.
