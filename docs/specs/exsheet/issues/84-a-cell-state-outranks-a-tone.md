# 84: A Cell State's colour outranks a tone's

Status: done

**What to build:** a fix found by ticket 81. `ex-grid.css` declares the tone rules (`.ex-cell.ex-tone-positive`,
`.ex-cell.ex-tone-negative`) **after** the Cell State block, at the same specificity. Their own comment says they are
declared before it, "so a state the Consumer named outranks a tone its rule derived".
- So a theme that sets `--ex-tone-negative-color` paints a Stale or Error cell in the tone's colour, not the
  state's.
- The italic of Stale and the wavy underline of Error survive, but the colour is gone.
- ADR-0006 says the state must never be the one that disappears.

**Blocked by:** None (can start immediately)

- [x] **The order.** A Stale, Error or Modified cell that is also toned paints in the state's colour, under every
      theme token. Forced-colours mode keeps its own rules.
- [x] **Layer 2.** Check the stylesheet's order, as `ShippedStylesheetTests` checks other orders. The test must fail
      on today's stylesheet.
- [x] **Layer 3.** A toned Error cell under a theme that sets the tone tokens paints in the error colour. Name the
      test after ADR-0006 and ADR-0029.

## Comments

2026-10-01, agent cf-84.

### What changed

- **The Tone block moved** in `src/ExGrid/wwwroot/ex-grid.css`, from near the end of the file to just
  before the Cell State block, where its comment already said it was. Both are written as
  `.ex-cell.ex-x`, so the order decides, and it no longer depends on any token: a Stale or Error cell
  paints its state's colour whatever colours a theme gives the two. The Cell State comment now names
  the Tone block beside Row Kind.
- **Modified and Missing were never at stake.** A tone paints `color` only. Modified's mark is a
  `box-shadow`, and Missing's tint is a background layer. A toned Modified cell paints its text in the
  tone's colour, with its mark, and a toned Missing cell paints it over its ground.
- **Forced colours are untouched.** That block comes last in the file and restates every state in a
  system colour.
- **A Font still outranks a tone and gives way to Stale and Error.** Its generated rule is
  `.ex-cell:not(.ex-state-stale, .ex-state-error).ex-font-x`, which is more specific than both.
- **A new demo page, `/tones`,** has a theme on the element around its grid that paints a gain green
  and a loss orange. The page has a gain, a loss and a flat value, which has no tone, in one column
  per Cell State.
- **`CellToneTests.A_tone_composes_with_the_other_vocabularies` said "the state is last, so it
  outranks".** It meant the class attribute's order, which plays no part in the cascade. The
  comment now points at the stylesheet's order and the test that checks it.

### Tests

- **Layer 2:** `ShippedStylesheetTests.A_cell_state_outranks_a_tone` (ADR-0006 / ADR-0029). It reads
  the shipped rules outside any at-rule. On every property a tone rule paints, it checks that each
  state rule painting the same property outranks it: more specific, or as specific and declared
  later. Specificity is AngleSharp's own, through its public selector parser. It also pins which
  pairs are contested: Stale and Error against both tones, on `color`.
  - On the stylesheet before this change it failed:
    `.ex-cell.ex-tone-positive (rule 152) outranks .ex-cell.ex-state-stale (rule 39) on color`.
- **Layer 3:** `presentation.spec.mjs`, "a toned Stale or Error cell paints in its state's colour
  under a theme that sets the tone tokens (ADR-0006, ADR-0029)", on `/tones`.
  - The theme is in effect: a plain gain and loss paint the tone colours.
  - For Stale and Error, a toned gain and a toned loss resolve to the colour the flat value of that
    state resolves to. Their most inked pixels lie on that colour's line over the ground, not on the
    tone's.
  - Stale stays italic, and Error keeps its wavy underline.
  - A Modified gain, loss and flat value paint the same corner mark.
- **Shown to fail by a local run** on 004a029's stylesheet: macOS, headless Chrome, the WebAssembly
  host, port 5451. "A stale gain, as the cascade resolved it" received 27,127,59, the theme's green,
  where Stale's colour is 138,109,31. With the fix in place it has not run locally: under the new
  process, CI on PR #42 runs layer 3, on Linux, for both browsers and both hosts. The pixel half of
  the test is therefore CI's to confirm.
- **Layers 1 and 2:** 871 + 2304 + 88 + 1257 (1 skipped) + 35 + 539 passed, with both commits in place.

### P1–P9

The change moves two rules. No markup, no C#, nothing reaching JavaScript, and no layer is added
anywhere.
