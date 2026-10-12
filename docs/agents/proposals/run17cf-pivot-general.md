# Run 17 Parts C to F, ExPivot's General and Count Numbers' caption: what the orchestrator writes

Branch `claude/run17cf-pivot-general`, from `main` (a533de01). The seventeenth Windows run's Part E
(`verification/2026-10-11-windows-17/expivot.md`, items 4 and 16; `part-e/expivot-cells.md`, e16, and e05a, e05b,
e08 and e15 for "the same value, shown differently"; `part-e/records/excel-wide.json`) read two things ExPivot did
otherwise than Excel. Both follow Excel's answer. Nothing here changes an ADR, `CONTEXT.md`, the Definition of Done,
a spec or the Docs Site: the text for each is below, in the repository's voice, as exact old -> new edits.

**Item 16 gates `0.1.0`** (ADR-0202: a gating disagreement is settled by correcting the ADR first, then the code).
The code is on this branch; the ADR-0060 note below is the correction it follows, and goes in with it.

## What was done, finding by finding

| Finding | Excel (run 17) | Was | Now | Why |
|---|---|---|---|---|
| E-item-16 | General in a value cell shows at most eleven characters, at the PivotTable's width and at forty alike: `1.414213562`, `1.632993162`, `2.666666667`, `1.23456789`, `1.23457E+11`, `1.23457E-05` | `G15`: `1.4142135623731`, `123456789012.346`, `1.23456789012346E-05`, `123456789013.88` | **Excel's General fitted to eleven characters besides a minus sign**, the rule ExSheet's `NumberText.General(number, characters, culture)` implements at a column twelve characters wide or wider, with `-` for the minus sign in every culture | FOLLOW. ADR-0060: the pivot engine answers as Excel's PivotTable. The reading "at most 15 significant digits, as a cell does" is wrong about a cell too: a cell fits General to eleven characters (SH-20, ADR-0120's note of 2026-10-12) |
| E-item-4-caption | Count Numbers of Value beside Count of Value is captioned `Count of Value2` (the caption Excel's own `DataField` read back) | `Count Numbers of Value` | **`Count of Value`**, numbered as any repeated caption is: `Count of Value2` | FOLLOW. ADR-0060, "Captions and words": a Value Field is captioned as Excel captions it, and a repeated caption takes a number |

**The rule taken, not only the cells.** `src/ExPivot.Engine/PivotGeneral.cs` is ExSheet's General at a width of
twelve characters or more, ported line for line (the decimal form, the scientific form, the closer of the two, half
away from zero from fifteen significant digits), because `ExPivot.Engine` references no package but `ExGrid.Data`
(PV-1). Twelve, not eleven: ExSheet's limit is `min(width - (minus ? 1 : 0), 11)`, so at eleven a negative number
has ten characters besides its sign, and only from twelve is every number's limit the pivot's eleven.
`PivotValue.From` uses it wherever a Value Field has no `NumberFormat` and no Show Values As; a number format of the
field's own is applied as written, as before. An exact `decimal` is rounded from its own digits,
never a `double`'s, so a sum of money is shown from the sum itself. A column narrower than the text is ExGrid's
`####` (ADR-0016), unchanged: a pivot never fits General narrower, as a cell does.

**The minus sign is `-` in every culture**, as ExSheet's General writes it (`NumberText.General`, fitted) and Excel's
General shows it. `G15` wrote the culture's own `NegativeSign`, which .NET's ICU data makes U+2212 `−` under sv-SE,
fi-FI and nb-NO: `−1,4142135623731` there is now `-1,414213562`. Only the decimal separator is the culture's.
`ADR0060_The_minus_sign_is_a_hyphen_minus_in_every_culture` pins it in those three cultures, and first asserts that
each culture's own sign is U+2212, so the case cannot pass without telling the two apart.

**The two cannot drift apart silently.** `PivotGeneralTests` reads ExSheet's corpus,
`tests/ExSheet.Engine.Tests/ExcelCases/general-width.json`, and pins every General case at a column twelve
characters or wider (GW-004, 009, 010, 011, 012, 013, 023) to the pivot's text; a wide case the corpus gains fails
`ADR0060_ADR0047_Every_wide_general_width_case_is_pinned` until its number is named there. Beside that, a sweep
outside the suite compared `PivotGeneral.Text` with ExSheet's `NumberText.General` at widths 12, 40 and 100 over
799,918 numbers (random bit patterns, decimals of every magnitude, ratios), under the invariant culture and de-DE:
no difference.

**What it costs, measured.** On CoreCLR (Release, a million values), General takes about 0.75 µs a `double`
where `G15` took 0.18, and 0.57 µs a `decimal` where `G15` took 0.08. A value cell is formatted when it is first
read, so a painted screen does not feel it; a copy of the million cells the cap allows formats each once, about half
a second more on CoreCLR. Nothing gates on it (PV-21 is observational); it is said here so that a slower large copy
is not a surprise.

**A copy still carries the whole number (PV-7).** The HTML flavour, which Excel reads, holds the raw number
(`123456789012.34567`); the TSV holds what the cell paints (`1.23457E+11`), as ADR-0005 and Excel's own text flavour
do. The TSV held the painted `G15` text before, so it is shorter now, not differently made.

**Left as they are, and why:**

- **A Number Item's label** (`ItemLabels.cs`, `G15`, or the field's `format`): not observed. The ask_excel item E-not-run
  reads one; until then it stays.
- **The Japanese caption of Count Numbers**, `数値の個数 / {0}`: not observed (item 27 ran without a Count Numbers
  field). It stays; `PivotWordsTests` pins it as a reading.
- **The Selection Summary under a General Focus** (`PivotSummary.cs`, and `LocalPivotReportSource.SummaryAsync`'s
  format, line 566): its figures are written `G15`, so they no longer read as the Focus cell paints (`1.414213562`
  painted, `Sum: 1.4142135623731` summarised). That is ADR-0130's rule, not item 16's, and ExSheet's status bar does
  the same over a General cell (`Sheet.FormatAs`). Changing ExPivot's alone would make the two products' status bars
  disagree over one number. `PivotSummary.cs`'s comment, which said the figures are shown "as the report shows that
  cell", now says so, and names the question. ask_excel below.
- **A number format of the field's own under sv-SE, fi-FI or nb-NO** still writes the culture's U+2212, as .NET's
  format strings do (`#,##0.00` gives `−1 234,57`), and so do the `G15` texts above (a Number Item's label, the
  Selection Summary, Show Details). Only General's sign follows ExSheet's here; whether Excel's number formats write
  `-` under those cultures was not asked.
- **Show Details' records** (`PivotDetailsSheet.cs`, `G15` where a field has no `format`) and **the sample in Value
  Field Settings' Number Format** (`ExPivot.FieldList.cs`, `G15` for an empty format; its number, `-1234.5678`,
  reads the same under either rule but for the sign under sv-SE, fi-FI and nb-NO): not observed, outside this
  area's files.

**If this branch is not in by the tag:** item 16 gates, so the tag waits for it. Count Numbers' caption is a wording
difference, which ADR-0202 lets ship as a Known limitation; the line would read: "A Count Numbers Value Field is
captioned `Count Numbers of <field>`, where Excel captions it `Count of <field>` (ADR-0060)."

## Tests, each named with its ADR

- `tests/ExPivot.Engine.Tests/PivotGeneralTests.cs` (new, layer 1): the run's cells (e05a, e05b, e08, e16) one by
  one and as a report over e16's records; ExSheet's wide general-width cases read from its corpus; rounding that
  carries into a twelfth integer digit; a minus sign not one of the eleven (a reading, as ExSheet's); the culture's
  separator; `-` for the minus sign under sv-SE, fi-FI and nb-NO, whose own is U+2212, as text and as a report
  shows it; zero; exact decimals; a decimal and its double agreeing over 20,000 values; never more than eleven
  characters over 20,000 doubles; a copy's raw form; a number format of the field's own left as written.
- `tests/ExPivot.Engine.Tests/PivotWordsTests.cs` (new, layer 1): e04's Count and Count Numbers captioned `Count of
  Value` and `Count of Value2`, in either order; Count Numbers alone `Count of Value`; the Japanese caption
  unchanged (a reading).
- `tests/ExPivot.Engine.Tests/AggregationTests.cs`: the texts that pinned fifteen digits now pin Excel's eleven
  characters (`6.666666667`, `2.581988897`, `2.236067977`, `3.333333333`, `40.71428571`).
- `tests/ExPivot.Engine.Tests/SortAndShowTests.cs` (outside the area's files, one assertion): `Default_captions`
  expects `Count of Amount` for Count Numbers.
- `tests/ExPivot.Components/PivotValueTextTests.cs` (new, layer 2): e16 painted as Excel showed it; a copy's TSV the
  painted text and its HTML the full-precision numbers; Count and Count Numbers headed, and listed in Values, `Count
  of Amount` and `Count of Amount2`.
- `tests/ExGrid.Browser/pivot-csv.spec.mjs` (outside the area's files, one test added, layer 3): e16.csv given to
  `/pivot-csv` through its unknown-file path, as the run gave it, and painted under both Chromes as Excel showed it,
  each value whole in its column and never `####`. Run with `pivot.spec.mjs` on the Server and the WebAssembly
  host, `--project=chrome` (Playwright's bundled Chromium, headed under xvfb): 88 passed and 7 skipped on each; the
  new test with `--repeat-each=3`: 6 of 6 on each. Run again at the second round's tip, `pivot-csv.spec.mjs` alone
  on each host: 14 of 14, and the new test with `--repeat-each=3` 6 of 6, on each.

## ADR-0060: what to write

**1. A new section, after "Show Values As"** (old -> new):

Old:

```
- A blank value stays blank, and an error value stays the error.
- A divisor that is zero, blank or an error gives `#DIV/0!`.
- The number format defaults to `0.00%`.

## The report's shape
```

New:

```
- A blank value stays blank, and an error value stays the error.
- A divisor that is zero, blank or an error gives `#DIV/0!`.
- The number format defaults to `0.00%`.

## A value in General *(2026-10-12, the seventeenth Windows run)*

A Value Field with no number format of its own, and no Show Values As, shows its values in **Excel's General as a
PivotTable shows it: at most eleven characters besides a minus sign**, whatever the column's width. Decimals are
rounded to what fits, and the form is scientific where the integer part has twelve or more digits, or where the
scientific form that fits is closer to the number than the decimal form that fits.

- The run read it at the PivotTable's own width and at forty characters alike: `1.414213562`, `1.632993162`,
  `2.666666667`, `1.23456789`, `1.23457E+11`, `1.23457E-05` (items 5, 8 and 16,
  `verification/2026-10-11-windows-17/part-e/expivot-cells.md`).
- It is ExSheet's General in a column twelve characters wide or wider (ADR-0047, SH-20), and `TEXT`'s `General`
  (ADR-0120's note of 2026-10-12). `ExPivot.Engine` references no ExSheet package (PV-1), so the rule is ported into
  it, and its tests pin it to ExSheet's general-width cases and to the run's cells, so the two cannot drift apart
  silently.
- Unlike a cell's, it is never fitted narrower: a column narrower than the text is `####` (ADR-0016).
- An exact `decimal` is rounded from its own digits, never a `double`'s.
- Whether a minus sign is one of the eleven was not asked. ExPivot follows ExSheet, where it is not (a reading).
- The minus sign is `-` in every culture, as ExSheet's General writes it and Excel's shows it; only the decimal
  separator is the culture's. The 15 significant digits written before took the culture's own sign, U+2212 `−` under
  sv-SE, fi-FI and nb-NO.
- A copy still carries the full-precision number (PV-7): the HTML flavour Excel reads holds the raw number, and the
  text flavour what the cell paints, as Excel's own text flavour does.
- *Corrected.* The reading was "at most 15 significant digits, as a cell does" (`excel-behaviours.md`, item 16).
  That is General written in full, as a concatenation or the Cell Editor writes it, not General shown in a cell,
  which a cell fits to eleven characters.
- Not observed, and left at 15 significant digits until a run reads them: a Number Item's label, the Selection
  Summary's figures under a General Focus (ADR-0130; ExSheet's are the same), Show Details' records, and the sample
  in Value Field Settings.

## The report's shape
```

**2. "Captions and words"** (old -> new):

Old:

```
- **A caption that a second Value Field would repeat takes a number**, such as `Sum of Amount2`, as
  in Excel.
```

New:

```
- **A caption that a second Value Field would repeat takes a number**, such as `Sum of Amount2`, as
  in Excel.
  - *(2026-10-12)* **Count Numbers is captioned as Count is**, `Count of Value`, so a Count and a Count Numbers of
    one field are `Count of Value` and `Count of Value2`, as Excel captioned them in the seventeenth Windows run
    (item 4). ExPivot wrote `Count Numbers of Value`, a caption Excel does not make. The bundled Japanese word,
    `数値の個数 / {0}`, was not observed, and stays.
```

## `docs/specs/expivot/excel-behaviours.md`: the readings corrected

Old:

```
16. **General** writes at most 15 significant digits, as a cell does.
```

New:

```
16. **General** shows at most eleven characters besides a minus sign, whatever the column's width, as a cell's
    General shows them in a column twelve characters wide or wider: decimals rounded to what fits, scientific where
    the integer part has twelve or more digits or where that is closer (`1.414213562`, `1.23457E+11`,
    `1.23457E-05`). *(Corrected by the seventeenth Windows run: the reading said 15 significant digits, which is
    General written in full, not General shown.)*
```

Old:

```
14. **Captions**: "Sum of Amount", "Count of Region", and a second Value Field of the same field
    and Aggregation "Sum of Amount2". A caption equal to a field's caption or another Value Field's
    is refused with "PivotTable field name already exists." — compared ignoring case.
```

New:

```
14. **Captions**: "Sum of Amount", "Count of Region", and a second Value Field of the same field
    and Aggregation "Sum of Amount2". Count Numbers is captioned as Count is, so a Count and a Count
    Numbers of one field are "Count of Value" and "Count of Value2" *(the seventeenth Windows run, item
    4)*. A caption equal to a field's caption or another Value Field's is refused with "PivotTable
    field name already exists." — compared ignoring case.
```

## The Definition of Done: PV-7

The criterion holds as written; its Statement can name General, so a reader does not take "its number format" to
leave General unsaid, and its Verification cell gains the layer-3 test that paints the run's e16 (old -> new, the
whole row):

Old:

```
| **PV-7** | MUST | A value paints its engine text in its number format, and a copy carries its full-precision number. A number format the engine cannot write within its caps is refused by name, in the sample and on OK (ADR-0060) | Layer 1 + Layer 2 | as stated |
```

New:

```
| **PV-7** | MUST | A value paints its engine text in its number format, or, with none, in General as Excel's PivotTable shows it: at most eleven characters besides a minus sign, and `-` for that sign in every culture (ADR-0060's note of 2026-10-12). A copy carries its full-precision number. A number format the engine cannot write within its caps is refused by name, in the sample and on OK (ADR-0060) | Layer 1 + Layer 2 + Layer 3 (`pivot-csv.spec.mjs`, e16) | as stated |
```

## The Docs Site

`samples/ExGrid.Docs/Pages/Pivot/PivotValues.razor`, "Number formats and ####" (old -> new):

Old:

```
            <code>#,##0</code>, <code>#,##0.00</code>, <code>0.0%</code>. Without one a value is shown as
            General, at most 15 significant digits. A field's <code>format</code> in its declaration labels its
            Items when it stands in Rows or Columns.
```

New:

```
            <code>#,##0</code>, <code>#,##0.00</code>, <code>0.0%</code>. Without one a value is shown in
            General as Excel's PivotTable shows it: at most eleven characters besides a minus sign, whatever the
            column's width, so √2 reads <code>1.414213562</code> and a sum with twelve or more integer digits
            <code>1.23457E+11</code>. Its minus sign is <code>-</code> in every culture, as Excel's General writes
            it. A field's <code>format</code> in its declaration labels its Items when it stands in Rows or
            Columns.
```

The same page, "Aggregations" (old -> new):

Old:

```
            <code>PivotAxis.Rows</code>, or drag Σ Values to Rows, to stack them instead. <code>Caption</code>
            gives a Value Field a name of its own, Excel's <em>Custom Name</em>.
```

New:

```
            <code>PivotAxis.Rows</code>, or drag Σ Values to Rows, to stack them instead. Each is captioned as
            Excel captions it: <code>Sum of Revenue</code>, and <code>Count of Revenue</code> for Count and Count
            Numbers alike; a caption a second one would repeat takes a number, <code>Count of Revenue2</code>.
            <code>Caption</code> gives a Value Field a name of its own, Excel's <em>Custom Name</em>.
```

No Example changes: every Docs Site Example and Showcase gives its Value Fields a `NumberFormat`, and none uses
Count Numbers, so none shows anything new and no GIF is re-recorded.

## Change Notes written

- `docs/releases/unreleased/run17cf-pivot-general-general-eleven-characters.md`: ExPivot, Behaviour.
- `docs/releases/unreleased/run17cf-pivot-general-count-numbers-caption.md`: ExPivot, Behaviour.

## ask_excel (for the next Windows pass)

- **A Number Item's label in General**: a row field of numbers holding `1.23456789012345` and `123456789012.345`.
  Does Excel label them `1.23456789` and `1.23457E+11`, as a value cell shows them? (ExPivot: `G15`.)
- **A negative value in General**: Average of `-1` and `-1` and `0` over three records (`-0.666666666666667`). Is it
  `-0.666666667` (the sign not one of the eleven, as ExPivot reads it) or `-0.66666667`?
- **The status bar over a General pivot cell**: select the StdDev cell holding √2 alone. Is Sum `1.414213562`,
  `1.414213562373`, or `1.4142135623731`? (ExPivot and ExSheet: `G15`, ADR-0130.)
- **The Japanese edition's Count Numbers caption** beside a Count of the same field: `数値の個数 / 値`, or
  `データの個数 / 値2` as the English edition numbers it?
