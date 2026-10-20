# 05: ExSheet shows a Workbook behind Sheet Tabs

Status: done
Roadmap: ExSheet — An ExSheet shows a tab for each Sheet at its foot and switches between them with a click or Ctrl+PageUp/PageDown.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §1, §2, §4 (switching). The component moves onto `Workbook`.

- [x] A `Workbook` parameter and its change event; `Document` still works as a Workbook of one Sheet; both set is refused by name
- [x] One ExGrid draws the Sheet shown; P1 to P9 hold with several Sheets
- [x] A tab per Sheet (Built-in Chrome), a tab list to assistive technology; a click and Ctrl+PageUp/PageDown switch
- [ ] Each switch an input in the Input Order: Ctrl+PageUp/PageDown are (declared keys); a click on a tab joins it with ExSheet's own surfaces, input-order ticket 04
- [x] Each Sheet's selection, active cell, scroll offsets and column widths are restored when it is shown again
- [x] Layer 2 for the parameters and view state; layer 3 for the keys and a key typed at once after a switch
- [x] Docs Site: the Workbook page and Example; Change Note

**Blocked by:** 02.

**Settled while building (2026-10-20):** ExGrid gained `ReadView()` and `ShowViewWith(view)` (`GridView`):
ExSheet keeps the view of the Sheet it leaves and hands the next Sheet's back ahead of the Row Sequence
Version it shows that Sheet under, so the Selection and the version reach the grid in one render and a
key typed at once after a switch meets the Sheet's own Selection. A Sheet's rows are places, so the
version moves only on another document or another Sheet shown; a gesture aimed at the Sheet left is
refused, and the order-moved notices now say "another Sheet was shown, or another Sheet Document
opened". Another Sheet is not shown while an edit is open (refused with a notice) until pointing across
Sheets (09). The Sheet Tabs show by default, also under one Sheet, as tall as the Formula Bar and taken
from ExSheet's height; `ShowSheetTabs="false"` hides them. `DocumentChanged` is raised only while the
Workbook holds one Sheet, and `ToDocument()` throws with several. An operation that takes the shown
Sheet away (its deletion, the undo of its insertion) shows the Sheet the Workbook shows in its place. A
press on a tab is not numbered in the Input Order: the tabs stand outside the grid's root, where its
capture-phase click listener (ADR-0240) does not reach, and ExSheet's own surfaces join the order in
input-order ticket 04. The MudBlazor Chrome draws the Built-in tabs for now; its own drawing comes with
06. Building it found a Formula a deletion leaves naming no Sheet (`=#REF!*2`) kept its old Value; every
Formula a rename or deletion rewrites is now recalculated (fixed under 03).
