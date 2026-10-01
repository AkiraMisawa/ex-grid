# 96: A number filter operand reads back as itself in every culture

Status: ready-for-agent

**What to build:** a quiet fault ticket 94 found (ADR-0006's note of 2026-10-01; filter panel, ADR-0009 and
ADR-0023). The condition form reopens a number operand in the current culture's text. Under de-DE, `1234.5` reopens
as `1234,5`. `ParseOperand` reads invariant first, with thousands separators allowed, so OK turns it into 12345. That
is quietly wrong (principle 1).

**Blocked by:** None (can start immediately)

- [ ] **An operand reopens in a text that reads back to the same value, in every culture.**
- [ ] **A number the user types reads as the user meant it.** Read it in the culture the form shows numbers in, and
      never silently in another culture's separators. A text that reads two ways (`1.234` under de-DE) is refused by
      name, not guessed.
- [ ] **Both Chromes' panels use the same reading.** `FilterPanelChoices`, and the MudBlazor panel's `CanApply`.
- [ ] **Layers 1 and 2** under en-US, de-DE, fr-FR and ja-JP, named after ADR-0023 and principle 1.
