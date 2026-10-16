Product: ExGrid
Kind: Behaviour
ADR: 0050, 0061

The keys a frame of yours hands back with the keyboard (`ReturnKeyboardAsync(from, typedOnTheWay)`) now wait for a hand-off told meanwhile (`HandKeyboardToFrameAsync`, or a popover of yours opening) and go where it leads, in order, rather than being taken as the grid's: take the keyboard back, then tell the hand-off, to carry keys typed on one frame of yours through the grid to another. A hand-off told while one of your controls outside the grid still holds the keyboard is still taken as reached there, so tell it after asking for the keyboard back.
