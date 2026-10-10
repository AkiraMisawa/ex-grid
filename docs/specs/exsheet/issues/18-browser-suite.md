# 18: ExSheet in real browsers

Status: needs-info

**What to build:** Layer 3 for everything above, on Chrome and Edge, against both hosts. It covers typing, Point by
keys and by mouse, completion accepted with Tab, the Formula Bar mirroring, the fill-handle drag,
paste from the real clipboard, Ctrl+arrow, Headings, insertion, and two ExSheets on one page staying
independent. The console must stay clean, and the invariants must hold. Only one agent runs
layer 3 at a time.

**Blocked by:** 06, 07, 10, 11, 13, 15, 16

- [ ] Every ExSheet criterion marked layer 3 in the Definition of Done passes on both browsers and both hosts
- [x] Two ExSheets: keys, popovers, undo and the Formula Bar never cross (ADR-0018)
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

*(2026-10-10, backlog cleanup.)* The second box is ticked. `sheets.spec.mjs` covers all four: the
keys ("SH-13/DC-25: typing and Formulas stay in the Sheet that has the keyboard", and a cancel
finished late in the other Sheet), the popovers (a completion list, a pointing outline and the
Formula Bar belonging to one Sheet; "a Context Menu opens in the Sheet it was asked of"), undo
("SH-13: each Sheet keeps its own undo stack") and the Formula Bar. The `test.fail` defects listed
above are no longer marked anywhere.

The first box stays open. Two criteria of §27 name Layer 3 and have no layer-3 test on a Sheet:

- **SH-27**, Ctrl+Enter with a Formula over a range: `=A1` over B2:C3 from B2 gives `=A1`, `=B1`,
  `=A2`, `=B2`. Layer 2 has it (`tests/ExSheet.Components/ExcelKeyWiringTests.cs`); its layer 3 is
  "beside Excel", so a probe in `sheet-vs-excel.spec.mjs`.
- **SH-41**, a bold number's `####` and an italic number's edges, read in pixels. Only the core's
  DC-58 tests exist (`appearance.spec.mjs`), on `/appearance`, which has no ExSheet.

Every other §27 criterion that names Layer 3 has a live test. What SH-42, SH-51 and SH-52 ask by
hand on Windows is a Windows run's, not this ticket's.

*(2026-10-10, SH-27 and SH-41 in layer 3.)* Both have tests now. SH-27 passes. SH-41 fails on Linux,
where `system-ui` is DejaVu Sans. The fix needs a decision, so the first box stays open and the status
is `needs-info`.

- **SH-27**, `sheet-vs-excel.spec.mjs`, "third run, main keys case 5". This is Excel's case 5 of the
  third Windows run, on `/sheet`'s own cells. B2:C3 is selected with the mouse from B2, and `=A1` is
  typed with Ctrl+Enter. B2 holds `=A1`, C2 `=B1`, B3 `=A2` and C3 `=B2`, showing Item, Qty, Apples
  and Item. The Selection stays B2:C3, with B2 active. One Ctrl+Z gives all four cells their Entries
  back, and D2 its 6.
- **SH-41**, `sheet-paper.spec.mjs`, four tests under each Chrome. They run on two new cases of
  `SheetCases`, `bold` and `fit`. Each case sets its columns a hundredth of a pixel from where a number
  stops fitting, in the Cell Metrics the Sheet paints with, which `SheetPage` passes in.
  - At the bold edge: a hundredth under it, 12% is shown plain and `####` bold, named by its value. A
    hundredth over it, the bold 12% is shown.
  - General: each cell shows the widest General text its own widths fit. Under the built-in Chrome the
    plain cell shows `1.23457E+20` and the bold one `1.2346E+20`, because the bold `+` is the wider.
    Under the Wrapper's Chrome it is the other way round, because Roboto's bold separator is the
    narrower.
  - Italic: 12%, aligned right and aligned left, a hundredth over the regular edge, is not cut at
    either edge.
  - "Painted whole" is read in pixels. The cell as painted and the same cell with its clip and ellipsis
    lifted must be one picture (`stillPictures`, `pixelsApart` at 0).
- **Every other §27 criterion that names Layer 3 has a live test**, confirmed by reading them: SH-14,
  18, 28 (DC-45's F4 tests), 29, 31, 32, 34, 35, 36, 39, 42, 43, 45, 46, 48, 50, 51, 52, 53 and 54,
  and SH-58 and SH-60 (`sheet-today.spec.mjs`'s TODAY() and NOW() tests, titled by ADR). None is
  `fixme`. Each skip is by host, and the other host runs the case.

**Found: on Linux a bold number at its bold edge can be cut.** The fourth SH-41 test puts the bold 12%
a hundredth over its bold edge in eight columns of one declared width, B to I, at eight offsets, so
that their edges round to Device Pixels in more than one way. With `system-ui` in DejaVu Sans, D2 is
cut. Linux paints `system-ui` in DejaVu Sans (ADR-0016's measurement), so CI's Ubuntu runners should
show this too; that was not seen here. D2 is declared 49.87px and painted 49px, because its edges were
rounded to Device Pixels (ADR-0090). That leaves 33px of content for the text's 33.516px, and
`text-overflow: ellipsis` draws `1…`. `scrollWidth` does not show this, but the pictures do.
- The `####` decision and General's fit read the declared width (ADR-0090). A column can be painted up
  to a Device Pixel narrower than it is declared.
- ADR-0090 relies on the estimate to "leave more than that to spare". DejaVu Sans Bold fills the core's
  bold classes almost exactly: its digit is 9.742px against 9.75. So 12% has 0.35px to spare.
- ExGrid's own group and total rows paint at 600, which Linux draws in the same Bold face, and they are
  judged by the regular widths, whose digit class is the same 9.75. So the same arithmetic applies to
  them. That was not tested here.
- Under the Wrapper's Chrome (Roboto), and in this container's own `system-ui` (Inter), all eight
  columns are painted whole.

The test is left failing by `test.fail` where `system-ui` is DejaVu Sans, measured in the page, and
names SH-41. The decision needed is on ADR-0090's estimate, and with it ADR-0016. Two proposals:
- **Charge the estimate one more pixel** for the `####` decision and General's fit, and size Auto
  columns with the same charge. A number shown then fits its painted cell however its edges round, at
  100% and above. The decision still reads the declared width, so it does not change with the scale.
  The cost is that `####` comes up to a pixel earlier.
- **Let a shown number use one Device Pixel of the cell's right padding.** The content box is then
  never narrower than the declared content. No decision changes, but every right-aligned number moves
  right by one Device Pixel.

Runs, with Playwright's Chromium 1194 under xvfb, `--project=chrome`: SH-27's and SH-41's tests with
`--repeat-each=3` passed on both hosts in this container's face (Inter). With `system-ui` pointed at
DejaVu Sans by a local fontconfig file, 24 of 24 passed on each host, the D2 sweep being the expected
failure three times. The whole of `sheet-vs-excel.spec.mjs` and `sheet-paper.spec.mjs` ran once on each
host, with no console message. The first box still needs Edge too, which is CI's.
