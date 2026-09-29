# 23: F4 cycles the Reference at the caret

Status: ready-for-agent

**What to build:** ADR-0051, "F4 cycles the Reference at the caret" (2026-09-29), with ADR-0021's
note of the same day. While an edit is open, F4 cycles the Reference at the caret: `=B2`, F4, gives
`=$B$2`, then `=B$2`, `=$B2`, `=B2`. The core claims the key and asks the Consumer; ExSheet rewrites
the text from its parser.

**Blocked by:** None (can start immediately)

Core (ExGrid):

- [ ] A Consumer declaration: a synchronous function over the editor's text and its selection
      (start, end) that answers the new text and the new selection, or nothing. Off by default; a
      plain ExGrid is unchanged (DC-1, DC-45)
- [ ] Declared, the capture-phase listener claims F4 only while an edit is open, in the Cell Editor
      or the Formula Bar, and sends the text and selection with the key message; with no edit open,
      F4 is not claimed (DC-45)
- [ ] The answer is written to the editor surface the key came from, and to the other surface as any
      edit is, and its selection is placed by the listener that already places the caret (DC-45)
- [ ] While pointing, F4 rewrites the Reference the outline wrote, and pointing goes on; a further
      move writes the next Reference as pointing writes it (ADR-0051 reading) (DC-45)
- [ ] A burst of F4 presses on the Server host with 150 ms injected gives the four forms in order:
      each press is decided from the text its own key message carries (DC-45)
- [ ] No JavaScript use is added; the listener stays inside ADR-0021's keyboard/editor use (DC-24)

ExSheet:

- [ ] The rewrite, pure and tested in layer 1 against ADR-0051's readings: the Reference the caret is
      inside or touching; a range as one, from its first end; every Reference in a selection; whole
      columns and rows in two forms; the Sheet qualifier kept; structured references, function
      names, numbers and non-Formula text unchanged; the caret at the end of the rewritten Reference,
      or the selection over the rewritten span (SH-28)
- [ ] ExSheet declares the function, so `/sheet` in the DemoHost cycles on F4 (SH-28)
- [ ] Layer 3: `=B2` and four F4 presses in a cell and in the Formula Bar, on both hosts (DC-45)

## Comments

The readings are asked of Excel in [verify-on-windows-6.md](../verify-on-windows-6.md), Part A. A
reading Excel contradicts is a defect of the reading, fixed after the run with the ADR paragraph.
