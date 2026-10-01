# 86: ADR-0057's coloured layer lies over the field's text under the Wrapper's font

Status: ready-for-agent

**What to build:** the fix ticket 48 found and decided (its comment, "DC-48 under ExSheet.MudBlazor, at End").
- Under `ExSheet.MudBlazor`'s Chrome, with Roboto, the coloured layer of
  [ADR-0057](../../../adr/0057-references-are-outlined-in-colour-while-a-formula-is-edited.md) drifts from
  the Cell Editor's own text at the far end of a long Formula: 112 pixels differ at End.
- MudBlazor's grey text hid it. Ticket 48 draws the editor in the Ink, and 2 pixels now cross DC-48's
  threshold, so `reference-text.spec.mjs` › "DC-48 … (mud Chrome)" fails at End.
- The built-in Chrome shows 88 differing pixels, none past the threshold.

**Blocked by:** None (can start immediately)

- [ ] **Find where the layer and the field part.** The test's comment names per-span snapping. Check
      the font's metrics, `letter-spacing`, `font-kerning`, `text-rendering` and `font-feature-settings`
      on the layer against the field, under the Wrapper.
- [ ] **Make the layer draw where the field draws**, under both Chromes. Add no script beyond ADR-0057's
      allowance (ADR-0021).
- [ ] **DC-48 passes at its threshold under both Chromes**, at both ends, on both hosts. The threshold
      is not loosened.
- [ ] **Say in the comment** how many pixels still differ under each Chrome.
