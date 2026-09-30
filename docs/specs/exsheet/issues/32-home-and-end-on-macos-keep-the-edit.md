# 32: Home and End on macOS move the caret and keep the edit

Status: ready-for-agent

**What to build:** ADR-0010's note of 2026-09-30. In Caret, Home and End belong to the editor and
move the caret (ADR-0010's key table). Chrome on macOS binds them to scrolling the document instead.
On a real Mac, End in Caret therefore scrolled the grid to its last row, the edited cell left the
painted rows, the Cell Editor unmounted, and DOM focus fell to `body` with the user's text in it.
The agent building ticket 29 found it (the DC-48 layer-3 test timed out on macOS for this reason).

**Blocked by:** None.

- [ ] On Apple platforms, with an edit open in Caret in the Cell Editor or the Formula Bar, the
      capture-phase listener claims Home, End, Shift+Home and Shift+End. It places or extends the
      caret to the start or end of the text, as the browser does on Windows and Linux (ADR-0010,
      ADR-0021)
- [ ] Any other key macOS binds to a scroll inside a text field (check Playwright's macOS
      key-binding table: PageUp, PageDown and the rest) is handled so that nothing scrolls the grid
      away from an open edit. Its meaning stays what it is on Windows and Linux
- [ ] Overwrite is unchanged: Home and End there are the core's, commit and move (ADR-0010, ADR-0012)
- [ ] Windows and Linux are unchanged: the listener claims these keys only on Apple platforms
- [ ] Layer 3 on macOS: F2 on a cell with long text, End then Home, and the same in the Formula
      Bar. The caret moves to each end, the scroller does not move, and the edit stays open with DOM
      focus in its field. The test also passes on Linux, where the browser does it
- [ ] The script-shape tests pin the claimed keys; no layout is read (DC-24)

## Comments
