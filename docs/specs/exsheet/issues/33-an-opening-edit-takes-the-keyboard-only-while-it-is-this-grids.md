# 33: An edit that opens takes the keyboard only while it is still this grid's

Status: done

**What to build:** ADR-0021's note of 2026-09-30, with ADR-0018 section 6 and ADR-0010's notes of
the same day. Found by CI on PR #34 (msedge, Server host): ED-26's test on `/sheets` typed `=` on the
left Sheet and pressed the right one. The left editor's focus landed a round trip later and took the
keyboard back to the left. The race was on the base before that PR: 9 runs in 40 failed there with
40 ms injected.

**Blocked by:** None (can start immediately)

Core (ExGrid):

- [x] The core's request that the Cell Editor, or the Formula Bar's text, take DOM focus (on
      opening, on F2, after a Reject) goes through the module. It is granted only while DOM focus is
      inside this root or on nothing, the condition `reclaimFocus` reads (ADR-0021, ED-28)
- [x] When it is declined, the edit is left standing, and a press on the rows brings the keyboard
      back to it (ADR-0018 section 6, ED-26)
- [x] Keys held behind the key that opened the edit are typed into it, in order, when the focus is
      declined. They do not wait for the listener's fallback, and never reach the grid the keyboard
      went to (ADR-0010's note of 2026-09-30, ED-22)
- [x] `CellEditorContext` and `FormulaBarTextContext` hand a Chrome a focus function of the core's.
      The Chrome calls it on a request it has not answered, once its control is painted. The core
      finds the control inside its own box and holds no reference to it (ADR-0010)
- [x] The comments on `reclaimFocus` and on the press back count three decisions about focus
      made in script, not two
- [x] No JavaScript use is added, and no layout is read (ADR-0021)

ExGrid.MudBlazor:

- [x] `MudCellEditor` and `MudFormulaBarText` call the context's focus function instead of their own
      `FocusAsync`

Tests:

- [x] Layer 2: the editor's focus goes through the module, for the built-in editor and under the Mud
      Chrome (ED-28)
- [x] Layer 3 on `/sheets`, Server host, 40 ms injected, under both Chromes: `=` on the left and a
      press on the right at once, 30 times; `=1` at full speed and a press on the right at once. The
      right holds DOM focus every time, and the left edit stands with its text (ED-28)
- [x] ED-26's `/sheets` test passes on both hosts under the suite's own runner

Open, recorded in ADR-0021 and not built here: a popover's opening focus (the column menu, the
filter, Find) and a Template cell's own focus have the same race in principle.

## Comments

2026-09-30, implementation: the core's request that the Cell Editor or the Formula Bar's text take
DOM focus is `focusEditor(bar)` on the module's handle. It finds the field in the core's own
`.ex-editor` box (`ownSurface`, `surfaceField`) and focuses it only while DOM focus is inside the root
or on nothing. Declined, it marks the edit `focusDeclined`: the hold's wait for the editor's focus
ends, and the keys held behind the opening key are typed into the surface that last held the keyboard
(`standingField`). The mark is forgotten as an edit opens or ends, and when a press back puts the
keyboard in the edit. `CellEditorContext.TakeFocus` and `FormulaBarTextContext.TakeFocus` are the
core's focus function; `MudCellEditor` and `MudFormulaBarText` call it. Layer 2:
`EditorFocusTests` (6), a script inspection in `ShippedStylesheetTests`, and three Mud tests.

Measured on the Server host with 40 ms injected, headless:
- The new ED-28 test in `edit-stands.spec.mjs` (`=` on the left, the right pressed at once, 30
  rounds, a soft check per round): before the change, 30 of 30 rounds lost the keyboard to the
  left editor under each Chrome, and the `=1` case failed under each. After it, 0 of 30 under each
  Chrome, and the `=1` case passes under each.
- The flow of the CI failure (ED-26's `/sheets` steps: `=` on the left, wait until it is painted,
  then the press on the right), replayed by a temporary probe that logged every focus change: 9 of
  40 failed on the base, each with the left editor taking focus after the press on the right; 0 of
  40 after the change.

ED-26's `/sheets` test passes on both hosts under the suite's runner; the ED-28 tests skip on
WebAssembly, which has no round trip for a press to land in.

*(2026-10-10, backlog cleanup.)* Status set to done. Every box was ticked, and
`edit-stands.spec.mjs` runs ED-28 under both Chromes on the Server host, and ED-26 on `/sheets`, in
CI.
