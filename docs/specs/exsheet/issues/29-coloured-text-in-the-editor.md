# 29: The References coloured in the editor's text

Status: done — but for a real IME, which a Windows run checks

**What to build:** ADR-0057, "The coloured text is a layer that shows only while it is up to date".
Beneath each editor surface, a layer draws the text with its References coloured. The field's own
text turns transparent only while the layer holds the field's current value.

**Blocked by:** Ticket 28 (the spans and their colours). `claude/exsheet-edit-stands` is changing
`ex-grid.js`'s editor listener, and whichever lands second takes the other's change.

- [x] The layer: the same text, font, size, padding and letter spacing as the field, `white-space:
      pre`, each span in its colour. The core supplies it, and each Chrome places it behind its
      field: the Cell Editor and the Formula Bar, built-in and MudBlazor (DC-47, DC-48)
- [x] The gate: on each input, and when the layer's text attribute changes (a `MutationObserver`
      on that one attribute), the listener compares the layer's text with the field's value and sets
      or clears one class. The field's text is transparent only under that class; the caret keeps its
      colour (DC-47, DC-51)
- [x] Only the surface the edit is in shows the colours; the other keeps its plain text, and the
      colours follow a press from one surface into the other mid-edit. Excel colours the cell's
      text or the Formula Bar's, never both (the eighth Windows run, `range-finder.md` cases 1, 21
      and 1fb). The surface is the field holding DOM focus, the one held keys go to (ADR-0051)
- [x] The Reference Point is writing is shown selected: its span in the layer lies on a grey ground
      (`--ex-reference-pointed-background`), its text a darker shade of its colour
      (`--ex-reference-1-pointed` to `--ex-reference-7-pointed`, one per place in the palette since
      ticket 43; built here as one `--ex-reference-pointed-color` for all seven, retired
      2026-09-30, ADR-0029), unless the span starts right after the text's first
      character (`=` ↓ ↓ shows none; `=SUM(` ↓, `=1+` ↓ and `=D11+` ↓ ↓ do). A look on the layer,
      never a selection of the field's text: `5` typed next follows the Reference and ends Point
      (ADR-0051's newest section; ADR-0057, "What cases 24–32 settled")
- [x] An IME composition leaves the field ahead, so its own text shows until the composition ends
      and the layer catches up (DC-47). *Every input of a composition, its last included, carries
      `isComposing`, and Chrome sends no input after `compositionend`. So the listener also hears
      `compositionend` (capture phase, on the root, only while an edit is open, as the field's
      `scroll` is), clears the composing mark and compares again: the colours come back when the
      composition ends, with no keystroke after it (decided with the user, 2026-09-30). Layer 3
      passes on both hosts, through CDP; a real IME is still a run by hand.*
- [x] Selected text in the field stays readable while the layer shows (DC-47)
- [x] The layer's `scrollLeft` follows the field's (DC-48, DC-51)
- [x] No layout is read; the script-shape tests say so (DC-51)
- [x] Layer 3: on the Server host with 150 ms injected, a burst of typing sampled every animation
      frame never shows transparent field text over a layer that differs; on WebAssembly the colours
      follow each keystroke; a Formula longer than the field keeps its colours over the right
      characters at either end (DC-47, DC-48). *In `tests/ExGrid.Browser/reference-text.spec.mjs`.
      Passed 2026-09-30 on macOS, headless Chrome, both hosts, both Chromes, once three causes of
      DC-48 were fixed: a word the spelling check marks was drawn a second time over the layer; End
      on macOS scrolled the grid away (ticket 32); and `/sheet?chrome=mud` lacked the Wrapper's
      shape (ADR-0057, "The Wrapper's shape is required…"). Linux and Edge are CI's.*

## Comments

*(2026-10-10, backlog cleanup.)* Status set to done: every box is built, and
`reference-text.spec.mjs` runs on both hosts in CI, the composition through CDP included. A real IME
is still a run by hand. One was seen hiding the layer while composing (ADR-0057), and no record
shows the colours coming back when a real composition ends.
