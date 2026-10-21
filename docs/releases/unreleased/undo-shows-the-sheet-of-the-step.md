Product: ExSheet
Kind: Behaviour
ADR: 0331

Ctrl+Z and Ctrl+Y, and `UndoAsync` and `RedoAsync`, now show the Sheet the step was taken on and select the range it changed, as Excel's undo does: the cell typed in (not the one Enter moved to), the range pasted, filled or formatted, the whole rows or columns inserted or deleted, with the Focus on its top-left cell scrolled into view. The Selection therefore moves on an undo or a redo, also in a Workbook of one Sheet; an operation over several ranges selects the one it acted from, since the grid places one range. Undoing a Sheet operation shows the Sheet it brings back or the one it leaves: a Sheet deleted, renamed or moved is shown, and when a Sheet added or copied is taken away, the Sheet it was added beside or copied from is shown (until now, the Sheet in its place). The engine's `WorkbookStep.Ranges` names the ranges a step changed.
