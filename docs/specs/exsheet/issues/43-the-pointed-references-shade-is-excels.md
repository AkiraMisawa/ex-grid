# 43: The pointed Reference's shade is Excel's

Status: ready-for-agent

**What to build:** ADR-0057, "What Part B of the eighth Windows run settled", and ADR-0029's change
of the same day. The Reference Point is writing wears a dark shade of its colour on the grey ground.
The shade was approximated (55% toward black); Excel's are `#0401a2` for the first colour and
`#630101` for the second.

**Blocked by:** None (can start immediately)

- [x] One Visual Token per place in the palette, `--ex-reference-1-pointed` to
      `--ex-reference-7-pointed`, read by `.ex-reference-pointed` for the span's colour
      (`src/ExGrid/wwwroot/ex-grid.css`, the rule around `.ex-reference-text .ex-reference-pointed`)
      (DC-56)
- [x] Defaults: `#0401a2` and `#630101` for the first two in light mode; the other five, and all
      seven in dark mode, keep today's mix (toward black in light, toward white in dark) (DC-56)
- [x] `--ex-reference-pointed-color` is retired: the stylesheet no longer reads it, and its comment
      and ticket 29's mention are updated (ADR-0029)
- [x] `--ex-reference-pointed-background` is unchanged
- [x] The shipped-stylesheet tests cover the seven tokens and the two Excel defaults (DC-56)
- [ ] Layer 3 under both Chromes: `=D11+`, ↓↓ in the cell gives the pointed text `#0401a2` on
      `#c6c6c6`, and a second Reference pointed gives `#630101` (DC-56). Write it; the orchestrator runs
      it. *Written, not run*
- [x] The next Windows run asks Excel for the other five shades (`verify-on-windows-10.md`, group
      2, on `claude/exsheet-pointing-scope`): `#44007c`, `#003600`, `#550059`, `#531c00`, `#00323f`
- [x] The other five light defaults are Excel's, as the tenth run read them; dark mode keeps the mix
      toward white (Excel's Black theme keeps its cells white) (DC-56)

## Comments

*(2026-09-30, built.)*

- **The stylesheet.** `.ex-reference-text .ex-reference-pointed` keeps only the grey ground,
  `--ex-reference-pointed-background`, unchanged. Seven rules,
  `.ex-reference-text .ex-reference-N.ex-reference-pointed`, set the text's fill from
  `--ex-reference-N-pointed`. The pointed span already carries its colour's class, so no markup
  changed. The defaults are `light-dark(#0401a2, …)` and `light-dark(#630101, …)` for the first two.
  The other five keep the mix 55% toward black. All seven mix 55% toward white in dark mode.
  `currentColor` still reads the span's own colour, so a Theme's `--ex-reference-N` moves the
  approximated shades with it. Nothing reads `--ex-reference-pointed-color` any more. The
  stylesheet's comment and ticket 29 now name the seven tokens.
- **Layer 2**:
  `ShippedStylesheetTests.DC56_the_pointed_shade_is_one_token_per_place_in_the_palette` checks the
  rules and defaults for each of `ReferenceColour.PaletteLength` places. It checks there is none
  past the palette's end, that the ground's rule is unchanged, and that no shipped asset names the
  retired token. It fails on 4179010.
- **Layer 3, written, not run**: `DC-56: the pointed Reference's text is Excel's shade of its colour
  …` in `tests/ExGrid.Browser/reference-text.spec.mjs`, under the built-in Chrome and
  ExGrid.MudBlazor's. From D10, as the run typed Excel's cases 20 and 20x: `=D11+`, then ↓ points
  D11 in the first colour, with text `rgb(4, 1, 162)` on `rgb(198, 198, 198)`. ↓ again points D12
  in the second colour, `rgb(99, 1, 1)`. Then `=SUM(`, ↓, ↓ gives the first colour's shade.
- **Not done here**: asking Excel for the other five shades is in `verify-on-windows-10.md`, group
  2, on `claude/exsheet-pointing-scope`, not on this branch.

2026-09-30, after the tenth Windows run: the other five shades were observed and are now the light
defaults of `--ex-reference-3-pointed` to `--ex-reference-7-pointed`. The shipped-stylesheet test
pins all seven.
