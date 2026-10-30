Product: ExSheet
Kind: Behaviour
ADR: 0370

`ExSheet.Components.ExSheet` is drawn by its TypeScript Script Core (ADR-0370 section 1): the Workbook, its Formulas, the Formula Bar, the Sheet Tabs, the toolbar and the Selection Summary run in the browser on both hosting models, under the same parameter names and defaults. Documents cross as the JSON both engines read and write: a `Document` or `Workbook` of the Consumer's goes to the browser, every change comes back through `DocumentChanged` and `WorkbookChanged`, and one the Sheet raised, bound back two ways, crosses no more. `CanUndo` and `CanRedo` are told by the browser as soon as a step moves the undo stack, and a `PointingScope` is held in the browser, so a press is written in the turn it is made on Blazor Server too. Building ExSheet from source now needs Node, as ExGrid's and ExPivot's builds do, which bundles the script it ships.
