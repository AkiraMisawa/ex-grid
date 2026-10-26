Product: ExSheet
Kind: Added
ADR: 0100, 0370

The TypeScript Sheet (`@ex-grid/sheet`, and `@ex-grid/sheet-react`'s `ExSheet`) shows the Sheet Toolbar when `showToolbar` is set: Excel's Home tab Bold, Italic, Underline, Strikethrough, the three alignments and Percent Style, each acting on the Selection as one step to undo, a toggle pressed while the Focus cell has what it sets. Comma Style is shown disabled with its reason, as in the Blazor Sheet. It is one tab stop with ← and → among its items; a press keeps the keyboard on the Sheet, and while an edit is open every item is unavailable. The item names are `SheetWords`, in English and Japanese. The colour, Borders and Number Format lists, Format Cells…, KeyTips and a Consumer's own items follow.
