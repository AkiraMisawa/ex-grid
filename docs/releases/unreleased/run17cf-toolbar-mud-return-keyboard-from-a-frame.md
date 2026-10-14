Product: ExGrid
Kind: Added
ADR: 0050

`ExGrid.ReturnKeyboardAsync(ElementReference from, Func<IReadOnlyList<KeyboardEventArgs>> typedOnTheWay)` gives the keyboard back to the grid from a frame of your own outside it while the frame still holds it, and hands the grid the keys typed on the frame meanwhile, in order, ahead of any typed after: hold the frame's keys from the moment you call it, and return them when the grid asks. Your frame no longer needs to give the keyboard up to nothing first, where a key typed reaches no grid; a keyboard already in the grid, in a popover a command of the frame opened, stays where it is. Await its completion before removing the frame or clearing its queue: the task completes after the grid has collected the held keys, including when another control kept focus and the hand-back was declined.
