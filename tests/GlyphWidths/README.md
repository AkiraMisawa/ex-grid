# Glyph widths: what the per-class estimate has to cover

ADR-0016 decides `####` from an estimate, never a measurement: each character is charged the width
of its class (`CellTextMetrics`). The estimate is only safe if no glyph a Number Format emits is
wider than its class's width, at every weight the grid paints (ADR-0016's notes of 2026-10-01;
tickets 82 and 83). This folder holds the evidence and the tool that makes it.

| File | What it is |
|---|---|
| `corpus.json` | The 3,015 strings ExSheet's built-in formats paint: Number and Currency in each negative style at 0 and 2 places, Percentage, Scientific, General, each culture's built-in currency, and the Date and Time types, under 24 cultures, for amounts, fractions, every month and times either side of noon |
| `measure.mjs` | Measures one face in Chrome: every corpus string, and every glyph the corpus holds, the letters and currency signs a table may hold, and every glyph `CellTextMetrics` names in a class, at 14px and 12px, weights 400, 500, 600 and 700, tabular digits. `--text <string>` measures a string and prints it |
| `<face>.<platform>.json` | One face as `measure.mjs` measured it, on one platform |
| `tables.mjs` | Writes the per-glyph tables from the records: `src/ExGrid/Columns/DefaultGlyphWidths.cs` and `src/ExGrid.MudBlazor/RobotoGlyphTable.cs` |
| `classes.mjs` | The glyphs `CellTextMetrics` names in a class, which a table leaves out |
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
3. Write the tables again: `node tests/GlyphWidths/tables.mjs`.
4. Run layer 1. A failure names each string or glyph charged under its width, the face that painted
   it, and the size and weight. A glyph a table holds is fixed by step 3. Otherwise raise the
   class width that charges it — the preset table in `GridMetrics.Resolve`, or
   `MudExGridPresentation`'s constants — to a shade over the widest reading, at both sizes
   (`MudExGridPresentation`'s widths are scaled from 14px to 12px).

## The tables

A letter or currency sign is charged its own measured width where a table holds it, and the other
class otherwise (ticket 83). A table holds only glyphs its face draws itself in every record:
- **the core's** a glyph DejaVu Sans draws, which is what Linux paints `system-ui` in, at the
  widest any core record shows — macOS's `system-ui`, whatever font of the stack painted it, and
  DejaVu Sans — measured at 14px and at 12px apart;
- **Roboto's** a glyph Roboto draws, at the wider of its 14px width and its 12px one scaled to 14,
  because the core scales the table.

Each width is the widest at 400, 500 and 600 (regular) or at 700 (bold), a shade over. A glyph a face
lacks is painted in a fallback the platform chooses, so it stays the other class's.

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

- **Glyphs only a Custom format emits**, such as quoted text or `‰`. A letter a table holds is
  charged its width, and anything else the other class, so they are covered as far as the other
  class covers them, but they are not measured. `‰` is 20.17px in DejaVu Sans Bold (ticket 82),
  past every class.
- **Cultures outside the 24.** Their letters in the Latin, Greek and Cyrillic ranges are in the
  tables, but their strings are not in the corpus. Letters of other scripts are the other class's.
- **Fallback faces on another platform.** A glyph the face does not draw is measured in the
  fallback this platform chose, and left out of the tables. Linux and Windows choose others.
- **Windows' `system-ui`**, Segoe UI, until a record from Windows is added. Running `measure.mjs`
  there and then `tables.mjs` folds it into the core's table.
- **A Consumer's own font.** Whoever sets `--ex-font-family` owes the widths (ADR-0027's
  metrics-bearing obligation, ADR-0030), and a table if they want letters charged their own widths
  (`GlyphWidthTable`). Without one, a letter is charged the other class. This tool can measure a
  font: add a face to `measure.mjs` and a test that holds the Consumer's widths to its record.
