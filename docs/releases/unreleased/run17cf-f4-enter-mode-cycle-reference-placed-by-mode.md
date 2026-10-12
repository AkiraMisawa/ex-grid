Product: ExGrid
Kind: Behaviour
ADR: 0051

The selection a `CycleReference` function answers is placed as given only while the edit is in Caret — after F2 in a cell, and in the Formula Bar until F2 takes it to Enter mode; in Overwrite and while pointing the grid collapses it to a caret at its end (`SelectionEnd`), as Excel leaves the caret after F4 there. A function that answered a selection over what it rewrote now sees it selected in Caret alone, and one that answers a caret sees no change.
