Product: ExGrid
Kind: Fixed
ADR: 0051, 0010, 0220

On Blazor Server, a press into the Formula Bar's text made straight after Enter and a press on a cell could lose its caret: the renders of those inputs, landing after the press, rewrote the bar's text and put the caret at its end, so F4 cycled the last Reference instead of the one pressed. Where the bar still holds the text the press was made on when the press takes its turn, the caret now goes back where it was pressed, as a slower press leaves it (ticket 202).
