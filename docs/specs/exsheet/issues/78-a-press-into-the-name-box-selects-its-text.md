# 78: A press into the Name Box selects its text

Status: done

**What to build:** ADR-0051, the Name Box bullet, as decided with the user on 2026-10-01. The fifteenth
Windows run (i7) found ExSheet's press into the Name Box leaving the caret after `D10`, so what was
typed (a composition, `かな`) was appended: `D10かな`. Excel's press selected `D10`, and the composition
replaced it.

**Blocked by:** None.

- [x] A press into the Name Box selects its whole text, the built-in Chrome's and the MudBlazor
      Chrome's alike, so what is typed replaces it. A second press, or a drag, inside it places the
      caret or selects as the field does (only the press that gives it the keyboard selects all)
- [x] Nothing else of the Name Box changes: Enter still goes to the address, Escape still gives the
      keyboard back, and a Reject met by a press into it still takes the keyboard out of it
      (ADR-0021, 2026-10-01)
- [x] No listener is added, and no layout is read (ADR-0021): the selection is set where the field's
      focus is already handled
- [x] Layer 2 for the focus path; Layer 3 on `/sheet` under both Chromes, on both hosts: press the
      Name Box, type `B2`, Enter: the Focus is on B2

## Comments

2026-10-01, implemented on `agent/ps-78`.

- **Where it is done: `ex-grid.js` alone.** C# hears the Name Box's focus a round trip after the
  press on a circuit. A selection asked for from there would land after the first keys, which by then
  have gone in after `D10`. Only the browser sees the press and the keys in the order the user made
  them.
- **No focus is moved from script.** The first way tried took the press's default and focused the
  field by hand. That would be a fourth decision about focus made in script, and ADR-0021 records
  three, and `ShippedStylesheetTests.A_press_back_on_the_rows_brings_the_keyboard_back_in_the_existing_mousedown`
  counts the module's `.focus(` calls at 3. Instead the press keeps its default, which gives the field the keyboard and puts the caret
  where the press landed.
  - **The release selects.** The capture-phase `mousedown` notes a primary press on this grid's own
    Name Box that does not hold DOM focus. This is the built-in input, or a Chrome's control inside the
    core's `.ex-name-box` box, never a nested grid's. The `mouseup` that follows, if the field now holds
    DOM focus, selects its whole text. It also takes the release's default, which would put the caret
    back where the press landed.
  - **A press into the Name Box while it holds the keyboard is not noted.** It places the caret, and a
    drag selects, as the field does.
  - **A drag that starts on the press that gives the keyboard ends with the whole text selected**, by
    the same release. The ticket's "only the press that gives it the keyboard selects all" is read that
    way. Excel was not asked about such a drag.
- **A render can rename the Name Box after the press.** The browser then writes the new name over the
  selection and leaves the caret after it. Two cases:
  - **The commit the press makes.** While pointing, the Name Box names the pointed cell. The press
    commits, and the Name Box names the Focus again (ADR-0051): `=1+` and ↓ in F2 show `F3`, and the
    press leaves `F2`. On WebAssembly this can land before the release. On the Server host it lands a
    round trip later.
  - **A render a round trip behind the press on a circuit.** A press on F8 and then at once on the Name
    Box: the render naming F8 lands after the Name Box's selection. The existing late hand-back test in
    `declarations.spec.mjs` already notes this, and presses Ctrl+A before typing because of it.

  So the keyboard listener keeps a mark on the field from the selecting release until the first key,
  input or press after it. The first key typed in the field selects its whole text again before it goes
  on, so what is typed replaces the name shown. That includes an IME's first composing key: the
  selection is made before the listener lets IME keys through. An input that comes without a key (a
  paste from a menu, a drop) takes the mark off, so the next key types after it.
- **Nothing else changes.** The C# focus path is untouched: a press with no edit open renders nothing,
  one with an edit open commits and keeps the keyboard in the Name Box, and a Reject takes it back to
  the editor (ADR-0021, 2026-10-01). Enter still submits the Name Box's form, and Escape still hands
  the keyboard back. No listener is added. The module still reads no layout: `select()` is the
  field's own API, as Find's field uses it.
- **Tests.**
  - Layer 2, `ShippedStylesheetTests.ADR0051_the_press_that_gives_the_name_box_the_keyboard_selects_its_text`.
    It pins the press, the release, the first key, the input and the dispose clauses. It also checks:
    - `select()` is called in those two places only;
    - `.focus(` still appears 3 times;
    - no layout read anywhere in the module;
    - the first key's selection comes before the IME early return.

    It failed before the script changed. The allowlist count test is unchanged.
  - Layer 2, `PointModeTests.ADR0051_a_press_into_the_name_box_while_pointing_renames_it_to_the_focus`
    (the focus path). A press into the Name Box while pointing commits `=A2` and renames the Name Box
    from `A2` to `A1`. The core asks for no focus and calls no script: the renamed text is the first key's
    to select. This describes the existing behaviour, and it passed before the change.
  - Layers 1 and 2: 829 + 2135 + 91 + 1208 (1 skipped, as before) + 390, all passing.
  - Layer 3 has 3 new tests in `sheet.spec.mjs`, under both Chromes:
    - press D10, then the Name Box: its selection is 0 to 3. `B` gives `B`, `2` gives `B2`, and Enter
      puts the Focus on B2 with the keyboard on the grid. The next press into the Name Box gives it the
      keyboard again and selects 0 to 2. A further press, at the text's end while the box holds the
      keyboard, places the caret at 2 to 2. Escape hands the keyboard back.
    - `=1+` and ↓ in F2, then the Name Box: F2 commits and shows `1`, and the Name Box reads `F2` and keeps
      the keyboard. `B2` typed replaces it and Enter goes there. Then `=SUM(` in F3 and a press into the
      Name Box: the Reject sends the keyboard back to the editor, and `1)` with Enter gives `1`.
    - Server only, at a 150 ms round trip: F8 pressed, then the Name Box at once. After the renames have
      landed, `D4` typed replaces `F8`, and Enter goes there.

  Runs: headless, macOS, `--project=chrome`, private ports. Edge and the full run are CI's. Each run also
  included the existing Name Box tests: `sheet.spec.mjs` DC-11, `declarations.spec.mjs` "late
  hand-back" and `circuit.spec.mjs` "the Name Box, at 10 keys a second".
  - After the fix: WebAssembly 9 passed and 2 skipped (the Server-only tests). Server 11 passed.
  - With the fix set aside, on WebAssembly: all 4 new tests failed. The selection was 3 to 3 where 0 to 3
    was expected, and the Name Box read `F2B` where `B` was expected.
  - With only the first key's selection set aside, on the Server host: the rename test and the circuit
    test failed under both Chromes, with `F2B` and `F8D`. The selection at the release alone does not
    cover a render that lands after it.
- **Not checked here:** a real Japanese IME. The first composing key's selection is made in the
  keydown, before the IME's composition starts, and on Windows that keydown is `Process` (keyCode 229).
  This is read from the code, not measured. The i7 case belongs in the next Windows run, beside Excel.
- **For ADR-0021's notes, not changed here:** the `mousedown`, `mouseup` and `keydown` now also select
  the Name Box's text for the press that gives it the keyboard. `ex-grid.js`'s header lists this beside
  the other uses. ADR-0021's notes list each thing the `mousedown` and `mouseup` do, and they do not list
  this yet.
