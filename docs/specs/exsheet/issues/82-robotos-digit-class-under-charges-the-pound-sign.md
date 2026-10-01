# 82: `ExGrid.MudBlazor` charges `£` below its width

Status: done

**What to build:** a fix found by ticket 47. `ExGrid.MudBlazor` declares Roboto's regular digit class
at 8.0px. `£`, which is in that class, measured 8.281px at weight 600. A number with a `£` can then be
judged to fit when its last pixels do not, and it is cut instead of shown as `####`. That is quietly
wrong (principle 1; ADR-0016, ADR-0030).

**Blocked by:** None (can start immediately)

- [x] Re-measure Roboto's character classes at the weights `ExGrid.MudBlazor` paints (400 and 600 for
      regular, 700 for bold), as §21.7a measured the core's: Chrome, 14px, tabular digits, each
      class's glyphs.
- [x] Each class declares its widest glyph, or the glyph moves to the class that covers it.
- [x] A layer-1 test with `£1,234,567.50` at the width where the old value fits and the true one does
      not, named after ADR-0016 and ADR-0030.
- [x] Say in the comment whether `€`, `¥` and `%` were checked too.

## Comments

2026-10-01, agent cf-77.

### The measurement

Chrome 154, headless on macOS, from a scratch page that loads the files and rules the demo pages
serve (`samples/ExGrid.DemoPages/wwwroot/fonts/roboto`, Roboto v51, one variable file per subset),
inlined so that nothing else could answer. 14px, `font-variant-numeric: tabular-nums`. A glyph's
width is the wider of the glyph alone and a hundred of it in a row, divided by a hundred.
`CSS.getPlatformFontsForNode` confirmed that Roboto itself painted every glyph in the table.
Weight 400 is a cell, 500 the Wrapper's header, 600 a group or total row, and 700 bold.

Taking the wider of the two readings matters. Ticket 47's numbers (`%` 10.352, `£` 8.323, `)` 4.917)
match the run averages, and a glyph that kerns with itself reads short in a run: a run of `/`
averages 3.726px at 600, and one `/` alone is 5.328px.

| glyph | 400 | 500 | 600 | 700 |
|---|---|---|---|---|
| `0`–`9`, `$`, `€` | 7.875 | 7.969 | 8.000 | 8.047 |
| `£` | 8.156 | 8.250 | **8.281** | **8.328** |
| `₺` | 7.797 | 8.063 | 8.188 | 8.297 |
| `₫` | 8.109 | 8.094 | 8.094 | 8.094 |
| `¥` | 7.359 | 7.438 | 7.484 | 7.516 |
| `₹` | 7.250 | 7.313 | 7.344 | 7.375 |
| `-` | 3.875 | 4.766 | 5.141 | 5.516 |
| `%` | 10.266 | 10.313 | **10.344** | **10.359** |
| `#` | 8.625 | 8.453 | 8.375 | 8.297 |
| `−` | 8.016 | 7.891 | 7.828 | 7.781 |
| `+` | 7.953 | 7.781 | 7.703 | 7.641 |
| `/` | **5.781** | 5.469 | 5.328 | **5.203** |
| `)` | 4.875 | 4.906 | 4.906 | 4.922 |
| `(` | 4.797 | 4.859 | 4.875 | 4.906 |
| `.` | 3.703 | 3.906 | 3.984 | 4.063 |
| `,` | 2.766 | 3.141 | 3.297 | 3.453 |
| `:` | 3.391 | 3.703 | 3.828 | 3.953 |
| space, U+00A0 | 3.484 | 3.484 | 3.484 | 3.484 |
| `’` (de-CH's group separator) | 2.813 | 3.031 | 3.125 | 3.219 |
| U+202F (fr-FR's) | 1.750 | 1.750 | 1.750 | 1.750 |

**`€`, `¥` and `%` were checked.** `€` is the digit's width at every weight. `¥` is narrower. `%` is
10.344 at 600 and 10.359 at 700, inside the declared 10.4. So the wide class and the bold digit
class hold as declared.

**Two findings that were not predicted:**

- **`/` is 5.781px at weight 400, against a narrow class of 4.95.** ADR-0016 asks the widths to
  cover every painted variant, and says that this means "the boldest weight the grid itself
  paints". So the first measurement (2026-09-01) took weight 600 alone. Roboto is a variable face,
  though, and `/`, `#`, `+` and `−` narrow as it gets bolder. So the widest `/` is the regular
  cell's, not the total row's.
- **`£1,234,567.50` itself was never cut.** At 400, 500, 600 and 700 it paints 88.156, 90.031,
  90.813 and 91.594px, against an old estimate of 94.85 (98.15 bold). The separators, charged at
  4.95 and painted at 3.3 to 4.0, cover the 0.281px `£` is short.

  The text the old widths did cut was found by measuring every string ExSheet's built-in formats
  make: 3,015 strings, from Number, Currency, Percentage, Scientific, General, the Date and Time
  types and the built-in currency, under 24 cultures, at each weight. Of these, 76 strings were
  made only of digits, signs and separators and still painted wider than their estimate, at one
  weight or more:
  - a date with `/` at any regular weight (`1/1/2026` by 0.866px at 400; `12/31/2026` by 0.741
    at 600);
  - `£` with a single digit (`£0` by 0.281 at 600);
  - `₺` with a single digit (`₺0` by 0.172).

### What changed

- `RobotoDigitPx` 8.0 → **8.3**, over `£` at 8.281.
- `RobotoNarrowPx` 4.95 → **5.8**, over `/` at 5.781 (400).
- `RobotoBoldNarrowPx` 4.95 → **5.25**, over `/` at 5.203.
- The wide class (10.4), bold wide (10.4) and bold digit (8.33, over `£` at 8.328) were right, and
  stay.

With the new widths, none of the 3,015 strings that is made of digits, signs and separators paints
wider than its estimate, at any weight. The cost is an earlier `####`. A tabular digit is charged
3.75% more, and a separator 0.85px more, so `123,456,789,012.50` is charged 139.4px against
131.8 before; it paints 125.8 at 600. Under ExGrid.MudBlazor, ExSheet's column widths in
characters convert at 8.3px a character instead of 8.0 (`SheetColumns.PxOf`).

### Not covered by any class: a decision, reported rather than taken

Roboto draws some currency signs wider than the digit class, and four of them wider than the wide
class too. Capital and wide letters are the same:

| glyph | 400 | 500 | 600 | 700 | cultures |
|---|---|---|---|---|---|
| `₼` | 9.250 | 9.422 | 9.500 | 9.578 | az |
| `₽` | 9.297 | 9.609 | 9.750 | 9.891 | ru-RU |
| `¤` | 10.000 | 9.844 | 9.766 | 9.703 | the invariant culture |
| `₱` | 10.250 | 10.359 | 10.406 | 10.453 | en-PH, fil-PH |
| `₩` | 10.359 | 10.906 | 11.141 | 11.375 | ko-KR |
| `₦` | 11.094 | 11.250 | 11.328 | 11.391 | en-NG |
| `₪` | 10.797 | 11.563 | 11.891 | 12.219 | he-IL |
| `M` | 12.234 | 12.250 | 12.250 | 12.266 | month names, `AM`/`PM` |
| `W` | 12.422 | 12.328 | 12.297 | 12.250 | `en-WS` and `ko-KP`'s symbols |

With the new widths, 63 of the 3,015 strings still paint wider than their estimate, at one weight or
more. Every one of them holds one of these glyphs:
- a currency sign: `₦0` by 2.73px at 600, `₩0` by 2.75 at 700, `₱0` by 1.82 at 700;
- `AM` or `PM`: `8:08 AM` by 1.05 at 700;
- `฿` (th-TH), which Roboto does not draw, so a fallback face paints it: `฿0` by 1.60 at 600.

A month name escapes in the built-in Date types only because the `-` and the space beside it are
overcharged. `May` alone, as a custom `mmm` would show it, is 1.73px short at 600.

Letters and the wider currency signs have no class that covers them in any face. The core's
defaults have the same gap (below). Covering them needs a decision about ADR-0016's classes, so it
was not taken here.

### The core's defaults

system-ui was measured on macOS (SF). DejaVu Sans 2.37, Book and Bold from the nix store, was loaded
as a web font to stand in for Linux, so that 600 matches Bold as it does there.
- **They do not have `£`'s or `/`'s problem.** `£` is the digit's width in both faces. `/` is at
  most 4.547 (SF) and 5.125 (DejaVu), inside the narrow class of 6.398. Every digit, sign and
  separator is inside its class at 400 to 700.
- **They share the gap above.** `₩` is 12.688 in SF at 700 and 15.453 in DejaVu at 600, and `₪` is
  12.672 in DejaVu. Both are past the digit class, and `₩` is past the wide class (14.028) in
  DejaVu. Letters fall short as well: in DejaVu at 600, `11:11 AM` by 2.97px and `May` by 2.85.
- **The core's widths have no margin.** They equal the run averages (the DejaVu Bold digit is
  9.7413, declared 9.742). Chrome rounds a run's width up to 1/64px, so a string can paint up to
  0.012px past its estimate (`100%` in DejaVu at 600). A column set to the estimate's exact
  fractional width would cut it by that much.

### Tests

- Layer 1, `RobotoWidthTests` in `ExGrid.MudBlazor.Tests` (4 tests, 42 cases):
  - `£1,234,567.50` at the width where the old widths fit it and `£` at 8.281 does not, which is
    `####` now (ADR-0016 / ADR-0030);
  - each regular class over its widest glyph at 400, 500 and 600, and each bold class at 700;
  - the five strings the old widths cut, each charged at least its painted width.

  11 of the cases fail against the old widths.
- `MudTestContext`'s twelve-digit amount column is now 116px. The amount needs 115.6 at Roboto's
  8.3 and 132.9 at the core's 9.742, so the column still tells whose widths the grid is using.
- Layers 1 and 2 pass: ExGrid.Tests 871, ExGrid.Components 1248 (one skipped), ExGrid.MudBlazor.Tests
  130, ExSheet.Engine.Tests 2304, ExSheet.Components.Tests 533, ExSheet.MudBlazor.Tests 34.
- Layer 3, headless on this Mac, `appearance.spec.mjs` and `mud.spec.mjs` (which also matches
  `format-cells-mud.spec.mjs`), project `chrome`: 49 passed on WebAssembly and 49 passed on Server.
  DC-58 passes with the 99px column.

### Left for others

- **`/appearance`'s `#appearance-bold` "Fits" column is now 99px, not 97.** It was sized for ten
  regular digits at 8.0. At 8.3, the regular digits fit it exactly and the bold ones, at 8.33, need
  99.3, so DC-58 (`appearance.spec.mjs`) still tells the two apart.
- **ADR-0063 records Roboto's bold widths as 10.4 / 8.33 / 4.95.** The narrow one is now 5.25.
- **ADR-0016 equates "every painted variant" with "the boldest weight the grid itself paints".**
  That does not hold for a variable face (`/` above). The widths here take the widest at every
  weight painted. `CellTextMetrics`' summary repeats the boldest-weight wording.
- **Two core tests hold their own copy, `new(10.4, 8.0, 4.95, 14)`:**
  `tests/ExGrid.Tests/GridPresentationDefaultsTests.cs` and
  `tests/ExGrid.Components/PresentationDefaultsWiringTests.cs`. Their comments called it Roboto's
  measured widths. They now say it is Roboto's as first declared, and point at
  `MudExGridPresentation`. Only the comments changed.
