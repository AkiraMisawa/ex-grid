Product: ExSheet
Kind: Breaking
ADR: 0201

These members of public ExSheet.Engine types are no longer public, each the engine's own machinery or a second way to what a public member already gives: `Entry.FromFormula`, `Sheet.SetEntry`, `Sheet.Rename`, `FormulaEntry.PointedReferenceAt` and `NumberFormat.AbbreviatedMonthNamesOf`. A Consumer reads a Formula with `Entry.Parse`, which takes text beginning with `=` as a Formula in invariant syntax; sets one cell with `SetEntries` and a single pair; and renames the Sheet with `Do(SheetEdit.Rename(name))`, the same change, which can be undone. The other two had no Consumer use: `FormulaEntry.PointAt` still says where a Reference can be written, and a date's month is shown through its number format.
