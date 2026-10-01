# 14: Show Details in a tab or a dialog

Status: done

**What to build:** the three destinations of Show Details (ADR-0058).

- **A tab at the report's foot, by default.** It is closable, titled by the cell, not part of the
  layout, and holds an ExGrid of the Pivot Fields fetched in pages from `DetailsAsync` under the
  report's Source Version.
- **A dialog**, when the Consumer asks for one.
- **The Consumer**, when it listens to `OnShowDetails`.

A tab whose version the source can no longer answer says that the data has changed.

**Blocked by:** 12

- [x] PV-14, in layer 2 and on `/pivot` in layer 3

## Comments

2026-10-01: Built. By default a tab opens at the report's foot: closable, titled by the cell,
not part of the layout, holding an ExGrid of the source's fields fetched in pages through
`GridSource.Fetch` over `DetailsAsync` under the report's Source Version; a tab whose version the
source refuses says the data has changed. `DetailsView="PivotDetailsView.Dialog"` opens ExPivot's
own dialog with the same grid, and a Consumer that listens to `OnShowDetails` is handed a
`PivotDetails` and neither opens. The Chrome draws the tabs and the dialog's content through
`IPivotChrome.DetailsTabs` and `DetailsDialog`. Layer 3 runs all three on `/pivot` (WebAssembly,
Chromium, both Chromes). Two gaps need a decision about ExGrid's core and are reported, not
built: closing the dialog cannot give the keyboard back to the report, because nothing lets a
Consumer hand a grid's root the keyboard; and Escape pressed inside the dialog's grid is that
grid's way out (it takes the key and stops it), so the dialog stays open with the keyboard on
the page.
