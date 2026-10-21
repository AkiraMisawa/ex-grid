Product: ExSheet
Kind: Behaviour
ADR: 0332

A Sheet now draws its find panel through its own Chrome, so a Consumer Chrome's `IGridChrome.FindPanel` is no longer asked on a Sheet: ExSheet wraps the Chrome it is handed and draws the Sheet's panel, with Replace and Within, in its place. A Chrome of your own that drew the grid's find panel on a Sheet implements `ISheetChrome.SheetFindPanel` instead, handed a `SheetFindContext`; one that does not gets ExSheet's built-in Sheet panel.
