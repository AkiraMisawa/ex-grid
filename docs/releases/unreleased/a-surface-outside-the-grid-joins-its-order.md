Product: ExGrid
Kind: Added
ADR: 0240
Docs: exgrid/appearance

`JoinSurfaceAsync(element, around, escape)` puts a surface a design system draws outside the grid's Input Root — a `MudDialog`, a popover at the page's body — in the grid's Input Order until you dispose of what it returns: its claimed clicks, presses, submissions and keys are handled in their place, the keys typed in it while an earlier input is answered go, in order, to where that input left the keyboard, and an Escape no control of it keeps runs `escape` in its turn. A claim can name one key (`data-ex-claims="keydown:Enter"`). `HandKeyboardToFrameAsync(element)` has the keyboard wait on an empty element of your frame's own while the frame is on its way, so a dialog that gives the keyboard back as it closes gives it to nothing.
