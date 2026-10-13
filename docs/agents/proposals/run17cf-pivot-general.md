# Run 17 Parts C to F, ExPivot's General and its captions: what the orchestrator writes

Branch `claude/run17cf-pivot-general`, from `main` (a533de01). Two rounds of evidence are answered here:

- **The seventeenth Windows run's Part E** (`verification/2026-10-11-windows-17/expivot.md`, items 4 and 16;
  `part-e/expivot-cells.md`, e16, and e05a, e05b, e08 and e15 for "the same value, shown differently";
  `part-e/records/excel-wide.json`), in Excel's English (United Kingdom) edition.
- **The readings of 2026-10-12 (R3)**, which put ExPivot beside the same English (UK) edition, and the Japanese one for
  R3 (b) and (d) (`verification/2026-10-12-windows-readings/report.md` on
  `claude/exsheet-windows-readings-2026-10-12`, 89e0b5ad); and **the English (US) record of the same day**, which read
  R3 (a), (b) and (d) again in Excel's English (United States) edition
  (`verification/2026-10-12-windows-pivot-en-us/report.md` on `claude/exsheet-windows-pivot-en-us-2026-10-12`,
  76c0067e; "the English (US) record" below).

Nothing here is applied to an ADR, `CONTEXT.md`, the Definition of Done, `docs/specs/expivot/excel-behaviours.md` or
the Docs Site. The text for each is the numbered list of edits below, in the repository's voice, as exact old -> new
blocks that apply in order at the branch tip (`docs/agent-runs/check-proposal.py`, git-ignored, checks them).

**Item 16 gates `0.1.0`** (ADR-0202: a gating disagreement is settled by correcting the ADR first, then the code).
The code is on this branch; edit 3 is the correction it follows, and goes in with it.

## Round 3 (2026-10-12): R3 and the English (US) record, point by point

| Point | Excel | ExPivot before | ExPivot now | Evidence |
|---|---|---|---|---|
| (a) Every Aggregation over a text-only field (e05c) | A (x, y) and B (z): `#DIV/0!` for Average, StdDev, Var, StdDevp and Varp; `0` for Sum, Max, Min, Product and Count Numbers; Count 2 and 1. C (two blanks): every cell empty, Count included. Grand Total likewise, Count 3 | the same, every cell | unchanged: **observed** | R3 (a), all 44 cells; the English (US) record, (a): "every other header and every cell is the same" |
| (b) The Field List's two instruction lines | each ends with a colon, in English (UK), English (US) and Japanese, read from pictures; UI Automation's names leave it out | `:` U+003A at the end of both, in English and in `PivotWords.Japanese` | unchanged: **observed**, with the measurement's caveat | R3 (b); the English (US) record, (b) |
| (c) A numeric Row field's Item label in General (e16b) | `1.23456789`, `1.23457E+11`, at the PivotTable's width and at forty characters; the cell holds the whole number (Value2 `123456789012.345`) | `1.23456789012345`, `123456789012.345` (`G15`) | **General by the rule a value cell is shown by**: at most eleven characters besides a minus sign, `-` for that sign in every culture; a copy carries the number in full | R3 (c); the English (US) record, (a), "for e16b" |
| (d) Count Numbers' caption | English (UK) and (US): `Count of Amount` chosen in Value Field Settings, `Count of Note2` beside a Count through COM. Japanese: `データの個数 / Amount`, chosen in the dialog and added through COM | English `Count of` (this branch's first round); Japanese `数値の個数 / {0}` | Japanese **`データの個数 / {0}`**; a repeated caption takes the next free number; a caption of the user's own is kept | R3 (a) and (d); the English (US) record, (a) and (d); run 17, items 4 and 14 |
| (e) The English (UK) edition's words | UK: `Max. of`, `Min. of`, `Summarise Values By`. US: `Max of`, `Min of`, `Summarize Values By`, `Summarize value field by` | the US edition's words | unchanged; the UK words are a **Known limitation** of `0.1.0` | R3 (a), R3 (d) and its record `steps-by-hand.jsonl` (`Summarise Values By`); the English (US) record, (a) and (d) |

**(a)** ADR-0060's table, columns "Records, but no number" and "Some value, none numeric", called the two right-hand
columns readings. R3 (a) observed both over text, all eleven Aggregations, and the seventeenth run's item 5 (a) had
observed "Records, but no number" for Average, StdDev and Var. Edit 2 records it; edit 8 rewrites
`excel-behaviours.md`'s item 5, whose "over no numbers are `#DIV/0!`" is so over text and not over blanks. The layer-1
tests that were marked readings now cite the observation, `Only_blanks_are_an_empty_cell` covers all eleven
Aggregations, and `ADR0060_Every_Aggregation_over_a_text_only_field_is_as_Excel_showed_e05c` pins e05c's 44 cells and
its headers as the English (US) edition gave them. No code changed.

**(b)** R3 (b) read each line's last glyph from Excel's own picture, enlarged four times. In English the colon's dots
stand one blank column after the last letter. In Japanese they begin four device pixels after `い`, where U+003A
began (three to five pixels) and not where U+FF1A began (about ten) when the same text was drawn at that size in Yu
Gothic UI and Meiryo UI; R3 says a full-width colon that Office draws narrower (proportional metrics) is not excluded
by this. UI Automation's names leave the colon out in every edition, so the character itself was never read, in any
edition. ExPivot's four words already end with U+003A, and
`ADR0061_The_Field_Lists_two_instruction_lines_end_with_a_colon` pins them, saying that no record read the code point. The seventeenth run's
`expivot.md` (item 27) left this "not settled by this reading", having no picture; edits 6 and 11 settle it. No code
changed.

**(c)** `ItemLabels` labels a number Item whose field has no `format` with `PivotGeneral.Text`, the rule this branch
built for values, where it wrote `G15`. An Item's key is its number, a `double` whatever the column (ADR-0060, "A
number is its value"), so a Decimal column's `1.23456789012345` is labelled from that `double`, as Excel holds it.
The same label reaches Filter…'s list and the report filter band, which read the engine's labels. A field's own
`format` still labels as written. ADR-0060's "Labels" said "by its shortest round-trip text", which the code never
did (it wrote 15 significant digits) and Excel does not show; edit 1 corrects it, and edit 3's General section names
Item labels.
- **A copy of a number Item's label carries the number in full** (found in review). The report source copied a
  label cell as its text in both flavours, so with the label in General the HTML held `1.23457E+11`, which Excel
  reads as 123,457,000,000. Excel's own label cell holds the whole number (R3 (c): Value2 `123456789012.345`). Each
  number Item's label now carries its number (an internal `PivotRowLabel.Number`, set where the report builder
  labels an Item), and `LocalPivotReportSource.CopyAsync` copies it as a value cell is copied: the painted text in
  the text flavour, the shortest round-trip number in the HTML. Only an Item's own label carries it: a subtotal's
  `<Item> Total`, a caption and a date part's words copy as their text. Pinned by
  `ItemTests.ADR0060_PV7_A_number_Items_copy_carries_the_number_in_full` (layer 1) and
  `PivotValueTextTests.ADR0060_PV7_A_number_Items_copy_carries_the_number_in_full` (layer 2), each failing without
  the change.
- **A column Item in a copy's header row: decided with the user on 2026-10-13** (round 4 below). A number Item
  standing in Columns heads its value columns with its label, and a copy with headers wrote the column's header, the
  same text in both flavours (ADR-0005, "Copy with headers"), so the HTML held `1.23457E+11`. A header now carries a
  raw form of its own, as a cell does (edit 16).
- **An Item and a value of the same number** (found in review). An Item is labelled from its key, a `double`; a
  value of a Decimal column is shown from its exact `decimal`. Round 1's decimal path built its two forms from fifteen
  significant digits but judged the closer one against the decimal's further digits, so `0.10000000049999999m`
  showed `1E-01` and `1.00000000049999999m` `1E+00` where their doubles show `0.100000001` and `1.000000001`. It
  now judges against the same fifteen digits it builds from
  (`PivotGeneralTests.ADR0060_A_decimals_closer_form_is_judged_from_its_fifteen_digits`). A `double` is still judged
  against its own value, as ExSheet's General judges it, so a decimal of more than fifteen significant digits whose
  fifteen digits fall exactly halfway between the two forms can still be shown otherwise than its double:
  `0.1000000004999999m` is `0.100000001`, its double `1E-01`
  (`ADR0060_A_decimal_and_its_double_can_part_where_fifteen_digits_tie`). The edge is stated, in the Change Note,
  edit 3 and PV-3's wording, rather than removed: agreement would need an Item to carry its
  decimal, against ADR-0060's "A number is its value" (an Integer 1, a Decimal 1.0 and a Double 1 are one Item), or
  the double path to leave ExSheet's rule. A sweep outside the suite over 300,000 decimals of up to 28 digits (ratios
  of money) found no difference between the two paths, before or after the change.

**(d)** The English caption was already `Count of {0}` (this branch's first round). `PivotWords.Japanese` now
captions Count Numbers `データの個数 / {0}`, as R3 (d) observed. Count Numbers keeps its own name, `数値の個数`, in
the Aggregation list, as R3 (d) read the Japanese list. The numbering was already "the next free one"
(`ValueCaptions.Resolve`: from 2 upward, past every caption taken and every Pivot Field's caption):
- **observed** for a second Value Field in English: `Count of Note2` (R3 (a), and the English (US) record, (a)) and
  `Sum of Amount2` (run 17, item 14, through COM). *The brief named "Sum twice" as by generalisation; the seventeenth
  run's item 14 observed it.*
- **by generalisation**: a third (`Count of Value3`), Count Numbers standing before the Count, and the number in the
  Japanese words (`データの個数 / Amount2`; R3 (d)'s Japanese pivots held one Value Field).
- **A caption of the user's own** (Value Field Settings' Custom Name, or `PivotValueField.Caption`) is kept as given.
  In a layout, a default it would repeat goes round it, wherever the two stand: an own `Count of Amount` after a
  Count makes that Count `Count of Amount2`, and an own `Count of Amount2` pushes the next default to `3`
  (`ADR0060_A_caption_the_user_set_is_kept_and_the_generated_ones_go_round_it`). Typed into Value Field Settings, a
  caption that another Value Field already shows is refused, "PivotTable field name already exists." (ADR-0060), with
  one exception (`ExPivot.FieldList.cs`, OK's handler): a caption typed equal to the edited Value Field's own default
  is taken as no caption of its own, so beside a Count of Amount, `Count of Amount` typed for a Count Numbers is not
  refused, and becomes `Count of Amount2`. All of this is ExPivot's rule as it stood, not observed: no run gave Excel
  a Custom Name.
- **Past a Pivot Field's caption too**: a default a Pivot Field's caption already holds takes the next number, as a
  caption of a Value Field's own may not repeat a Pivot Field's. ExPivot's rule, not observed; pinned by
  `ADR0060_A_default_caption_goes_past_a_Pivot_Fields_caption`, which fails if `ValueCaptions.Resolve` stops looking
  at Pivot Fields' captions (found in review).
- **Found, not fixed: the refusal does not hold under other words.** `PivotLayoutEdits.SetValueField` resolves the
  other Value Fields' captions with no `PivotOptions`, so in English words. Under `PivotWords.Japanese`, `合計 / Amount`
  typed as a Count's Custom Name beside a Sum of Amount, which shows `合計 / Amount`, is not refused: it is kept, and
  the Sum becomes `合計 / Amount2` (checked against this branch's engine; English refuses `Sum of Amount` there). The
  review found it wider: under Japanese, `Sum of Amount` is refused as taken beside a Sum shown as `合計 / Amount`.
  It was so before this branch. *Decided with the user on 2026-10-13, and fixed in round 4*: `SetValueField` takes the
  report's words (edit 17).

**(e)** By the user's rule of 2026-10-12, "read the US edition first", ExPivot's English is the US edition's, and keeps
`Max of`, `Min of` and `Summarize value field by`. Its Aggregation list is the US edition's list, word for word and in
its order (`ADR0060_The_English_words_are_Excels_English_US_editions`). Edit 5 is ADR-0060's note, edit 7 ADR-0202's
Known limitations line, edit 14 the page's sentence, and the Release Notes' line is below. No code changed: a comment
and the class's summary in `PivotWords.cs`, and the **Label** line of `src/ExPivot/README.md` (the package's readme),
say which edition the English is.

## Round 4 (2026-10-13): the user's three decisions

The three questions this proposal left open were put to the user, who chose each recommended option (2026-10-13).

| | Decided with the user | Built | Evidence |
|---|---|---|---|
| A1 | A copy-with-headers header gets a raw form of its own, as a cell has: the TSV keeps the painted text, the HTML carries the number in full | `ClipboardData.Assemble` gains `headerRawAt`; ExPivot gives a number column Item's leaf header its number | `ClipboardDataTests.A_header_with_a_raw_form_carries_it_in_the_html_only`, `…A_header_raw_form_of_null_copies_as_before`; `ItemTests.ADR0005_A_number_column_Items_header_carries_its_number_in_full` (with the JSON a server's report travels in); `PivotValueTextTests.ADR0005_A_number_column_Items_header_copies_in_full`; layer 3, `pivot-csv.spec.mjs`'s copy with headers of e16b's Key in Columns |
| A2 | Value Field Settings compares a typed caption with the other Value Fields' captions in the report's words | `PivotLayoutEdits.SetValueField(…, PivotOptions? options = null)`, passed from the Field List | `LayoutEditTests.ADR0060_Value_field_settings_refuses_a_caption_the_report_shows_in_its_words` and `…_does_not_refuse_a_caption_of_other_words`, each failing without the change; `PanelTests.ADR0060_Value_field_settings_refuse_a_caption_the_report_shows_in_Japanese`, failing when the Field List passes no options |
| A3 | The Japanese list word for Count is `個数`; the caption `データの個数 / …` stays | `["aggregation-count"] = "個数"` in `PivotWords.Japanese` | `JapaneseWordsTests.ADR0060_The_Japanese_list_names_Count_個数_and_the_caption_stays` |

**A1, the design** (mine; the decision is the user's):
- `ClipboardData.Assemble` takes an optional `Func<int, string?>? headerRawAt`, read only with `headerAt`. A header
  whose raw form is null writes its text into both flavours, exactly as before: ExGrid's and ExSheet's calls pass
  none, and `A_header_raw_form_of_null_copies_as_before` holds that the payload is identical.
- ExPivot.Engine: a value column's internal `HeaderNumber`, set where the report builder heads a column with a
  number Item (a collapsed or innermost column Item, its Value Fields not in Columns); `PivotReportCopyBlock` gains
  an init-only `HeaderRaws`, null unless some header of the rectangle has a raw form. It travels a server's JSON
  (source-generated, round-tripped in the test). A fetching report source and ExPivot refuse a block whose
  `HeaderRaws` does not match its `Headers`, as they refuse one whose rows do not.
- Only an Item's own leaf header carries a number. `<Item> Total`, `Grand Total`, captions and date parts' words
  copy as text. A column Item that heads a span above its Value Fields' captions is not in a copy's header row,
  which names leaf columns only.

**A2.** `ValueCaptions.Resolve` already took the options; `SetValueField` now passes them. Left out, they are the
English words, as before.

**A3.** One word. The Change Note covering the Japanese words says it.

## The first two rounds, finding by finding

| Finding | Excel (run 17) | Was | Now | Why |
|---|---|---|---|---|
| E-item-16 | General in a value cell shows at most eleven characters, at the PivotTable's width and at forty alike: `1.414213562`, `1.632993162`, `2.666666667`, `1.23456789`, `1.23457E+11`, `1.23457E-05` | `G15`: `1.4142135623731`, `123456789012.346`, `1.23456789012346E-05`, `123456789013.88` | **Excel's General fitted to eleven characters besides a minus sign**, the rule ExSheet's `NumberText.General(number, characters, culture)` implements at a column twelve characters wide or wider, with `-` for the minus sign in every culture | FOLLOW. ADR-0060: the pivot engine answers as Excel's PivotTable. The reading "at most 15 significant digits, as a cell does" is wrong about a cell too: a cell fits General to eleven characters (SH-20, ADR-0120's note of 2026-10-12) |
| E-item-4-caption | Count Numbers of Value beside Count of Value is captioned `Count of Value2` (the caption Excel's own `DataField` read back) | `Count Numbers of Value` | **`Count of Value`**, numbered as any repeated caption is: `Count of Value2` | FOLLOW. ADR-0060, "Captions and words": a Value Field is captioned as Excel captions it, and a repeated caption takes a number |

**The rule taken, not only the cells.** `src/ExPivot.Engine/PivotGeneral.cs` is ExSheet's General at a width of
twelve characters or more, ported line for line (the decimal form, the scientific form, the closer of the two, half
away from zero from fifteen significant digits), because `ExPivot.Engine` references no package but `ExGrid.Data`
(PV-1). Twelve, not eleven: ExSheet's limit is `min(width - (minus ? 1 : 0), 11)`, so at eleven a negative number
has ten characters besides its sign, and only from twelve is every number's limit the pivot's eleven.
`PivotValue.From` uses it wherever a Value Field has no `NumberFormat` and no Show Values As, and `ItemLabels` wherever
a number Item's field has no `format` (round 3); a number format of the field's own is applied as written, as before.
An exact `decimal` value is rounded from its own digits, never a `double`'s, so a sum of money is shown from the sum
itself. A value column narrower than the text is ExGrid's `####` (ADR-0016), unchanged: a pivot never fits General
narrower, as a cell does.

**The minus sign is `-` in every culture**, as ExSheet's General writes it (`NumberText.General`, fitted). `G15` wrote
the culture's own `NegativeSign`, which .NET's ICU data makes U+2212 `−` under sv-SE, fi-FI and nb-NO:
`−1,4142135623731` there is now `-1,414213562`. Only the decimal separator is the culture's. No run read Excel under
those cultures, so the sign there is by generalisation from ExSheet's rule; the first round's text said "and Excel's
General shows it", which no run observed, and is corrected here and in `PivotGeneral.cs`.
`ADR0060_The_minus_sign_is_a_hyphen_minus_in_every_culture` pins it in those three cultures, and first asserts that
each culture's own sign is U+2212, so the case cannot pass without telling the two apart;
`ADR0060_A_number_Items_minus_sign_is_a_hyphen_minus_in_every_culture` does the same for an Item's label.

**The two cannot drift apart silently.** `PivotGeneralTests` reads ExSheet's corpus,
`tests/ExSheet.Engine.Tests/ExcelCases/general-width.json`, and pins every General case at a column twelve
characters or wider (GW-004, 009, 010, 011, 012, 013, 023) to the pivot's text; a wide case the corpus gains fails
`ADR0060_ADR0047_Every_wide_general_width_case_is_pinned` until its number is named there. Beside that, a sweep
outside the suite compared `PivotGeneral.Text` with ExSheet's `NumberText.General` at widths 12, 40 and 100 over
799,918 numbers (random bit patterns, decimals of every magnitude, ratios), under the invariant culture and de-DE:
no difference. `ADR0060_A_number_Items_label_is_the_value_cells_General` holds an Item's label to the same function.

**What it costs, measured.** On CoreCLR (Release, a million values), General takes about 0.75 µs a `double`
where `G15` took 0.18, and 0.57 µs a `decimal` where `G15` took 0.08. A value cell is formatted when it is first
read, so a painted screen does not feel it; a copy of the million cells the cap allows formats each once, about half
a second more on CoreCLR. An Item is labelled once per report. Nothing gates on it (PV-21 is observational); it is
said here so that a slower large copy is not a surprise.

**A copy still carries the whole number (PV-7).** The HTML flavour, which Excel reads, holds the raw number
(`123456789012.34567`); the TSV holds what the cell paints (`1.23457E+11`), as ADR-0005 and Excel's own text flavour
do. The TSV held the painted `G15` text before, so it is shorter now, not differently made.

**Left as they are, and why:**

- **The Selection Summary under a General Focus** (`PivotSummary.cs`, and `LocalPivotReportSource.SummaryAsync`'s
  format, line 566): its figures are written `G15`, so they no longer read as the Focus cell paints (`1.414213562`
  painted, `Sum: 1.4142135623731` summarised). That is ADR-0130's rule, not item 16's, and ExSheet's status bar does
  the same over a General cell (`Sheet.FormatAs`). Changing ExPivot's alone would make the two products' status bars
  disagree over one number. `PivotSummary.cs`'s comment, which said the figures are shown "as the report shows that
  cell", now says so, and names the question. ask_excel below.
- **A number format of the field's own under sv-SE, fi-FI or nb-NO** still writes the culture's U+2212, as .NET's
  format strings do (`#,##0.00` gives `−1 234,57`), and so do the `G15` texts above (the Selection Summary, Show
  Details). Only General's sign follows ExSheet's here; whether Excel's number formats write `-` under those cultures
  was not asked.
- **Show Details' records** (`PivotDetailsSheet.cs`, `G15` where a field has no `format`) and **the sample in Value
  Field Settings' Number Format** (`ExPivot.FieldList.cs`, `G15` for an empty format; its number, `-1234.5678`,
  reads the same under either rule but for the sign under sv-SE, fi-FI and nb-NO): not observed, outside this
  area's files.
- **Value Field Settings' Custom Name box** shows the bare default when the Aggregation changes (`Count of Amount`),
  even where OK will number it (`Count of Amount2`, beside a Count of the field): R3 (d) observed the box only on a
  PivotTable with one Value Field. ask_excel below.

**If this branch is not in by the tag:** item 16 gates, so the tag waits for it. Count Numbers' caption is a wording
difference, which ADR-0202 lets ship as a Known limitation; the line would read: "A Count Numbers Value Field is
captioned `Count Numbers of <field>`, and in the Japanese words `数値の個数 / <field>`, where Excel captions it as
Count, `Count of <field>` and `データの個数 / <field>` (ADR-0060)." So would the Item labels: "A number Item is
labelled with up to 15 significant digits, where Excel labels it in General, at most eleven characters (ADR-0060)."

## Tests, each named with its ADR

- `tests/ExPivot.Engine.Tests/PivotGeneralTests.cs` (layer 1): the run's cells (e05a, e05b, e08, e16) one by
  one and as a report over e16's records; ExSheet's wide general-width cases read from its corpus; rounding that
  carries into a twelfth integer digit; a minus sign not one of the eleven (a reading, as ExSheet's); the culture's
  separator; `-` for the minus sign under sv-SE, fi-FI and nb-NO, whose own is U+2212, as text and as a report
  shows it (by generalisation); zero; exact decimals; a decimal's closer form judged from its fifteen digits
  (`0.10000000049999999` is `0.100000001`), and the tie where it and its double part; a decimal and its double
  agreeing over 20,000 values; never more
  than eleven characters over 20,000 doubles; a copy's raw form; a number format of the field's own left as written.
- `tests/ExPivot.Engine.Tests/ItemTests.cs` (layer 1, round 3): e16b's Items labelled `1.23456789` and `1.23457E+11`
  with their sums (R3 (c)); their copy carrying `1.23456789012345` and `123456789012.345`; Filter…'s list by the
  same labels; an Item's label equal to the value cell's General over
  seven numbers, never more than eleven characters; `-` under sv-SE, fi-FI and nb-NO; a field's `format` still
  labelling as written.
- `tests/ExPivot.Engine.Tests/PivotWordsTests.cs` (layer 1): Count and Count Numbers captioned `Count of Value` and
  `Count of Value2` (observed), Count Numbers alone `Count of Value` (observed), in either order (by
  generalisation); the Japanese `データの個数 / Value` alone (observed) and `データの個数 / Value2` beside a Count (by
  generalisation); the next free number, `…2` observed and `…3` by generalisation, and past a Pivot Field's caption
  (ExPivot's rule); a caption the user set kept, with
  the defaults going round it, in English and in Japanese (ExPivot's rule); the English (US) edition's words and its
  Aggregation list, in its order; the Field List's two lines ending with `:` in English and in Japanese.
- `tests/ExPivot.Engine.Tests/AggregationTests.cs`: the texts that pinned fifteen digits now pin Excel's eleven
  characters (`6.666666667`, `2.581988897`, `2.236067977`, `3.333333333`, `40.71428571`); round 3: the two
  right-hand columns cite their observation, blanks are an empty cell for all eleven Aggregations, and e05c whole.
- `tests/ExPivot.Engine.Tests/SortAndShowTests.cs` (one assertion): `Default_captions` expects `Count of Amount` for
  Count Numbers.
- `tests/ExPivot.Components/PivotValueTextTests.cs` (layer 2): e16 painted as Excel showed it; a copy's TSV the
  painted text and its HTML the full-precision numbers; Count and Count Numbers headed, and listed in Values, `Count
  of Amount` and `Count of Amount2`; round 3: the same in `PivotWords.Japanese`, `データの個数 / Amount` and
  `データの個数 / Amount2`; e16b's Items painted `1.23456789` and `1.23457E+11`, and copied as painted in the TSV and
  in full in the HTML.
- `tests/ExGrid.Browser/pivot-csv.spec.mjs` (layer 3): e16.csv given to `/pivot-csv` through its unknown-file path, as
  the run gave it, and painted under both Chromes as Excel showed it, each value whole in its column and never
  `####`; round 3: e16b.csv given the same way, its Schema suggested as the run's (Key a Decimal, Amount an Integer),
  Key moved to Rows from its menu, and its Items painted `1.23456789` and `1.23457E+11`. Rounds 1 and 2 ran it with
  Playwright's bundled Chromium; round 3 ran it with the installed Chrome and Edge, on both hosts (the report gives
  the counts).

## The edits, in order

The checker applies them one after another to the files at the branch tip: each old text must occur exactly once in
its file when its turn comes, on whole lines.

### 1. docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md

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
  - *(2026-10-12, decided with the user: read the US edition first.)* **The English is Excel's
    English (United States) edition's.** That edition captions `Max of Note` and `Min of Note`,
    lists `Sum, Count, Average, Max, Min, Product, Count Numbers, StdDev, StdDevp, Var, Varp`, and
    heads the dialog `Summarize value field by` (`verification/2026-10-12-windows-pivot-en-us`, (a)
    and (d)): ExPivot's words, in that order. The English (United Kingdom) edition writes `Max. of`,
    `Min. of`, `Max.`, `Min.` and `Summarise Values By` (the readings of 2026-10-12, R3 (a) and (d)).
    ExPivot does not bundle those, and that is a Known limitation of `0.1.0` (ADR-0202); a Consumer
    gives them through `Label`.
```

### 6. docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md

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

### 7. docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md

Old:

```
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
```

New:

```
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
  *(2026-10-12, decided with the user: read the US edition first.)* By that rule ExPivot's English words are
  Excel's English (United States) edition's, `Max of`, `Min of` and `Summarize`, so `0.1.0`'s Known limitations
  also hold the English (United Kingdom) edition's `Max. of`, `Min. of` and `Summarise`, which ExPivot does not
  bundle (ADR-0060's note of that date). A Consumer gives them through `Label`.
```

### 8. docs/specs/expivot/excel-behaviours.md

Old:

```
5. **Average, StdDev, Var** over no numbers are `#DIV/0!`; StdDev and Var over one number are
   `#DIV/0!`; StdDevp and Varp over one number are 0.
```

New:

```
5. **Over records whose every value is Blank**, every Aggregation is an empty cell, Count
   included. **Over values none of which is a number**, Average, StdDev, Var, StdDevp and Varp are
   `#DIV/0!`, Sum, Max, Min, Product and Count Numbers are 0, and Count counts the values. StdDev
   and Var over one number are `#DIV/0!`; StdDevp and Varp over one number are 0. *(Observed: (a)
   by the seventeenth Windows run, item 5 (a), for Average, StdDev and Var over blanks, and by the
   readings of 2026-10-12, R3 (a), for all eleven over text and over blanks, in the English (UK)
   and English (US) editions; (b) by the seventeenth run, item 5 (b). The reading said `#DIV/0!`
   over no numbers, which is so over text and not over blanks.)*
```

### 9. docs/specs/expivot/excel-behaviours.md

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
    Numbers of one field are "Count of Value" and "Count of Value2", and Count Numbers chosen in
    Value Field Settings is "Count of Amount", "データの個数 / Amount" in Japanese. Max and Min are
    "Max of Amount" and "Min of Amount" in the English (US) edition, whose words ExPivot's are; the
    English (UK) edition writes "Max. of" and "Min. of". A caption equal to a field's caption or
    another Value Field's is refused with "PivotTable field name already exists." — compared
    ignoring case. *(Observed: the captions by the seventeenth Windows run, items 4 and 14, through
    COM; Count Numbers', Max's and Min's by the readings of 2026-10-12, R3 (a) and (d), and the
    English (US) record of that day, (a) and (d). Not asked: the refusal.)*
```

### 10. docs/specs/expivot/excel-behaviours.md

Old:

```
16. **General** writes at most 15 significant digits, as a cell does.
```

New:

```
16. **General** shows at most eleven characters besides a minus sign, whatever the column's width, as a cell's
    General shows them in a column twelve characters wide or wider: decimals rounded to what fits, scientific where
    the integer part has twelve or more digits or where that is closer (`1.414213562`, `1.23457E+11`,
    `1.23457E-05`). A number Item's label is the same General (`1.23456789`, `1.23457E+11`). *(Corrected by the
    seventeenth Windows run: the reading said 15 significant digits, which is General written in full, not General
    shown. The Item labels observed by the readings of 2026-10-12, R3 (c).)*
```

### 11. docs/specs/expivot/excel-behaviours.md

Old:

```
27. **The Japanese edition's words**: `行ラベル`, `列ラベル`, `総計`, `集計`, `合計 / 金額`,
    `データの個数 / 地域`, `(空白)`, `(すべて)`, `(複数のアイテム)`, `値`, and the pane's
    `ピボットテーブルのフィールド`, each read against Excel's own screens.
```

New:

```
27. **The Japanese edition's words**: `行ラベル`, `列ラベル`, `総計`, `集計`, `合計 / 金額`,
    `データの個数 / 地域`, `(空白)`, `(すべて)`, `(複数のアイテム)`, `値`, and the pane's
    `ピボットテーブルのフィールド`, each read against Excel's own screens. Count Numbers is captioned
    as Count is, `データの個数 / Amount`, and the pane's two instruction lines end with a colon.
    *(Observed: the words by the seventeenth Windows run, item 27; Count Numbers' caption and the
    colon by the readings of 2026-10-12, R3 (d) and (b), the colon from pictures, as UI Automation's
    names leave it out.)*
```

### 12. docs/definition-of-done.md

Old:

```
| **PV-3** | MUST | Items: text is told apart ignoring case and labelled by the first spelling to arrive among the records present; numbers by value, across Integer, Decimal and Double columns; dates by their clock value; a Blank as `(blank)`. They are ordered Number, Date, Text, Boolean, Error, Blank, with Blank last in either direction and a declared order first (ADR-0060) | Layer 1 | every clause a named test |
```

New:

```
| **PV-3** | MUST | Items: text is told apart ignoring case and labelled by the first spelling to arrive among the records present; numbers by value, across Integer, Decimal and Double columns, and labelled by the field's format or, with none, in General from the Item's number, the rule a value is shown by (ADR-0060's note of 2026-10-12); dates by their clock value; a Blank as `(blank)`. They are ordered Number, Date, Text, Boolean, Error, Blank, with Blank last in either direction and a declared order first (ADR-0060) | Layer 1 + Layer 3 (`pivot-csv.spec.mjs`, e16b) | every clause a named test |
```

### 13. docs/definition-of-done.md

Old:

```
| **PV-7** | MUST | A value paints its engine text in its number format, and a copy carries its full-precision number. A number format the engine cannot write within its caps is refused by name, in the sample and on OK (ADR-0060) | Layer 1 + Layer 2 | as stated |
```

New:

```
| **PV-7** | MUST | A value paints its engine text in its number format, or, with none, in General as Excel's PivotTable shows it: at most eleven characters besides a minus sign, and `-` for that sign in every culture (ADR-0060's note of 2026-10-12). A copy carries its full-precision number, and so does a copy of a number Item's label. A number format the engine cannot write within its caps is refused by name, in the sample and on OK (ADR-0060) | Layer 1 + Layer 2 + Layer 3 (`pivot-csv.spec.mjs`, e16) | as stated |
```

### 14. samples/ExGrid.Docs/Pages/Pivot/PivotValues.razor

Old:

```
            <code>PivotAxis.Rows</code>, or drag Σ Values to Rows, to stack them instead. <code>Caption</code>
            gives a Value Field a name of its own, Excel's <em>Custom Name</em>.
```

New:

```
            <code>PivotAxis.Rows</code>, or drag Σ Values to Rows, to stack them instead. Each is captioned as
            Excel captions it: <code>Sum of Revenue</code>, and <code>Count of Revenue</code> for Count and Count
            Numbers alike; a caption a second one would repeat takes the next free number, <code>Count of
            Revenue2</code>. <code>Caption</code> gives a Value Field a name of its own, Excel's <em>Custom
            Name</em>, which is kept as given.
        </p>
        <p>
            The words are those of Excel's English (US) edition: <code>Max of Revenue</code>, <code>Min of
            Revenue</code>, <em>Summarize value field by</em>. <code>PivotWords.Japanese</code> gives the Japanese
            edition's, where Count and Count Numbers are both <code>データの個数 / Revenue</code>. The English (UK)
            edition's <code>Max. of</code>, <code>Min. of</code> and <em>Summarise</em> are not bundled, a known
            limitation of 0.1.0: <code>Label</code> gives them by id, as any word (<code>caption-max</code>,
            <code>aggregation-max</code>, <code>summarize-by</code>).
```

### 15. samples/ExGrid.Docs/Pages/Pivot/PivotValues.razor

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
            <code>1.23457E+11</code>. Its minus sign is <code>-</code> in every culture. A field's
            <code>format</code> in its declaration labels its Items when it stands in Rows or Columns; without one,
            a number Item is labelled in the same General, as Excel labels it.
```

No Example changes: every Docs Site Example and Showcase gives its Value Fields a `NumberFormat`, none puts a number
field in Rows or Columns without a `format`, and none uses Count Numbers or the Japanese words with it, so none shows
anything new and no GIF is re-recorded.

### 16. docs/adr/0005-copy-refuses-rather-than-truncates.md

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

### 17. docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md

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

### 18. docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md

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

### 19. docs/definition-of-done.md

Old:

```
| **CP-17** | MUST | Copy with headers emits the column's **declared `Header`** as one extra row — the first TSV line and a `<th>` row in `text/html` — for a disjoint selection in either orientation, and never the truncated paint (ADR-0005) | Layer 1 for the assembly, Layer 3 for the real clipboard | one header row, in both formats, naming the covered columns in emission order |
```

New:

```
| **CP-17** | MUST | Copy with headers emits the column's **declared `Header`** as one extra row — the first TSV line and a `<th>` row in `text/html` — for a disjoint selection in either orientation, and never the truncated paint. A header with a raw form of its own carries it in the `<th>`, as a cell carries its raw value, and its text in the TSV (ADR-0005; its note of 2026-10-13) | Layer 1 for the assembly, Layer 3 for the real clipboard (`features.spec.mjs`; `pivot-csv.spec.mjs` for a raw form) | one header row, in both formats, naming the covered columns in emission order |
```

### 20. samples/ExGrid.Docs/Pages/Grid/GridClipboard.razor

Old:

```
                <strong>Two formats go on the clipboard.</strong> The text each cell shows, its <code>format</code>
                applied, as plain text; and the raw values as an HTML table, so Excel receives every digit of an
                amount the grid displays rounded. A cell showing <code>####</code> copies its value, never the hashes.
```

New:

```
                <strong>Two formats go on the clipboard.</strong> The text each cell shows, its <code>format</code>
                applied, as plain text; and the raw values as an HTML table, so Excel receives every digit of an
                amount the grid displays rounded. A cell showing <code>####</code> copies its value, never the hashes.
                <em>Copy with headers</em> puts each column's header in both. When you answer a copy yourself,
                <code>ClipboardData.Assemble</code> makes the two formats from each cell's shown and raw text, and a
                header that shows a number rounded can carry the number in full for the HTML through
                <code>headerRawAt</code>, as ExPivot's number column Items do.
```

### 21. samples/ExGrid.Docs/Pages/Pivot/PivotValues.razor

Old:

```
            text a cell paints is not what a copy carries: copying puts the full-precision value on the
            clipboard, so Excel receives the number, not its rounding.
```

New:

```
            text a cell paints is not what a copy carries: copying puts the full-precision value on the
            clipboard, so Excel receives the number, not its rounding. So does a number Item's label, and a
            number column Item's header in a copy with headers: <code>1.23457E+11</code> on screen and in the
            plain text, <code>123456789012.345</code> in the HTML that Excel reads.
```

## The Release Notes' Known limitations line (for `0.1.0`)

Under "Known limitations" (ADR-0202), linking the page edit 14 writes:

- **ExPivot:** The English words are Excel's English (US) edition's, `Max of`, `Min of` and `Summarize`; the English
  (UK) edition's `Max. of`, `Min. of` and `Summarise` are not bundled, and a Consumer gives them through `Label`
  ([Values](expivot/values#aggregations); ADR-0060).

## Change Notes written

- `docs/releases/unreleased/run17cf-pivot-general-general-eleven-characters.md`: ExPivot, Behaviour (round 1).
- `docs/releases/unreleased/run17cf-pivot-general-count-numbers-caption.md`: ExPivot, Behaviour (round 1; round 3
  adds the Japanese caption).
- `docs/releases/unreleased/run17cf-pivot-general-item-labels-general.md`: ExPivot, Behaviour (round 3).
- `docs/releases/unreleased/run17cf-pivot-general-header-raw-form.md`: ExGrid, Added (round 4, A1).
- `docs/releases/unreleased/run17cf-pivot-general-column-item-header-copy.md`: ExPivot, Behaviour (round 4, A1).
- `docs/releases/unreleased/run17cf-pivot-general-caption-refusal-words.md`: ExPivot, Fixed (round 4, A2).
- `run17cf-pivot-general-count-numbers-caption.md` also says the Japanese list word `個数` (round 4, A3).

## For the merge

This branch starts at a533de01, before main's #89 (2265cda6, ADR-0201), which added each shipped project's
`PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` and made the engine's report classes internal. The branch adds
no PublicAPI file (an add/add of the same path would conflict). After the merge, the build fails on RS0016 and RS0017
until these lines are written, in main's format, into the files main added:

`src/ExGrid/PublicAPI.Unshipped.txt`, replace:

```
static ExGrid.Clipboard.ClipboardData.Assemble(ExGrid.Clipboard.CopyPlan! plan, System.Func<int, int, string!>! displayAt, System.Func<int, int, string!>! rawAt, System.Func<int, string!>? headerAt = null) -> (string! Text, string! Html)
```

with:

```
static ExGrid.Clipboard.ClipboardData.Assemble(ExGrid.Clipboard.CopyPlan! plan, System.Func<int, int, string!>! displayAt, System.Func<int, int, string!>! rawAt, System.Func<int, string!>? headerAt = null, System.Func<int, string?>? headerRawAt = null) -> (string! Text, string! Html)
```

`src/ExPivot.Engine/PublicAPI.Unshipped.txt`, add (after `ExPivot.Engine.PivotReportCopyBlock.Headers.init -> void`):

```
ExPivot.Engine.PivotReportCopyBlock.HeaderRaws.get -> System.Collections.Generic.IReadOnlyList<string?>?
ExPivot.Engine.PivotReportCopyBlock.HeaderRaws.init -> void
```

and replace:

```
static ExPivot.Engine.PivotLayoutEdits.SetValueField(ExPivot.Engine.PivotLayout! layout, int index, ExPivot.Engine.PivotValueField! value, System.Collections.Generic.IReadOnlyDictionary<string!, ExPivot.Engine.PivotFieldInfo!>! fields) -> ExPivot.Engine.PivotEditResult!
```

with:

```
static ExPivot.Engine.PivotLayoutEdits.SetValueField(ExPivot.Engine.PivotLayout! layout, int index, ExPivot.Engine.PivotValueField! value, System.Collections.Generic.IReadOnlyDictionary<string!, ExPivot.Engine.PivotFieldInfo!>! fields, ExPivot.Engine.PivotOptions? options = null) -> ExPivot.Engine.PivotEditResult!
```

Nothing else public changes: `PivotRowLabel.Number` and the value column's `HeaderNumber` are internal, and
`ItemLabels` is internal.

**Conflicts to expect** (`git merge-tree` of this branch's tip with origin/main):

- `src/ExPivot/PivotSummary.cs`: deleted on main (#89), modified here (round 1: a comment naming the Selection
  Summary's `G15` under a General Focus). Take main's deletion. The live path is
  `LocalPivotReportSource.SummaryAsync`, whose `G15` the proposal's "Left as they are" already names.
- `src/ExPivot.Engine/PivotReport.cs` and `PivotValue.cs` merge without conflict: main made `PivotReport`,
  `PivotReportRow`, `PivotReportColumn` and `PivotValue` internal, and this branch adds internal members and
  comments elsewhere in those files.
- After the merge, check that main's tests of the copy, the captions and the words still pass with this branch's
  answers: main's suites predate `Count of` for Count Numbers, General labels and the Japanese `個数`.


## What waits on a decision

Nothing. The three questions this section held were decided with the user on 2026-10-13 (round 4) and are built:

1. The Japanese Aggregation list names Count `個数`, as R3 (d) read it: edit 18.
2. Value Field Settings compares a typed caption in the report's words: edit 17.
3. A copy-with-headers header carries a raw form of its own: edits 16 and 19 to 21.

## ask_excel (for the next Windows pass)

- **A negative value in General**: Average of `-1` and `-1` and `0` over three records (`-0.666666666666667`). Is it
  `-0.666666667` (the sign not one of the eleven, as ExPivot reads it) or `-0.66666667`? And under sv-SE, is the sign
  `-` or U+2212?
- **The status bar over a General pivot cell**: select the StdDev cell holding √2 alone. Is Sum `1.414213562`,
  `1.414213562373`, or `1.4142135623731`? (ExPivot and ExSheet: `G15`, ADR-0130.)
- **The number in the Japanese edition**: a Count and a Count Numbers of one field. Is the second
  `データの個数 / 値2`, as the English edition numbers it?
- **Value Field Settings' Custom Name beside a Count**: on a PivotTable with Count of Amount and Sum of Amount, choose
  Count Numbers for the Sum. Does the box show `Count of Amount` or `Count of Amount2`? (ExPivot's box shows the bare
  default, and OK numbers it.)
- **A Custom Name another Value Field's default would take**: type `Count of Amount` as the Custom Name of a Sum, then
  add a Count of Amount. Is the new one `Count of Amount2`? (ExPivot: yes, by its own rule.)
- **The Japanese edition's Aggregation list after 数値の個数**: R3 (d) read it as far as `数値の個数, …`; the names of
  StdDev, StdDevp, Var and Varp were not read.
