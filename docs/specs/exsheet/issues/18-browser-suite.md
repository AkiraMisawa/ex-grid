# 18: ExSheet in real browsers

Status: ready-for-agent

**What to build:** Layer 3 for everything above, on Chrome and Edge, against both hosts. It covers typing, Point by
keys and by mouse, completion accepted with Tab, the Formula Bar mirroring, the fill-handle drag,
paste from the real clipboard, Ctrl+arrow, Headings, insertion, and two ExSheets on one page staying
independent. The console must stay clean, and the invariants must hold. Only one agent runs
layer 3 at a time.

**Blocked by:** 06, 07, 10, 11, 13, 15, 16

- [ ] Every ExSheet criterion marked layer 3 in the Definition of Done passes on both browsers and both hosts
- [ ] Two ExSheets: keys, popovers, undo and the Formula Bar never cross (ADR-0018)
- [x] No console message, and the DOM does not grow with the extent

## Comments

2026-09-27, the suite. Three specs in `tests/ExGrid.Browser`, sharing `sheet-helpers.mjs`, run by
the committed config on `chrome` and `msedge`: `sheet.spec.mjs` (`/sheet`: typing and Formulas,
Ctrl+arrow, the Headings and the band held while scrolling sideways, the Name Box with and without
an edit open, the Formula Bar mirroring, insertion by button and Context Menu with one Ctrl+Z each,
the Linked Table's `#GETTING_DATA` then its values, XFD1048576 with the DOM no larger than at A1),
`declarations.spec.mjs` (§26 as ExSheet declares it: completion under both Chromes, Point by keys
and mouse, F2, from the bar, DC-20 and DC-28 at 150 ms, the fill-handle drag and its edge
auto-scroll, a spill from the real clipboard, copy and paste inside the Sheet, the invariant
marker, undo and redo, the grips, the positions grid declaring nothing) and `sheets.spec.mjs` (two
ExSheets on the new `/sheets` page). `/sheet?chrome=mud` runs the Sheet under `ExGrid.MudBlazor`'s
Chrome. The page index tests (RI-1..3) list `/sheets`.

Run locally under xvfb with Playwright's Chromium (build 1194) — this machine has neither Google
Chrome nor Edge, so the first criterion's "both browsers" is CI's to show — against the WebAssembly
host and the Server host behind the latency proxy. The whole suite, once per host at the end: on
WebAssembly 249 passed, 7 skipped by name, 0 failed; on Server 251 passed, 5 skipped by name, 0
failed. Of those, the ExSheet specs: `sheet.spec.mjs` 10/10 on both; `sheets.spec.mjs` 5/5 on
WebAssembly, 4 + 1 expected failure on Server; `declarations.spec.mjs` 31 + 1 expected failure on
WebAssembly, 28 + 4 expected failures on Server. No console message in any of them (CON-*).

**Left failing, each by `test.fail` naming its criterion** — defects found by this suite, none
fixed here because each needs a decision:

- **SRV-5 / ED-22, Server host.** Typing into an editor that is already open loses characters at an
  ordinary speed: each input raises a render, and the render writes the text as it stood at that
  input back into the field, over what was typed since. `=SUM(1,2,3,4,5)` typed at 10 keys a second
  at 150 ms arrives as `=SM1234,)`; the plain grid on `/features` loses them too (`Xabcdefghij` →
  `Xabdfhj`); the Name Box as well (`nonsense` → `nnse`). ED-22's own cases pass only because every
  key there is held behind the key that opened the editor.
- **DC-28, Server host.** The keys held behind F2 land in the Formula Bar in order, but the same
  write-back then puts the browser's caret at the end, so the next key lands there: `=7*+12` where
  `=7*2+1` was typed.
- **ADR-0007, Server host.** With an edit open, Ctrl+Z is the input's own and should undo
  uncommitted typing; the write-back empties the input's undo history, so it undoes nothing. DC-30
  itself (the core does not claim the keys while an edit is open) passes.
- **DC-34 / DC-22, `ExGrid.MudBlazor`'s Chrome, both hosts.** A press into the Formula Bar opens the
  edit with DOM focus in the Mud Cell Editor, not the bar: `MudCellEditor` mounts with no request
  seen and takes any `FocusRequest` as new. The caret is not where the bar was pressed, and keys
  typed next go to the cell's surface. The seam's contract does not say how a control mounted by an
  edit the bar opened knows it was not asked to focus.
- **ADR-0018, Server host (two Sheets).** Escape cancels an edit and the core hands DOM focus back to
  that grid's root a round trip later; a press on the other Sheet inside that round trip is
  overtaken by it, and the keys typed next go to the Sheet the user left. At 150 ms it happens every
  time, and it showed at 0 ms too. Focus stays out of JavaScript (ADR-0021), so the answer is a
  decision. Because of it the second criterion stays open.

**The invariant marker (ADR-0050, fourth round).** In Chromium 1194 the paste event is handed
`<table data-ex-grid="invariant">` on every route: the copy event (WebAssembly's keyboard copy),
`navigator.clipboard.write` (every copy on the Server host) and a Context Menu copy on either host.
Each run records it in `metrics.json` (`DC-33 marker …`). Edge proper is not checked here.

**Seen and reported, not pinned:** on a circuit the completion list is painted by one message and
the key listener is told it is open by the next, so a ← pressed in between is gated as Overwrite's
and swallowed; and every change to the Sheet clears ExSheet's notice, a Consumer's Linked Table push
included, so a refusal said just before a push disappears. The tests wait past both.

Also found: `memory.spec.mjs`'s MEM-4 listed five listeners on the root and failed against the
module, which attaches a sixth for the caret report (ADR-0051, DC-24). The test now names six.

**Still open here:** the first criterion (both browsers — CI; the failures above on the Server host
and under the Mud Chrome), the second (the late focus reclaim).
