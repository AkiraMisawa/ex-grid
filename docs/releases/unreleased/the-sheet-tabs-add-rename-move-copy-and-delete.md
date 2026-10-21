Product: ExSheet
Kind: Added
ADR: 0331

The Sheet Tabs add, rename, move, copy and delete Sheets: `+` adds one after the Sheet shown and Shift+F11 one before it; a double click edits a tab's name in place (Enter commits, Escape keeps the old one, a name Excel refuses is refused by name); a tab's menu (a right-click, Shift+F10 or the menu key) offers Rename, Delete, Move Left, Move Right and Copy; and dragging a tab onto another moves it. Each is one undo step, said in the Sheet's notice, and taken in its place among the keys. `ISheetChrome` gains `SheetTab`, `SheetTabAdd` and `SheetTabMenu`, which `MudSheetChrome` draws in MudBlazor's controls. Shift+F11 is now a key the Sheet claims from the browser.
