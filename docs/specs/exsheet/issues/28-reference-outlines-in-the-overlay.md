# 28: Reference Outlines in the selection overlay

Status: ready-for-agent

**What to build:** the core's half of ADR-0057: the References function as a declaration, the
colours, and the outlines. While a Formula edit is open, every Reference the Consumer answers is
outlined in its colour. Point's outline takes the colour of the Reference it writes.

**Blocked by:** None (can start immediately). `claude/exsheet-selection-look` is changing how a
range is drawn in the overlay (drawn whole in both layers and clipped to each side, the outline
inside the range, forced colours). Whichever lands second takes the other's way of drawing. A
Reference Outline is drawn as a range's outline is drawn.

- [x] A Consumer declaration: a synchronous function over the editor's text, answering each
      Reference's span and either the cells it names or a key. Off by default; a plain ExGrid is
      unchanged (DC-1, DC-46)
- [x] The colours: the same cells or the same key share one, handed out in order of first
      appearance, round a palette of eight. The palette's length is a C# constant; its colours are
      `--ex-reference-1` to `--ex-reference-8` in the stylesheet (DC-46, UX-1)
- [x] Asked from the text each keystroke carries, in every editing state, in the Cell Editor and in
      the Formula Bar; removed on commit and on cancel (DC-46)
- [x] One element per outlined range in the selection overlay, in both layers across the pinned
      boundary, cut to the painted rows; nothing per cell, and no scroll to show a Reference (DC-46)
- [x] Point's outline takes the colour of the Reference it is writing, dashed; every other outline
      is solid over a pale wash, as the readings say (DC-46)
- [x] The core tells the Consumer the colour each key was given, when that changes, and an empty
      set when the edit ends (DC-49)
- [x] The classes and tokens enter ADR-0029's list as that ADR's ADR-0057 section says; the
      MudBlazor Wrapper leaves the tokens alone
- [ ] ExSheet declares the function from ticket 27, so `/sheet` shows the outlines (DC-46)
- [ ] Layer 3 on both hosts: `=A1+B2:C3` outlines A1 and B2:C3 in two colours; `=A1+A1` outlines
      A1 once; `=`, ↓, ↓ moves a dashed outline in the first colour; Enter removes them all (DC-46)
