Product: ExGrid
Kind: Added
ADR: 0005

Ctrl+Shift+C (Command+Shift+C on a Mac) copies the selection with its header row, as the Context Menu's Copy with headers does, on every grid that offers the item: React, Mantine, MUI and Blazor's `ExGrid.Components.ExGrid`. A grid given `hideCopyWithHeaders` (`HideCopyWithHeaders`) leaves the key to the browser, and declaring it on a grid that offers the item is refused at mount.
