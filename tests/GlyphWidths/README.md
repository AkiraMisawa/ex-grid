# Glyph widths: what the per-class estimate has to cover

ADR-0016 decides `####` from an estimate, never a measurement: each character is charged the width
of its class (`CellTextMetrics`). The estimate is only safe if no glyph a Number Format emits is
wider than its class's width, at every weight the grid paints (ADR-0016's notes of 2026-10-01;
tickets 82 and 83). This folder holds the evidence and the tool that makes it.

| File | What it is |
|---|---|
| `corpus.json` | The 3,091 strings ExSheet's built-in formats paint: Number and Currency in each negative style at 0 and 2 places, Percentage, Scientific, General, each culture's built-in currency, and the Date and Time types, under 24 cultures, for amounts, fractions, every month and times either side of noon |
| `measure.mjs` | Measures one face in Chrome or Edge: every corpus string, and every glyph the corpus holds, the letters and currency signs a table may hold, and every glyph `CellTextMetrics` names in a class, at 14px and 12px, weights 400, 500, 600 and 700, tabular digits. `--text <string>` measures a string and prints it |
| `<face>.<platform>.json` | One face as `measure.mjs` measured it, on one platform, in Chrome: `system-ui.macos.json`, `roboto.macos.json` and `dejavu-sans.macos.json` (2026-10-01) |
| `<face>.<platform>.<browser>.json` | One face measured on one platform in the browser `--browser` named: `system-ui.windows.chrome.json`, `system-ui.windows.msedge.json`, `roboto.windows.chrome.json` and `roboto.windows.msedge.json` (Chrome 154 and Edge 155, display en-GB at 150%, the seventeenth Windows run, 2026-10-11) |
| `<face>.windows-ja.<browser>.json` | Windows under a Japanese display language, where `system-ui` is Yu Gothic UI: `system-ui.windows-ja.chrome.json` and `system-ui.windows-ja.msedge.json` (Chrome 154 and Edge 155, display ja-JP at 150%, regional format en-GB, 2026-10-12; `verification/2026-10-12-windows-glyphs-ja/`). `measure.mjs` names a record by platform only, so the display language was added to the name by hand. Roboto's records of the same run are not here (What stays out) |
| `liberation-sans.linux.json` | Liberation Sans, what Linux paints Helvetica and Arial in, measured as a web font as DejaVu Sans is (Chromium 153, 2026-10-27): a face of `@ex-grid/mantine`'s stack, read only by its widths |
| `tables.mjs` | Writes the per-glyph tables from the records: `src/ExGrid/Columns/DefaultGlyphWidths.cs`, `src/ExGrid.MudBlazor/RobotoGlyphTable.cs`, the Script Core's, and `src/npm/mantine/src/mantine-glyph-widths.ts` with the class widths `@ex-grid/mantine` hands its grid |
| `classes.mjs` | The glyphs `CellTextMetrics` names in a class, which a table leaves out |
| `GlyphWidthRecord.cs` | Reads a record; compiled into the test projects below |

## What reads them

- **ExSheet.Components, `GlyphCorpusTests`**: the corpus is still what the built-in formats paint.
  A format that changes its output fails it, and the corpus it makes is written beside the test
  binary as `corpus.generated.json`.
- **ExGrid.Tests, `GlyphWidthCorpusTests`**: under the core's defaults, at every density, no corpus
  string paints wider than its estimate and no glyph wider than its class, in `system-ui.*` (macOS's,
  and Windows' under an English and a Japanese display, each browser's record) and `dejavu-sans.*`.
  Weights 400 to 600 are held to the regular widths, 700 to the bold ones.
- **ExGrid.MudBlazor.Tests, `RobotoGlyphWidthCorpusTests`**: the same, under the widths
  `MudExGridPresentation` cascades, in `roboto.*` (macOS's and Windows' under an English display).

- **@ex-grid/mantine, `glyph-widths.test.ts`**: the same, under the widths `MantineExGrid` hands its grid,
  in every face Mantine's default stack resolves to: `system-ui.*` but Japanese Windows' (Segoe UI is named
  ahead of Yu Gothic UI), `roboto.*`, `dejavu-sans.*` and `liberation-sans.*`.

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
   node tests/GlyphWidths/measure.mjs liberation-sans \
     --liberation "$(nix build --no-link --print-out-paths nixpkgs#liberation_ttf)/share/fonts/truetype"
   ```

   Each writes `<face>.<platform>.json` here. `system-ui` is whatever the platform resolves it to,
   so a run on Linux or Windows adds a record of its own, and the core's tests read it too.

   To measure with Edge, or to keep one record per browser on a platform, name the browser:
   `--browser chrome` or `--browser msedge`. Each is found where it installs itself, and the record
   is `<face>.<platform>.<browser>.json`. On Windows, measure `system-ui` and `roboto` in both,
   as the Windows records were:

   ```sh
   node tests/GlyphWidths/measure.mjs system-ui --browser chrome
   node tests/GlyphWidths/measure.mjs system-ui --browser msedge
   node tests/GlyphWidths/measure.mjs roboto --browser chrome
   node tests/GlyphWidths/measure.mjs roboto --browser msedge
   ```

   Under a display language other than English, `system-ui` is another face. Sign in under that
   language, measure the same four, and give the records the language in their platform's name, as
   `<face>.windows-ja.<browser>.json` is Japanese's, so they sit beside the English ones and are read
   with them. `measure.mjs` writes the platform only, so a run from a copy of these files, as the
   Japanese one was, keeps the English records here from being overwritten.
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
  widest any core record shows — macOS's `system-ui` and Windows' (Segoe UI under an English
  display and Yu Gothic UI under a Japanese one, in Chrome and Edge), whatever font of the stack
  painted it, and DejaVu Sans — measured at 14px and at 12px apart;
- **Roboto's** a glyph Roboto draws, at the wider of its 14px width and its 12px one scaled to 14,
  because the core scales the table.
- **Mantine's** a glyph every Linux face of its stack draws itself (Roboto, Liberation Sans and DejaVu
  Sans), at the widest any of the stack's records shows, at 14px and at 12px scaled to 14. Its class
  widths are written beside it, each the widest glyph of the class in those records; its other class is
  the widest glyph any of them paints outside the classes and the table, full-width blocks aside.

Each width is the widest at 400, 500 and 600 (regular) or at 700 (bold), a shade over. A glyph a face
lacks is painted in a fallback the platform chooses, so it stays the other class's.

## How a width is read

- Headless Chrome or Edge at a device scale factor of 1, from a page that sets the face the grid
  sets: `ex-grid.css`'s `system-ui, sans-serif`, `MudExGridFont.Roboto.Family` over the files the
  demo pages serve, and DejaVu Sans Book and Bold as web fonts (Linux has no 600, so 600 matches
  Bold there and here).
- A glyph's width is the wider of the glyph alone and a hundred in a row, divided by a hundred: a
  glyph that kerns with itself reads short in a run (Roboto's `//`), and a glyph alone is laid out
  to the next 1/64px. A string's width is the string as one run.
- Every width is in 64ths of a pixel, rounded up, so no reading is under what was painted.
- The painters are read after the widths, and each glyph is read again then: one that is another
  width by the time its painter is read means the face changed while measuring, and nothing is
  written. Without that check, Roboto under a Japanese display was recorded with Arial's widths and
  Roboto named as their painter (What stays out).
- `painters` names the face that painted each glyph. A glyph the family does not draw is painted
  by the platform's fallback:
  - on macOS, Thai in every face (Thonburi), Hebrew in Roboto (Arial), `₴` and `฿` in Roboto
    (Helvetica Neue), `₼` in DejaVu Sans (Kefa), and the ideographs and Hangul in every face
    (PingFang, Apple SD Gothic Neo);
  - on Windows, `system-ui` is Segoe UI, and Segoe UI Semibold at 500 and 600. Thai is painted in
    Tahoma in every face, the ideographs in Microsoft YaHei (Chrome) or Noto Sans JP (Edge), and
    Hangul in Malgun Gothic. In Roboto, Hebrew, `֏`, `؋` and `₡ ₭ ₮ ₲ ₴ ₵ ₸ ₾ ₿` are painted in
    Arial, `฿` in Tahoma, `৳` in Nirmala UI and `៛` in Leelawadee UI.
  - on Windows under a Japanese display, `system-ui` is Yu Gothic UI, and Yu Gothic UI Semibold at
    500 and 600: the Latin, Greek and Cyrillic letters, the digits and signs, the currency signs but
    those below, and the ideographs, 467 of the 523 glyphs. The Hebrew letters and `֏`, which it
    lacks, are painted in Segoe UI (Semibold at 500 and 600), Thai, `؋` and `₭ ₲ ₴ ₵ ₼ ₽ ₾ ₿` in
    Tahoma, and Hangul in Malgun Gothic. Chrome and Edge are alike. A glyph painted in the same face
    as under English reads the same width.

## What stays out

- **Glyphs only a Custom format emits**, such as quoted text or `‰`. A letter a table holds is
  charged its width, and anything else the other class, so they are covered as far as the other
  class covers them, but they are not measured. `‰` is 20.17px in DejaVu Sans Bold (ticket 82),
  past every class.
- **Cultures outside the 24.** Their letters in the Latin, Greek and Cyrillic ranges are in the
  tables, but their strings are not in the corpus. Letters of other scripts are the other class's.
- **Fallback faces on another platform.** A glyph the face does not draw is measured in the
  fallback each measured platform chose, and left out of the tables. Linux chooses others.
- **Windows under a display language other than English and Japanese.** `system-ui` is the message
  font Windows sets for its display language, so under another one it may be another face again.
  English (United Kingdom), where it is Segoe UI, and Japanese, where it is Yu Gothic UI, are
  measured (2026-10-11 and 2026-10-12); no other display language is, Chinese and Korean among them.
  Whether more are measured is an open question for the user.
- **Roboto under a Japanese display.** Its records of 2026-10-12, in
  `verification/2026-10-12-windows-glyphs-ja/glyphs/`, are not read. Every glyph they name Roboto as
  the painter of reads another face's width: 446 of the 451 read exactly Arial's advance (Liberation
  Sans's, metric for metric) at 14px and 12px, at 400 and at 700, and the English record's do not.
  The widths were read in the stack's fallback, and the painters after Roboto had applied. Read as
  Roboto's, they would put `%` (12.453px) and `E` (9.344px) past the wide and digit classes
  `MudExGridPresentation` cascades (10.4 and 8.3). Roboto is a web font the page serves, so its own
  widths should not change with the display language, and the glyphs it lacks read the same widths
  as under English, the ideographs included (Noto Sans JP's in Chrome, Microsoft YaHei's before, an
  em either way). Whether it is measured again, with the check in "How a width is read", is an open
  question for the user.
- **A Consumer's own font.** Whoever sets `--ex-font-family` owes the widths (ADR-0027's
  metrics-bearing obligation, ADR-0030), and a table if they want letters charged their own widths
  (`GlyphWidthTable`). Without one, a letter is charged the other class. This tool can measure a
  font: add a face to `measure.mjs` and a test that holds the Consumer's widths to its record.
