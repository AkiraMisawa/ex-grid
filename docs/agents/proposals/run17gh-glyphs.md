# Run 17, Part G, glyphs (FN-12e): what the orchestrator applies

Branch `claude/run17gh-glyphs`, 2026-10-12. The seventeenth Windows run's four glyph records (an English (United
Kingdom) display, 2026-10-11) and the two `system-ui` records of the Japanese-display run (2026-10-12,
`verification/2026-10-12-windows-glyphs-ja/`, merged here) are in `tests/GlyphWidths`, and the tables are written
again from every record. The Japanese run's two Roboto records are not among them (below). Nothing in the numbered
blocks is applied: each is an exact old text and its new text, in whole lines, for a file a background agent does not
change.

## How to apply

Apply the blocks in order. Each old text occurs exactly once in its file, and the list applies in order without one
block spoiling another's match. Blocks 1 to 3 were checked against this branch's tip (`docs/adr/0016` there is
`origin/main`'s at `a533de01`, and unchanged on `origin/main` at `2265cda6`). Block 4's file is not on this branch:
it was checked against `origin/claude/exsheet-windows-verify-17` at `2fc23f56`, where the seventeenth run's report is.

## What changed on the branch

- **An English display (the seventeenth run's records).** The core's table (`DefaultGlyphWidths`): seven glyphs rise
  to what Segoe UI paints.

  | glyph | 14px regular | 14px bold | 12px regular | 12px bold |
  |---|---|---|---|---|
  | `ƒ` U+0192 | 6.10 → **7.79** | 6.10 → **8.07** | 5.24 → **6.68** | 5.24 → **6.92** |
  | `ť` U+0165 | 6.71 | 6.71 → **7.20** | 5.76 | 5.76 → **6.17** |
  | `ג` U+05D2 | 6.28 → **6.49** | 6.31 → **7.07** | 5.38 → **5.57** | 5.54 → **6.06** |
  | `ט` U+05D8 | 9.78 → **9.82** | 9.95 → **10.03** | 8.51 | 8.67 |
  | `י` U+05D9 | 4.13 | 4.13 → **4.28** | 3.56 | 3.56 → **3.67** |
  | `ס` U+05E1 | 9.74 → **9.90** | 9.93 → **10.09** | 8.49 | 8.64 → **8.65** |
  | `פ` U+05E4 | 9.23 | 9.35 → **9.48** | 8.01 | 8.15 |

  Roboto's table (`RobotoGlyphTable`): 39 of 415 entries rise by 0.01 or 0.02px (`Ю`, `г` and `ѓ` by 0.02), where
  Windows lays the same web font out a 64th of a pixel wider.
- **A Japanese display (the records of 2026-10-12).** `system-ui` is Yu Gothic UI there. The core's table rises in
  eight more glyphs, 29 entries; every other entry is unchanged, and none falls. Chrome and Edge read the same widths.

  | glyph | 14px regular | 14px bold | 12px regular | 12px bold |
  |---|---|---|---|---|
  | `ď` U+010F | 10.40 → **10.42** | 10.74 | 9.04 | 9.34 |
  | `Ѐ` U+0400 | 9.59 → **14.26** | 9.59 → **14.26** | 8.21 → **12.23** | 8.21 → **12.23** |
  | `ѐ` U+0450 | 9.51 → **14.45** | 9.51 → **14.45** | 8.15 → **12.38** | 8.15 → **12.38** |
  | `ѝ` U+045D | 9.82 → **15.45** | 9.82 → **15.45** | 8.43 → **13.24** | 8.43 → **13.24** |
  | `฿` U+0E3F | 9.76 → **9.84** | 9.76 → **9.84** | 8.37 → **8.43** | 8.37 → **8.43** |
  | `₡` U+20A1 | 9.76 → **10.07** | 9.76 → **10.07** | 8.37 → **8.64** | 8.37 → **8.64** |
  | `₦` U+20A6 | 9.76 → **11.62** | 9.76 → **11.62** | 8.37 → **9.96** | 8.37 → **9.96** |
  | `₱` U+20B1 | 9.76 → **11.12** | 9.76 → **11.12** | 8.37 → **9.53** | 8.37 → **9.53** |

  Roboto's table is unchanged by it.
- **No class width changes.** Every class's widest glyph is within its width on Windows under both displays, at both
  sizes, regular and bold (block 1's tables). Under the Japanese display the margins are narrow: the wide class by
  0.040px at both sizes (Yu Gothic UI draws `−` an em wide, 14.000px against 14.04), the digit class by 0.047px at 14px
  and 0.032px at 12px (`₺`, 9.703 against 9.75 and 8.328 against 8.36).
- **The Japanese run's figure for the other class was a table letter.** Its report gives the other class's widest as
  `ѝ` (15.438px against 15.46, 13.234 against 13.26). `ѝ` is a letter the core's table holds, so it was charged its
  entry, 9.82px, not the other class, and the table now charges it 15.45. The widest glyph no table holds is `؋`,
  painted in Tahoma, 14.328px against 15.46 (12.281 against 13.26). Block 3 corrects the report's reading.
- **Roboto under the Japanese display is not read.** Every glyph its two records name Roboto as the painter of reads
  another face's width: 446 of the 451 read exactly Arial's advance (Liberation Sans's, which has Arial's metrics) at
  14px and at 12px, at 400 and at 700; the English record's do not (6 and 2, by coincidence). Its fallbacks read what
  they read under English. So the widths were read in the stack's fallback, and the painters after Roboto applied.
  Read as Roboto's, they put `%` (12.453px) past the wide class `MudExGridPresentation` cascades (10.4), `E` (9.344)
  past the digit class (8.3), and `₺` bold (8.344) past the bold digit class (8.34), so the corpus test fails whatever
  the table says. That is a class width change, which is not made without a decision, and on readings that are not
  Roboto's it would be the wrong one. The records stay in `verification/` only.
- **`measure.mjs` refuses such a record.** After the painters are read, each glyph's span is read again, and a glyph
  that is another width by then means the face changed while measuring: nothing is written. Run here on Linux, in
  Playwright's Chromium, it writes Roboto's record as before (each of the 451 glyphs Roboto paints at macOS's width,
  at 14px and 400), and a run whose spans are switched to Arial between the readings is refused. What it measures is
  unchanged.
- **Layer 1.** With the old tables and the Japanese records, `GlyphWidthCorpusTests` fails 32 cases (each glyph test
  of the two records, at every density and weight); with the new tables, none. With the Japanese Roboto records,
  `RobotoGlyphWidthCorpusTests` failed 64 cases under the old Roboto table and 64 under one written from them. Layers 1
  and 2 pass whole. Two tests hold which records stay read; the core's now names the two Japanese records.

### 1. docs/adr/0016-column-width-and-overflow.md

FN-12e's "the measurements are recorded in ADR-0016": Windows under an English display, and under a Japanese one. In
"The defaults have to hold on every platform, not on one machine — decided", the paragraph's last two lines.

Old:

````text
Windows and macOS are measured by hand, like the other checks only a person can run, and the
numbers are recorded here. Until they are, the defaults are the widest measured so far.
````

New:

````text
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
full-width class's, as on macOS.)*
*(2026-10-12, FN-12e: **Windows under a Japanese display measured.** The same tool, browsers and desk at
150%, with Windows' display language Japanese and its regional format left English (United Kingdom).
`system-ui` is the message font Windows sets for its display language: Yu Gothic UI under Japanese, and
Yu Gothic UI Semibold at 500 and 600, for 467 of the 523 glyphs measured. The Hebrew letters and `֏`,
which it lacks, stay in Segoe UI; Thai, `؋` and `₭ ₲ ₴ ₵ ₼ ₽ ₾ ₿` go to Tahoma. The two browsers read
the same widths. Each class's widest glyph, as above:)*

| Yu Gothic UI | 14px regular | 14px bold | 12px regular | 12px bold |
|---|---|---|---|---|
| wide | 14.000 (`−`) / 14.04 | 14.000 (`−`) / 14.36 | 12.000 (`−`) / 12.04 | 12.000 (`−`) / 12.44 |
| digit | 9.703 (`₺`) / 9.75 | 9.703 (`₺`) / 9.75 | 8.328 (`₺`) / 8.36 | 8.328 (`₺`) / 8.36 |
| narrow | 5.797 (`/`) / 6.41 | 5.797 (`/`) / 6.41 | 4.969 (`/`) / 5.49 | 4.969 (`/`) / 5.49 |
| other | 14.328 (`؋`, Tahoma) / 15.46 | 14.328 (`؋`, Tahoma) / 15.46 | 12.281 (`؋`, Tahoma) / 13.26 | 12.281 (`؋`, Tahoma) / 13.26 |

*Every class holds under a Japanese display, by little: the wide class by 0.04px at both sizes, since Yu
Gothic UI draws `−` an em wide, and the digit class by 0.047px at 14px and 0.032px at 12px (`₺`). Eight
letters and signs the table holds did not: Yu Gothic UI paints the Cyrillic `Ѐ` `ѐ` `ѝ` an em wide or more
(`ѝ` 15.438px at 14px, against 9.82 charged), `₦` `₱` `₡` `฿` wider than the table charged them (`₦`
11.609 against 9.76), and `ď` at 500 and 600 by 0.006px. The records are in `tests/GlyphWidths` as
`system-ui.windows-ja.<browser>.json`, and the table takes them: `ѝ` is charged 15.45 at 14px and 13.24 at
12px. ExGrid.MudBlazor's Roboto was measured in the same run, and its records are not read: every glyph
they name Roboto as the painter of reads Arial's advance (446 of 451, exactly), so the widths were read
before Roboto applied and the painters after. `measure.mjs` now reads each glyph again after its painter
and refuses a record whose widths changed in between. Roboto is a web font the page serves, so its own
widths should not change with the display language, and the glyphs it lacks read the same widths as
under English. No other display language is measured.)*
````

### 2. docs/adr/0016-column-width-and-overflow.md

The core's table is the widest of more faces than two. In the note "*(2026-10-01, ticket 83, built.)*".

Old:

````text
    - The core's table has 438 glyphs: those DejaVu draws, each at the wider of DejaVu and macOS
      system-ui, measured at 14px and at 12px. Roboto's has 415.
````

New:

````text
    - The core's table has 438 glyphs: those DejaVu draws, each at the widest of DejaVu, macOS's
      system-ui and Windows' (Segoe UI under an English display and Yu Gothic UI under a Japanese
      one, in Chrome and Edge; from 2026-10-12), measured at 14px and at 12px. Roboto's has 415, each
      at the widest of macOS and Windows under an English display.
````

### 3. verification/2026-10-12-windows-glyphs-ja/report.md

A reading correction to the Japanese-display run's report: its other class is a table letter, and its Roboto
comparison is with the core's widths, of readings that are Arial's. Kept as a note beside the text it corrects, as
block 4 does for the seventeenth run. *(Optional: the record is the Windows session's; the README and block 1 say the
same.)*

Old:

````text
- **By class, every widest glyph is within its default**, in both faces and both browsers. Three are within 0.05 px:
  the digit `₺` at both sizes, and the other class's `ѝ` at 14 px bold.
- `roboto` under the Japanese display is within too, as under the English one (wide 12.453, digit 9.344, narrow 4.672
  at 14 px).
````

New:

````text
- **By class, every widest glyph is within its default**, in both faces and both browsers. Three are within 0.05 px:
  the digit `₺` at both sizes, and the other class's `ѝ` at 14 px bold.
  *(Corrected 2026-10-12, when the records were folded on `claude/run17gh-glyphs`: `ѝ` is a letter the core's table
  holds, so it is charged its entry, not the other class, and was charged 9.82 px at 14 px, under what Yu Gothic UI
  paints; the table now charges it 15.45. The widest glyph no table holds is `؋` in Tahoma, 14.328 px against 15.46
  (12.281 against 13.26 at 12 px). Within 0.05 px are the digit `₺` at both sizes, by 0.047 and 0.032 px, and the
  wide class's `−` at both sizes, regular, by 0.04 px.)*
- `roboto` under the Japanese display is within too, as under the English one (wide 12.453, digit 9.344, narrow 4.672
  at 14 px).
  *(Corrected 2026-10-12: those are compared with the core's defaults. Against the widths `MudExGridPresentation`
  cascades for Roboto, `%` and `E` are past the wide and digit classes (10.4 and 8.3). And they are not Roboto's:
  every glyph the records name Roboto as the painter of reads Arial's advance, 446 of 451 exactly, so the widths
  were read before Roboto applied. Layer 1 does not read these two records; `tests/GlyphWidths/README.md` says why.)*
````

### 4. verification/2026-10-11-windows-17/report.md

A reading correction to the seventeenth run's report (Part G, FN-12e). `part-g/glyph-classes.txt` compares the
Roboto records with the core's defaults (14.04, 9.75, 6.41), not with the widths `MudExGridPresentation` cascades for
Roboto (10.4, 8.3, 5.8; bold 10.4, 8.34, 5.25). Against Roboto's own, every class is still within, by as little as
0.005px (the digit class at 12px: `£` 7.109 against 7.114), and layer 1 now holds every Roboto glyph and string of
that run to them. The file is on `origin/claude/exsheet-windows-verify-17`, not on this branch.

Old:

````text
  - Layer 1 reads `system-ui` and DejaVu Sans. The Windows Roboto records are not among its faces, and were compared by
    class only.
````

New:

````text
  - Layer 1 reads `system-ui` and DejaVu Sans. The Windows Roboto records are not among its faces, and were compared by
    class only, against the core's defaults rather than Roboto's own widths. *(Corrected 2026-10-12: against Roboto's
    widths every class is within, by as little as 0.005px, and `RobotoGlyphWidthCorpusTests` now reads these records
    and passes.)*
````

## Notes without a block

### The release check's table of paths (ADR-0208), and the eighteenth run

The table "Rows checked by hand, and the code each covers" is not on `main` yet (ADR-0208's Consequences; Definition
of Done Step 4). For whoever writes it, and for the eighteenth run's list:

- **FN-12e's paintings.** This branch changes the estimate's tables, `src/ExGrid/Columns/DefaultGlyphWidths.cs` and
  `src/ExGrid.MudBlazor/RobotoGlyphTable.cs`, after the run's commit `8436e45e`. They are the code FN-12e's pass column
  covers, so its two paintings go on the eighteenth run's list, on Windows, in Chrome and Edge, on WebAssembly and
  Server:
  - `sizing.spec.mjs`'s "a bold total the estimate fits paints whole, and a #### run fits its cell (FN-12e,
    ADR-0016)";
  - `part-c.spec.mjs`'s "FN-12e (Part C, case 3c): #### fills its cell with whole # in the face it is painted in, and
    no more fit (ADR-0016)".
- **FN-12e's measurement is of the platform's fonts, under a display language.** Its record is the six Windows
  records this branch adds: four under an English display, two under a Japanese one. It holds until the fonts, the
  display language, the browsers' text layout, the corpus or what the tool reads change. This branch changes
  `measure.mjs` in how it finds its own folder and names its record (the patch the run measured with) and in refusing
  a record whose widths changed before its painters were read; what it measures is unchanged. Proposed paths, the
  measurement's apart from the paintings':
  - the measurement: `tests/GlyphWidths/corpus.json`, `tests/GlyphWidths/classes.mjs`,
    `tests/GlyphWidths/measure.mjs`, `samples/ExGrid.DemoPages/wwwroot/fonts/roboto/`, and
    `src/ExGrid/Assets/ex-grid.css` (the family stack);
  - the paintings: `src/ExGrid/Columns/` (`CellTextMetrics.cs`, `DefaultGlyphWidths.cs`, `GlyphWidthTable.cs`),
    `src/ExGrid/GridMetrics.cs`, `src/ExGrid.MudBlazor/RobotoGlyphTable.cs`,
    `src/ExGrid.MudBlazor/MudExGridPresentation.cs`, `src/ExGrid/Assets/` (the `####` run's wrapping), and the two
    specs and their pages.
  - `measure.mjs` is in the measurement's list and this branch changes it, so the measurement goes on the eighteenth
    run's list too: the four commands on the Windows desk under English (`tests/GlyphWidths/README.md`, "Measuring
    again"), and Roboto's two under a Japanese display if the user wants them (the questions below).

### The Definition of Done

No text change. FN-12e's first clause, "each `CellTextMetrics` default is at least the widest glyph measured for its
class on Windows", is about the core's defaults, and holds for Windows under an English and under a Japanese display
once the tables are written from these records (this branch). "The measurements are recorded in ADR-0016" holds once
block 1 is applied. Roboto's widths are the Wrapper's (ADR-0030), held by `RobotoGlyphWidthCorpusTests` to the English
records.

### The Docs Site

Nothing. No page names the platforms measured, a display language or a glyph's width: `/exgrid/appearance` says the
grid decides `####` from its glyphs' widths and that a font's metrics come with it, which stays true. The change is
fifteen letters and signs charged more on Windows, which no Example shows.

### The Change Notes

Two, both in the branch: `docs/releases/unreleased/run17gh-glyphs-windows-widths.md` (ExGrid, Fixed: an English
display) and `docs/releases/unreleased/run17gh-glyphs-windows-ja-widths.md` (ExGrid, Fixed: a Japanese display, where
eight glyphs rise).

## Open questions for the user

1. **Roboto under a Japanese display.** Its records read Arial's widths, so they are held out (above). Measure Roboto
   again on the Windows desk under a Japanese display with the corrected `measure.mjs` (two commands, after a sign-out
   and sign-in each way), or record that a web font's own widths do not depend on the display language and that the
   English Roboto records cover it, as the fallbacks' identical readings suggest? Until one is chosen, the Wrapper's
   widths are held to the English records only.
2. **Windows under a display language other than English and Japanese.** `system-ui` is the message font Windows sets
   for its display language, so under another one (Chinese or Korean, for instance) it may be another face again, and
   a letter it paints wider than the table would be charged under its width there. Measure more on the Windows desk (a
   display language per run, signed out and in), or record them as out of scope for `0.1.0`, as
   `tests/GlyphWidths/README.md` now does?
