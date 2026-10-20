Product: ExSheet
Kind: Added
ADR: 0331

Inserting or deleting rows or columns on one Sheet of a Workbook rewrites the References to it on every other Sheet, as on its own: they follow their cells, or become `Sheet!#REF!` when their cells are deleted, and the undo gives them back exactly. Such an edit on a Sheet of a Workbook of several no longer throws `NotSupportedException`.
