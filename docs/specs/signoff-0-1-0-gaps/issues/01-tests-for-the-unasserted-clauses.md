# 01: A test for each clause of a criterion that no test asserts

Status: ready-for-agent
Roadmap: internal

**Why:** 37 criteria passed the 0.1.0 sign-off with one clause of their Pass that no test in any layer
asserts: the test named for the row checks the rest, and the clause holds in the code as it reads. The
user signed off with them as they stood on 2026-10-18, and each becomes a test here. The
record is `verification/2026-10-18-linux-signoff/results.md`, where each reads `pass, accepted as is`.

**What:** for each row below, a test in the layer the note names, carrying the row's ID in its name
(AGENTS.md, "Tests"). A test that fails is a defect in the product, fixed as one, not a criterion to
restate.

- **FN-1** (layers 1–2): Rows painted match the pushed Window, WindowStart/TotalCount placeholders are checked; no test asserts that no component field retains a Window after the parameter changes.
- **A11Y-6** (layer 3): No Playwright test scrolls by wheel until the Focus row leaves and asserts aria-activedescendant is cleared and restored; only the Layer 2 half (AccessibilityTests.Activedescendant_clears_when_the_focus_leaves_the_paint) exists.
- **A11Y-9** (layer 3): Specs read the announced text after a selection settles, but none drags across 200 rows and counts live-region writes (only the bUnit AccessibilityTests.The_live_region_announces_once_per_settled_selection).
- **A11Y-10** (layer 3): Only a collapse to one cell is shown to leave the region empty (features.spec); no test presses plain arrows and asserts the live region unchanged.
- **VZ-2** (layers 1–2): FindComponents<ExGridRow<T>>().Count equals RowsPerViewport at one large total (100,000); not swept over several TotalCounts.
- **ED-13** (layers 1–2): C# mirror: Control+Alt opens the editor and either alone opens nothing; the JS gate in ex-grid.js is only grep-pinned for the metaIsPrimary byWord rule (ShippedStylesheetTests:1711), not tested for AltGr.
- **ED-19** (layer 3): Editor stands with the typed 7 after the refused fill and nothing is written; the follow-up that Enter then raises exactly one Edit Intent is not asserted at Layer 3.
- **SL-1** (layers 1–2): Ctrl+A is one rectangle (asserted at 100x26) and the 10^6x50 count is computed, but Ranges.Count==1 is not asserted at 10^6x50 and per-cell allocation is shown only by element/render counts.
- **SL-2** (layers 1–2): Whole-result selection succeeds at 10^6x50 (count) and in the grid, but no test is named for the absence of a cap at larger sizes.
- **SL-11** (layer 3): No Playwright test drags to a page boundary and checks the pager stays; only the bUnit PagerTests.A_drag_at_the_page_boundary_stays_on_the_page (L2, tagged SL-11) covers it.
- **KB-3** (layer 3): Meta+click on a header is platform-conditioned, and ControlOrMeta+Enter exercises Cmd+Enter on macOS; no test presses Win/Meta+ArrowDown off Apple or Meta+Enter doing nothing, and no macOS run is shown.
- **KB-5** (layers 1–2): Arrows, Shift/Ctrl/Ctrl+Shift arrows are tested, Home/End only as key mapping plus Ctrl+Home/Ctrl+Shift+End/Home; not every row of the ADR-0012 table has a named test.
- **KB-9** (layers 1–2): First ArrowDown places the Focus on the first visible cell (row 30 after scroll); does not assert the Viewport stays unmoved (no scroll-count check).
- **KB-10** (layer 3): Ctrl+Home on /wide (2 pinned) lands the Focus on r0c0 and scrollTop 0; scrollbar spec deliberately avoids asserting scrollLeft; no pinned-vs-unpinned agreement or Home-to-start-of-row check in a browser.
- **KB-11** (layer 3): Typing in the Template Column input works and Escape returns the keyboard; arrows moving the caret and not the selection are not asserted.
- **KB-28** (layer 3): Alt+ArrowDown opens the Focus column's menu with aria-label Book and focus in it; browser taking no action of its own and the no-menu column case are not asserted (title escapes the apostrophe, so ID scan missed it).
- **KB-40** (layer 3): Browser shows the Extent followed with the Focus left behind and Shift+PageDown carrying the view; the move-back-inside-scrolls-nothing clause is only tested for the Focus (loop test at ~71), not the Extent.
- **KB-42** (layers 1–2): Corners, reveal and collapse are asserted in Layers 1 and 2; 'none is taken from an open editor' has no found test.
- **KB-42** (layer 3): Corners clockwise, Ctrl+Backspace reveal-only and Shift+Backspace collapse asserted; not-taken-from-an-open-editor not asserted.
- **CP-7** (layer 3): No Playwright test copies a selection beyond the Window to exercise the async Range route; only Layer 2 (ClipboardWiringTests.cs:108 A_selection_beyond_the_window_takes_the_async_route) covers it.
- **CTX-3** (layers 1–2): Asserts the clicked row instance (Same), column name and a single-range Selection; Row Sequence Version in the context and 'no row beyond the Window resolved' are not asserted there.
- **PST-4** (layers 1–2): Shape mismatch (3 rows into 5) is refused with ShapeMismatch and refusals raise no intent, but no test pastes a payload 'far larger than the target' and asserts zero notifications for that case.
- **ERR-1** (layers 1–2): Most rules have their own refusal test (empty, misaligned, cap, shape, editability, oversized fill, MinWidth, gutter); no single test enumerates all the listed rules and a both-or-neither Window/Source test was only found for 'neither' (FindTests.cs:413).
- **ERR-7** (layer 3): Repeated mount/dispose in a browser under the console-clean fixture, but no test deliberately disposes during an in-flight interop call.
- **ASY-7** (layer 3): Mark off/on around keys, a 20-key burst, a reveal and an edit commit; paste, composition, copy-in-flight, a press on rows and each owed timer (data-ex-owing) are not exercised in the browser spec.
- **WR-1** (layer 3): popovers.spec.mjs runs under both builtin and mud Chromes (VALUE_LIST/CONDITION tables): value list, search, condition, Apply/Cancel; no assertion on the Blank entry, TooMany, or FilterSpec equality across Chromes beyond row counts.
- **WR-10** (layer 3): No test asserts the MudBlazor floor or range; the Check is a Step 4 floor run plus per-version layer-2 runs recorded under verification/, not a Playwright test.
- **DC-39** (layer 3): ExSheet drag shows 4R x 3C and returns to D5 on release; keyboard extension showing no size is only at layer 2.
- **DC-40** (layers 1–2): Ctrl+D top row, Ctrl+R left column, clipboard paste and Ctrl+Enter naming none are asserted; the row above / column to the left of a range one cell deep is not.
- **DC-62** (layer 3): Escape dismisses the grid layer first, then leaves once, and held repeats leave once, on /pivot?details=dialog; not asserted: Tab release before the raise, every other dismissal kind, DOM focus kept, the undeclared case.
- **DC-63** (layer 3): A double click on a /pivot value cell raises once with the Focus already placed there; Editable cell, open edit, controls in Action/Template/Mark cells and no-listener cases are layer 2 only.
- **SH-60** (layer 3): Only NOW() in the browser's zone on a page; no layer-3 check of #GETTING_DATA, the minute turning, or the Consumer's zone for NOW.
- **PV-3** (layer 3): Only the number-Item-label-in-General clause is read in a browser; case-insensitive text, dates, Blank, and ordering clauses are not at layer 3.
- **PV-7** (layer 3): Eleven-character General, number-Item label and full-precision copy of the header are covered; a value cell copy and the refusal of a format the engine cannot write are not at layer 3.
- **PV-13** (layer 3): The ± button collapses and keeps the Focus; expand via Context Menu or double click on the label, and a values-only refresh keeping Row Sequence Version and Selection, are not at layer 3.
- **PV-18** (layer 3): Palette reaches the pane, entries and − button in both schemes and the stale token is probed under the Wrapper mapping; not every Visual Token is read in a browser.
- **DA-18** (layers 1–2): Definitions are tested in ExGrid.Data and ExPivot tables are checked per Aggregation, but no test asserts that both ExPivot.Engine and the Selection Summary answer through the same ExGrid.Data accumulators ("inspect the references").
- **LV-27** (layer 3): Server-path Window Changes are exercised end to end, but no layer-3 test targets digest verification, mismatch recovery or a stale fallback.

## Comments

- 2026-10-22: tests now assert FN-1, VZ-2, ED-13 (both layers), ED-19, SL-1, SL-2, SL-11, KB-3 (off
  Apple), KB-5 (on the component), KB-9, KB-10, KB-11, KB-28, KB-40, KB-42 (both layers), A11Y-6, A11Y-9,
  A11Y-10, CP-7, CTX-3, PST-4, ERR-1, ERR-7, DC-39, DC-40, SH-60 (the Consumer's zone), PV-13 (the
  Context Menu) and DA-18. Still open: ASY-7, WR-1, DC-62, DC-63, SH-60
  (#GETTING_DATA, the minute turning), PV-3, PV-7, PV-13 (the double click, a values-only refresh),
  PV-18 and LV-27.
  WR-10 is not a Playwright test: its Check is the by-hand MudBlazor floor run under `verification/`.
- 2026-10-22: PV-13's double click on the label found a defect. On CI (PR #246, Edge, Server host,
  `/pivot?chrome=mud`, once in three) a double click on APAC's label after an Expand from the Context
  Menu collapsed APAC and left the report with no Focus (`aria-activedescendant` absent for 60 s),
  where the Context Menu's Collapse in the same run kept it. The input journal shows the double
  click's four presses answered in order; the Focus is placed by `ExPivot.OnAfterRenderAsync` through
  `PlaceSelectionAsync` once the toggled report lands. Not reproduced locally in 30 runs on Chromium.
  The clause's test goes in with the fix.
