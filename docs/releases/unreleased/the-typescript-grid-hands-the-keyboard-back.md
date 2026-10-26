Product: ExGrid
Kind: Added
ADR: 0070

`@ex-grid/core`'s grid handle, and `@ex-grid/react`'s ref, offer `returnKeyboard()` and `handKeyboardTo(control)`, the Blazor component's `ReturnKeyboardAsync()` and `HandKeyboardToAsync()`: the keyboard back to the grid as something of the Consumer's that held it goes away, and on from the grid to a control of the Consumer's own, a container naming its first text field. Each is granted only while the keyboard is still the grid's, so a control or another grid the user moved to keeps it, and so do the Formula Bar and the Name Box; neither moves the Focus or the Selection.
