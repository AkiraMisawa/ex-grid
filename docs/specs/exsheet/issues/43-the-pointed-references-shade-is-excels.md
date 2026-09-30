# 43: The pointed Reference's shade is Excel's

Status: ready-for-agent

**What to build:** ADR-0057, "What Part B of the eighth Windows run settled", and ADR-0029's change
of the same day. The Reference Point is writing wears a dark shade of its colour on the grey ground.
The shade was approximated (55% toward black); Excel's are `#0401a2` for the first colour and
`#630101` for the second.

**Blocked by:** None (can start immediately)

- [ ] One Visual Token per place in the palette, `--ex-reference-1-pointed` to
      `--ex-reference-7-pointed`, read by `.ex-reference-pointed` for the span's colour
      (`src/ExGrid/wwwroot/ex-grid.css`, the rule around `.ex-reference-text .ex-reference-pointed`)
      (DC-56)
- [ ] Defaults: `#0401a2` and `#630101` for the first two in light mode; the other five, and all
      seven in dark mode, keep today's mix (toward black in light, toward white in dark) (DC-56)
- [ ] `--ex-reference-pointed-color` is retired: the stylesheet no longer reads it, and its comment
      and ticket 29's mention are updated (ADR-0029)
- [ ] `--ex-reference-pointed-background` is unchanged
- [ ] The shipped-stylesheet tests cover the seven tokens and the two Excel defaults (DC-56)
- [ ] Layer 3 under both Chromes: `=D11+`, ↓↓ in the cell gives the pointed text `#0401a2` on
      `#c6c6c6`, and a second Reference pointed gives `#630101` (DC-56). Write it; the orchestrator runs
      it
- [ ] The next Windows run asks Excel for the other five shades (`verify-on-windows-9.md` Part B, on
      `claude/exsheet-pointing-scope`, when the branches meet)
