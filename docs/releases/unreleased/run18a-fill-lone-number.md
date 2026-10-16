Product: ExSheet
Kind: Fixed
Docs: exsheet
ADR: 0050

The fill handle continues the one number of a source that holds nothing else, as Excel does: `1` over an empty cell, dragged right, gives `2 3`, where it gave `1 1`, and columns B:C with B1 `1` and C1 empty, dragged right, give D1 `2` and leave E1 empty, where the fill was refused. A single number in a line of a source whose every cell holds a number is still copied, as Excel copies it; beside text, a boolean or a gap between numbers, the fill is refused rather than copied, since Excel was not read there.
