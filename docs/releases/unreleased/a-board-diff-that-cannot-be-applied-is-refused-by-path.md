Product: ExBoard
Kind: Fixed
ADR: 0340

`BoardDiff.Read` now refuses, naming the path, a pivot layout of a version it does not read, a column both shown and hidden, and an end of a chart's range past a double's range. The first two threw an exception that named nothing, and the last was read and could not be written back. `FitTo` now drops by name a grid part that would show no column, where it laid an empty grid over the view.
