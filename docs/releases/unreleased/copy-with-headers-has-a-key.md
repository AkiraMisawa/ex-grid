Product: ExGrid
Kind: Added
ADR: 0005

Ctrl+Shift+C (Command+Shift+C on a Mac) copies the selection with its header row, as the Context Menu's Copy with headers does, on every grid drawn by the Script Core that offers the item: React, Mantine, MUI and Blazor's `ExGrid.Script.ExGrid`. A grid given `hideCopyWithHeaders` leaves the key to the browser, and declaring it on a grid that offers the item is refused at mount. Blazor's C# core grid keeps Copy with headers on its menu only.
