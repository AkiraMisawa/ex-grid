Product: ExGrid
Kind: Behaviour
ADR: 0050

`HandKeyboardToAsync(control)` called while the keyboard is on its way to a frame of yours (`HandKeyboardToFrameAsync`) and the grid holds keys for it gives the control the keyboard when the grid is ready to hand those keys over, and hands them over in the same moment, rather than at once: a key typed straight on the control can no longer overtake one still held for it.
