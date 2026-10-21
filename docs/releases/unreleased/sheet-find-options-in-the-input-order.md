Product: ExSheet
Kind: Fixed
ADR: 0240

A tick of Match case or Match entire cell contents in a Sheet's find panel, under the built-in Chrome and `MudSheetChrome`, is now taken in its place in the grid's Input Order, as Within is: a step or a Replace All over the Workbook clicked before the tick no longer runs with the option the tick set. A Chrome of your own that implements `ISheetChrome.SheetFindPanel` changes as the grid's find panel does: it returns the task `FindContext.MatchCaseChanged` and `WholeCellChanged` give, and its checkboxes claim their clicks (`data-ex-claims="click"`).
