# 75: The Cell Editor shows what Point writes

Status: needs-info

**What to build:** ADR-0058, "What the thirteenth Windows run settled", the defect seen and not asked.
After `Home` or Shift+→ over Point wrote `A10` or `D10:E10` (b3–b5 of the thirteenth run), the Cell
Editor's own `scrollLeft` stayed where it was (81.3 px), and the caret and the written Reference lay 24
to 50 px past its right edge, in all six configurations. The user saw a Formula that did not show what
had just been written.

**Blocked by:** None.

- [ ] After Point writes into the Cell Editor or the Formula Bar — by an arrow, `Home`, a Shift+arrow, a
      press on the Sheet, or a press on a registered grid — the field's caret is inside its visible
      width, so the written Reference can be read. Find which of these paths leave the field's
      `scrollLeft` behind, and fix them all, not only the two the run saw (ADR-0051, ADR-0058)
- [ ] The field's scroll is set through the scroll offsets ADR-0021 allows, at the caret the core
      placed, without measuring text (no layout read on the path to a paint). If the caret cannot be
      brought into view without measuring, stop and report the proposal: that is a decision
- [x] Reference Outlines' text layer (ADR-0057) follows the field's `scrollLeft`, as it does when the
      user types, so the colours stay over the right characters (DC-48)
- [x] Layer 3 on `/sheet` under both Chromes, on both hosts: `=XLOOKUP(1,A2:A4,B2:B4,,` in D10, then
      `Home`; and then Shift+→ instead: the caret's position lies within the editor's client width, and
      the coloured layer matches the field's `scrollLeft`

## Comments

2026-10-01, implemented on `agent/ps-75` in two commits. **The first, `722e763`, is the fix: a caret
at the end of the text. The second, `b8a2930`, is a proposal awaiting a decision: a caret short of
the end.** It can be reverted alone. The status is `needs-info` until the decision is made. The first
two boxes hold at the end of the text, and short of it they wait on that decision.

- **Which paths left the scroll behind: all of them.** Every write the core makes reaches the field
  through `setCaret` in `ex-grid.js`: an arrow, `Home`, a Shift+arrow, a press on the Sheet, a press on
  a registered grid (`WritePointedTextAsync`) and its taking back, in the Cell Editor and in the
  Formula Bar alike, and also F4's rewrite, an accepted candidate and an edit's opening. `setCaret`
  set the selection only. Setting the value or the selection from script moves no view in Chrome, even
  when the selection changes. A static page in Chrome showed it after frames too. So the field kept its
  old offset: 81.3 px after b3 and b5, and 0 after b7 and b11, where the text had grown from `=`. No
  path needed anything in C#. A layer 2 characterisation pins that every key Point writes with, and a
  press, at the end or short of it, tells the listener the caret after what it wrote
  (`CompletionOverPointTests`, `Ticket75_*`).
- **The fix (`722e763`).** `showCaret(input, at)` sets the field's `scrollLeft` to 0 for a caret at 0.
  For a caret at the end of the text it sets it past the far end, and the browser clamps it, so the
  caret stands at the right edge. That is the technique `placeCaretAtEnd` already used (ticket 32),
  which now calls it. Nothing is read but the field's value. `setCaret` calls it for the moving end
  of what it places, before placing it. The coloured layer follows through the field's own `scroll`
  event, as when the user types. That event fires before the next frame: on a static page the layer's
  line equalled the field's offset at the frame. No listener, interop call or read is added.
- **Found by this ticket's own test, on the Server host: held keys.** On a circuit, the keys typed
  while an edit opens are held, then replayed into the field by `typeInto` with `setRangeText`, which
  moves no view either. After `=XLOOKUP(1,A2:A4,B2:B4,,` was typed, the caret stood at the end with
  `scrollLeft` 0. This is not a Point write. It is the same defect at the end of the text, so
  `typeInto` calls `showCaret` after each typed key and each replayed ← or →.
- **The proposal (`b8a2930`), short of the end.** A caret short of the end is left to the browser by
  the first commit. A write moves it right by what was written, and it can cross the field's right
  edge: 19–20 px after a press on the Sheet before `)`, and 297 px after a press on the positions grid
  before `)`, in layer 3 without the proposal. No place for that caret can be set without the width of
  the text before it. The proposal gets that place from the browser. The field holds only the text
  before the caret while the offset is set past its end, then its whole text and its selection again,
  in the same task. That is the value written twice. The browser lays out the shorter text to clamp
  the offset, which may count as the measuring ADR-0021 refuses.
  1. **How often Point writes short of the end.** Typing a Formula from its start always points at the
     end: in Overwrite the caret is where typing left it, so the run's cases are all the first commit's.
     Short of the end, the gestures are:
     - F2 into Caret, the caret moved back with ← or a press in the text, then a press on the Sheet or
       on a registered grid, at a place a Reference can go.
     - The same, then F2 again, which points from there, then an arrow, `Home` or a Shift+arrow.
     - A press into the Formula Bar's text short of its end, which opens Caret there (DC-34), then
       either of the above.

     F4 on a Reference short of the end, a candidate accepted short of the end, and a held key
     replayed short of the end use the same placement. It shows only when the text before the caret
     is wider than the field (the Cell Editor's text box is 83 px, about 12 characters), so when an
     existing long Formula is edited in its middle.
  2. **What the swap disturbs, checked** (Chrome, a static page, against the placement alone and
     against the first commit):
     - **Events:** no `input` or `beforeinput` is dispatched. There is one `selectionchange`, at the
       placed caret, and one `select`, as without the swap.
     - **MutationObserver:** neither ADR-0057's `data-ex-text` observer nor an observer of everything
       on the field records anything.
     - **Native undo:** Cmd/Ctrl+Z after a core write did nothing in all three variants. The core's
       own write, a value set from script, has already emptied the field's history.
     - **The selection:** writing the value takes it to the end of the text, so the swap must put it
       back. The first version did not. A held ← replayed after F2 then left the caret at the end,
       and seven layer 3 tests failed (SH-36 x1 and 11a, DC-19, DC-28/ED-22, and the proposal's own).
       Putting it back keeps the start, the end and the direction.
     - **An IME composition:** the swap disturbs it. With `か` composing and the field's value equal
       to the placed text, the placement alone and the first commit leave the composition going, and
       it ends as `…かな)`. The swap ends it and commits `か`, so the next update starts a new
       composition and the text becomes `…かかな)`. It happens only when a placement lands while a
       composition stands with the same text. A guard that skips the swap while composing would need
       the composition known on every surface. `composingIn` is kept only for a field beside a
       Reference layer.
     - **The MudBlazor Chrome's control:** its Cell Editor and Formula Bar text are plain `<input>`s
       bound with `@bind:get`/`@bind:set` on `oninput` (`MudCellEditor.razor`,
       `MudFormulaBarText.razor`), with no MudBlazor component or script on them. The proposal's tests
       pass under it on both hosts.
  3. **The alternatives that measure nothing:**
     - **The first commit alone.** Short of the end stays as before, recorded here as a known gap.
     - **The proposal**, with an IME guard if it is taken.
     - **Show the end when only a few characters follow the caret** (`)`, `))`, `,0)`). This writes
       nothing and reads nothing. It is an estimate from a character count, as ADR-0016 estimates `####`
       from digits. It covers the commonest case, `=SUM(A1,|)`, and leaves longer tails as they are.
       In a field narrower than those characters, it could put the caret past the field's left edge.
     - **Let the browser's editing write what the core writes,** with `execCommand('insertText')` over
       the span. That reveals the caret natively and keeps native undo. It changes how the core's
       writes reach the field, which today the core renders, and dispatches inputs that the core
       must not take for typing. That is an ADR.

     Refused: reading `scrollWidth` or `clientWidth`, which is measuring. `scrollIntoView` on the
     layer's spans, which ADR-0021 refuses. A blur and a focus to make the browser reveal the caret,
     which would move focus under the grid's own focus logic.
- **Tests.** Layer 2: `ShippedStylesheetTests.A_caret_placed_by_the_core_is_brought_into_view`, new,
  pins `showCaret` and its four callers, and that nothing else sets a field's offset.
  `The_listener_reports_and_places_the_caret` and the ticket 32 test follow the change.
  `CompletionOverPointTests` gains 9 + 1 cases. The script pins failed first, and the
  characterisation passed at once: the core was already right. Layers 1 and 2: 826 + 2135 + 88 + 1202
  (1 skipped, as before) + 390, all passing, after each commit.

  Layer 3 has 7 tests for the first commit, measured by `expectCaretShown` in `sheet-helpers.mjs`. It
  sets a copy of the text before the caret in the field's own font, in the test only, and requires the
  caret inside the field's text box within 1 px, the field scrolled, and the layer's line at the
  field's offset.
  - In `declarations.spec.mjs`, under both Chromes: Home, and Shift+→ instead, at the open value list
    after `=XLOOKUP(1,A2:A4,B2:B4,,` in D10 (b3, b5); ↓ and a press on the Sheet at the end of
    `=SUM(A2:A4,B2:B4,C2:C4,`; ↓ and a press in the Formula Bar after forty ranges.
  - In `pointing-scope.spec.mjs`: a press on the positions grid after `=` (b11).

  The proposal adds 3: a press and ↓ after F2 before `)`, under both Chromes, and a press on the
  positions grid before `)`.

  Runs: headless, macOS, `--project=chrome`, private ports. Edge, and the full run, are CI's.
  - With the fix set aside, all 7 failed at their first check after the write (WebAssembly). The
    caret stood 25–27 px past the Cell Editor's 83 px text box, 28 and 25 px past the bar's, and
    247 px for the positions grid.
  - At the first commit, with DC-19, DC-34, DC-45, DC-48, SH-36 and the circuit spec's ED-22, SRV
    and CP tests: WebAssembly 80 passed and 2 skipped (the Server-only tests); Server 82 passed.
    Before `typeInto` was changed, the Server host failed b3/b5's check before the key, under both
    Chromes.
  - The proposal's 3 failed at the first commit on WebAssembly.
  - At the proposal, with DC-47 and DC-28 as well: WebAssembly 93 passed, 2 skipped, and SRV-5's
    held Tab failed once and passed 8 of 8 alone. Server 93 passed and 2 skipped, and the proposal's
    press failed once: it raced the caret report. It now waits for the positions grid to be pointed
    at, as the pointing-scope tests do. Ticket 75's 10 tests, 3 times each: 30 of 30 on each host.
- **Seen, and not this ticket's:** SRV-5's failed WebAssembly run threw
  `InvalidOperationException: Operator In on column 'Book' requires a non-empty Values list`
  (`GridQueryEngine.ValidateOperands`) from a render on `/features`, when the filter was applied with
  nothing ticked.
