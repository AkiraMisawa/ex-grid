# 77: Escape with nothing to dismiss releases Tab, not the keyboard

Status: done

**What to build:** ADR-0012, the paragraph rewritten on 2026-10-01 (decided with the user), and KB-8
as rewritten. The fifteenth Windows run (`verification/2026-10-01-windows-15/report.md`, "Seen, and not
asked", case i2) saw a second Escape with no edit open send DOM focus from the grid to `body` on
`/sheet`, in all twelve configurations. That was ADR-0012's Leave as first decided
(`GridKeyKind.Leave` → `LeaveAsync` → the handle's `blur`), not a defect; the decision itself changed.
*(This ticket first called it a defect; ps-77 found the decision before writing code.)*

**Blocked by:** None. Ticket 79's prototype moves the keyboard off the root; what Escape releases
there is that prototype's to report.

- [x] Escape with nothing left to dismiss (no edit, no popover, no list, no Interactive cell) keeps DOM
      focus on the root and releases Tab: the next Tab or Shift+Tab is not claimed, so the browser moves
      to the next or the previous element of the page (ADR-0012, KB-8)
- [x] Any other key after that Escape keeps its meaning and ends the release: a character opens an edit
      in the selected cell, an arrow moves, and a later Tab cycles inside the selection again. A press
      on the grid ends it too (decided 2026-10-01): in the capture-phase `mousedown` already attached
- [x] The Escape is a change of the claimed set (ADR-0010): keys typed after it are held until it is
      answered, as after any mode change; no listener is added and no layout is read (ADR-0021)
- [x] ExGrid and ExSheet alike, under both Chromes; the Escape a popover, an Inner Popup, Find, a
      completion list or an edit takes keeps its meaning
- [x] Layer 2 for the gate and the core; Layer 3 on both hosts: `features.spec.mjs`'s KB-8 test rewritten
      (Escape, then Tab reaches the next page element; the root held focus in between), and on `/sheet`
      under both Chromes: a character, Escape, Escape, a character opens an edit in the selected cell

## Comments

2026-10-01, implemented on `agent/ps-77`.

- **What moved focus.** It was ADR-0012's Leave as first decided, not a defect. Escape with nothing
  to dismiss resolved to `GridKeyKind.Leave`, whose last branch called `LeaveAsync` and so the
  handle's `blur`. ExSheet has no Escape of its own. Found by reading the path and confirmed with a
  throwaway bUnit probe on `/sheet`'s sequence (`k` on D10, Escape, Escape: one `blur`). The ticket
  stopped there for the decision, and was rewritten after it.
- **The core.** The last branch of Leave now calls `ReleaseTabAsync`, which tells the handle
  `releaseTab`. The handle's `blur` is gone. The core keeps no release of its own, and every key
  after it is answered as before. Where an earlier layer takes the Escape (a popover, an Interactive
  cell, a descendant's Escape, a cancelled edit, a completion list), nothing is released, as before.
- **The gate** (`ex-grid.js`, inside the capture-phase entry; no listener added, no layout read):
  - `releaseTab` sets `tabReleased`. While it is set, Tab or Shift+Tab on the root with no edit open
    gets the new verdict `'out'`. Nothing is prevented, the browser moves focus, and the release is
    spent.
  - Any other key the gate sees ends the release, a modifier's own keydown excepted (the Shift of
    Shift+Tab). The gate counts it, as it counts an Inner Popup's Escape, so a key held behind the
    Escape ends it in its turn, after the answer that granted it.
  - Escape on the root with no edit open is now `'mode'`, so the keys after it are held until it is
    answered (ADR-0010).
  - A released Tab held behind its Escape is dropped, with every key behind it, and the release
    stands. Script cannot move focus for it (ADR-0021), and the keys after it were meant for the
    page's next element. This happens only faster than a round trip, so on a circuit.
- **A press ends the release** (decided with the user while this was built). The existing
  capture-phase `mousedown` clears it, at the press and again at a held press's replay. It also
  counts presses. `releaseTab` is granted only if no press reached the root since the Escape was
  forwarded, because a press the hold does not take (a heading, the Name Box) reaches the core
  ahead of the Escape's answer. A press elsewhere on the page is not heard.
- **Where the released Tab goes.** On `/features` it goes to the header's first ▾: those buttons
  are tab stops of their own (KB-12), and Tab goes on through them to the page. The old `blur()`
  went to the same place. On `/sheet` it goes to the element after the Sheet.
- **Not covered here.** After Escape, a press elsewhere on the page and a Tab back into the grid, the
  next Tab still leaves, because the press was not heard. Ticket 79's prototype moves the keyboard
  off the root, and what Escape releases there is that prototype's to report.
- **Tests.** Layer 2:
  - `GridJSInterop` stubs `releaseTab` (`TabReleases`) and no longer stubs `blur`, so a blur fails
    the strict stub. Red first: 12 failures, all of them the stub refusing `blur`.
  - The existing Escape assertions read `TabReleases`: `DescendantKeyTests`, `InteractiveTests`,
    `FilterChromeTests`, `EditStandsTests` and `FormulaBarEditingTests`.
  - `EscapeReleasesTabTests` +3 in ExGrid: the cancelling Escape releases nothing, the next one does,
    and `k` then opens an edit in the selected cell; an arrow moves and Tab cycles after it; each
    Escape releases again.
  - `EscapeReleasesTabTests` +2 in ExSheet: the fifteenth run's case i2 on D10, and the Escape a
    completion list takes, which releases nothing.
  - The press rule has no C# side, so layer 2 cannot see it.
- **Layer 3, `--project=chrome`, headless:**
  - `features.spec.mjs`: ED-3 is cut to the cancel. KB-8 has four new tests: Escape keeps the
    keyboard and Tab or Shift+Tab leaves, with a button either side of the grid; Escape, Escape, a
    character; Escape, a press, Tab; Escape, an arrow, Tab. A Server-only fifth runs at a 150 ms
    round trip: a Tab typed behind Escape is dropped, and a character opens an edit.
  - `sheet.spec.mjs`: two KB-8 tests under each Chrome. One is case i2. The other checks that Tab
    cycles after Escape and an arrow, or after Escape and a press, and that Escape then Tab leaves
    for a button after the Sheet.
  - `edit-stands.spec.mjs`: Escape in the positions grid now keeps its focus.
  - Before the press line, the three press cases failed: Tab left the grid and the Focus stayed on
    E12.
  - Results: on WebAssembly 11 passed + 1 skipped; on Server 12 passed. Because the gate changed
    every Escape on the root, features, popovers, circuit, find, inspectors and excel-keys also ran
    in full: Server 180 passed, WebAssembly 177 passed + 3 skipped.
  - Layers 1–2: 829 + 2135 + 91 + 1209 (1 skipped, as before) + 392, all passing.

