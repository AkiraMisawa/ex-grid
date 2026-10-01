# Glyph widths: what the per-class estimate has to cover

ADR-0016 decides `####` from an estimate, never a measurement: each character is charged the width
of its class (`CellTextMetrics`). The estimate is only safe if no glyph a Number Format emits is
wider than its class's width, at every weight the grid paints (ADR-0016's notes of 2026-10-01;
tickets 82 and 83). This folder holds the evidence and the tool that makes it.

| File | What it is |
|---|---|
| `corpus.json` | The 3,015 strings ExSheet's built-in formats paint: Number and Currency in each negative style at 0 and 2 places, Percentage, Scientific, General, each culture's built-in currency, and the Date and Time types, under 24 cultures, for amounts, fractions, every month and times either side of noon |
| `measure.mjs` | Measures one face in Chrome: every corpus string, and every glyph the corpus holds, the Latin letters and every glyph `CellTextMetrics` names in a class, at 14px and 12px, weights 400, 500, 600 and 700, tabular digits |
| `<face>.<platform>.json` | One face as `measure.mjs` measured it, on one platform |
| `GlyphWidthRecord.cs` | Reads a record; compiled into the test projects below |

## What reads them

- **ExSheet.Components, `GlyphCorpusTests`**: the corpus is still what the built-in formats paint.
  A format that changes its output fails it, and the corpus it makes is written beside the test
  binary as `corpus.generated.json`.
- **ExGrid.Tests, `GlyphWidthCorpusTests`**: under the core's defaults, at every density, no corpus
  string paints wider than its estimate and no glyph wider than its class, in `system-ui.*` and
  `dejavu-sans.*`. Weights 400 to 600 are held to the regular widths, 700 to the bold ones.
- **ExGrid.MudBlazor.Tests, `RobotoGlyphWidthCorpusTests`**: the same, under the widths
  `MudExGridPresentation` cascades, in `roboto.*`.

A record measured from another corpus is refused (`corpusSha256`).

## Measuring again

When a font changes, Chrome changes how it lays text out, a built-in format changes, or a platform
has not been measured yet:

1. If `GlyphCorpusTests` failed, copy the `corpus.generated.json` it names over `corpus.json`.
2. Measure each face. Chrome is found where it installs itself; `--chrome <path>` names another.

   ```sh
   node tests/GlyphWidths/measure.mjs roboto
   node tests/GlyphWidths/measure.mjs system-ui
   node tests/GlyphWidths/measure.mjs dejavu-sans \
     --dejavu "$(nix build --no-link --print-out-paths nixpkgs#dejavu_fonts)/share/fonts/truetype"
   ```

   Each writes `<face>.<platform>.json` here. `system-ui` is whatever the platform resolves it to,
   so a run on Linux or Windows adds a record of its own, and the core's tests read it too.
3. Run layer 1. A failure names each string or glyph charged under its width, the face that painted
   it, and the size and weight. Raise the class width that charges it — the preset table in
   `GridMetrics.Resolve`, or `MudExGridPresentation`'s constants — to a shade over the widest
   reading, at both sizes (`MudExGridPresentation`'s widths are scaled from 14px to 12px).

## How a width is read

- Headless Chrome at a device scale factor of 1, from a page that sets the face the grid sets:
  `ex-grid.css`'s `system-ui, sans-serif`, `MudExGridFont.Roboto.Family` over the files the demo
  pages serve, and DejaVu Sans Book and Bold as web fonts (Linux has no 600, so 600 matches Bold
  there and here).
- A glyph's width is the wider of the glyph alone and a hundred in a row, divided by a hundred: a
  glyph that kerns with itself reads short in a run (Roboto's `//`), and a glyph alone is laid out
  to the next 1/64px. A string's width is the string as one run.
- Every width is in 64ths of a pixel, rounded up, so no reading is under what was painted.
- `painters` names the face that painted each glyph. A glyph the family does not draw is painted
  by the platform's fallback: on macOS, Thai in every face (Thonburi), Hebrew in Roboto (Arial),
  `₴` and `฿` in Roboto (Helvetica Neue), `₼` in DejaVu Sans (Kefa), and the ideographs and
  Hangul in every face (PingFang, Apple SD Gothic Neo).

## What stays out

- **Glyphs only a Custom format emits**, such as quoted text or `‰`. The other class charges them
  the widest glyph measured, so they are covered as far as they are no wider than it, but they
  are not measured. `‰` is 20.17px in DejaVu Sans Bold (ticket 82), past every class.
- **Cultures outside the 24.** Their month names and currency signs are charged the other class
  too, and are covered as far as the widest glyph measured covers them.
- **Fallback faces on another platform.** A glyph the face does not draw is measured in the
  fallback this platform chose. Linux and Windows choose others.
- **Windows' `system-ui`**, Segoe UI, until a record from Windows is added.
- **A Consumer's own font.** Whoever sets `--ex-font-family` owes the widths (ADR-0027's
  metrics-bearing obligation, ADR-0030). This tool can measure it: add a face to `measure.mjs`
  and a test that holds the Consumer's widths to its record.
