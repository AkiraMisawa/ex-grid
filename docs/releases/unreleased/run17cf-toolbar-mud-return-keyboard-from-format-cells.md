Product: ExSheet
Kind: Added
ADR: 0071, 0050

`FormatCellsContext` gains `ReturnKeyboardFrom(from, typedOnTheWay)`, ExGrid's hand-back from a frame for a Chrome's Format Cells: called with the frame's element that holds the keyboard and the keys typed in the frame since its close began, it gives the keyboard back to the Sheet with those keys, in order. ExSheet now keeps a Chrome's Format Cells frame on the page after OK or Cancel until the Chrome has given the keyboard back through it or through `ReturnKeyboard`: a Chrome of your own that never calls either sees its fragment stay rendered.
