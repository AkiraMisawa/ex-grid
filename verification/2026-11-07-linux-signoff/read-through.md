# The read-through of every criterion, at c8b721f9

The evidence for each of the Definition of Done's 703 rows, read from the tests and the code by section
on 2026-11-06, against `c8b721f9`, `main` two merges after `v0.2.0-rc.2` (#537, #538). Between it and X,
#539 and #540 fixed or restated the rows the sign-off's [`results.md`](../2026-11-09-linux-signoff/results.md)
names, and #542 fixed the Hunt's reds of 2026-11-07; the verdict of every row at X, `v0.2.0-rc.4`'s commit, is
that file's table, which this file's tables feed, with each line number carried to X. "cna" below is
`pass, clause not asserted`, which `results.md` reads as `pass, accepted as is`.

# A-pre-vz.md

# PRE, FN, UX, A11Y, HG, DIR, VZ against c8b721f9 (0.2.0 sign-off, Step 7)

Paths shortened: `core/` = `packages/core/test/`, `react/` = `packages/react/test/`, `blazor/` = `packages/blazor/test/`,
`browser/` = `tests/browser/`, `cases/` = `tests/cases/`, C# as `<Project>/<File>.cs:<line> <Name>`. "cna" below = `pass, clause not asserted`.

| ID | Level | Result | Evidence |
|---|---|---|---|
| PRE-1 | MUST | pass (Step 0) | §22 Step 0 at 308a8555: 0 Warning(s), 0 Error(s) |
| PRE-2 | MUST | pass (Step 0) | Step 0: every `dotnet/src` project `net10.0`, no `<LangVersion>` |
| PRE-3 | MUST | pass (Step 0) | Step 0: `check.sh` and `tools/npm-package/check.mjs` passed |
| PRE-4 | MUST | pass (Step 0) | Step 0: references printed as listed; Step 3 grep no match |
| PRE-5 | MUST | pass (Step 0) | `browser/playwright.config.mjs:180` projects `chrome`, `msedge` (+`chrome-150`) |
| PRE-6 | MUST | pass (Step 0) | Step 0: `git status` clean |
| FN-1 | MUST | pass, clause not asserted | L2: `core/grid-dom.test.ts:335 'a position the Window does not hold is a Placeholder, and its range is asked for once'`, `core/grid-range-request-margin.test.ts:43` (windowed source). Not asserted: nothing retains a Window after it is replaced |
| FN-2 | MUST | gap | No test. Code refuses: `grid-view.ts:2581` (TS), `ExGrid.cs:475` (Blazor) |
| FN-3 | MUST | pass, clause not asserted | L2: `core/grid-dom.test.ts:335` (asked once), `core/grid-range-request-margin.test.ts:43 'not asked again until the Window moves'`. Not asserted: a synchronous answer does not re-enter |
| FN-3a | MUST | pass, clause not asserted | L2: `core/grid-range-request-margin.test.ts:43 'at 10 …'`, `:64 'at 0, the default …'`, `:82 'a total not known yet …'` (+1). Not asserted: the upper edge (scrolling up) and the clamp at the total's end |
| FN-4 | MUST | gap | No test runs one scenario through both `from` and `fetch`. Structurally one path (`grid-view.ts:2586`, `rows` → `InMemoryGridSource.from`) |
| FN-5 | MUST | pass | L1: `cases/source/fetch.json:7 'cold start …'`, `:68 'same range … one fetch'`, `:99 'a superseded question is called off …'` (+14); `core/fetch-boundary.test.ts:101` |
| FN-6 | MUST | pass | L2: `core/grid-dom.test.ts:218 'a fling paints Placeholders with the pinned cells alone …'`; L3: `browser/virtualisation.spec.mjs:694 'the Pinned Columns are painted … mid-fling included (FN-6)'` |
| FN-6a | MUST | pass, clause not asserted | L1: `cases/columns/geometry.json:353`, `:364`, `:373` (FN-6a); L3: `browser/sizing.spec.mjs:312 'a window narrower than the pinned block suspends pinning …'`. Not asserted: no `OnColumn…`/View State notification raised (only the pinned count after widening) |
| FN-7 | MUST | pass | L1: `cases/paint/cell-classes.json:23 'every state has a class'`, `:73 'cast in from a number is refused'`; L2: `core/grid-cell-paint.test.ts:46 'a Cell State is painted as its class and nothing more'` |
| FN-7a | MUST | pass, clause not asserted | L2: `core/grid-cell-paint.test.ts:66 '… tone rule paints a gain and a loss … not asked about a null'`; L3: `browser/mud.spec.mjs:203 'a loss is painted in the palette's error colour … (FN-7a)'`. Not asserted: a negative in a column with no rule; text/copy/raw forms untouched |
| FN-8 | MUST | pass | L1: `cases/paint/row-classes.json:13`, `core/paint-classes.test.ts:39`; L2: `core/grid-row-kind.test.ts:64 'each row wears the class of the role it was told'` |
| FN-9 | MUST | pass | L2: `core/grid-actions.test.ts:334 'content is mounted once its row stands in the DOM … taken away when it is not'`, `:83`, `react/ex-grid.test.ts:153`; `core/grid-dom.test.ts:118` (listeners on root) |
| FN-10 | MUST | pass | L1: `cases/columns/overflow.json:13`, `:26`, `:32` (+12); L2: `core/grid-dom.test.ts:315 '… a number too wide for a Fixed one is ####'`, `core/grid-cell-paint.test.ts:96` |
| FN-11 | MUST | pass | L1: `cases/columns/auto-width.json:21`, `:28 'do not narrow'`, `:35 'any order'`; `cases/columns/column-width-spec.json:44` (40/400); `cases/view-state/write.json:2236` (width intents); L2: `core/grid-dom.test.ts:315` |
| FN-12 | MUST | pass | L1: `ExGrid.Tests/GridColumnTests.cs:26 (FN-12)`, `ExGrid.Tests/ActionAndTemplateColumnTests.cs:19 Action_and_template_columns_refuse_a_width_below_min_width_by_name`, `cases/columns/column-width-spec.json:34`; L2: `core/grid-column-gestures.test.ts:115`; L3: `browser/gestures.spec.mjs:122 'a drag past MaxWidth … (FN-12)'` |
| FN-12a | MUST | pass | L1: `cases/columns/column-width-spec.json:54 'Size to fit … clamped'`; L2: `core/grid-column-gestures.test.ts:140 'FN-12a …'`; L3: `browser/sizing.spec.mjs:80 '… fits a value the Viewport has not painted (FN-12a)'` |
| FN-12b | MUST | pass | L2: `core/grid-column-gestures.test.ts:86 'a press that barely moves reports nothing …'` (4px) |
| FN-12c | MUST | pass, clause not asserted | L1: `cases/selection/whole-ranges.json:113`, `:124`; L2: `core/grid-column-gestures.test.ts:103`, `:128` (FN-12c); L3: `browser/sizing.spec.mjs:270`. Not asserted: a double-click fitting each whole column to its own content |
| FN-12d | MUST | pass | L1: `cases/columns/grid-metrics.json:104 'one em of slack, the menu band and the sort room'`, `:131`, `core/text-geometry.test.ts:49`; L3: `browser/sizing.spec.mjs:50`, `:56` (FN-12d) |
| FN-12e | MUST | pass | L1: `ExGrid.Tests/GlyphWidthCorpusTests.cs:73 (FN-12e)`, `cases/columns/text-metrics.json:73`, `cases/columns/overflow.json:52`; L3: `browser/sizing.spec.mjs:65`, `browser/part-c.spec.mjs:487`, run by `ci.yml` `windows-desktop` (:600) and `macos-desktop` (:735) in Chrome and Edge |
| FN-13 | MUST | pass | `ExGrid.Tests/QuerySerialisationTests.cs:32 FN13_no_public_signature_mentions_an_expression_type`, `:21 FN13_a_row_range_round_trips…`, `cases/view-state/equality.json:3196 'FN-13 …'` (+70); no `Expression<` in any `PublicAPI.Unshipped.txt` |
| FN-14 | MUST | pass | L1: `cases/query/sort.json:7`, `:17`, `cases/query/text.json:7` (+N, run by `core/query.test.ts:31`); `ExGrid.Tests/SortSemanticsTests.cs:22 Sort_collation_is_ordinal_and_ignores_current_culture` (+N) |
| FN-15 | MUST | pass | Inspection: `grep -n '\.sort(' packages/core/src/dom/*.ts` prints nothing; no `.filter(` there reads a row against a predicate (the one over rows, `grid-view.ts:6037`, matches a Row Key) |
| FN-16 | MUST | pass, clause not asserted | L2: `core/grid-pager.test.ts:63 'the pager turns pages …'`, `:51`. Not asserted: a page click raises the same Range Request shape as a scroll |
| FN-17 | MUST | pass | L2: `react/chrome.test.ts:124-130` (the slot contracts against plain Chrome), `packages/mantine/test/contracts.test.ts:19`, `packages/mui/test/contracts.test.ts` (same contracts); `ExGrid.MudBlazor.Tests/MudGridChromeTests.cs`; L3: `browser/popovers.spec.mjs` (per Chrome), `browser/mud-app.spec.mjs:422` |
| FN-18 | MUST | pass | L2: `core/grid-filter-slot.test.ts:208 'FN-18: an operator the core did not offer … refused by name'`, `react/menu-slot-contract.ts:94`, `:131`; `ExGrid.MudBlazor.Tests/MudScriptFilterPanelTests.cs:66` |
| FN-19 | MUST | pass | L1: `cases/selection/queries.json:16 'count … needs no data'`, `cases/selection/range.json:19`; L2: `core/grid-off-screen.test.ts:28`, `:44` |
| FN-20 | SHOULD | pass, clause not asserted | L2: `core/grid-formula-bar.test.ts:70 'the bar reads the Focus cell's text …'`. Not asserted: a `####` cell's bar shows the value, not `####` |
| FN-21 | MUST | pass, clause not asserted | L2: `core/grid-consumer-popover.test.ts:115 'while a popup of the contents' own is open, Escape is the popup's'`; L3: `browser/mud-app.spec.mjs:542`, `:558`, `:572` (each for MudSelect and MudDatePicker, both Blazor hosts). Not asserted: the other grid unaffected; the Inner Popup clauses for a grid inside a MudDialog (`:620` only draws and chooses) |
| FN-22 | MUST | gap | No test. Code contradicts the clause: `grid-view.ts:1464` `sameItems` short-circuits on `a === b` and `#declared` holds the Consumer's array, not a copy; `ExGrid.cs:571` keeps `_numberedColumns = Columns` (the instance) |
| UX-1 | MUST | pass | `ExSheet.MudBlazor.Tests/NoColourParameterTests.cs:123 UX1_no_public_component_parameter_is_a_colour`, `:145`; `core/stylesheet-lengths.test.ts:192`, `:222` (UX-1) |
| UX-2 | MUST | pass | L3: `browser/presentation.spec.mjs:20 'geometry tokens are inline and read-only … (UX-2)'` |
| UX-3 | MUST | pass | L3: `browser/presentation.spec.mjs:89 '… at every preset'`, `browser/mud.spec.mjs:24 (UX-3)`, `browser/mantine.spec.mjs:28`, `browser/mui.spec.mjs:28` |
| UX-4 | MUST | pass | L3: `browser/presentation.spec.mjs:742 'every class under the root is one ADR-0029 names (UX-4)'` |
| UX-5 | MUST | pass, clause not asserted | L3: `browser/presentation.spec.mjs:169 '… recolours the paint (UX-5, the paint half)'`, `browser/mud.spec.mjs:225 'a theme switch re-renders nothing inside the grid: the rows are the same elements'`. Not asserted: the count half on the React host, and a .NET render count |
| UX-6 | MUST | pass | L3: `browser/presentation.spec.mjs:185`, `browser/mud.spec.mjs:45` (UX-6); `ExGrid.MudBlazor.Tests/WrapperStylesheetTests.cs:98` |
| UX-7 | MUST | pass | L3: `browser/presentation.spec.mjs:201 'UX-7 … under forced colors every Cell State and Row Kind stays tellable'` |
| UX-8 | MUST | pass | L3: `browser/presentation.spec.mjs:378 'under a dark scheme … (UX-8)'` (4.5:1); `ex-grid.css` declares no `color-scheme` (two comment lines only) |
| UX-9 | MUST | pass | L3: `browser/gestures.spec.mjs:193 (UX-9)`, `browser/mud.spec.mjs:79 (UX-9)`, `browser/mantine.spec.mjs:57` (+`mui.spec.mjs:57`) |
| UX-10 | SHOULD | pass | L3: `browser/gestures.spec.mjs:218 'narrowing the scrollbar by token … (UX-10)'`, `browser/stripes.spec.mjs:275` |
| UX-11 | MUST | pass | L2: `core/grid-column-menu.test.ts:63` (inline max-height), `core/grid-context-menu.test.ts:140`; L3: `browser/popovers.spec.mjs:201`, `:223` (UX-11), `browser/mud-app.spec.mjs:355 'WR-7: a grid in a MudDialog opens its popovers whole'` |
| UX-11a | MUST | pass | L2: `core/grid-context-menu.test.ts:243 '… a box shrunk below a row of room closes it'`; L3: `browser/stretch.spec.mjs:116 (UX-11a)` |
| UX-11b | MUST | pass | L3: `browser/sizing.spec.mjs:287 (UX-11b)` |
| UX-12 | MUST | gap | Inherits §4.1: A11Y-8 and A11Y-14 are gaps |
| UX-13 | MUST | pass, clause not asserted | L2: `core/grid-bands.test.ts:26 'UX-13 / RR-11 …'`; L3: `browser/mud.spec.mjs:243 '… in the hovered instance only (UX-13)'` (two grids, no row class, gone after leave). Not asserted: the band's fill is `--ex-row-hover-background` |
| UX-14 | MUST | pass | L3: `browser/features.spec.mjs:1483 'the chosen action is outlined, with and without forced colors (UX-14)'` |
| UX-15 | MUST | pass, clause not asserted | L2: `core/grid-row-kind.test.ts:25 '… stays with its row on scroll'`, `:45` (Placeholders); L3: `browser/stripes.spec.mjs:87 (UX-15)`, `browser/appearance.spec.mjs:380`. Not asserted: parity under a pager |
| UX-16 | MUST | pass | L3: `browser/stripes.spec.mjs:111`, `:226`, `:258` (UX-16) |
| UX-17 | MUST | pass | L3: `browser/selection-look.spec.mjs:76 'UX-17 (${chrome} Chrome) …'` |
| UX-18 | MUST | pass | L3: `browser/selection-look.spec.mjs:110`, `:131`, `:151` (UX-18); `ExGrid.Components/ShippedStylesheetTests.cs:210` |
| UX-19 | MUST | pass | L1/2: `cases/paint/selection-styles.json`, `ExGrid.MudBlazor.Tests/WrapperStylesheetTests.cs:25`, `ExSheet.MudBlazor.Tests/PaperStylesheetTests.cs:91`; L3: `browser/selection-look.spec.mjs:181`, `:194`, `:226` (+5), `browser/sheet-paper.spec.mjs:209`, `browser/mud.spec.mjs:160`; 150% via `chrome-150` |
| UX-20 | MUST | pass | L3: `browser/presentation.spec.mjs:329 'the Modified mark is a triangle … (UX-20)'` |
| UX-21 | MUST | pass, clause not asserted | L1: `cases/selection/heading-marks.json`, `core/heading-marks-property.test.ts:36`; L2: `core/grid-row-headings.test.ts:154`, `core/grid-selection.test.ts:275`; L3: `browser/heading-mark.spec.mjs:75`, `:316`, `:374` (+8). Not asserted: no row repainted as the Selection moves the Heading Mark (`grid-selection.test.ts:255` holds it for the pinned shade only) |
| A11Y-1 | MUST | pass | L2: `core/grid-dom.test.ts:54` (`role="grid"`), `core/grid-actions.test.ts:92` (gridcell), `core/grid-header-groups.test.ts:36` (row/columnheader); L3: `browser/aria-rows.spec.mjs:100` (rows owning headers and cells in the tree) |
| A11Y-2 | MUST | pass | L2: `core/grid-dom.test.ts:54` (1001), `core/grid-header-groups.test.ts:36` (1002 under a band), `core/grid-row-headings.test.ts:253` (hidden header); L3: `browser/aria-rows.spec.mjs:100` (402), `:139` (21), `:161` (1048577) |
| A11Y-3 | MUST | pass | L2: `core/grid-dom.test.ts:88` (indices after a scroll), `core/grid-header-groups.test.ts:150`; L3: `browser/aria-rows.spec.mjs:100`, `:139`, `:161` (far down), `browser/virtualisation.spec.mjs:579` (header `aria-colindex` after panning) |
| A11Y-4 | MUST | pass | L2: `core/grid-edit.test.ts:114`; L3: `browser/features.spec.mjs:1086`, `browser/key-field.spec.mjs:755`, `:786`, `:830` (+1) |
| A11Y-5 | MUST | pass | L2: `core/grid-edit.test.ts:114`, `:135`; L3: `browser/mud-app.spec.mjs:247 'WR-7: the two grids … stay independent'` (own id spaces), `browser/key-field.spec.mjs:821`, `:650` |
| A11Y-6 | MUST | pass | L3: `browser/features.spec.mjs:1583 'A11Y-6 …'` |
| A11Y-7 | MUST | pass | L2: `core/grid-dom.test.ts:315` (`aria-label` the number, text `####`), `core/grid-cell-paint.test.ts:96` |
| A11Y-8 | MUST | gap | No test greps for `aria-selected` after a drag (the bUnit check went with #501). Code writes it only on listbox options (completion, find-all list, filter values), never on a cell |
| A11Y-9 | MUST | pass | L3: `browser/features.spec.mjs:1559 'A11Y-9: a drag across 200 rows writes the live region once …'`; L2: `core/grid-selection.test.ts:119` |
| A11Y-10 | MUST | pass | L3: `browser/features.spec.mjs:1542 'A11Y-10 …'` |
| A11Y-11 | MUST | pass | L2: `core/grid-dom.test.ts:218` (every Placeholder `aria-busy="true"`) |
| A11Y-12 | MUST | pass, clause not asserted | L3: `browser/heading-mark.spec.mjs:187` (overlay layer `aria-hidden`), `browser/aria-rows.spec.mjs:100` (tree holds only rows). Not asserted: the Selection layers' `aria-hidden` (`grid-view.ts:2157-2159` sets it) |
| A11Y-13 | MUST | pass | L2: `core/grid-header-groups.test.ts:36` (`aria-colspan` = member count, 1 and 2) |
| A11Y-14 | MUST | gap | No test counts row paints across a drag with the live region; `core/grid-selection.test.ts:119` checks the announcement, not paints |
| A11Y-15 | MUST | pass, clause not asserted | L2: `core/grid-edit.test.ts:349 'ED-15: a Reject … shows and says the Consumer's sentence'` (`aria-invalid`, `.ex-announce` verbatim), `react/editor-slot-contract.ts:107`; L3: `browser/features.spec.mjs:988`. Not asserted: one write per Reject; nothing written on Accept or Flag |
| A11Y-16 | MUST | pass | L2: `core/grid-copy.test.ts:243 'A11Y-16 …'`; L3: `browser/features.spec.mjs:403 '… announced in the root's live region … again when repeated (A11Y-16)'` (DemoHost line has no role) |
| A11Y-17 | MUST | pass | L2: `core/grid-actions.test.ts:83 '… out of the tab sequence …'`; L3: `browser/features.spec.mjs:1461 (A11Y-17)` |
| A11Y-18 | MUST | pass, clause not asserted | L2: `core/grid-actions.test.ts:118`, `:155`, `:83` (label); L3: `browser/key-field.spec.mjs:885`. Not asserted: the attribute cleared while the chosen button is not painted |
| A11Y-19 | MUST | pass | L2: `core/grid-column-menu.test.ts:56` (menu named by header), `core/grid-filter-panel.test.ts:87` (dialog named by header), `core/grid-context-menu.test.ts:45`; L3: `browser/popovers.spec.mjs` (roles, per Chrome incl. MudBlazor) |
| A11Y-20 | MUST | pass | L2: `blazor/bridge.test.ts:134 'A11Y-20 …'`, `ExGrid.Components/ScriptGridHandleTests.cs:61`, `ExSheet.Components/ScriptSheetTests.cs:250` (+`ExPivot.Components/ScriptPivotTests.cs:65`); L3: `browser/circuit.spec.mjs:471 '… no grid to press or tab to until its circuit connects (A11Y-20)'` |
| A11Y-21 | MUST | pass | L3: `browser/key-field.spec.mjs:650`, `:885` (A11Y-21, per Chrome) |
| HG-1 | MUST | pass | L1: `cases/columns/header-group-layout.json:7 'HG-1 …'`; L2: `core/grid-header-groups.test.ts:36` (members declared out of order) |
| HG-2 | MUST | pass | L2: `core/grid-header-groups.test.ts:36` (width spans members), `:69 '… says its rowspan'` |
| HG-3 | MUST | pass | L1: `cases/columns/header-group-layout.json:90 'HG-3 …'`; L2: `core/grid-header-groups.test.ts:69` |
| HG-4 | MUST | pass | L1: `cases/columns/header-group-layout.json:16 'HG-4 …'` |
| HG-5 | MUST | pass | L1: `cases/columns/header-group-layout.json:22 'HG-5 …'`; L2: `core/grid-header-groups.test.ts:98 "'Apart' members are not adjacent"` |
| HG-6 | MUST | pass | L1: `cases/columns/header-group-layout.json:28`, `:34`, `:43` |
| HG-7 | MUST | pass | L1: `cases/columns/header-group-layout.json:49 '… naming the boundary'`, `:55`, `:63`; L2: `core/grid-header-groups.test.ts:98` |
| HG-8 | MUST | pass, clause not asserted | L2: `core/grid-header-groups.test.ts:36` (band 2 × 28 at one tier), `:150` (rows' place moves with tiers); L1: `cases/viewport/geometry.json:83`. Not asserted: the first painted row at a scroll offset identical at 0, 1 and 2 tiers |
| HG-9 | MUST | pass | L2: `core/grid-header-groups.test.ts:98` ('must exceed the header band — 2 tiers × 28px …') |
| HG-10 | MUST | gap | No test scrolls a group's first members out of the DOM and reads its `left`/`width` |
| HG-11 | MUST | gap | No test counts member resolution across scroll steps; `core/grid-header-groups.test.ts:113` keeps the rectangle across a re-push and a Sort but scrolls nothing |
| HG-12 | MUST | gap | No test of a long group label over narrow columns (no `####`, ellipsis, member Auto widths kept) |
| HG-13 | MUST | pass | `ExGrid.Components/ShippedStylesheetTests.cs:32 The_shipped_stylesheet_aligns_nothing_vertically`, `ExGrid.Tests/NoVerticalAlignmentTests.cs:21 Nothing_public_offers_a_vertical_alignment`; L3: `browser/presentation.spec.mjs:494 (HG-13)`; the row's grep over `dotnet/src/ExGrid/Assets/` finds nothing |
| HG-14 | MUST | pass | L1: `cases/columns/header-reorder.json:20 'HG-14 …'`; L2: `core/grid-column-gestures.test.ts:174`; L3: `browser/gestures.spec.mjs:30`, `:68` |
| HG-15 | MUST | pass | L1: `core/header-groups.test.ts:9 'HG-15 …'`, `cases/columns/header-reorder.json:39`; L3: `browser/gestures.spec.mjs:68 'HG-14/HG-15 …'` |
| HG-16 | MUST | pass, clause not asserted | L2: `core/grid-header-groups.test.ts:134 'a click on a rectangle sorts nothing …'`. Not asserted under tiers: Focus never enters the header; no tier row copied (the selection and copy code takes no tiers) |
| HG-17 | MUST | gap | No test counts elements per rectangle or row paints with groups (3 groups over 40 columns) |
| HG-18 | MUST | pass | L1: `cases/columns/header-group-toggle.json:61`; L2: `core/grid-header-group-toggle.test.ts:74`, `:110`; L3: `browser/group-toggle.spec.mjs:33 'HG-18 …'` |
| HG-19 | MUST | pass | L1: `cases/columns/header-group-toggle.json:26`, `:31`, `:46` (+4), `cases/columns/header-group-toggle-layout.json:7`, `:19`; L2: `core/grid-header-group-toggle.test.ts:178`, `:183` |
| HG-20 | MUST | pass | L2: `core/grid-header-group-toggle.test.ts:119 'HG-20 …'` |
| HG-21 | MUST | pass | L1: `cases/columns/header-group-toggle-layout.json:31`, `:45`, `:59`, `cases/keys/header-group-keys.json:7`, `:17`, `:24`; L2: `core/grid-header-group-toggle.test.ts:143`, `:155`, `:168` |
| HG-22 | MUST | pass | L1: `cases/columns/header-group-toggle.json:69`; L2: `core/grid-header-group-toggle.test.ts:130`; L3: `browser/group-toggle.spec.mjs:50 'HG-22 …' @blazor` (Server, answers held) |
| HG-23 | MUST | pass | L1: `cases/columns/header-group-toggle-layout.json:68`, `:76`, `:86`; L2: `core/grid-header-group-toggle.test.ts:194`, `:207`, `:225` (+4); L3: `browser/group-toggle.spec.mjs:63` |
| HG-24 | MUST | pass | L2: `core/grid-header-group-toggle.test.ts:322 'HG-24 … wears its %s mark, the reason its description'` (four states); L3: `browser/group-toggle.spec.mjs:81 'HG-24 …'` (tokens, forced colours) |
| DIR-1 | MUST | pass | L2: `core/grid-dom.test.ts:54` (`dir: 'ltr'` on the root) |
| DIR-2 | MUST | pass | L3: `browser/presentation.spec.mjs:401 'inside an RTL ancestor the grid stays an LTR island (DIR-2)'` |
| DIR-3 | MUST | pass | L3: `browser/presentation.spec.mjs:457 'DIR-3 …'` |
| DIR-4 | SHOULD | pass | `ExGrid.Components/ShippedStylesheetTests.cs:53 DIR4_the_shipped_stylesheet_uses_no_logical_property`; the row's grep over `ex-grid.css` finds nothing |
| VZ-1 | MUST | pass | L3: `browser/virtualisation.spec.mjs:212 'the element count is the same at 10³, 10⁵ and 10⁶ rows (VZ-1 …)'` |
| VZ-2 | MUST | pass | L2: `core/grid-dom.test.ts:54` (20 rows at 1000), `:88 'a row that stays on screen is not painted again …'`; L3: `browser/virtualisation.spec.mjs:212`, `:305` |
| VZ-3 | MUST | pass | L1: `cases/viewport/geometry.json:15`, `:22`, `:37` ('fractional heights are carried') |
| VZ-4 | MUST | pass | L1: `cases/columns/geometry.json:373` (slice), `:139`; L2: `core/grid-dom.test.ts:218` (pinned cells while flung); L3: `browser/virtualisation.spec.mjs:579 (DOM-3)` |
| VZ-5 | MUST | pass, clause not asserted | L2: `core/grid-dom.test.ts:218` (Placeholders until 150 ms of stillness, real cells at 150); L1: `cases/viewport/geometry.json:73`, `cases/columns/geometry.json:139`. Not asserted: a horizontal fling's Placeholders at layer 2 |
| VZ-6 | MUST | gap | No test sets `virtualiseColumns: false` (none in `packages/*/test`, `tests/browser`, `dotnet/tests`) |
| VZ-7 | MUST | pass | L2: `core/grid-dom.test.ts:54` (`.ex-spacer` 24 + 1000 × 24), `core/grid-pager.test.ts:63` |
| VZ-8 | MUST | gap | L1: `cases/viewport/compression.json:8`, `:16`, `:23`, `cases/viewport/geometry.json:119`. Gap: "header included" — the refusal counts rows × height alone (`viewport-geometry.ts:78`); the band only leaves the compression room (`grid-view.ts:3453`). Not asserted either: 1,000,000 × 40px throws |
| VZ-9 | MUST | pass | L1: `cases/viewport/box.json:7`, `:35`, `:53`; L2: `core/grid-dom.test.ts:280`; L3: `browser/scrollbar.spec.mjs:230`, `:240` |
| VZ-10 | MUST | pass | L3: `browser/scrollbar.spec.mjs:266 'at every zoom level, after moving again'` (Linux CI, classic scrollbars) |
| VZ-11 | MUST | gap | No test changes density at a scroll position; no re-anchor on a metrics change found in `grid-view.ts` (`#scrollTopPx` kept in pixels; `#topRowOwed` is set only for a page size change, `:3075`) |
| VZ-12 | MUST | pass | L2: `core/grid-dom.test.ts:294` (reported 0 and band-tall heights throw nothing, paint nothing), `core/grid-header-groups.test.ts:98` (declared height too small throws) |
| VZ-12a | MUST | pass | L3: `browser/stretch.spec.mjs:35`, `:47`, `:90` (VZ-12a) |
| VZ-12b | MUST | pass | L2: `core/grid-dom.test.ts:294 'ADR-0028 (VZ-12b) … heard once'`; L3: `browser/stretch.spec.mjs:74 '… names the parent (VZ-12b)' @blazor` |
| VZ-13 | MUST | gap | L2: `core/grid-pager.test.ts:51` (geometry over the page alone). Gap: no 2M-row paged result rendered, and the page-local re-anchor on a Density/RowHeight change is neither tested nor found (as VZ-11) |
| VZ-14 | MUST | pass | L3: `browser/scrollbar.spec.mjs:345 'the Focus stays readable with the OS doing the scaling'`, run by `ci.yml` `windows-desktop` at 125% (:606) and 150% (:615), Chrome and Edge |
| VZ-15 | MUST | pass | L1: `cases/viewport/compression.json:33`, `:46`, `:67`; L3: `browser/virtualisation.spec.mjs:154 (VZ-15)` and the scrollbar/BIG/SH/DC tests in project `chrome-150` (`playwright.config.mjs:209`) |
| VZ-16 | MUST | pass | L2: `core/grid-dom.test.ts:54` (`--ex-dp`), `core/grid-row-headings.test.ts:85` (2.25); L3: `browser/device-pixel-225.spec.mjs:37 'VZ-16 …'`, `browser/windows-desktop.spec.mjs:207 'VZ-16 … a real browser zoom is told …'` (`ci.yml` `windows-desktop` :615) |
| VZ-17 | MUST | pass, clause not asserted | L1: `core/column-geometry.test.ts:13`, `:25` (VZ-17), `cases/columns/geometry.json:455`, `ExGrid.Tests/ColumnGeometryTests.cs`; L2: `core/grid-row-headings.test.ts:85`; L3: `browser/part-c.spec.mjs:455 'VZ-17/DC-59 …'`. Not asserted: Selection, Cell Editor and hit test reading the rounded edges; View State, resize report and `####` keeping declared widths at a fractional ratio |
| VZ-18 | MUST | pass | L2: `core/grid-selection.test.ts:165`, `:196`; L3: `browser/virtualisation.spec.mjs:79`, `:113` (VZ-18). The Citrix check is withdrawn (row's note of 2026-11-02) |
| VZ-19 | MUST | pass | L1/2: `core/column-geometry.test.ts:38 'VZ-19 …'`, `ExGrid.Tests/ColumnGeometryTests.cs:57`; L3: `browser/sheet-paper.spec.mjs:714 'SH-41/VZ-19 (${chrome} Chrome) …'` (incl. `chrome-150`); the pixel is `.ex-cell-numeric`'s alone (`ex-grid.css:232`) |

## Gaps and notes

**Gaps**

- **FN-2** — No test refuses `source` with `rows`, or neither. The code refuses both by name (`grid-view.ts:2581`, `ExGrid.cs:475`). Close: one `mount` per case in `core/grid-dom.test.ts`, and one `Render` in `ExGrid.Components/ScriptGridSurfaceTests.cs`, each asserting the message.
- **FN-4** — No test runs one scenario through `GridSource.from` and `GridSource.fetch` and compares what is painted (the bUnit test went with #501). The grid has one source path (`rows` becomes `InMemoryGridSource.from`). Close: a `core/` test that scrolls, sorts and filters both sources over the same rows and compares `rowsOf(root)` at each step.
- **FN-22** — Untested, and the code does the opposite. TS `#take` compares `options.columns` with `#declared`, which is the Consumer's own array, and `sameItems` returns at once on `a === b` (`grid-view.ts:1464`, `:2620`, `:3053`). So the same array changed in place and handed again counts as no change. Blazor keeps the instance too (`ExGrid.cs:571` `_numberedColumns = Columns`; `SameColumns` then compares the list with itself). Probably a defect: the copy that ADR-0003's note of 2026-10-13 requires is never taken. Close: copy on take, then a test that pushes, splices and re-hands the same array.
- **UX-12** — Fails because A11Y-8 and A11Y-14 do.
- **A11Y-8** — No test checks for zero `aria-selected` after a 400-cell drag. In `packages/core/src` the attribute appears only on listbox options (completion, Find All list, filter values), never on a cell. Close: drag across 400 cells in `core/grid-selection.test.ts` and assert `root.querySelector('[role=gridcell][aria-selected], [role=row][aria-selected]') === null`. Also decide whether those listbox options count under "no element".
- **A11Y-14** — No test shows that announcing paints no row. `core/grid-selection.test.ts:119` reads the announcement and nothing else. Close: in that test, assert `data-ex-paint` on `.ex-viewport` is the same before and after the drag and the settle.
- **HG-10** — No test scrolls a group's first members out of the DOM. Close: a `core/grid-header-groups.test.ts` case with wide columns, `scrollLeft` past the first member, and the rectangle's `left`/`width` equal to the unscrolled values minus the scroll.
- **HG-11** — Resolution is not counted across scroll steps. Close: spy on the layout, or keep the `.ex-header-group` element identity and the paint id across 20 vertical scrolls.
- **HG-12** — No long-label test. Close: a group label wider than its members, asserting the ellipsis class, no `####`, and member widths equal to those without the group.
- **HG-17** — No test of cost per rectangle. Close: 3 groups over 40 columns, assert `.ex-header-group` count is 3 and row paints are unchanged against the ungrouped grid.
- **VZ-6** — `virtualiseColumns: false` appears in no test. Close: pan a wide grid with it off and assert every column is painted, no Placeholder appears, and the cells match the "on" case apart from the slicing.
- **VZ-8** — The row says "header included". The Script Core's refusal counts rows × height only (`viewport-geometry.ts:78`). The header band only comes out of the compression room (`grid-view.ts:3453`), so a result whose rows fit and whose band does not is compressed, not refused. Nothing asserts 1,000,000 × 40px refused at grid level either. Either the wording goes to §21, or the grid's check adds the band, with a test.
- **VZ-11** — No test, and no re-anchor found: after a Density/RowHeight change `#scrollTopPx` keeps its pixels, so the first visible row would move. `#topRowOwed` is set only when the page size changes (`grid-view.ts:3075`). Close: scroll to row P, change `density`, assert `data-ex-first-row` is P and the Focus's visibility is unchanged. This likely needs a fix, not only a test.
- **VZ-13** — Untested: a 2M-row paged result binding, and the page-local re-anchor on a Density change (the same missing re-anchor as VZ-11). `core/grid-pager.test.ts:51` holds only the geometry over one page.

**Pass, clause not asserted** (the code plainly does the unasserted part unless said)

- **FN-1** — Nothing asserts that no Window outlives its replacement (LV-22's area).
- **FN-3** — A source answering inside `needRange` (pushing synchronously) is never tested for re-entry. "Never during a render" has no Script Core equivalent.
- **FN-3a** — Only the downward edge is tested. Scrolling up near the Window's start, and the clamp at the total's end (`grid-view.ts:3584`), are not.
- **FN-6a** — No assertion that the suspension raises no width or pin notification. Only the pinned count after widening is read.
- **FN-7a** — Not asserted: a negative value in a column with no tone rule stays an ordinary cell, and the text, copy and raw forms are unchanged by the rule.
- **FN-12c** — A double-click on a grip of a whole-column selection, fitting each column to its own content, is not tested (`#onHeaderDoubleClick`, `grid-view.ts:10465`).
- **FN-16** — No test compares a page click's `needRange` with a scroll's.
- **FN-20** — The formula-bar test reads an ordinary cell, not one showing `####`.
- **FN-21** — Each clause is held for MudSelect and MudDatePicker on both Blazor hosts (`mud-app.spec.mjs:542/558/572`). Not asserted: the page's other grid (`#orders-paper`) is unaffected, and the Inner Popup clauses for a grid inside a MudDialog (`:620` only draws the list above the dialog and takes OK).
- **UX-5** — The React-host test is the paint half only. The count is seen only on Blazor, as rows staying the same elements (`mud.spec.mjs:225`); no .NET render count is read.
- **UX-13** — No test reads the band's colour against `--ex-row-hover-background`.
- **UX-15** — Stripe parity under a pager is not tested. Positions are absolute, so it follows from the scroll test.
- **UX-21** — "No row's render count moves" as the Selection moves the Heading Mark is not asserted. The paint-id check exists only for the pinned shade layer (`grid-selection.test.ts:255`).
- **A11Y-12** — `.ex-selection`, `.ex-selection-pinned` and `.ex-shade-pinned` set `aria-hidden` (`grid-view.ts:2157`), but only the Heading Mark layer's is asserted.
- **A11Y-15** — Not counted: one write per Reject. Nothing checks the live region stays empty on Accept or Flag.
- **A11Y-18** — Clearing the attribute while the chosen button is scrolled out of paint is not tested.
- **HG-8** — The first painted row at a scroll offset is never compared across 0, 1 and 2 tiers.
- **HG-16** — Focus never entering the header, and no tier row being copied, are not tested with Header Groups declared. Selection and copy take no tiers.
- **VZ-5** — The horizontal fling is decided at layer 1 (`columns/geometry.json:139`), but its Placeholders are not checked at layer 2.
- **VZ-17** — The rounded edges are asserted for cells, header and row headings only. Not asserted: Selection, Cell Editor and hit test read the same edges; the View State, a resize report and the `####` decision keep declared widths at 1.25, 1.5 and 2.25.

**Other notes**

- No OBSERVATIONAL row and no `n-a?` row falls in these prefixes.
- FN-12e, VZ-14 and VZ-16 rest on `ci.yml` `windows-desktop` (100%, 125%, 150%) and `macos-desktop`, as `ExGrid.Release.Tests/RepositoryTests.cs:28` lists.
- The FN-21 tests in `mud-app.spec.mjs` were read in full. They match the row's 2026-11-06 note except for the two clauses named above.

---

# B-fl-kb.md

# FL, SR, ED, SL, KB — evidence at c8b721f9 (0.2.0 sign-off)

Paths: `core/` = packages/core/test/, `react/` = packages/react/test/, `blazor/` = packages/blazor/test/,
`cases/` = tests/cases/, `browser/` = tests/browser/, C# = dotnet/tests/. Titles are shortened. No test was run.
Abbreviation: CNA = `pass, clause not asserted`.

| ID | Level | Result | Evidence |
|---|---|---|---|
| FL-1 | MUST | pass | react/filter-slot-contract.ts:93 `filterPanelSlotContract`, which imports only `@ex-grid/core`, run by react/chrome.test.ts:125, mui/test/contracts.test.ts:21, mantine/test/contracts.test.ts; inspected: packages/mui/src and packages/mantine/src import only `'@ex-grid/core'` (the package exports `.` alone); the ExGrid.MudBlazor panel is a separate assembly |
| FL-2 | MUST | pass | cases/source/from.json:328 'another column's Filter narrows a column's value list', :343 'a column's own Filter is left out of its value list' |
| FL-3 | MUST | pass | core/grid-filter-panel.test.ts:156 'TooMany gets the condition form'; core/filter-panel-choices.test.ts:148 'TooMany degrades to Excel's search box: contains'; browser/filter-panel.spec.mjs:91 'TooMany gets the condition form' (both Chromes) |
| FL-4 | MUST | CNA | OK applies / Cancel discards: core/grid-filter-panel.test.ts:106, :245 'Cancel sends nothing'; core/grid-filter-slot.test.ts:179. **Not asserted:** one `distinctValues` fetch per opening, and no Consumer notification on a tick before OK (the code fetches once, in the `FilterPanelModel` constructor) |
| FL-5 | MUST | pass | cases/query/filter.json:7 'columns combine with And', :17 'clauses within a column can combine with Or', :39 |
| FL-6 | MUST | pass | core/query.test.ts:48 'FL-6: every operator on every column type is either offered, with its cases, or refused'; cases/query/operators.json (108 cases); cases/query/text.json, numbers.json, dates.json; C# ExGrid.Tests/Cases/QueryCaseTests.cs:46 |
| FL-7 | MUST | CNA | cases/query/filter.json:48 'the Opaque Filter's meaning is the Consumer's'; cases/view-state/equality.json:3760 'a Query that differs in an Opaque Filter'; core/source-boundary.test.ts:107. **Not asserted:** the Opaque Filter kept through a panel's OK or Clear filter, and absent from the panel, at layer 2 (grid-view.ts:8388 and :8431 keep it) |
| FL-8 | MUST | pass | cases/source/from.json:93 'Row Sequence Version moves when a Filter changes the rows'; cases/selection/held.json:12 'dropped when the sort order changes'; core/grid-selection.test.ts:213 'new rows drop the Selection, and the Consumer hears it empty' |
| FL-9 | MUST | pass | core/filter-panel-choices.test.ts:22 'applied list starts chosen, intersected with the domain just fetched', :33 'set membership, never a count', :43 'nothing chosen is never silently no filter' |
| FL-10 | MUST | pass | core/filter-panel-choices.test.ts:77, :84 'a checked value the search hides is not applied'; browser/popovers.spec.mjs:621 'S sorts, E … Enter applies the search's matches' (both Chromes) |
| FL-11 | MUST | pass | core/filter-panel-choices.test.ts:110, :117; core/grid-filter-panel.test.ts:88 (shows '(Select All Search Results)'), :106; browser/filter-panel.spec.mjs:67 (both Chromes) |
| FL-12 | MUST | pass | core/grid-filter-panel.test.ts:325 'Tab … wraps back to the first command', :357; core/grid-column-menu.test.ts:51; cases/chrome/menu-keys.json:129; browser/popovers.spec.mjs:558, :591 (both Chromes) |
| FL-13 | MUST | pass | core/filter-panel-choices.test.ts:57, :88, :93; core/grid-filter-panel.test.ts:141 'offers to add to the filter in force'; browser/popovers.spec.mjs:720 (both Chromes draw the checkbox) |
| FL-14 | MUST | pass | core/filter-panel-choices.test.ts:173, :179, :184; core/grid-filter-panel.test.ts:245 'two conditions joined by Or apply as one spec' |
| FL-15 | MUST | pass | cases/chrome/menu-keys.json:99, :108, :115 (+4); core/grid-column-menu.test.ts:51 (letters underlined `[S]ort ascending`); browser/popovers.spec.mjs:621, :668, :703 (both Chromes) |
| FL-16 | MUST | pass | core/grid-column-menu.test.ts:51 '[C]lear filter (disabled)', :103 'C clears its filter'; cases/chrome/menu-keys.json:108; browser/popovers.spec.mjs:621 |
| SR-1 | MUST | pass | browser/features.spec.mjs:1141 'a header click sorts and the selection is untouched by it (SR-1)'; browser/sizing.spec.mjs:113; core/grid-selection.test.ts:317 |
| SR-2 | MUST | pass | cases/selection/keyboard.json:80 'Ctrl+Shift+Down from the first row is a whole-column selection', :88 'Ctrl+Space'; core/grid-row-headings.test.ts:229 'a click on the corner selects all' |
| SR-2a | MUST | pass | cases/selection/whole-ranges.json:63, :79, :87; browser/sizing.spec.mjs:96 'Shift+click on a header selects whole columns and does not sort'; core/grid-selection.test.ts:334 |
| SR-2b | MUST | pass | cases/selection/whole-ranges.json:7 'Ctrl+Space then Shift+Right gives two whole columns', :16, :27 (+2) |
| SR-2c | MUST | CNA | browser/sizing.spec.mjs:237, :251; browser/headings.spec.mjs:48, :71, :92; core/grid-selection.test.ts:98; core/grid-pager.test.ts:82 (Ctrl+Shift+↓ from the first row turns no page). **Not asserted:** under a pager, Shift+→ over whole columns turns no page |
| SR-2d | MUST | pass | browser/sizing.spec.mjs:113, :125, :149, :164; core/grid-selection.test.ts:357, :382 |
| SR-2e | MUST | pass | C# ExGrid.Tests/HeadingToggleTests.cs:8 (10 Facts); cases/selection/whole-ranges.json:143, :153; browser/sizing.spec.mjs:177, :194, :213 (Meta only where Meta is Command); browser/headings.spec.mjs:369 |
| SR-3 | MUST | pass | inspected: `grep -n '\.sort(' packages/core/src/dom/*.ts` prints nothing; core/grid-dom.test.ts:361 (painted first row follows the source's sort), :371 (a reversed array is painted reversed) |
| SR-4 | MUST | CNA | drops: core/grid-marks.test.ts:211 'marks survive a sort …; the Selection does not'; cases/selection/held.json:12. Keeps: cases/selection/held.json:7 'the same order keeps the selection', cases/source/from.json:123, :251 (version unmoved). **Not asserted:** at layer 2, a sort that leaves the sequence identical keeps the Selection |
| SR-5 | MUST | pass | cases/query/sort.json:7, :17, :27, :37 (+11: null ordering, stability, ordinal collation, mixed number types); C# ExGrid.Tests/SortSemanticsTests.cs |
| SR-6 | MUST | pass | core/grid-dom.test.ts:361 'a header says the Sort in force' (`none` / `descending`) |
| ED-1 | MUST | CNA | grid-view.ts:4863 makes one `input.ex-editor` per edit, and `#paintEditor` (:12432) floats it outside the rows; core/grid-edit.test.ts:175. **Not asserted:** at most one input under the root while editing, and row writes unchanged by editing |
| ED-2 | MUST | pass | core/grid-edit.test.ts:175, :221 'F2 opens Caret … F2 again is Overwrite', :295; cases/keys/editing-keys.json:7; browser/features.spec.mjs:78, :720, :758 (+9, incl. f4-enter-mode, declarations; :871/:932 @blazor) |
| ED-3 | MUST | pass | core/grid-edit.test.ts:308; cases/keys/editing-keys.json:7 'every mode claims commit, cancel, Tab'; browser/features.spec.mjs:95 'Escape cancels the editor and never blurs the grid mid-edit' |
| ED-4 | MUST | pass | core/grid-edit.test.ts:175; browser/features.spec.mjs:33 (React); browser/mud.spec.mjs:271 'the Wrapper's editor opens … with the typed character' @blazor |
| ED-5 | MUST | pass | core/grid-edit.test.ts:175 (the cell still reads 'Book-0' after the commit; 'the grid writes nothing'); core/grid-paste.test.ts:81 |
| ED-6 | MUST | pass | core/grid-edit.test.ts:785 'an edit with typing in it stays when the Window leaves its row' (the text unchanged); :679 |
| ED-7 | MUST | gap | no test changes the density while an edit is open. `#paintEditor` re-derives the box from `#metrics` on each pass, so the code appears to do it |
| ED-8 | MUST | gap | no test opens an edit over a Placeholder. `#isEditableCell` (grid-view.ts:4732) refuses a row outside the Window and refuses during placeholder mode |
| ED-9 | MUST | pass | browser/presentation.spec.mjs:515 "the editor's box is exactly the cell's (ED-9)" |
| ED-10 | MUST | pass | core/grid-paste.test.ts:81 'a block … raises one intent with every cell'; core/grid-edit.test.ts:392 'Ctrl+Enter fills the Selection with one paste intent' |
| ED-11 | MUST | pass | browser/gestures.spec.mjs:261 'a composing IME keydown is never taken (ED-11)' |
| ED-12 | MUST | CNA | core/grid-edit.test.ts:327 'a press on another cell commits and selects it'; core/grid-actions.test.ts:237; core/grid-pending-commit.test.ts:229 (a press made while pending is taken after the answer); core/grid-summary.test.ts:350. **Not asserted:** a header press commits; the editor's input stops propagation; presses on the rows, a header and a column menu during a slow `onEdit` (in order and reversed) raise one Edit Intent, and a Source replaced afterwards still discards the next editor |
| ED-13 | MUST | pass | core/grid-edit.test.ts:202 'ED-13: Control and Alt together type a character'; browser/features.spec.mjs:60 |
| ED-14 | MUST | pass | core/grid-edit.test.ts:175 (no validate → Accept → intent), :349, :376 (a Flag raises it unchanged); core/grid-cell-message.test.ts:291 |
| ED-15 | MUST | pass | core/grid-edit.test.ts:349 'a Reject holds every commit gesture'; core/grid-pending-commit.test.ts:229; browser/features.spec.mjs:980, :996; browser/script-grid.spec.mjs:112 @blazor |
| ED-16 | MUST | pass | core/grid-edit.test.ts:349 (a Flag raises the intent with '-5' unchanged); core/grid-cell-paint.test.ts:46 (a Cell State is painted only from the function); blazor/bridge.test.ts:782 |
| ED-17 | MUST | pass | core/grid-cell-message.test.ts:43 'asked once the Focus has stood still for 300 ms, never while painting', :105 'a run of arrows faster than the delay asks nothing', :228 |
| ED-18 | MUST | CNA | shape refusals: core/grid-paste.test.ts:211, :226, :259. **Not asserted:** that no validate function runs during a clipboard paste (grid-paste.ts never names `validate`) |
| ED-17b | MUST | pass | core/grid-cell-message.test.ts:118, :141, :158 (the open editor's message outranks it), :177; browser/features.spec.mjs:1018 |
| ED-17c | MUST | pass | core/grid-bands.test.ts:26 'a move within that row paints nothing', :45 'off unless asked'; core/grid-cell-message.test.ts:241 'not heard for a rest while nobody asks why'; inspected: packages/blazor/src has no pointer listener |
| ED-19 | MUST | pass | browser/features.spec.mjs:385 'ED-19: after a refused fill the editor stands, and Enter then raises exactly one Edit Intent'; core/grid-edit.test.ts:392 |
| ED-20 | MUST | CNA | core/grid-pending-commit.test.ts:324 'Ctrl+Enter waits for the verdict, a Reject holds the editor, and an Accept raises one fill'; browser/script-grid.spec.mjs:172 @blazor; C# ExGrid.Components/ScriptGridAnswerTests.cs:75. **Not asserted:** exactly one validate call per fill, and no validate call under the Editable refusal |
| ED-21 | MUST | CNA | core/grid-edit.test.ts:740 (`columnsChanged`, `sourceChanged`), :762 (`columnNoLongerEditable`), :436/:555 (`orderMoved`), :875 (a handler hands over other columns, accepted, and no discard follows). **Not asserted:** `rowLeftTheWindow`; a commit whose handler replaces the Source (accepted or refused); a refused commit whose handler changes the columns (the refused intent, then one discard) |
| ED-22 | MUST | pass | browser/circuit.spec.mjs:30 (describe), :585; browser/edit-stands.spec.mjs:544 (describe), :734 (describe); browser/declarations.spec.mjs:870, :2067 (+many); core/grid-pending-commit.test.ts:516 |
| ED-23 | MUST | pass | core/grid-edit.test.ts:308 'Backspace opens an empty Overwrite'; browser/excel-keys.spec.mjs:86 |
| ED-24 | MUST | CNA | core/grid-edit.test.ts:945 (one Clear Intent whose targets are the selection; refused whole as `targetNotEditable`); browser/excel-keys.spec.mjs:68. **Not asserted:** zero validate calls; the intent carries the Row Sequence Version |
| ED-25 | MUST | pass | cases/keys/grid-keys.json:186; browser/excel-keys.spec.mjs:51 (inside the editor), :126 (display-only) |
| ED-26 | MUST | pass | browser/edit-stands.spec.mjs:60, :152 (both Chromes), :179, :349 @blazor, :286, :322; browser/pointing-scope.spec.mjs:216 |
| ED-27 | MUST | pass | browser/edit-stands.spec.mjs:379 (both Chromes), :442; C# ExGrid.Components/ShippedStylesheetTests.cs:290 An_edit_whose_keyboard_is_elsewhere_is_drawn_with_a_1px_outline; core/stylesheet-lengths.test.ts:65 |
| ED-28 | MUST | CNA | browser/edit-stands.spec.mjs:475 (describe, 40 ms, both Chromes): :486 thirty times, :512 '=1'; core/grid-chrome-slots.test.ts:192. **Not asserted:** on /sheet at 150 ms, a character typed onto a cell and the Formula Bar pressed in the task that paints the Cell Editor, after which the bar keeps the keyboard (declarations.spec.mjs:870 is the nearest case) |
| ED-29 | MUST | pass | browser/declarations.spec.mjs:832, :870 @blazor, :899 (case 7k), :1214 |
| ED-30 | MUST | gap | composition held: core/grid-edit.test.ts:489, :508; browser/key-field.spec.mjs:187, :225, :249 (+9, incl. k6 :337, k15 :475, 150 ms @blazor); browser/windows-ime.spec.mjs:85 … :374 (CI `windows-ime`). **Missing:** a composition finished while a popover opens (Ctrl+F, Ctrl+1, Format Cells…). The 2026-10-30 note removed its tests and put nothing in their place, but the Statement and Pass columns still name the clause |
| ED-31 | MUST | CNA | browser/circuit.spec.mjs:619 'a press made behind a page move lands on the row painted under it at the press' @blazor; core/grid-selection.test.ts:459 (a press before the grid heard a scroll); core/grid-actions.test.ts:267. **Not asserted:** at layers 1/2, a press told an earlier slice or layout, another row order, reordered columns, or a layout no longer kept |
| ED-32 | MUST | pass | core/grid-pending-commit.test.ts:137, :156, :344 |
| ED-33 | MUST | pass | core/grid-pending-commit.test.ts:169 (aria-busy, nothing applied), :194, :229; browser/script-grid.spec.mjs:86 @blazor |
| ED-34 | MUST | gap | open on Blazor (ADR-0240's note of 2026-10-29): a cancel that reaches .NET after a synchronous `OnEdit`/`OnPaste` has written is still said as cancelled. The React/Script Core half is held: core/grid-pending-commit.test.ts:393, :422, :497; core/grid-fill-handle.test.ts:171; blazor/bridge.test.ts:745; C# ScriptGridAnswerTests.cs:93, ScriptGridFillTests.cs:85 |
| ED-35 | MUST | pass | blazor/bridge.test.ts:726, :760, :782; C# ExGrid.Components/ScriptGridAnswerTests.cs:75, :115, :222; browser/script-grid.spec.mjs:112, :133, :172 @blazor |
| ED-36 | MUST | CNA | layer 1: core/grid-breach.test.ts:60, :83, :92 (a set rule without a Row Key throws); C# ExGrid.Tests/GridRulesetTests.cs:156 ED36_a_set_rule_marks_every_cell_its_breach_names_by_row_key, :186. **Not asserted (layer 2):** `Error` painted on every named cell, one outside the Window once scrolled to, with the editor closed on the commit |
| ED-37 | MUST | pass | core/grid-breach.test.ts:74; C# GridRulesetTests.cs:173 ED37_every_involved_cell_carries_the_breach_message |
| ED-38 | MUST | pass | core/grid-breach.test.ts:119, :184 'F8 … brings it to an error far below the Window', :206 (no findError: F8 is the browser's); C# GridRulesetTests.cs:245; blazor/bridge.test.ts:853; browser/script-grid.spec.mjs:145 @blazor |
| ED-39 | MUST | CNA | core/grid-breach.test.ts:269, :286 (a Filter says hidden; the press steps as F8), :304 (asked again; a late answer dropped), :328; C# GridRulesetTests.cs:275, ScriptGridAnswerTests.cs:337. **Not asserted:** asked again on another source or another set of visible columns |
| SL-1 | MUST | pass | cases/selection/keyboard.json:242; C# ExGrid.Tests/SelectionKeyboardTests.cs:15 SL1_select_all_over_a_million_rows_is_one_rectangle_and_allocates_nothing_per_cell |
| SL-2 | MUST | pass | cases/selection/keyboard.json:251, :260, :269; C# SelectionKeyboardTests.cs:36 SL2_selecting_the_whole_result_succeeds_at_any_size; core/selection-boundary.test.ts:46 |
| SL-3 | MUST | CNA | version: cases/selection/held.json:7, :12, :17; core/grid-selection.test.ts:213; core/grid-marks.test.ts:211. Negative case: cases/source/from.json:251 (an edit that moves nothing keeps the version). **Not asserted (layer 2):** the Selection dropped when the visible-column set changes (core/grid-copy.test.ts:468 checks only the outline), and kept when values change and the order does not (`#reconcileSelection` does both) |
| SL-4 | MUST | CNA | core/grid-dom.test.ts:209 (a fresh array of the same columns is no change). **Not asserted:** the Selection survives it. `#reconcileSelection` compares names in order (`sameItems`) |
| SL-5 | MUST | pass | cases/selection/range.json:21, :26, :31; cases/selection/gestures.json:73, :113, :129 (the only cell cannot be taken out); cases/selection/active-cell-third-run.json:62 … :162 |
| SL-6 | MUST | CNA | core/grid-selection.test.ts:119 (one `ex-range` element for the range, beside its tint and the Focus); browser/virtualisation.spec.mjs:327. **Not asserted:** no cell class changes when the selection moves |
| SL-7 | MUST | pass | core/grid-off-screen.test.ts:28 (scrolled out, said, and brought back); core/grid-edit.test.ts:785 (editor text survives its row leaving) |
| SL-8 | MUST | pass | cases/selection/queries.json:16, :24 'overlapping ranges count twice'; cases/selection/range.json:19 |
| SL-9 | MUST | pass | core/grid-pager.test.ts:103 'Ctrl+A selects the page, and the status line … offers the whole result'; cases/selection/keyboard.json:219 |
| SL-10 | MUST | pass | core/grid-off-screen.test.ts:28 (scrolling); core/grid-pager.test.ts:103 (paging: 'outside the visible range') |
| SL-11 | MUST | pass | browser/features.spec.mjs:1628 'SL-11: a drag held against either boundary of a page does not turn it' |
| SL-12 | MUST | pass | cases/selection/edge-band.json:27 … (the rate rises with depth); core/grid-selection.test.ts:412 'a drag held in the bottom edge band scrolls a step a tick and extends with it' |
| SL-13 | MUST | pass | cases/selection/edge-band.json:321 'the ceiling stays one row short of the fling threshold', :328 |
| SL-14 | MUST | pass | browser/presentation.spec.mjs:535 @behaviour, :574 @blazor |
| SL-15 | MUST | CNA | released: browser/presentation.spec.mjs:535; core/grid-selection.test.ts:473, :498. **Not asserted:** coming back with the button held resumes the drag (layer 3) |
| SL-16 | MUST | pass | cases/selection/keyboard.json:219 'Ctrl+A under a pager names the page, and the Focus stays'; core/grid-pager.test.ts:103 |
| KB-1 | MUST | pass | inspected: grid-view.ts:2259 `root.addEventListener('keydown', …, true)`; browser/edit-stands.spec.mjs:60 (both Chromes); browser/mud-app.spec.mjs:247 @blazor; browser/features.spec.mjs:1122 |
| KB-2 | MUST | pass | inspection: the tables are in packages/core/src/keys/ (grid-keys.ts, editing-keys.ts). The only key sets in grid-view.ts are `modifierKeys` (the gate's release mechanics) and `appleCaretKeys` (Home and End on Apple, which the restatement names), with the PageUp/PageDown drop at :4994; C# ExGrid.Docs.Generator.Tests/KeyboardCompletenessTests.cs |
| KB-3 | MUST | pass | cases/keys/grid-keys.json:16, :24; browser/features.spec.mjs:1653 (off Apple, Meta+Enter fills nothing); `macos-desktop` in ci.yml:706 runs features.spec.mjs, sheet-vs-excel, sizing, part-c and write-lands with grep `$KB3` ('ED-19: after a refused fill', 'on an Apple platform': features.spec.mjs:385, :913, :932) |
| KB-4 | MUST | CNA | cases/keys/grid-keys.json:16, :24 (keyboard); browser/sizing.spec.mjs:213 (pointer: 'Meta counts as Ctrl … only where Meta is Command'). **Not asserted:** one layer-2 test that Ctrl+click and Ctrl+A agree for each platform value. Both read `#metaIsPrimary` (grid-view.ts:7351, :10156) |
| KB-5 | MUST | pass | cases/keys/grid-keys.json:9 … :59 (Home/End with Ctrl and Shift), :109; cases/selection/keyboard.json:7, :42, :54, :72; cases/selection/active-cell.json:119 |
| KB-6 | MUST | pass | cases/selection/cycling.json:7, :15, :23, :31, :39 |
| KB-7 | MUST | pass | cases/selection/cycling.json:92, :101 'no wrap target is invented' |
| KB-8 | MUST | pass | browser/features.spec.mjs:125, :167, :184, :236 (+:197 @blazor); browser/key-field.spec.mjs:574, :598, :848; browser/sheet.spec.mjs:820, :848 (both Chromes) |
| KB-9 | MUST | pass | core/grid-selection.test.ts:151 'the first key with nothing selected places the Focus on the first cell on screen, without its step' |
| KB-10 | MUST | pass | browser/features.spec.mjs:1679 'KB-10: Home and Ctrl+Home … pinned and unpinned alike' |
| KB-11 | MUST | pass | browser/features.spec.mjs:1182 'KB-11: arrows typed in a Template Column's input move its caret' |
| KB-12 | MUST | pass | browser/key-field.spec.mjs:616 (both Chromes), :865; browser/features.spec.mjs:1086; core/grid-edit.test.ts:175 |
| KB-13 | MUST | gap | only the key table: cases/keys/grid-keys.json:109 (`moveByViewport`); cases/selection/keyboard.json:149 (the Focus moves by the rows passed). **Missing:** Focus and Viewport moving by the same N, and the Focus's offset unchanged after presses 2 and 3 |
| KB-14 | MUST | gap | parts at layer 1: cases/selection/keyboard.json:160, :168 (the Focus clamps); cases/viewport/geometry.json:91, :105 (reveal). **Missing:** PageDown to the end at layer 2, with the Focus fully visible at every step |
| KB-14a | MUST | gap | no test reports a shrinking box (or a scrollbar appearing over the Focus) and checks scroll and reveal. core/grid-selection.test.ts:572 covers only a box that grows back |
| KB-15 | MUST | pass | browser/features.spec.mjs:1048 'Ctrl+PageDown is neither handled nor prevented (KB-15)'; cases/keys/grid-keys.json (the Control forms of PageUp/PageDown are the browser's) |
| KB-16 | MUST | pass | ran §22 Step 3's command (dod.md:1078) on packages/core/src/keys/: it printed nothing |
| KB-17 | MUST | pass | core/grid-context-menu.test.ts:198; core/grid-column-menu.test.ts:51, :69; core/grid-filter-panel.test.ts:245 (Cancel sends nothing); browser/popovers.spec.mjs:806 (both Chromes) |
| KB-18 | MUST | pass | core/grid-actions.test.ts:413 (Escape from the control: the root is focused); browser/features.spec.mjs:1416 on /cells |
| KB-19 | MUST | pass | browser/features.spec.mjs:1155 'a display-only grid does not take printable keys away from the page' |
| KB-20 | MUST | pass | core/grid-actions.test.ts:118; browser/features.spec.mjs:1365 |
| KB-21 | MUST | pass | core/grid-actions.test.ts:118 'the arrows choose, clamped; Space fires the chosen one and leaves'; browser/features.spec.mjs:1365 |
| KB-22 | MUST | pass | core/grid-actions.test.ts:155; browser/features.spec.mjs:1393, :1405 |
| KB-23 | MUST | pass | core/grid-actions.test.ts:413 (one request; no later mount handed it), :380; browser/page-follow.spec.mjs:247 @blazor |
| KB-24 | MUST | pass | browser/features.spec.mjs:1416 'Space puts the caret in a Template cell's field, and Escape brings the keyboard back' |
| KB-25 | MUST | pass | core/grid-edit.test.ts:417 'Space opens Overwrite …; with nothing selected Space opens nothing' |
| KB-26 | MUST | pass | core/grid-actions.test.ts:95 'a held Space fires once'; browser/features.spec.mjs:1433, :1447 |
| KB-27 | MUST | pass | browser/features.spec.mjs:1499 'a press dragged off an action leaves no button holding the keyboard' |
| KB-36 | MUST | pass | core/grid-actions.test.ts:176 'a click fires … and moves no Focus'; browser/inspectors.spec.mjs:91 (RI-4), :108 (RI-26), :119 (RI-5), :127 (RI-6), :139 (RI-7) @blazor |
| KB-28 | MUST | pass | cases/keys/grid-keys.json:100; core/grid-column-menu.test.ts:79; browser/popovers.spec.mjs:846 |
| KB-29 | MUST | pass | core/grid-context-menu.test.ts:131; core/grid-filter-slot.test.ts:215; browser/popovers.spec.mjs:241 (both Chromes) |
| KB-30 | MUST | pass | cases/chrome/menu-keys.json:7 … :153; core/grid-context-menu.test.ts:157, :173; browser/popovers.spec.mjs:514, :542, :576 (both Chromes) (+mui/mantine specs) |
| KB-31 | MUST | pass | core/grid-filter-slot.test.ts:290; react/filter-slot-contract.ts:111; browser/popovers.spec.mjs:591, :754 (both Chromes); browser/circuit.spec.mjs:428 @blazor |
| KB-32 | MUST | pass | browser/popovers.spec.mjs:283 (both Chromes), :332, :438 (+3 @blazor, keys typed at once with the answers held); browser/mud-app.spec.mjs:542 (from inside an Inner Popup) |
| KB-33 | MUST | pass | browser/circuit.spec.mjs:287 'keys typed straight after a key that opens a menu reach the menu (KB-33)' @blazor |
| KB-34 | MUST | CNA | cases/chrome/menu-keys.json:7 (the key resolved against the place); browser/circuit.spec.mjs:265 '↓ Enter … Copy with headers ran' @blazor, built-in Chrome. **Not asserted:** under the mud Chrome |
| KB-35 | MUST | pass | browser/mud-app.spec.mjs:607 (MudSelect: the list closes, the popover stands; MudDatePicker: the popover stands) @blazor, 150 ms |
| KB-40 | MUST | CNA | browser/features.spec.mjs:1713; browser/page-follow.spec.mjs:103; cases/selection/keyboard.json:176 (Shift+PageDown moves the Extent). **Not asserted:** Shift+PageDown moves the view a page with the Extent (grid or browser) |
| KB-41 | MUST | pass | cases/selection/cycling.json:133, :146, :157; browser/sheet-vs-excel.spec.mjs:281 |
| KB-42 | MUST | pass | cases/selection/active-cell.json:67, :76, :103; browser/sheet-vs-excel.spec.mjs:320; browser/features.spec.mjs:957 |
| KB-43 | MUST | pass | cases/selection/active-cell-third-run.json:30, :41, :142 (+2) |
| KB-44 | MUST | CNA | core/grid-leave.test.ts:63, :74, :89; core/grid-pending-commit.test.ts:422; browser/pivot.spec.mjs:498; browser/features.spec.mjs:249. **Not asserted:** a held Escape that closes a Formula Entry's list leaves the edit standing |
| KB-45 | MUST | CNA | browser/page-follow.spec.mjs:80 (arrows, both kinds), :103, :153 (resize not followed), :169 (editor) @behaviour; :195, :221 (scripted focus) @blazor; core/grid-edit.test.ts:160. **Not asserted:** that Ctrl+arrows, Home/End, PageUp/PageDown, Enter/Tab cycling and Ctrl+Backspace each follow; no follow once the keyboard has left or the Focus has moved on; the follow on the Blazor hosts |
| KB-39 | MUST | pass | cases/keys/grid-keys.json:144; core/grid-edit.test.ts:967; browser/excel-keys.spec.mjs:28, :126 |

## Gaps and notes

- **FL-4** (CNA): I found no test that counts `distinctValues` calls per opening, or that checks the Source's filter is unchanged after a tick and before OK. The Script Core fetches once, in `FilterPanelModel`'s constructor (packages/core/src/chrome/filter-panel-model.ts:177). A layer-2 test in grid-filter-panel.test.ts that spies on `distinctValues` and reads the filter after each tick would close it.
- **FL-7** (CNA): layer 1 holds the reference ignoring the Opaque Filter and a Query carrying it. No layer-2 test applies or clears a column's filter through the panel while an Opaque Filter stands. The code keeps it (grid-view.ts:8388, :8431). Close it with a grid-filter-panel test: OK with `opaque` set leaves `filter.opaque` unchanged.
- **SR-2c** (CNA): pager clause. Ctrl+Shift+↓ is held (grid-pager.test.ts:82); Shift+→ over whole columns under a pager is not. Close it by adding it to grid-pager.test.ts:82.
- **SR-4** (CNA): the keep direction is held only at layer 1 (held.json:7, from.json:123/251). A grid-level test where a sort or filter leaves the sequence identical and the Selection stays would close it.
- **ED-1** (CNA): no test counts inputs under the root or row writes during an edit. The code makes one editor element per edit and floats it outside the rows (grid-view.ts:4863, :12432). Add `querySelectorAll('input').length <= 1` and `watchWrites` to a grid-edit.test.ts case.
- **ED-7** (gap): no test changes the density mid-edit, in core/ or browser/ (presentation.spec.mjs:89 changes density with no edit open). `#paintEditor` re-derives the box from `#metrics` each pass. A layer-2 test is needed: open an edit, update the density, check the text is unchanged and the box is the new cell's.
- **ED-8** (gap): no test opens an edit on a Placeholder or on an unfetched row. `#isEditableCell` (grid-view.ts:4732) refuses outside the Window and in placeholder mode. A layer-2 test is needed: a fetching source with the Focus on a row not yet fetched; type a character, expect no `.ex-editor`; let the row arrive.
- **ED-12** (CNA): the header-press commit, the editor's input stopping propagation (grid-view.ts:4064/:11437 `stopPropagation`), and the slow-`onEdit` press ordering ("in order and reversed", then a Source replaced) are not asserted by Script Core tests.
- **ED-18** (CNA): no test checks that a validate spy is never called during a clipboard paste. grid-paste.ts never references `validate`. Add a counting `validate` to a grid-paste.test.ts case.
- **ED-20** (CNA): no test counts validate calls per fill (exactly one), checks the row it is run against at layer 2, or checks no call under the Editable refusal (grid-edit.test.ts:392's Book column has no validate). Extend grid-pending-commit.test.ts:324.
- **ED-21** (CNA): `rowLeftTheWindow` as a discard reason is not asserted anywhere in core/. Also not asserted: a commit whose own handler replaces the Source (accepted, then refused), and a refused commit whose handler changes the columns (the refused intent before one discard). Only the accepted columns case is held (grid-edit.test.ts:875).
- **ED-24** (CNA): grid-edit.test.ts:945 does not count validate calls and does not read `rowSequenceVersion` on the Clear Intent.
- **ED-28** (CNA): the 2026-10-01 Formula Bar clause has no test I could find (/sheet, 150 ms, a character typed and the bar pressed in the paint task). declarations.spec.mjs:870 types `=A1+` into an open edit first, which is a different case.
- **ED-30** (gap): the clause on a composition finished while a popover opens (Ctrl+F, Ctrl+1, Format Cells…) lost its three tests on 2026-10-30, and nothing replaces them. The note argues the case is moot over the Script Core, but the Statement and Pass columns still require it. Either a 0 ms test of that clause, or a rewrite of the row taking it out, would settle it. Every other clause is held, including the real IME on CI's `windows-ime` job (windows-ime.spec.mjs).
- **ED-31** (CNA): layer 3 (circuit.spec.mjs:619) and the scroll case (grid-selection.test.ts:459) are held. The layer 1/2 list (an earlier slice or layout, another order, reordered columns, a layout no longer kept) has no named Script Core tests.
- **ED-34** (gap): open on Blazor's ExGrid by the row's own note of 2026-10-29 (ADR-0240's note of that day). A cancel reaching .NET after a synchronous `OnEdit`/`OnPaste` has written is announced as cancelled; naming the true outcome needs a wire and core change. The React / Script Core half is held (grid-pending-commit.test.ts:393, :422, :436, :497; grid-fill-handle.test.ts:171), and so is the abort reaching .NET (bridge.test.ts:745; ScriptGridAnswerTests.cs:93).
- **ED-36** (CNA): the ruleset is held at layer 1 (TS and C#). No layer-2 test paints `Error` from the ruleset for an off-Window row once scrolled to, or checks that the editor closes on the commit.
- **ED-39** (CNA): re-asking is held for a new judgement and a Filter. Re-asking on another source or a changed set of visible columns is not.
- **SL-3** (CNA): at layer 2, only the version trigger is asserted. Not asserted: the Selection dropped on a visible-column change (grid-copy.test.ts:468 checks only the dashes), and the Selection kept when values change in place.
- **SL-4** (CNA): grid-dom.test.ts:209 shows a fresh equal columns array is no change, but no Selection was present. `#reconcileSelection` compares names (`sameItems`). Add a placed Selection to that test.
- **SL-6** (CNA): one element per range is asserted. No test checks that cell classes are unchanged as the selection moves (for example `watchWrites` on rows while dragging).
- **SL-15** (CNA): only "returning with the button released ends the drag" is asserted (presentation.spec.mjs:535; grid-selection.test.ts:473 at layer 2). Returning with the button held and resuming has no layer-3 test.
- **KB-4** (CNA): keyboard and pointer each read `#metaIsPrimary`, but no single test checks that Ctrl+click and Ctrl+A agree under `primaryIsMeta` true and false.
- **KB-13** (gap): no PageUp/PageDown grid test since the C# core went. grid-view.ts:4078 says the step is the rows fully visible. A layer-2 test in grid-selection.test.ts is needed: a known viewport height, three PageDowns, checking the Focus row step equals the scroll step and the Focus offset is unchanged after presses 2 and 3.
- **KB-14** (gap): no grid test presses PageDown to the end and checks the Focus is inside the visible box at each step. grid-consistency.test.ts checks only scroll bounds.
- **KB-14a** (gap): no test reports a shrinking box (`told.viewport` smaller) with the Focus near the bottom, then checks no scroll; then an arrow reveals; then a gutter appearing reveals.
- **KB-34** (CNA): circuit.spec.mjs:265 runs under the built-in Chrome only. The row asks for both Chromes.
- **KB-40** (CNA): no test checks Shift+PageDown moving the view (scrollTop) a page with the Extent. Layer 1 moves only the Extent.
- **KB-44** (CNA): no test checks that a held Escape closing a Formula Entry's (completion) list leaves the edit standing. Close it in grid-completion.test.ts with `repeat: true`.
- **KB-45** (CNA): only the arrows and extending (layer 3) and the editor opening (layer 2) are followed in tests. Not asserted: the other moves, no follow once the keyboard has left or the Focus has moved on, and the follow on the Blazor hosts (only the scripted-focus clauses run there, page-follow.spec.mjs:195/:221).
- Notes on rows judged pass: KB-2 is an inspection row whose layer-2 test went with ex-grid.js; the inspection holds. KB-16's command printed nothing. ED-13, KB-10 and KB-17 have notes that name their replacement tests, and those tests exist. KB-8's note removed a 150 ms test that its Pass column does not name, and features.spec.mjs:197 @blazor still covers a similar race. KB-35 is held for both MudSelect and MudDatePicker by mud-app.spec.mjs:607.

---

# C-cp-mk.md

# CP, CTX, BIG, PST, ERR, CON, PF, MEM, DOM, RR, ASY, ST, WR, SRV, MK — evidence at c8b721f9

Read-only mapping; nothing run. Paths shortened: `core/` = `packages/core/test/`, `browser/` = `tests/browser/`,
`cases/` = `tests/cases/` (run by Vitest: `core/clipboard.test.ts:155`, `core/source.test.ts`, `core/fetch.test.ts:138`),
`ExGrid.X/` = `dotnet/tests/ExGrid.X/`. "cna" below = `pass, clause not asserted`.

| ID | Level | Result | Evidence |
|---|---|---|---|
| CP-1 | MUST | pass, clause not asserted | L1 `cases/clipboard/copy.json:15 'past the cap it does not copy at all'`; L2 `core/grid-copy.test.ts:110 'no Selection, and one past the cap, are refused'`; L3 `browser/features.spec.mjs:321 'a misaligned selection refuses and the clipboard stays untouched (CP-1/CP-3)'`. Not asserted: in a real browser, the clipboard untouched by a copy refused **for the cap** (layer 3 refuses only a misaligned one) |
| CP-2 | MUST | pass, clause not asserted | `cases/clipboard/copy.json:22 'a whole column of a million cells copies'`, `:30`, `:39 'the cap is the Consumer's to raise'`; `core/grid-copy.test.ts:110` (cap 2: 2 copy, 3 refuse). Not asserted: 1,000,001 cells refused at the default cap (the +1 boundary is held at cap 2 only) |
| CP-3 | MUST | pass | `cases/clipboard/copy.json:108 'nothing selected…'`, `:78 'ranges lining up on neither axis…'`, `:114 'a misaligned selection over the cap reports the misalignment'`; `core/grid-copy.test.ts:94` |
| CP-4 | MUST | pass | `browser/features.spec.mjs:272 'the clipboard carries both formats, and #### never reaches it'` @behaviour; `core/grid-copy.test.ts:73` |
| CP-5 | MUST | pass | `browser/features.spec.mjs:272` (narrowed column paints `#`, clipboard holds 1000000); `core/grid-copy.test.ts:73` |
| CP-6 | MUST | pass, clause not asserted | React: `browser/features.spec.mjs:501 'CP-6 / ADR-0005: a Ctrl+C inside the Window writes with no permission granted and no prompt, by the copy event'`; Blazor: `browser/declarations.spec.mjs:1990 'CP-6/CP-10/CP-14 … (mud Chrome)'`, `browser/key-field.spec.mjs:526` (both with permissions granted). Not asserted: event route / no prompt on the Blazor hosts |
| CP-7 | MUST | pass | L1 `cases/source/fetch.json:384 'rows for a copy are one fetch … and kept nowhere'`; L2 `core/grid-copy.test.ts:141`, `:498`; L3 `browser/features.spec.mjs:1738 'CP-7: a copy of rows beyond the Window takes the asynchronous route'` |
| CP-8 | MUST | pass, clause not asserted | No test copies after a column reorder or with a column hidden. Code: copy reads the resolved columns, which are the display order the Consumer hands, a hidden column being one left out (`packages/core/src/dom/grid-copy.ts:26`, `view-state.ts:54`) |
| CP-9 | MUST | pass | `cases/clipboard/assemble.json:75 'vertically aligned ranges stack in position order'`, `cases/clipboard/copy.json:98 'segments come back in position order'`, `:88 'overlapping row spans … refused as misaligned'` |
| CP-10 | MUST | pass | L3 `browser/features.spec.mjs:561 'CP-10 / ADR-0005: text selected with the mouse, then …'` (3 ways) @behaviour, `browser/declarations.spec.mjs:1990` (mud, @blazor); L2 `core/grid-copy.test.ts:271` |
| CP-11 | MUST | pass | `cases/clipboard/paste.json:7`, `:86`, `:94`, `:102`, `:110 'range to one cell is refused'`, `:117 'a ragged shape is refused'` |
| CP-12 | MUST | pass | `cases/clipboard/paste.json:138 'a block into disjoint ranges is refused'`, `:148` |
| CP-13 | MUST | pass | `cases/clipboard/paste.json:178 'a cell outside every target range is a caller's defect'`, `:156`; `core/grid-paste.test.ts:81` |
| CP-14 | MUST | pass, clause not asserted | React: `browser/features.spec.mjs:597 'an Excel-shaped paste arrives typed and at full precision'`, `:642`; L1 `cases/clipboard/parse.json:73`, `:117`. Not asserted: an Excel-shaped paste on the Blazor hosts arriving typed in .NET (Blazor pastes are text only: `browser/circuit.spec.mjs:141`, `declarations.spec.mjs:1990`) |
| CP-15 | MUST | pass | `cases/clipboard/copy.json:108`, `cases/clipboard/paste.json:172 'nowhere to paste is refused with its own reason'` |
| CP-16 | MUST | pass | `cases/clipboard/paste.json:202`, `:235 'the declaration outranks every shape rule'`; `core/grid-paste.test.ts:211`; Ctrl+Enter: `core/grid-edit.test.ts:392` |
| CP-17 | MUST | pass | L1 `cases/clipboard/assemble.json:15`, `:23` (raw form), `:47`, `:57`; L3 `browser/features.spec.mjs:521` (header row), `browser/pivot-csv.spec.mjs:370` (raw form in HTML) |
| CP-18 | MUST | pass | `cases/clipboard/copy.json:122 'a selection exactly on it copies plainly and refuses with headers'`, `:131`; `core/grid-context-menu.test.ts:263` |
| CP-19 | MUST | pass | `browser/features.spec.mjs:521 'CP-19 / ADR-0005/0036: a menu copy writes with no permission granted and no prompt, by the asynchronous route'` (chrome + edge projects) |
| CP-20 | MUST | pass | `cases/clipboard/parse.json:13`, `:21`, `:27` |
| CP-21 | MUST | pass | `browser/circuit.spec.mjs:141 'a paste past a Server hub's message limit arrives whole (CP-21)'` @blazor, `browser/script-grid.spec.mjs:191`; console via `browser/fixtures.mjs:512-527` |
| CP-22 | MUST | pass | `core/grid-paste.test.ts:259 'a paste past the byte cap is refused whole, counted in UTF-8 across both flavours'` (exactly on cap parsed, one past refused, no intent; judged in the browser before .NET reads anything); default `packages/core/src/dom/grid-paste.ts:6` |
| CP-23 | MUST | pass | L2 `core/grid-copy.test.ts:168`, `:519`; L3 `browser/circuit.spec.mjs:244 'a clipboard write the browser rejects is refused by name (CP-23)'` @blazor |
| CP-24 | MUST | pass | L1 `cases/clipboard/fill.json:7`, `:15`, `:23`, `:31`; L2 `core/grid-edit.test.ts:993`; L3 `browser/excel-keys.spec.mjs:105` |
| CP-25 | MUST | pass | `cases/clipboard/fill.json:39`, `:53`, `:68`, `:76`, `:84`, `:93` (+3); `core/grid-edit.test.ts:993`, `:1018` (sourceUnavailable) |
| CP-26 | MUST | pass | `core/grid-copy.test.ts:306`, `:331`, `:588`; `browser/copied-range.spec.mjs:42` |
| CP-27 | MUST | pass | `core/grid-copy.test.ts:331`, `:354`, `:379` (+1); `browser/copied-range.spec.mjs:82` |
| CP-28 | MUST | pass, clause not asserted | `core/grid-copy.test.ts:306 'CP-26/CP-28 … only the next Escape leaves'`, `core/grid-leave.test.ts:119` (Interactive before outline); `browser/copied-range.spec.mjs:99`. Not asserted: the popover and the control layers peeled before the outline |
| CP-29 | MUST | pass, clause not asserted | `core/grid-copy.test.ts:468 'the order moving or the columns changing drops the outline'`. Not asserted: an edit opening (F2) removing it; `grid-view.ts:4829` (`#startEdit` clears `#copied`) does |
| CP-30 | MUST | pass, clause not asserted | `core/grid-copy.test.ts:447`; `browser/copied-range.spec.mjs:120` (`?upstream=1`, F9). Not asserted: the same for a copy gathered beyond the Window |
| CP-31 | MUST | pass | `browser/copied-range.spec.mjs:99 '… a paste leaves it (CP-28, CP-31)'` (no layer-2 test found) |
| CP-33 | MUST | pass | `cases/keys/copy-with-headers-keys.json`; `core/grid-context-menu.test.ts:293`, `:312`, `:330` |
| CP-32 | MUST | pass | `browser/copied-range.spec.mjs:145 'draws no outline, and the copy still lands'`, `:42` (dashed/none/0s, top edge pixels); `core/grid-copy.test.ts:550` |
| CTX-1 | MUST | pass | `core/grid-context-menu.test.ts:76`, `:93`; `browser/popovers.spec.mjs:178` (builtin @behaviour, mud @blazor) |
| CTX-2 | MUST | pass | `core/grid-context-menu.test.ts:106 'the core's commands, then the Consumer's'`; `browser/popovers.spec.mjs:152` |
| CTX-3 | MUST | pass | `core/grid-context-menu.test.ts:106 '… handed the row, the column and the Selection as rectangles in their version'` |
| CTX-4 | MUST | pass, clause not asserted | L1 `cases/keys/grid-keys.json:135`; L2 `core/grid-context-menu.test.ts:131`; L3 `browser/popovers.spec.mjs:190 'the ContextMenu key opens it on the Focus'`. Not asserted in layer 3: Shift+F10, and the browser's menu staying away under either key (`defaultPrevented` is checked for the mouse only, `:152`) |
| CTX-5 | MUST | pass | Inspection: `grid-view.ts:2267` (Viewport `contextmenu`), `:2222` (summary box), `menu-popover.ts:172-174` (its container); `summary-menu.ts` listens on its own element only |
| BIG-1 | MUST | pass | `browser/virtualisation.spec.mjs:39 'the far corner is reachable and painted at 10⁶ rows (BIG-1)'` |
| BIG-2 | MUST | pass | `browser/virtualisation.spec.mjs:212 'the element count is the same at 10³, 10⁵ and 10⁶ rows'` |
| BIG-3 | MUST | pass | `browser/virtualisation.spec.mjs:327 'Ctrl+A over 10⁶ × 100 is one rectangle, counted as 10⁸'` |
| BIG-4 | MUST | pass | `cases/viewport/compression.json:8`, `:16`, `:23 'a true height past the ceiling is refused'`; `cases/viewport/geometry.json:119` |
| BIG-5 | MUST | pass | `browser/virtualisation.spec.mjs:227`, `:260` |
| BIG-6 | OBSERVATIONAL | recorded in Step 6 | `browser/observational.spec.mjs:149 '… (DOM-5, PF-6, BIG-6)'` → metrics.json `BIG-6` |
| BIG-7 | OBSERVATIONAL | recorded in Step 6 | `browser/observational.spec.mjs:136 'mount to first painted row at 10⁶ rows (BIG-7)'` |
| PST-1 | MUST | pass | `cases/clipboard/paste.json:156 'one value into a whole column of a million rows is one range'`; `core/grid-paste.test.ts:81` |
| PST-2 | MUST | pass | `cases/clipboard/paste.json:156 '… rows not fetched included'`, `:164` |
| PST-3 | MUST | pass | `browser/gestures.spec.mjs:139`; `core/grid-off-screen.test.ts:28` |
| PST-4 | MUST | pass | `cases/clipboard/paste.json:128 'a payload far larger than the target is refused by shape, with no plan to raise an intent from'` |
| PST-5 | MUST | pass | `browser/gestures.spec.mjs:160 'a ~10MB paste parses without freezing the grid'` (next key ≤ 500 ms) |
| PST-6 | OBSERVATIONAL | recorded in Step 6 | `browser/gestures.spec.mjs:160` → metrics.json `PST-6` (see notes: 400 source cells, not 10⁵/10⁶) |
| PST-7 | MUST | pass | `cases/clipboard/parse.json:111 '&nbsp; is value, not markup'` |
| ERR-1 | MUST | pass, clause not asserted | copy cap/misalignment/empty `cases/clipboard/copy.json:15`, `:78`, `:108`; paste shape/editability `paste.json:117`, `:202`; oversized `viewport/compression.json:23`; width `columns/column-width-spec.json:12`; gutter `viewport/box.json:145`, `:151`. Not asserted: both-or-neither rows/Source (`grid-view.ts:2582`, `ExGrid.cs:476` throw; no test) |
| ERR-2 | MUST | pass | L1 `ExGrid.Tests/RefusalWordsTests.cs:39`, `:76`, `:89` (+3), `core/grid-words.test.ts:30`; L2 `core/grid-copy.test.ts:129`, `:243`, `core/grid-paste.test.ts:162`; L3 `browser/features.spec.mjs:403` |
| ERR-3 | MUST | pass | `core/fetch-boundary.test.ts:41 'a page whose rows run past its own total is refused as a failed fetch'`, `:52` |
| ERR-4 | MUST | gap | No test; the Script Core does not refuse the same row instance twice: `vouchesDistinctRows` is read by nothing in `packages/core/src/dom/`, and `cases/source/marks.json:236` treats an object handed twice as one row |
| ERR-5 | MUST | pass | `cases/source/fetch.json:313 'a failure is heard, kept, and leaves the rows on screen standing'`, `:330`, `:343` |
| ERR-6 | MUST | n-a? | The Range Request is raised by the script itself (`grid-view.ts:3549`); a missing or broken module is ERR-10's refusal, so "rows still requested" names nothing that exists |
| ERR-7 | MUST | pass | L3 `browser/features.spec.mjs:1768 'ERR-7: disposing a grid while its interop calls are in flight …'` @blazor; L2 `ExGrid.Components/ScriptGridSurfaceTests.cs:138` |
| ERR-8 | MUST | pass | `cases/viewport/box.json:145 'a vertical scrollbar that swallows the width is refused by name'`; `core/grid-copy.test.ts:110` (names `copyCellCap`) |
| ERR-9 | MUST | pass | `ExGrid.Components/ShippedAssetTests.cs:78`, `:93`; `ShippedFilesTests.cs:21`, `:31` (+Wrappers'); L3 `browser/freshness.spec.mjs:194`, `browser/shipped-files.spec.mjs:112`, `:147` |
| ERR-10 | MUST | pass, clause not asserted | L2 `ExGrid.Components/ScriptGridHandleTests.cs:61` (another stamp); L3 `browser/shipped-files.spec.mjs:186` (another version, older than the stamp), `:197`, `:213` (integrity-pinned). Not asserted for ExGrid in layer 2: no stamp, and no module |
| ERR-11 | MUST | pass | L2 `core/grid-dom.test.ts:37`, `pivot/test/pivot-view.test.ts:601`, `sheet/test/create-sheet.test.ts:2416`, `ExGrid.Components/ShippedAssetTests.cs:102` (+2); L3 `browser/shipped-files.spec.mjs:262`, `:276`, `:289` per product |
| CON-1 | MUST | pass | `browser/fixtures.mjs:512` (every test's fixture, written to console.json); `browser/harness.spec.mjs:204` |
| CON-2 | MUST | pass | `browser/fixtures.mjs:513` |
| CON-3 | MUST | pass | `browser/fixtures.mjs:520` (third-party warnings left for results.md) |
| CON-4 | MUST | pass | `browser/presentation.spec.mjs:141 'CON-4 / ADR-0013: no ResizeObserver loop error from attach through density changes and window resizes'` |
| CON-5 | MUST | pass | `browser/presentation.spec.mjs:602 'the Blazor error UI never appears (CON-5)'` @blazor |
| CON-6 | MUST | pass | `browser/fixtures.mjs:521` (Server host log per test, `:378`; WebAssembly's in the console) |
| CON-7 | MUST | pass | `dotnet/tests/UnobservedTaskWatch.cs:22` (assembly fixture, `GC.Collect`/`WaitForPendingFinalizers` at the end), compiled into every bUnit suite (`Directory.Build.targets:52`) |
| CON-8 | MUST | pass | `.oxlintrc.json:49` (`no-console`); `browser/fixtures.mjs:527`; `browser/features.spec.mjs:675 'CON-8 … a copy whose payload fails to build says so'` @blazor; `browser/harness.spec.mjs:234`, `:240`, `:246` |
| PF-1 | MUST | pass | Grep of `packages/*/src` and `dotnet/src`: only `grid-view.ts:11074` (ADR-0204's follow) and `sheet-blazor/src/razor-sheet-slots.ts:342,346` (toolbar list placement), both allowed; `browser/virtualisation.spec.mjs:504 'a scroll frame makes at most one interop call (PF-1)'` @blazor |
| PF-2 | MUST | gap | Read against ADR-0350 §6: `packages/pivot/src/report-grid.ts:103-105` binds mousedown/click/dblclick to every expand/collapse button its label Template paints, one per label cell — "nothing per cell bound to a listener". The three listeners the row's 2026-11-01 note names pass (`pivot-view.test.ts` 'the document is heard only while…') |
| PF-3 | MUST | pass, clause not asserted | Inspection `paint/cell-classes.ts` (one string per combination, composed once); `core/grid-cell-paint.test.ts:128`, `core/grid-header-groups.test.ts:113`, `core/grid-dom.test.ts:88`. Not asserted: the allocation slope (no Script Core measurement, as the row says) |
| PF-4 | MUST | pass, clause not asserted | No test counts offset reads (`OffsetReads` went with the C# core). Code: `grid-view.ts:3257` `#onScroll` reads `scrollTop` and `scrollLeft` once each per scroll event |
| PF-5 | MUST | pass, clause not asserted | Element count: `browser/virtualisation.spec.mjs:327` (10⁸ cells, Pinned Columns: +6 elements), `core/grid-selection.test.ts:119`, `:255`. Not asserted: rows painted independent of selection area (no layer-2 count) |
| PF-6 | OBSERVATIONAL | recorded in Step 6 | `browser/observational.spec.mjs:149` → `PF-6` |
| PF-7 | OBSERVATIONAL | recorded in Step 6 | `browser/observational.spec.mjs:193 'a selection drag, one row a step, costs this much per step (PF-7)'` |
| PF-8 | OBSERVATIONAL | recorded in Step 6 | `spikes/render-bench` harness; latest entry `spikes/render-bench/results/20261030-script-core` |
| MEM-1 | MUST | gap | No measurement: the C# `GC.GetAllocatedBytesForCurrentThread` scroll test went with the core (#501) and nothing in `packages/*/test` measures allocation at 10³ vs 10⁶ rows |
| MEM-2 | MUST | pass | `browser/memory.spec.mjs:82 'mounting and disposing the grid fifty times returns nodes and listeners to baseline (MEM-2)'` @behaviour (asserts ±2; also records) |
| MEM-3 | MUST | pass, clause not asserted | `ExGrid.Tests/GridSourceFetchTests.cs:83 'ASY-2 / MEM-3: each fetch's token source is disposed once'`. Not asserted: the Blazor adapter's DotNetObjectReference, IJSObjectReferences and CTSs counted against disposals; by reading each is disposed once (`ExGrid.cs:1481`, `:1514` `using`; `:2289-2325`) |
| MEM-4 | MUST | pass | L2 `core/grid-dom.test.ts:118 'MEM-4: destroying the grid removes every listener …'`; L3 `browser/memory.spec.mjs:124` @blazor |
| MEM-5 | MUST | pass | `browser/memory.spec.mjs:178 'a ten-minute scripted scroll does not grow the JS heap (MEM-5, MEM-6)'` @blazor, run with `EXGRID_SOAK=1` on CI's long run (`ci.yml:396`, `:528`); asserts ≤ 20% |
| MEM-6 | OBSERVATIONAL | recorded in Step 6 | `browser/memory.spec.mjs:178` → `MEM-6` |
| MEM-7 | OBSERVATIONAL | not recorded | Nothing measures it (§22 Step 6: `not run` until the Script Core has a measurement) |
| DOM-1 | MUST | pass | `browser/virtualisation.spec.mjs:212` |
| DOM-2 | MUST | pass | `browser/virtualisation.spec.mjs:327` (+6: tints, boxes, shade layer, Heading Marks); `browser/heading-mark.spec.mjs:164` (state markers = Row Headings) |
| DOM-3 | MUST | pass | `browser/virtualisation.spec.mjs:579 'the cells in the DOM are the painted rows × (pinned + scrollable columns on screen), exactly'` |
| DOM-4 | MUST | pass | `browser/features.spec.mjs:1122 'two instances stay independent: no --ex-* on :root, no window global'` @behaviour; `browser/mud-app.spec.mjs:247` @blazor |
| DOM-5 | OBSERVATIONAL | recorded in Step 6 | `browser/observational.spec.mjs:149` → `DOM-5` |
| RR-1 | MUST | pass | L2 `ExGrid.MudBlazor.Tests/MudExGridPaperTests.cs:128`; L3 `browser/mud.spec.mjs:225 'a theme switch re-renders nothing inside the grid'`, `browser/mud-app.spec.mjs:449` |
| RR-2 | MUST | gap | No test; by reading the opposite holds: a row-height change makes `metricsChanged` true (`grid-metrics.ts:193`), which resolves the columns anew (`grid-view.ts:2620-2622`, `:3048`), and every surviving row fails `was.columns === this.#columns` and is painted again (`grid-view.ts:12227-12229`) |
| RR-3 | MUST | pass, clause not asserted | No test. By reading, a metrics change resolves the columns anew and every painted row is painted again in that pass (same path as RR-2) |
| RR-4 | MUST | pass | `core/grid-dom.test.ts:88 'a row that stays on screen is not painted again when the Viewport moves one row'` |
| RR-5 | MUST | pass | `core/grid-dom.test.ts:178 'ADR-0003 RR-5 …'` |
| RR-6 | MUST | pass | `core/grid-bands.test.ts:26 '… a move within that row paints nothing'` |
| RR-7 | MUST | pass, clause not asserted | No test presses a key that moves nothing and counts writes; `core/grid-dom.test.ts:199` shows a pass with nothing changed writes nothing but the Settled Mark |
| RR-8 | MUST | pass, clause not asserted | No test hands an unchanged Scrollbar Gutter report; the report only invalidates a pass (`grid-view.ts:2319-2322`), which writes nothing unchanged (`core/grid-dom.test.ts:199`) |
| RR-9 | MUST | pass | Inspection: `ExGrid.Popover.cs:141-143` (`_drawPopover ??=`, `_popoverContentsStanding ??=`) |
| RR-10 | MUST | pass | Inspection: `ExGrid.cs:642`, `ExSheet.cs:378`, `ExPivot.cs:304`, `ExChart.cs:333`, `ExBoard.cs:284` (`ShouldRender() => _renderOwed`) |
| RR-11 | MUST | pass | `core/grid-bands.test.ts:26 'ADR-0029 UX-13 / RR-11 …'` |
| RR-12 | MUST | pass, clause not asserted | `core/grid-actions.test.ts:118`, `:380`, `:413` (behaviour). Not asserted: per-row paint counts across entering, choosing, leaving, and a focus request; by reading only the row whose `chosen` moved is painted again (`grid-view.ts` `PaintedRow.chosen`) |
| RR-13 | MUST | pass | `core/grid-dom.test.ts:100 'ADR-0038 RR-13 …'` |
| RR-14 | MUST | pass | `core/grid-dom.test.ts:199 'a pass with nothing changed writes nothing'` |
| ASY-1 | MUST | pass | `cases/source/fetch.json:99 'a superseded question is called off, and its answer never reaches the screen'`, `:116`; `core/fetch-boundary.test.ts:81` |
| ASY-2 | MUST | pass | `cases/source/fetch.json:353`; `ExGrid.Tests/GridSourceFetchTests.cs:83`; `ExGrid.Components/ScriptGridHandleTests.cs:269`; `core/grid-work-in-order.test.ts:330` |
| ASY-3 | MUST | pass, clause not asserted | No burst test; each scroll event reads the offsets then and paints from them (`grid-view.ts:3257-3277`) |
| ASY-4 | MUST | pass, clause not asserted | `core/grid-dom.test.ts:335 '… its range is asked for once'`; `cases/source/fetch.json:68`. Not asserted: a synchronous source cannot re-enter (no test answers inside `needRange`) |
| ASY-5 | MUST | pass, clause not asserted | No test fires the settle timer after destroy; `destroy()` cancels it (`grid-view.ts:2499`) |
| ASY-6 | MUST | pass | Judged from Step 1/4 logs: `tests/browser/playwright.config.mjs:143` (300 s), MTP `--timeout 5m` (§22 Step 1); all suites green |
| ASY-7 | MUST | pass | L2 `core/grid-dom.test.ts:237`, `:255`, `core/grid-selection.test.ts:509` (+cell-message, filter-panel, chrome-slots); L3 `browser/settled-mark.spec.mjs:30`–`:153` (@behaviour and @blazor), `browser/input-text-order.spec.mjs:5`–`:144` |
| ASY-8 | MUST | pass | `browser/differential.spec.mjs:26 'ADR-0240 §8: ${product} ends the same typed fast and slow, seed …'` @blazor, ExGrid/ExSheet/ExPivot, seeds `:40` |
| ST-1 | MUST | pass | `core/grid-consistency.test.ts:407` (seeds), `:411` (10⁶, passed locally with `EXGRID_ST1_MILLION=1`); seed to results.md (Step 5) |
| ST-2 | MUST | pass, clause not asserted | `core/grid-edit.test.ts:780`, `:785`; `browser/sheet.spec.mjs:698`, `:743`. Not asserted: an empty Window pushed at the end of a sequence paints nothing and throws nothing |
| ST-3 | MUST | pass | `browser/presentation.spec.mjs:89 'UX-3 / ST-3 / ADR-0027 P6 …'` |
| ST-4 | MUST | pass | `cases/view-state/write.json:79`, `:2236`, `read.json`; `ExGrid.Tests/QuerySerialisationTests.cs:92`, `GridViewStateJsonTests.cs` |
| ST-5 | MUST | pass, clause not asserted | `browser/circuit.spec.mjs:492 'a store change reaches both, a sort reaches one, and a shared source is refused'` (Server). Not asserted: same-dispatcher sharing and attach after disposal; no layer-2 test of `CircuitBinding` (`dotnet/src/ExGrid/CircuitBinding.cs:27-50` does both) |
| WR-1 | MUST | pass | L2 `ExGrid.MudBlazor.Tests/MudScriptFilterPanelTests.cs:37`; L3 `browser/filter-panel.spec.mjs:67`, `:91`, `:105` (each under both Chromes) |
| WR-2 | MUST | gap | The row asks for a `MudSelect` operator, a `MudDatePicker` for a date and a `MudSelect` true/false for a boolean; `MudExGridScriptFilterPanel.razor:146-178` draws a native select and a `MudTextField` for every type (ADR-0410's note of 2026-10-28). Gating held: `MudScriptFilterPanelTests.cs:54`, `browser/popovers.spec.mjs:768` |
| WR-3 | MUST | gap | No ExGrid.MudBlazor test registers a non-English localizer, or checks the Blank wording is the Chrome's (`MudScriptFilterPanelTests.cs:40` checks the core's "(Blanks)"); only ExSheet's (`ExSheet.MudBlazor.Tests/MudSheetPackageTests.cs:159`, `:185`) |
| WR-4 | MUST | pass, clause not asserted | Items/order/roles: `browser/popovers.spec.mjs:152` (mud, @blazor), keys `core/grid-context-menu.test.ts:157`. Not asserted: each item's Enabled state and Material icon, none for a Consumer's command; `MudExGridScriptMenu.razor:19-20` draws `Disabled`/`StartIcon` |
| WR-5 | MUST | pass | `browser/popovers.spec.mjs:144` (`CHROMES = ['builtin','mud']`), `browser/find.spec.mjs:44`–`:135`, `browser/filter-panel.spec.mjs` |
| WR-6 | MUST | pass | L2 `MudExGridPaperTests.cs:97`, `:108`; L3 `browser/mud-app.spec.mjs:449` |
| WR-7 | MUST | pass | `browser/mud-app.spec.mjs:137`, `:191`, `:247`, `:294`, `:355`, `:620` |
| WR-7a | MUST | pass | `browser/stretch.spec.mjs:56` |
| WR-8 | MUST | pass | `ExGrid.MudBlazor.Tests/WrapperScriptTests.cs:60`, `:72`, `:87` |
| WR-9 | MUST | pass | `browser/fixtures.mjs:512-527`; `browser/mud-app.spec.mjs:403`; `UnobservedTaskWatch.cs` in each Wrapper suite |
| WR-10 | MUST | pass, clause not asserted | CI `ci.yml:822` `mudblazor-floor` (layer 3, `--grep "$WRAPPED"`, `:839`), `:881` `mudblazor-range`. Not asserted: "WR-10 tests pin the role and name on every MudSelect a Wrapper draws" — none exist, and no Wrapper draws a MudSelect now |
| SRV-1 | MUST | pass | Inspection: `ExGrid.DemoHost.csproj:19`, `ExGrid.DemoHost.Server.csproj:11` reference only DemoPages; `Program.cs:63`, `App.razor:23,27` `InteractiveServer` (prerender default) |
| SRV-2 | MUST | pass | CI's Server-host layer-3 jobs; skips on Server only `measure-live.spec.mjs:26`, `measure-pivot.spec.mjs:26`, `shipped-files.spec.mjs:187` (named exceptions) |
| SRV-3 | MUST | pass | `browser/circuit.spec.mjs:492` |
| SRV-4 | MUST | pass | `browser/fixtures.mjs:521`, `:378` (Server host log) |
| SRV-5 | MUST | pass, clause not asserted | `browser/circuit.spec.mjs:30`, `:265`, `:428` (+6), `page-follow.spec.mjs:195`, `:221`, `format-cells.spec.mjs:191` (300 ms). Not asserted: CP-21 and CP-23 under 150 ms (`circuit.spec.mjs:141`, `:244` run at the proxy's 0 ms) |
| SRV-6 | OBSERVATIONAL | recorded in Step 6 | `browser/measure.spec.mjs:33 'what the pointer reports cost on a circuit, at 0, 50 and 150 ms'` (`EXGRID_MEASURE=pointer`) |
| SRV-7 | MUST | pass | `browser/circuit.spec.mjs:780`, `:793` (both Chromes, @blazor), `script-grid-chrome.spec.mjs:126`, `format-cells.spec.mjs:242` @behaviour; `MudScriptFilterPanelTests.cs:80`, `MudScriptFindPanelTests.cs:63` |
| MK-1 | MUST | pass | `cases/rows/row-mark-rules.json:7`, `cases/source/marks.json:144`, `:159`; `core/grid-marks.test.ts:168`; `browser/marks.spec.mjs:72` |
| MK-2 | MUST | pass | `cases/rows/row-mark-rules.json:36`; `core/grid-marks.test.ts:129`; `core/grid-row-kind.test.ts:100` (Group/Total) |
| MK-3 | MUST | pass | `core/grid-marks.test.ts:108`, `:129`, `:168` (Ctrl+A+Space over 10⁵ rows: one intent); `cases/source/marks.json:174`, `core/fetch-marks.test.ts:204`, `ExGrid.Tests/RowMarkFetchTests.cs:174` |
| MK-4 | MUST | pass | `core/grid-marks.test.ts:108 '… only that row paints again'` |
| MK-5 | MUST | pass | `core/grid-marks.test.ts:211`; `cases/source/marks.json:65` |
| MK-6 | MUST | pass | `browser/marks.spec.mjs:41` @behaviour (chrome+edge); `core/fetch-marks.test.ts:152`; `RowMarkFetchTests.cs:114` |
| MK-7 | MUST | pass | `browser/marks.spec.mjs:56`; `core/fetch-marks.test.ts:177`; `cases/source/marks.json:95` |
| MK-8 | MUST | pass | `core/fetch-marks.test.ts:163`; `cases/source/marks.json:112`; `RowMarkFetchTests.cs:128` |
| MK-9 | MUST | pass, clause not asserted | `core/grid-marks.test.ts:228`, `:257`, `:282` (+2). Not asserted: the pager clauses (a header press on a page turned since; a pager added, removed or resized between press and click), and "Mark all N rows" refused under a moved order; `grid-view.ts:4184-4202` does them |

## Gaps and notes

Counts: pass 100 · pass, clause not asserted 28 · gap 6 · n-a? 1 · OBSERVATIONAL 10 (one not recorded) = 145.

### Gaps
- **ERR-4** — No test refuses a null row or one instance twice in a Window. The Script Core reads `vouchesDistinctRows` nowhere in `packages/core/src/dom/`, and `cases/source/marks.json:236` treats an object handed over twice as one row by identity. So the code may disagree with the row, not just lack a test. Closing it needs a decision first (does ADR-0003's refusal still hold over the Script Core?), then a layer-2 test handing `rows: [a, a]` and `[a, null]`.
- **PF-2** — `packages/pivot/src/report-grid.ts:103-105` binds three listeners to each expand/collapse button ExPivot's label Template paints, one per label cell. That reads against ADR-0350 §6's "nothing per cell bound to a listener", which holds for every Script Core. The fix is to delegate from the report grid's root, or to admit Template cells in a decision. Note: `navigator.clipboard`'s `clipboardchange` listener (`browser-reports.ts:167`) and `window.matchMedia` (`:128`) are held for the grid's life. These are "told" reports that ADR-0170 and CLAUDE.md admit, but ADR-0350 §6's note of 2026-11-01 ("an instance at rest listens to nothing outside itself") does not name them. They are not counted as failures here.
- **MEM-1** — The only measurement (C# `GC.GetAllocatedBytesForCurrentThread` around scroll steps) went with #501, and unlike PF-3 and MEM-7 the row was not annotated. It needs a Script Core allocation measurement, ADR-0350 §15's comparison, at 10³ and 10⁶ rows.
- **RR-2** — No test. Reading the code points the other way: a row-height-only change is a `GridMetrics` change (`grid-metrics.ts:193`), so `#columns` is resolved anew (`grid-view.ts:2620-2622`, `:3048`). Every surviving row then fails `was.columns === this.#columns` (`:12229`) and is painted again. A layer-2 test with a MutationObserver around a `rowHeight` update would settle it, and would likely fail.
- **WR-2** — The row is out of date against ADR-0410's note of 2026-10-28. The panel now draws a native select for the operator and a `MudTextField` for every type's value (`MudExGridScriptFilterPanel.razor:146-178`), so there is no MudSelect, MudDatePicker or boolean MudSelect. The row needs rewriting (a DoD change), plus a layer-2 test pinning the controls as decided.
- **WR-3** — No ExGrid.MudBlazor test registers a non-English localizer or checks the Blank wording comes from `MudExGridWords`. It needs a bUnit test over `MudExGridScriptFilterPanel` with a stub `ILocalizationInterceptor`.

### n-a?
- **ERR-6** — "Rows still requested" assumes a C# core whose Range Request outlives a broken JS module. The script is now the grid (`grid-view.ts:3549`), so a missing or broken module is ERR-10's refusal, shown and never busy: `ScriptGridHandleTests.cs:61`, `shipped-files.spec.mjs:186`, `:213`.

### Clauses not asserted (code plainly does the rest)
- **CP-1** — No layer-3 copy past the cap. **CP-2** — 1,000,001 at the default cap. **CP-6** — The Blazor half of the event route / no prompt is untested. SRV-2's 2026-10-29 note says `features.spec.mjs` asserts it "on both hosts", but `features.spec.mjs:501` is @behaviour only. **CP-8** — No copy after a reorder or hide. **CP-14** — No typed Excel paste on a Blazor host. **CP-28** — Popover and control layers before the outline. **CP-29** — An edit opening. **CP-30** — A copy gathered beyond the Window.
- **CTX-4** — Shift+F10 and the browser menu staying away, in layer 3.
- **ERR-1** — Rows/Source exclusivity. **ERR-10** — Layer 2's no stamp and no module for ExGrid.
- **PF-3** — The allocation slope; the row itself says it is not checked. **PF-4** — No offset-read count. **PF-5** — Rows painted independent of selection area.
- **MEM-3** — No counting stubs over the Blazor adapter.
- **RR-3** — Exactly once per visible row. **RR-7**, **RR-8** — No-op inputs. **RR-12** — Per-row counts across Interactive.
- **ASY-3** — A scroll burst. **ASY-4** — Synchronous re-entrancy. **ASY-5** — A timer after destroy.
- **ST-2** — An empty Window at the end. **ST-5** — Same-dispatcher sharing and after-disposal attach.
- **WR-4** — Enabled state and icons. **WR-10** — The "WR-10 tests" pinning MudSelect roles do not exist, and no Wrapper draws a MudSelect.
- **SRV-5** — CP-21 and CP-23 are not run under 150 ms.
- **MK-9** — The pager clauses and "Mark all N rows" under a moved order.

### Observational
- **BIG-6, BIG-7, PF-6, PF-7, DOM-5** — `observational.spec.mjs`. **MEM-6** — `memory.spec.mjs:178` (soak). **SRV-6** — `measure.spec.mjs:33`. **PF-8** — `spikes/render-bench/results/`.
- **PST-6** — `gestures.spec.mjs:160` records 400 source cells (about 10 MB), not the 10⁵ and 10⁶ source cells the row names.
- **MEM-7** — Nothing records it (`not run`, §22 Step 6).

---

# D-dc-sh.md

# DC and SH rows against c8b721f9 (v0.2.0-rc.3)

Paths: `core/…` and similar are `packages/<pkg>/test/…`; `browser/…` is `tests/browser/…`; `cases/…` is `tests/cases/…`;
`ExcelCases/…` is `dotnet/tests/ExSheet.Engine.Tests/ExcelCases/…`, run by `sheet-engine/excel-cases.test.ts:368` and
`ExSheet.Engine.Tests/ExcelCaseTests.cs`. Titles are shortened.

| ID | Level | Result | Evidence |
|---|---|---|---|
| DC-1 | MUST | pass | core/grid-row-headings.test.ts:51 'none given, no band'; core/grid-fill-handle.test.ts:65 'not without the declaration'; core/grid-formula-bar.test.ts:138 (bar off: no `.ex-formula-bar`); core/grid-completion.test.ts:122 'DC-1: nothing declared, nothing asked' (+2 ExGrid.Tests CellTypeTests.cs:19, CellAlignTests.cs:16) |
| DC-2 | MUST | pass | core/grid-cell-paint.test.ts:204 'where a header click selects, it selects the whole column and sorts nothing'; cases/selection/headings.json 'declared header click selects the whole column', 'Shift+click … the Focus stays'; browser/sheet.spec.mjs:77 'SH-18/DC-2/DC-3' |
| DC-3 | MUST | pass | core/grid-row-headings.test.ts:51, :125 'press selects the row … Shift extends', :229 'corner selects all, Focus top-left on screen'; browser/sheet.spec.mjs:77, :134; browser/headings.spec.mjs:92 'Row Heading click … scrollLeft where it was' |
| DC-4 | MUST | pass, clause not asserted | cases/selection/headings.json '(DC-4): a row selected by its heading cycles only through real columns'; not asserted: Ctrl+A then copy yields no heading label |
| DC-5 | MUST | pass | core/grid-row-headings.test.ts:253 'the band goes, the rows start at the Viewport's top'; :51 (no Row Headings unless given) |
| DC-6 | MUST | pass | core/grid-row-headings.test.ts:51 'at the width declared', :74 'undeclared, the width is the wider…'; core/stylesheet-lengths.test.ts:192 'every length in ex-grid.css is a token fallback…'; inspected `dotnet/src/ExGrid/Assets/ex-grid.css:591` (no width) |
| DC-7 | MUST | pass | cases/selection/headings.json 'Ctrl+arrow follows the Consumer's edge answer…'; ExGrid.Tests/EdgeAnswerTests.cs:25, :34, :63; browser/sheet.spec.mjs:52 'SH-18/DC-7' |
| DC-8 | MUST | pass, clause not asserted | cases/clipboard/spill.json 'DC-8: a block onto one cell … plans the block', 'one plan … one notification'; core/grid-paste.test.ts:226 '…then it becomes the Selection'; browser/declarations.spec.mjs:1965, :2170; not asserted: nothing scrolls to show the block |
| DC-9 | MUST | pass | cases/clipboard/spill.json 'DC-9: without the declaration, range to one cell keeps its refusal'; core/grid-paste.test.ts:226 (`singleCellTarget`) |
| DC-10 | MUST | pass | cases/clipboard/spill.json 'DC-10: past the last row / last column refused by name', 'Editable is judged on the spilled block'; core/grid-paste.test.ts:226 (`spillPastExtent`) |
| DC-11 | MUST | pass | core/grid-placement.test.ts:67 'placed … heard once, Focus scrolled into view', :81 'request under another Row Sequence Version places nothing'; browser/sheet.spec.mjs:174 |
| DC-12 | MUST | pass | cases/selection/fill-rules.json 'bottom-right, top-right of whole columns, bottom-left of whole rows, nowhere on every cell'; core/grid-fill-handle.test.ts:65, :81; browser/selection-look.spec.mjs:476, :497, :518 (both Chromes) (+3 declarations.spec.mjs:1864, :1902, :1942) |
| DC-13 | MUST | pass | core/grid-fill-handle.test.ts:91 'one axis, outline, one intent'; browser/declarations.spec.mjs:1706 'DC-13/DC-27 (both Chromes)', :1732, :1775 |
| DC-14 | MUST | pass | core/grid-fill-handle.test.ts:224 'target over a column that is not Editable is refused whole before anything is raised' |
| DC-15 | MUST | pass | inspection of `packages/core/src/dom/`: no layout read in the header click, Headings, fill drag or edge answer; panel listeners on elements inside the root (see note) |
| DC-16 | MUST | pass, clause not asserted | core/grid-cell-paint.test.ts:178 'an edit opens on the Consumer's text'; core/grid-edit.test.ts:221 'F2 opens Caret on the cell's text'; browser/sheet.spec.mjs:41; not asserted: Overwrite over a declared opening text (the test titled 'typing still replaces it' types nothing) |
| DC-17 | MUST | pass | core/grid-completion.test.ts:129, :143 '↑ and ↓ choose, Tab accepts', :169; browser/declarations.spec.mjs:51, :94, :170 (both Chromes) |
| DC-18 | MUST | pass | core/grid-completion.test.ts:202 'an answer for text that has since changed is dropped' |
| DC-19 | MUST | pass | core/grid-point.test.ts:104, :153, :199; browser/declarations.spec.mjs:568, :606, :624 (+2 :668 both Chromes, f4-enter-mode.spec.mjs:139) |
| DC-20 | MUST | pass | browser/declarations.spec.mjs:965 'DC-20: typed quickly on a 150 ms circuit, no arrow points where the text forbids it @blazor' |
| DC-21 | MUST | pass, clause not asserted | core/grid-formula-bar.test.ts:70 'bar reads the Focus cell's text and the Name Box names the Focus, band off the Viewport'; not asserted: on a display grid the bar shows the full value behind a `####` |
| DC-22 | MUST | pass | core/grid-formula-bar.test.ts:87 'Enter commits it once', :109 'shown in the bar … Escape cancels the one edit'; browser/sheet.spec.mjs:396 'SH-18/DC-22' |
| DC-23 | MUST | pass | cases/columns/grid-metrics.json (`formulaBarHeightPx`); core/grid-formula-bar.test.ts:70 (scroller height = 480 − band) |
| DC-24 | MUST | pass | inspection of `grid-view.ts`, `cell-editing.ts`, `formula-bar.ts`: caret read in place (`grid-view.ts:6247`), bar caret on keyup/mouseup (`:2227`); no layout read on this path (see note) |
| DC-26 | MUST | pass | core/grid-cell-paint.test.ts:96 'a cell declared a Number in a Text column is right-aligned and becomes ####'; ExGrid.Tests/CellTypeTests.cs:59, :71 |
| DC-27 | MUST | pass | core/grid-fill-handle.test.ts:91, :125, :138; blazor/bridge.test.ts:711; browser/declarations.spec.mjs:1706 |
| DC-28 | MUST | pass | browser/declarations.spec.mjs:756 'keys typed into the Formula Bar before a 150 ms circuit has answered … keep their meaning @blazor', :997 |
| DC-29 | MUST | pass | core/grid-cell-paint.test.ts:106 'a cell's own alignment'; ExGrid.Tests/CellAlignTests.cs:24, :33 |
| DC-30 | MUST | pass | core/grid-edit.test.ts:967 'Ctrl+Z and Ctrl+Y forwarded … never while an edit is open; unheard they are the browser's'; browser/declarations.spec.mjs:2256, :2295 @blazor, :2337 |
| DC-31 | MUST | pass | browser/declarations.spec.mjs:107 '← and → move the caret and the list stays', :136 '=SS completes the span at the caret', :647 |
| DC-32 | MUST | pass | core/grid-copy.test.ts:561 'the Consumer's two flavours are written … a refusal says the Consumer's sentence', :588, :611 (asynchronous route) |
| DC-33 | MUST | pass | cases/clipboard/parse.json; browser/declarations.spec.mjs:2170 'Excel's column … same flavours', :2199 'SH-14/DC-33 @blazor' |
| DC-34 | MUST | pass | core/grid-point.test.ts:171 'a caret moved into the text is Caret'; browser/declarations.spec.mjs:668 (both Chromes), :719 @blazor |
| DC-35 | MUST | pass | core/grid-cell-paint.test.ts:106 '…painted text fitted to the content width, named by its value' (`aria-label`) |
| DC-36 | MUST | pass | core/grid-column-gestures.test.ts:64 'a grip stands … only while a resize is heard', :140 'double click fits'; browser/declarations.spec.mjs:2361 |
| DC-37 | MUST | pass | core/grid-context-menu.test.ts:123 'may leave Copy with headers off', :312 'hides Copy with headers leaves Ctrl+Shift+C to the page' |
| DC-38 | MUST | pass | core/grid-paste.test.ts:125 'a paste the Consumer refuses leaves the Selection … nor does a spill'; browser/declarations.spec.mjs:2150 |
| DC-39 | MUST | pass | browser/sheet-vs-excel.spec.mjs:251 'while a drag is held the Name Box shows 4R x 3C, D5 after release', :266 'DC-39: a range extended by key shows no size' |
| DC-40 | MUST | pass, clause not asserted | core/grid-edit.test.ts:993 'Ctrl+D and Ctrl+R … naming the source; one deep fills from beyond it'; not asserted: a clipboard paste and Ctrl+Enter carry no `fillSource` |
| DC-41 | MUST | pass, clause not asserted | core/grid-edit.test.ts:392 'Ctrl+Enter … one paste intent naming the cell typed in', :993 (fill key `enteredAt` undefined); not asserted: a clipboard paste names none, and the pair differ only in `EnteredAt` |
| DC-42 | MUST | pass | core/grid-row-headings.test.ts:125, :190; core/grid-selection.test.ts:334, :357; browser/headings.spec.mjs:148, :176, :196 (+3 :369, :387, :405) |
| DC-43 | MUST | pass | core/grid-row-headings.test.ts:209 'scrolls down only'; core/grid-selection.test.ts:436 'scrolls sideways only'; browser/headings.spec.mjs:217, :240 |
| DC-44 | MUST | pass, clause not asserted | browser/headings.spec.mjs:261, :298, :320 'redrawn only when the Extent moves to another Heading'; ExGrid.MudBlazor.Tests/WrapperStylesheetTests.cs:63, :75; not asserted: without a size label there is no Size Tip |
| DC-45 | MUST | pass | core/grid-point.test.ts:226; browser/declarations.spec.mjs:1061, :1085 (both Chromes), :1106, :1243 '150 ms … four F4 in a burst @blazor' (+4 :1120, :1162, :1188, :1270) |
| DC-46 | MUST | pass | cases/cells/reference-colouring.json; core/grid-reference-outlines.test.ts:87, :102; browser/declarations.spec.mjs:1454, :1481, :1558 |
| DC-47 | MUST | pass | core/grid-reference-outlines.test.ts:184, :201; browser/reference-text.spec.mjs:136 '150 ms burst', :194, :260 'IME composition' (+3 :163, :228, windows-ime.spec.mjs:319) |
| DC-48 | MUST | pass | browser/reference-text.spec.mjs:540 'longer than the field keeps its colours at either end (both surfaces, both Chromes)', :584 'comparison finds a layer half a pixel out' |
| DC-49 | MUST | pass | core/grid-reference-outlines.test.ts:112 'each key's colour told once per change … empty list as the edit ends', :138, :159; sheet/create-sheet.test.ts:227 |
| DC-50 | MUST | pass | core/grid-reference-outlines.test.ts:125 'columns asked for are outlined over their bodies'; browser/declarations.spec.mjs:1610, :1641 |
| DC-51 | MUST | pass | inspection of `core/src/dom/reference-text.ts`, `reference-outlines.ts`: `createRange` + `CSS.highlights` named per grid, `scrollLeft` write; no layout read, no MutationObserver needed now the core renders |
| DC-52 | MUST | pass | core/grid-pointed-at.test.ts:79, :94, :107 'press on a column header is the column … neither sorts' (+1 :167); browser/pointing-scope.spec.mjs (both tags); ExGrid.Components/ShippedStylesheetTests.cs:251 |
| DC-53 | MUST | pass, clause not asserted | core/grid-pointed-at.test.ts:135 'dashes over the named row's cell while painted, or down a column'; browser/pointing-scope.spec.mjs:442 'dashes follow the row through a sort', :876; ShippedStylesheetTests.cs:275; not asserted: no scroll for an unpainted row, and an empty request draws nothing |
| DC-54 | MUST | pass | browser/pointing-scope.spec.mjs:249, :386, :420 '=-( … at 0 and 150 ms', :735 'press and Shift+↓ @blazor'; sheet/pointing-scope.test.ts:385; inspection of `pointed-at.ts`, `point-mode.ts` (no document/window listener, no layout read) |
| DC-55 | MUST | pass | core/grid-pointed-at.test.ts:151 'steps from a cell and from a column'; sheet/pointing-scope.test.ts:590; browser/pointing-scope.spec.mjs:939 'Id's header, → … scrollTop unchanged', :691 |
| DC-56 | MUST | pass, clause not asserted | browser/reference-text.spec.mjs:421 'Excel's shade — #0401a2, #630101 — on #c6c6c6 (both Chromes)'; not asserted: places 3 to 7 and the dark-ground mix (the code has them, `ex-grid.css:1252-1258`) |
| DC-57 | MUST | pass | cases/keys/grid-keys.json; core/grid-declared-keys.test.ts:26, :39, :72 'a key the core answers … refused by name' |
| DC-58 | MUST | pass | cases/columns/overflow.json; ExGrid.Tests/BoldWidthTests.cs; core/grid-cell-paint.test.ts:128, :164 (row not repainted); browser/appearance.spec.mjs:226 @blazor, :278 (+3 sheet-paper.spec.mjs:57, :524, :563) |
| DC-59 | MUST | pass | core/grid-cell-paint.test.ts:147 'an edge is drawn once'; browser/sheet-borders.spec.mjs:95, :241, :260 (100% and chrome-150); browser/part-c.spec.mjs:253, :332, :435 (ADR-0206, pinned) (+5) |
| DC-60 | MUST | pass | core/grid-consumer-popover.test.ts:41, :62; browser/format-cells.spec.mjs:61, :460 'scrolls … closes as a Cancel below one row', :493 'each Sheet's Format Cells is its own' (+3 :257, :278, :305 @blazor) |
| DC-61 | MUST | pass | core/grid-keyboard-handoff.test.ts:42, :56, :132 (+6); blazor/bridge.test.ts:612, :624; browser/pivot.spec.mjs:1121, :1149, script-grid.spec.mjs:44 @blazor |
| DC-62 | MUST | pass | core/grid-leave.test.ts:52, :63, :74 (+5); browser/features.spec.mjs:1279 |
| DC-63 | MUST | pass | core/grid-edit.test.ts:98 (`onCellDoubleClick` heard); browser/features.spec.mjs:1217, :1259 |
| DC-64 | MUST | pass | core/grid-change-highlight.test.ts:32 'unmarked in one step; only the rows whose marks ended paint', :54 'nothing asked without the declaration; identity is the signal', :79; core/change-highlight.test.ts (7) |
| DC-65 | MUST | pass | browser/grid-live.spec.mjs:123 'DC-65: a mark stays on its cell across a scroll @blazor' |
| DC-66 | MUST | pass | browser/grid-live.spec.mjs:163 'marks paint without animating, forced colours restate them, live region says nothing @blazor'; ExGrid.MudBlazor.Tests/WrapperStylesheetTests.cs |
| DC-67 | MUST | pass | core/grid-reference-drag.test.ts:108, :127, :152 (+13); blazor/bridge.test.ts:1055; browser/declarations.spec.mjs:1502 |
| DC-25 | MUST | pass | browser/declarations.spec.mjs:2386 'positions grid, declaring nothing, keeps ExGrid's own behaviour', :2337; browser/sheets.spec.mjs:29, :157 |
| SH-1 | MUST | pass | ExSheet.Engine.Tests/PackageBoundaryTests.cs:8 'engine references only the BCL'; HeadlessTests.cs:43; inspection of csproj: ExSheet → ExSheet.Engine, ExGrid; only ExSheet.MudBlazor references ExSheet |
| SH-2 | MUST | pass | browser/sheet.spec.mjs:784 'SH-2: the Focus reaches XFD1048576 and the DOM does not grow' |
| SH-3 | MUST | pass | cases/viewport/compression.json 'VZ-8: refusal at the scale-one Layout Ceiling', 'exactly at the ceiling accepted', 'past the ceiling refused'; message names rows, height and ceiling (`viewport-geometry.ts:80`) |
| SH-4 | MUST | pass | sheet/create-sheet.test.ts:89 'a row the change did not name is not painted again' |
| SH-5 | MUST | pass | cases/sheet/structure.json (SH-5 cases), cases/sheet/undo.json; browser/sheet-vs-excel.spec.mjs:711 'inserting rows keeps the Selection'; browser/sheet.spec.mjs:439, :475, :546 |
| SH-6 | MUST | pass | cases/sheet/reference-grammar-culture.json; ExSheet.Engine.Tests/ReferenceGrammarCultureTests.cs:92 |
| SH-7 | MUST | pass | ExcelCases/*.json (164 files, every function); ExSheet.Engine.Tests/RefusalTests.cs:60 'corpus #NAME? refused where Excel answers otherwise'; cases/sheet/functions.json; sheet-engine/catalogue.test.ts:56 |
| SH-8 | MUST | pass | cases/sheet/recalculation.json 'only the dependents recompute, counted', 'Values published only from a completed recalculation' |
| SH-9 | MUST | pass | ExcelCases/errors.json 'cycle is #CIRC! in each member and each dependent', 'Breaking the cycle recovers…'; ExcelCaseTests.cs:30 |
| SH-10 | MUST | pass | ExcelCases/dates.json, general-width.json; cases/sheet/number-format.json '15 significant digits', 'what General cannot fit is ####' |
| SH-11 | MUST | pass | cases/sheet/culture.json 'SH-11: a document saved under en-US … de-DE shows the same numbers' |
| SH-12 | MUST | pass | cases/sheet/sheet-document.json 'SH-12, SH-17: a document holds no Values', 'unknown version is refused', round-trips |
| SH-13 | MUST | pass | cases/sheet/undo.json; sheet/create-sheet.test.ts:139 'a document opened empties it', :1780 'a command is one step'; browser/sheets.spec.mjs:51 'each Sheet keeps its own undo stack' |
| SH-14 | MUST | pass | cases/sheet/clipboard.json; browser/declarations.spec.mjs:2199 'SH-14/DC-33 … @blazor' |
| SH-15 | MUST | pass | cases/sheet/fill.json (series, whole axis, refusals, formats); ExcelCases/fill.json |
| SH-16 | MUST | pass | cases/sheet/linked-table.json (SH-16 cases); ExcelCases/linked-tables.json; browser/sheet.spec.mjs:574 |
| SH-17 | MUST | pass, clause not asserted | cases/sheet/headless.json 'engine alone … are the Values Excel gives'; ExSheet.Engine.Tests/HeadlessTests.cs:23; cases/sheet/random.json; not asserted: engine-alone Values equal to a rendered Sheet's |
| SH-18 | MUST | pass | browser/sheet.spec.mjs (SH-18 tests, :52, :77, :396 and the insert/delete tests) under the fixture's console check |
| SH-20 | MUST | pass | ExcelCases/general-width.json; cases/sheet/general-width.json; sheet/create-sheet.test.ts:366 |
| SH-21 | MUST | pass | cases/sheet/format-levels.json; cases/sheet/sheet-document.json 'SH-21: formats on whole columns … one entry' |
| SH-22 | MUST | pass | cases/sheet/column-width.json; ExcelCases/column-widths.json; ExSheet.Engine.Tests/ColumnWidthTests.cs |
| SH-23 | MUST | pass | cases/sheet/fill.json 'SH-23: Ctrl+D copies …', 'a fill key copies'; cases/sheet/cell-format-carry.json; core/grid-edit.test.ts:1030 'source past the Window' |
| SH-24 | MUST | pass, clause not asserted | sheet/create-sheet.test.ts:128 'Delete clears … as one step, formats stay'; cases/sheet/cell-format-carry.json 'SH-38, SH-24'; not asserted: no step over empty cells, Selection and Focus unchanged |
| SH-25 | MUST | pass | sheet/sheet-find.test.ts:26 'finds the text a cell is shown with', :35 'from the cell after the Focus, wrapping, scope' (match case, whole cell at :31-32), :137 |
| SH-26 | MUST | pass | cases/sheet/column-width.json (CW cases); browser/sheet-vs-excel.spec.mjs:740 |
| SH-27 | MUST | pass | cases/sheet/fill.json 'SH-27' cases; browser/sheet-vs-excel.spec.mjs:361 |
| SH-28 | MUST | pass | cases/sheet/reference-cycle.json (107); browser/declarations.spec.mjs:1188, :1323, :1339 |
| SH-29 | MUST | gap | sheet/create-sheet.test.ts:1866 'every command is refused by name … onEditingChanged', :469; browser/sheet.spec.mjs:594, :615 @blazor, :644; gap: replacing the Sheet Document while an edit is open |
| SH-30 | MUST | pass | cases/sheet/formula-references.json (43); ExSheet.Engine.Tests/FormulaReferencesTests.cs; sheet-engine/surface.test.ts |
| SH-31 | MUST | pass | sheet/create-sheet.test.ts:227; browser/declarations.spec.mjs:1610, :1641; browser/sheets.spec.mjs:157 |
| SH-32 | MUST | pass | cases/sheet/pointed-text.json; sheet/pointing-scope.test.ts:177, :202, :250 (+17); browser/pointing-scope.spec.mjs (14), sheets.spec.mjs:157 |
| SH-33 | MUST | pass | cases/sheet/linked-table.json 'SH-33: a repeated key refuses the snapshot by name…'; ExSheet.Engine.Tests/LinkedTableTests.cs:21 |
| SH-34 | MUST | pass | cases/sheet/pointed-text.json; browser/pointing-scope.spec.mjs:442 'dashes follow the row through a sort' |
| SH-35 | MUST | pass | sheet/pointing-scope.test.ts (15); browser/pointing-scope.spec.mjs:691, :735 @blazor, :939 (+25) |
| SH-36 | MUST | pass | cases/sheet/completion-trigger.json, completion-trigger-sheet.json; ExSheet.Engine.Tests/CompletionTriggerTests.cs; browser/declarations.spec.mjs:220, :246 (+18) |
| SH-37 | MUST | pass, clause not asserted | ExSheet.Engine.Tests/PointingScopeWatchTests.cs:104 'ADR0058_a_range_of_columns_is_refused_by_the_grammar' (message names ADR-0058's next step); not asserted: the same watch over `packages/sheet-engine` |
| SH-38 | MUST | pass | cases/sheet/cell-format.json, cell-format-carry.json, cell-format-document.json; ExSheet.Engine.Tests/CellFormatTests.cs |
| SH-39 | MUST | pass | browser/sheet-paper.spec.mjs:57, :121, :158 (both schemes, both Chromes); ExSheet.MudBlazor.Tests/PaperStylesheetTests.cs |
| SH-40 | MUST | pass | cases/sheet/number-format-colour.json (39); browser/sheet-paper.spec.mjs:57 |
| SH-41 | MUST | pass | core/column-geometry.test.ts:39; ExGrid.Tests/ColumnGeometryTests.cs:71; browser/sheet-paper.spec.mjs:697, :736, :750 (both Chromes) |
| SH-42 | MUST | pass | sheet/sheet-keys.test.ts; cases/sheet/culture.json (SH-42); browser/format-keys.spec.mjs:44, :73, :115; CI windows-desktop: browser/windows-desktop.spec.mjs:72 'Ctrl+1 to Ctrl+5 through the OS' |
| SH-43 | MUST | pass | sheet/create-sheet.test.ts:959, :2325 'formatting key while an edit is open changes nothing, says why, heard as a refusal', :1866; browser/format-keys.spec.mjs:141 'Ctrl+U' |
| SH-44 | MUST | pass | cases/sheet/cell-format.json (20), cell-format-carry.json; ExSheet.Engine.Tests/CellFormatTests.cs |
| SH-45 | MUST | pass | sheet/format-cells-draft.test.ts (32), format-cells-panel.test.ts; core/grid-pending-commit.test.ts:576, :603, :629 (ADR-0210); browser/format-cells.spec.mjs:61, :228, :242, format-cells-keys.spec.mjs:92 (80 ms), format-cells-mud.spec.mjs |
| SH-46 | MUST | pass | browser/sheet-borders.spec.mjs:120, :158, :199, :228 'dashes at the run's scale' (+26; 100% and chrome-150) |
| SH-47 | MUST | pass | ExSheet.MudBlazor.Tests/MudSheetPackageTests.cs:62, :74, :104, :119; `dotnet/tests/ExGrid.PackageSmoke/check.sh` (CI `package` job) |
| SH-48 | MUST | pass, clause not asserted | sheet/create-sheet.test.ts:1084 'shown only when asked, above the grid, band off the height'; browser/sheet-toolbar.spec.mjs:109 (both Chromes), :302; not asserted: the Viewport loses exactly rows × the Toolbar Row height |
| SH-49 | MUST | gap | sheet/create-sheet.test.ts:1084 (default row order); ExSheet.Components/ScriptSheetTests.cs:870 'ToolbarContent crosses as the rows it declares, in markup order'; gap: the reference check (no internal member from an item type) was removed in #501 |
| SH-50 | MUST | pass | browser/sheet-toolbar.spec.mjs:132, :153, :309, :501 (both Chromes); browser/format-keys.spec.mjs:94 'pressed straight after Shift+ArrowDown @blazor'; sheet/create-sheet.test.ts:1102, :1537 |
| SH-51 | MUST | pass | sheet/create-sheet.test.ts:1459 'Ctrl+F1 … the browser's where it does not'; browser/sheet-toolbar.spec.mjs:585; CI windows-desktop: browser/windows-desktop.spec.mjs:104 |
| SH-52 | MUST | pass | sheet/create-sheet.test.ts:1251, :1448 'KeyTips taken for one another refused by name', :1513; browser/sheet-toolbar.spec.mjs:329, :616, :714 (80 ms) (+20); CI windows-desktop: browser/windows-desktop.spec.mjs:122, :141 |
| SH-53 | MUST | pass | sheet/sheet-keys.test.ts:78, :85; browser/part-c.spec.mjs:526, :551 |
| SH-54 | MUST | pass | browser/part-c.spec.mjs:85, :116, :134, :194 (both Chromes, pinned); browser/sheet-paper.spec.mjs (SH-54, chrome-150); PaperStylesheetTests.cs |
| SH-55 | MUST | pass | sheet/format-cells-draft.test.ts:442, :454, :467 |
| SH-56 | MUST | pass | cases/sheet/sheet-day.json (11); sheet/create-sheet.test.ts:1923 'browser's zone at once, fixed day first', :1934 |
| SH-57 | MUST | pass, clause not asserted | sheet/create-sheet.test.ts:1934 'read again as midnight passes there' (fake timers); cases/sheet/sheet-day.json 'the day the Sheet already has changes nothing'; not asserted: readings at most an hour apart, and no clock read once destroyed |
| SH-58 | MUST | n-a? | `OnBrowserTimeZone` no longer exists (searched `packages/*/src`, `dotnet/src`); the Sheet reads `Intl` itself (`sheet/src/sheet-moment.ts:20`); the date clause is held by browser/sheet-today.spec.mjs:62, :75 (Kiritimati vs Pago Pago) |
| SH-59 | MUST | pass | cases/sheet/indirect.json, now-source.json, volatile.json |
| SH-60 | MUST | pass | sheet/create-sheet.test.ts:1947 'NOW moves on as the minute turns'; cases/sheet/volatile.json 'no volatile Formula is not recalculated by the clock'; browser/sheet-today.spec.mjs:81, :97, :120 |
| SH-61 | MUST | pass | cases/sheet/linked-table.json 'two rows hold the same Values in every key column', 'a blank part … has no key'; cases/sheet/sheet-document.json (version 9); sheet/pointing-scope.test.ts:187 |
| SH-62 | MUST | pass | cases/sheet/spill.json (#SPILL!, overlap order, not recorded, #NUM! past 2^24) |
| SH-63 | MUST | pass | cases/sheet/spill.json 'A1# reads the Spill Range…', 'is #REF! when A1 does not spill', '# follows one cell only, @ refused' |
| SH-64 | MUST | pass | ExcelCases/filter.json, unique.json, sort.json, sortby.json, sequence.json, transpose.json, sumproduct.json, index.json, xlookup.json, row.json, column.json |
| SH-65 | MUST | pass | sheet/create-sheet.test.ts:419 'spilled cell shows its Anchor's Formula in the bar, dimmed, read only'; cases/sheet/spill.json (typing, copy without/with Anchor); browser/sheet-spill.spec.mjs:12, :30 |
| SH-66 | MUST | pass | cases/sheet/random.json; ExcelCases/rand.json, randbetween.json; sheet/create-sheet.test.ts:1897, :1913 |
| SH-67 | MUST | pass | cases/sheet/let.json (107, refusals on entry); ExcelCases/let.json; ExSheet.Engine.Tests/LetTests.cs:15; sheet/create-sheet.test.ts:113 (refusal holds the editor) |
| SH-68 | MUST | pass | cases/sheet/subtotal.json, error-type.json, indirect.json; ExSheet.Engine.Tests/SubtotalTests.cs:25; sheet-engine/surface.test.ts:60 'no row can be hidden while SUBTOTAL … skip nothing' |
| SH-69 | MUST | pass | sheet-engine/move-reference.test.ts |
| SH-19 | OBSERVATIONAL | recorded in Step 6 | browser/measure-sheet.spec.mjs:108 'SH-19: completion's keystroke-to-list time … 150 ms'; ExSheet.Engine.Tests/Measurements.cs:39, :47, :55 (Explicit facts: full recalculation, one edit, a row inserted) |

## Gaps and notes

- **DC-4** (clause not asserted): no test runs Ctrl+A then a copy on a grid with Row Headings and reads the text for a
  heading label. Looked in `core/grid-copy.test.ts`, `core/grid-row-headings.test.ts` and the sheet browser specs. The
  copy is built from columns, and headings are not a column, so the code holds it. A `grid-row-headings` test that
  copies after Ctrl+A would close it.
- **DC-8** (clause not asserted): `core/grid-paste.test.ts:226` and the browser spill tests never read the scroll
  offset after a spill. Adding a `scrollTop`/`scrollLeft` check before and after the spill in that test would close it.
- **DC-16** (clause not asserted): `core/grid-cell-paint.test.ts:178` is titled "typing still replaces it" but opens
  with F2 and cancels, so nothing types over the declared text. Typing a character over a cell with `editorText`
  declared would close it.
- **DC-21** (clause not asserted): no test shows the Formula Bar holding the full value of a cell painted `####`.
  Searched `core/grid-formula-bar.test.ts` and the browser specs for `####` together with the bar. A narrow numeric
  column pressed under `showFormulaBar` would close it.
- **DC-40 / DC-41** (clause not asserted): `core/grid-edit.test.ts:392/:993` check `fillSource` on fill keys and
  `enteredAt` on Ctrl+Enter, but no test checks that a clipboard paste carries neither, or that a clipboard paste and a
  Ctrl+Enter over the same Selection differ only in `enteredAt`. The constructor makes both optional
  (`clipboard/paste-intent.ts:59`). One test comparing the two intents would close both rows.
- **DC-44** (clause not asserted): every Size Tip test runs on `/sheet`, which declares a label. No test drags
  Headings with no `nameBoxSizeLabel`. The code returns early when there is no label (`grid-view.ts:7646`).
- **DC-53** (clause not asserted): `core/grid-pointed-at.test.ts:135` shows that an unpainted row draws nothing, but
  it never checks that the grid did not scroll, and never sends an empty request. Both checks fit in that test.
- **DC-56** (clause not asserted): `browser/reference-text.spec.mjs:421` checks places 1 and 2 under the light scheme.
  Places 3 to 7 and the dark-ground `color-mix` exist only in `ex-grid.css:1252-1258`. A stylesheet test over the
  seven defaults would close it.
- **SH-17** (clause not asserted): the engine-alone Values are checked against Excel (`headless.json`) and
  across cultures (`HeadlessTests.cs:23`). No test compares them with what a rendered Sheet shows for the same document
  (searched `sheet/`, `sheet-blazor/`, `ExSheet.Components/ScriptSheetTests.cs:329`, which compares two .NET
  workbooks). Both engines answer the same shared corpus. A component test that opens a document and compares cell
  text with `ExSheet.Engine`'s Values (or the TS engine alone) would close it.
- **SH-24** (clause not asserted): `create-sheet.test.ts:128` asserts the clear and its undo only. No test asserts
  that a Delete over empty cells records no step, or that the Selection and the Focus stay. The code returns when
  nothing is cleared (`sheet/src/create-sheet.ts:1493`).
- **SH-29** (gap): no test replaces the Sheet Document while an edit is open (searched `sheet/`, `sheet-react/`,
  `sheet-blazor/`, `ExSheet.Components`, `edit-stands.spec.mjs`, `sheet.spec.mjs`). The Script Core's own word for
  it, `editDiscardedByNewDocument` (`sheet/src/sheet-word-tables.ts:104`), is used nowhere. A replaced document would
  reach the user, if at all, through the grid's generic `sourceChanged` discard words, so "announces it with that
  reason" cannot be confirmed from the code. A `create-sheet` test is needed: F2, `update({document})`, then assert
  nothing is written and the notice's reason. Its finding may be that the word must be wired.
- **SH-37** (clause not asserted): the watch exists only for the C# engine
  (`PointingScopeWatchTests.cs:104`). The TS engine that the Sheet draws with also refuses the form
  (`sheet-engine/src/formulas/lexer.ts:275`), but no test there would fail the day it stops. A twin in
  `sheet-engine/test/` or a shared case under `tests/cases/sheet/` would close it.
- **SH-48** (clause not asserted): `create-sheet.test.ts:1084` asserts only that the band has a height (> 0).
  `sheet-toolbar.spec.mjs:109` checks that the rows are equal and that the band meets the grid. No test checks that the
  Viewport loses exactly rows × the row height, or that the band's height follows the Density.
- **SH-49** (gap): the reference check ("no internal member referenced from an item type") was
  `tests/ExSheet.Components/SheetToolbarTests.cs:169`, removed with the Razor components in #501 (be0b7474e). Nothing
  replaces it, either over `dotnet/src/ExSheet/Toolbar/` or over the Script Core's toolbar
  (`sheet/src/sheet-toolbar.ts`). The order clauses are held. A reflection test over the C# item types, or an
  import check over the TS toolbar, would close it.
- **SH-57** (clause not asserted): the "registered `TimeProvider`" is now the browser's timers in the Script Core.
  The midnight move is tested on fake timers (`create-sheet.test.ts:1934`). The one-hour cap
  (`sheet-moment.ts:9`, `:51`) and the cleared timers on destroy (`create-sheet.ts:1223`) have no test.
- **SH-58** (n-a?): `OnBrowserTimeZone` and the grid's read of the zone at attach no longer exist. No match in
  `packages/*/src` or `dotnet/src`; the last mention went with #507. The Sheet reads the zone from `Intl` in the browser
  (`sheet-moment.ts:20`). The row's outcome, a browser in another date's zone showing its own date, is held by
  `sheet-today.spec.mjs:62` and `:75` on two zones 25 hours apart. The row needs restating against the Script Core.
- **SH-19** (OBSERVATIONAL): completion is measured by `measure-sheet.spec.mjs:108`. The recalculation half is
  `Measurements.cs` (Explicit facts) on the C# engine, while the Sheet on screen now computes with the TS engine.
  Step 6 should say which engine its numbers are for.
- **Finding outside these rows' subjects** (DC-15 and DC-24 inspect this directory):
  `packages/core/src/dom/grid-view.ts:5352` calls `doc.elementFromPoint` to re-land a held press whose control was
  repainted. It is not on a paint path, so ADR-0350 section 6 (P4) is not broken, but it is a layout query. ADR-0021
  rejected "asking where the pointer is at the replay (`elementFromPoint`)". No ADR records an allowance for this use.
  It needs an owner's look.

---

# E-fd-rt.md

# Criteria E: FD, PV, DA, SM, LV, CH, RT at c8b721f9

| ID | Level | Result | Evidence |
|---|---|---|---|
| FD-1 | MUST | pass, clause not asserted | cases keys/grid-keys.json:194; core/grid-find.test.ts:133; browser/find.spec.mjs:184, :199, :213 (+5). Not asserted: Ctrl+F inside a Template cell's own control stays the control's |
| FD-2 | MUST | pass | core/grid-find.test.ts:133; browser/find.spec.mjs:472 |
| FD-3 | MUST | pass, clause not asserted | browser/find.spec.mjs:44, :251; core/grid-find-slot.test.ts:124. Not asserted: a pointer-down elsewhere closes Find and returns the keyboard |
| FD-4 | MUST | pass, clause not asserted | core/grid-find.test.ts:200. Not asserted: visible columns in current order (hidden/moved column); no scope when a single cell is selected (held for Find All only, grid-find-all.test.ts:141) |
| FD-5 | MUST | pass, clause not asserted | core/grid-find.test.ts:183; core/grid-find-slot.test.ts:135; browser/find.spec.mjs:135. Not asserted: with a scope the Selection stands |
| FD-6 | MUST | gap | core/grid-find.test.ts:200, :221, :237. Row says a new step cancels the one out; ADR-0451 s3 replaced that with waiting |
| FD-7 | MUST | pass | cases finding/find.json (12 cases), source/finding.json:7, :29; ExGrid.Tests/Cases/FindCaseTests.cs:25 |
| FD-8 | MUST | pass | core/grid-find.test.ts:154, :183; browser/find.spec.mjs:441, :455 |
| FD-9 | MUST | pass | ExGrid.Tests/GridFindTests.cs:52 The_fetching_source_finds_through_its_delegate, :151 A_source_without_find_says_it_cannot |
| FD-10 | MUST | n-a? | No `OnFind` exists in the Blazor ExGrid or the Script Core; Find belongs to the Source |
| FD-11 | MUST | pass, clause not asserted | No test; code: core/src/dom/grid-view.ts:8741 (throws on an answer outside the request), :8762 (column left = orderChanged) |
| FD-12 | MUST | pass | core/grid-find-all.test.ts:114; react/find-slot-contract.ts:104 (mui, mantine); ExGrid.MudBlazor.Tests/MudScriptFindPanelTests.cs:41; ExGrid.Tests/GridFindTests.cs:83, :151 |
| FD-13 | MUST | pass | cases finding/find-all.json (14 cases); ExGrid.Tests/Cases/FindCaseTests.cs:46 |
| FD-14 | MUST | pass, clause not asserted | core/grid-find-all.test.ts:130, :143, :150 (+2). Not asserted: answer outside the request / too many / too few / out of order throws (code grid-view.ts:9326-9345) |
| FD-15 | MUST | pass, clause not asserted | core/grid-find-all.test.ts:150, :162, :186; browser/find.spec.mjs:524. Not asserted: Enter on a line (find-all-list.ts:134) |
| FD-16 | MUST | pass, clause not asserted | core/grid-find-all.test.ts:244, :256, :273. Not asserted: list goes on an option change and after Replace All |
| FD-17 | MUST | pass | sheet/create-sheet.test.ts:893, :909; sheet/sheet-find.test.ts:76, :94, :149; pivot/pivot-view.test.ts:2453, :2539 |
| PV-1 | MUST | gap | ExGrid.PackageSmoke/check.sh:137, :151, :156; check.sh:179 shows ExBoard references ExPivot, which the row forbids |
| PV-2 | MUST | gap | pivot/pivot-view.test.ts:296, :313. Not met: report header click selecting; Group/Total row painting; server Window clause (rewrite owed) |
| PV-3 | MUST | pass | cases pivot/items.json (55), source-snapshot.json:82, :240; pivot-engine/incremental.test.ts:323; browser/pivot-csv.spec.mjs:258, :291 |
| PV-4 | MUST | pass | cases pivot/aggregation.json (30), source-snapshot.json:282-559 |
| PV-5 | MUST | pass | cases pivot/hidden.json:9, :34, :127; pivot/pivot-view.test.ts:1734 |
| PV-6 | MUST | pass | cases pivot/forms.json (23), columns.json (10), sorting.json (16); ExPivot.Engine.Tests/Cases/PivotReportCaseTests.cs:27 |
| PV-7 | MUST | pass | cases pivot/general.json, general.texts.json, items.json:378-402 (+1); pivot/pivot-view.test.ts:323; browser/pivot-csv.spec.mjs:226, :337, :370 (+1) |
| PV-8 | MUST | pass | cases pivot/layout-edits.json (20); pivot/pivot-view.test.ts:1386, :1493; ExPivot.Engine.Tests PivotLayoutCaseTests.cs:24 |
| PV-9 | MUST | pass | pivot-react/pivot-slot-contract.ts:205 (mui, mantine); pivot-blazor/razor-pivot-slots.test.ts; browser/pivot.spec.mjs:138, :149, :162; browser/script-pivot.spec.mjs:78 |
| PV-10 | MUST | gap | browser/pivot.spec.mjs:138, :149, :162 (+5); pivot/pivot-view.test.ts:1045. Stale: "Blazor's own events, no JavaScript of ExPivot's" and its inspection; dotnet/src/ExPivot/wwwroot/ex-pivot.min.js exists |
| PV-11 | MUST | pass | browser/pivot.spec.mjs:668, :761, :782 (+5); pivot/pivot-view.test.ts:1386, :1443, :1480 (+1) |
| PV-12 | MUST | pass | pivot/pivot-view.test.ts:2044, :2072, :2113; browser/pivot.spec.mjs:947, :967 |
| PV-13 | MUST | pass, clause not asserted | pivot/pivot-view.test.ts:358, :389, :405; cases pivot/hidden.json:412, report-source.json:373; browser/pivot.spec.mjs:303, :316; pivot-find.spec.mjs:160. Not asserted: Selection kept across a values-only refresh |
| PV-14 | MUST | pass | pivot/pivot-view.test.ts:2157, :2179, :2191 (+2); cases pivot/hidden.json:106; browser/pivot.spec.mjs:339, :398, :656 (+1) |
| PV-15 | MUST | gap | No count of report-grid updates per Field List interaction, nor of the grid Chrome asked once; Razor render-count tests removed |
| PV-16 | MUST | pass | cases pivot/layout-json.json (23), source-json.json (113); ExPivot.Engine.Tests PivotJsonCaseTests.cs:18 |
| PV-17 | MUST | pass, clause not asserted | browser/pivot.spec.mjs:263. Not asserted: a menu open in one pivot is not open in the other |
| PV-18 | MUST | pass, clause not asserted | ExPivot.MudBlazor.Tests/WrapperStylesheetTests.cs:43 Every_visual_token..., :58 The_wrapper_sets_no_geometry_token, :62; browser/pivot.spec.mjs:1233, :1265. Not asserted: the expand/collapse glyph is plain markup |
| PV-19 | MUST | pass | browser/pivot-find.spec.mjs:160, pivot-csv.spec.mjs:319; ExPivot.Components/ScriptPivotTests.cs:82; pivot/pivot-view.test.ts:2349 |
| PV-20 | MUST | pass | browser/pivot-db.spec.mjs:109, pivot-risk.spec.mjs:70, grid-live.spec.mjs:288 (+3 spec files: pivot, pivot-csv, pivot-live) |
| PV-21 | OBSERVATIONAL | observational | browser/measure-pivot.spec.mjs:265, :379, :416, :471, :598; recorded in Step 6 |
| PV-22 | MUST | pass | ExGrid.DemoApi.Tests/PivotSourceTests.cs:85, :98, :129 (+5); ExPivot.Engine.Tests/SnapshotSourceTests.cs |
| PV-23 | MUST | pass | cases pivot/source-questions.json:559, :678, report-source.json:1143; pivot/pivot-view.test.ts:2273 |
| PV-24 | MUST | pass | pivot/pivot-view.test.ts:1641; cases pivot/source-questions.json:939 |
| PV-25 | MUST | pass, clause not asserted | pivot/pivot-view.test.ts:1004, :1024. Not asserted: a further change cancels the question in flight (pivot-view.ts:1082) |
| PV-26 | MUST | pass | cases pivot/hidden.json:142, :234; pivot/pivot-view.test.ts:1584, :2375 |
| PV-27 | MUST | pass | pivot-engine/slicing.test.ts:50, :72, :82; ExPivot.Engine.Tests SlicedWorkTests |
| PV-28 | MUST | pass | pivot/pivot-view.test.ts:990, :1858, :1884 (+1) |
| PV-29 | MUST | pass | cases pivot/source-questions.json:831; pivot/pivot-view.test.ts:1170, :1191, :1200 |
| PV-30 | MUST | pass | pivot/pivot-view.test.ts:1088, :1108, :1335 (+1); browser/pivot.spec.mjs:931, :992, :1045 |
| PV-31 | MUST | pass | cases pivot/orderkeys.json (16); pivot-engine/order-key.test.ts:27, :41; browser/pivot-risk.spec.mjs:70, :86, :108 |
| PV-32 | MUST | pass | cases pivot/dates.json (17); browser/pivot.spec.mjs:1091 |
| PV-33 | MUST | gap | cases pivot/words.tables.json; ExPivot.Engine.Tests JapaneseWordsTests.cs:16. en-GB chosen by UI culture contradicts ADR-0370 s10 (pivot-view.test.ts:1099; words.json:200, :226); the component has no such selection |
| PV-34 | MUST | pass | pivot-engine/live.test.ts:158 (24 random runs); ExPivot.Engine.Tests LiveDataTests.cs:56 |
| PV-35 | MUST | pass, clause not asserted | pivot/pivot-view.test.ts:759, :793, :947; cases pivot/hidden.json:412. Not asserted: the 250 ms default (always passed explicitly); Selection and open menu kept over a values-only redraw; an Item appearing drops the Selection |
| PV-36 | MUST | pass | pivot/pivot-view.test.ts:910; cases pivot/report-source.json:842; browser/pivot-live.spec.mjs:154, :287 |
| PV-37 | MUST | pass | pivot/pivot-view.test.ts:836, :868, :879; browser/pivot-live.spec.mjs:197 |
| PV-38 | MUST | pass | pivot/pivot-view.test.ts:757, :1108; pivot-engine/server-live.test.ts:412 |
| PV-39 | MUST | pass | pivot/pivot-view.test.ts:2191, :2218; browser/pivot.spec.mjs:420, :457, :481 (+4) |
| PV-40 | MUST | pass | pivot-engine/slicing.test.ts, sliced-layout.test.ts:102, :226, incremental.test.ts:336 (+1); pivot/pivot-view.test.ts:1004; browser/measure-pivot.spec.mjs:416 |
| PV-41 | MUST | pass | browser/pivot.spec.mjs:616, :637, :1121 (+1); pivot/pivot-view.test.ts:2830 |
| PV-42 | MUST | pass, clause not asserted | cases pivot/row-keys.json:9, :67; pivot/report-source.test.ts:50; core/grid-actions.test.ts:352, grid-cell-paint.test.ts:164. Not asserted: no pivot-level count of rows painted over a live redraw (holds by composition, report-grid.ts:332) |
| PV-43 | MUST | gap | ExPivot.Engine.Tests/RowKeyTests.cs:40 Reading_every_rows_key_allocates_nothing (C# only). Browser report key allocates per read (pivot/src/report-grid.ts:61) |
| PV-45 | MUST | pass, clause not asserted | pivot/report-source.test.ts:50; core/grid-cell-paint.test.ts:164. Not asserted: per-redraw paint count over the pivot |
| PV-47 | MUST | n-a? | Names OutOfMemoryException in the removed Razor ExPivot's report computation; the TS pivot has no such path (failure: pivot-view.test.ts:879) |
| PV-48 | OBSERVATIONAL | observational | verification/2026-10-06-macos-live-report-after, 2026-10-10-linux-merged-live-costs (C# report-Window path); recorded in Step 6 |
| PV-49 | MUST | pass, clause not asserted | pivot/pivot-view.test.ts:836; browser/pivot-live.spec.mjs:197; ExPivot.MudBlazor.Tests/WrapperStylesheetTests.cs:142. Not asserted: Summary answering while stale; refusal's Japanese text and replacement by id; no row painted again |
| PV-50 | MUST | gap | browser/pivot.spec.mjs:707, :735. Row itself says Escape, Tab and the no-report panel typed while opening are not checked over the Script Core |
| DA-1 | MUST | gap | ExGrid.PackageSmoke/check.sh:119-148, :179. Row's referrer list omits ExChart.Engine (check.sh:162) and ExGrid.Data.Xlsx (check.sh:127) |
| DA-2 | MUST | pass | ExGrid.Data.Tests/ImmutabilityTests.cs:14, :45 |
| DA-3 | MUST | pass | ExGrid.Data.Tests/KindTests.cs:12, :51, :92 (+5) |
| DA-4 | MUST | pass | ExGrid.Data.Tests/ObjectTests.cs:13, :58 |
| DA-5 | MUST | pass | ExGrid.Data.Tests/LoadTests.cs:12, :67, CsvLoadTests.cs:67 (+2: DataReaderTests.cs:348, ExGrid.Data.Arrow.Tests/LoadTests.cs:59) |
| DA-6 | MUST | pass | ExGrid.Data.Tests/RefusalTests.cs:12, CsvRefusalTests.cs:19; ExGrid.Data.Arrow.Tests/RefusalTests.cs:15 (+1) |
| DA-7 | MUST | pass | ExGrid.Data.Tests/CsvTests.cs:15, :86, :118 (+3 and the separator/decimal/thousands/culture/date tests) |
| DA-8 | MUST | pass | ExGrid.Data.Tests/CsvEncodingTests.cs:37, :50, :103 (+1) |
| DA-9 | MUST | pass | ExGrid.Data.Tests/CsvSuggestionTests.cs:29, :274 |
| DA-10 | MUST | pass | ExGrid.Data.Tests/DataReaderTests.cs:78, :125, :141 |
| DA-11 | MUST | pass | cases data/change-batch.json (19); ExGrid.Data.Tests/RecordKeyTests.cs:29, ChangeBatchCaseTests.cs:50 |
| DA-12 | MUST | pass | ExGrid.Data.Tests/SharingTests.cs:67 |
| DA-13 | MUST | pass | cases arrow/write.json; ExGrid.Data.Arrow.Tests/RoundTripTests.cs:11, :79, ArrowCaseTests.cs:58 |
| DA-14 | MUST | pass | cases arrow/read-text.json, refusals-types.json; ExGrid.Data.Arrow.Tests/CompressionTests.cs:20, :33 |
| DA-15 | MUST | pass | cases arrow/write.json (date32, coarsest timestamp); round trip reads with no codec |
| DA-16 | MUST | pass | ExGrid.PackageSmoke/check.sh:231-236 (ArrowRoundTrip.cs) |
| DA-17 | OBSERVATIONAL | observational | ExGrid.Data.Tests/MeasureTests.cs:36, :55; ExGrid.Data.Arrow.Tests/MeasureTests.cs:22; browser/measure-pivot.spec.mjs:265, :537, :598 (+1); recorded in Step 6 |
| DA-18 | MUST | gap | ExPivot.Components/OneAggregationDefinitionTests.cs:42, :51, :60 (C# only). Script Core has two definitions: core/src/summary/summary-accumulator.ts, pivot-engine/src/aggregates/parts.ts |
| SM-1 | MUST | gap | core/grid-summary.test.ts:89. "Nothing else" is stale (request carries `focus`, grid-summary.ts:30); ranges and RSV not asserted |
| SM-2 | MUST | pass, clause not asserted | core/grid-summary.test.ts:77; browser/selection-summary.spec.mjs:165, :224. Not asserted: an answer under a stale RSV shows nothing |
| SM-3 | MUST | pass | core/grid-summary.test.ts:217, :268; sheet/create-sheet.test.ts:2302 |
| SM-4 | MUST | pass | cases summary.json (million rows); core/grid-summary.test.ts:108; ExGrid.Tests/GridSummaryTests.cs:60; browser/selection-summary.spec.mjs:258 |
| SM-5 | MUST | gap | core/grid-summary.test.ts:210; ExGrid.Tests/GridSummaryTests.cs:91, :114; ExGrid.Components ScriptGridHandleTests.cs:308. OnSummarize refused only beside a source that summarises (grid-view.ts:2919, ExGrid.Surface.cs:206), not beside any bound Source |
| SM-6 | MUST | pass | cases summary.json (SM-6); sheet/sheet-summary.test.ts:163; ExGrid.Tests/GridSummaryTests.cs:50, :75 |
| SM-7 | MUST | pass | core/summary.test.ts:72; sheet/sheet-summary.test.ts:51, :57; ExGrid.Tests/GridSummaryTests.cs:124; browser/selection-summary.spec.mjs:100 |
| SM-8 | MUST | pass | ExGrid.DemoApi.Tests/TradeSummaryTests.cs:46 |
| SM-9 | MUST | pass | core/grid-summary.test.ts:60, :121, :306; sheet/sheet-summary.test.ts:69; browser/selection-summary.spec.mjs:44, :64 |
| SM-10 | MUST | pass, clause not asserted | No test asserts the absence of a live region; selection-summary.ts sets no aria-live or role |
| SM-11 | MUST | pass | browser/selection-summary.spec.mjs:44, :165, :224 (+1) |
| SM-14 | MUST | pass, clause not asserted | core/grid-summary.test.ts:192. Not asserted: switched on again asks; ExSheet/ExPivot carry it to their grid (pivot-view.ts passes showSelectionSummary) |
| SM-12 | MUST | pass, clause not asserted | sheet/sheet-summary.test.ts:51, :83, :116 (+3). Not asserted: every cell of a spilled array is summed |
| SM-13 | MUST | pass | cases pivot/report-source.json:730; pivot/pivot-view.test.ts:661, report-summary.test.ts |
| SM-15 | MUST | pass, clause not asserted | core/grid-summary.test.ts:268; sheet/create-sheet.test.ts:2302. Not asserted: with no figure the walk is not made |
| LV-1 | MUST | pass | core/grid-actions.test.ts:352, grid-cell-paint.test.ts:164; react/ex-grid.test.ts:153 |
| LV-2 | MUST | gap | core/row-key.ts:22; cases source/live.json:119; core/fetch-live.test.ts:235. Grid has no Window repeated-key check; only built-in sources refuse (fetching-source.ts:841) |
| LV-3 | MUST | pass | cases source/live.json:7-168 |
| LV-4 | MUST | pass | cases source/live.json:239-470, change-highlight.json:196 |
| LV-5 | MUST | pass | core/live-requery-property.test.ts:344, source-boundary.test.ts:262; browser/grid-live-local.spec.mjs:65 |
| LV-6 | MUST | pass | cases source/live.json:486-649; core/source-boundary.test.ts:165, :179, :203 (+1: fetch-live.test.ts:191) |
| LV-7 | MUST | pass | cases source/live.json:674-734; core/source-boundary.test.ts:187; browser/grid-live-local.spec.mjs:65 |
| LV-8 | MUST | pass | core/fetch-live.test.ts:103, :110, :131 (+1); browser/grid-live.spec.mjs:326 |
| LV-9 | MUST | pass | cases change-highlight.json (12); core/fetch-live.test.ts:250-389, source-boundary.test.ts:232 |
| LV-10 | MUST | gap | cases source/live.json:225; core/fetch-live.test.ts:235. Grid never reads vouchesDistinctRows; no unvouched Window or painted-row key check |
| LV-11 | MUST | pass | core/grid-edit.test.ts:834, :848; browser/write-lands.spec.mjs:92, :119, :141 (+1) |
| LV-12 | MUST | pass | core/grid-actions.test.ts:214, :267, grid-held-row-commands.test.ts:109; browser/write-lands.spec.mjs:157 |
| LV-13 | MUST | pass | core/grid-edit.test.ts:862, grid-actions.test.ts:295; browser/write-lands.spec.mjs:183, :215, :240 (+3) |
| LV-14 | MUST | pass | browser/write-lands.spec.mjs:183; inspection of the grid-view.ts key gate and grid-paste.ts |
| LV-16 | MUST | pass | cases source/live.json:606, :627, :649 |
| LV-17 | MUST | pass | core/grid-edit.test.ts:900; browser/write-lands.spec.mjs:281, :311 |
| LV-18 | MUST | pass | browser/grid-live-local.spec.mjs:35, :65, :94 |
| LV-19 | MUST | pass | core/grid-edit.test.ts:379, grid-pending-commit.test.ts:344 |
| LV-20 | MUST | pass, clause not asserted | core/grid-edit.test.ts:662, :679, :696 (+2); browser/write-lands.spec.mjs:429. Not asserted: RowLeftTheWindow commit lands once the key is back; with a Row Key, an order-moved press with its row gone is OrderMoved (grid-view.ts:5951-5967) |
| LV-21 | MUST | pass | browser/write-lands.spec.mjs:92 |
| LV-22 | MUST | gap | blazor/bridge.test.ts:164 (release protocol only). Weak-reference retention tests removed with Razor; nothing measures retention by the Script Core or the ExPivot report source |
| LV-23 | OBSERVATIONAL | observational | verification/2026-10-10-linux-merged-live-costs (removed Razor path), no current spec; recorded in Step 6 |
| LV-24 | MUST | gap | pivot-engine/incremental.test.ts:299; ExPivot.Engine.Tests/IncrementalOracleTests.cs:135. Component and browser clauses not met (DoD note) |
| LV-25 | MUST | gap | ExPivot.Engine.Tests/ReportProtocolTests.cs:85. Server report Window path removed; rewrite owed |
| LV-26 | MUST | gap | ExPivot.Engine.Tests/ReportProtocolTests.cs:108, :137; cases pivot/report-source.json:493. Layer 2 not met |
| LV-27 | MUST | gap | ExPivot.Engine.Tests/WindowChangesDigestTests.cs:42, :132, :160 (+2). Layers 2 and 3 not met |
| LV-28 | MUST | gap | ExPivot.Engine.Tests/ReportProtocolTests.cs:248, :304; browser/pivot-db.spec.mjs:79 (Leaf Aggregate path). Layer 2 not met |
| LV-29 | MUST | pass | pivot-engine/incremental.test.ts:299, :336; ExPivot.Engine.Tests/IncrementalOracleTests.cs:135 |
| LV-30 | MUST | gap | pivot/report-source.test.ts:261, :279; ExPivot.Engine.Tests/ReportProtocolTests.cs:171, :384. Layer 2 render counts not met |
| LV-31 | MUST | gap | Layer 2 and the remote browser scenario not met (DoD note) |
| LV-32 | MUST | pass, clause not asserted | core/grid-actions.test.ts:267, grid-marks.test.ts:257, grid-find.test.ts:301 (+4). Not asserted: paste, Delete, Ctrl+D/R, fill handle and fill key refused as SourceChanged (grid-view.ts:7759, :7827); ExSheet document clause |
| LV-33 | MUST | pass, clause not asserted | core/grid-edit.test.ts:436, :555, :1076. Not asserted: replaced-Source variants (typing, composition, nothing selected); a moving key aimed the same way |
| LV-34 | MUST | gap | ExPivot.Engine.Tests/ReportProtocolTests.cs:504-609 (6). Component and browser clauses not met (DoD note) |
| LV-15 | OBSERVATIONAL | observational | browser/measure-live.spec.mjs:93; recorded in Step 6 |
| CH-1 | MUST | pass | chart/chart-dom.test.ts:156 |
| CH-2 | MUST | pass | chart/chart-dom.test.ts:194; browser/chart.spec.mjs:54 |
| CH-3 | MUST | pass, clause not asserted | chart/chart-dom.test.ts:152. Not asserted: the 10^3 / 10^5 sizes (test compares 10 and 10,000 points) |
| CH-4 | MUST | pass | chart/chart-dom.test.ts:156, :182; browser/chart.spec.mjs:31 |
| CH-5 | MUST | pass | cases chart/scene.json:663, :684; chart/chart-dom.test.ts:238; browser/chart.spec.mjs:83 |
| CH-6 | MUST | pass | cases chart/appearance.json:129, :537 |
| CH-7 | MUST | pass | cases chart/surface.json:135, :171, surface-mesh.json:135; chart/surface-dom.test.ts:92; browser/surface.spec.mjs:27 |
| CH-8 | MUST | pass | cases chart/summary.json:174, csv.json:634; browser/chart.spec.mjs:104 |
| CH-9 | MUST | pass | browser/chart-showcases.spec.mjs:70, :98, :157 (+4) |
| CH-10 | MUST | gap | browser/chart-files.spec.mjs:58, :70 (chrome-150: PNG and 3D surface). Scale 1.25 never run; no check that 2D plot lines lie on Device Pixels |
| CH-11 | MUST | gap | cases chart/appearance.json:943 (line-style keys only). No forced-colors test for ExChart |
| CH-12 | MUST | pass | chart/chart-dom.test.ts:508, :545; browser/chart-files.spec.mjs:27, :35, :58 |
| CH-13 | MUST | pass | ExGrid.PackageSmoke/check.sh:162, :166 |
| CH-14 | SHOULD | pass | spikes/render-bench/results/20261019-chart (3 JSON + summary.txt); predates the TS move |
| RT-1 | MUST | pass, clause not asserted | browser/responsive.spec.mjs:3, responsive-family.spec.mjs:48 (grid/sheet/pivot x builtin/mud). Not asserted: phone portrait/landscape viewports |
| RT-2 | MUST | gap | Touch not implemented; no real-device record |
| RT-3 | MUST | gap | Touch not implemented; no real-device record |
| RT-4 | MUST | gap | Touch not implemented; no real-device record |
| RT-5 | MUST | gap | Touch not implemented; no real-device record |
| RT-6 | MUST | gap | Touch not implemented; no real-device record |
| RT-7 | MUST | gap | Touch not implemented; no real-device record |
| RT-8 | MUST | gap | Touch not implemented; no real-device record |
| RT-9 | MUST | gap | Touch not implemented; no real-device record |
| RT-10 | MUST | gap | Touch not implemented; no real-device record |
| RT-11 | MUST | gap | Touch not implemented; no real-device record |
| RT-12 | MUST | gap | Touch not implemented; no real-device record |
| RT-13 | MUST | gap | Touch not implemented; no real-device record |
| RT-14 | MUST | gap | Touch not implemented; no real-device record |
| RT-15 | MUST | gap | Touch not implemented; no real-device record |
| RT-16 | MUST | gap | Touch not implemented; no real-device record |
| RT-17 | MUST | gap | Desktop invariants held by other sections; targeted touch regressions and observations owed |
| RT-18 | MUST | gap | Touch not implemented; no real-device record |
| RT-19 | OBSERVATIONAL | observational | No measurement exists (touch not implemented); owed in Step 6 |

## Gaps and notes

**FD-1** No test presses Ctrl+F inside a Template cell's own control; grid-view.ts:349 lets a control keep its keys. A core test in core/grid-find.test.ts with a template control holding focus would close it.

**FD-3** The tests cover Escape closing Find. None covers a pointer-down elsewhere closing it and returning the keyboard to the root. A find.spec.mjs press outside the panel would close it.

**FD-4** The request's columns are asserted, but not with a hidden or moved column. "No scope when a single cell is selected" is held only for Find All (grid-find-all.test.ts:141). Add step-request cases to grid-find.test.ts.

**FD-5** Only the collapse onto the found cell is asserted. "With a scope the selection stands" is in the code (grid-view.ts ~:8776), and the C# GridFindTests.cs:160 holds only the C# helper. Add a scoped step to grid-find.test.ts that reads the Selection.

**FD-6** The row still says a new step cancels the step in flight. ADR-0451 s3 makes it wait its turn instead (grid-find.test.ts:237). The row needs restating; its other clauses are held at :200 and :221.

**FD-10** The Blazor ExGrid and the Script Core have no `OnFind` parameter; Find is the Source's. The FindRefusalReason.cs doc is stale too. The row names something that no longer exists.

**FD-11** No test feeds an answer outside the request (code throws at grid-view.ts:8741). No test removes a column during a step (code returns orderChanged at :8762). A core test for each in grid-find.test.ts would close it.

**FD-14** The validation of a Find All answer is code only (grid-view.ts:9326-9345): an answer outside the request, too many or too few matches, or matches out of order. Add throwing cases to grid-find-all.test.ts.

**FD-15** Moving through the list and clicking a line are asserted. Enter on a line (find-all-list.ts:134) is not. Add a keydown test to grid-find-all.test.ts.

**FD-16** No test checks that the list goes away on an option change or after Replace All. Add these two cases to grid-find-all.test.ts.

**PV-1** check.sh:179 packs ExBoard with a reference to ExPivot. The row says nothing references ExPivot except its listed dependants, and it was not restated the way DA-1 was. Restate the row or change the reference.

**PV-2** The report grid gets only hideColumnMenu (pivot-view.ts:1542). A click on a report header does not select, and Group/Total rows are not painted: RowKind is unused in packages/pivot*. The server Window clause's rewrite is owed. One grid, pinned, is held at pivot-view.test.ts:296 and :313.

**PV-10** The row and its inspection describe a Razor ExPivot with no JavaScript of its own. ExPivot is now a Script Core and ships dotnet/src/ExPivot/wwwroot/ex-pivot.min.js. The drag clauses are held in browser tests. The row needs restating for ADR-0370.

**PV-13** The kept RSV is held by hidden.json:412 and report-source.json:373. No test reads the Selection after a values-only refresh. A pivot-view.test.ts case would close it.

**PV-15** Nothing counts report-grid updates across a Field List interaction, nor how often the grid Chrome is asked for; the bUnit render counts went with Razor. A pivot-view.test.ts spy on the report grid's setters would close it.

**PV-17** Only the move between two pivots is asserted (pivot.spec.mjs:263). A test opening a menu in one pivot and checking the other has none would close it.

**PV-18** The token and geometry rules are asserted. The expand/collapse glyph as plain markup is not asserted on its own. A minor clause.

**PV-25** No test checks that a further change cancels the question in flight (pivot-view.ts:1082). Add an abort-signal case to pivot-view.test.ts.

**PV-33** The row's en-GB default by UI culture contradicts ADR-0370 s10, under which words never follow locale (pivot-view.test.ts:1099; words.json:200, :226). ExPivot.cs has no such selection, and the PivotWords.cs doc is stale. The row needs restating.

**PV-35** Every test passes the 250 ms throttle explicitly, so the default is not asserted. The Selection and an open menu kept over a values-only redraw are not asserted, and neither is an appearing Item dropping the Selection. Add pivot-view.test.ts cases with no throttle option.

**PV-42** It holds by composition: report row keys are stable (row-keys.json, report-source.test.ts:50) and the grid skips rows whose key and version are unchanged (grid-cell-paint.test.ts:164). No test counts row paints over a live pivot redraw. A pivot-view.test.ts paint count would close it.

**PV-43** reportRowKey (pivot/src/report-grid.ts:61) allocates a PivotRowKey and a string on every read. Only the C# engine is held allocation-free (RowKeyTests.cs:40), and the browser path does not meet the clause.

**PV-45** As PV-42: it holds by composition, with no per-redraw paint count over the pivot.

**PV-47** The OutOfMemoryException path belonged to the removed Razor ExPivot's report computation. The TS pivot reports a source failure as a Stale Report (PV-37). The row names something that no longer exists.

**PV-49** The stale state and the refusal are held. Not asserted: the Summary answering while stale, the refusal's Japanese text and its replacement by id, and no row painted again. Add pivot-view.test.ts cases.

**PV-50** The row itself records Escape, Tab and the no-report panel, when typed while the panel opens, as not checked over the Script Core. A pivot.spec.mjs test typing at machine speed would close it.

**DA-1** The row lists the packages that reference ExGrid.Data. It omits ExChart.Engine (check.sh:162, ADR-0420) and ExGrid.Data.Xlsx (check.sh:127). The check enforces the real list; the row's text needs updating.

**DA-18** The C# test holds a single aggregation definition across the C# assemblies only. The Script Core defines aggregations twice, in core/src/summary/summary-accumulator.ts and pivot-engine/src/aggregates/parts.ts, neither in @ex-grid/data. Not met for the TS family.

**SM-1** The request also carries `focus` (grid-summary.ts:30, for SM-9), so "nothing else" is no longer true. The test asserts figures, columns and focus, but not ranges or the RSV. Restate the row and assert the full request in grid-summary.test.ts.

**SM-2** No test answers under a stale RSV and checks that nothing shows. Add a grid-summary.test.ts case with a late answer.

**SM-5** OnSummarize is refused only beside a source that summarises (grid-view.ts:2919; ExGrid.Surface.cs:206, and not over Rows). ADR-0130 and SM-5 say it is refused beside any bound Source. ADR-0440 changed the Rows case, and the row and code disagree.

**SM-10** No test asserts that the strip announces nothing. selection-summary.ts sets no aria-live or role. A grid-summary.test.ts attribute check would close it.

**SM-14** Only switching the strip off is asserted. Switching it back on asking again is not, and neither is ExSheet or ExPivot passing the option through (pivot-view.ts passes showSelectionSummary). Add those cases.

**SM-12** No test sums every cell of a spilled array. Add a sheet-summary.test.ts case with a spill.

**SM-15** No test counts that the walk is skipped when no figure is shown. A spy case in grid-summary.test.ts would close it.

**LV-2** The grid has no check for a repeated Row Key in a Window. Only the built-in sources refuse one (fetching-source.ts:841, in-memory). A Consumer source with a Row Key and no vouch is never checked. A core grid test with a Window that repeats a key would close it once the grid checks.

**LV-10** The grid never reads vouchesDistinctRows. There is no check of an unvouched Window or of painted-row keys. As LV-2, not met.

**LV-20** Not asserted: a commit refused as RowLeftTheWindow that lands once the key returns, and, with a Row Key, an order-moved press with its row gone giving OrderMoved. Both are in grid-view.ts:5951-5967. Add grid-edit.test.ts cases.

**LV-22** The weak-reference retention tests went with Razor. Nothing measures what the Script Core grid or the ExPivot report source keeps alive after a row leaves. bridge.test.ts:164 covers only the release protocol. A heap or WeakRef test would close it.

**LV-24, LV-25, LV-26, LV-27, LV-28, LV-30, LV-31, LV-34** The DoD note says the component and browser clauses of LV-24..31 and LV-34 have nothing to run against and are met by nothing else; the rewrite for the Leaf Aggregate boundary is owed. Only layer-1 engine tests remain (ReportProtocolTests, WindowChangesDigestTests, IncrementalOracleTests, incremental.test.ts). LV-29 passes because its clauses are layer 1 only.

**LV-32** Typing, edits and Find under a replaced Source are asserted. Paste, Delete, Ctrl+D/R, the fill handle and the fill key refused as SourceChanged are code only (grid-view.ts:7759, :7827), and so is the ExSheet document clause. Add grid-actions.test.ts cases.

**LV-33** Not asserted: the replaced-Source variants for typing, composition and nothing selected, and a moving key aimed the same way. Add grid-edit.test.ts cases.

**CH-3** The test compares 10 and 10,000 points, not the row's 10^3 and 10^5. Add the stated sizes to chart-dom.test.ts.

**CH-10** Only the PNG and the 3D surface run at scale 1.5 (chrome-150). Scale 1.25 is never run, and no test checks that a 2D plot's lines lie on Device Pixels. Add a chrome-125 project and a line-snapping check in chart.spec.mjs.

**CH-11** There is no forced-colors test for ExChart; appearance.json:943 covers only line-style keys. A Playwright `forcedColors: 'active'` chart test would close it.

**CH-14** The record at spikes/render-bench/results/20261019-chart predates the move to the Script Core.

**RT-1** responsive.spec.mjs and responsive-family.spec.mjs vary container widths inside a 1280 desktop window. No phone portrait or landscape viewport is run.

**RT-2..RT-16, RT-18** docs/specs/responsive-touch/README.md says the pointer bridge, touch editing, keyboard preparation, external frames and Pivot touch are not implemented. There is no pointerType or visualViewport code in packages/*/src and no real-device record. Section 34 gates only a release that claims mobile support, and 0.2.0 claims none.

**RT-17** The desktop invariants are held by other sections. The targeted touch regressions and observations are owed until touch exists.

**RT-19** Observational, but nothing has been measured, because touch is not implemented. It is owed in Step 6, not recorded.

