Product: ExGrid
Kind: Fixed
ADR: 0210

Text typed into a field while the grid was still handing it the keyboard, and committed by an Enter typed after that text was shown, is now committed. Before, the field showed the text and its Consumer kept the old value: in ExSheet's Format Cells, More Colours' HEX could show `#FF0000` while the fill stayed unchanged. ExSheet and ExPivot, drawn by ExGrid, behave the same.
