# 29: The References coloured in the editor's text

Status: ready-for-agent

**What to build:** ADR-0057, "The coloured text is a layer that shows only while it is up to date".
Beneath each editor surface, a layer draws the text with its References coloured. The field's own
text turns transparent only while the layer holds the field's current value.

**Blocked by:** Ticket 28 (the spans and their colours). `claude/exsheet-edit-stands` is changing
`ex-grid.js`'s editor listener, and whichever lands second takes the other's change.

- [ ] The layer: the same text, font, size, padding and letter spacing as the field, `white-space:
      pre`, each span in its colour. The core supplies it, and each Chrome places it behind its
      field: the Cell Editor and the Formula Bar, built-in and MudBlazor (DC-47, DC-48)
- [ ] The gate: on each input, and when the layer's text attribute changes (a `MutationObserver`
      on that one attribute), the listener compares the layer's text with the field's value and sets
      or clears one class. The field's text is transparent only under that class; the caret keeps its
      colour (DC-47, DC-51)
- [ ] An IME composition leaves the field ahead, so its own text shows until the composition ends
      and the layer catches up (DC-47)
- [ ] Selected text in the field stays readable while the layer shows (DC-47)
- [ ] The layer's `scrollLeft` follows the field's (DC-48, DC-51)
- [ ] No layout is read; the script-shape tests say so (DC-51)
- [ ] Layer 3: on the Server host with 150 ms injected, a burst of typing sampled every animation
      frame never shows transparent field text over a layer that differs; on WebAssembly the colours
      follow each keystroke; a Formula longer than the field keeps its colours over the right
      characters at either end (DC-47, DC-48)
