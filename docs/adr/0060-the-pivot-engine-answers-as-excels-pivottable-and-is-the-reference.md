# The pivot engine answers as Excel's PivotTable does, and is the reference implementation

*(2026-10-25: [ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md) moves the pivot engine to TypeScript. ExPivot.Engine stays the reference until it
is removed at that ADR's step 3, and is the oracle the TypeScript engine is proved against; from then
the TypeScript engine is the reference this ADR names, and every rule below holds for it as written.)*

*(Numbered ADR-0059 until 2026-10-01. ExSheet's Pointing Scope took ADR-0058 first, and ExPivot's
ADRs moved up by one into the block [`docs/agents/numbering.md`](../agents/numbering.md) reserves
for them. Commit messages before then use the old numbers.)*

*(Proposed 2026-09-30 with [ADR-0059](./0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md),
and decided with the user the same day. The grilling held the rule this ADR is built on (Q1): both
the numbers and the gestures are Excel's, and ExPivot departs from Excel only where an ADR says how
and why. What the grilling changed is marked **Changed when decided**, each with its reason:*

- *values are read from a Snapshot's typed columns
  ([ADR-0064](./0064-the-snapshot-is-the-familys-immutable-data-held-in-columns.md));*
- *the Order Key;*
- *the date parts;*
- *only the parts that are asked for are accumulated;*
- *the Japanese words.*

*Several rules below are **readings** of Excel, not observations. Each reading is listed in
`docs/specs/expivot/excel-behaviours.md` with the procedure that observes it on Windows, as
ExSheet's readings were. An observation that disagrees with a reading rewrites the rule here, and
the engine follows.)*

`ExPivot.Engine` computes a Pivot Report under a Pivot Layout. It works from the Leaf Aggregates a
Pivot Source answers with
([ADR-0066](./0066-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md)), and
it computes those Leaf Aggregates from a Snapshot for the bundled source. It has no UI and depends
only on `ExGrid.Data`, so a server computes the same report the screen shows.
*(2026-10-12, [ADR-0201](./0201-the-public-surface-is-what-consumers-chromes-and-the-sibling-products-use.md),
decided with the user: the whole-report path is internal, `PivotEngine.Compute` and `PivotReport` with
what it names. A server computes the same report through `PivotReportSource`, the contract the screen
reads.)*

**Like `GridSource.From` for filter and sort
([ADR-0023](./0023-filter-and-sort-semantics-of-the-reference-implementation.md)), its behaviour is
the specification.** A server that answers a Pivot Source's questions is held to it.

The rule of [ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)
carries over: **the engine answers as Excel does, or says it cannot**. A report is read for money,
and the failure to rule out is a plausible total that is not Excel's.

## Items

An **Item** is one distinct value of a Pivot Field standing in Rows, Columns or Filters. Items are
taken over all the data, not only over the records that the other fields' filters leave.

*Changed when decided.* The proposal read each value through a delegate and took each Item's kind
from the value itself. **Values now come from a Snapshot column, so an Item's kind is its column's
kind** (ADR-0064). The Snapshot also now defines what a number, a date's clock value and a Blank
are. The Item rules that follow from those definitions are:

- **Text is compared ignoring case.** Excel puts `East` and `EAST` in one Item. The Item's label is
  the spelling of the first record that carries it: its column's dictionary is folded, and the first
  spelling to arrive among the records still present wins. Spaces are significant. *(Reading.)*
- **A number is its value.** An Integer 1, a Decimal 1.0 and a Double 1 are one Item. A non-finite
  Double is not a number Excel could hold, so its Item is `#NUM!`.
- **A date is its clock value** (ADR-0064). Two offsets of one instant are two Items, so a Consumer
  that means instants passes UTC.
- **A Boolean is `TRUE` or `FALSE`**, as Excel labels it.
- **A Blank is the Item `(blank)`.**
  - Read from objects, a Blank is an accessor that returned null, and **an empty string is a
    value**: an Item with an empty label, as ADR-0023 has it for filters.
  - Read from a CSV, an empty field is a Blank in every kind (ADR-0064, Q55).
- **A value of any other type is Text, by its invariant text**, when an untyped accessor declares
  the column Text. An enum is its name. *(Refined 2026-10-01: the proposal said the
  culture-formatted text. A Snapshot holds the text, and it is read independently of any
  report's culture, so the text is the invariant one. Two reports in two cultures show the same
  Items.)*

**The declared type decides only defaults**: where a ticked field goes, which Aggregation a new
Value Field takes, and how an Item is labelled. A Decimal, Double or Integer column is declared as a
Number field.

**Labels.**

- Text is labelled as it is.
- A number is labelled by the field's format under the report's culture, or, with none, in General
  by the rule a value is shown by ("A value in General" below): at most eleven characters, so
  `1.23456789012345` is `1.23456789` and `123456789012.345` is `1.23457E+11`. *(2026-10-12,
  observed in the readings of 2026-10-12 (R3 (c)), in Excel's English (UK) and English (US)
  editions, for two positive Items. That a minus sign is not one of the eleven is a reading, as it
  is for a value. This said "by its shortest round-trip text", which the code never wrote: it wrote
  15 significant digits, and Excel shows neither.)*
- A date is labelled by the field's format, or by the culture's short date, with the time when it
  has one.
- A Boolean is labelled `TRUE` or `FALSE`.
- A Blank is labelled `(blank)`.

**Hidden Items are written as keys**: the Item's kind and its invariant text (`1234.5`,
`2026-09-30T00:00:00`, `TRUE`, or the text itself). A saved Pivot Layout therefore reads back the
same Items under any culture, and a server can put them in a `WHERE`.

## Order

- **Items are ascending by default.**
  - The kinds come in this order: numbers, then dates, then text, then Booleans, then `#NUM!`, then
    `(blank)`.
  - Within a kind, numbers and dates are ordered by value, and `FALSE` comes before `TRUE`.
  - Text is ordered by the report culture's comparison ignoring case, and ties are broken ordinally,
    so the order is total.
  - *Excel has no separate date kind — a date is a number there — so the order across kinds is
    ExPivot's.*
- **Descending reverses the order**, and `(blank)` stays last, as it does in both directions for a
  Grid Source (ADR-0023).
- **A field may declare an Item order**, which is Excel's custom lists, such as the months of a
  year. The declared values come first, in the declared order, and the rest follow in the field's
  order.
- **A field may declare an Order Key.** *Changed when decided* (Q19, Q20, Q27). The user needs
  tenors in Rows or Columns for rate and credit delta reports — `ON`, `TN`, `1W`, `1M` … `1Y6M` …
  `30Y` — and no alphabetical or declared order holds them all.
  - The Order Key is a function from an Item's value to a key, called once per Item. Items are
    ordered by their keys, ascending, and ties fall back to the label.
  - A key function was chosen over a comparer. It always gives a total order, where a comparer that
    breaks transitivity breaks the sort quietly. It is tested on its own ("1Y6M" → 18 months). And
    it runs once per Item rather than once per comparison. The user's reading of it:
    "more functional, and easier to test".
  - **An Item the function gives no key (null) comes after the keyed ones**, in label order.
  - **A function that throws is refused, naming the field and the value**, for example "the Order
    Key of Tenor failed on '7Y'". An order that quietly fell back to the labels would be the
    plausible wrong answer.
  - **The key orders Items and never merges them.** `18M` and `1Y6M` stay two Items, side by side.
    A Consumer that means them as one Item writes them the same way in its data.
  - The key applies to ascending and descending sorts, and to the Item lists of Filter… and of the
    report filter band. It does not affect a sort by value.
  - The engine parses no tenor. The demo and the documentation show a tenor key.
  - **Execution side changed, 2026-10-06:** it runs where the report is computed. For a remote
    report, the application registers the function on the server and selects it by identifier;
    local computation still uses the local function. An unsupported policy is refused, never
    replaced with a different order. Culture and display words are explicit settings
    ([ADR-0152](./0152-report-apis-may-change-and-remote-reports-recover-their-baseline.md)).
    The earlier Leaf Aggregate boundary ran the function only beside the component and said
    the server never saw it; ADR-0151 moved report computation across that boundary.
- **A field may be sorted by a Value Field**, ascending or descending. An Item's place is then its
  value at its total across the other axis, as shown, after Show Values As. Blank and error values
  go last, and ties fall back to the label, ascending.

## Date parts

*Changed when decided* (Q3). The proposal left dates for the Consumer to derive, as the demo's Month
field did. **A field can now be declared as a part of a Date column in one line: its year, its
quarter or its month.**

- Each part is labelled as Excel labels it, in the report's words: `2026`, `Qtr3`, `Sep`.
- Each part is ordered by the calendar, never alphabetically.
- The part of a Blank is a Blank.

This is the common case of Excel's automatic date grouping, declared rather than inferred. Excel's
Group… command, which groups numbers into ranges and dates from within the report, is later
(ADR-0059's table).

## Hidden Items

- **A record whose Item is hidden in any placed field — in Rows, Columns or Filters — is left out of
  the report**: out of every cell, every subtotal and every grand total. That is Excel's behaviour
  for a PivotTable over a range; the "include filtered items in totals" option exists only for OLAP.
- **A field holds what is hidden, not what is shown**, so an Item that first appears in a later
  version of the data is shown, as it is by Excel's default.
- **Hiding every Item of a field is refused**, by name. Excel disables OK for it, and a report of
  nothing would read as "no data".

## Aggregation

A value is aggregated over the records where a row position and a column position cross. **A total
— a subtotal or a grand total — is computed from all its records, never from the totals below it.**
The grand total of an Average is therefore the average of the records, as in Excel. The Leaf
Aggregates keep this rule, because their parts combine exactly (ADR-0066).

A **number** is a value in a Decimal, Double or Integer column. Text that looks like a number is
text, because Excel does not sum numbers stored as text. A Boolean or a date is not a number here.
*(Excel sums date serials. Max of a date field is later: ADR-0059's table.)*

| Aggregation | Over the numbers | Records, but no number | Some value, none numeric |
|---|---|---|---|
| **Sum** | their sum | blank | `0` |
| **Count** | the count of values that are not Blank, numbers or not (Excel's `COUNTA`) | blank | the count |
| **Average** | sum ÷ count | blank | `#DIV/0!` |
| **Max**, **Min** | the largest, the smallest | blank | `0` |
| **Product** | their product | blank | `0` |
| **Count Numbers** | the count of numbers (`COUNT`) | blank | `0` |
| **StdDev**, **Var** | sample, over two or more; `#DIV/0!` for one | blank | `#DIV/0!` |
| **StdDevp**, **Varp** | population | blank | `#DIV/0!` |

*("Records, but no number" means every value was Blank. The two right-hand columns were readings.)*

*(2026-10-12.)* **Both right-hand columns are observed**, every row of them. The readings of
2026-10-12 (R3 (a), `e05c`) put all eleven Aggregations over a text field beside Excel: over text
alone ("Some value, none numeric") and over blanks alone ("Records, but no number"), every cell
was as the table says, Count over blanks empty too, in the English (UK) edition and in the English
(US) one. The seventeenth Windows run had observed "Records, but no number" for Average, StdDev and
Var (item 5 (a)).

- **No record at all is an empty cell**, for every Aggregation, Count included, as Excel leaves it.
- **Money stays exact.** An Integer or Decimal column is summed, averaged and compared exactly.
  - Its sum is a 64-bit integer at the column's scale, or a `decimal` where the column needs one
    (ADR-0064).
  - It falls back to `double`, Excel's own arithmetic, only when the exact sum overflows.
  - A Double column is summed in `double`.
  - Product and the four variance Aggregations are computed in `double`.
- **Error values** are `#DIV/0!` and `#NUM!`.
  - `#DIV/0!` comes from the table above, and from a Show Values As divisor that is zero or empty.
  - `#NUM!` comes from a non-finite number in the data, or from an overflow in `double`.
  - An error value is painted as its text, centred as Excel centres it, and copied as its text. It
    never becomes a number.
- **The default Aggregation of a new Value Field** is Sum for a field declared Number, and Count for
  every other field. *(Excel picks Count when a column holds any text or blank. ExPivot goes on the
  declared type instead.)*

## Show Values As

Show Values As is applied to a Value Field after aggregation. The first version has three:

- **% of Grand Total**: the value divided by the Value Field's grand total.
- **% of Column Total**: the value divided by its column's total, which is the value at the grand
  total row.
- **% of Row Total**: the value divided by its row's total, which is the value at the grand total
  column.

A divisor is the aggregate over the records its total covers, whether or not that total is shown.

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

- **The Compact form** is the default, as it is Excel's.
  - It has one label column, headed `Row Labels`, with each level indented by one em.
  - Each outer Item has a **group row**, which carries its subtotal when subtotals are at the top.
  - Every outer Item has a `±` button.
- **The Outline form** has one label column per row field, headed by the field's caption. Each outer
  Item has a group row, with its label in its own field's column.
- **The Tabular form** has one label column per row field and no group rows. An outer Item's label
  stands on the row of its first child, and subtotals are always at the bottom.
- **Repeat Item Labels** fills in the outer columns of every row in the Outline and Tabular forms.
- **Subtotals** are set per field.
  - A field's subtotal is either Automatic — the Value Field's own Aggregation over the group's
    records — or none. The innermost field has none, because its rows are its own.
  - **At the top** (the default), a subtotal is carried on the group row.
  - **At the bottom**, it is a row of its own, `<item> Total`.
  - The Tabular form puts subtotals at the bottom whatever the setting.
- **Grand totals** are a `Grand Total` row at the bottom and a `Grand Total` column at the right,
  each on unless switched off. *(Excel calls the row "grand totals for columns" and the column
  "grand totals for rows". The layout names what is painted: `GrandTotalRow`, `GrandTotalColumn`.)*
  Excel's four choices — off for both, on for both, rows only, columns only — are the two switches'
  four settings, and the Layout menu offers them under Excel's names
  ([ADR-0061](./0061-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md)).
- **Collapse is held per Item of a field**, as Excel's `ShowDetail` is.
  - Each field has a field-wide default, which Expand / Collapse Entire Field sets.
  - Collapsing `East` collapses it wherever it appears.
  - A collapsed Item's row carries its totals, in every form and whatever the subtotal setting.
  - A collapsed column Item becomes one column.
  - The innermost field cannot be collapsed.
- **Column Items are Header Groups** (ADR-0032). An outer Item's rectangle covers its descendants'
  columns. Its subtotal column follows it, outside the rectangle, headed `<item> Total`, and that
  header stretches up to the rectangle above it.
- **With no row field**, there is no label column, and one row of totals.
- **With no column field**, there is one value column per Value Field, headed by its caption.

## Σ Values

With two or more Value Fields, their captions form a pseudo-field, **Σ Values**. It stands in
Columns, where Excel puts it, or in Rows.

- **In the first version, Σ Values is always the innermost level of its Area.** Excel lets it stand
  anywhere. At an outer position, a subtotal splits into one per Value Field above the fields it
  totals, which needs rules of its own. Those rules wait (ADR-0059's table).
- **In Columns**, every column position splits into one column per Value Field, headed by its
  caption. A subtotal's columns and the grand total's columns stand under a rectangle labelled
  `<item> Total` or `Grand Total`.
- **In Rows**, an Item's row carries no values. One row per Value Field follows it, labelled by the
  Value Field's caption, one level deeper. A subtotal and the grand total are one row per Value
  Field, labelled `<item> <caption>` and `Total <caption>`.

## Captions and words

- **A Value Field is captioned `<Aggregation> of <field caption>`**, such as `Sum of Amount`, unless
  it has a caption of its own.
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
- **A caption of its own that equals another Value Field's, or a Pivot Field's, is refused**, as
  Excel refuses it ("PivotTable field name already exists").
  - *(2026-10-13, decided with the user: a typed caption is compared with the other Value Fields'
    captions in the report's own words.)* `PivotLayoutEdits.SetValueField` takes the report's
    `PivotOptions`, and the Field List passes its own: under `PivotWords.Japanese`, `合計 / Amount`
    beside a Sum of Amount is refused, and `Sum of Amount` is not. It compared them in the English
    words, whatever the report's (found in review, 2026-10-12).
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
    Kingdom) edition; the UI culture chooses the edition, and the Consumer's `Label` wins.)*
    **Both English editions are followed.** *As built:*
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
- **The words of Excel's Japanese edition are bundled.** *Changed when decided* (Q6).
  - They are the words that edition shows: `行ラベル`, `総計`, `合計 / 金額`, `(空白)`, `(すべて)`,
    `(複数のアイテム)`, `ピボットテーブルのフィールド`, and every other word ExPivot paints.
  - They include the words of the ExGrid commands in the report's Context Menu, because ExGrid ships
    only English ones.
  - **The Consumer chooses them in one line, explicitly.** They never follow the culture on their
    own, because a screen whose language changed unasked is the surprise the family avoids.
  - A Consumer that needs another language replaces the words by id, as before.
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
  - *(2026-10-13, decided with the user: Value Field Settings lists Count as `個数`, as the
    Japanese edition does; the caption stays `データの個数 / {0}`.)* The readings of 2026-10-12
    (R3 (d)) read the edition's list as `合計, 個数, 平均, 最大, 最小, 積, 数値の個数, …`; ExPivot
    listed `データの個数`, the caption's word.

## The held answer is kept, and the layout is redone

**What is aggregated depends only on four things**: the data, the placed fields with their Hidden
Items, the fields in Values, and the parts those Value Fields ask for.

- Collapse, order, the form, subtotals, grand totals, Show Values As, formats and captions only lay
  the result out.
- ExPivot therefore keeps the answer it was given — the Leaf Aggregates — and lays it out again
  when one of these changes.
- Expanding an Item, sorting it, or changing a Value Field from Sum to Average asks no new question
  of the source, as long as the held answer carries the parts Average needs.

*Changed when decided:* **only the parts the Value Fields ask for are accumulated.** The proposal
accumulated every part for every field in Values: count, numbers, sum, extremes, product and the
running variance. Measured over columns, that full accumulator cost 2.6–3.3 times a sum-only loop
on CoreCLR, and 6–7 times in the browser. A change that needs a part the answer lacks asks again.

**A value cell is computed when it is first read.** ExGrid reads only the rows it paints, so a
report of many rows costs its rows, not its rows × its columns.

## Refined while building it

*(2026-10-01, when the engine was rebuilt on the Snapshot.)*

- **An exact sum is a 128-bit integer, not a 64-bit one.** "Money stays exact" above said a sum
  was a 64-bit integer at the column's scale, or a `decimal`. A leaf now sums each run of 64-bit
  values into a 128-bit integer, at the scale of the run, so slices written at different scales
  sum exactly. The finished sum is a `decimal` without trailing zeros. It falls back to Excel's
  `double` only when no `decimal` holds the sum exactly — when its digits, trailing zeros gone,
  need more than a `decimal`'s 96 bits — and past 128 bits it stays a `double`. A `decimal`
  rounded quietly is never the answer. How a source writes a Decimal is not part of the report
  either: `75.60` and `75.6` are one value, and every exact value is painted and copied as `75.6`
  ([ADR-0066](./0066-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md)).
- **The Order Key never keys `(blank)` or `#NUM!`.** They keep their places, last, as they do
  under every order. An Item the key leaves null still comes after the keyed ones.
- **Keys of two types are refused**, naming the field, an Item of each type and the two types. An
  `int` beside a `string` has no order the Consumer chose, and comparing their texts would be the
  order that looks right until `10` comes before `9`.
- **A date part is captioned as any declared field is**: by the caption it is given, or else by
  its name. The first build captioned it as Excel's automatic date grouping does, `Months (Trade
  date)`. Excel makes that name up because nobody named the field; here the Consumer declared it
  and named it.
- **A date part's labels are words with ids** (`date-year`, `date-quarter`, `date-month-1` to
  `date-month-12`), so a Consumer replaces them as it replaces any other word. The bundled
  Japanese words have the Japanese edition's: `2026年`, `第3四半期`, `9月`.
- **A record behind a cell carries a date part as its number**: `2026`, `3`, `9`. The label is the
  report's painting, and a record is data.

## The definitions are shared with the Selection Summary *(added 2026-10-05)*

[ADR-0130](./0130-the-selection-summary-is-asked-of-the-consumer-like-find.md) shows Excel's
status-bar figures over a grid's selection, and gives six of them the meaning of the Aggregations
of the same names. **The definitions of all eleven Aggregations — what each counts and includes,
and how a result is finished from its parts — move to `ExGrid.Data`**, so a pivot cell and the
status bar cannot disagree. Their meaning here is unchanged. The columnar accumulator stays in
`ExPivot.Engine` and produces the parts.

## A next report shares what did not change *(2026-10-06; taken 2026-10-08)*

[ADR-0153](./0153-reports-share-unchanged-computation-and-display-rows-own-no-report.md) computes a live
report from the last one. Published Report Versions stay immutable and share the structure a batch did
not change, and the display rows handed to ExGrid hold no report. The engine stays the reference: an
incremental result equals a fresh computation, which Layer 1 holds it to. *(The Claude Code track
decided the same direction with a narrower scope on 2026-10-07, as ADR-0161; comparing the two tracks
on 2026-10-08, the user took ADR-0153.)*

## Consequences

- **`ExPivot.Engine`'s tests pin every rule here** (layer 1), and name this ADR. Readings are marked
  in their test names until they are observed.
- **The reference implementation is the engine behind `PivotSource.From`, not the component.** Show
  Details, the Item lists of Filter…, and every Field List rule are the engine's functions. A server
  and the screen therefore cannot disagree about them.
- **Anything Excel does that ExPivot does not is listed in ADR-0059's table and never approximated.**
  That includes Σ Values at an outer position, collapsing one column Item, ranges of numbers, and
  Max of dates.
