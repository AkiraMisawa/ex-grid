# 79: A Cell State's colour outranks a tone's

Status: ready-for-agent

**What to build:** a fix found by ticket 76. `ex-grid.css` declares the tone rules (`.ex-cell.ex-tone-positive`,
`.ex-cell.ex-tone-negative`) **after** the Cell State block, at the same specificity. Their own comment says they are
declared before it, "so a state the Consumer named outranks a tone its rule derived".
- So a theme that sets `--ex-tone-negative-color` paints a Stale or Error cell in the tone's colour, not the
  state's.
- The italic of Stale and the wavy underline of Error survive, but the colour is gone.
- ADR-0006 says the state must never be the one that disappears.

**Blocked by:** None (can start immediately)

- [ ] **The order.** A Stale, Error or Modified cell that is also toned paints in the state's colour, under every
      theme token. Forced-colours mode keeps its own rules.
- [ ] **Layer 2.** Check the stylesheet's order, as `ShippedStylesheetTests` checks other orders. The test must fail
      on today's stylesheet.
- [ ] **Layer 3.** A toned Error cell under a theme that sets the tone tokens paints in the error colour. Name the
      test after ADR-0006 and ADR-0029.
