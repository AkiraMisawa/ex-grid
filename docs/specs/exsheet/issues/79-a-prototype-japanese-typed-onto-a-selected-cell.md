# 79: A prototype: Japanese typed onto a selected cell composes

Status: needs-info — the prototype is built; the user decides whether to take it (see the Comments)

**What to build:** a prototype, not for merging, so that the user can decide (ADR-0010, the note of
2026-10-01). The fifteenth Windows run found that on a selected cell with no edit open the IME cannot
start: DOM focus is on the root, which is not editable, Chrome and Edge give it no input context, and
`kana` types Latin text. Excel composes from the first key. With F2 first, every IME reading held.

The idea to try: while a cell is selected, the keyboard is held by a text field of the grid's own, not
seen, inside the root. Keys it does not take reach the capture-phase listener on the root as today. A
composition that starts in it opens the edit in the selected cell, and the composing text and the
IME's state are carried into the Cell Editor (or the edit is drawn over that field), so the user sees
the composition in the cell, as in Excel.

**Blocked by:** None. Build on its own branch from the base; do not merge.

- [ ] A prototype on a branch of its own. It changes no ADR, CONTEXT.md or DoD row; what it would
      change is listed in its Comments as a proposal
- [ ] Measured, not argued: layers 1 and 2 in full, and layer 3 for the keyboard (KB-*), the
      clipboard (CP-*), the editor's focus (ED-26, ED-28, ADR-0021's notes), multiple instances
      (ADR-0018) and Find, on both hosts. Every failure is listed, with whether the prototype or the
      test is wrong
- [ ] What a real IME does with it is for a Windows run: write its procedure as a proposal, numbered
      from `docs/agents/numbering.md` only once the user reserves the run
- [ ] The Comments say which way the design could go (a hidden field carried into the Cell Editor;
      the Cell Editor itself kept open and unseen; a field that becomes the editor), what each costs
      against ADR-0010, ADR-0018 and ADR-0021, and what the prototype chose

## Comments

2026-10-01, built on `agent/ps-79-ime-prototype` (from `claude/exsheet-ime-and-scaling` at `5c7ceaa`).
**Not for merging.** No ADR, `CONTEXT.md` or DoD row was changed; what would change is proposed at
the end.

### What the prototype does

While a grid has an editable column and no edit is open, DOM focus is on the **Keyboard Field**
(`input.ex-key-field`, a working name), a text field of the grid's own, first among the Viewport's
children, keyed so no render replaces it, standing in the Focus cell's box from the Cell Editor's own
arithmetic (`ExGrid.KeyField.cs`). It is unseen (opacity 0, no caret, no pointer events) and
read-only over a cell that typing would not open, so an IME stays off there as it did on the root.
A display-only grid has none and keeps the keyboard on its root.

- **Keys.** The capture-phase listener on the root hears every key first, as before. The field
  counts as the root (`isRoot`), so the gate decides exactly what it decided: the keys it takes are
  `preventDefault`-ed and never reach the field; a composing key (`isComposing`, 229) is left alone,
  and reaches the field, where the IME composes.
- **A composition.** At `compositionstart` the script marks the field and the stylesheet draws it as
  the Cell Editor is drawn, over the Focus cell; the core is told, and reveals the Focus if a scroll
  had taken it away. No edit is open while it lasts. At `compositionend` its text takes its place
  among the held keys (ADR-0010's hold): the core opens the Cell Editor in Overwrite holding it,
  judged as a typed character is (`OnKeyFieldTextAsync`), and the keys typed after it wait until the
  editor has the keyboard. The field keeps showing the text until the keyboard has left it, so nothing
  blinks out for a round trip. A cancelled composition (text `''`) opens an empty edit, as Excel's
  first Escape leaves one (the fifteenth run, i2).
- **DOM focus never moves while a composition lasts.** Moving it ends the composition there and then;
  the keys after it start another, and `kana` comes out `ｋあ` (the fifteenth run's i1y saw `k穴`). So
  the core's request that the editor take the keyboard waits while the field composes, and is granted
  when it ends; a second composition the IME finishes in the field before the first one's editor has
  the keyboard (a circuit, a fast typist) is appended to the edit at its caret, in order; and a
  primary press anywhere in the root during a composition ends it first (`blur()` on the field), so its
  text is held ahead of the press and goes to the cell it was composed on — the press then commits it
  there as Excel's click-away does, and selects what it pressed.
- **Focus coming back.** `reclaimFocus` puts the keyboard in the field instead of on the root, and
  focus that lands on the root itself (Tab, a press on a part of the root that takes no focus) is
  passed on to it. Copy and paste fire on the field and are the root's, as before; the document's
  selection is no longer dropped when a key lands on the field, since it is the field's caret, which
  the IME needs.

### The three ways it could go, and what each costs

1. **A hidden field whose composition is carried into the Cell Editor** — chosen, in this form: the
   field is drawn as the editor while it composes, and hands over only at the composition's end.
   Carrying a composition while it lasts is not possible: DOM focus moving ends it (above).
   - *ADR-0010.* The Cell Editor seam is untouched: a Chrome's editor receives the text as
     `InitialText`, exactly as a typed character arrives. The gate's guard (`event.target === root`)
     widens to the field. ADR-0010's hold gains one kind of held item, a composition's text. The
     composition is drawn by the core's field, in the core's editor look, not by the Chrome's editor,
     for as long as it lasts: under `?chrome=mud` the composing cell looks like the built-in editor
     until the IME commits. And no edit is open in the core's terms until the composition ends: the
     Formula Bar shows the cell's old value, and `OnEditingChanged` is raised at the end, where Excel
     is in Enter mode from the first composed key.
   - *ADR-0018.* Section 1's mechanism holds — the listener is on the root, and which grid is active
     is still answered by the browser's focus, now `root.contains(activeElement)` — but its words
     ("make the root focusable … and attach the listener there"; ED-26's "the root (committed)") name
     the root as the element that holds focus. Each grid has its own field; nothing is shared.
   - *ADR-0021.* New listeners on the root: `compositionstart`, a second `compositionend` (always on,
     where the coloured text's is on only while an edit is open), `focus` (the root's own, passed on)
     and `focusout` (the field emptied as it is left). Two new decisions about focus made in script:
     the root's own focus passed to the field, and the field blurred by a press that lands during a
     composition; and a refinement of the third (the editor's request waits while the field
     composes). The ground is the first: no Blazor API gives an IME an input context on an element
     that is not editable, and the order of a composition's end among held keys and presses exists
     only in the browser. And ADR-0005's note that the copy and paste events fire on the focused root
     "would have forced the hidden-textarea trick every other grid library carries" — this is that
     trick, carried for the IME rather than for the clipboard.
   - *Elsewhere.* ADR-0033: DOM focus is on an unlabelled text field, so the root's
     `aria-activedescendant` is no longer on the focused element, and an assistive technology would
     announce an edit field instead of the Focus cell. Not built; it needs a decision of its own. The
     root's focus ring (`.ex-grid:focus-visible`) no longer shows, and a ring keyed to the field would
     show after a click too, since a text field always matches `:focus-visible`.
2. **The Cell Editor itself kept open and unseen.** The IME would compose in the real editor, and no
   hand-over would be needed; the edit could open at the composition's start, as Excel's does.
   - *ADR-0010/0030.* Every Chrome's editor control would have to stand mounted, empty, focusable and
     invisible whenever a cell is selected — a dormant state the `CellEditorContext` does not have,
     and a contract a Chrome can break visibly (a MudTextField's focus underline and label over every
     selected cell). Swapping Chrome would then change what the user sees, and could change where the
     keyboard is.
   - *ADR-0010/0051/0057, and the suite.* "An edit is open" and "the editor is mounted" come apart:
     every place that reads `.ex-editor` as an open edit (the gate's surfaces, the coloured text, Find
     refused while editing, the held keys' target) changes, and the layer-3 suite asserts
     `editor toHaveCount(0)` after an edit ends in dozens of places.
   - *ADR-0018/0021.* The same focus costs as option 1.
3. **A field that becomes the editor.** The core's own field holds the keyboard and is the edit's
   surface from the composition's start, so focus never moves at all. Under the built-in Chrome this
   is option 2 confined to the core's own input, and the cleanest of the three. Under a substituted
   Chrome the edit an IME opens would be typed in the core's field for its whole life, the Chrome's
   editor bypassed by input method (ADR-0010/0030: swapping Chrome would change which control the
   user types into); or it hands over at the composition's end, which is option 1.

**Why option 1:** it is the only one that leaves the Chrome seam's contract as it is, and it never asks
a composition to cross elements. Its residual costs are named above: the composition's look is the
core's for its duration, and the core hears of the edit only when it ends.

### Escape's release of Tab (ADR-0012 as rewritten in `b917cd1`, ticket 77), with the Keyboard Field

Not built here: the prototype is on `5c7ceaa`, where Escape with nothing to dismiss still blurs.
Ticket 77's Escape keeps the keyboard and leaves the next Tab or Shift+Tab to the browser. From the
Keyboard Field, the browser's own Tab is sequential navigation from the field's place in the markup,
not the root's. What follows is read from the order of the markup and not measured, since on this base
the core takes every Tab:

- **Tab** goes to the first tabbable element after the field. The field stands inside the root,
  after the header, before the rows and the Formula Bar. With one tab stop per grid (A11Y-4) there is
  none inside, so Tab leaves the grid as it would from the root. A Consumer's tabbable control in a
  Template cell would be reached from either.
- **Shift+Tab** goes to the previous tabbable element, and the root itself, with `tabindex="0"`, is
  the one right before the field. The root takes focus, the prototype hands it straight back to the
  field, and the user is trapped: the trap ticket 77 exists to avoid. So the release needs the field
  to be the grid's tab stop instead of the root: `tabindex="0"` on the field and `-1` on the root,
  which stays focusable by a press and by script. Shift+Tab then leaves the grid, and Tab into the
  grid lands on the field with no script. That moves ADR-0033's "one tab stop; `tabindex="0"` on the
  root" to the field, and A11Y-4's reading with it. Escape itself needs nothing more: the field keeps
  the keyboard, as the root would.

### Measured

**Layers 1 and 2, in full** (`nix develop -c dotnet test ExGrid.slnx`, macOS, at the prototype's
commit): ExGrid.Tests 829 passed; ExSheet.Engine.Tests 2135 passed; ExGrid.MudBlazor.Tests 91 passed;
ExSheet.Components.Tests 390 passed; ExGrid.Components 1210 passed, 1 skipped (ST-1 at a million rows,
opt-in as always), **2 failed**:

- `ShippedStylesheetTests.The_module_installs_only_the_listeners_the_allowlist_names` (ADR-0021).
  The module now installs `compositionstart`, a second `compositionend`, `focus` and `focusout`.
  **The prototype is what fails it, and the test is right.** It is the allowlist doing its job:
  these are uses ADR-0021 does not name.
- `ShippedStylesheetTests.The_listener_gates_the_coloured_text_on_its_text_and_sets_one_class`
  (ADR-0057). It asserts that `compositionend` is heard once, by the coloured text, only while an
  edit is open. The prototype adds a second listener, always on. **The prototype fails it, and the
  test is right**, for the same reason.

Three other source-shape tests failed on the first build and pass now, because the prototype was
fitted to the shapes they pin rather than the tests being changed: `focusEditor` stays in the handle
(ED-28's test reads it there), a composition's text is typed through `typeInto` rather than a fifth
`showCaret` call (ticket 75's count), and the field joins the Cell Editor's box rule ahead of the
selector list DC-48's test reads.

Six layer-2 tests are new (`KeyFieldPrototypeTests`), and pass: one field, the Viewport's first
child, in no row and not a tab stop; none on a display-only grid; the field in the Focus cell's box,
read-only over a cell that does not edit; a composition's text opening Overwrite holding it, with the
editor asked for the keyboard and an arrow committing it; a cancelled composition opening an empty
edit that Escape cancels; and nothing opening over a cell that does not edit.

**Layer 3: not run.** The user made the pull request the priority and asked for layers 1 and 2 only
on this machine. What was planned, headless, `--project=chrome`, on WebAssembly and on the Server
host, and then every failure again on `5c7ceaa` to tell the prototype's failures from a Mac's:

- `--grep "KB-|CP-|ED-26|ED-28|ED-11|ADR-0021|ADR-0018|ADR-0055|[Ff]ind|DC-47|IME|A11Y|ticket 79"`:
  150 tests in 18 files before A11Y was added (`circuit`, `declarations`, `edit-stands`,
  `excel-keys`, `features`, `find`, `gestures`, `harness`, `inspector-edits`, `key-field`, `memory`,
  `mud-app`, `pointing-scope`, `popovers`, `reference-text`, `sheet-vs-excel`, `sheets`, `stretch`).
- `key-field.spec.mjs`, the prototype's own: under both Chromes on `/sheet`, the keyboard in the field
  over the Focus cell, the arrows and plain typing as before; a composition made through the DevTools
  protocol (`Input.imeSetComposition`, `Input.insertText`, as DC-47 makes one) drawn over D10 with
  nothing moving, its end opening the Cell Editor holding it, Enter committing (i1); a cancelled
  composition leaving an empty edit (i2); a press on D12 while composing putting the text in D10 and
  selecting D12; two compositions at once behind 150 ms appended in order; Ctrl+C and Ctrl+V from the
  field. And on `/sheets` a field per Sheet (ADR-0018), the root's own focus passed to the field, a
  read-only field over `/features`' Book column, no field on `/wide`, and Enter twice straight after a
  composition behind 150 ms (Server only).

**What layer 3 is expected to show, read from the specs and not run.** About 45 assertions in nine
spec files expect a grid's root itself to hold DOM focus (`expect(grid).toBeFocused()` and the
like; `edit-stands` has 22, `declarations`, `sheet` and `sheets` most of the rest). On a grid with an
editable column the prototype puts DOM focus on the field inside the root, so each of those would
fail. Where the assertion means "the keyboard is this grid's, not the editor's or another grid's",
the prototype keeps the meaning and the test's wording would change with an ADR. That is a decision,
not a fix to make here. ED-26's DoD row says the same in its words ("`document.activeElement` is …
the root (committed)"). ED-11's own test dispatches its composing keydowns on the root, and is
expected to pass. Whatever else fails is unknown until it runs.

### What an ADR would have to say, if the user takes this way

- **ADR-0010** (the Cell Editor, and "A composing IME is left alone"): with no edit open, the
  keyboard on a grid that edits is held by the Keyboard Field over the Focus cell. A composition
  there is drawn there, and its end opens Overwrite holding its text, as a typed character does. The
  text is a held item among the keys. A press during a composition ends it first, and the text goes
  to the cell it was composed on. The editor's request for the keyboard waits while the field
  composes. And whether the edit should count as open from the composition's start, as Excel's does,
  which this prototype does not do.
- **ADR-0018 section 1**: which grid is active is answered by `root.contains(document.activeElement)`,
  not by the root holding focus; the listener stays on the root.
- **ADR-0021**: the listeners (`compositionstart`, `compositionend` always on, the root's `focus`,
  `focusout`), the two new decisions about focus made in script (the root's own focus passed to the
  field; the field blurred by a press during a composition), the third one refined, and the
  hidden-field trick ADR-0005 recorded as avoided, now carried for the IME.
- **ADR-0033**: what an assistive technology is given when DOM focus is on a text field: a label, a
  role, `aria-activedescendant` on the field, or another design. Not built; a decision of its own.
  And which element is the one tab stop. Ticket 77's release of Tab needs the field to be it (above).
- **CONTEXT.md**: the term, if it stays ("Keyboard Field" is a working name).
- **Definition of Done**: ED-11 gains a case on a selected cell. ED-26's and A11Y-4's readings,
  and every layer-3 assertion of the root holding focus, would read "the root or its Keyboard Field".

A real IME is a Windows run's. The procedure is proposed at
`docs/specs/exsheet/verify-on-windows-PROPOSED-ime-prototype.md`, unnumbered until the user reserves
a run.
