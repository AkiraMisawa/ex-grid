# 39: Argument value lists

Status: ready-for-agent

**What to build:** ADR-0058, "Completion, aligned with Excel", and ADR-0051's note of 2026-09-30.
At an argument whose values are a fixed list, completion lists the values, as Excel was seen to
(`verification/2026-09-27-windows-excel/behaviours.md`, item 14).

**Blocked by:** None (can start immediately)

- [ ] The engine's function declarations can say that an argument takes one of a fixed list of
      values, each with Excel's text (`FunctionDefinition` holds the arguments as one display string
      today) (SH-36)
- [ ] `XLOOKUP`'s `match_mode`, with the texts observed: `0 - Exact match`, `-1 - Exact match or next
      smaller item`, `1 - Exact match or next larger item`, `2 - Wildcard character match`,
      `3 - Regex match` (SH-36)
- [ ] `XLOOKUP`'s `search_mode`, with Excel's documented texts, marked as a reading in the code and the
      test until the ninth Windows run observes them (SH-36)
- [ ] `FormulaEntry.Complete` offers the list when the caret stands at such an argument, with nothing
      typed or with a prefix of a value. Tab writes the value (its number), not the text (SH-36)
- [ ] Nothing is listed after `=`, an operator, `(` or `,` at any other argument, so ↓ still points
      (SH-36, ADR-0051)
- [ ] Layer 1 over the engine; Layer 2 for ↑/↓, Tab and Escape (SH-36)
