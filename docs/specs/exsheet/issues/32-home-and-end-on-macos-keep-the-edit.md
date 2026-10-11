# 32: Home and End on macOS move the caret and keep the edit

Status: done

**What to build:** ADR-0010's note of 2026-09-30. In Caret, Home and End belong to the editor and
move the caret (ADR-0010's key table). Chrome on macOS binds them to scrolling the document instead.
On a real Mac, End in Caret therefore scrolled the grid to its last row, the edited cell left the
painted rows, the Cell Editor unmounted, and DOM focus fell to `body` with the user's text in it.
The agent building ticket 29 found it (the DC-48 layer-3 test timed out on macOS for this reason).

**Blocked by:** None.

- [x] On Apple platforms, with an edit open in Caret in the Cell Editor or the Formula Bar, the
      capture-phase listener claims Home, End, Shift+Home and Shift+End. It places or extends the
      caret to the start or end of the text, as the browser does on Windows and Linux (ADR-0010,
      ADR-0021)
- [x] Any other key macOS binds to a scroll inside a text field (check Playwright's macOS
      key-binding table: PageUp, PageDown and the rest) is handled so that nothing scrolls the grid
      away from an open edit. Its meaning stays what it is on Windows and Linux
- [x] Overwrite is unchanged: Home and End there are the core's, commit and move (ADR-0010, ADR-0012)
- [x] Windows and Linux are unchanged: the listener claims these keys only on Apple platforms
- [x] Layer 3 on macOS: F2 on a cell with long text, End then Home, and the same in the Formula
      Bar. The caret moves to each end, the scroller does not move, and the edit stays open with DOM
      focus in its field. The test also passes on Linux, where the browser does it
- [x] The script-shape tests pin the claimed keys; no layout is read (DC-24)

## Comments

*(2026-09-30, built with ticket 29's fixes.)* Pressed one by one in Caret on `/sheet` at F200 on
macOS, only Home, End, PageUp and PageDown moved the grid; in the Cell Editor, Home, End and PageUp
also lost the edit. Control+↑ and Control+↓, which Playwright's macOS table binds to a page scroll,
did not scroll. The listener places the caret for Home and End (and Shift with them) in Caret. It
drops PageUp and PageDown whenever they would reach an editor field on a Mac, in Overwrite and Point
too, since there they would scroll the edit away as well. `setSelectionRange` does not scroll the
field, so the listener sets the field's `scrollLeft` with it (ADR-0021's scroll-offset entry).
Whether Windows and Linux need the same for PageUp and PageDown is put to the user.

*(2026-09-30, from CI.)* On Linux, on the Server host, End pressed straight after a press into the
Formula Bar was held until the press was answered, and was then replayed. The replay set the caret
at the end but moved no view, and dropped Shift. Held Home and End now replay through the same
placement as the Apple path, on every platform.

*(2026-09-30, decided with the user.)* PageUp and PageDown are dropped while an edit is open on
every platform, not only on a Mac. The layer-3 test asserts it everywhere.

*(2026-10-10, backlog cleanup.)* Status set to done. Every box was ticked, and `sheet.spec.mjs`,
"ticket 32/ADR-0010: in Caret in the {Cell Editor|Formula Bar}, Home and End move the caret, with
Shift extend the selection, and nothing scrolls the grid away", runs in CI on both hosts, PageUp and
PageDown included. The question put to the user above was decided with the user on 2026-09-30, in
the last comment before this one.
