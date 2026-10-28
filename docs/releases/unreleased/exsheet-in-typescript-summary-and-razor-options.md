Product: ExSheet
Kind: Added
ADR: 0370, 0130, 0071, 0048

`@ex-grid/sheet` now shows the Selection Summary the Razor ExSheet shows: Excel's figures over the selected cells' Values in Excel's doubles, Average, Min, Max and Sum in the active cell's Number Format, and no figure, with the reason, over a refused cell. It takes the Razor component's other options beside its grid: `showSelectionSummary`, `summaryFigures` (the Sheet keeps the user's choice from the menu until other figures are handed), `onSummaryFiguresChanged`, `onSelectionSummaryChanged`, `allowCopyWithHeaders`, `onFormatKeyRefused` and `commandLabel`, which words the grid's own commands and refusals. `@ex-grid/core` exports `GridSummaryCells`, the figures' one definition for an answerer that reads its cells itself.
