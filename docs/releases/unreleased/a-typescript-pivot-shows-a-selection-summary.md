Product: ExPivot
Kind: Added
ADR: 0130, 0370

ExPivot drawn by the Script Core (`@ex-grid/pivot`, `@ex-grid/pivot-react`) shows the Selection Summary under its report: Excel's figures over the cells selected, summed by the report source over the whole report, rows not drawn included, those that read numbers written in the Focus value's number format. The figures menu's choice is the pivot's to hold; `summaryFigures` sets the figures, `onSummaryFiguresChanged` hears the choice, `onSelectionSummaryChanged` hands the summary on, and `showSelectionSummary: false` turns the line off. Blazor's `ExPivot.Components.ExPivot` reads `Slicing`'s `Budget` once as it attaches, and refuses by name a `Slicing` that sets its clock, its yield or `RecordsPerCheck`. `@ex-grid/pivot-engine` exports `formatPivotFigure`, `pivotNumberAverage`, `pivotNumberCanonical` and `pivotNumberValue`.
