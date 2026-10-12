# Run 17, Part G, glyphs (FN-12e): what the orchestrator applies

Branch `claude/run17gh-glyphs`, 2026-10-12. The seventeenth Windows run's four glyph records are in
`tests/GlyphWidths`, `measure.mjs` and `tables.mjs` run on Windows, and the tables are written again
from every record. Nothing below is: each item is text for a file a background agent does not change,
as exact old and new whole lines, checked against `origin/main` at `a533de01`.

What changed on the branch, for the notes below:

- The core's table (`DefaultGlyphWidths`): seven glyphs rise to what Segoe UI paints. Every other entry is
  unchanged.

  | glyph | 14px regular | 14px bold | 12px regular | 12px bold |
  |---|---|---|---|---|
  | `ƒ` U+0192 | 6.10 → **7.79** | 6.10 → **8.07** | 5.24 → **6.68** | 5.24 → **6.92** |
  | `ť` U+0165 | 6.71 | 6.71 → **7.20** | 5.76 | 5.76 → **6.17** |
  | `ג` U+05D2 | 6.28 → **6.49** | 6.31 → **7.07** | 5.38 → **5.57** | 5.54 → **6.06** |
  | `ט` U+05D8 | 9.78 → **9.82** | 9.95 → **10.03** | 8.51 | 8.67 |
  | `י` U+05D9 | 4.13 | 4.13 → **4.28** | 3.56 | 3.56 → **3.67** |
  | `ס` U+05E1 | 9.74 → **9.90** | 9.93 → **10.09** | 8.49 | 8.64 → **8.65** |
  | `פ` U+05E4 | 9.23 | 9.35 → **9.48** | 8.01 | 8.15 |

- Roboto's table (`RobotoGlyphTable`): 39 of 415 entries rise by 0.01 or 0.02px (`Ю`, `г` and `ѓ` by 0.02),
  where Windows lays the same web font out a 64th of a pixel wider. None falls.
- No class width changes: every class's widest glyph on Windows is within its width, in both faces, at both
  sizes, regular and bold (the table in item 1).
- Layer 1: with the old tables and the Windows records, `GlyphWidthCorpusTests` fails 32 of its cases, as the
  run found; with the new tables, none. `RobotoGlyphWidthCorpusTests` now reads the Windows Roboto records
  and passes without a change to `MudExGridPresentation`.

## 1. ADR-0016: Windows' measurements (FN-12e's "the measurements are recorded in ADR-0016")

In "The defaults have to hold on every platform, not on one machine — decided", replace the paragraph's
last two lines:

old:

```
Windows and macOS are measured by hand, like the other checks only a person can run, and the
numbers are recorded here. Until they are, the defaults are the widest measured so far.
```

new:

```
Windows and macOS are measured by hand, like the other checks only a person can run, and the
numbers are recorded here. Until they are, the defaults are the widest measured so far.
*(2026-10-12, the seventeenth Windows run, FN-12e: **Windows measured.** Chrome 154 and Edge 155,
headless, on Windows 11 with an English (United Kingdom) display at 150%, measured `system-ui` (Segoe
UI, and Segoe UI Semibold at 500 and 600) and ExGrid.MudBlazor's Roboto with `tests/GlyphWidths`'
tool, over the corpus of 3,091 strings. The two browsers read the same widths. Each class's widest
glyph at 400, 500 and 600 (regular) and at 700 (bold), against the width that charges it; "other" is
the widest glyph no table holds, full-width aside, which is an em on Windows too:)*

| Segoe UI | 14px regular | 14px bold | 12px regular | 12px bold |
|---|---|---|---|---|
| wide | 11.766 (`%`) / 14.04 | 12.141 (`%`) / 14.36 | 10.094 (`%`) / 12.04 | 10.406 (`%`) / 12.44 |
| digit | 8.625 (`₫`) / 9.75 | 9.078 (`₫`) / 9.75 | 7.391 (`₫`) / 8.36 | 7.781 (`₫`) / 8.36 |
| narrow | 5.797 (`/`) / 6.41 | 6.219 (`/`) / 6.41 | 4.969 (`/`) / 5.49 | 5.328 (`/`) / 5.49 |
| other | 11.328 (`₼`) / 15.46 | 11.656 (`₼`) / 15.46 | 9.703 (`₼`) / 13.26 | 10.000 (`₼`) / 13.26 |

| Roboto | 14px regular | 14px bold | 12px regular | 12px bold |
|---|---|---|---|---|
| wide | 10.344 (`%`) / 10.40 | 10.359 (`%`) / 10.40 | 8.859 (`%`) / 8.91 | 8.875 (`%`) / 8.91 |
| digit | 8.281 (`£`) / 8.30 | 8.328 (`£`) / 8.34 | 7.109 (`£`) / 7.11 | 7.141 (`£`) / 7.15 |
| narrow | 5.781 (`/`) / 5.80 | 5.203 (`/`) / 5.25 | 4.953 (`/`) / 4.97 | 4.453 (`/`) / 4.50 |
| other | 11.547 (`₾`, Arial) / 12.44 | 11.547 (`₾`, Arial) / 12.28 | 9.891 (`₾`, Arial) / 10.66 | 9.891 (`₾`, Arial) / 10.53 |

*Every class holds on Windows. Roboto's widths at 12px are its 14px ones scaled, as the core scales them.
Seven letters did not: Segoe UI paints `ƒ` at every weight (7.547px against 6.10 at 14px, 400; 8.063 at
700), `ť` at 700 (7.188 against 6.71), and the Hebrew `ג` `ט` `י` `ס` `פ` at some weights wider than the
table charged them, by 0.001 to 0.76px. A Number or Date holding one was charged under what it paints. The
records are now in `tests/GlyphWidths` (`<face>.windows.<browser>.json`), and the table takes each glyph at
the widest of every record, so the seven are charged what Segoe UI paints: `ƒ` 7.79 and bold 8.07 at 14px,
6.68 and 6.92 at 12px. On Windows, Thai is painted in Tahoma, the ideographs in Microsoft YaHei (Chrome) or
Noto Sans JP (Edge) and Hangul in Malgun Gothic, in both faces; they are the other class's or the
full-width class's, as on macOS. Windows under another display language, where `system-ui` is another face
(Yu Gothic UI under Japanese), is not measured.)*
```

## 2. ADR-0016: the core's table is the widest of three faces, not two

In the note "*(2026-10-01, ticket 83, built.)*":

old:

```
    - The core's table has 438 glyphs: those DejaVu draws, each at the wider of DejaVu and macOS
      system-ui, measured at 14px and at 12px. Roboto's has 415.
```

new:

```
    - The core's table has 438 glyphs: those DejaVu draws, each at the widest of DejaVu, macOS's
      system-ui and Windows' (Segoe UI, in Chrome and Edge; from 2026-10-12), measured at 14px and at
      12px. Roboto's has 415, each at the widest of macOS and Windows.
```

## 3. The release check's table of paths (ADR-0208), and the eighteenth run

The table "Rows checked by hand, and the code each covers" is not on `main` yet (ADR-0208's
Consequences; Definition of Done Step 4). For whoever writes it, and for the eighteenth run's list:

- **FN-12e's paintings.** This branch changes the estimate's tables, `src/ExGrid/Columns/DefaultGlyphWidths.cs`
  and `src/ExGrid.MudBlazor/RobotoGlyphTable.cs`, after the run's commit `8436e45e`. They are the code
  FN-12e's pass column covers, so its two paintings go on the eighteenth run's list, on Windows, in Chrome
  and Edge, on WebAssembly and Server:
  - `sizing.spec.mjs`'s "a bold total the estimate fits paints whole, and a #### run fits its cell (FN-12e,
    ADR-0016)";
  - `part-c.spec.mjs`'s "FN-12e (Part C, case 3c): #### fills its cell with whole # in the face it is painted
    in, and no more fit (ADR-0016)".
- **FN-12e's measurement is of the platform's fonts.** Its record is the four Windows records this branch
  adds, and it holds until the fonts, the browsers' text layout, the corpus or what the tool reads change.
  This branch changes `measure.mjs` only in how it finds its own folder and names its record, which is the
  patch the run measured with; what it measures is unchanged. Proposed paths, the measurement's apart from
  the paintings':
  - the measurement: `tests/GlyphWidths/corpus.json`, `tests/GlyphWidths/classes.mjs`,
    `tests/GlyphWidths/measure.mjs`, `samples/ExGrid.DemoPages/wwwroot/fonts/roboto/`, and
    `src/ExGrid/Assets/ex-grid.css` (the family stack);
  - the paintings: `src/ExGrid/Columns/` (`CellTextMetrics.cs`, `DefaultGlyphWidths.cs`,
    `GlyphWidthTable.cs`), `src/ExGrid/GridMetrics.cs`, `src/ExGrid.MudBlazor/RobotoGlyphTable.cs`,
    `src/ExGrid.MudBlazor/MudExGridPresentation.cs`, `src/ExGrid/Assets/` (the `####` run's wrapping),
    and the two specs and their pages.
  - If the row keeps one list and `measure.mjs` is in it, the measurement goes on the eighteenth run's
    list too: four commands on the Windows desk (`tests/GlyphWidths/README.md`, "Measuring again").

## 4. A reading correction to the run's report (Part G, FN-12e)

`part-g/glyph-classes.txt` compares the Roboto records with the core's defaults (14.04, 9.75, 6.41), not
with the widths `MudExGridPresentation` cascades for Roboto (10.4, 8.3, 5.8; bold 10.4, 8.34, 5.25). Against
Roboto's own, every class is still within, by as little as 0.005px (the digit class at 12px: `£` 7.109
against 7.114), and layer 1 now holds every Roboto glyph and string on Windows to them. For the report's
bullet "Layer 1 reads `system-ui` and DejaVu Sans. The Windows Roboto records are not among its faces, and
were compared by class only.":

old:

```
  - Layer 1 reads `system-ui` and DejaVu Sans. The Windows Roboto records are not among its faces, and were compared by
    class only.
```

new:

```
  - Layer 1 reads `system-ui` and DejaVu Sans. The Windows Roboto records are not among its faces, and were compared by
    class only, against the core's defaults rather than Roboto's own widths. *(Corrected 2026-10-12: against Roboto's
    widths every class is within, by as little as 0.005px, and `RobotoGlyphWidthCorpusTests` now reads these records
    and passes.)*
```

## 5. The Definition of Done

No text change. FN-12e's first clause, "each `CellTextMetrics` default is at least the widest glyph measured
for its class on Windows", is met once the tables are written from the Windows records (this branch), and
"the measurements are recorded in ADR-0016" once item 1 is applied.

## 6. The Docs Site

Nothing. No page names the platforms measured or a glyph's width: `/exgrid/appearance` says the grid decides
`####` from its glyphs' widths and that a font's metrics come with it, which stays true. The change is seven
letters charged more on Windows, which no Example shows.

## Open question for the user

**Windows under a display language other than English.** `system-ui` there is another face (Yu Gothic UI
under Japanese, for one). Its Latin letters and currency signs are not measured, and a letter it paints wider
than the table would be charged under its width there. Measure them on the Windows desk (a display language per run, signed out and in), or
record them as out of scope for `0.1.0`, as `tests/GlyphWidths/README.md` now does?
