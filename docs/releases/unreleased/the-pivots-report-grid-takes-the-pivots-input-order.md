Product: ExPivot
Kind: Fixed
ADR: 0240

In `@ex-grid/pivot` and `@ex-grid/pivot-react`, the report's grid kept an Input Order of its own instead of the pivot's, so a press on one of the pivot's own controls was not numbered among the grid's inputs. The report's grid now takes the pivot's order, and a press on the Pivot Toolbar or the Field List made while the grid is still answering an earlier key waits its turn.
