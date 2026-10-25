Product: ExSheet
Kind: Behaviour
ADR: 0331

A new Sheet is numbered as Excel numbers one: `Sheet` and the next number of a count of the Sheets the Workbook has made since it was created or opened, copies included, passing over a name a Sheet has. With Sheet1 renamed Sheet5, the next Sheet is Sheet2, where it was Sheet6. Undoing an insertion gives its number back, and a Workbook opened from a document starts from the first number no Sheet has.
