# 83: No glyph a Number Format can emit is charged below its width

Status: done

**What to build:** ADR-0016's note of 2026-10-01, "No glyph a Number Format can emit is charged below
its width". This comes from ticket 82's measurement (see its comment).
- After ticket 82, 63 of 3,015 strings from ExSheet's built-in formats, across 24 cultures, are still
  estimated as fitting when they do not.
- Each of them holds a glyph wider than its class:
  - a currency sign: `₼ ₽ ¤ ₱ ₩ ₦ ₪`;
  - a letter such as `M` or `W`, in `AM`/`PM` or a month's name.
- Such a number or date is cut instead of shown as `####`.

**Blocked by:** 82

- [x] **Decide the mechanism in the ticket's comment, and give the reason.** Either:
  - add a class for wide letters and symbols, as ADR-0016 added full-width as a fourth class; or
  - charge every glyph outside the measured classes at a width that covers it.
  Pick whichever errs towards `####` and costs the fewest early `####`s on ordinary numbers. Record
  the measured widths.
- [x] **The core's defaults** (system-ui on macOS, DejaVu on Linux) and **`ExGrid.MudBlazor`'s Roboto**
      both cover these glyphs, at every weight painted.
- [x] **Layer 1:** re-run ticket 82's 3,015 strings, or its tool, against the new estimate. None may
      paint past its estimate.
  - Name the test after ADR-0016 and principle 1.
  - Keep the corpus or the tool in the repo, so a later font change can re-run it.
- [x] **Say what stays out.** For example, glyphs that only a Custom format can emit, and how a
      Consumer's font is covered (ADR-0027's metrics-bearing obligation).

## Comments

2026-10-01, agent cf-83.

### The mechanism: an other class, charged at the widest glyph outside the measured ones

`CellTextMetrics` gains a fifth class, **other**: every glyph that is not wide, narrow or
full-width and is not in the digit class. It is charged the widest such glyph measured. The digit
class becomes a list: the digits, and the glyphs number formats put among them that measured at a
digit's width or under in every face. These are `$ £ ¥ ₹ ₺ ₫`, the exponent's `E`, the
hyphen-minus, `'` and `’`, U+00A0 and U+202F, and the direction marks U+200E, U+200F and U+061C
(he-IL's General puts U+200E before a sign). Before, every glyph not classed otherwise was
charged as a digit.

**Why this one, and not a class for wide letters and symbols:**

- **They differ on the glyphs nobody listed.** A class for wide letters and symbols has to list its
  members. Taken from the three faces measured here, the list is 75 glyphs:
  - most capitals;
  - `b d g h m n p q u w`, which DejaVu Sans Bold draws past its 9.75px digit, and their accented
    forms in the corpus (`ú û ü ğ ń`);
  - Cyrillic `б д ж и й л м р ф ю`, and Hebrew and Thai letters;
  - the eight currency signs `₩ ₪ ₦ ₱ ₽ ₼ ₴ ฿`.

  Every glyph left off the list would still be charged as a digit, which is the failure this
  ticket removes. It would come back with the next culture, or the next face. The other class needs
  no list: a glyph nobody foresaw is charged the widest glyph measured. Only the other class errs
  towards `####` for what was not measured.
- **On ordinary numbers, the other class costs 150 more early `####`s.**
  - Under either mechanism, 553 strings from the number formats show `####` earlier:
    - the 400 amounts in `₩ ₪ ₦ ₱ ₽ ₼ ₴ ฿`, 50 strings in each of eight cultures;
    - the 153 in `Kč`, `CHF` and `R$`, whose `K`, `C`, `H` and `R` a list would hold too.
  - The other class adds the 150 amounts in `Ft`, `kr` and `zł` (hu-HU, sv-SE, pl-PL). Their letters
    are no wider than a digit in any face measured. The other class charges each of them 11.4px more
    under the core's widths, and 8.3px more under Roboto's.
  - Every other number keeps its estimate.

  Principle 1 puts the first point ahead of the second. 150 amounts in three cultures showing
  `####` a little early cost a hover. A glyph missing from a list cuts a number.
- **On dates and times, it costs more, and by more.** 1,120 date and time strings show `####`
  earlier under the other class, and 991 under a list. For example:
  - `September 30, 2026` is charged 51.4px more under the core's widths (22.8 under a list), and
    37.3 more under Roboto's (16.6);
  - `30 září 2026` is charged 22.8px more, and nothing more under a list.
- **The totals:** 1,823 of the 3,015 strings show `####` earlier under the other class, and 1,544
  under a list.
- **How the strings were counted:** a string shows `####` earlier when its estimate rises at any
  weight, against the same widths with every glyph outside the classes charged as a digit. The
  core's widths and Roboto's give the same strings.
- **A third shape, not taken:** the other class, with the letters that measured at a digit or
  under in every face moved into the digit class (`a c e f i j k l o r s t v x y z`,
  `E F I J L`, and some Cyrillic and accented letters).
  - It wins the 150 amounts back, and still charges anything unmeasured the widest glyph.
  - But it makes the digit class a claim about letters in every face. Every font would then owe
    it, and a Consumer's font that measured its digits and not its letters would be charged its
    letters as digits.
  - It is the way to win the 150 back if they matter.

**Where the other class applies: only to a value that can become `####`.**

- `CellTextMetrics.For(ColumnType)` returns these metrics for a Number or Date. For a Text or
  Boolean it returns them with the other class at the digit.
- The grid reads `For(ColumnType.Text)` for:
  - a Text or Boolean cell's Auto width and Size to fit;
  - header labels, the Name Box, the Row Headings, the Size Tip, and action labels.

  None of these widen. What never hashes is cut visibly (ADR-0016), and a header's em of slack
  already bounds its letters.
- A Number or Date cell is charged the other class:
  - in its `####` decision;
  - in its Auto width and its fit, so a column fitted to a value never hashes it.

  The type is the cell's own, so it follows ExSheet's per-cell `CellType`. ExSheet's widening on
  entry already reads the default estimate, so a date widens its column enough to show it.
- Charging Text with the other class too would have widened every text column and every header:
  in the core, a letter costs 15.46px against 9.75. That would have changed what ADR-0016 decided
  about headers, so it was not done.
- The orchestrator accepted this scope on 2026-10-01, for ADR-0016 at merge. A Number or Date must
  not be cut, so it is charged the other class. Text is cut visibly with an ellipsis, as before, so
  it is not.

### The measured widths

Chrome 154, headless on macOS, device scale factor 1, tabular digits, by `tests/GlyphWidths/measure.mjs`
(committed this time; ticket 82's scratch page was not). It measures every corpus string, and every
glyph of the corpus, the Latin letters and every glyph `CellTextMetrics` names in a class, so the
digit list's claim is held to each face. A glyph is the wider of itself alone and a hundred in a
row, divided by a hundred. Every reading is rounded up to 1/64px. Regular is the widest at 400, 500
and 600, and bold is 700. Measuring again gave the same widths.

The widest glyph of each class:

| face | size | weight | wide | digit | narrow | other |
|---|---|---|---|---|---|---|
| system-ui (SF) | 14 | regular | `%` 13.844 | 9.063 | `(` 5.641 | `W` 13.797 |
| | 14 | bold | `%` 14.359 | 9.250 | `(` 5.859 | `W` 13.984 |
| | 12 | regular | `%` 12.000 | 7.906 | `(` 4.969 | `W` 11.953 |
| | 12 | bold | `%` 12.438 | 8.063 | `(` 5.156 | `W` 12.125 |
| DejaVu Sans | 14 | both | `%` 14.031 | 9.750 | `(` 6.406 | `W` `₩` 15.453 |
| | 12 | both | `%` 12.031 | 8.359 | `(` 5.484 | `W` 13.250 |
| Roboto | 14 | regular | `%` 10.344 | `£` 8.281 | `/` 5.781 | `W` 12.422 (400) |
| | 14 | bold | `%` 10.359 | `£` 8.328 | `/` 5.203 | `M` 12.266 |
| | 12 | regular | `%` 8.859 | `£` 7.109 | `/` 4.953 | `W` 10.656 |
| | 12 | bold | `%` 8.875 | `£` 7.141 | `/` 4.453 | `M` 10.516 |

DejaVu Sans is Book at 400 and 500 and Bold at 600 and 700, as on Linux, so its regular and bold
readings are the same. The other class's next widest:
- Roboto: `m` 12.281, `M` 12.250, `₪` 12.219 (700), `ж` 12.016 (700), `₦` 11.391 (700);
- SF: `m` 12.906 (700), `₩` 12.688 (700);
- DejaVu Sans Bold: `m` 14.594, `M` and `ж` 13.938.

**Declared**, a shade over each reading. The earlier values are in brackets:

| | wide | digit | narrow | other |
|---|---|---|---|---|
| core, 14px (Compact, Standard, Comfortable) | 14.04 (14.028) | 9.75 (9.742) | 6.41 (6.398) | 15.46 |
| core, 14px bold | 14.36 (14.35) | 9.75 (9.742) | 6.41 (6.398) | 15.46 |
| core, 12px (Excel) | 12.04 (12.024) | 8.36 (8.351) | 5.49 (5.484) | 13.26 |
| core, 12px bold | 12.44 (12.3) | 8.36 (8.351) | 5.49 (5.484) | 13.26 |
| Roboto, at 14px | 10.4 | 8.3 | 5.8 | 12.44 |
| Roboto bold, at 14px | 10.4 | 8.34 (8.33) | 5.25 | 12.28 |

Roboto's widths are stated at 14px, and the core scales them to 12px for Excel. So each covers both
sizes: `W` at 12px is 12.432 at 14, and `£` bold at 12px is 8.331.

**Three findings that were not predicted:**

- **The core's widths had no margin** (ticket 82 saw it). They were run averages, and a glyph alone
  is laid out to the next 1/64px:
  - DejaVu Sans Bold's digit is 9.75 against 9.742, `%` is 14.031 against 14.028, and `(` is 6.406
    against 6.398;
  - SF's bold `%` is 14.359 against 14.35.

  So `0` alone painted 0.008px past its estimate. The new margins cost `123,456,789,012.50` 0.16px.
- **Excel's 12px set was the 14px set scaled, and SF is optically sized.** At 12px, SF is wider than
  the scaled values:
  - its bold `%` is 12.438 against 12.3 declared;
  - its regular digit is 7.906, where 9.063 scales to 7.768.

  DejaVu Sans's 12px digit is 8.359 against 8.351. The Excel set is now measured at 12px.
- **Roboto's bold digit, 8.33, was 0.0006px short at 12px.** There `£` is 7.141, and the class
  scaled to 7.140. It is now 8.34. Ten bold digits still need 99.4px, so DC-58's 99px "Fits" column
  still tells regular from bold.

### Layer 1

- **`GlyphWidthCorpusTests`** (ExGrid.Tests, 66 cases) and **`RobotoGlyphWidthCorpusTests`**
  (ExGrid.MudBlazor.Tests, 33): named after ADR-0016 and principle 1. For each face, density and
  weight, no corpus string paints wider than its estimate, and no glyph wider than its class. A
  record measured from another corpus is refused. Weights 400 to 600 are held to the regular widths
  and 700 to the bold ones. Against the old widths, with the other class charged as a digit, all 64
  core cases and all 32 Roboto cases of the first two kinds fail.
- **`GlyphCorpusTests`** (ExSheet.Components): the corpus is still what the built-in formats paint,
  regenerated through the public `Sheet` API. On a mismatch it writes the new corpus beside the test
  binary for the README's steps.
- **`OtherClassTests`** (ExGrid.Tests): which glyphs are the other class and which stay digits;
  `For`; the allowance; the refusals; and that "11:11 AM", painted at 74.219px in DejaVu Sans Bold,
  is `####` at the width the digit charge gave it.
- **`OtherClassWidthTests`** (ExGrid.Components): an Auto Date column holds "September 30, 2026" at
  the other class's charge and paints it. An Auto Text column with the same text charges its letters
  at the digit. A Fixed Date column as wide as the digit charge paints `####`.
- **The tool is `tests/GlyphWidths`**: the corpus, `measure.mjs`, a record per face and platform,
  the reader the tests share, and a README. A later font change re-runs it.

### Tests that moved

- `SizeToFitTests` (two) and `GripsWithoutMenuTests`: a Text column's fit is its text estimate, so
  the expected widths are `For(ColumnType.Text)`'s.
- `HeadingDragTests`: the Size Tip's width is the same.
- `GridMetricsTests`, `BoldWidthTests` and `GridPresentationDefaultsTests` pin the declared widths.
- `CellTextMetricsClassTests`: `A` and halfwidth `ｱ` are the other class, which is 18 under the
  three-class form.
- No column a layer-2 test pins moved. `MudTestContext`'s 116px amount column still fits at
  Roboto's 8.3 and hashes at the core's 9.75 (133px).

Layers 1 and 2 pass: ExGrid.Tests 984, ExGrid.Components 1259 (one skipped), ExGrid.MudBlazor.Tests
164, ExSheet.Engine.Tests 2304, ExSheet.Components.Tests 540, ExSheet.MudBlazor.Tests 35.

Layer 3 was not run locally. Since 2026-10-01 it runs in CI only, on the merged branch's draft
pull request (#42). The specs this change bears on are:
- `appearance.spec.mjs`: DC-58's 99px bold column, and the Excel preset's 67px italic one;
- `mud.spec.mjs`;
- `format-keys.spec.mjs`: the date and time keys widen a column by the new estimate.

### What stays out

- **Glyphs only a Custom format emits**, such as quoted text or `‰`. They are charged the other
  class too, so they are covered as far as they are no wider than the widest glyph measured. They
  are not measured. `‰` measured 20.17px in DejaVu Sans Bold when ticket 82 measured it, past
  every class.
- **Cultures outside the 24**, on the same terms.
- **Fallback faces on another platform.** A glyph the face does not draw is measured in the
  fallback macOS chose:
  - Thai, in Thonburi;
  - Hebrew in Roboto, in Arial;
  - `₴` and `฿` in Roboto, in Helvetica Neue;
  - `₼` in DejaVu Sans, in Kefa.

  Linux and Windows choose other faces, and those are not measured.
- **Windows' `system-ui`**, Segoe UI, until a record from Windows is added. A run of `measure.mjs`
  there adds `system-ui.windows.json`, and the core's tests read it.
- **A Consumer's own font.** It is the metrics-bearing obligation (ADR-0027, ADR-0030), and the
  obligation now includes the other class:
  - `GridPresentationDefaults` and `MudExGridFont` take it;
  - without it, the class is charged twice the digit (`CellTextMetrics.OtherWidthAllowance`). The
    widest glyph measured is 1.59 digits, so that errs towards `####`;
  - the uniform form keeps one width for everything, as its contract says.

  `measure.mjs` can measure a Consumer's font as another face.

### For ADR-0016 (public shape, recorded by the orchestrator)

- `CellTextMetrics`:
  - a ten-number constructor, the eight-number one followed by `otherWidthPx` and `boldOtherWidthPx`;
  - `OtherWidthPx` and `BoldOtherWidthPx`;
  - `OtherWidthAllowance = 2`, used by the forms that do not state the class;
  - `For(ColumnType)`;
  - `WithBoldWidths` derives the bold other class again from the new bold digit;
  - an other width narrower than its digit is refused.
- `GridPresentationDefaults`: a constructor with `otherWidthPx` and `boldOtherWidthPx` after the bold
  widths, and the two properties. The older constructors derive them.
- `MudExGridPresentation.RobotoOtherPx` (12.44) and `RobotoBoldOtherPx` (12.28), and
  `RobotoBoldDigitPx` 8.34. `MudExGridFont` has optional `OtherWidthPx` and `BoldOtherWidthPx`.

**Text now out of date,** for the orchestrator:
- ADR-0016's tables and its Consequences bullet still say an unclassified glyph is a digit, and that
  the theme's digit covers every glyph its formats emit.
- It quotes 14.028 / 9.742 / 6.398, and "Excel's 12px set is that measurement scaled".
- ADR-0071 records Roboto's bold digit as 8.33.
