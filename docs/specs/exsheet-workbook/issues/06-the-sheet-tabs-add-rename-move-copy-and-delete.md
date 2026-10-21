# 06: The Sheet Tabs add, rename, move, copy and delete

Status: done
Roadmap: ExSheet — Sheets are added, renamed, moved, copied and deleted from their tabs, by pointer or keyboard alone.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §4.

- [x] `+` and Shift+F11 add; a double click renames on the tab (Enter commits, Escape cancels); a tab's menu deletes, moves left/right and copies; a drag moves
- [x] Every one reachable from the keyboard, announced in the live region
- [x] Each is an input in the Input Order and one step on the undo stack
- [x] Layer 3 at machine speed on both hosts
- [x] Docs Site and Change Note

**Blocked by:** 03, 05.

**Settled while building (2026-10-21):** `ISheetChrome` gained three seams, each null by default:
`SheetTab(SheetTabContext)` draws a tab's face, `SheetTabAdd()` the `+` button's, and
`SheetTabMenu(SheetTabMenuContext)` the menu's commands (`SheetTabCommand`: its kind, label, whether it can
be done and why not, whether the keyboard is on it, and `Invoke`). ExSheet keeps every element that takes a
press or holds the keyboard — the tab, the `+` button, the name's field and the menu's frame, which holds the
keyboard with `aria-activedescendant` — so their claims and their place in the Input Order are the same under
either Chrome; `MudSheetChrome` draws the faces in MudText and MudIcon and the menu as a MudPaper, not a
MudMenu, whose list MudBlazor draws outside the Sheet, where a click is not in the order. The name's field is
ExSheet's plain input under both Chromes, styled by the MudBlazor stylesheet. Shift+F11 is a key the Sheet
now declares to its grid. The menu opens at the strip's start, above the tabs, not at the pointer: nothing is
measured. The drag is Blazor's own mouse events, a press on a tab and its release on another or past the
last, with no script (ADR-0021); leaving the strip cancels it. The one change to the script is a reorder
within the tenth entry (ADR-0210): an Enter replayed into a text field outside the root, typed while held,
first commits the held text with its change event, so the name typed at machine speed is the one Enter
commits. A Sheet operation's undo shows the Sheet the engine shows (the one in the place of a Sheet taken
away), until 08 decides it.
