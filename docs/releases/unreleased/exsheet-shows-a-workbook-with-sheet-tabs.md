Product: ExSheet
Kind: Behaviour
ADR: 0331

ExSheet shows a Workbook: bind `Workbook` (a `WorkbookDocument`) in place of `Document`, and a tab for each Sheet stands under the grid, switched by a press or by Ctrl+PageUp and Ctrl+PageDown, each Sheet keeping its own Selection and scroll position. `WorkbookChanged` and `ToWorkbookDocument()` carry the Workbook; `DocumentChanged` is raised only while it holds one Sheet, and `ToDocument()` throws once it holds several. The Sheet Tabs show by default, also under a single Sheet, in a strip as tall as the Formula Bar taken from the height you give ExSheet, so the grid of every existing ExSheet is one row shorter; `ShowSheetTabs="false"` gives the height back. A write or typing aimed at the Sheet left that reaches the component after the switch is refused, and its notice now says "another Sheet was shown, or another Sheet Document opened". `Sheet.Workbook` is public.
