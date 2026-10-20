Product: ExSheet
Kind: Added
ADR: 0331

A Workbook's Sheets are added, renamed, moved, copied and deleted through `SheetEdit.InsertSheet`, `Rename`, `MoveSheet`, `CopySheet` and `DeleteSheet`, each one step that undoes it exactly. A rename rewrites every Formula on every Sheet that names the Sheet; a deletion turns References to it into `#REF!` and gives them back on undo. A name another Sheet has, or deleting the last Sheet, is refused by name (`SheetNameTaken`, `LastSheet`). `WorkbookChange.ChangesSheets` reports that the Sheets themselves changed.
