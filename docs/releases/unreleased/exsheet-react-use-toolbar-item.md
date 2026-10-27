Product: ExSheet
Kind: Added
ADR: 0390, 0100, 0071

`@ex-grid/sheet-react` exports `useToolbarItem(context)`, which holds everything a Chrome's Toolbar Item needs but its look: the item's view, the props each control and each list choice takes, the list's rows, its opening and closing on the toolbar's requests and on a press outside, and the list's keys, the built-in list's. `useFormatCells` gains `submit`, OK as a form's submit that shows a field left invalid on its tab, and `@ex-grid/sheet`'s `FormatCellsDraft` gains `colourOf(target)` and `setColour(target, colour)` for a palette. The Mantine and MUI Wrappers draw with them, so the same keys and choices do the same thing under either.
