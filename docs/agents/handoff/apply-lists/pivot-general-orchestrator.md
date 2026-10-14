# ExPivot's General and its captions: the ADR, Definition of Done and glossary edits for the orchestrator

From `claude/run17cf-pivot-general` after its merges of `origin/main` (112a0ccb, then 73d2b2bc). The proposal's edits 1 to 7, 12, 13
and 16 to 19, in its order, renumbered here; the proposal's number is given under each. Its other edits (8 to 11,
`docs/specs/expivot/excel-behaviours.md`; 14, 15, 20 and 21, the Docs Site) are applied on the branch.
No glossary edit: the proposal had none, and no new term arrived.

Every Old text was checked against the merged tree, and main changed none of them. Edit 12 (the proposal's 18)
applies only after edit 6: its Old text is edit 6's last three lines.

**Rewritten 2026-10-14, after the merge of `origin/main` at c476ddf8, to the user's decision of that day** ("ExPivot's
English words also follow Excel's English (United Kingdom) edition"). Edit 5 now says both English editions are
followed, crediting the user with the reading of 2026-10-12 (to read the English (US) edition before deciding the
English words) and with the decision of 2026-10-14; what is built is marked *As built*. **The former edit 7
(ADR-0202) is dropped**: the English (UK) words are no longer a Known limitation, and the branch removed its line from
`docs/agents/release-cut/0.1.0-known-limitations.md`. The English cultures no run read (`en-AU`, `en-IE`, `en-IN`, …)
keep the English (US) words by generalisation; that is an open question for run 18
(`docs/specs/exsheet/run-18/pivot-general.md`, item 9), not a difference a run found, so it is not a Known limitation
either. The other numbers are unchanged, so there is no edit 7.

### 1. docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md

The proposal's edit 1.

Old:

```
- A number is labelled by the field's format under the report's culture, or by its shortest
  round-trip text.
```

New:

```
- A number is labelled by the field's format under the report's culture, or, with none, in General
  by the rule a value is shown by ("A value in General" below): at most eleven characters, so
  `1.23456789012345` is `1.23456789` and `123456789012.345` is `1.23457E+11`. *(2026-10-12,
  observed in the readings of 2026-10-12 (R3 (c)), in Excel's English (UK) and English (US)
  editions, for two positive Items. That a minus sign is not one of the eleven is a reading, as it
  is for a value. This said "by its shortest round-trip text", which the code never wrote: it wrote
  15 significant digits, and Excel shows neither.)*
```

### 2. docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md

The proposal's edit 2.

Old:

```
*("Records, but no number" means every value was Blank. The two right-hand columns are readings.)*
```

New:

```
*("Records, but no number" means every value was Blank. The two right-hand columns were readings.)*

*(2026-10-12.)* **Both right-hand columns are observed**, every row of them. The readings of
2026-10-12 (R3 (a), `e05c`) put all eleven Aggregations over a text field beside Excel: over text
alone ("Some value, none numeric") and over blanks alone ("Records, but no number"), every cell
was as the table says, Count over blanks empty too, in the English (UK) edition and in the English
(US) one. The seventeenth Windows run had observed "Records, but no number" for Average, StdDev and
Var (item 5 (a)).
```

### 3. docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md

The proposal's edit 3.

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
- An exact `decimal` is rounded from its own digits, never a `double`'s: its two forms are built from its fifteen
  significant digits, and the closer one is judged against those same digits. *(A first build judged them against
  the decimal's further digits, so `0.10000000049999999` showed `1E-01` where its `double` shows `0.100000001`.)*
  A `double` is judged against its own value, as ExSheet's General judges it.
- Whether a minus sign is one of the eleven was not asked. ExPivot follows ExSheet, where it is not (a reading).
- The minus sign is `-` in every culture, as ExSheet's General writes it; only the decimal separator is the
  culture's. No run read Excel under a culture whose own sign is another character, so that is by generalisation.
  The 15 significant digits written before took the culture's own sign, U+2212 `−` under sv-SE, fi-FI and nb-NO.
- A copy still carries the full-precision number (PV-7): the HTML flavour Excel reads holds the raw number, and the
  text flavour what the cell paints, as Excel's own text flavour does.
- **A number Item with no format of its field's own is labelled in the same General** ("Labels" above): Excel
  labelled `e16b`'s Items `1.23456789` and `1.23457E+11`, at the PivotTable's width and at forty characters (the
  readings of 2026-10-12, R3 (c); the same in the English (US) edition). The label is the Item's everywhere it is
  named: the report, Filter… and the report filter band. **A copy of the label carries the number in full**, as a
  value cell's does: the HTML flavour holds `123456789012.345`, the text flavour `1.23457E+11` (PV-7, ADR-0005);
  Excel's own label cell holds the whole number (R3 (c)). An Item is labelled from its number as a `double`, as it
  is keyed ("A number is its value"), where a value of a Decimal column is shown from its exact `decimal`: for a
  decimal of more than fifteen significant digits whose fifteen digits fall exactly halfway between the two forms
  that fit, a label and a value of the same number can choose differently (`1E-01` and `0.100000001` for
  `0.1000000004999999`).
- *Corrected.* The reading was "at most 15 significant digits, as a cell does" (`excel-behaviours.md`, item 16).
  That is General written in full, as a concatenation or the Cell Editor writes it, not General shown in a cell,
  which a cell fits to eleven characters.
- Not observed, and left at 15 significant digits until a run reads them: the Selection Summary's figures under a
  General Focus (ADR-0130; ExSheet's are the same), Show Details' records, and the sample in Value Field Settings.

## The report's shape
```

### 4. docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md

The proposal's edit 4.

Old:

```
- **A caption that a second Value Field would repeat takes a number**, such as `Sum of Amount2`, as
  in Excel.
```

New:

```
- **A caption that a second Value Field would repeat takes a number**, such as `Sum of Amount2`, as
  in Excel.
  - *(2026-10-12)* **Count Numbers is captioned as Count is**: `Count of Amount`, and in the
    Japanese words `データの個数 / Amount`. Excel captioned it so through COM beside a Count of the
    same field (`Count of Value2`, the seventeenth Windows run, item 4; `Count of Note2`, the
    readings of 2026-10-12, R3 (a)), and chosen alone in Value Field Settings (`Count of Amount`,
    R3 (d)), in its English (UK) edition and in its English (US) one
    (`verification/2026-10-12-windows-pivot-en-us`, (a) and (d)); its Japanese edition captioned it
    `データの個数 / Amount`, through the dialog and through COM (R3 (d)). ExPivot wrote
    `Count Numbers of Amount` and `数値の個数 / Amount`, captions Excel does not make. The Aggregation
    keeps its own name, Count Numbers and `数値の個数`, in Value Field Settings' list, as both
    editions list it.
  - *(2026-10-12)* **The number is the next free one**: from `2` upward, past every caption already
    taken. A second Value Field is observed, in English: `Sum of Amount2` (the seventeenth run, item
    14) and `Count of Note2` (R3 (a)). A third (`3`), and the number in the Japanese words
    (`データの個数 / Amount2`), are by generalisation. It also goes past every Pivot Field's caption,
    which a Value Field's caption may not repeat (below): ExPivot's rule, which no run observed.
  - *(2026-10-12)* **A caption of a Value Field's own is kept as given**, and a default it would
    repeat goes round it, wherever the two stand in Values: beside an own `Count of Amount`, the
    Count that would take it is `Count of Amount2`. Typed into Value Field Settings, a caption another
    Value Field already shows is refused, as below, with one exception: a caption typed equal to the
    edited Value Field's own default is no caption of its own, so beside a Count of Amount,
    `Count of Amount` typed for a Count Numbers is not refused and becomes `Count of Amount2`. All of
    this is ExPivot's rule; no run gave Excel a Custom Name.
```

### 5. docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md

The proposal's edit 5, **rewritten 2026-10-14** to the user's decision of that day: both English editions are
followed. The note of 2026-10-12 keeps the user's decision of that day (the English (US) edition read before the
English words were decided) and what the reading found; the note of 2026-10-14 quotes the decision, and what follows it
is as built.

Old:

```
- **The words a report paints are English by default**, and the Consumer can replace each one by
  its id. They include `Row Labels`, `Grand Total`, `Total`, `(blank)`, `(All)`,
  `(Multiple Items)`, `Values` and the Aggregations' names.
```

New:

```
- **The words a report paints are English by default**, and the Consumer can replace each one by
  its id. They include `Row Labels`, `Grand Total`, `Total`, `(blank)`, `(All)`,
  `(Multiple Items)`, `Values` and the Aggregations' names.
  - *(2026-10-12, decided with the user: Excel's English (United States) edition is read before the
    English words are decided.)* It was read that day. That edition captions `Max of Note` and
    `Min of Note`, lists `Sum, Count, Average, Max, Min, Product, Count Numbers, StdDev, StdDevp, Var,
    Varp`, and heads the list `Summarize value field by` (`verification/2026-10-12-windows-pivot-en-us`,
    (a) and (d)). The English (United Kingdom) edition captions `Max. of Note` and `Min. of Note`,
    lists `Max.` and `Min.`, and heads the list `Summarise value field by` (the readings of 2026-10-12,
    R3 (a) and (d)). Every other word of ExPivot's that the two read was the same in both, the Field
    List's instruction lines and Count Numbers' caption among them. The dialog's `Summarise Values By`
    tab and the sentence under its heading, which ExPivot does not paint, are spelt with an s in the
    English (United Kingdom) edition too.
  - *(2026-10-14, decided with the user: ExPivot's English words also follow Excel's English (United
    Kingdom) edition.)* **Both English editions are followed.** *As built:*
    - Under an `en-GB` UI culture (`CultureInfo.CurrentUICulture` as ExPivot's parameters are set),
      ExPivot paints the English (United Kingdom) edition's words where they differ, `Max.`, `Min.`,
      `Max. of {0}`, `Min. of {0}` and `Summarise value field by`, and the English (United States)
      edition's under any other UI culture.
    - Excel's words follow Office's display language, not the regional format: R3 read the United
      Kingdom's words with Windows' regional format set to en-US (the readings' report), and the
      English (US) record read the United States' once Office's display language was English (United
      States), `LanguageID(msoLanguageIDUI)` 1033 where it had been 2057. The UI culture is the
      display language's counterpart. The report's `Culture`, which formats its numbers and dates as
      the regional format does, changes no word.
    - The Consumer's `Label` still wins. `PivotWords.EnglishUnitedKingdom` gives the United Kingdom's
      words in one line, under any culture; `PivotWords.EnglishFor` keeps the United States' under
      `en-GB`.
    - No culture chooses the language (the Japanese words below, and ADR-0152): under every UI culture
      the words stay English until `Label` says otherwise. ExPivot composes the edition's words into
      the words it gives the engine as its parameters are set, so a report a server computes receives
      them as values in its explicit settings and never reads the server's culture (ADR-0152).
    - Only `en-GB` was observed. `en-AU`, `en-IE`, `en-IN` and every other English culture keep the
      United States' words, by generalisation; which English display languages Excel gives the United
      Kingdom's words is asked of the next Windows run (`docs/specs/exsheet/run-18/pivot-general.md`).
    - Value Field Settings' list below Count Numbers (StdDev, StdDevp, Var and Varp) was not seen in
      the English (United Kingdom) edition. It is the United States' list there, by generalisation
      from that edition's captions, `StdDev of Note` and the rest (R3 (a)).
```

### 6. docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md

The proposal's edit 6.

Old:

```
  - *(2026-10-09.)* They include the sentences ExPivot gives the grid to say for it — a refused Copy or
    Selection Summary — and a source's own refusal is said inside `source-refused`, never raw. As first
    built with the report-Window design, five such sentences were English only (found in review).
```

New:

```
  - *(2026-10-09.)* They include the sentences ExPivot gives the grid to say for it — a refused Copy or
    Selection Summary — and a source's own refusal is said inside `source-refused`, never raw. As first
    built with the report-Window design, five such sentences were English only (found in review).
  - *(2026-10-12.)* **The Field List's two instruction lines end with a colon**, in English and in
    Japanese: `Choose fields to add to report:` and `Drag fields between areas below:`,
    `レポートに追加するフィールドを選択してください:` and `次のボックス間でフィールドをドラッグしてください:`.
    ExPivot ends each with `:` U+003A. Observed from Excel's own pictures in its English (UK) and
    Japanese editions (the readings of 2026-10-12, R3 (b)) and its English (US) one
    (`verification/2026-10-12-windows-pivot-en-us`, (b)). UI Automation's names leave the colon out,
    so the character itself was not read. In Japanese the colon on screen sits where U+003A sits
    when the same text is drawn for comparison, and U+FF1A begins about 10 px later; a full-width
    colon that Office draws narrower (proportional metrics) is not excluded by that measurement.
```

### 8. docs/definition-of-done.md

The proposal's edit 12.

Old:

```
| **PV-3** | MUST | Items: text is told apart ignoring case and labelled by the first spelling to arrive among the records present; numbers by value, across Integer, Decimal and Double columns; dates by their clock value; a Blank as `(blank)`. They are ordered Number, Date, Text, Boolean, Error, Blank, with Blank last in either direction and a declared order first (ADR-0060) | Layer 1 | every clause a named test |
```

New:

```
| **PV-3** | MUST | Items: text is told apart ignoring case and labelled by the first spelling to arrive among the records present; numbers by value, across Integer, Decimal and Double columns, and labelled by the field's format or, with none, in General from the Item's number, the rule a value is shown by (ADR-0060's note of 2026-10-12); dates by their clock value; a Blank as `(blank)`. They are ordered Number, Date, Text, Boolean, Error, Blank, with Blank last in either direction and a declared order first (ADR-0060) | Layer 1 + Layer 3 (`pivot-csv.spec.mjs`, e16b) | every clause a named test |
```

### 9. docs/definition-of-done.md

The proposal's edit 13.

Old:

```
| **PV-7** | MUST | A value paints its engine text in its number format, and a copy carries its full-precision number. A number format the engine cannot write within its caps is refused by name, in the sample and on OK (ADR-0060) | Layer 1 + Layer 2 | as stated |
```

New:

```
| **PV-7** | MUST | A value paints its engine text in its number format, or, with none, in General as Excel's PivotTable shows it: at most eleven characters besides a minus sign, and `-` for that sign in every culture (ADR-0060's note of 2026-10-12). A copy carries its full-precision number, and so does a copy of a number Item's label. A number format the engine cannot write within its caps is refused by name, in the sample and on OK (ADR-0060) | Layer 1 + Layer 2 + Layer 3 (`pivot-csv.spec.mjs`, e16) | as stated |
```

### 10. docs/adr/0005-copy-refuses-rather-than-truncates.md

The proposal's edit 16. Unchanged by main: ADR-0005's "Copy with headers" bullets are as the proposal found them.

Old:

```
- **It goes into both formats** — a `<th>` row in `text/html` as well as the first TSV line.
  Excel prefers the HTML flavour, and a header row that reached the text editor but not the
  spreadsheet would miss the case the feature exists for.
```

New:

```
- **It goes into both formats** — a `<th>` row in `text/html` as well as the first TSV line.
  Excel prefers the HTML flavour, and a header row that reached the text editor but not the
  spreadsheet would miss the case the feature exists for.
  - *(2026-10-13, decided with the user: a header can carry a raw form of its own, as a cell does.)*
    The TSV line takes the header as shown; the `<th>` takes its raw form where it has one, and its
    text where it has none, as before. A pivot's number column Item heads its column in General
    (`1.23457E+11`) and is copied in full (`123456789012.345`), as its cells are
    ([ADR-0060](./0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)'s note of
    2026-10-12). `ClipboardData.Assemble` takes the raw form through `headerRawAt`, and ExPivot
    supplies it from the Item; ExGrid's own columns have none, and copy as before.
```

### 11. docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md

The proposal's edit 17.

Old:

```
- **A caption of its own that equals another Value Field's, or a Pivot Field's, is refused**, as
  Excel refuses it ("PivotTable field name already exists").
```

New:

```
- **A caption of its own that equals another Value Field's, or a Pivot Field's, is refused**, as
  Excel refuses it ("PivotTable field name already exists").
  - *(2026-10-13, decided with the user: a typed caption is compared with the other Value Fields'
    captions in the report's own words.)* `PivotLayoutEdits.SetValueField` takes the report's
    `PivotOptions`, and the Field List passes its own: under `PivotWords.Japanese`, `合計 / Amount`
    beside a Sum of Amount is refused, and `Sum of Amount` is not. It compared them in the English
    words, whatever the report's (found in review, 2026-10-12).
```

### 12. docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md

The proposal's edit 18.

Old:

```
    so the character itself was not read. In Japanese the colon on screen sits where U+003A sits
    when the same text is drawn for comparison, and U+FF1A begins about 10 px later; a full-width
    colon that Office draws narrower (proportional metrics) is not excluded by that measurement.
```

New:

```
    so the character itself was not read. In Japanese the colon on screen sits where U+003A sits
    when the same text is drawn for comparison, and U+FF1A begins about 10 px later; a full-width
    colon that Office draws narrower (proportional metrics) is not excluded by that measurement.
  - *(2026-10-13, decided with the user: Value Field Settings lists Count as `個数`, as the
    Japanese edition does; the caption stays `データの個数 / {0}`.)* The readings of 2026-10-12
    (R3 (d)) read the edition's list as `合計, 個数, 平均, 最大, 最小, 積, 数値の個数, …`; ExPivot
    listed `データの個数`, the caption's word.
```

### 13. docs/definition-of-done.md

The proposal's edit 19.

Old:

```
| **CP-17** | MUST | Copy with headers emits the column's **declared `Header`** as one extra row — the first TSV line and a `<th>` row in `text/html` — for a disjoint selection in either orientation, and never the truncated paint (ADR-0005) | Layer 1 for the assembly, Layer 3 for the real clipboard | one header row, in both formats, naming the covered columns in emission order |
```

New:

```
| **CP-17** | MUST | Copy with headers emits the column's **declared `Header`** as one extra row — the first TSV line and a `<th>` row in `text/html` — for a disjoint selection in either orientation, and never the truncated paint. A header with a raw form of its own carries it in the `<th>`, as a cell carries its raw value, and its text in the TSV (ADR-0005; its note of 2026-10-13) | Layer 1 for the assembly, Layer 3 for the real clipboard (`features.spec.mjs`; `pivot-csv.spec.mjs` for a raw form) | one header row, in both formats, naming the covered columns in emission order |
```
